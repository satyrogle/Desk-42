using System.Collections.Generic;
using Desk42.Core;
using Desk42.Encounter;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Desk42.Tests.EditMode
{
    /// <summary>
    /// Bucket 2 — the proof state must survive save/reload and an application
    /// restart, and ending a session must not destroy causal evidence.
    ///
    /// Round-trips through the real Newtonsoft settings rather than copying
    /// objects, so a missing [JsonProperty] fails these tests.
    /// </summary>
    public sealed class ProofPersistenceTests
    {
        // Mirrors SaveSystem._settings exactly (SaveSystem.cs:71-78). If these
        // drift, these tests stop proving anything about the real save path.
        private static readonly JsonSerializerSettings Settings = new()
        {
            Formatting            = Formatting.Indented,
            NullValueHandling     = NullValueHandling.Include,
            DefaultValueHandling  = DefaultValueHandling.Include,
            ReferenceLoopHandling = ReferenceLoopHandling.Error,
            TypeNameHandling      = TypeNameHandling.None,
        };

        private static MetaProgressData RoundTrip(MetaProgressData meta)
        {
            string json = JsonConvert.SerializeObject(meta, Settings);
            return JsonConvert.DeserializeObject<MetaProgressData>(json, Settings);
        }

        private static MetaProgressData MetaWithShift2Branch()
        {
            var meta = new MetaProgressData();
            meta.EliasProof = EliasProofSessionState.Create("proof-persist");
            meta.EliasProof.Shift1Disposition   = EliasShift1Disposition.Approved;
            meta.EliasProof.Shift2Branch        = EliasShift2Branch.LegacyException;
            meta.EliasProof.Shift2ProcedureReceiptId = "receipt-18a";
            meta.EliasProof.Shift2FinalDisposition   = ClaimResolutionKind.Approve;
            meta.EliasProof.RecordedAppearanceKeys.Add(EliasProofContent.Shift1AppearanceKey);
            meta.EliasProof.RecordedAppearanceKeys.Add(EliasProofContent.Shift2AppearanceKey);
            meta.EliasProof.AppliedProcedureAppearanceKeys.Add(
                EliasProofContent.Shift2AppearanceKey);
            return meta;
        }

        // ── Shift 2 state survives save -> reload ────────────

        [Test]
        public void Shift2State_SurvivesSaveReload()
        {
            var reloaded = RoundTrip(MetaWithShift2Branch());

            Assert.AreEqual("proof-persist", reloaded.EliasProof.ProofSessionId);
            Assert.AreEqual(EliasShift1Disposition.Approved,
                reloaded.EliasProof.Shift1Disposition);
            Assert.AreEqual(EliasShift2Branch.LegacyException,
                reloaded.EliasProof.Shift2Branch,
                "The Shift 2 causal branch must survive an application restart.");
            Assert.AreEqual("receipt-18a", reloaded.EliasProof.Shift2ProcedureReceiptId);
            Assert.AreEqual(ClaimResolutionKind.Approve,
                reloaded.EliasProof.Shift2FinalDisposition);
        }

        [Test]
        public void AppearanceAndProcedureLedgers_SurviveSaveReload()
        {
            var reloaded = RoundTrip(MetaWithShift2Branch());

            Assert.IsTrue(reloaded.EliasProof.RecordedAppearanceKeys.Contains(
                EliasProofContent.Shift1AppearanceKey));
            Assert.IsTrue(reloaded.EliasProof.RecordedAppearanceKeys.Contains(
                EliasProofContent.Shift2AppearanceKey));
            Assert.IsTrue(reloaded.EliasProof.AppliedProcedureAppearanceKeys.Contains(
                EliasProofContent.Shift2AppearanceKey),
                "Procedure idempotency must survive reload or Shift 2 could reapply.");
        }

        [Test]
        public void ProofSessionIsActive_AfterReload()
        {
            var reloaded = RoundTrip(MetaWithShift2Branch());
            Assert.IsTrue(reloaded.EliasProof.IsActive);
        }

        // ── Shift 5 resolves to the original causal encounter ───

        [Test]
        public void Shift5Consequence_ResolvesToOriginalCausalBranch_AfterReload()
        {
            var meta = MetaWithShift2Branch();
            meta.EliasProof.Shift5LoadedClaimId = "elias_shift_5b_claim";

            var reloaded = RoundTrip(meta);

            // The Shift 5 claim variant is selected by the Shift 2 branch. If
            // either failed to persist, attribution breaks.
            Assert.AreEqual(EliasShift2Branch.LegacyException,
                reloaded.EliasProof.Shift2Branch);
            Assert.AreEqual("elias_shift_5b_claim",
                reloaded.EliasProof.Shift5LoadedClaimId);
        }

        [Test]
        public void Attribution_IsTiedToEncounterIdentity_NotClaimantName()
        {
            var meta = new MetaProgressData();
            var claim = new ActiveClaimData
            {
                ClaimId = "elias_shift_2_claim",
                ClientVariantId = EliasProofContent.CanonicalClaimantId,
                ClientSpeciesId = "human",
                AuthoredAppearanceKey = EliasProofContent.Shift2AppearanceKey,
            };
            var run = new RunData { SeedCode = "SEEDXX", ShiftNumber = 2 };

            EncounterCommitService.BeginEncounter(claim, run, meta);
            meta.Encounters.MarkCompleted(claim.EncounterId,
                ClaimResolutionKind.Approve, 99L);

            var reloaded = RoundTrip(meta);
            var record = reloaded.Encounters.Find(claim.EncounterId);

            Assert.IsNotNull(record, "Encounter identity must survive reload.");
            Assert.AreEqual(EliasProofContent.Shift2AppearanceKey,
                record.AuthoredAppearanceKey,
                "Attribution rides the appearance key + encounter id, not the name.");
            Assert.IsTrue(reloaded.Encounters.HasCompletedAppearance(
                EliasProofContent.Shift2AppearanceKey));
        }

        // ── EncounterId stability across reload ──────────────

        [Test]
        public void Reload_PreservesTheSameEncounterId()
        {
            var meta = new MetaProgressData();
            var run  = new RunData { SeedCode = "SEED77", ShiftNumber = 2 };
            var claim = new ActiveClaimData
            {
                ClaimId = "CLM-55555",
                ClientVariantId = "moth_accountant_301",
                ClientSpeciesId = "moth_accountant",
            };
            run.ActiveClaim = claim;

            EncounterCommitService.BeginEncounter(claim, run, meta);
            string before = claim.EncounterId;

            string runJson = JsonConvert.SerializeObject(run, Settings);
            var reloadedRun = JsonConvert.DeserializeObject<RunData>(runJson, Settings);
            var reloadedMeta = RoundTrip(meta);

            Assert.AreEqual(before, reloadedRun.ActiveClaim.EncounterId,
                "EncounterId must survive reload or a phantom visit is created.");

            // Re-presenting the resumed claim must not add a second record.
            EncounterCommitService.BeginEncounter(
                reloadedRun.ActiveClaim, reloadedRun, reloadedMeta);

            Assert.AreEqual(1, reloadedMeta.GetTotalPresentations("moth_accountant_301"));
            Assert.AreEqual(0, reloadedMeta.GetTotalVisits("moth_accountant_301"));
        }

        [Test]
        public void ActiveIncompleteEncounter_ResumesWithoutCountingAsVisit()
        {
            var meta = new MetaProgressData();
            var run  = new RunData { SeedCode = "SEED88", ShiftNumber = 1 };
            var claim = new ActiveClaimData
            {
                ClaimId = "CLM-1",
                ClientVariantId = "gel_anomaly_777",
                ClientSpeciesId = "gel_anomaly",
            };
            run.ActiveClaim = claim;

            EncounterCommitService.BeginEncounter(claim, run, meta);
            var reloadedMeta = RoundTrip(meta);

            Assert.AreEqual(EncounterStatus.Active,
                reloadedMeta.Encounters.StatusOf(claim.EncounterId, claim.EncounterId));
            Assert.AreEqual(0, reloadedMeta.GetTotalVisits("gel_anomaly_777"),
                "An in-progress encounter is never a completed visit.");
        }

        [Test]
        public void DuplicateCommit_RemainsIdempotentAfterReload()
        {
            var meta = new MetaProgressData();
            var run  = new RunData { SeedCode = "SEED99", ShiftNumber = 1 };
            var claim = new ActiveClaimData
            {
                ClaimId = "CLM-2",
                ClientVariantId = "void_proxy_501",
                ClientSpeciesId = "void_proxy",
            };

            EncounterCommitService.BeginEncounter(claim, run, meta);
            meta.Encounters.MarkCompleted(claim.EncounterId, ClaimResolutionKind.Approve, 1L);

            var reloaded = RoundTrip(meta);

            Assert.IsTrue(reloaded.Encounters.IsCompleted(claim.EncounterId),
                "Idempotency must be enforceable after reload.");
            Assert.IsFalse(
                reloaded.Encounters.MarkCompleted(claim.EncounterId,
                    ClaimResolutionKind.Deny, 2L),
                "A reloaded completed encounter must still reject a second commit.");
            Assert.AreEqual(1, reloaded.GetTotalVisits("void_proxy_501"));
        }

        [Test]
        public void CompletedVisit_IncrementsExactlyOnce()
        {
            var meta = new MetaProgressData();
            var run  = new RunData { SeedCode = "SEEDAA", ShiftNumber = 1 };
            var claim = new ActiveClaimData
            {
                ClaimId = "CLM-3",
                ClientVariantId = "unregistered_alien_222",
                ClientSpeciesId = "unregistered_alien",
            };

            EncounterCommitService.BeginEncounter(claim, run, meta);

            // Eleven attempts — the historical Shift.unity listener count.
            for (int i = 0; i < 11; i++)
                meta.Encounters.MarkCompleted(claim.EncounterId,
                    ClaimResolutionKind.Approve, i);

            Assert.AreEqual(1, meta.GetTotalVisits("unregistered_alien_222"));
            Assert.AreEqual(1, meta.GetTotalPresentations("unregistered_alien_222"));
        }

        // ── Derived lifecycle status ─────────────────────────

        [Test]
        public void InterruptedEncounter_IsDerived_NotStored()
        {
            var meta = new MetaProgressData();
            var run  = new RunData { SeedCode = "SEEDBB", ShiftNumber = 1 };

            var abandoned = new ActiveClaimData
            {
                ClaimId = "CLM-A", ClientVariantId = "v_1", ClientSpeciesId = "s",
            };
            var current = new ActiveClaimData
            {
                ClaimId = "CLM-B", ClientVariantId = "v_2", ClientSpeciesId = "s",
            };

            EncounterCommitService.BeginEncounter(abandoned, run, meta);
            EncounterCommitService.BeginEncounter(current, run, meta);

            string active = current.EncounterId;

            Assert.AreEqual(EncounterStatus.Interrupted,
                meta.Encounters.StatusOf(abandoned.EncounterId, active));
            Assert.AreEqual(EncounterStatus.Active,
                meta.Encounters.StatusOf(current.EncounterId, active));
            Assert.AreEqual(EncounterStatus.Unknown,
                meta.Encounters.StatusOf("ENC-NOPE", active));

            var interrupted = meta.Encounters.Interrupted(active);
            Assert.AreEqual(1, interrupted.Count);
            Assert.AreEqual(abandoned.EncounterId, interrupted[0].EncounterId);
        }

        [Test]
        public void CompletedStatus_IsIndependentOfWhatIsActive()
        {
            var meta = new MetaProgressData();
            var run  = new RunData { SeedCode = "SEEDCC", ShiftNumber = 1 };
            var claim = new ActiveClaimData
            {
                ClaimId = "CLM-C", ClientVariantId = "v_3", ClientSpeciesId = "s",
            };

            EncounterCommitService.BeginEncounter(claim, run, meta);
            meta.Encounters.MarkCompleted(claim.EncounterId, ClaimResolutionKind.Deny, 1L);

            Assert.AreEqual(EncounterStatus.Completed,
                meta.Encounters.StatusOf(claim.EncounterId, claim.EncounterId));
            Assert.AreEqual(EncounterStatus.Completed,
                meta.Encounters.StatusOf(claim.EncounterId, "ENC-SOMETHING-ELSE"));
        }

        // ── Ending a session must not destroy evidence ───────

        [Test]
        public void ArchivedSession_SurvivesSaveReload_AsEvidence()
        {
            var meta = MetaWithShift2Branch();
            meta.EliasProof.Shift5FinalDisposition = ClaimResolutionKind.Deny;

            // Simulates EndProofSession's archive step.
            meta.CompletedProofSessions.Add(meta.EliasProof);
            meta.EliasProof = new EliasProofSessionState();

            var reloaded = RoundTrip(meta);

            Assert.IsFalse(reloaded.EliasProof.IsActive,
                "The live slot must be clear so aftermath cannot leak forward.");
            Assert.AreEqual(1, reloaded.CompletedProofSessions.Count);

            var archived = reloaded.CompletedProofSessions[0];
            Assert.AreEqual("proof-persist", archived.ProofSessionId);
            Assert.AreEqual(EliasShift2Branch.LegacyException, archived.Shift2Branch,
                "Ending a session must not destroy the causal branch.");
            Assert.AreEqual(ClaimResolutionKind.Deny, archived.Shift5FinalDisposition);
        }

        [Test]
        public void EndingASession_DoesNotTouchEncounterHistory()
        {
            var meta = MetaWithShift2Branch();
            var run  = new RunData { SeedCode = "SEEDDD", ShiftNumber = 2 };
            var claim = new ActiveClaimData
            {
                ClaimId = "elias_shift_2_claim",
                ClientVariantId = EliasProofContent.CanonicalClaimantId,
                ClientSpeciesId = "human",
                AuthoredAppearanceKey = EliasProofContent.Shift2AppearanceKey,
            };

            EncounterCommitService.BeginEncounter(claim, run, meta);
            meta.Encounters.MarkCompleted(claim.EncounterId,
                ClaimResolutionKind.Approve, 1L);

            // Archive + clear the live slot.
            meta.CompletedProofSessions.Add(meta.EliasProof);
            meta.EliasProof = new EliasProofSessionState();

            Assert.AreEqual(1,
                meta.GetTotalVisits(EliasProofContent.CanonicalClaimantId),
                "Committed visits are independent evidence and must survive session end.");
            Assert.IsTrue(meta.Encounters.HasCompletedAppearance(
                EliasProofContent.Shift2AppearanceKey));
        }

        [Test]
        public void FreshMeta_HasEmptyProofState_AndDeserializesFromLegacyJson()
        {
            // A meta.json written before Bucket 2 has neither field.
            const string legacy = "{\"GlobalShiftNumber\":3,\"TutorialCompleted\":true}";
            var meta = JsonConvert.DeserializeObject<MetaProgressData>(legacy, Settings);

            Assert.IsNotNull(meta.Encounters, "Additive field must default, not null.");
            Assert.IsNotNull(meta.EliasProof);
            Assert.IsNotNull(meta.CompletedProofSessions);
            Assert.IsFalse(meta.EliasProof.IsActive);
            Assert.AreEqual(0, meta.Encounters.Count);
        }
    }
}
