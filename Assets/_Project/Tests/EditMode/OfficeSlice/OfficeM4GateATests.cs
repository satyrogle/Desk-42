using System;
using System.Collections.Generic;
using System.IO;
using Desk42.Product.OfficeSlice;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Desk42.Tests.EditMode.OfficeSlice
{
    public sealed class OfficeM4GateATests
    {
        private const string ManifestPath =
            "Assets/_Project/Art/OfficeSliceM4/Config/runtime-asset-manifest.json";
        private const string LedgerPath =
            "ArtLab/OfficeSliceM4/Provenance/asset-ledger.csv";

        [Test]
        public void AllM4VisualIdsResolveToApprovedAssets()
        {
            OfficeSpriteCatalog catalog = OfficeSpriteCatalog.LoadRequired();
            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.Entries.Count, Is.GreaterThanOrEqualTo(12));
            for (int i = 0; i < catalog.Entries.Count; i++)
            {
                OfficeSpriteCatalog.Entry entry = catalog.Entries[i];
                Assert.That(entry.Id, Is.Not.Empty);
                Assert.That(entry.Sprite, Is.Not.Null, entry.Id);
                Assert.That(AssetDatabase.GetAssetPath(entry.Sprite),
                    Does.StartWith("Assets/_Project/Art/OfficeSliceM4/"), entry.Id);
            }
        }

        [Test]
        public void NoApprovedRuntimeAssetLacksProvenanceEntry()
        {
            JObject manifest = JObject.Parse(File.ReadAllText(ManifestPath));
            string ledger = File.ReadAllText(LedgerPath);
            foreach (JToken asset in manifest["assets"] ?? new JArray())
            {
                string id = asset.Value<string>("asset_id");
                Assert.That(ledger, Does.Contain(id + ","), id);
                Assert.That(asset.Value<string>("reviewer_decision"), Is.EqualTo("approved"), id);
                Assert.That(asset.Value<string>("final_sha256"), Has.Length.EqualTo(64), id);
            }
        }

        [Test]
        public void NoDuplicateRuntimeAssetIdExists()
        {
            OfficeSpriteCatalog catalog = OfficeSpriteCatalog.LoadRequired();
            Assert.That(catalog.HasDuplicateIds(), Is.False);
        }

        [Test]
        public void PaletteContainsEveryRequiredSemanticColour()
        {
            OfficeSpriteCatalog catalog = OfficeSpriteCatalog.LoadRequired();
            string[] ids =
            {
                "cream-paper", "warm-plaster", "moss-furniture", "machine-teal",
                "coffee-wood", "calm-mint", "warning-amber", "break-red", "ink",
                "ghost-cyan", "impossible-violet",
            };
            var found = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < catalog.Theme.Colours.Count; i++)
                found.Add(catalog.Theme.Colours[i].Id);
            CollectionAssert.IsSubsetOf(ids, found);
        }

        [Test]
        public void VisualProjectionDoesNotMutateSimulationState()
        {
            OfficeSimulationState state = OfficeSimulationState.CreateM2();
            string before = state.Checksum;
            long tick = state.CurrentTick;
            var projector = new OfficeVisualStateProjector();

            OfficeVisualSnapshot snapshot = projector.Project(state, null);

            Assert.That(snapshot.Tick, Is.EqualTo(tick));
            Assert.That(snapshot.SimulationChecksum, Is.EqualTo(before));
            Assert.That(state.CurrentTick, Is.EqualTo(tick));
            Assert.That(state.Checksum, Is.EqualTo(before));
        }

        [Test]
        public void MissingVisualUsesFallbackWithoutChangingState()
        {
            OfficeSimulationState state = OfficeSimulationState.CreateM2();
            string before = state.Checksum;
            OfficeSpriteCatalog catalog = OfficeSpriteCatalog.LoadRequired();
            LogAssert.Expect(LogType.Error,
                "OFFICE_M4_VISUAL_MISSING_ID missing.required-test-id");

            Sprite sprite = catalog.ResolveOrFallback("missing.required-test-id", out bool fallback);

            Assert.That(fallback, Is.True);
            Assert.That(sprite, Is.Not.Null);
            Assert.That(state.Checksum, Is.EqualTo(before));
        }
    }
}
