using System;
using System.Collections.Generic;
using System.IO;
using Desk42.Product.OfficeSlice;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Desk42.Tests.EditMode.OfficeSlice
{
    public sealed class OfficeM4GateDTests
    {
        private static readonly string[] Machines =
        {
            "front-desk-counter", "paper-check", "money-trace", "auto-sorter",
            "copy-echo", "ghost-clock", "supervisor-stamp",
        };

        private static readonly string[] MachineStates =
        {
            "idle", "active", "warning", "jammed", "break",
        };

        private static readonly string[] Effects =
        {
            "paper-pickup", "folder-send-trail", "paper-compare-snap",
            "money-route-pulse", "rule-learned-stamp", "rule-accepted-tick",
            "rule-rejected-cross", "customer-mood-rise", "calm-effect",
            "copy-spawn", "copy-clear", "ghost-clock-slip", "machine-stop",
            "supervisor-stamp-attach", "supervisor-stamp-remove",
            "runner-allegiance-swap", "promotion-cascade-ink-fracture",
            "recovery-complete", "shift-close",
        };

        [Test]
        public void AllFolderStatesResolveAtBothUpgradeTiers()
        {
            OfficeSpriteCatalog catalog = OfficeSpriteCatalog.LoadRequired();
            string[] states =
            {
                "normal", "original", "copy", "time-slip", "promotion-form",
                "carried", "rule-matched", "returned", "copy.tier-0",
                "copy.tier-1", "copy.tier-2",
            };
            for (int i = 0; i < states.Length; i++)
            {
                string id = "folder." + states[i];
                Assert.That(catalog.TryResolve(id, out Sprite sprite) && sprite != null,
                    Is.True, id);
            }
            foreach (string family in new[] { "fast-trays", "calm-chairs", "red-labels" })
                for (int tier = 1; tier <= 2; tier++)
                {
                    string id = "upgrade." + family + ".tier-" + tier;
                    Assert.That(catalog.TryResolve(id, out Sprite sprite) && sprite != null,
                        Is.True, id);
                }
        }

        [Test]
        public void AllMachineStatesResolveForEveryCampaignShift()
        {
            OfficeSpriteCatalog catalog = OfficeSpriteCatalog.LoadRequired();
            for (int shift = 1; shift <= 3; shift++)
                for (int machine = 0; machine < Machines.Length; machine++)
                    for (int state = 0; state < MachineStates.Length; state++)
                    {
                        string id = "machine." + Machines[machine] + "." +
                            MachineStates[state];
                        Assert.That(catalog.TryResolve(id, out Sprite sprite) && sprite != null,
                            Is.True, "shift=" + shift + " id=" + id);
                    }
        }

        [Test]
        public void OriginalAndCopyFoldersDifferByShapeAndMark()
        {
            OfficeSpriteCatalog catalog = OfficeSpriteCatalog.LoadRequired();
            Assert.That(catalog.TryResolve("folder.original", out Sprite original), Is.True);
            Assert.That(catalog.TryResolve("folder.copy.tier-0", out Sprite copy), Is.True);
            Assert.That(AssetDatabase.GetAssetDependencyHash(
                    AssetDatabase.GetAssetPath(original)),
                Is.Not.EqualTo(AssetDatabase.GetAssetDependencyHash(
                    AssetDatabase.GetAssetPath(copy))));

            PixelSignature originalPixels = ReadSignature(original);
            PixelSignature copyPixels = ReadSignature(copy);
            Assert.That(copyPixels.OpaquePixels, Is.Not.EqualTo(originalPixels.OpaquePixels),
                "Original and copy silhouettes must differ.");
            Assert.That(copyPixels.BreakRedPixels, Is.GreaterThan(originalPixels.BreakRedPixels),
                "Copies must carry an explicit break-red visual mark.");
        }

        [Test]
        public void EveryRequiredVfxResolvesAndUsesTheBoundedPool()
        {
            OfficeSpriteCatalog catalog = OfficeSpriteCatalog.LoadRequired();
            var root = new GameObject("M4 VFX Catalog Test");
            try
            {
                var director = new OfficeVisualDirector(root.transform, catalog);
                director.BuildEnvironment();
                int childCount = root.transform.childCount;
                for (int i = 0; i < Effects.Length; i++)
                {
                    string id = "vfx." + Effects[i];
                    Assert.That(catalog.TryResolve(id, out Sprite sprite) && sprite != null,
                        Is.True, id);
                    Assert.That(director.RequestVfx(id, Vector3.zero), Is.Not.Null, id);
                }
                Assert.That(director.VfxPool.ActiveCount, Is.EqualTo(Effects.Length));
                Assert.That(root.transform.childCount, Is.EqualTo(childCount));
                Assert.That(director.VfxPool.GrowthCount, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void RepeatedBreakRestartDoesNotGrowVisualPools()
        {
            OfficeSpriteCatalog catalog = OfficeSpriteCatalog.LoadRequired();
            for (int restart = 0; restart < 12; restart++)
            {
                var root = new GameObject("M4 Restart Pool " + restart);
                try
                {
                    var director = new OfficeVisualDirector(root.transform, catalog);
                    director.BuildEnvironment();
                    int childCount = root.transform.childCount;
                    for (int cycle = 0; cycle < 4; cycle++)
                    {
                        for (int i = 0; i < Effects.Length; i++)
                            Assert.That(director.RequestVfx(
                                "vfx." + Effects[i], Vector3.zero), Is.Not.Null);
                        director.VfxPool.ReleaseAll();
                    }
                    Assert.That(director.VfxPool.Capacity, Is.EqualTo(32));
                    Assert.That(director.VfxPool.GrowthCount, Is.Zero);
                    Assert.That(root.transform.childCount, Is.EqualTo(childCount));
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }
        }

        [Test]
        public void PromotionCascadeVisualStressStaysWithinPoolBounds()
        {
            OfficeSpriteCatalog catalog = OfficeSpriteCatalog.LoadRequired();
            var root = new GameObject("M4 Promotion Cascade Pool Stress");
            try
            {
                var director = new OfficeVisualDirector(root.transform, catalog);
                director.BuildEnvironment();
                for (int wave = 0; wave < 100; wave++)
                {
                    director.VfxPool.ReleaseAll();
                    for (int i = 0; i < Effects.Length; i++)
                        Assert.That(director.RequestVfx(
                            "vfx." + Effects[i], new Vector3(i, wave, 0f)), Is.Not.Null);
                    Assert.That(director.VfxPool.ActiveCount,
                        Is.LessThanOrEqualTo(director.VfxPool.Capacity));
                }
                Assert.That(director.VfxPool.GrowthCount, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static PixelSignature ReadSignature(Sprite sprite)
        {
            byte[] bytes = File.ReadAllBytes(AssetDatabase.GetAssetPath(sprite));
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Assert.That(ImageConversion.LoadImage(texture, bytes, false), Is.True);
                Color32[] pixels = texture.GetPixels32();
                int opaque = 0;
                int red = 0;
                for (int i = 0; i < pixels.Length; i++)
                {
                    Color32 pixel = pixels[i];
                    if (pixel.a > 0) opaque++;
                    if (pixel.r == 181 && pixel.g == 59 && pixel.b == 56 && pixel.a > 0)
                        red++;
                }
                return new PixelSignature(opaque, red);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private readonly struct PixelSignature
        {
            public PixelSignature(int opaquePixels, int breakRedPixels)
            {
                OpaquePixels = opaquePixels;
                BreakRedPixels = breakRedPixels;
            }

            public int OpaquePixels { get; }
            public int BreakRedPixels { get; }
        }
    }
}
