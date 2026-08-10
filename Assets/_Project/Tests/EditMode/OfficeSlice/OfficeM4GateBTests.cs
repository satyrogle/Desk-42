using System.Collections.Generic;
using Desk42.Product.OfficeSlice;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Desk42.Tests.EditMode.OfficeSlice
{
    public sealed class OfficeM4GateBTests
    {
        [Test]
        public void EveryDepartmentHasDistinctSilhouetteSignature()
        {
            OfficeSpriteCatalog catalog = OfficeSpriteCatalog.LoadRequired();
            string[] ids =
            {
                "environment.kit.counter",
                "environment.kit.chair",
                "environment.kit.shelf",
                "environment.kit.vault",
                "environment.kit.impossible-door",
            };
            var hashes = new HashSet<Hash128>();
            for (int i = 0; i < ids.Length; i++)
            {
                Assert.That(catalog.TryResolve(ids[i], out Sprite sprite), Is.True, ids[i]);
                Assert.That(hashes.Add(AssetDatabase.GetAssetDependencyHash(
                    AssetDatabase.GetAssetPath(sprite))), Is.True, ids[i]);
            }
        }

        [Test]
        public void EnvironmentStateAndUpgradeVariantsResolve()
        {
            OfficeSpriteCatalog catalog = OfficeSpriteCatalog.LoadRequired();
            string[] ids =
            {
                "environment.state.rush", "environment.state.break",
                "environment.state.recovery", "environment.shift.2-dressing",
                "environment.shift.3-dressing", "environment.upgrade.fast-trays",
                "environment.upgrade.calm-chairs", "environment.upgrade.red-labels",
                "environment.interaction.socket", "environment.route.overlay",
            };
            for (int i = 0; i < ids.Length; i++)
                Assert.That(catalog.TryResolve(ids[i], out Sprite sprite) && sprite != null,
                    Is.True, ids[i]);
        }

        [Test]
        public void AuthoredEnvironmentCreatesNoColliderOrPrimitiveMesh()
        {
            var root = new GameObject("M4 Environment Test Root");
            try
            {
                var director = new OfficeVisualDirector(
                    root.transform, OfficeSpriteCatalog.LoadRequired());
                director.BuildEnvironment();

                Assert.That(root.GetComponentsInChildren<Collider>(true), Is.Empty);
                Assert.That(root.GetComponentsInChildren<MeshFilter>(true), Is.Empty);
                Assert.That(root.GetComponentsInChildren<SpriteRenderer>(true).Length,
                    Is.GreaterThan(10));
                Assert.That(director.UsedFallback, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void EnvironmentConstructionDoesNotChangeGridOrSimulationChecksum()
        {
            OfficeSimulationState state = OfficeSimulationState.CreateM2();
            string before = state.Checksum;
            var routeLengths = new List<int>();
            for (int i = 0; i < state.Grid.InteractionPoints.Count; i++)
            {
                Assert.That(state.Grid.TryFindPath(state.Grid.SpawnCell,
                    state.Grid.InteractionPoints[i].Cell, out List<OfficeCell> path), Is.True);
                routeLengths.Add(path.Count);
            }
            var root = new GameObject("M4 Route Test Root");
            try
            {
                new OfficeVisualDirector(root.transform,
                    OfficeSpriteCatalog.LoadRequired()).BuildEnvironment();
                Assert.That(state.Checksum, Is.EqualTo(before));
                for (int i = 0; i < state.Grid.InteractionPoints.Count; i++)
                {
                    Assert.That(state.Grid.TryFindPath(state.Grid.SpawnCell,
                        state.Grid.InteractionPoints[i].Cell, out List<OfficeCell> path), Is.True);
                    Assert.That(path.Count, Is.EqualTo(routeLengths[i]));
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ActiveAnomalyOutranksPriorShiftRecoveryInVisualProjection()
        {
            OfficeCampaignState campaign = OfficeCampaignState.Create();
            OfficeCampaignCaptureDriver.Prepare(campaign, 2, "rush");

            OfficeVisualSnapshot snapshot = new OfficeVisualStateProjector().Project(
                campaign.CurrentSimulation, campaign);

            Assert.That(campaign.CurrentSimulation.GhostClock.Active, Is.True);
            Assert.That(snapshot.Pressure, Is.EqualTo(OfficeVisualPressureState.Break));
        }
    }
}
