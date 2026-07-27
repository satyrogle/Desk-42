using System.Collections.Generic;
using Desk42.Core;
using Desk42.Encounter;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Desk42.Tests.EditMode
{
    /// <summary>
    /// Bucket C delta 1 — Mara scheduling independence, claimant identity
    /// semantics, and the historical-disposition query seam.
    ///
    /// Tests exercise the production query seam rather than inspecting private
    /// collections.
    /// </summary>
    public sealed class BucketCDelta1Tests
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

        private static List<ActiveClaimData> Queue(int n)
        {
            var list = new List<ActiveClaimData>();
            for (int i = 0; i < n; i++)
                list.Add(new ActiveClaimData
                {
                    ClaimId = $"CLM-{i:D5}",
                    ClientVariantId = $"filler_{i}",
                    ClientSpeciesId = "unregistered_alien",
                });
            return list;
        }

        private static ActiveClaimData Claim(string claimId, string variant,
            string species = "moth_accountant")
            => new()
            {
                ClaimId = claimId,
                ClientVariantId = variant,
                ClientSpeciesId = species,
            };

        // ── 1. Mara scheduling independence ──────────────────

        [Test]
        public void Mara_IsEligible_WhenEliasProofNeverStarted()
        {
            var queue = Queue(6);
            Assert.IsTrue(ControlClaimantContent.TryScheduleControlClaimant(
                queue, ControlClaimantContent.AppearanceShiftNumber, out _),
                "The control must not depend on Elias proof ever having started.");
        }

        [Test]
        public void Mara_IsEligible_RegardlessOfEliasProofLifecycleState()
        {
            // active, inactive, completed/archived, and never-started are all
            // represented; none of them may change the scheduling decision.
            var states = new[]
            {
                null,
                new EliasProofSessionState(),                       // inactive
                EliasProofSessionState.Create("live"),              // active
                EliasProofSessionState.Create("completed"),         // archived-shaped
            };

            foreach (var proofState in states)
            {
                var queue = Queue(6);
                bool scheduled = ControlClaimantContent.TryScheduleControlClaimant(
                    queue, ControlClaimantContent.AppearanceShiftNumber, out _);

                Assert.IsTrue(scheduled,
                    "Elias proof lifecycle must not gate control scheduling " +
                    $"(state: {proofState?.ProofSessionId ?? "none"}).");
            }
        }

        [Test]
        public void MaraScheduler_RequiresNoProofStateOrContent()
        {
            // Structural: the scheduling entry point cannot receive proof types,
            // so no caller can reintroduce the dependency through it.
            var method = typeof(ControlClaimantContent).GetMethod(
                nameof(ControlClaimantContent.TryScheduleControlClaimant));

            Assert.IsNotNull(method);
            foreach (var p in method.GetParameters())
            {
                Assert.AreNotEqual(typeof(EliasProofSessionState), p.ParameterType);
                Assert.AreNotEqual(typeof(EliasProofContent), p.ParameterType);
            }
        }

        [Test]
        public void MaraSchedulingCallSite_IsNotGatedByEliasProof()
        {
            // The defect was at the call site, not in the scheduler. Assert the
            // repair directly: the control call must not sit inside the
            // Elias-proof block in ShiftManager.
            string[] lines = System.IO.File.ReadAllLines(
                "Assets/_Project/Scripts/Core/ShiftManager.cs");

            int eliasGuardIndent = -1;
            int controlCallIndent = -1;

            foreach (string line in lines)
            {
                int indent = line.Length - line.TrimStart().Length;

                if (eliasGuardIndent < 0 && line.Contains("EliasProof?.HasActiveSession"))
                    eliasGuardIndent = indent;

                if (controlCallIndent < 0 && line.Contains("TryScheduleControlClaimant("))
                    controlCallIndent = indent;
            }

            Assert.Greater(eliasGuardIndent, -1, "Elias proof guard not found.");
            Assert.Greater(controlCallIndent, -1, "Control scheduling call not found.");

            // Nested inside the guard would be deeper than the guard itself.
            Assert.LessOrEqual(controlCallIndent, eliasGuardIndent,
                "Control scheduling is nested inside the Elias-proof guard. Mara's " +
                "eligibility must not depend on Elias proof state.");
        }

        [Test]
        public void Mara_IsNotScheduledOutsideHerAuthoredWindow()
        {
            foreach (int shift in new[] { 1, 2, 4, 5 })
            {
                var queue = Queue(6);
                Assert.IsFalse(ControlClaimantContent.TryScheduleControlClaimant(
                    queue, shift, out _), $"Unintended Mara schedule on shift {shift}.");
            }
        }

        [Test]
        public void Mara_DuplicateScheduling_DoesNotInsertTwice()
        {
            var queue = Queue(6);
            int shift = ControlClaimantContent.AppearanceShiftNumber;

            ControlClaimantContent.TryScheduleControlClaimant(queue, shift, out _);
            ControlClaimantContent.TryScheduleControlClaimant(queue, shift, out _);

            int count = 0;
            foreach (var c in queue)
                if (c.ClientVariantId == ControlClaimantContent.StableClaimantId) count++;

            Assert.AreEqual(1, count, "The same intended appearance must not duplicate.");
        }

        // ── 2. Claimant identity semantics ───────────────────

        [Test]
        public void ClaimantIdentity_IsStableAcrossEncounters_WhileEncounterIdsDiffer()
        {
            var meta = new MetaProgressData();
            var run  = new RunData { SeedCode = "IDENT", ShiftNumber = 1 };

            var first  = Claim("CLM-A", "elias_venn");
            var second = Claim("CLM-B", "elias_venn");

            EncounterCommitService.BeginEncounter(first, run, meta);
            run.ShiftNumber = 2;
            EncounterCommitService.BeginEncounter(second, run, meta);

            Assert.AreEqual(first.ClientVariantId, second.ClientVariantId,
                "Claimant identity must persist across appearances.");
            Assert.AreNotEqual(first.EncounterId, second.EncounterId,
                "Each appearance is a distinct encounter.");
        }

        [Test]
        public void ClaimantIdentity_SurvivesSaveLoad()
        {
            var meta = new MetaProgressData();
            var run  = new RunData { SeedCode = "IDENT2", ShiftNumber = 1 };
            var claim = Claim("CLM-C", "control_mara_kest");

            EncounterCommitService.BeginEncounter(claim, run, meta);
            meta.Encounters.MarkCompleted(claim.EncounterId, ClaimResolutionKind.Approve, 1L);

            var reloaded = RoundTrip(meta);
            var record = reloaded.Encounters.Find(claim.EncounterId);

            Assert.AreEqual("control_mara_kest", record.ClientVariantId);
            Assert.AreEqual(claim.EncounterId, record.EncounterId);
        }

        [Test]
        public void EncounterId_IsNotClaimantIdentity()
        {
            var meta = new MetaProgressData();
            var run  = new RunData { SeedCode = "IDENT3", ShiftNumber = 1 };
            var a = Claim("CLM-D", "elias_venn");
            var b = Claim("CLM-E", "elias_venn");

            EncounterCommitService.BeginEncounter(a, run, meta);
            EncounterCommitService.BeginEncounter(b, run, meta);

            // One claimant, two encounters — the identity must not collapse.
            Assert.AreEqual(2, meta.GetTotalPresentations("elias_venn"));
            Assert.AreNotEqual(a.EncounterId, b.EncounterId);
        }

        // ── 3. Historical disposition query ──────────────────

        [Test]
        public void UnknownClaimant_ReturnsNoHistory_AndDoesNotThrow()
        {
            var meta = new MetaProgressData();

            Assert.IsEmpty(meta.Encounters.CommittedDispositionsFor("never_seen"));
            Assert.IsEmpty(meta.Encounters.CommittedDispositionsFor(null));
            Assert.IsFalse(meta.Encounters.HasCommittedHistory("never_seen"));
            Assert.AreEqual(ClaimResolutionKind.Unspecified,
                meta.Encounters.LatestDispositionFor("never_seen"));
        }

        [Test]
        public void SingleCommittedEncounter_ReturnsThatResult()
        {
            var meta = new MetaProgressData();
            var run  = new RunData { SeedCode = "H1", ShiftNumber = 1 };
            var claim = Claim("CLM-F", "moth_accountant_100");

            EncounterCommitService.BeginEncounter(claim, run, meta);
            meta.Encounters.MarkCompleted(claim.EncounterId, ClaimResolutionKind.Deny, 5L);

            var history = meta.Encounters.CommittedDispositionsFor("moth_accountant_100");

            Assert.AreEqual(1, history.Count);
            Assert.AreEqual(ClaimResolutionKind.Deny, history[0].Outcome);
            Assert.AreEqual(claim.EncounterId, history[0].EncounterId);
        }

        [Test]
        public void MultipleEncounters_ReturnAllResultsInHistoryOrder()
        {
            var meta = new MetaProgressData();
            var run  = new RunData { SeedCode = "H2", ShiftNumber = 1 };

            var kinds = new[]
            {
                ClaimResolutionKind.Approve,
                ClaimResolutionKind.Deny,
                ClaimResolutionKind.Liquify,
            };

            var ids = new List<string>();
            for (int i = 0; i < kinds.Length; i++)
            {
                var c = Claim($"CLM-G{i}", "gel_anomaly_200");
                EncounterCommitService.BeginEncounter(c, run, meta);
                meta.Encounters.MarkCompleted(c.EncounterId, kinds[i], i);
                ids.Add(c.EncounterId);
            }

            var history = meta.Encounters.CommittedDispositionsFor("gel_anomaly_200");

            Assert.AreEqual(3, history.Count);
            for (int i = 0; i < kinds.Length; i++)
            {
                Assert.AreEqual(kinds[i], history[i].Outcome, $"order wrong at {i}");
                Assert.AreEqual(ids[i], history[i].EncounterId);
            }
            Assert.AreEqual(ClaimResolutionKind.Liquify,
                meta.Encounters.LatestDispositionFor("gel_anomaly_200"));
        }

        [Test]
        public void InterleavedClaimant_DoesNotContaminateResults()
        {
            var meta = new MetaProgressData();
            var run  = new RunData { SeedCode = "H3", ShiftNumber = 1 };

            var a1 = Claim("CLM-H1", "claimant_a");
            var b1 = Claim("CLM-H2", "claimant_b");
            var a2 = Claim("CLM-H3", "claimant_a");

            foreach (var (c, k) in new[]
                     {
                         (a1, ClaimResolutionKind.Approve),
                         (b1, ClaimResolutionKind.Liquify),
                         (a2, ClaimResolutionKind.Deny),
                     })
            {
                EncounterCommitService.BeginEncounter(c, run, meta);
                meta.Encounters.MarkCompleted(c.EncounterId, k, 1L);
            }

            var a = meta.Encounters.CommittedDispositionsFor("claimant_a");
            var b = meta.Encounters.CommittedDispositionsFor("claimant_b");

            Assert.AreEqual(2, a.Count);
            Assert.AreEqual(ClaimResolutionKind.Approve, a[0].Outcome);
            Assert.AreEqual(ClaimResolutionKind.Deny, a[1].Outcome);
            Assert.AreEqual(1, b.Count);
            Assert.AreEqual(ClaimResolutionKind.Liquify, b[0].Outcome);
        }

        [Test]
        public void InterruptedEncounter_IsNotAFinalDisposition()
        {
            var meta = new MetaProgressData();
            var run  = new RunData { SeedCode = "H4", ShiftNumber = 1 };
            var claim = Claim("CLM-I", "void_proxy_300");

            EncounterCommitService.BeginEncounter(claim, run, meta);   // never completed

            Assert.IsEmpty(meta.Encounters.CommittedDispositionsFor("void_proxy_300"),
                "A presentation must never masquerade as a committed disposition.");
            Assert.AreEqual(1, meta.GetTotalPresentations("void_proxy_300"));
            Assert.AreEqual(0, meta.GetTotalVisits("void_proxy_300"));
        }

        [Test]
        public void DuplicateCommit_DoesNotCreateADuplicateDisposition()
        {
            var meta = new MetaProgressData();
            var run  = new RunData { SeedCode = "H5", ShiftNumber = 1 };
            var claim = Claim("CLM-J", "unregistered_alien_400");

            EncounterCommitService.BeginEncounter(claim, run, meta);
            meta.Encounters.MarkCompleted(claim.EncounterId, ClaimResolutionKind.Approve, 1L);
            meta.Encounters.MarkCompleted(claim.EncounterId, ClaimResolutionKind.Deny, 2L);

            var history = meta.Encounters.CommittedDispositionsFor("unregistered_alien_400");

            Assert.AreEqual(1, history.Count);
            Assert.AreEqual(ClaimResolutionKind.Approve, history[0].Outcome,
                "The original committed outcome must stand.");
        }

        [Test]
        public void History_SurvivesSaveLoad_Unchanged()
        {
            var meta = new MetaProgressData();
            var run  = new RunData { SeedCode = "H6", ShiftNumber = 1 };

            var c1 = Claim("CLM-K1", "elias_venn");
            var c2 = Claim("CLM-K2", "elias_venn");
            EncounterCommitService.BeginEncounter(c1, run, meta);
            meta.Encounters.MarkCompleted(c1.EncounterId, ClaimResolutionKind.Approve, 1L);
            EncounterCommitService.BeginEncounter(c2, run, meta);
            meta.Encounters.MarkCompleted(c2.EncounterId, ClaimResolutionKind.Deny, 2L);

            var before = meta.Encounters.CommittedDispositionsFor("elias_venn");
            var after  = RoundTrip(meta).Encounters.CommittedDispositionsFor("elias_venn");

            Assert.AreEqual(before.Count, after.Count);
            for (int i = 0; i < before.Count; i++)
            {
                Assert.AreEqual(before[i].EncounterId, after[i].EncounterId);
                Assert.AreEqual(before[i].Outcome, after[i].Outcome);
            }
        }

        [Test]
        public void Reappearance_RetainsEarlierResults_WithADistinctEncounterId()
        {
            var meta = new MetaProgressData();
            var run  = new RunData { SeedCode = "H7", ShiftNumber = 1 };

            var first = Claim("CLM-L1", "elias_venn");
            EncounterCommitService.BeginEncounter(first, run, meta);
            meta.Encounters.MarkCompleted(first.EncounterId, ClaimResolutionKind.Approve, 1L);

            run.ShiftNumber = 5;
            var later = Claim("CLM-L2", "elias_venn");
            EncounterCommitService.BeginEncounter(later, run, meta);

            var history = meta.Encounters.CommittedDispositionsFor("elias_venn");

            Assert.AreEqual(1, history.Count, "Only the committed one counts.");
            Assert.AreEqual(first.EncounterId, history[0].EncounterId);
            Assert.AreNotEqual(first.EncounterId, later.EncounterId);
        }

        [Test]
        public void MaraHistory_IsQueryableWithoutAnyEliasProofState()
        {
            var meta = new MetaProgressData();   // no proof session at all
            var run  = new RunData { SeedCode = "H8", ShiftNumber = 3 };
            var claim = ControlClaimantContent.BuildClaim();

            EncounterCommitService.BeginEncounter(claim, run, meta);
            meta.Encounters.MarkCompleted(claim.EncounterId, ClaimResolutionKind.Approve, 1L);

            var history = meta.Encounters.CommittedDispositionsFor(
                ControlClaimantContent.StableClaimantId);

            Assert.AreEqual(1, history.Count,
                "Control history must not require Elias proof state.");
            Assert.IsFalse(meta.EliasProof.IsActive);
        }
    }
}
