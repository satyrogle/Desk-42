using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Desk42.Core;
using Desk42.OfficeSupplies;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Desk42.UI
{
    public enum CascadeValueKind
    {
        Duration,
        CreditCost,
        SoulCost
    }

    /// <summary>
    /// Presents the exact modifier packet that already drove card mechanics.
    /// The readout is semantic feedback and is never dropped by FeedbackBudget.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CascadePresenter : MonoBehaviour
    {
        [Header("Timing")]
        [SerializeField] private CascadeConfig _config;
        [SerializeField] private KeyCode _fastForwardKey = KeyCode.Space;

        [Header("Modifier Chain")]
        [SerializeField] private TMP_Text _modifierLabel;
        [SerializeField] private Slider _durationBar;
        [SerializeField] private Slider _creditCostBar;
        [SerializeField] private Slider _soulCostBar;

        [Header("Result")]
        [SerializeField] private TMP_Text _stampLabel;

        [Header("Audio")]
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private AudioClip _modifierChime;

        public event Action<bool> IntentStateChanged;
        public event Action<CascadeValueKind, ModifierStep> ModifierPresented;
        public event Action<StampPayload> StampPresented;

        private Coroutine _activeSequence;
        private bool _intentActive;
        private GameObject _panelRoot;
        private string _activeCardLabel;
        private readonly List<string> _revealedChains = new();

        private void Awake()
        {
            if (_modifierLabel == null)
                BuildRuntimeUI();
        }

        private void OnEnable()
        {
            if (_modifierLabel == null)
                BuildRuntimeUI();
            RumorMill.OnCardCascadeResolved += HandleCascadeResolved;
            RumorMill.OnRunCompleted += HandleRunCompleted;
        }

        private void OnDisable()
        {
            RumorMill.OnCardCascadeResolved -= HandleCascadeResolved;
            RumorMill.OnRunCompleted -= HandleRunCompleted;

            if (_activeSequence != null)
            {
                StopCoroutine(_activeSequence);
                _activeSequence = null;
            }

            SetIntentState(false);
        }

        private void HandleCascadeResolved(CardCascadeResolvedEvent e)
            => PlaySequence(e.Packet);

        private void HandleRunCompleted(RunCompletedEvent _)
        {
            if (_activeSequence != null)
            {
                StopCoroutine(_activeSequence);
                _activeSequence = null;
            }
            SetIntentState(false);
            if (_panelRoot != null) _panelRoot.SetActive(false);
        }

        public void PlaySequence(SynergyResolutionPacket packet)
        {
            if (_activeSequence != null)
            {
                StopCoroutine(_activeSequence);
                _activeSequence = null;
                SetIntentState(false);
            }

            _activeCardLabel = FormatCard(packet.CardType);
            _activeSequence = StartCoroutine(PlaySequenceRoutine(packet));
        }

        private IEnumerator PlaySequenceRoutine(SynergyResolutionPacket packet)
        {
            SetIntentState(true);
            if (_panelRoot != null) _panelRoot.SetActive(true);
            _revealedChains.Clear();
            if (_stampLabel != null) _stampLabel.text = "CALCULATING";
            RefreshChainText();

            SetBarValue(_durationBar, packet.BaseDuration);
            SetBarValue(_creditCostBar, packet.BaseCreditCost);
            SetBarValue(_soulCostBar, packet.BaseSoulCost);

            yield return new WaitForSeconds(BaseDelay * 1.5f);

            yield return PresentSteps(packet.DurationSteps, CascadeValueKind.Duration,
                _durationBar, packet.BaseDuration, "Duration");
            yield return PresentSteps(packet.CreditCostSteps, CascadeValueKind.CreditCost,
                _creditCostBar, packet.BaseCreditCost, "Corporate cost");
            yield return PresentSteps(packet.SoulCostSteps, CascadeValueKind.SoulCost,
                _soulCostBar, packet.BaseSoulCost, "Soul cost");

            // This confirms a calculation, not an adjudication. APPROVED or
            // DENIED here would imply the card had a morally correct use.
            var stamp = new StampPayload("APPLIED", StampConsequence.None, 0f);
            if (_stampLabel != null) _stampLabel.text = stamp.StampText;
            StampPresented?.Invoke(stamp);

            // Leave the complete chain readable briefly, then return the desk.
            // The latest applied-card result remains persistent below.
            yield return new WaitForSecondsRealtime(1.75f);
            if (_panelRoot != null) _panelRoot.SetActive(false);

            SetIntentState(false);
            _activeSequence = null;
        }

        private IEnumerator PresentSteps(
            List<ModifierStep> steps,
            CascadeValueKind valueKind,
            Slider targetBar,
            float baseValue,
            string label)
        {
            string chain = $"{label}: {FormatValue(baseValue, valueKind)}";

            if (steps == null || steps.Count == 0)
            {
                _revealedChains.Add(chain);
                RefreshChainText();
                yield break;
            }

            foreach (ModifierStep step in steps)
            {
                chain += $" → {step.DisplayName} {FormatDelta(step.Delta, valueKind)}" +
                         $" = {FormatValue(step.NewValue, valueKind)}";
                RefreshChainText(chain);
                SetBarValue(targetBar, step.NewValue);

                if (_audioSource != null && _modifierChime != null)
                    _audioSource.PlayOneShot(_modifierChime);

                ModifierPresented?.Invoke(valueKind, step);

                string comboKey = $"{step.SupplyId}_{step.Zone}";
                bool fastForward = Input.GetKey(_fastForwardKey)
                    || GetSeenComboCount(comboKey) >= AutoFastForwardCount;
                yield return new WaitForSeconds(fastForward ? SkipDelay : BaseDelay);
            }

            _revealedChains.Add(chain);
            RefreshChainText();
        }

        private void RefreshChainText(string activeChain = null)
        {
            if (_modifierLabel == null) return;
            var lines = new List<string>(_revealedChains);
            if (!string.IsNullOrEmpty(activeChain)) lines.Add(activeChain);
            _modifierLabel.text = $"<b>{_activeCardLabel ?? "CARD"} — CONSEQUENCE CHAIN</b>";
            if (lines.Count > 0)
                _modifierLabel.text += "\n" + string.Join("\n", lines);
        }

        private void SetIntentState(bool active)
        {
            if (_intentActive == active) return;
            _intentActive = active;
            IntentStateChanged?.Invoke(active);
        }

        private static void SetBarValue(Slider bar, float value)
        {
            if (bar != null) bar.value = value;
        }

        private static string FormatCard(PunchCardType cardType)
        {
            string value = cardType.ToString();
            return System.Text.RegularExpressions.Regex.Replace(
                value, "([a-z])([A-Z])", "$1 $2").ToUpperInvariant();
        }

        private static string FormatValue(float value, CascadeValueKind kind)
            => kind switch
            {
                CascadeValueKind.Duration => $"{value:0.##}s",
                CascadeValueKind.CreditCost => $"¢{value:0.##}",
                _ => value.ToString("0.##"),
            };

        private static string FormatDelta(float delta, CascadeValueKind kind)
        {
            string sign = delta > 0f ? "+" : delta < 0f ? "−" : "";
            string magnitude = Mathf.Abs(delta).ToString("0.##");
            return kind switch
            {
                CascadeValueKind.Duration => $"{sign}{magnitude}s",
                CascadeValueKind.CreditCost => $"{sign}¢{magnitude}",
                _ => $"{sign}{magnitude}",
            };
        }

        private void BuildRuntimeUI()
        {
            if (_panelRoot != null) return;

            var canvasObject = new GameObject("CascadeCanvas");
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 650;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasObject.AddComponent<GraphicRaycaster>();

            _panelRoot = new GameObject("CascadeReadout");
            _panelRoot.transform.SetParent(canvasObject.transform, false);
            var panelRect = _panelRoot.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0f);
            panelRect.anchorMax = new Vector2(0.5f, 0f);
            panelRect.pivot = new Vector2(0.5f, 0f);
            panelRect.sizeDelta = new Vector2(760f, 170f);
            panelRect.anchoredPosition = new Vector2(0f, 374f);
            var panelImage = _panelRoot.AddComponent<Image>();
            panelImage.color = new Color(0.07f, 0.13f, 0.14f, 0.97f);
            panelImage.raycastTarget = false;
            var outline = _panelRoot.AddComponent<Outline>();
            outline.effectColor = new Color(0.33f, 0.68f, 0.72f, 0.95f);
            outline.effectDistance = new Vector2(2f, -2f);

            var textObject = new GameObject("ChainText");
            textObject.transform.SetParent(_panelRoot.transform, false);
            var textRect = textObject.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(20f, 18f);
            textRect.offsetMax = new Vector2(-110f, -18f);
            _modifierLabel = textObject.AddComponent<TextMeshProUGUI>();
            _modifierLabel.fontSize = Desk42.Accessibility.AccessibilitySettings.Scaled(19f);
            _modifierLabel.color = new Color(0.9f, 0.93f, 0.84f, 1f);
            _modifierLabel.alignment = TextAlignmentOptions.TopLeft;
            _modifierLabel.enableWordWrapping = true;
            _modifierLabel.raycastTarget = false;

            var stampObject = new GameObject("AppliedStamp");
            stampObject.transform.SetParent(_panelRoot.transform, false);
            var stampRect = stampObject.AddComponent<RectTransform>();
            stampRect.anchorMin = new Vector2(1f, 1f);
            stampRect.anchorMax = new Vector2(1f, 1f);
            stampRect.pivot = new Vector2(1f, 1f);
            stampRect.sizeDelta = new Vector2(100f, 42f);
            stampRect.anchoredPosition = new Vector2(-14f, -14f);
            _stampLabel = stampObject.AddComponent<TextMeshProUGUI>();
            _stampLabel.fontSize = Desk42.Accessibility.AccessibilitySettings.Scaled(15f);
            _stampLabel.fontStyle = FontStyles.Bold;
            _stampLabel.color = new Color(0.35f, 0.78f, 0.62f, 1f);
            _stampLabel.alignment = TextAlignmentOptions.Center;
            _stampLabel.raycastTarget = false;

            _panelRoot.SetActive(false);
        }

        private int GetSeenComboCount(string comboKey)
        {
            MetaProgressData meta = GameManager.Instance?.Meta;
            if (meta == null) return 0;

            const string memberName = "SeenComboCount";
            Type metaType = meta.GetType();
            object value = metaType
                .GetField(memberName, BindingFlags.Instance | BindingFlags.Public)
                ?.GetValue(meta)
                ?? metaType
                    .GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public)
                    ?.GetValue(meta);

            if (value is IReadOnlyDictionary<string, int> readOnlyCounts
                && readOnlyCounts.TryGetValue(comboKey, out int readOnlyCount))
                return readOnlyCount;

            if (value is IDictionary<string, int> counts
                && counts.TryGetValue(comboKey, out int count))
                return count;

            return 0;
        }

        private float BaseDelay => _config != null ? _config.BaseDelay : 0.3f;
        private float SkipDelay => _config != null ? _config.SkipDelay : 0.08f;
        private int AutoFastForwardCount => _config != null ? _config.AutoFastForwardCount : 3;
    }
}
