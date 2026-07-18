using System.Collections;
using Desk42.Accessibility;
using Desk42.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Desk42.UI
{
    [DisallowMultipleComponent]
    public sealed class HazardChainVisual : MonoBehaviour
    {
        [Header("Coordinate Space")]
        [Tooltip("Full-screen UI RectTransform used to convert normalized hazard positions.")]
        [SerializeField] private RectTransform _visualRoot;

        [Header("Connection Line")]
        [Tooltip("Stretched UI image used to draw the amber connection between hazards.")]
        [SerializeField] private Image _lineImage;

        [Header("Consequence Label")]
        [Tooltip("Midpoint label that names the chained hazard consequence.")]
        [SerializeField] private TMP_Text _consequenceLabel;

        private const float ChainWindowSeconds = 30f;
        private static readonly Color ChainColor = new Color32(0xEF, 0x9F, 0x27, 0xFF);

        private OfficeHazardType _lastHazard;
        private float _lastHazardAt = -999f;
        private bool _hasLastHazard;
        private Coroutine _activeVisual;

        private void Awake()
        {
            HideVisual();
        }

        private void OnEnable()
        {
            RumorMill.OnOfficeHazard += HandleOfficeHazard;
        }

        private void OnDisable()
        {
            RumorMill.OnOfficeHazard -= HandleOfficeHazard;

            if (_activeVisual != null)
            {
                StopCoroutine(_activeVisual);
                _activeVisual = null;
            }

            _hasLastHazard = false;
            HideVisual();
        }

        private void HandleOfficeHazard(OfficeHazardEvent hazardEvent)
        {
            OfficeHazardType current = hazardEvent.HazardType;
            float now = Time.unscaledTime;

            if (_hasLastHazard && now - _lastHazardAt <= ChainWindowSeconds)
                TryShowChain(_lastHazard, current);

            _lastHazard = current;
            _lastHazardAt = now;
            _hasLastHazard = true;
        }

        private void TryShowChain(OfficeHazardType from, OfficeHazardType to)
        {
            if (_visualRoot == null || _consequenceLabel == null)
                return;

            if (!EntropyManager.CanActivate(EntropyLayer.ShadowBureaucrat))
                return;

            if (!FeedbackBudget.RequestBurst(FeedbackKind.Flash))
                return;

            if (_activeVisual != null)
                StopCoroutine(_activeVisual);

            _activeVisual = StartCoroutine(ShowChainRoutine(from, to));

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[HazardChainVisual] Chain visual: {from} -> {to}.");
#endif
        }

        private IEnumerator ShowChainRoutine(OfficeHazardType from, OfficeHazardType to)
        {
            if (_visualRoot == null || _visualRoot.gameObject == null
                || _consequenceLabel == null || _consequenceLabel.gameObject == null)
            {
                yield break;
            }

            GetEndpoints(from, to, out Vector2 start, out Vector2 end);
            Vector2 midpoint = (start + end) * 0.5f;

            // NEEDS: public accessor on OfficeHazardInteractionTable.LastConsequence — Claude to add
            _consequenceLabel.text = $"→ {FormatHazardName(to)}";
            _consequenceLabel.fontSize = AccessibilitySettings.Scaled(14f);
            _consequenceLabel.color = ChainColor;
            _consequenceLabel.rectTransform.anchoredPosition = midpoint;

            if (AccessibilitySettings.ReducedMotion)
            {
                SetLineAlpha(0f);
                SetLabelAlpha(1f);
                yield return new WaitForSecondsRealtime(2f);

                if (_consequenceLabel != null && _consequenceLabel.gameObject != null)
                    HideVisual();

                _activeVisual = null;
                yield break;
            }

            ConfigureLine(start, end);
            yield return FadeVisual(0f, 1f, 0.2f);
            yield return new WaitForSecondsRealtime(1.5f);
            yield return FadeVisual(1f, 0f, 0.5f);

            if (_consequenceLabel != null && _consequenceLabel.gameObject != null)
                HideVisual();

            _activeVisual = null;
        }

        private IEnumerator FadeVisual(float from, float to, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (_consequenceLabel == null || _consequenceLabel.gameObject == null)
                    yield break;

                elapsed += Time.unscaledDeltaTime;
                float alpha = Mathf.Lerp(from, to, elapsed / duration);
                SetLineAlpha(alpha);
                SetLabelAlpha(alpha);
                yield return null;
            }

            SetLineAlpha(to);
            SetLabelAlpha(to);
        }

        private void GetEndpoints(
            OfficeHazardType from,
            OfficeHazardType to,
            out Vector2 start,
            out Vector2 end)
        {
            Rect rect = _visualRoot.rect;

            if (from == OfficeHazardType.FireDrill || to == OfficeHazardType.FireDrill)
            {
                start = NormalizedToLocal(new Vector2(0.06f, 0.08f), rect);
                end = NormalizedToLocal(new Vector2(0.94f, 0.92f), rect);
                return;
            }

            start = NormalizedToLocal(GetNormalizedPosition(from), rect);
            end = NormalizedToLocal(GetNormalizedPosition(to), rect);
        }

        private void ConfigureLine(Vector2 start, Vector2 end)
        {
            if (_lineImage == null || _lineImage.gameObject == null)
                return;

            RectTransform line = _lineImage.rectTransform;
            Vector2 delta = end - start;
            line.anchorMin = new Vector2(0.5f, 0.5f);
            line.anchorMax = new Vector2(0.5f, 0.5f);
            line.pivot = new Vector2(0.5f, 0.5f);
            line.anchoredPosition = (start + end) * 0.5f;
            line.sizeDelta = new Vector2(Mathf.Max(2f, delta.magnitude), 2f);
            line.localRotation = Quaternion.Euler(
                0f,
                0f,
                Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            _lineImage.color = ChainColor;
            SetLineAlpha(0f);
        }

        private static Vector2 GetNormalizedPosition(OfficeHazardType hazard)
        {
            return hazard switch
            {
                OfficeHazardType.PrinterJam => new Vector2(0.88f, 0.48f),
                OfficeHazardType.CoffeeMachineDown => new Vector2(0.12f, 0.45f),
                OfficeHazardType.MandatoryMeeting => new Vector2(0.5f, 0.88f),
                OfficeHazardType.SystemCrash => new Vector2(0.5f, 0.5f),
                OfficeHazardType.FireDrill => new Vector2(0.5f, 0.5f),
                OfficeHazardType.UnscheduledAudit => new Vector2(0.84f, 0.82f),
                _ => new Vector2(0.5f, 0.5f)
            };
        }

        private static Vector2 NormalizedToLocal(Vector2 normalized, Rect rect)
        {
            return new Vector2(
                Mathf.Lerp(rect.xMin, rect.xMax, normalized.x),
                Mathf.Lerp(rect.yMin, rect.yMax, normalized.y));
        }

        private static string FormatHazardName(OfficeHazardType hazard)
        {
            return hazard switch
            {
                OfficeHazardType.PrinterJam => "printer jam",
                OfficeHazardType.CoffeeMachineDown => "coffee machine down",
                OfficeHazardType.MandatoryMeeting => "mandatory meeting",
                OfficeHazardType.SystemCrash => "system crash",
                OfficeHazardType.FireDrill => "fire drill",
                OfficeHazardType.UnscheduledAudit => "unscheduled audit",
                _ => hazard.ToString()
            };
        }

        private void HideVisual()
        {
            SetLineAlpha(0f);
            SetLabelAlpha(0f);

            if (_consequenceLabel != null)
                _consequenceLabel.text = string.Empty;
        }

        private void SetLineAlpha(float alpha)
        {
            if (_lineImage == null || _lineImage.gameObject == null)
                return;

            Color color = ChainColor;
            color.a = alpha;
            _lineImage.color = color;
        }

        private void SetLabelAlpha(float alpha)
        {
            if (_consequenceLabel == null || _consequenceLabel.gameObject == null)
                return;

            Color color = ChainColor;
            color.a = alpha;
            _consequenceLabel.color = color;
        }
    }
}
