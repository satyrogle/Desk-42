using System;
using Desk42.Core;
using Desk42.Encounter;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Desk42.Tests.EditMode
{
    /// <summary>
    /// Bucket 3 validation matrix for the Five-Shift proof spine.
    ///
    /// The branches are asserted SEPARATELY throughout — 5A, 5B and 5C are
    /// analytically distinct outcomes and must never be collapsed into one
    /// metric.
    /// </summary>
    public sealed class FiveShiftProofSpineTests
    {
        private static readonly JsonSerializerSettings Settings = new()
        {
            Formatting            = Formatting.Indented,
            NullValueHandling     = NullValueHandling.Include,
            DefaultValueHandling  = DefaultValueHandling.Include,
            ReferenceLoopHandling = ReferenceLoopHandling.Error,
            TypeNameHandling      = TypeNameHandling.None,
        };

        private static MetaProgressData RoundTrip(MetaProgressData meta)
            => JsonConvert.DeserializeObject<MetaProgressData>(
                JsonConvert.SerializeObject(meta, Settings), Settings);

        // ── Branch state derives from the persisted branch ───

        [Test]
        public void BranchA_NormalisedAddress_ActivatesTheDependentAction()
        {
            var s = EliasShift5Policy.ForBranch(EliasShift2Branch.NormalisedAddress);

            Assert.AreEqual(EliasRecordClassification.Registered18A, s.Classification);
            Assert.AreEqual(EliasDependentAction.Active, s.DependentAction);
            Assert.IsTrue(s.RecordIsValid, "5A's record is valid — that is why it acts.");
            Assert.AreEqual("M. VENN", s.SourceRecord);
        }

        [Test]
        public void BranchB_LegacyException_Retains18B_AndBlocksAutomatedAction()
        {
            var s = EliasShift5Policy.ForBranch(EliasShift2Branch.LegacyException);

            Assert.AreEqual(EliasRecordClassification.Legacy18B, s.Classification);
            Assert.AreEqual(EliasDependentAction.NotAuthorised, s.DependentAction);
            Assert.AreNotEqual(EliasDependentAction.Active, s.DependentAction,
                "5B must NOT receive the 5A downstream consequence.");
            Assert.IsFalse(s.RecordIsValid);
        }

        [Test]
        public void BranchC_PhysicalVerification_HoldsTheConsequence()
        {
            var s = EliasShift5Policy.ForBranch(EliasShift2Branch.PhysicalVerification);

            Assert.AreEqual(EliasRecordClassification.NotFinal, s.Classification);
            Assert.AreEqual(EliasDependentAction.Held, s.DependentAction);
            Assert.AreNotEqual(EliasDependentAction.Active, s.DependentAction,
                "5C must NOT receive the 5A downstream consequence.");
            Assert.IsFalse(s.RecordIsValid);
        }

        [Test]
        public void ThreeBranches_RemainIndependentlyIdentifiable()
        {
            var a = EliasShift5Policy.ForBranch(EliasShift2Branch.NormalisedAddress);
            var b = EliasShift5Policy.ForBranch(EliasShift2Branch.LegacyException);
            var c = EliasShift5Policy.ForBranch(EliasShift2Branch.PhysicalVerification);

            Assert.AreNotEqual(a.DependentAction, b.DependentAction);
            Assert.AreNotEqual(a.DependentAction, c.DependentAction);
            Assert.AreNotEqual(b.DependentAction, c.DependentAction);
            Assert.AreNotEqual(a.Classification, b.Classification);
            Assert.AreNotEqual(b.Classification, c.Classification);
        }

        // ── No clean out (5A primary causal test) ────────────

        [Test]
        public void Approve_In5A_DoesNotReverseTheRegistration()
        {
            var s = EliasShift5Policy.ForBranch(EliasShift2Branch.NormalisedAddress);

            var after = EliasShift5Policy.DependentActionAfter(
                s, ClaimResolutionKind.Approve);

            Assert.AreEqual(EliasDependentAction.Active, after,
                "Approving processes the current claim; it does not undo Shift 2.");
        }

        [Test]
        public void Deny_In5A_DoesNotReverseTheRegistration()
        {
            var s = EliasShift5Policy.ForBranch(EliasShift2Branch.NormalisedAddress);

            var after = EliasShift5Policy.DependentActionAfter(
                s, ClaimResolutionKind.Deny);

            Assert.AreEqual(EliasDependentAction.Active, after,
                "Denial refuses the claim but is not a reversal of the record.");
        }

        [Test]
        public void NoDisposition_ClearsTheDependentAction()
        {
            var s = EliasShift5Policy.ForBranch(EliasShift2Branch.NormalisedAddress);

            foreach (ClaimResolutionKind kind in Enum.GetValues(typeof(ClaimResolutionKind)))
            {
                Assert.AreEqual(EliasDependentAction.Active,
                    EliasShift5Policy.DependentActionAfter(s, kind),
                    $"No disposition may act as a hidden undo — '{kind}' did.");
            }
        }

        [Test]
        public void ReversalAuthority_IsNeverAvailableAtThisDesk()
        {
            foreach (EliasShift2Branch branch in Enum.GetValues(typeof(EliasShift2Branch)))
                Assert.IsFalse(EliasShift5Policy.ForBranch(branch)
                        .ReversalAvailableAtThisDesk,
                    $"Branch {branch} exposed a clean escape.");
        }

        // ── Resolution comes from persisted state, not name ──

        [Test]
        public void Shift5State_ResolvesFromPersistedProof_NotClaimantName()
        {
            var meta = new MetaProgressData();
            meta.EliasProof = EliasProofSessionState.Create("spine");
            meta.EliasProof.Shift2Branch = EliasShift2Branch.NormalisedAddress;

            var reloaded = RoundTrip(meta);
            var s = EliasShift5Policy.Resolve(reloaded.EliasProof);

            Assert.AreEqual(EliasDependentAction.Active, s.DependentAction,
                "Branch must survive reload and drive Shift 5 state.");
        }

        [Test]
        public void SaveReload_PreservesBranchAndCausalReference()
        {
            var meta = new MetaProgressData();
            meta.EliasProof = EliasProofSessionState.Create("causal");
            meta.EliasProof.Shift2Branch = EliasShift2Branch.PhysicalVerification;
            meta.EliasProof.Shift2ProcedureReceiptId = "receipt-ref-1";
            meta.EliasProof.Shift5LoadedClaimId = "elias_shift_5c_claim";

            var reloaded = RoundTrip(meta);

            Assert.AreEqual(EliasShift2Branch.PhysicalVerification,
                reloaded.EliasProof.Shift2Branch);
            Assert.AreEqual("receipt-ref-1", reloaded.EliasProof.Shift2ProcedureReceiptId);
            Assert.AreEqual("elias_shift_5c_claim", reloaded.EliasProof.Shift5LoadedClaimId);
            Assert.AreEqual(EliasDependentAction.Held,
                EliasShift5Policy.Resolve(reloaded.EliasProof).DependentAction);
        }

        [Test]
        public void UnestablishedBranch_ResolvesToNone_NotSilentlyTo5B()
        {
            var s = EliasShift5Policy.ForBranch(EliasShift2Branch.None);

            Assert.IsFalse(s.IsResolved);
            Assert.AreEqual(EliasDependentAction.None, s.DependentAction,
                "A lost causal chain must surface, not masquerade as a branch.");
        }

        [Test]
        public void Resolve_ThrowsOnNullState_RatherThanGuessing()
            => Assert.Throws<ArgumentNullException>(
                () => EliasShift5Policy.Resolve(null));

        // ── Control claimant isolation ───────────────────────

        [Test]
        public void ControlClaimant_IsNotEliasAndCarriesNoAppearanceKey()
        {
            var claim = ControlClaimantContent.BuildClaim();

            Assert.AreEqual("control_mara_kest", claim.ClientVariantId);
            Assert.AreNotEqual(EliasProofContent.CanonicalClaimantId, claim.ClientVariantId);
            Assert.IsNull(claim.AuthoredAppearanceKey,
                "An appearance key would let the control enter the proof machinery.");
            Assert.AreEqual("Mara Kest", claim.ClaimantName);
        }

        [Test]
        public void ControlClaimant_IsRejectedByEliasAppearanceRecording()
        {
            var host = new UnityEngine.GameObject("proof-control");
            try
            {
                var proof = host.AddComponent<EliasProofSessionController>();
                proof.BeginProofSession("control-isolation");

                // Mara Kest must not be recordable as an Elias appearance.
                Assert.Throws<InvalidOperationException>(() =>
                    proof.RecordAppearance(
                        ControlClaimantContent.StableClaimantId,
                        EliasProofContent.Shift2AppearanceKey));

                Assert.AreEqual(EliasShift2Branch.None, proof.State.Shift2Branch,
                    "The control claimant must not establish a branch.");
                Assert.IsEmpty(proof.State.RecordedAppearanceKeys);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void ControlClaimant_CannotCarryAnEliasAppearanceKey()
        {
            Assert.Throws<InvalidOperationException>(() =>
                ControlClaimantContent.AssertNotEliasIdentity(
                    ControlClaimantContent.StableClaimantId,
                    EliasProofContent.Shift1AppearanceKey));
        }

        [Test]
        public void ControlClaimant_HasNoClassificationMechanic()
        {
            var claim = ControlClaimantContent.BuildClaim();

            Assert.IsFalse(claim.IncidentText.Contains("18A"));
            Assert.IsFalse(claim.IncidentText.Contains("18B"),
                "The control must not be mechanically equivalent to Elias.");
            Assert.IsTrue(ControlClaimantContent.Anchor.Contains("ROUTE 7C"),
                "She still needs a memorable anchor to support false attribution.");
        }

        [Test]
        public void ControlClaimant_AppearsBetweenTheCausalEndpoints()
        {
            Assert.Greater(ControlClaimantContent.AppearanceShiftNumber, 2);
            Assert.Less(ControlClaimantContent.AppearanceShiftNumber, 5,
                "She must sit between the Shift 2 cause and the Shift 5 effect.");
        }

        [Test]
        public void PresentingTheControlClaimant_MutatesNoProofState()
        {
            var meta = new MetaProgressData();
            meta.EliasProof = EliasProofSessionState.Create("no-spawn-mutation");
            meta.EliasProof.Shift2Branch = EliasShift2Branch.NormalisedAddress;

            var run = new RunData { SeedCode = "CTRL", ShiftNumber = 3 };
            var claim = ControlClaimantContent.BuildClaim();

            EncounterCommitService.BeginEncounter(claim, run, meta);

            Assert.AreEqual(EliasShift2Branch.NormalisedAddress,
                meta.EliasProof.Shift2Branch, "Branch must be untouched.");
            Assert.IsEmpty(meta.EliasProof.RecordedAppearanceKeys);
            Assert.AreEqual(0,
                meta.GetTotalVisits(EliasProofContent.CanonicalClaimantId),
                "Presenting the control must not credit Elias with a visit.");
        }

        // ── Control claimant is LIVE in the Shift 3 queue ────

        private static System.Collections.Generic.List<ActiveClaimData> Queue(int n)
        {
            var list = new System.Collections.Generic.List<ActiveClaimData>();
            for (int i = 0; i < n; i++)
                list.Add(new ActiveClaimData
                {
                    ClaimId = $"CLM-{i:D5}",
                    ClientVariantId = $"filler_{i}",
                    ClientSpeciesId = "unregistered_alien",
                });
            return list;
        }

        [Test]
        public void ControlClaimant_IsScheduledIntoTheShift3Queue()
        {
            var queue = Queue(6);

            bool scheduled = ControlClaimantContent.TryScheduleControlClaimant(
                queue, ControlClaimantContent.AppearanceShiftNumber, out var claim);

            Assert.IsTrue(scheduled, "Mara Kest must actually appear in game.");
            Assert.IsNotNull(claim);
            Assert.AreEqual(ControlClaimantContent.StableClaimantId,
                queue[ControlClaimantContent.QueuePosition - 1].ClientVariantId);
        }

        [Test]
        public void ControlClaimant_IsNotScheduledOnCausalShifts()
        {
            foreach (int shift in new[] { 1, 2, 4, 5 })
            {
                var queue = Queue(6);
                Assert.IsFalse(
                    ControlClaimantContent.TryScheduleControlClaimant(
                        queue, shift, out _),
                    $"The control must not appear on causal shift {shift}.");
            }
        }

        [Test]
        public void ControlClaimant_AppearsExactlyOnce_NoCallback()
        {
            int appearances = 0;
            for (int shift = 1; shift <= 5; shift++)
            {
                var queue = Queue(6);
                if (ControlClaimantContent.TryScheduleControlClaimant(
                        queue, shift, out _))
                    appearances++;
            }

            Assert.AreEqual(1, appearances,
                "A reminder or callback would prompt recall and invalidate " +
                "the attribution measurement.");
        }

        [Test]
        public void ControlClaimantScheduling_TakesNoProofState()
        {
            // Structural guarantee: the scheduling entry point has no
            // EliasProofSessionState parameter, so it cannot mutate the branch.
            var method = typeof(ControlClaimantContent).GetMethod(
                nameof(ControlClaimantContent.TryScheduleControlClaimant));

            Assert.IsNotNull(method);
            foreach (var p in method.GetParameters())
            {
                Assert.AreNotEqual(typeof(EliasProofSessionState), p.ParameterType,
                    "Control scheduling must not receive proof state.");
                Assert.AreNotEqual(typeof(EliasProofContent), p.ParameterType,
                    "Control scheduling must not receive Elias content.");
            }
        }

        // ── Aftermath reachability (audit lock) ──────────────

        [Test]
        public void EveryScheduledAftermathId_HasAuthoredCopy()
        {
            // Aftermath claims ARE queued during the Shift 5 proof path, so a
            // missing id would surface as blank participant-facing content.
            foreach (string id in new[]
                     {
                         "elias_aftermath_5a_1", "elias_aftermath_5a_2",
                         "elias_aftermath_5b_1",
                         "elias_aftermath_5c_1", "elias_aftermath_5c_2",
                     })
            {
                EliasAftermathPolicy.GetClaimCopy(
                    id, out string name, out string text);

                Assert.IsNotEmpty(name, $"'{id}' has no claimant name.");
                Assert.IsNotEmpty(text, $"'{id}' has no incident text.");
            }
        }

        // ── Presentation is not a proof mutation ─────────────

        [Test]
        public void PresentingElias_DoesNotMutateBranchOrCompleteAVisit()
        {
            var meta = new MetaProgressData();
            meta.EliasProof = EliasProofSessionState.Create("spawn-safety");

            var run = new RunData { SeedCode = "SPAWN", ShiftNumber = 2 };
            var claim = new ActiveClaimData
            {
                ClaimId = "elias_shift_2_claim",
                ClientVariantId = EliasProofContent.CanonicalClaimantId,
                ClientSpeciesId = "moth_accountant",
                AuthoredAppearanceKey = EliasProofContent.Shift2AppearanceKey,
            };

            EncounterCommitService.BeginEncounter(claim, run, meta);

            Assert.AreEqual(EliasShift2Branch.None, meta.EliasProof.Shift2Branch);
            Assert.AreEqual(0,
                meta.GetTotalVisits(EliasProofContent.CanonicalClaimantId));
        }
    }
}
