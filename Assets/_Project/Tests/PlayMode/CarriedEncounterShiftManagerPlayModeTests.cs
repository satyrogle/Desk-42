using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Desk42.Claims;
using Desk42.Core;
using Desk42.Encounter;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Desk42.Tests.PlayMode
{
    public sealed class CarriedEncounterShiftManagerPlayModeTests
    {
        private const BindingFlags InstancePrivate =
            BindingFlags.Instance | BindingFlags.NonPublic;
        private const BindingFlags StaticPrivate =
            BindingFlags.Static | BindingFlags.NonPublic;

        private static readonly MethodInfo StartClockOut =
            typeof(ShiftManager).GetMethod(
                "StartClockOut", InstancePrivate);
        private static readonly MethodInfo StartShiftManager =
            typeof(ShiftManager).GetMethod("Start", InstancePrivate);
        private static readonly FieldInfo RunDataField =
            typeof(RunStateController).GetField("_data", InstancePrivate);
        private static readonly FieldInfo GameManagerInstanceField =
            typeof(GameManager).GetField(
                "<Instance>k__BackingField", StaticPrivate);
        private static readonly FieldInfo GameManagerRunField =
            typeof(GameManager).GetField(
                "<Run>k__BackingField", InstancePrivate);
        private static readonly FieldInfo ClaimTemplatesField =
            typeof(ShiftManager).GetField("_claimTemplates", InstancePrivate);
        private static readonly FieldInfo AnomalyTagsField =
            typeof(ShiftManager).GetField("_anomalyTags", InstancePrivate);
        private static readonly FieldInfo TideTuningField =
            typeof(ShiftManager).GetField("_tideTuning", InstancePrivate);

        private readonly List<UnityEngine.Object> _owned = new();
        private string _saveDirectory;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Assert.IsNotNull(StartClockOut);
            Assert.IsNotNull(StartShiftManager);
            Assert.IsNotNull(RunDataField);
            Assert.IsNotNull(GameManagerInstanceField);
            Assert.IsNotNull(GameManagerRunField);
            Assert.IsNotNull(ClaimTemplatesField);
            Assert.IsNotNull(AnomalyTagsField);
            Assert.IsNotNull(TideTuningField);

            GameManagerInstanceField.SetValue(null, null);
            _saveDirectory = Path.Combine(
                Path.GetTempPath(),
                $"Desk42_Delta3A_PlayMode_{Guid.NewGuid():N}");
            SaveSystem.SetSaveDirectoryOverrideForTests(_saveDirectory);
            SeedEngine.Init(7347);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            RumorMill.ClearAllSubscriptions();
            GameManagerInstanceField.SetValue(null, null);
            for (int i = _owned.Count - 1; i >= 0; i--)
            {
                if (_owned[i] != null)
                    UnityEngine.Object.DestroyImmediate(_owned[i]);
            }
            _owned.Clear();

            SaveSystem.WipeAllSaveData();
            SaveSystem.ClearSaveDirectoryOverrideForTests();
            if (Directory.Exists(_saveDirectory))
                Directory.Delete(_saveDirectory, recursive: true);
            yield return null;
        }

        [UnityTest]
        public IEnumerator LiveClockOut_NextShiftQueue_AndRestartRestoreExactlyOnce()
        {
            var meta = new MetaProgressData();
            var claim = new ActiveClaimData
            {
                ClaimId = "CLM-LIVE-CARRY",
                ClientVariantId = "moth_347",
                ClientSpeciesId = "moth_accountant",
                IncidentText = "Live carried payload",
                ClaimantName = "Live Carry",
                ClaimAmount = 347,
                AnomalyTagIds = new[] { "live_tag" },
            };
            var oldData = NewRun("DELTA3A-LIVE-OLD", 1, claim);
            RunStateController controller = RunController(oldData);
            EncounterCommitService.BeginEncounter(claim, oldData, meta);
            string encounterId = claim.EncounterId;

            GameManager gameManager = GameManagerHost(meta, controller);
            ShiftManager endingShift = Shift("Ending");
            StartClockOut.Invoke(
                endingShift, new object[] { controller });
            StartClockOut.Invoke(
                endingShift, new object[] { controller });

            Assert.AreEqual(ShiftPhase.ClockOut, oldData.CurrentPhase);
            Assert.AreEqual(1, meta.CarriedEncounters.Count);
            Assert.AreEqual(1,
                meta.CarriedEncounters.Find(encounterId).InterruptCount,
                "Duplicate live clock-out reentry must not count twice.");
            Assert.AreEqual(0, meta.GetTotalVisits("moth_347"));
            Assert.IsFalse(
                ApprovalLiabilityPolicy.HasApprovalLiability(
                    meta, encounterId));
            Assert.IsTrue(
                SaveSystem.LoadMeta().CarriedEncounters.Has(encounterId));

            // Replace the old per-run state, then execute normal ShiftManager Start.
            var nextData = NewRun("DELTA3A-LIVE-NEXT", 2);
            nextData.CurrentPhase = ShiftPhase.ClockIn;
            RunDataField.SetValue(controller, nextData);
            GameManagerRunField.SetValue(gameManager, controller);
            ShiftManager nextShift = Shift("Next");
            StartShiftManager.Invoke(nextShift, null);

            Assert.AreEqual(1, CountEncounter(nextData, encounterId));
            Assert.AreEqual(encounterId, nextData.ActiveClaim.EncounterId);
            Assert.AreEqual("moth_347",
                nextData.ActiveClaim.ClientVariantId);
            Assert.AreEqual("Live carried payload",
                nextData.ActiveClaim.IncidentText);
            Assert.IsTrue(nextData.PendingClaims.Any(
                c => c != null && c.EncounterId != encounterId),
                "Freshly generated work must remain alongside carried work.");

            StartShiftManager.Invoke(nextShift, null);
            Assert.AreEqual(1, CountEncounter(nextData, encounterId),
                "Repeated live queue setup must not duplicate X.");

            // Crash/restart after queueing, before any interaction.
            Assert.IsTrue(SaveSystem.SaveRun(nextData));
            Assert.IsTrue(SaveSystem.SaveMeta(meta));
            MetaProgressData reloadedMeta = SaveSystem.LoadMeta();
            RunData reloadedRun = SaveSystem.LoadRun();
            RunStateController reconstructed =
                RunController(reloadedRun);
            gameManager.SetMetaForTesting(reloadedMeta);
            GameManagerRunField.SetValue(gameManager, reconstructed);
            ShiftManager reconstructedShift = Shift("Reconstructed");
            StartShiftManager.Invoke(reconstructedShift, null);

            Assert.AreEqual(1,
                CountEncounter(reloadedRun, encounterId));
            Assert.AreEqual(encounterId,
                reloadedRun.ActiveClaim.EncounterId);
            Assert.AreEqual(0,
                reloadedMeta.GetTotalVisits("moth_347"));
            Assert.AreEqual(1, reloadedMeta.Encounters.Count);
            Assert.AreEqual(1,
                reloadedMeta.CarriedEncounters.Count);

            yield return null;
        }

        private GameManager GameManagerHost(
            MetaProgressData meta,
            RunStateController run)
        {
            var host = new GameObject("Delta3A_PlayMode_GameManager");
            host.SetActive(false);
            GameManager manager = host.AddComponent<GameManager>();
            _owned.Add(host);
            GameManagerInstanceField.SetValue(null, manager);
            GameManagerRunField.SetValue(manager, run);
            manager.SetMetaForTesting(meta);
            return manager;
        }

        private RunStateController RunController(RunData data)
        {
            var host = new GameObject(
                $"Delta3A_PlayMode_Run_{_owned.Count}");
            _owned.Add(host);
            RunStateController run =
                host.AddComponent<RunStateController>();
            RunDataField.SetValue(run, data);
            return run;
        }

        private ShiftManager Shift(string label)
        {
            var host = new GameObject(
                $"Delta3A_PlayMode_Shift_{label}");
            _owned.Add(host);
            ShiftManager shift = host.AddComponent<ShiftManager>();
            shift.enabled = false;

            ClaimTemplateData template =
                ScriptableObject.CreateInstance<ClaimTemplateData>();
            _owned.Add(template);
            template.TemplateId = $"delta3a_playmode_{label}";
            template.SpeciesPool = new[] { "unregistered_alien" };
            template.ClaimantNamePool = new[] { "Fresh Live Claim" };
            template.DeptNamePool = new[] { "Live Generated" };
            template.IncidentTextVariants =
                new[] { "{claimant} at {dept} for {amount}." };
            template.AnomalyTagSlots = 0;
            template.ClaimAmountMin = 12;
            template.ClaimAmountMax = 12;
            template.MinShiftNumber = 1;
            template.SpawnWeight = 1f;

            var tuning =
                ScriptableObject.CreateInstance<TideTuningData>();
            _owned.Add(tuning);
            ClaimTemplatesField.SetValue(
                shift, new[] { template });
            AnomalyTagsField.SetValue(
                shift, Array.Empty<AnomalyTagData>());
            TideTuningField.SetValue(shift, tuning);
            return shift;
        }

        private static RunData NewRun(
            string seed,
            int shift,
            ActiveClaimData active = null)
            => new()
            {
                SeedCode = seed,
                ShiftNumber = shift,
                ActiveClaim = active,
                Sanity = 100f,
                SoulIntegrity = 100f,
                ComboMultiplier = 1f,
                Stats = new RunStatistics(),
            };

        private static int CountEncounter(
            RunData data, string encounterId)
        {
            int count = data.PendingClaims.Count(c =>
                c != null
                && string.Equals(
                    c.EncounterId,
                    encounterId,
                    StringComparison.Ordinal));
            if (data.ActiveClaim != null
                && string.Equals(
                    data.ActiveClaim.EncounterId,
                    encounterId,
                    StringComparison.Ordinal))
            {
                count++;
            }
            return count;
        }
    }
}
