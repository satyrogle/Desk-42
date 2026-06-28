// ============================================================
// DESK 42 — Desk Entropy Tier Unit Tests (Edit Mode)
//
// The task brief asked for "entropy tier -> stamp corruption
// mapping, tied to Sanity." That literal system does not exist
// anywhere in the codebase (verified: no "StampCorruption" /
// "CorruptionTier" type, and the only "corruption" field,
// TransitionContext.ClaimCorruption, has no producer).
//
// The closest real analog is DeskEntropyRenderer's four-tier
// visual mapping (Pristine/Cluttered/Deteriorated/Collapsed),
// which is driven by RunStateController.DeskEntropy (0-1) —
// NOT Sanity (0-100), a separate field. These tests cover the
// real tier thresholds; the final test documents the Sanity-tie
// gap and is expected to fail.
// ============================================================

using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Desk42.Core;
using Desk42.UI;

namespace Desk42.Tests.EditMode
{
    [TestFixture]
    public sealed class DeskEntropyTierTests
    {
        private GameObject _go;
        private DeskEntropyRenderer _renderer;
        private CanvasGroup _tier1;
        private CanvasGroup _tier2;
        private CanvasGroup _tier3;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("TestDeskEntropyRenderer");
            _renderer = _go.AddComponent<DeskEntropyRenderer>();

            // CanvasGroup only allows one instance per GameObject, so each
            // tier needs its own child object.
            _tier1 = new GameObject("Tier1").AddComponent<CanvasGroup>();
            _tier2 = new GameObject("Tier2").AddComponent<CanvasGroup>();
            _tier3 = new GameObject("Tier3").AddComponent<CanvasGroup>();
            _tier1.transform.SetParent(_go.transform);
            _tier2.transform.SetParent(_go.transform);
            _tier3.transform.SetParent(_go.transform);

            SetPrivateField("_tier1Clutter", _tier1);
            SetPrivateField("_tier2Deteriorated", _tier2);
            SetPrivateField("_tier3Collapsed", _tier3);
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null)
                Object.DestroyImmediate(_go);
            RumorMill.ClearAllSubscriptions();
        }

        private void SetPrivateField(string name, object value)
        {
            var field = typeof(DeskEntropyRenderer).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"Expected private field '{name}' on DeskEntropyRenderer — rename? Test is bound to real field names.");
            field.SetValue(_renderer, value);
        }

        [Test]
        public void Entropy_BelowAllThresholds_AllTiersInvisible()
        {
            _renderer.PreviewEntropy(0.10f);

            Assert.AreEqual(0f, _tier1.alpha, 0.001f);
            Assert.AreEqual(0f, _tier2.alpha, 0.001f);
            Assert.AreEqual(0f, _tier3.alpha, 0.001f);
        }

        [Test]
        public void Entropy_MidClutterTier_Tier1PartiallyVisible_OthersZero()
        {
            // InverseLerp(0.20, 0.40, 0.30) == 0.5
            _renderer.PreviewEntropy(0.30f);

            Assert.AreEqual(0.5f, _tier1.alpha, 0.01f);
            Assert.AreEqual(0f, _tier2.alpha, 0.001f);
            Assert.AreEqual(0f, _tier3.alpha, 0.001f);
        }

        [Test]
        public void Entropy_DeterioratedTier_Tier1Full_Tier2PartiallyVisible()
        {
            // InverseLerp(0.45, 0.60, 0.55) == 0.667
            _renderer.PreviewEntropy(0.55f);

            Assert.AreEqual(1f, _tier1.alpha, 0.001f);
            Assert.AreEqual(0.667f, _tier2.alpha, 0.01f);
            Assert.AreEqual(0f, _tier3.alpha, 0.001f);
        }

        [Test]
        public void Entropy_CollapsedTier_Tier1And2Full_Tier3PartiallyVisible()
        {
            // InverseLerp(0.70, 0.85, 0.80) == 0.667
            _renderer.PreviewEntropy(0.80f);

            Assert.AreEqual(1f, _tier1.alpha, 0.001f);
            Assert.AreEqual(1f, _tier2.alpha, 0.001f);
            Assert.AreEqual(0.667f, _tier3.alpha, 0.01f);
        }

        [Test]
        public void Entropy_AtMaximum_AllTiersFullyVisible()
        {
            _renderer.PreviewEntropy(1f);

            Assert.AreEqual(1f, _tier1.alpha, 0.001f);
            Assert.AreEqual(1f, _tier2.alpha, 0.001f);
            Assert.AreEqual(1f, _tier3.alpha, 0.001f);
        }

        [Test]
        public void PreviewEntropy_ClampsOutOfRangeInput()
        {
            Assert.DoesNotThrow(() => _renderer.PreviewEntropy(5f));
            Assert.AreEqual(1f, _tier1.alpha, 0.001f);

            Assert.DoesNotThrow(() => _renderer.PreviewEntropy(-5f));
            Assert.AreEqual(0f, _tier1.alpha, 0.001f);
        }

        // ── WIRING GAP ─────────────────────────────────────────

        [Test]
        public void WIRING_GAP_StampCorruptionMapping_TiedToSanity_DoesNotExist()
        {
            // Task intent: an entropy-tier -> stamp-corruption mapping tied to Sanity.
            // Reality: DeskEntropyRenderer's tiers are tied to DeskEntropy, a field
            // distinct from Sanity, and no "stamp corruption" type exists at all.
            var corruptionType = AppDomainHasType("StampCorruption", "CorruptionTier");

            Assert.IsNotNull(corruptionType,
                "WIRING GAP: no 'stamp corruption' tier system exists anywhere in the codebase. " +
                "The closest analog, DeskEntropyRenderer, maps tiers from RunStateController.DeskEntropy " +
                "(0-1) — a field separate from Sanity (0-100, also on RunStateController). " +
                "If a Sanity-tied corruption system is wanted, it does not exist yet and needs design, " +
                "not a test.");
        }

        private static System.Type AppDomainHasType(params string[] nameFragments)
        {
            return System.AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a =>
                {
                    try { return a.GetTypes(); }
                    catch { return System.Array.Empty<System.Type>(); }
                })
                .FirstOrDefault(t => nameFragments.Any(f => t.Name.Contains(f)));
        }
    }
}
