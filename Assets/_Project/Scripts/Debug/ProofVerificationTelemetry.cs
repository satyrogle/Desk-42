// ============================================================
// DESK 42 — Proof verification telemetry (PASSIVE OBSERVER)
//
// Emits objective evidence for the human-driven Shift 1 -> 5 proof run.
//
// STRICTLY PASSIVE. This layer subscribes to the existing RumorMill bus
// and reads authoritative state. It contains NO call that advances
// gameplay, chooses a card, clicks a button, selects a procedure,
// resolves a claim, spawns or reorders claims, touches EliasProof,
// EncounterHistory, RunData or MetaProgressData, alters timeScale, or
// forces Sanity. Every write it performs goes to its own evidence file.
//
// It required ZERO production changes: every fact it records is already
// published on the bus, and receipt ordering is recomputed with the pure
// EliasProcedureReceiptSequence.Build rather than by observing the
// presenter.
//
// Proof-state fingerprints are JSON serialisations of the live proof
// object, hashed. Serialising does not mutate, so the before/after pair
// around Mara Kest is evidence, not interference.
//
// Output: <persistentDataPath>/ProofEvidence/proof-run-<session>.jsonl
// One JSON object per line, stable field names, ordered by emission.
// ============================================================

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;
using Desk42.Core;

namespace Desk42.Debugging
{
    [DisallowMultipleComponent]
    public sealed class ProofVerificationTelemetry : MonoBehaviour
    {
        public const string EvidenceFolderName = "ProofEvidence";

        private static readonly JsonSerializerSettings FingerprintSettings = new()
        {
            Formatting            = Formatting.None,
            NullValueHandling     = NullValueHandling.Include,
            DefaultValueHandling  = DefaultValueHandling.Include,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            TypeNameHandling      = TypeNameHandling.None,
        };

        private string _evidencePath;
        private string _pendingProofFingerprint;
        private string _pendingProofClaimId;

        /// <summary>
        /// Self-attaches in editor and development builds so the evidence run
        /// needs no scene edit and no production file referencing this type —
        /// passivity stays structural. Creating an observer GameObject does not
        /// touch gameplay state.
        /// </summary>
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoAttach()
        {
            if (FindObjectOfType<ProofVerificationTelemetry>() != null) return;

            var host = new GameObject("[ProofVerificationTelemetry]");
            host.AddComponent<ProofVerificationTelemetry>();
            DontDestroyOnLoad(host);
        }
#endif

        // ── Lifecycle ────────────────────────────────────────

        private void OnEnable()
        {
            RumorMill.OnShiftLifecycle        += HandleShiftLifecycle;
            RumorMill.OnClaimQueued           += HandleClaimQueued;
            RumorMill.OnClaimResolved         += HandleClaimResolved;
            RumorMill.OnEliasProcedureApplied += HandleProcedureApplied;
            RumorMill.OnEliasAftermathApplied += HandleAftermathApplied;
        }

        private void OnDisable()
        {
            RumorMill.OnShiftLifecycle        -= HandleShiftLifecycle;
            RumorMill.OnClaimQueued           -= HandleClaimQueued;
            RumorMill.OnClaimResolved         -= HandleClaimResolved;
            RumorMill.OnEliasProcedureApplied -= HandleProcedureApplied;
            RumorMill.OnEliasAftermathApplied -= HandleAftermathApplied;
        }

        // ── Evidence file ────────────────────────────────────

        /// <summary>Directory the evidence file is written to.</summary>
        public static string EvidenceDirectory
            => Path.Combine(Application.persistentDataPath, EvidenceFolderName);

        /// <summary>Path of the active evidence file, once a session starts.</summary>
        public string EvidencePath => _evidencePath;

        private void EnsureEvidenceFile(string sessionId)
        {
            if (!string.IsNullOrEmpty(_evidencePath)) return;

            try
            {
                Directory.CreateDirectory(EvidenceDirectory);
                string safe = string.IsNullOrWhiteSpace(sessionId) ? "unknown" : sessionId;
                foreach (char c in Path.GetInvalidFileNameChars())
                    safe = safe.Replace(c, '_');

                _evidencePath = Path.Combine(
                    EvidenceDirectory, $"proof-run-{safe}.jsonl");

                UnityEngine.Debug.Log(
                    $"[ProofTelemetry] Evidence file: {_evidencePath}");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning(
                    $"[ProofTelemetry] Could not open evidence file: {ex.Message}");
            }
        }

        /// <summary>
        /// Appends one JSONL record. Failures are swallowed: telemetry must
        /// never be able to break or alter the run it is observing.
        /// </summary>
        private void Emit(string eventName, Dictionary<string, object> fields)
        {
            fields ??= new Dictionary<string, object>();
            fields["event"] = eventName;
            fields["t"]     = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            fields["frame"] = Time.frameCount;

            string line;
            try { line = JsonConvert.SerializeObject(fields, FingerprintSettings); }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning(
                    $"[ProofTelemetry] Serialise failed for '{eventName}': {ex.Message}");
                return;
            }

