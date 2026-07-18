using System;
using System.Collections;
using Desk42.Accessibility;
using Desk42.BSM;
using TMPro;
using UnityEngine;

namespace Desk42.UI
{
    [DisallowMultipleComponent]
    public sealed class TellVisualIndicator : MonoBehaviour
    {
        [Header("Tell Icon")]
        [Tooltip("Text label positioned near the client portrait for gameplay-critical tell icons.")]
        [SerializeField] private TMP_Text _iconLabel;

        private enum TellCategory
        {
            None,
            Hostile,
            Withdrawn,
            Deceptive
        }

        private static readonly Color HostileColor = new(0.94f, 0.49f, 0.16f, 1f);
        private static readonly Color WithdrawnColor = new(0.58f, 0.58f, 0.62f, 1f);
        private static readonly Color DeceptiveColor = new(0.58f, 0.35f, 0.72f, 1f);

        private Coroutine _activeTell;

        private void Awake()
        {
            HideIcon();
        }

        private void OnDisable()
        {
            CancelTell();
        }

        public void ShowTell(TellDefinition tell)
        {
            CancelTell();

            if (tell == null || _iconLabel == null || _iconLabel.gameObject == null)
                return;

            TellCategory category = ResolveCategory(tell);
            if (category == TellCategory.None)
                return;

            ConfigureIcon(category, tell.IsSubtleVariant);
            _activeTell = StartCoroutine(ShowTellRoutine(
                Mathf.Max(0f, tell.LeadTimeSeconds),
                tell.IsSubtleVariant ? 0.4f : 1f));

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[TellVisualIndicator] Showing {category} tell for {tell.LeadTimeSeconds:0.##}s.");
#endif
        }

        public void CancelTell()
        {
            if (_activeTell != null)
            {
                StopCoroutine(_activeTell);
                _activeTell = null;
            }

            HideIcon();
        }

        private IEnumerator ShowTellRoutine(float duration, float maximumAlpha)
        {
            if (_iconLabel == null || _iconLabel.gameObject == null)
                yield break;

            if (AccessibilitySettings.ReducedMotion)
            {
                SetAlpha(maximumAlpha);
                yield return new WaitForSecondsRealtime(duration);

                if (_iconLabel != null && _iconLabel.gameObject != null)
                    HideIcon();

                _activeTell = null;
                yield break;
            }

            float fadeIn = Mathf.Min(0.15f, duration);
            float fadeOut = Mathf.Min(0.2f, Mathf.Max(0f, duration - fadeIn));
            float hold = Mathf.Max(0f, duration - fadeIn - fadeOut);

            yield return Fade(0f, maximumAlpha, fadeIn);

            if (hold > 0f)
                yield return new WaitForSecondsRealtime(hold);

            yield return Fade(maximumAlpha, 0f, fadeOut);

            if (_iconLabel != null && _iconLabel.gameObject != null)
                HideIcon();

            _activeTell = null;
        }

        private IEnumerator Fade(float from, float to, float duration)
        {
            if (duration <= 0f)
            {
                SetAlpha(to);
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (_iconLabel == null || _iconLabel.gameObject == null)
                    yield break;

                elapsed += Time.unscaledDeltaTime;
                SetAlpha(Mathf.Lerp(from, to, elapsed / duration));
                yield return null;
            }

            SetAlpha(to);
        }

        private void ConfigureIcon(TellCategory category, bool subtle)
        {
            switch (category)
            {
                case TellCategory.Hostile:
                    _iconLabel.text = "!";
                    _iconLabel.color = HostileColor;
                    break;
                case TellCategory.Withdrawn:
                    _iconLabel.text = "...";
                    _iconLabel.color = WithdrawnColor;
                    break;
                default:
                    _iconLabel.text = "?";
                    _iconLabel.color = DeceptiveColor;
                    break;
            }

            _iconLabel.rectTransform.sizeDelta = subtle
                ? new Vector2(14f, 14f)
                : new Vector2(20f, 20f);
            SetAlpha(0f);
        }

        private void HideIcon()
        {
            if (_iconLabel == null || _iconLabel.gameObject == null)
                return;

            SetAlpha(0f);
            _iconLabel.text = string.Empty;
        }

        private void SetAlpha(float alpha)
        {
            if (_iconLabel == null)
                return;

            Color color = _iconLabel.color;
            color.a = alpha;
            _iconLabel.color = color;
        }

        private static TellCategory ResolveCategory(TellDefinition tell)
        {
            string key = $"{tell.TellType} {tell.AnimationTrigger}".ToLowerInvariant();

            if (ContainsAny(key, "smalltalk", "cooperative"))
                return TellCategory.None;

            if (ContainsAny(
                key,
                "litigious",
                "agitated",
                "straightenpapers",
                "checkwatch",
                "tapfinger"))
            {
                return TellCategory.Hostile;
            }

            if (ContainsAny(
                key,
                "resigned",
                "dissociat",
                "heavysigh",
                "starethrough",
                "vacantstare",
                "shoulderdrop"))
            {
                return TellCategory.Withdrawn;
            }

            return TellCategory.Deceptive;
        }

        private static bool ContainsAny(string source, params string[] values)
        {
            foreach (string value in values)
            {
                if (source.IndexOf(value, StringComparison.Ordinal) >= 0)
                    return true;
            }

            return false;
        }
    }
}
