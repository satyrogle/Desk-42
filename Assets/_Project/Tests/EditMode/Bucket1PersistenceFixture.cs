using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Desk42.Core;
using Desk42.Encounter;
using NUnit.Framework;
using UnityEngine;

namespace Desk42.Tests.EditMode
{
    /// <summary>
    /// Deterministic, disk-isolated fixture for adversarial Bucket 1 tests.
    /// It deliberately invokes the production commit service rather than
    /// reproducing any persistence behavior in test code.
    /// </summary>
    public abstract class Bucket1PersistenceFixture
    {
        private static readonly FieldInfo RunDataField =
            typeof(RunStateController).GetField(
                "_data", BindingFlags.NonPublic | BindingFlags.Instance);

        private readonly List<GameObject> _hosts = new();
        private string _saveDirectory;

        protected MetaProgressData Meta { get; private set; }

        [SetUp]
        public void SetUpBucket1Fixture()
        {
            Assert.IsNotNull(RunDataField,
                "RunStateController._data is required by this test-only fixture.");

            _saveDirectory = Path.Combine(
                Path.GetTempPath(), $"Desk42_Bucket1_{Guid.NewGuid():N}");
            SaveSystem.SetSaveDirectoryOverrideForTests(_saveDirectory);
            SeedEngine.Init(4242);
            Meta = new MetaProgressData();
        }

        [TearDown]
        public void TearDownBucket1Fixture()
        {
            RumorMill.ClearAllSubscriptions();
            SaveSystem.WipeAllSaveData();
            SaveSystem.ClearSaveDirectoryOverrideForTests();

            for (int i = _hosts.Count - 1; i >= 0; i--)
            {
                if (_hosts[i] != null)
                    UnityEngine.Object.DestroyImmediate(_hosts[i]);
            }
            _hosts.Clear();

            if (Directory.Exists(_saveDirectory))
                Directory.Delete(_saveDirectory, recursive: true);
        }

        protected string SaveDirectory => _saveDirectory;

        protected static RunData RunData(
            string seed = "BUCKET1",
            int shift = 1,
            ActiveClaimData activeClaim = null)
            => new()
            {
                SeedCode = seed,
                ShiftNumber = shift,
                ActiveClaim = activeClaim,
                Sanity = 100f,
                SoulIntegrity = 100f,
                ComboMultiplier = 1f,
                Stats = new RunStatistics(),
            };

        protected static ActiveClaimData Claim(
            string claimId = "CLM-42424",
            string variant = "elias_bucket1",
            string species = "human",
            string appearanceKey = null)
            => new()
            {
                ClaimId = claimId,
                ClientVariantId = variant,
                ClientSpeciesId = species,
                AuthoredAppearanceKey = appearanceKey,
            };

        protected RunStateController Controller(RunData data)
        {
            var controller = Component<RunStateController>("Run");
            RunDataField.SetValue(controller, data);
            return controller;
        }

        protected T Component<T>(string label) where T : Component
        {
            var host = new GameObject($"Bucket1_{label}_{_hosts.Count + 1}");
            _hosts.Add(host);
            return host.AddComponent<T>();
        }

        protected static EncounterBaseline Present(
            ActiveClaimData claim,
            RunData run,
            MetaProgressData meta)
            => EncounterCommitService.BeginEncounter(claim, run, meta);

        protected static CommitResult Commit(
            ActiveClaimData claim,
            ClaimResolutionKind kind,
            RunStateController run,
            MetaProgressData meta)
        {
            ClaimResolutionOutcome outcome = kind switch
            {
                ClaimResolutionKind.Approve => new ClaimResolutionOutcome(
                    kind, creditsEarned: 12, sanityCost: 3f, soulCost: 1f),
                ClaimResolutionKind.Deny => new ClaimResolutionOutcome(
                    kind, creditsEarned: 0, sanityCost: 3f, soulCost: 0f),
                ClaimResolutionKind.Liquify =>
                    ClaimResolutionConsequencePolicy.Liquify(),
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
            };

            return EncounterCommitService.CommitEncounterResult(
                claim, outcome, run, meta, proof: null, eliasContent: null);
        }

        protected static EncounterRecord OnlyRecord(MetaProgressData meta)
        {
            Assert.IsNotNull(meta?.Encounters);
            Assert.AreEqual(1, meta.Encounters.Count);
            return meta.Encounters.Records[0];
        }
    }
}
