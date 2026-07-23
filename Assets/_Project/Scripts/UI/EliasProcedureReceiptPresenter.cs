using System;
using System.Collections;
using System.Collections.Generic;
using Desk42.Accessibility;
using Desk42.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Desk42.UI
{
    public enum EliasProcedureReceiptBeatKind
    {
        Action = 0,
        RecordChange = 1,
        MemoryAnchor = 2,
        Processing = 3,
        AppliedDelta = 4,
    }

    public readonly struct EliasProcedureReceiptBeat
    {
        public readonly EliasProcedureReceiptBeatKind Kind;
        public readonly string Text;

        public EliasProcedureReceiptBeat(
            EliasProcedureReceiptBeatKind kind, string text)
        {
            Kind = kind;
            Text = text;
        }
    }

    /// <summary>
    /// Factual, ordered copy generated exclusively from the immutable applied
    /// procedure. It formats the result but never recalculates its effects.
    /// </summary>
    public static class EliasProcedureReceiptSequence
    {
        public static EliasProcedureReceiptBeat[] Build(
            AppliedEliasProcedure result)
        {
            var beats = new List<EliasProcedureReceiptBeat>(7);
            switch (result.ActionId)
            {
                case EliasProcedureActionId.AmendRecord:
                    Require(result.AddressBefore, nameof(result.AddressBefore));
                    Require(result.AddressAfter, nameof(result.AddressAfter));
                    Require(result.MiriamRegistrationReference,
                        nameof(result.MiriamRegistrationReference));
                    Add(beats, EliasProcedureReceiptBeatKind.Action,
                        "RECORD AMENDED");
                    Add(beats, EliasProcedureReceiptBeatKind.RecordChange,
                        $"{ShortAddress(result.AddressBefore)} -> " +
                        $"{ShortAddress(result.AddressAfter)}");
                    Add(beats, EliasProcedureReceiptBeatKind.MemoryAnchor,
                        result.MiriamRegistrationReference);
                    break;

                case EliasProcedureActionId.RetainLegacyUnit:
                    Add(beats, EliasProcedureReceiptBeatKind.Action,
                        "LEGACY UNIT RETAINED");
                    Add(beats, EliasProcedureReceiptBeatKind.RecordChange,
                        $"RECORD HELD AT {ShortAddress(result.AddressAfter)}");
                    break;

                case EliasProcedureActionId.ReferForReview:
                    Add(beats, EliasProcedureReceiptBeatKind.Action,
                        result.AppearanceKey
                            == EliasProofContent.Shift2AppearanceKey
                            ? "PHYSICAL VERIFICATION OPENED"
                            : "REVIEW REFERRED");
                    Add(beats, EliasProcedureReceiptBeatKind.RecordChange,
                        $"RECORD HELD AT {ShortAddress(result.AddressAfter)}");
                    break;

                case EliasProcedureActionId.RequestClarification:
                    Add(beats, EliasProcedureReceiptBeatKind.Action,
                        "CLARIFICATION REQUESTED");
                    Add(beats, EliasProcedureReceiptBeatKind.RecordChange,
                        $"RECORD HELD AT {ShortAddress(result.AddressAfter)}");
                    break;

                default:
                    throw new InvalidOperationException(
                        $"Unsupported Elias procedure receipt action " +
                        $"'{result.ActionId}'.");
            }

            Add(beats, EliasProcedureReceiptBeatKind.Processing,
                "CLAIM ACCEPTED FOR PROCESSING");
            AddAppliedDeltas(beats, result);
            return beats.ToArray();
        }

        private static void AddAppliedDeltas(
            ICollection<EliasProcedureReceiptBeat> beats,
            AppliedEliasProcedure result)
        {
            if (result.CreditsDelta != 0)
            {
                Add(beats, EliasProcedureReceiptBeatKind.AppliedDelta,
                    $"CORPORATE CREDITS {FormatSigned(result.CreditsDelta)}");
            }
            if (!Mathf.Approximately(result.SanityDelta, 0f))
            {
                Add(beats, EliasProcedureReceiptBeatKind.AppliedDelta,
                    $"SANITY {FormatSigned(result.SanityDelta)}");
            }
            if (!Mathf.Approximately(result.SoulIntegrityDelta, 0f))
            {
                Add(beats, EliasProcedureReceiptBeatKind.AppliedDelta,
                    $"SOUL INTEGRITY " +
                    $"{FormatSigned(result.SoulIntegrityDelta)}");
            }
            if (!Mathf.Approximately(
                    result.ComplianceStreakDelta, 0f))
            {
                Add(beats, EliasProcedureReceiptBeatKind.AppliedDelta,
                    $"COMPLIANCE STREAK " +
                    $"{FormatSigned(result.ComplianceStreakDelta)}");
            }
        }

        private static void Add(
            ICollection<EliasProcedureReceiptBeat> beats,
            EliasProcedureReceiptBeatKind kind,
            string text)
            => beats.Add(new EliasProcedureReceiptBeat(kind, text));

        private static string ShortAddress(string address)
        {
            Require(address, nameof(address));
            const string suffix = " Calder House";
            return address.EndsWith(suffix, StringComparison.Ordinal)
                ? address.Substring(0, address.Length - suffix.Length)
                : address;
        }

        private static string FormatSigned(float value)
            => value > 0f
                ? $"+{value:0.##}"
                : value.ToString("0.##");

        private static void Require(string value, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException(
                    $"Applied Elias procedure is missing {fieldName}.");
            }
        }
    }

    /// <summary>
    /// Protected semantic receipt for authored procedures. The overlay is not
    /// governed by FeedbackBudget and temporarily intercepts input so the
    /// memory anchor lands before the player can disposition the claim.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EliasProcedureReceiptPresenter : MonoBehaviour
    {
        [Header("Semantic timing")]
        [SerializeField, Min(0.1f)]
        private float _standardBeatSeconds = 0.72f;
        [SerializeField, Min(0.1f)]
        private float _memoryAnchorSeconds = 1.35f;
        [SerializeField, Min(0.1f)]
        private float _rewardBeatSeconds = 0.90f;
        [SerializeField, Min(0f)]
        private float _finalHoldSeconds = 0.40f;

        private GameObject _root;
        private Image _panel;
        private TMP_Text _heading;
        private TMP_Text _beatText;
        private TMP_Text _progressText;
        private CanvasGroup _canvasGroup;
        private Coroutine _sequenceRoutine;

        public bool IsPresenting => _sequenceRoutine != null;
        public string RenderedBeat
            => _beatText != null ? _beatText.text : string.Empty;

        public event Action<int, EliasProcedureReceiptBeat> BeatPresented;
        public event Action<AppliedEliasProcedure> PresentationCompleted;

        public static EliasProcedureReceiptPresenter Ensure(
            GameObject owner)
        {
            if (owner == null)
                return null;
            var presenter =
                owner.GetComponent<EliasProcedureReceiptPresenter>();
            return presenter != null
                ? presenter
                : owner.AddComponent<EliasProcedureReceiptPresenter>();
        }

        private void Awake() => BuildUI();

        private void OnEnable()
            => RumorMill.OnEliasProcedureApplied += HandleProcedureApplied;

        private void OnDisable()
        {
            RumorMill.OnEliasProcedureApplied -= HandleProcedureApplied;
            if (_sequenceRoutine != null)
                StopCoroutine(_sequenceRoutine);
            _sequenceRoutine = null;
            if (_root != null)
                _root.SetActive(false);
        }

        private void OnDestroy()
        {
            // The screen-space receipt is intentionally a scene-root canvas so
            // a zero-sized overlay owner cannot clip it. Own its cleanup
            // explicitly because it is not a transform child.
            if (_root != null)
                Destroy(_root);
            _root = null;
        }

        public void Present(AppliedEliasProcedure result)
        {
            EliasProcedureReceiptBeat[] beats =
                EliasProcedureReceiptSequence.Build(result);
            if (_sequenceRoutine != null)
                StopCoroutine(_sequenceRoutine);
            _sequenceRoutine =
                StartCoroutine(PlaySequence(result, beats));
        }

        private void HandleProcedureApplied(EliasProcedureAppliedEvent e)
            => Present(e.Result);

        private IEnumerator PlaySequence(
            AppliedEliasProcedure result,
            IReadOnlyList<EliasProcedureReceiptBeat> beats)
        {
            _root.SetActive(true);
            _root.transform.SetAsLastSibling();
            _canvasGroup.alpha = 1f;
            _canvasGroup.blocksRaycasts = true;
            _canvasGroup.interactable = true;
            _heading.text = "CORPOS RECORD SERVICES";

            for (int i = 0; i < beats.Count; i++)
            {
                EliasProcedureReceiptBeat beat = beats[i];
                ApplyBeatVisual(beat);
                _beatText.text = beat.Text;
                _progressText.text =
                    $"RECEIPT {result.ReceiptId}  ·  {i + 1}/{beats.Count}";
                BeatPresented?.Invoke(i, beat);
                yield return new WaitForSecondsRealtime(
                    DurationFor(beat.Kind));
            }

            if (_finalHoldSeconds > 0f)
                yield return new WaitForSecondsRealtime(_finalHoldSeconds);

            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;
            _root.SetActive(false);
            _sequenceRoutine = null;
            PresentationCompleted?.Invoke(result);
        }

        private float DurationFor(EliasProcedureReceiptBeatKind kind)
            => kind switch
            {
                EliasProcedureReceiptBeatKind.MemoryAnchor =>
                    _memoryAnchorSeconds,
                EliasProcedureReceiptBeatKind.AppliedDelta =>
                    _rewardBeatSeconds,
                _ => _standardBeatSeconds,
            };

        private void ApplyBeatVisual(EliasProcedureReceiptBeat beat)
        {
            bool anchor =
                beat.Kind == EliasProcedureReceiptBeatKind.MemoryAnchor;
            bool reward =
                beat.Kind == EliasProcedureReceiptBeatKind.AppliedDelta;
            _panel.color = anchor
                ? new Color(0.34f, 0.23f, 0.08f, 0.99f)
                : reward
                    ? new Color(0.08f, 0.30f, 0.20f, 0.99f)
                    : new Color(0.055f, 0.11f, 0.095f, 0.99f);
            _beatText.color = anchor
                ? new Color(1f, 0.82f, 0.36f, 1f)
                : reward
                    ? new Color(0.42f, 1f, 0.66f, 1f)
                    : new Color(0.96f, 0.92f, 0.80f, 1f);
            _beatText.fontSize =
                AccessibilitySettings.Scaled(anchor ? 43f : 37f);
            _panel.rectTransform.localScale =
                anchor ? Vector3.one * 1.06f : Vector3.one;
        }

        private void BuildUI()
        {
            if (_root != null)
                return;

            _root = new GameObject(
                "EliasProcedureReceiptCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(CanvasGroup),
                typeof(Image));
            // A ScreenSpaceOverlay canvas must not inherit the dimensions of
            // ShiftFeedbackOverlay's marker transform. Keeping it as a scene
            // root makes the semantic receipt occupy the actual screen.
            _root.transform.SetParent(null, false);
            var rootRect = _root.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            Canvas canvas = _root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 4500;
            var scaler = _root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            Image inputShield = _root.GetComponent<Image>();
            inputShield.color = new Color(0f, 0f, 0f, 0.38f);
            inputShield.raycastTarget = true;
            _canvasGroup = _root.GetComponent<CanvasGroup>();

            var panelObject = new GameObject(
                "Receipt", typeof(RectTransform), typeof(Image),
                typeof(Outline));
            panelObject.transform.SetParent(_root.transform, false);
            var panelRect = panelObject.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(780f, 230f);
            _panel = panelObject.GetComponent<Image>();
            var outline = panelObject.GetComponent<Outline>();
            outline.effectColor = new Color(0.82f, 0.66f, 0.25f, 1f);
            outline.effectDistance = new Vector2(3f, -3f);

            _heading = BuildText(
                "Heading", panelObject.transform,
                new Vector2(22f, 176f), new Vector2(-22f, -12f),
                18f, TextAlignmentOptions.TopLeft);
            _heading.color = new Color(0.68f, 0.62f, 0.45f, 1f);

            _beatText = BuildText(
                "Beat", panelObject.transform,
                new Vector2(22f, 50f), new Vector2(-22f, -54f),
                37f, TextAlignmentOptions.Center);
            _beatText.fontStyle = FontStyles.Bold;

            _progressText = BuildText(
                "Progress", panelObject.transform,
                new Vector2(22f, 12f), new Vector2(-22f, -188f),
                14f, TextAlignmentOptions.BottomRight);
            _progressText.color =
                new Color(0.68f, 0.62f, 0.45f, 1f);

            _root.SetActive(false);
        }

        private static TMP_Text BuildText(
            string name,
            Transform parent,
            Vector2 offsetMin,
            Vector2 offsetMax,
            float fontSize,
            TextAlignmentOptions alignment)
        {
            var textObject = new GameObject(
                name, typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            var rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            var text = textObject.GetComponent<TextMeshProUGUI>();
            text.fontSize = AccessibilitySettings.Scaled(fontSize);
            text.alignment = alignment;
            text.enableWordWrapping = true;
            text.raycastTarget = false;
            return text;
        }
    }
}
