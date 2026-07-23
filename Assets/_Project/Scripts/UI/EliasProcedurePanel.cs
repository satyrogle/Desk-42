using System;
using System.Collections.Generic;
using Desk42.Core;
using Desk42.Encounter;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Desk42.UI
{
    /// <summary>
    /// Functional authored-action row for Elias encounters. Availability is
    /// rendered from EncounterManager's canonical preview API; button clicks
    /// execute through that same validator.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EliasProcedurePanel : MonoBehaviour
    {
        private sealed class ActionButton
        {
            public GameObject Root;
            public Button Button;
            public Image Background;
            public TMP_Text Label;
            public EliasProcedureActionId ActionId;
        }

        private static readonly Color PanelColor =
            new(0.055f, 0.11f, 0.095f, 0.97f);
        private static readonly Color AvailableColor =
            new(0.12f, 0.27f, 0.22f, 1f);
        private static readonly Color AppliedColor =
            new(0.48f, 0.35f, 0.10f, 1f);
        private static readonly Color LockedColor =
            new(0.20f, 0.20f, 0.20f, 1f);
        private static readonly Color PaperColor =
            new(0.95f, 0.92f, 0.80f, 1f);

        private readonly List<ActionButton> _buttons = new();
        private EncounterManager _encounter;
        private ActiveClaimData _claim;
        private TMP_Text _statusLabel;
        private RectTransform _buttonRow;
        private bool _built;

        public static EliasProcedurePanel Ensure(
            EncounterManager encounter, Transform parent)
        {
            if (encounter == null || parent == null)
                return null;

            Transform existing = parent.Find("EliasProcedurePanel");
            EliasProcedurePanel panel = existing != null
                ? existing.GetComponent<EliasProcedurePanel>()
                : null;
            if (panel == null)
            {
                var root = new GameObject(
                    "EliasProcedurePanel",
                    typeof(RectTransform),
                    typeof(Image),
                    typeof(Outline));
                root.transform.SetParent(parent, worldPositionStays: false);
                panel = root.AddComponent<EliasProcedurePanel>();
            }

            panel.Configure(encounter);
            return panel;
        }

        public void SetClaim(ActiveClaimData claim)
        {
            _claim = claim;
            bool requiresProcedure = claim != null
                && (string.Equals(
                        claim.AuthoredAppearanceKey,
                        EliasProofContent.Shift2AppearanceKey,
                        StringComparison.Ordinal)
                    || string.Equals(
                        claim.AuthoredAppearanceKey,
                        EliasProofContent.Shift5AppearanceKey,
                        StringComparison.Ordinal));
            bool isAuthoredElias = claim != null
                && requiresProcedure
                && string.Equals(
                    claim.ClientVariantId,
                    EliasProofContent.CanonicalClaimantId,
                    StringComparison.Ordinal)
                && EliasProofContent.TryGetExpectedPriorVisits(
                    claim.AuthoredAppearanceKey, out _)
                && GameManager.Instance?.EliasProof?.HasActiveSession == true;

            gameObject.SetActive(isAuthoredElias);
            if (isAuthoredElias)
                Refresh();
        }

        public void Clear()
        {
            _claim = null;
            gameObject.SetActive(false);
        }

        private void Configure(EncounterManager encounter)
        {
            _encounter = encounter;
            if (!_built)
                BuildUI();
            Clear();
        }

        private void BuildUI()
        {
            _built = true;
            var rootRect = GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 0f);
            rootRect.anchorMax = new Vector2(0.5f, 0f);
            rootRect.pivot = new Vector2(0.5f, 0f);
            rootRect.anchoredPosition = new Vector2(0f, 250f);
            rootRect.sizeDelta = new Vector2(1040f, 150f);

            Image panelImage = GetComponent<Image>();
            panelImage.color = PanelColor;
            panelImage.raycastTarget = true;
            Outline outline = GetComponent<Outline>();
            outline.effectColor = new Color(0.58f, 0.44f, 0.16f, 1f);
            outline.effectDistance = new Vector2(2f, -2f);

            var statusObject = new GameObject(
                "Status", typeof(RectTransform), typeof(TextMeshProUGUI));
            statusObject.transform.SetParent(transform, false);
            var statusRect = statusObject.GetComponent<RectTransform>();
            statusRect.anchorMin = new Vector2(0f, 1f);
            statusRect.anchorMax = new Vector2(1f, 1f);
            statusRect.pivot = new Vector2(0.5f, 1f);
            statusRect.offsetMin = new Vector2(18f, -38f);
            statusRect.offsetMax = new Vector2(-18f, -8f);
            _statusLabel = statusObject.GetComponent<TextMeshProUGUI>();
            _statusLabel.fontSize = 18f;
            _statusLabel.fontStyle = FontStyles.Bold;
            _statusLabel.alignment = TextAlignmentOptions.Center;
            _statusLabel.color = PaperColor;
            _statusLabel.raycastTarget = false;

            var rowObject = new GameObject(
                "Actions", typeof(RectTransform),
                typeof(HorizontalLayoutGroup));
            rowObject.transform.SetParent(transform, false);
            _buttonRow = rowObject.GetComponent<RectTransform>();
            _buttonRow.anchorMin = new Vector2(0f, 0f);
            _buttonRow.anchorMax = new Vector2(1f, 1f);
            _buttonRow.offsetMin = new Vector2(18f, 14f);
            _buttonRow.offsetMax = new Vector2(-18f, -46f);
            var layout = rowObject.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 14f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
        }

        private void Refresh()
        {
            if (_claim == null || _encounter == null)
            {
                Clear();
                return;
            }

            bool alreadyApplied =
                GameManager.Instance.EliasProof.State
                    .AppliedProcedureAppearanceKeys.Contains(
                        _claim.AuthoredAppearanceKey);
            _statusLabel.text = alreadyApplied
                ? "PROCEDURE APPLIED - APPROVE OR DENY"
                : "PROCEDURE REQUIRED - CHOOSE ONE, THEN APPROVE OR DENY";

            EliasProcedureActionId[] actions =
                string.Equals(
                    _claim.AuthoredAppearanceKey,
                    EliasProofContent.Shift2AppearanceKey,
                    StringComparison.Ordinal)
                    ? new[]
                    {
                        EliasProcedureActionId.AmendRecord,
                        EliasProcedureActionId.RetainLegacyUnit,
                        EliasProcedureActionId.ReferForReview,
                    }
                    : new[]
                    {
                        EliasProcedureActionId.ReferForReview,
                        EliasProcedureActionId.RequestClarification,
                    };

            EnsureButtonCount(actions.Length);
            EliasProcedureActionId appliedAction =
                string.Equals(
                    _claim.AuthoredAppearanceKey,
                    EliasProofContent.Shift2AppearanceKey,
                    StringComparison.Ordinal)
                    ? GameManager.Instance.EliasProof.State
                        .Shift2ProcedureAction
                    : GameManager.Instance.EliasProof.State
                        .Shift5ProcedureAction;

            for (int i = 0; i < _buttons.Count; i++)
            {
                ActionButton view = _buttons[i];
                bool visible = i < actions.Length;
                view.Root.SetActive(visible);
                if (!visible)
                    continue;

                EliasProcedureActionId actionId = actions[i];
                view.ActionId = actionId;
                bool available =
                    _encounter.TryPreviewEliasProcedure(
                        actionId,
                        out ProjectedEliasProcedure projection,
                        out string failureReason);

                view.Button.interactable = available;
                if (alreadyApplied)
                {
                    bool selected = actionId == appliedAction;
                    view.Label.text = selected
                        ? $"{DisplayName(actionId)}\nAPPLIED"
                        : $"{DisplayName(actionId)}\nUNAVAILABLE - PROCEDURE RECORDED";
                    view.Background.color =
                        selected ? AppliedColor : LockedColor;
                }
                else if (available)
                {
                    view.Label.text =
                        BuildPreviewCopy(actionId, projection);
                    view.Background.color = AvailableColor;
                }
                else
                {
                    view.Label.text =
                        BuildLockedCopy(actionId, failureReason);
                    view.Background.color = LockedColor;
                }
            }
        }

        private void EnsureButtonCount(int count)
        {
            while (_buttons.Count < count)
            {
                int index = _buttons.Count;
                var root = new GameObject(
                    $"ProcedureAction{index + 1}",
                    typeof(RectTransform),
                    typeof(Image),
                    typeof(Button),
                    typeof(LayoutElement),
                    typeof(Outline));
                root.transform.SetParent(_buttonRow, false);
                var rect = root.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(310f, 78f);
                var layoutElement = root.GetComponent<LayoutElement>();
                layoutElement.preferredWidth = 310f;
                layoutElement.preferredHeight = 78f;

                var labelObject = new GameObject(
                    "Label", typeof(RectTransform),
                    typeof(TextMeshProUGUI));
                labelObject.transform.SetParent(root.transform, false);
                var labelRect =
                    labelObject.GetComponent<RectTransform>();
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = new Vector2(8f, 5f);
                labelRect.offsetMax = new Vector2(-8f, -5f);
                var label =
                    labelObject.GetComponent<TextMeshProUGUI>();
                label.fontSize = 15f;
                label.fontStyle = FontStyles.Bold;
                label.alignment = TextAlignmentOptions.Center;
                label.color = PaperColor;
                label.raycastTarget = false;

                var button = root.GetComponent<Button>();
                var view = new ActionButton
                {
                    Root = root,
                    Button = button,
                    Background = root.GetComponent<Image>(),
                    Label = label,
                };
                button.targetGraphic = view.Background;
                button.onClick.AddListener(() => OnActionClicked(view));
                Outline outline = root.GetComponent<Outline>();
                outline.effectColor =
                    new Color(0.80f, 0.72f, 0.50f, 0.8f);
                outline.effectDistance = new Vector2(1f, -1f);
                _buttons.Add(view);
            }
        }

        private void OnActionClicked(ActionButton view)
        {
            if (_encounter.TryApplyEliasProcedure(
                    view.ActionId, out _, out string failureReason))
            {
                Refresh();
                return;
            }

            Refresh();
            _statusLabel.text =
                $"PROCEDURE NOT APPLIED - {FormatFailure(failureReason)}";
        }

        private static string BuildPreviewCopy(
            EliasProcedureActionId actionId,
            ProjectedEliasProcedure projection)
            => actionId switch
            {
                EliasProcedureActionId.AmendRecord =>
                    $"AMEND RECORD\n18B -> 18A | STREAK " +
                    $"{FormatSigned(projection.ComplianceStreakDelta)}",
                EliasProcedureActionId.RetainLegacyUnit =>
                    "RETAIN LEGACY UNIT\nADDRESS REMAINS 18B",
                EliasProcedureActionId.ReferForReview =>
                    "REFER FOR REVIEW\nPHYSICAL VERIFICATION",
                EliasProcedureActionId.RequestClarification =>
                    "REQUEST CLARIFICATION\nNO BRANCH CHANGE",
                _ => DisplayName(actionId),
            };

        private static string BuildLockedCopy(
            EliasProcedureActionId actionId, string failureReason)
        {
            if (string.Equals(
                    failureReason,
                    EliasProcedureFailureReason.ToolLockedByBranch.ToString(),
                    StringComparison.Ordinal))
            {
                return actionId switch
                {
                    EliasProcedureActionId.RequestClarification =>
                        "REQUEST CLARIFICATION\nUNAVAILABLE - LEGACY RECORD RETAINED",
                    EliasProcedureActionId.ReferForReview =>
                        "REFER FOR REVIEW\nUNAVAILABLE - VERIFICATION ALREADY OPEN",
                    _ => $"{DisplayName(actionId)}\nUNAVAILABLE",
                };
            }
            return $"{DisplayName(actionId)}\nUNAVAILABLE - " +
                $"{FormatFailure(failureReason)}";
        }

        private static string DisplayName(
            EliasProcedureActionId actionId)
            => actionId switch
            {
                EliasProcedureActionId.AmendRecord => "AMEND RECORD",
                EliasProcedureActionId.RetainLegacyUnit =>
                    "RETAIN LEGACY UNIT",
                EliasProcedureActionId.ReferForReview =>
                    "REFER FOR REVIEW",
                EliasProcedureActionId.RequestClarification =>
                    "REQUEST CLARIFICATION",
                _ => "UNSPECIFIED PROCEDURE",
            };

        private static string FormatFailure(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
                return "UNAVAILABLE";
            return reason switch
            {
                "ProcedureAlreadyApplied" => "PROCEDURE RECORDED",
                "AppearanceNotRecorded" => "APPEARANCE NOT RECORDED",
                "NoActiveRun" => "NO ACTIVE RUN",
                _ => reason.ToUpperInvariant(),
            };
        }

        private static string FormatSigned(float value)
            => value > 0f
                ? $"+{value:0.##}"
                : value.ToString("0.##");
    }
}
