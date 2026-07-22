// ============================================================
// DESK 42 — SaveSystem Unit Tests (Edit Mode)
// Tests use a temp directory to avoid polluting real saves.
// ============================================================

using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using Desk42.Core;

namespace Desk42.Tests.EditMode
{
    [TestFixture]
    public sealed class SaveSystemTests
    {
        private string _tempSaveDirectory;

        [SetUp]
        public void SetUp()
        {
            _tempSaveDirectory = Path.Combine(
                Path.GetTempPath(), $"Desk42_SaveTests_{Guid.NewGuid():N}");
            SaveSystem.SetSaveDirectoryOverrideForTests(_tempSaveDirectory);
        }

        [TearDown]
        public void TearDown()
        {
            SaveSystem.WipeAllSaveData();
            SaveSystem.ClearSaveDirectoryOverrideForTests();
            if (Directory.Exists(_tempSaveDirectory))
                Directory.Delete(_tempSaveDirectory, recursive: true);
        }

        // ── MetaProgressData Serialization ────────────────────

        [Test]
        public void SaveAndLoad_MetaProgressData_RoundTrips()
        {
            var meta = new MetaProgressData();
            meta.BankBalance = 999;
            meta.GlobalShiftNumber = 42;
            meta.UnlockMilestone(MilestoneID.FirstPromotion);
            meta.EmployeeHandbook.UnlockBenefit("health_insurance", 30);
            meta.RecordCardUsed("alien_variant_001", PunchCardType.ThreatAudit);
            meta.AddCounterTrait("alien_variant_001", "retained_counsel");
            meta.RefreshTimestamp();

            bool saved = SaveSystem.SaveMeta(meta);
            Assert.IsTrue(saved, "SaveMeta must return true on success.");

            var loaded = SaveSystem.LoadMeta();
            Assert.IsNotNull(loaded, "LoadMeta must return a non-null result.");
            Assert.AreEqual(999, loaded.BankBalance);
            Assert.AreEqual(42, loaded.GlobalShiftNumber);
            Assert.IsTrue(loaded.IsMilestoneUnlocked(MilestoneID.FirstPromotion));
            Assert.IsTrue(loaded.EmployeeHandbook.HasBenefit("health_insurance"));
            Assert.AreEqual(1,
                loaded.GetOrCreateProfile("alien_variant_001")
                    .GetCardUsage(PunchCardType.ThreatAudit));
            Assert.IsTrue(loaded.HasCounterTrait("alien_variant_001", "retained_counsel"));
        }

        [Test]
        public void LoadMeta_WhenNoFileExists_ReturnsDefaultInstance()
        {
            // Wipe save files so we can test fresh-load behaviour
            SaveSystem.WipeAllSaveData();

            var meta = SaveSystem.LoadMeta();
            Assert.IsNotNull(meta, "Must return a default MetaProgressData, not null.");
            Assert.AreEqual(0, meta.BankBalance);
            Assert.AreEqual(0, meta.GlobalShiftNumber);
        }

        // ── RunData Serialization ────────────────────────────

        [Test]
        public void SaveAndLoad_RunData_RoundTrips()
        {
            var run = new RunData
            {
                MasterSeed    = 12345,
                SeedCode      = "ABCDEF",
                ShiftNumber   = 3,
                ArchetypeId   = "gaslighter",
                Sanity        = 67.5f,
                SoulIntegrity = 42.1f,
                CorporateCredits = 150,
                CurrentPhase  = ShiftPhase.AfternoonBlock,
            };
            run.RefreshTimestamp();

            bool saved = SaveSystem.SaveRun(run);
            Assert.IsTrue(saved, "SaveRun must return true on success.");

            var loaded = SaveSystem.LoadRun();
            Assert.IsNotNull(loaded, "LoadRun must return non-null.");
            Assert.AreEqual(12345,       loaded.MasterSeed);
            Assert.AreEqual("gaslighter", loaded.ArchetypeId);
            Assert.AreEqual(67.5f,        loaded.Sanity,           0.001f);
            Assert.AreEqual(42.1f,        loaded.SoulIntegrity,    0.001f);
            Assert.AreEqual(150,          loaded.CorporateCredits);
            Assert.AreEqual(ShiftPhase.AfternoonBlock, loaded.CurrentPhase);
        }

        [Test]
        public void MigrateLegacyRun_PreservesTotalsWithoutInventingDispositionHistory()
        {
            const string json = @"{
              'SaveVersion': 1,
              'Stats': {
                'ClaimsProcessed': 7,
                'HumaneResolutions': 5,
                'BureaucraticResolutions': 2
              },
              'ResolvedClaims': [
                { 'ClaimId': 'legacy-claim', 'IsResolved': true, 'WasHumane': true }
              ]
            }";

