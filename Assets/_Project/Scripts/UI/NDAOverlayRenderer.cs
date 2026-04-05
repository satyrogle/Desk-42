// ============================================================
// DESK 42 — NDA Overlay Renderer (MonoBehaviour)
//
// Renders semi-opaque NDA panels that progressively obscure
// the gameplay screen as the player signs more NDAs.
//
// Each NDA covers a normalised region of the screen with:
//   - A semi-opaque beige rectangle
//   - Dense, illegible legalese text rendered on top
//   - A stamped "CONFIDENTIAL" mark
//
// At 3+ active NDAs, the overlays begin to drift slowly
// (the bureaucracy is actively encroaching).
//
// NDA overlays persist across encounters within a shift.
// They are cleared at shift end.
//
// The "NDA Creep" — the further soul drops, the slightly
// more opaque each NDA becomes.
// ============================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Desk42.Core;

namespace Desk42.UI
{
    [DisallowMultipleComponent]
    public sealed class NDAOverlayRenderer : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────

        [Header("Prefab")]
        [SerializeField] private RectTransform _ndaPanelPrefab;

        [Header("NDA Style")]
        [SerializeField] private Color  _ndaBaseColor      = new(0.94f, 0.91f, 0.82f, 0.82f);
        [SerializeField] private Color  _ndaCreepColor     = new(0.94f, 0.91f, 0.82f, 0.95f);
        [SerializeField] private Sprite _confidentialStamp;
        [SerializeField] private string _legalese          =
            "The undersigned agrees to maintain in strict confidence all " +
            "information, documentation, case materials, client identities, " +
            "outcome data, and associated anomalous phenomena disclosed " +
            "pursuant to this agreement. Breach of this agreement carries " +
            "consequences not limited to those permitted under applicable law.";

        [Header("Drift (3+ NDAs)")]
        [SerializeField] private float _driftSpeed   = 0.8f;
        [SerializeField] private float _driftAmount  = 3f;    // pixels

        [Header("Parent Canvas")]
        [SerializeField] private RectTransform _canvasRoot;

        // ── State ─────────────────────────────────────────────

        private readonly List<NDAPanel> _panels = new();

        private sealed class NDAPanel
        {
            public RectTransform Rect;
            public Image         Background;
            public float         DriftPhase;
            public bool          IsDrifting;
        }

        // ── Public API ────────────────────────────────────────

        /// <summary>
        /// Spawn a new NDA overlay at a normalised screen region.
        /// CoveredRegion.x and .y are the centre in 0-1 normalised space.
        /// </summary>
        public void AddOverlay(string claimId, Vector2 coveredRegionCentre)
        {
            if (_ndaPanelPrefab == null || _canvasRoot == null)
            {
                // Fallback: create a raw panel if no prefab assigned
                AddFallbackOverlay(coveredRegionCentre);
                return;
            }

            var rect = Instantiate(_ndaPanelPrefab, _canvasRoot);
            PositionPanel(rect, coveredRegionCentre);
            ConfigurePanel(rect);

            bool isDrifting = _panels.Count >= 2; // 3rd NDA onward

            _panels.Add(new NDAPanel
            {
                Rect       = rect,
                Background = rect.GetComponent<Image>(),
                DriftPhase = Random.Range(0f, Mathf.PI * 2f),
                IsDrifting = isDrifting,
            });

            // Pulse in
            StartCoroutine(PulseIn(rect));

            Debug.Log($"[NDARenderer] Overlay added for claim {claimId}. " +
                      $"Total: {_panels.Count}.");

            if (_panels.Count >= 3)
                ActivateDrift();
        }

        public void ClearAll()
        {
            foreach (var p in _panels)
                if (p.Rect != null) Destroy(p.Rect.gameObject);
            _panels.Clear();
        }

        // ── Drift Update ──────────────────────────────────────

        private void Update()
        {
            if (_panels.Count < 3) return;

            foreach (var panel in _panels)
            {
                if (!panel.IsDrifting || panel.Rect == null) continue;

                float offset = Mathf.Sin(Time.time * _driftSpeed + panel.DriftPhase)
                               * _driftAmount;
                var pos = panel.Rect.anchoredPosition;
                panel.Rect.anchoredPosition = new Vector2(pos.x + offset * Time.deltaTime, pos.y);
            }
        }

        // ── NDA Creep (soul integrity drops → more opaque) ────

        private void LateUpdate()
        {
            if (_panels.Count == 0) return;

            float soul = GameManager.Instance?.Run?.SoulIntegrity ?? 100f;
            float creepT = Mathf.Clamp01(1f - soul / 40f); // fully crept below soul 40

            Color c = Color.Lerp(_ndaBaseColor, _ndaCreepColor, creepT);
            foreach (var p in _panels)
                if (p.Background != null) p.Background.color = c;
        }

        // ── Helpers ───────────────────────────────────────────

        private void PositionPanel(RectTransform rect, Vector2 normalised)
        {
            if (_canvasRoot == null) return;

            var canvasSize = _canvasRoot.rect.size;
            float x = (normalised.x - 0.5f) * canvasSize.x;
            float y = (normalised.y - 0.5f) * canvasSize.y;

            float w = Random.Range(canvasSize.x * 0.20f, canvasSize.x * 0.35f);
            float h = Random.Range(canvasSize.y * 0.15f, canvasSize.y * 0.25f);
            float rot = Random.Range(-4f, 4f);

            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta        = new Vector2(w, h);
            rect.localRotation    = Quaternion.Euler(0f, 0f, rot);
        }

        private void ConfigurePanel(RectTransform rect)
        {
            var bg = rect.GetComponent<Image>();
            if (bg != null) bg.color = _ndaBaseColor;

            // Find the legalese text child
            var textObj = rect.GetComponentInChildren<TMP_Text>();
            if (textObj != null) textObj.text = _legalese;
        }

        private void AddFallbackOverlay(Vector2 normalised)
        {
            var go   = new GameObject("NDA_Overlay");
            go.transform.SetParent(_canvasRoot != null ? _canvasRoot : transform, false);

            var rect  = go.AddComponent<RectTransform>();
            var image = go.AddComponent<Image>();
            image.color = _ndaBaseColor;

            PositionPanel(rect, normalised);
            _panels.Add(new NDAPanel
            {
                Rect       = rect,
                Background = image,
                DriftPhase = Random.Range(0f, Mathf.PI * 2f),
                IsDrifting = _panels.Count >= 2,
            });
        }

        private void ActivateDrift()
        {
            foreach (var p in _panels)
                p.IsDrifting = true;
        }

        private IEnumerator PulseIn(RectTransform rect)
        {
            float t = 0f;
            const float dur = 0.3f;
            rect.localScale = Vector3.zero;

            while (t < dur)
            {
                t += Time.deltaTime;
                float s = Mathf.Lerp(0f, 1f, t / dur);
                rect.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            rect.localScale = Vector3.one;
        }
    }
}