            if (!string.IsNullOrEmpty(_evidencePath))
            {
                try { File.AppendAllText(_evidencePath, line + Environment.NewLine, Encoding.UTF8); }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogWarning(
                        $"[ProofTelemetry] Write failed: {ex.Message}");
                }
            }

            UnityEngine.Debug.Log($"[ProofTelemetry] {eventName} {line}");
        }

        // ── Read-only state access ───────────────────────────

        private static EliasProofSessionState ProofState
            => GameManager.Instance?.EliasProof?.State;

        /// <summary>
        /// Stable fingerprint of the live proof state. Serialisation is a pure
        /// read; this is how the Mara before/after pair proves non-interference.
        /// </summary>
        public static string FingerprintProofState(EliasProofSessionState state)
        {
            if (state == null) return "null";

            try
            {
                string json = JsonConvert.SerializeObject(state, FingerprintSettings);
                unchecked
                {
                    uint hash = 2166136261u;
                    foreach (char c in json) { hash ^= c; hash *= 16777619u; }
                    return $"{hash:x8}:{json.Length}";
                }
            }
            catch { return "unserialisable"; }
        }

        private static Dictionary<string, object> ProofSnapshot(EliasProofSessionState s)
            => s == null
                ? new Dictionary<string, object> { ["present"] = false }
                : new Dictionary<string, object>
                {
                    ["present"]              = true,
                    ["proofSessionId"]       = s.ProofSessionId,
                    ["isActive"]             = s.IsActive,
                    ["shift1Disposition"]    = s.Shift1Disposition.ToString(),
                    ["shift2Branch"]         = s.Shift2Branch.ToString(),
                    ["shift2Action"]         = s.Shift2ProcedureAction.ToString(),
                    ["shift2ReceiptId"]      = s.Shift2ProcedureReceiptId,
                    ["shift2Disposition"]    = s.Shift2FinalDisposition.ToString(),
                    ["shift5LoadedClaimId"]  = s.Shift5LoadedClaimId,
                    ["shift5Disposition"]    = s.Shift5FinalDisposition.ToString(),
                    ["recordedAppearances"]  = new List<string>(s.RecordedAppearanceKeys),
                    ["appliedProcedures"]    = new List<string>(s.AppliedProcedureAppearanceKeys),
                    ["fingerprint"]          = FingerprintProofState(s),
                };

        private static bool IsElias(string variantId)
            => string.Equals(variantId, EliasProofContent.CanonicalClaimantId,
                StringComparison.Ordinal);

        private static bool IsControl(string variantId)
            => ControlClaimantContent.IsControlClaimant(variantId);

        private static string RoleOf(string variantId)
            => IsElias(variantId) ? "ELIAS"
             : IsControl(variantId) ? "MARA_CONTROL"
             : "ordinary";

        // ── Handlers ─────────────────────────────────────────

        private void HandleShiftLifecycle(ShiftLifecycleEvent e)
        {
            var state = ProofState;
            var meta  = GameManager.Instance?.Meta;

            if (e.IsStart) EnsureEvidenceFile(state?.ProofSessionId);

            Emit(e.IsStart ? "shift_start" : "shift_end", new Dictionary<string, object>
            {
                ["shift"]                  = e.ShiftNumber,
                ["phase"]                  = e.Phase.ToString(),
                ["seed"]                   = e.Seed,
                ["proof"]                  = ProofSnapshot(state),
                ["completedProofSessions"] = meta?.CompletedProofSessions?.Count ?? 0,
                ["runInstanceId"]          = GameManager.Instance?.Run?.RawData?.RunInstanceId,
            });
        }

        private void HandleClaimQueued(ClaimQueuedEvent e)
        {
            var claim = e.Claim;
            if (claim == null) return;

            var meta = GameManager.Instance?.Meta;
            var run  = GameManager.Instance?.Run;

            // Fingerprint BEFORE this encounter commits. Paired with the
            // fingerprint at resolution, this is what demonstrates that the
            // control claimant left Elias causal state untouched.
            _pendingProofFingerprint = FingerprintProofState(ProofState);
            _pendingProofClaimId     = claim.ClaimId;

            Emit("encounter_presented", new Dictionary<string, object>
            {
                ["shift"]              = run?.ShiftNumber ?? 0,
                ["encounterId"]        = claim.EncounterId,
                ["claimId"]            = claim.ClaimId,
                ["claimantStableId"]   = claim.ClientVariantId,
                ["claimantName"]       = claim.ClaimantName,
                ["role"]               = RoleOf(claim.ClientVariantId),
                ["appearanceKey"]      = claim.AuthoredAppearanceKey,
                ["routeAnchor"]        = IsControl(claim.ClientVariantId)
                                            ? ControlClaimantContent.Anchor : null,
                ["visitsBefore"]       = meta?.GetTotalVisits(claim.ClientVariantId) ?? 0,
                ["proofFingerprintBefore"] = _pendingProofFingerprint,
            });
        }

        private void HandleClaimResolved(ClaimResolvedEvent e)
        {
            var meta  = GameManager.Instance?.Meta;
            var state = ProofState;
            string after = FingerprintProofState(state);

            var fields = new Dictionary<string, object>
            {
                ["claimId"]                = e.ClaimId,
                ["claimantStableId"]       = e.ClientVariantId,
                ["role"]                   = RoleOf(e.ClientVariantId),
                ["disposition"]            = e.Kind.ToString(),
                ["creditsDelta"]           = e.CreditsDelta,
                ["visitsAfter"]            = meta?.GetTotalVisits(e.ClientVariantId) ?? 0,
                ["proofFingerprintBefore"] = string.Equals(
                    _pendingProofClaimId, e.ClaimId, StringComparison.Ordinal)
                        ? _pendingProofFingerprint : null,
                ["proofFingerprintAfter"]  = after,
                ["proofUnchanged"]         = string.Equals(
                    _pendingProofFingerprint, after, StringComparison.Ordinal),
                ["proof"]                  = ProofSnapshot(state),
            };

            // Shift 5 causal readout, resolved from the persisted branch.
            if (IsElias(e.ClientVariantId) && state != null
                && state.Shift2Branch != EliasShift2Branch.None)
            {
                var s5 = EliasShift5Policy.ForBranch(state.Shift2Branch);
                fields["shift5"] = new Dictionary<string, object>
                {
                    ["restoredShift2Branch"]      = state.Shift2Branch.ToString(),
                    ["causalReceiptId"]           = state.Shift2ProcedureReceiptId,
                    ["classification"]            = s5.Classification.ToString(),
                    ["sourceRecord"]              = s5.SourceRecord,
                    ["recordIsValid"]             = s5.RecordIsValid,
                    ["dependentActionBefore"]     = s5.DependentAction.ToString(),
                    ["dependentActionAfter"]      =
                        EliasShift5Policy.DependentActionAfter(s5, e.Kind).ToString(),
                    ["reversalAvailableAtDesk"]   = s5.ReversalAvailableAtThisDesk,
                };
            }

            Emit("encounter_committed", fields);
        }

        private void HandleProcedureApplied(EliasProcedureAppliedEvent e)
        {
            var result = e.Result;
            var state  = ProofState;

            // Receipt beat ordering, recomputed with the pure builder rather
            // than observed from the presenter. This is what proves the
            // M. VENN memory anchor precedes the Compliance Streak beat.
            var beats = new List<Dictionary<string, object>>();
            int anchorIndex = -1, streakIndex = -1;
            try
            {
                var built = UI.EliasProcedureReceiptSequence.Build(result);
                for (int i = 0; i < built.Length; i++)
                {
                    string text = built[i].Text ?? string.Empty;
                    beats.Add(new Dictionary<string, object>
                    {
                        ["index"] = i,
                        ["kind"]  = built[i].Kind.ToString(),
                        ["text"]  = text,
                    });
                    if (anchorIndex < 0 && text.Contains("REGISTERED 18A")) anchorIndex = i;
                    if (streakIndex < 0 && text.Contains("COMPLIANCE STREAK")) streakIndex = i;
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning(
                    $"[ProofTelemetry] Receipt rebuild failed: {ex.Message}");
            }

            Emit("elias_procedure_applied", new Dictionary<string, object>
            {
                ["appearanceKey"]      = result.AppearanceKey,
                ["actionId"]           = result.ActionId.ToString(),
                ["branchWritten"]      = result.ResultingBranch.ToString(),
                ["receiptId"]          = result.ReceiptId,
                ["addressBefore"]      = result.AddressBefore,
                ["addressAfter"]       = result.AddressAfter,
                ["registrationRef"]    = result.MiriamRegistrationReference,
                ["complianceStreakDelta"] = result.ComplianceStreakDelta,
                ["receiptBeats"]       = beats,
                ["anchorBeatIndex"]    = anchorIndex,
                ["streakBeatIndex"]    = streakIndex,
                ["anchorPrecedesStreak"] =
                    anchorIndex >= 0 && (streakIndex < 0 || anchorIndex < streakIndex),
                ["proof"]              = ProofSnapshot(state),
            });
        }

        private void HandleAftermathApplied(EliasAftermathAppliedEvent e)
            => Emit("elias_aftermath_applied", new Dictionary<string, object>
            {
                ["proof"] = ProofSnapshot(ProofState),
            });
    }
}
