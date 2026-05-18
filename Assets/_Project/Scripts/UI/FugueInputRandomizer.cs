// ============================================================
// DESK 42 — Fugue Input Randomizer (MonoBehaviour)
//
// When the player enters Fugue State (sanity <= 0), the card
// buttons shuffle positions every few seconds, making the
// hand disorienting to navigate. Resolves when sanity recovers.
//
// Uses SeedEngine for deterministic shuffles (replay-safe).
// ============================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Desk42.Core;

namespace Desk42.UI
{
    [DisallowMultipleComponent]
    public sealed class FugueInputRandomizer : RumorMillListener
    {
        // ── Inspector ─────────────────────────────────────────

        [Tooltip("The container whose child RectTransforms will be shuffled.")]
        [SerializeField] private Transform _cardContainer;

        [Tooltip("Seconds between shuffle cycles during fugue.")]
        [SerializeField] private float _shuffleInterval = 2.5f;

        [Tooltip("Duration of the shuffle animation lerp.")]
        [SerializeField] private float _shuffleDuration = 0.3f;

        // ── State ─────────────────────────────────────────────

        private bool  _inFugue;
        private Coroutine _shuffleLoop;

        // ── Unity Lifecycle ───────────────────────────────────

        protected override void Subscribe()
        {
            RumorMill.OnSanityChanged  += HandleSanityChanged;
            RumorMill.OnShiftLifecycle += HandleShiftLifecycle;
        }

        protected override void Unsubscribe()
        {
            RumorMill.OnSanityChanged  -= HandleSanityChanged;
            RumorMill.OnShiftLifecycle -= HandleShiftLifecycle;
            StopFugue();
        }

        // ── Event Handlers ────────────────────────────────────

        private void HandleSanityChanged(SanityChangedEvent e)
        {
            if (e.TriggeredFugue && !_inFugue)
                StartFugue();
            else if (e.Current > 0f && _inFugue)
                StopFugue();
        }

        private void HandleShiftLifecycle(ShiftLifecycleEvent e)
        {
            if (e.IsStart) StopFugue();
        }

        // ── Fugue Control ─────────────────────────────────────

        private void StartFugue()
        {
            _inFugue = true;
            _shuffleLoop = StartCoroutine(ShuffleLoop());
            Debug.Log("[FugueRandomizer] Fugue state — input scramble active.");
        }

        private void StopFugue()
        {
            if (_shuffleLoop != null)
            {
                StopCoroutine(_shuffleLoop);
                _shuffleLoop = null;
            }

            if (_inFugue)
            {
                RestoreOrder();
                _inFugue = false;
                Debug.Log("[FugueRandomizer] Fugue ended — input restored.");
            }
        }

        // ── Shuffle Loop ──────────────────────────────────────

        private IEnumerator ShuffleLoop()
        {
            while (_inFugue)
            {
                yield return new WaitForSeconds(_shuffleInterval);

                if (!_inFugue || _cardContainer == null) yield break;

                int childCount = _cardContainer.childCount;
                if (childCount < 2) continue;

                // Build index array and shuffle
                int[] indices = new int[childCount];
                for (int i = 0; i < childCount; i++) indices[i] = i;
                SeedEngine.Shuffle(SeedStream.FormCorruption, indices);

                // Capture current positions
                var positions = new Vector2[childCount];
                for (int i = 0; i < childCount; i++)
                {
                    var rt = _cardContainer.GetChild(i) as RectTransform;
                    positions[i] = rt != null ? rt.anchoredPosition : Vector2.zero;
                }

                // Animate toward shuffled positions
                yield return AnimateShuffle(positions, indices);
            }
        }

        private IEnumerator AnimateShuffle(Vector2[] originalPositions, int[] targetIndices)
        {
            int count = _cardContainer.childCount;
            var startPositions = new Vector2[count];

            for (int i = 0; i < count; i++)
            {
                var rt = _cardContainer.GetChild(i) as RectTransform;
                startPositions[i] = rt != null ? rt.anchoredPosition : Vector2.zero;
            }

            float t = 0f;
            while (t < _shuffleDuration)
            {
                t += Time.deltaTime;
                float progress = Mathf.SmoothStep(0f, 1f, t / _shuffleDuration);

                for (int i = 0; i < count; i++)
                {
                    var rt = _cardContainer.GetChild(i) as RectTransform;
                    if (rt == null) continue;

                    Vector2 target = originalPositions[targetIndices[i]];
                    rt.anchoredPosition = Vector2.Lerp(startPositions[i], target, progress);
                }

                yield return null;
            }
        }

        // ── Restore ───────────────────────────────────────────

        private void RestoreOrder()
        {
            if (_cardContainer == null) return;

            // Force a layout rebuild by toggling sibling indices back to sequential
            // (the LayoutGroup will reposition children on the next frame)
            for (int i = 0; i < _cardContainer.childCount; i++)
                _cardContainer.GetChild(i).SetSiblingIndex(i);

            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(
                _cardContainer as RectTransform);
        }
    }
}