            File.WriteAllText(Path.Combine(_tempSaveDirectory, "run.json"), json);
            var run = SaveSystem.LoadRun();

            Assert.AreEqual(SaveSystem.CurrentRunSaveVersion, run.SaveVersion);
            Assert.AreEqual(5, run.Stats.LegacyHumaneResolutions);
            Assert.AreEqual(2, run.Stats.LegacyBureaucraticResolutions);
            Assert.AreEqual(0, run.Stats.ApprovedClaims);
            Assert.AreEqual(0, run.Stats.DeniedClaims);
            Assert.AreEqual(0, run.Stats.LiquifiedClaims);
            Assert.AreEqual(ClaimResolutionKind.Unspecified,
                run.ResolvedClaims[0].ResolutionKind);

            Assert.IsTrue(SaveSystem.SaveRun(run));
            string saved = File.ReadAllText(Path.Combine(_tempSaveDirectory, "run.json"));
            StringAssert.DoesNotContain("\"HumaneResolutions\":", saved);
            StringAssert.DoesNotContain("\"BureaucraticResolutions\":", saved);
            StringAssert.DoesNotContain("\"WasHumane\":", saved);
        }

        [Test]
        public void MigrateLegacyMeta_PreservesTotalsAndStartsFactualBucketsClean()
        {
            const string json = @"{
              'SaveVersion': 2,
              'LifetimeStats': {
                'TotalClaimsProcessed': 11,
                'TotalHumaneResolutions': 8,
                'TotalBureaucraticResolutions': 3
              }
            }";

            File.WriteAllText(Path.Combine(_tempSaveDirectory, "meta.json"), json);
            var meta = SaveSystem.LoadMeta();

            Assert.AreEqual(SaveSystem.CurrentMetaSaveVersion, meta.SaveVersion);
            Assert.AreEqual(8, meta.LifetimeStats.LegacyTotalHumaneResolutions);
            Assert.AreEqual(3, meta.LifetimeStats.LegacyTotalBureaucraticResolutions);
            Assert.AreEqual(0, meta.LifetimeStats.TotalApprovedClaims);
            Assert.AreEqual(0, meta.LifetimeStats.TotalDeniedClaims);
            Assert.AreEqual(0, meta.LifetimeStats.TotalLiquifiedClaims);

            Assert.IsTrue(SaveSystem.SaveMeta(meta));
            string saved = File.ReadAllText(Path.Combine(_tempSaveDirectory, "meta.json"));
            StringAssert.DoesNotContain("\"TotalHumaneResolutions\":", saved);
            StringAssert.DoesNotContain("\"TotalBureaucraticResolutions\":", saved);
        }

        [Test]
        public void HasActiveRun_AfterSave_ReturnsTrue()
        {
            var run = new RunData { IsComplete = false, IsAbandoned = false };
            run.RefreshTimestamp();
            SaveSystem.SaveRun(run);

            Assert.IsTrue(SaveSystem.HasActiveRun());
        }

        [Test]
        public void HasActiveRun_AfterDelete_ReturnsFalse()
        {
            var run = new RunData { IsComplete = false };
            run.RefreshTimestamp();
            SaveSystem.SaveRun(run);
            SaveSystem.DeleteRun();

            Assert.IsFalse(SaveSystem.HasActiveRun());
        }

        [Test]
        public void HasActiveRun_CompletedRun_ReturnsFalse()
        {
            var run = new RunData { IsComplete = true };
            run.RefreshTimestamp();
            SaveSystem.SaveRun(run);

            Assert.IsFalse(SaveSystem.HasActiveRun());
        }

        // ── RumorMill Event Bus ───────────────────────────────

        [Test]
        public void RumorMill_Subscribe_ReceivesPublishedEvent()
        {
            bool received = false;
            var expected  = new SanityChangedEvent(100f, 75f);

            RumorMill.OnSanityChanged += evt =>
            {
                received = true;
                Assert.AreEqual(100f, evt.Previous, 0.001f);
                Assert.AreEqual(75f,  evt.Current,  0.001f);
            };

            RumorMill.Publish(expected);

            RumorMill.OnSanityChanged -= _ => { };
            RumorMill.ClearAllSubscriptions();

            Assert.IsTrue(received, "Subscriber must receive the published event.");
        }

        [Test]
        public void RumorMill_ClearSubscriptions_NoLongerFires()
        {
            int callCount = 0;
            RumorMill.OnSanityChanged += _ => callCount++;

            RumorMill.ClearAllSubscriptions();
            RumorMill.Publish(new SanityChangedEvent(100f, 50f));

            Assert.AreEqual(0, callCount,
                "After ClearAllSubscriptions, no events should fire.");
        }
    }
}
