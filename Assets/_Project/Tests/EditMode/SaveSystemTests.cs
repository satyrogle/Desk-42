// ============================================================
// DESK 42 — SaveSystem Unit Tests (Edit Mode)
// Tests use a temp directory to avoid polluting real saves.
// ============================================================

using System.IO;
using NUnit.Framework;
using UnityEngine;
using Desk42.Core;

namespace Desk42.Tests.EditMode
{
    [TestFixture]
    public sealed class SaveSystemTests
    {
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
