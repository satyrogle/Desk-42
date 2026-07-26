using System;
using System.Collections.Generic;
using UnityEngine;

namespace Desk42.Core
{
    /// <summary>
    /// Owns the validation spine across five fresh RunData instances. The
    /// controller is a child of the DontDestroyOnLoad GameManager.
    ///
    /// Bucket 2: this is now a runtime FAÇADE. The authoritative record lives
    /// on MetaProgressData.EliasProof and is written to meta.json, so the
    /// Shift 2 -> Shift 5 causal chain survives scene changes, run boundaries
    /// and an application restart. It was previously session-only, which meant
    /// a restart silently destroyed the chain.
    ///
    /// Ending a session archives into MetaProgressData.CompletedProofSessions
    /// rather than erasing, so causal evidence outlives the session.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EliasProofSessionController : MonoBehaviour
    {
        public const string ContinuityHoldFailureReason =
            "RecurringClaimantContinuityHold";
        public const string ProcedureRequiredFailureReason =
            "EliasProcedureRequired";

        // Fallback state used only when no MetaProgressData is reachable
        // (EditMode fixtures that construct this controller standalone).
        // In production the authoritative record lives on MetaProgressData
        // so it survives scene changes, run boundaries AND app restart.
        [SerializeField]
        private EliasProofSessionState _detachedState = new();

        private static MetaProgressData Meta => GameManager.Instance?.Meta;

        /// <summary>
        /// Runtime façade over the persisted proof record. Reads
        /// MetaProgressData.EliasProof when a meta profile exists, otherwise a
        /// detached instance so headless fixtures still work.
        /// </summary>
        public EliasProofSessionState State
        {
            get
            {
                var meta = Meta;
                if (meta != null)
                    return meta.EliasProof ??= new EliasProofSessionState();
                return _detachedState ??= new EliasProofSessionState();
            }
        }

        /// <summary>True when the authoritative record is the persisted one.</summary>
        public bool IsPersisted => Meta != null;

        private void SetState(EliasProofSessionState next)
        {
            var meta = Meta;
            if (meta != null) meta.EliasProof = next;
            else              _detachedState  = next;
        }

        public bool HasActiveSession => State.IsActive;

        /// <summary>
        /// Captures prior visits, records the authored appearance once, then
        /// exposes both the BSM input and the human-facing visit number.
        /// Replaying the same appearance after reconstruction returns the same
        /// transaction without advancing the session.
        /// </summary>
        public EliasVisitTransaction RecordAppearance(
            string stableClaimantId, string appearanceKey)
        {
            if (!HasActiveSession)
                throw new InvalidOperationException(
                    "An active Elias proof session is required.");
            if (!string.Equals(stableClaimantId,
                    EliasProofContent.CanonicalClaimantId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Elias must use stable claimant ID " +
                    $"'{EliasProofContent.CanonicalClaimantId}', received " +
                    $"'{stableClaimantId}'.");
            }
            if (!EliasProofContent.TryGetExpectedPriorVisits(
                    appearanceKey, out int expectedPriorVisits))
            {
                throw new ArgumentException(
                    $"Unknown Elias appearance key '{appearanceKey}'.",
                    nameof(appearanceKey));
            }

            bool alreadyRecorded =
                State.RecordedAppearanceKeys.Contains(appearanceKey);
            if (!alreadyRecorded)
            {
                AssertPrecedingAppearances(expectedPriorVisits);
                State.RecordedAppearanceKeys.Add(appearanceKey);
            }

            var transaction = new EliasVisitTransaction(
                State.ProofSessionId,
                appearanceKey,
                stableClaimantId,
                expectedPriorVisits,
                !alreadyRecorded);
            Debug.Log(
                $"[EliasProof] appearance={appearanceKey} " +
                $"priorVisits={transaction.PriorVisits} " +
                $"currentVisit={transaction.CurrentVisitNumber} " +
                $"recordedNow={transaction.WasNewlyRecorded}.");
            return transaction;
        }

        /// <summary>
        /// Uses the same proof-specific validator as execution and never
        /// changes resources, branch state, quota, or claim statistics.
        /// </summary>
        public ProjectedEliasProcedure PreviewProcedure(
            RunStateController run,
            string stableClaimantId,
            string appearanceKey,
            EliasProcedureActionId actionId)
            => EliasProcedurePolicy.Preview(
                State, run, stableClaimantId, appearanceKey, actionId);

        /// <summary>
        /// Applies one nonterminal authored procedure. Resource mutation,
        /// branch write, decision record, and the factual event occur in one
        /// synchronous transaction before the claim can be disposed.
        /// </summary>
        public bool TryApplyProcedure(
            RunStateController run,
            string stableClaimantId,
            string appearanceKey,
            EliasProcedureActionId actionId,
            out AppliedEliasProcedure applied,
            out EliasProcedureFailureReason failureReason)
        {
            ProjectedEliasProcedure projection = PreviewProcedure(
                run, stableClaimantId, appearanceKey, actionId);
            if (!projection.IsAvailable)
            {
                applied = default;
                failureReason = projection.FailureReason;
                return false;
            }

            int creditsBefore = run.Credits;
            float sanityBefore = run.Sanity;
            float soulBefore = run.SoulIntegrity;
            float streakBefore = run.ComboMultiplier;

            if (projection.RequestedCreditsDelta != 0)
                run.ModifyCredits(projection.RequestedCreditsDelta);
            if (!Mathf.Approximately(
                    projection.RequestedSanityDelta, 0f))
            {
                run.ModifySanity(projection.RequestedSanityDelta);
            }
            if (!Mathf.Approximately(
                    projection.RequestedSoulIntegrityDelta, 0f))
            {
                run.ModifySoulIntegrity(
                    projection.RequestedSoulIntegrityDelta);
            }
            if (!Mathf.Approximately(
                    projection.RequestedComplianceStreakDelta, 0f))
            {
                run.ApplyComplianceStreakDelta(
                    projection.RequestedComplianceStreakDelta);
            }

            if (projection.BranchToWrite != EliasShift2Branch.None)
                State.Shift2Branch = projection.BranchToWrite;

            State.AppliedProcedureAppearanceKeys.Add(appearanceKey);
            RecordProcedureDecision(projection);

            applied = new AppliedEliasProcedure(
                State.ProofSessionId,
                appearanceKey,
                stableClaimantId,
                actionId,
                State.Shift2Branch,
                projection.PriorVisits,
                projection.CurrentVisitNumber,
                run.Credits - creditsBefore,
                run.Sanity - sanityBefore,
                run.SoulIntegrity - soulBefore,
                run.ComboMultiplier - streakBefore,
                projection.AddressBefore,
                projection.AddressAfter,
                projection.MiriamRegistrationReference,
                projection.ReceiptId,
                auditRiskDelta: 0f);

            if (string.Equals(appearanceKey,
                    EliasProofContent.Shift2AppearanceKey,
                    StringComparison.Ordinal))
            {
                State.Shift2ComplianceStreakDelta =
                    applied.ComplianceStreakDelta;
                State.Shift2AuditRiskDelta = applied.AuditRiskDelta;
            }

            failureReason = EliasProcedureFailureReason.None;
            RumorMill.Publish(new EliasProcedureAppliedEvent(applied));
            Debug.Log(
                $"[EliasProof] procedure={actionId} " +
                $"appearance={appearanceKey} branch={State.Shift2Branch} " +
                $"receipt={projection.ReceiptId} " +
                $"streakDelta={applied.ComplianceStreakDelta:+0.##;-0.##;0}.");
            return true;
        }

        /// <summary>
        /// Produces the one factual instrumentation record for this proof run.
        /// All fields are read from canonical proof state; UI text is never
        /// parsed and mutable economy values are not used to infer the branch.
        /// </summary>
        public EliasProofRunRecord CaptureInstrumentation()
            => EliasProofRunRecord.Capture(State);

        /// <summary>
        /// Validates the terminal claim disposition before normal resources or
        /// quota are applied. Liquify remains visible but cannot execute.
        /// </summary>
        public bool TryValidateDisposition(
            string stableClaimantId,
            string appearanceKey,
            ClaimResolutionKind kind,
            out string failureReason)
        {
            failureReason = null;
            if (!HasActiveSession)
            {
                failureReason = "NoActiveProofSession";
                return false;
            }
            if (!string.Equals(stableClaimantId,
                    EliasProofContent.CanonicalClaimantId,
                    StringComparison.Ordinal))
            {
                failureReason = "InvalidClaimantIdentity";
                return false;
            }
            if (!EliasProofContent.TryGetExpectedPriorVisits(
                    appearanceKey, out _)
                || !State.RecordedAppearanceKeys.Contains(appearanceKey))
            {
                failureReason = "AppearanceNotRecorded";
                return false;
            }
            if (kind == ClaimResolutionKind.Liquify)
            {
                failureReason = ContinuityHoldFailureReason;
                return false;
            }
            if (kind != ClaimResolutionKind.Approve
                && kind != ClaimResolutionKind.Deny)
            {
                failureReason = "UnsupportedDisposition";
                return false;
            }
            bool requiresProcedure =
                string.Equals(appearanceKey,
                    EliasProofContent.Shift2AppearanceKey,
                    StringComparison.Ordinal)
                || string.Equals(appearanceKey,
                    EliasProofContent.Shift5AppearanceKey,
                    StringComparison.Ordinal);
            if (requiresProcedure
                && !State.AppliedProcedureAppearanceKeys.Contains(
                    appearanceKey))
            {
                failureReason = ProcedureRequiredFailureReason;
                return false;
            }
            return true;
        }

        /// <summary>
        /// Records the later ordinary disposition without changing the branch
        /// selected by the preceding procedure.
        /// </summary>
        public void RecordDisposition(
            string appearanceKey,
            AppliedClaimResolution resolution)
        {
            if (!TryValidateDisposition(
                    resolution.ClientVariantId,
                    appearanceKey,
                    resolution.Kind,
                    out string failureReason))
            {
                throw new InvalidOperationException(
                    $"Cannot record Elias disposition: {failureReason}.");
            }

            if (string.Equals(appearanceKey,
                    EliasProofContent.Shift1AppearanceKey,
                    StringComparison.Ordinal))
            {
                EliasShift1Disposition next =
                    resolution.Kind == ClaimResolutionKind.Approve
                        ? EliasShift1Disposition.Approved
                        : EliasShift1Disposition.Denied;
                if (State.Shift1Disposition != EliasShift1Disposition.None
                    && State.Shift1Disposition != next)
                {
                    throw new InvalidOperationException(
                        "Elias Shift 1 disposition cannot be overwritten.");
                }
                State.Shift1Disposition = next;
            }
            else if (string.Equals(appearanceKey,
                         EliasProofContent.Shift2AppearanceKey,
                         StringComparison.Ordinal))
            {
                if (State.Shift2FinalDisposition
                        != ClaimResolutionKind.Unspecified
                    && State.Shift2FinalDisposition != resolution.Kind)
                {
                    throw new InvalidOperationException(
                        "Elias Shift 2 final disposition cannot be overwritten.");
                }
                State.Shift2FinalDisposition = resolution.Kind;
            }
            else if (string.Equals(appearanceKey,
                         EliasProofContent.Shift5AppearanceKey,
                         StringComparison.Ordinal))
            {
                if (State.Shift5FinalDisposition
                        != ClaimResolutionKind.Unspecified
                    && State.Shift5FinalDisposition != resolution.Kind)
                {
                    throw new InvalidOperationException(
                        "Elias Shift 5 disposition cannot be overwritten.");
                }
                State.Shift5FinalDisposition = resolution.Kind;
            }
        }

        public EliasAftermathModifierState ActivateShift5Aftermath(
            EliasProofContent content)
        {
            if (!HasActiveSession)
            {
                throw new InvalidOperationException(
                    "An active Elias proof session is required.");
            }
            if (State.Shift2Branch == EliasShift2Branch.None)
            {
                throw new InvalidOperationException(
                    "Shift 5 aftermath requires an Elias branch.");
            }
            if (State.Shift5FinalDisposition
                == ClaimResolutionKind.Unspecified)
            {
                throw new InvalidOperationException(
                    "Shift 5 aftermath cannot activate before Elias resolves.");
            }
            if (!string.IsNullOrWhiteSpace(
                    State.ActiveAftermathModifier?.ModifierId))
            {
                throw new InvalidOperationException(
                    "Elias aftermath cannot be activated more than once.");
            }

            EliasAftermathDefinition definition =
                EliasAftermathPolicy.ForBranch(
                    content, State.Shift2Branch);
            State.ActiveAftermathModifier =
                EliasAftermathModifierState.Create(definition);
            Debug.Log(
                $"[EliasProof] aftermath={definition.ModifierId} " +
                $"pending={string.Join(",", definition.ClaimIds)}.");
            return State.ActiveAftermathModifier;
        }

        public bool TryApplyAftermathToClaim(
            string claimId, out AppliedEliasAftermath applied)
        {
            applied = default;
            EliasAftermathModifierState modifier =
                State.ActiveAftermathModifier;
            if (modifier?.IsActive != true
                || string.IsNullOrWhiteSpace(claimId)
                || !modifier.PendingClaimIds.Contains(claimId))
            {
                return false;
            }

            if (modifier.AppliedClaimIds.Contains(claimId))
            {
                throw new InvalidOperationException(
                    $"Aftermath already applied to '{claimId}'.");
            }
            modifier.PendingClaimIds.Remove(claimId);
            modifier.AppliedClaimIds.Add(claimId);

            int total = modifier.AppliedClaimIds.Count
                + modifier.PendingClaimIds.Count;
            applied = new AppliedEliasAftermath(
                State.ProofSessionId,
                State.Shift2Branch,
                modifier.ModifierId,
                claimId,
                modifier.AppliedClaimIds.Count,
                total,
                modifier.PendingClaimIds.Count);
            RumorMill.Publish(new EliasAftermathAppliedEvent(applied));
            Debug.Log(
                $"[EliasProof] aftermath={applied.ModifierId} " +
                $"claim={claimId} applied={applied.AppliedCount}/" +
                $"{applied.TotalClaimCount} remaining=" +
                $"{applied.RemainingClaimCount}.");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (modifier.IsExpired)
                Debug.Log(CaptureInstrumentation().ToString());
#endif
            return true;
        }

        /// <summary>
        /// Starts from a clean record even if a previous proof was active.
        /// Supplying an ID keeps automated routes deterministic; runtime proof
        /// sessions receive a new opaque ID.
        /// </summary>
        public EliasProofSessionState BeginProofSession(
            string proofSessionId = null)
        {
            string id = string.IsNullOrWhiteSpace(proofSessionId)
                ? Guid.NewGuid().ToString("N")
                : proofSessionId.Trim();

            var next = EliasProofSessionState.Create(id);
            SetState(next);
            Debug.Log($"[EliasProof] Session started: {id} " +
                      $"(persisted={IsPersisted}).");
            return next;
        }

        /// <summary>
        /// Closes the live proof session and ARCHIVES it.
        ///
        /// Bucket 2: this must not destroy causal evidence. The record is moved
        /// into MetaProgressData.CompletedProofSessions so branch, receipts and
        /// dispositions remain available for attribution and post-test
        /// inspection; only the LIVE slot is cleared, which is what stops
        /// aftermath and appearance keys leaking into the next session.
        ///
        /// EncounterHistory is untouched by this boundary — committed visits
        /// are independent evidence and are never archived away.
        /// </summary>
        public void EndProofSession()
        {
            var current = State;
            if (!current.IsActive)
            {
                SetState(new EliasProofSessionState());
                return;
            }

            var meta = Meta;
            if (meta != null)
            {
                meta.CompletedProofSessions ??= new List<EliasProofSessionState>();

                bool alreadyArchived = false;
                for (int i = 0; i < meta.CompletedProofSessions.Count; i++)
                {
                    if (string.Equals(meta.CompletedProofSessions[i]?.ProofSessionId,
                            current.ProofSessionId, StringComparison.Ordinal))
                    {
                        alreadyArchived = true;
                        break;
                    }
                }

                if (!alreadyArchived)
                    meta.CompletedProofSessions.Add(current);
            }

            Debug.Log($"[EliasProof] Session ended and archived: " +
                      $"{current.ProofSessionId}.");

            SetState(new EliasProofSessionState());
        }

        /// <summary>
        /// Production boundary for ending the proof: the authored spine is over
        /// once Shift 5 has a terminal disposition. Safe to call repeatedly and
        /// safe to call when no proof is running.
        /// </summary>
        public bool TryEndCompletedSession()
        {
            var current = State;
            if (!current.IsActive) return false;
            if (current.Shift5FinalDisposition == ClaimResolutionKind.Unspecified)
                return false;

            EndProofSession();
            return true;
        }

        /// <summary>Archived proof sessions, newest last. Evidence, not live state.</summary>
        public IReadOnlyList<EliasProofSessionState> ArchivedSessions
            => (IReadOnlyList<EliasProofSessionState>)Meta?.CompletedProofSessions
               ?? Array.Empty<EliasProofSessionState>();

        private void Awake()
        {
            _detachedState ??= new EliasProofSessionState();
        }

        private void AssertPrecedingAppearances(int expectedPriorVisits)
        {
            bool shift1Recorded = State.RecordedAppearanceKeys.Contains(
                EliasProofContent.Shift1AppearanceKey);
            bool shift2Recorded = State.RecordedAppearanceKeys.Contains(
                EliasProofContent.Shift2AppearanceKey);
            bool valid = expectedPriorVisits switch
            {
                0 => !shift1Recorded && !shift2Recorded,
                1 => shift1Recorded && !shift2Recorded,
                2 => shift1Recorded && shift2Recorded,
                _ => false,
            };
            if (!valid)
            {
                throw new InvalidOperationException(
                    $"Elias appearance order is invalid for priorVisits=" +
                    $"{expectedPriorVisits}. Recorded keys: " +
                    $"{string.Join(", ", State.RecordedAppearanceKeys)}.");
            }
        }

        private void RecordProcedureDecision(
            ProjectedEliasProcedure projection)
        {
            if (string.Equals(projection.AppearanceKey,
                    EliasProofContent.Shift2AppearanceKey,
                    StringComparison.Ordinal))
            {
                State.Shift2ProcedureAction = projection.ActionId;
                State.Shift2ProcedureReceiptId = projection.ReceiptId;
            }
            else if (string.Equals(projection.AppearanceKey,
                         EliasProofContent.Shift5AppearanceKey,
                         StringComparison.Ordinal))
            {
                State.Shift5ProcedureAction = projection.ActionId;
                State.Shift5ProcedureReceiptId = projection.ReceiptId;
            }
        }
    }
}
