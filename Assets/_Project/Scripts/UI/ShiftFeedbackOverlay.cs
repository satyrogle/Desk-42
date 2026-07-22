// ============================================================
// DESK 42 — Shift Feedback Overlay (MonoBehaviour)
//
// Self-bootstrapping visual feedback layer. Subscribes to
// RumorMill events and shows animated toast messages for:
//   - Persistent factual claim receipts (APPROVE / DENY / LIQUIFY)
//   - Phase transitions (MORNING → LUNCH → AFTERNOON → OVERTIME)
//   - Credit gains/losses
//
// Attach to the ShiftUI root GameObject (auto-added by
// ShiftSceneAutoLayout if missing).
// ============================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using Desk42.Core;
using Desk42.Encounter;
using Desk42.OfficeSupplies;

namespace Desk42.UI
{
    [DisallowMultipleComponent]
    public sealed class ShiftFeedbackOverlay : MonoBehaviour
    {
        // ── Config ────────────────────────────────────────────

        private const float TOAST_DURATION   = 1.8f;
        private const float TOAST_FADE_TIME  = 0.4f;
        private const int   MAX_ACTIVE_TOASTS = 4;

        // ── State ─────────────────────────────────────────────

        private Transform _toastContainer;
        private Transform _uiRoot;
        private GameObject _receiptPanel;
        private TMP_Text _receiptText;
        private GameObject _obligationsPanel;
        private TMP_Text _obligationsText;
        private GameObject _officeModifiersPanel;
        private TMP_Text _officeModifiersText;
        private GameObject _clientModifiersPanel;
        private TMP_Text _clientModifiersText;
        private GameObject _injectedStatePanel;
        private TMP_Text _injectedStateText;
        private string _latestReceiptText;
        private ClaimResolutionKind _activeProjectionKind;
        private EncounterManager _encounter;
        private int _lastObligationCredits = int.MinValue;
        private int _lastObligationCount = -1;
        private bool _lastObligationsApplied;
        private readonly Queue<GameObject> _activeToasts = new();
        private readonly HashSet<string> _projectedModifierKeys = new();
        private readonly HashSet<string> _appliedModifierKeys = new();
        private bool _cardProjectionVisible;
        private bool _concealedRiskProjected;
        private float _modifierRefreshTimer;

        public string RenderedOfficeModifiers
            => _officeModifiersText != null ? _officeModifiersText.text : "";
        public string RenderedClientModifiers
            => _clientModifiersText != null ? _clientModifiersText.text : "";
        public string RenderedInjectedState
            => _injectedStateText != null ? _injectedStateText.text : "";

        // ── Colors ────────────────────────────────────────────

        private static readonly Color ApproveColor = new(0.2f, 0.75f, 0.3f);
        private static readonly Color DenyColor    = new(0.85f, 0.2f, 0.2f);
        private static readonly Color SlamColor    = new(0.3f, 0.6f, 0.95f);
        private static readonly Color JamColor     = new(1f, 0.5f, 0.1f);
        private static readonly Color PhaseColor   = new(0.9f, 0.85f, 0.5f);
        private static readonly Color CreditColor  = new(0.95f, 0.85f, 0.4f);
        private static readonly Color BlockColor   = new(0.6f, 0.25f, 0.8f);

        // ── Unity Lifecycle ───────────────────────────────────

        private void Awake()
        {
            EnsureCanvas();
            BuildToastContainer();
            BuildReceiptPanel();
            BuildObligationsPanel();
            BuildModifierRows();
            BuildInjectedStateMarker();
            WireDecisionPreviewTriggers();
        }

        private void Start() => RefreshObligations(force: true);

        private void Update()
        {
            RefreshObligations(force: false);
            RefreshClaimProjection();
            RefreshInjectedStateMarker();
            _modifierRefreshTimer += Time.unscaledDeltaTime;
            if (_modifierRefreshTimer >= 0.25f)
            {
                _modifierRefreshTimer = 0f;
                RefreshModifierRows();
            }
        }

        private void OnEnable()
        {
            RumorMill.OnClaimResolved       += HandleClaimResolved;
            RumorMill.OnShiftPhaseChanged   += HandlePhaseChanged;
            RumorMill.OnCounterTraitGenerated += HandleCounterTrait;
            RumorMill.OnCardPreview         += HandleCardPreview;
            RumorMill.OnCardSlammed         += HandleCardSlammed;
            RumorMill.OnClaimQueued         += HandleClaimQueued;
        }

        private void OnDisable()
        {
            RumorMill.OnClaimResolved       -= HandleClaimResolved;
            RumorMill.OnShiftPhaseChanged   -= HandlePhaseChanged;
            RumorMill.OnCounterTraitGenerated -= HandleCounterTrait;
            RumorMill.OnCardPreview         -= HandleCardPreview;
            RumorMill.OnCardSlammed         -= HandleCardSlammed;
            RumorMill.OnClaimQueued         -= HandleClaimQueued;
        }

        private void BuildModifierRows()
        {
            BuildModifierRow("OfficeModifiers", new Vector2(0f, 0f),
                new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(16f, 92f), out _officeModifiersPanel,
                out _officeModifiersText);
            BuildModifierRow("ClientModifiers", new Vector2(1f, 0f),
                new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-16f, 92f), out _clientModifiersPanel,
                out _clientModifiersText);
            RefreshModifierRows();
        }

        private void BuildInjectedStateMarker()
        {
            _injectedStatePanel = new GameObject("ActiveInjectedState");
            _injectedStatePanel.transform.SetParent(_uiRoot, false);
            var rt = _injectedStatePanel.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(560f, 70f);
            rt.anchoredPosition = new Vector2(0f, -100f);

            var background = _injectedStatePanel.AddComponent<Image>();
            background.color = new Color(0.055f, 0.11f, 0.12f, 0.96f);
            background.raycastTarget = false;
            var outline = _injectedStatePanel.AddComponent<Outline>();
            outline.effectColor = new Color(0.95f, 0.76f, 0.3f, 0.95f);
            outline.effectDistance = new Vector2(2f, -2f);

            var textObject = new GameObject("InjectedStateText");
            textObject.transform.SetParent(_injectedStatePanel.transform, false);
            var textRt = textObject.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(12f, 6f);
            textRt.offsetMax = new Vector2(-12f, -6f);

            _injectedStateText = textObject.AddComponent<TextMeshProUGUI>();
            _injectedStateText.fontSize =
                Desk42.Accessibility.AccessibilitySettings.Scaled(19f);
            _injectedStateText.fontStyle = FontStyles.Bold;
            _injectedStateText.color = new Color(0.95f, 0.88f, 0.64f, 1f);
            _injectedStateText.alignment = TextAlignmentOptions.Center;
            _injectedStateText.enableWordWrapping = true;
            _injectedStateText.raycastTarget = false;
            _injectedStatePanel.SetActive(false);
        }

        private void RefreshInjectedStateMarker()
        {
            if (_injectedStatePanel == null) return;
            if (_encounter == null)
                _encounter = Desk42Services.Get<EncounterManager>()
                    ?? FindObjectOfType<EncounterManager>();

            var client = _encounter?.ActiveClient;
            if (client == null
                || !client.TryGetActiveInjectedState(out var active))
            {
                _injectedStateText.text = "";
                _injectedStatePanel.SetActive(false);
                return;
            }

            string time = float.IsPositiveInfinity(active.TimeRemaining)
                ? "UNTIL CLEARED"
                : $"{active.TimeRemaining:0.0}s";
            string stack = active.StackDepth > 1
                ? $"\nTOP EFFECT · {active.StackDepth} STACKED"
                : "";
            _injectedStateText.text =
                $"{active.DisplayName} · {time}{stack}";
            _injectedStatePanel.SetActive(true);
            _injectedStatePanel.transform.SetAsLastSibling();
        }

        private void BuildModifierRow(string objectName,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 position, out GameObject panel, out TMP_Text label)
        {
            panel = new GameObject(objectName);
            panel.transform.SetParent(_uiRoot, false);
            var rt = panel.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.sizeDelta = new Vector2(390f, 92f);
            rt.anchoredPosition = position;

            var background = panel.AddComponent<Image>();
            background.color = new Color(0.055f, 0.11f, 0.12f, 0.94f);
            background.raycastTarget = false;
            var outline = panel.AddComponent<Outline>();
            outline.effectColor = new Color(0.33f, 0.68f, 0.72f, 0.82f);
            outline.effectDistance = new Vector2(2f, -2f);

            var textObject = new GameObject("ModifierText");
            textObject.transform.SetParent(panel.transform, false);
            var textRt = textObject.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(12f, 8f);
            textRt.offsetMax = new Vector2(-12f, -8f);

            label = textObject.AddComponent<TextMeshProUGUI>();
            label.fontSize = Desk42.Accessibility.AccessibilitySettings.Scaled(18f);
            label.color = new Color(0.87f, 0.9f, 0.8f, 1f);
            label.alignment = TextAlignmentOptions.TopLeft;
            label.enableWordWrapping = true;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.raycastTarget = false;
        }

        private void HandleCardPreview(CardPreviewEvent e)
        {
            _cardProjectionVisible = e.IsVisible;
            _projectedModifierKeys.Clear();
            _concealedRiskProjected = false;
            if (e.IsVisible)
            {
                CollectChangedSources(e.Projection.Cascade,
                    _projectedModifierKeys);
                if (e.Projection.IsBlockingModifierRevealed
                    && !string.IsNullOrWhiteSpace(e.Projection.BlockingModifierId))
                {
                    _projectedModifierKeys.Add(SourceKey(
                        ModifierSourceKind.CounterTrait,
                        e.Projection.BlockingModifierId));
                }
                _concealedRiskProjected = e.Projection.HasConcealedBlockRisk;
            }
            RefreshModifierRows();
        }

        private void HandleCardSlammed(CardSlammedEvent e)
        {
            _cardProjectionVisible = false;
            _projectedModifierKeys.Clear();
            _appliedModifierKeys.Clear();
            CollectChangedSources(e.Result.Cascade, _appliedModifierKeys);
            if (!string.IsNullOrWhiteSpace(e.BlockingModifierId))
                _appliedModifierKeys.Add(SourceKey(
                    ModifierSourceKind.CounterTrait,
                    e.BlockingModifierId));
            _concealedRiskProjected = false;
            RefreshModifierRows();
        }

        private void HandleClaimQueued(ClaimQueuedEvent _)
        {
            _appliedModifierKeys.Clear();
            _projectedModifierKeys.Clear();
            _cardProjectionVisible = false;
            _concealedRiskProjected = false;
            RefreshModifierRows();
        }

        private static void CollectChangedSources(
            SynergyResolutionPacket packet, HashSet<string> destination)
        {
            CollectChangedSources(packet.DurationSteps, destination);
            CollectChangedSources(packet.CreditCostSteps, destination);
            CollectChangedSources(packet.SoulCostSteps, destination);
        }

        private static void CollectChangedSources(
            List<ModifierStep> steps, HashSet<string> destination)
        {
            if (steps == null) return;
            foreach (var step in steps)
            {
                if (!step.Changed || string.IsNullOrWhiteSpace(step.SourceId))
                    continue;
                destination.Add(step.SourceKey);
            }
        }

        private void RefreshModifierRows()
        {
            if (_officeModifiersText == null || _clientModifiersText == null)
                return;

            var highlighted = _cardProjectionVisible
                ? _projectedModifierKeys
                : _appliedModifierKeys;
            var office = new List<string>();
            var run = GameManager.Instance?.Run;
            if (run?.Archetype != null)
            {
                office.Add(FormatModifierChip(
                    run.Archetype.DisplayName,
                    SourceKey(ModifierSourceKind.Archetype,
                        run.Archetype.ArchetypeId), highlighted));
            }

            var supplies = GameManager.Instance?.Supplies?.AllActive;
            if (supplies != null)
            {
                foreach (var supply in supplies)
                {
                    if (supply?.Data == null) continue;
                    office.Add(FormatModifierChip(supply.Data.DisplayName,
                        SourceKey(ModifierSourceKind.Supply,
                            supply.Data.SupplyId), highlighted));
                }
            }

            if (run?.ActiveVows != null)
            {
                foreach (var vow in run.ActiveVows)
                {
                    if (vow == null || string.IsNullOrWhiteSpace(vow.VowId))
                        continue;
                    string rank = vow.Rank > 1 ? $" {vow.Rank}" : "";
                    office.Add(FormatModifierChip(
                        $"{FormatIdentifier(vow.VowId)}{rank}",
                        SourceKey(ModifierSourceKind.Vow, vow.VowId),
                        highlighted));
                }
            }

            if (run != null)
            {
                AddFactionChip(office, run, FactionID.Legal, "LEGAL",
                    highlighted);
                AddFactionChip(office, run, FactionID.Accounting,
                    "ACCOUNTING", highlighted);
                if (!string.IsNullOrWhiteSpace(run.EscalatingRegulationCardId))
                {
                    office.Add(FormatModifierChip(
                        $"REG: {run.EscalatingRegulationCardId}",
                        SourceKey(ModifierSourceKind.Regulation,
                            run.EscalatingRegulationCardId), highlighted));
                }
            }

            if (!Mathf.Approximately(
                OfficeEnvironmentState.GetInjectionDurationMultiplier(), 1f))
            {
                office.Add(FormatModifierChip(
                    OfficeEnvironmentState.GetTemperatureState(),
                    SourceKey(ModifierSourceKind.Environment,
                        "office_temperature"), highlighted));
            }

            var client = new List<string>();
            if (_encounter == null)
                _encounter = Desk42Services.Get<EncounterManager>()
                    ?? FindObjectOfType<EncounterManager>();
            var activeClient = _encounter?.ActiveClient;
            if (activeClient != null)
            {
                string moodId = $"state_{activeClient.CurrentMoodState.ToString().ToLowerInvariant()}";
                client.Add(FormatModifierChip(
                    $"MOOD: {activeClient.CurrentMoodState}",
                    SourceKey(ModifierSourceKind.ClientState, moodId),
                    highlighted));
            }

            var snapshots = activeClient?.GetModifierSnapshot();
            if (snapshots != null)
            {
                foreach (var modifier in snapshots)
                {
                    if (!modifier.IsRevealed)
                    {
                        bool activeRisk = _cardProjectionVisible
                            && _concealedRiskProjected;
                        client.Add(activeRisk
                            ? "<color=#F4D35E><b>[? MAY BLOCK]</b></color>"
                            : "<color=#87938C>[? CONCEALED]</color>");
                        continue;
                    }

                    string label = FormatIdentifier(modifier.DisplayName);
                    if (!string.IsNullOrWhiteSpace(modifier.BlockedCardType))
                        label += $" Â· BLOCKS {FormatIdentifier(modifier.BlockedCardType)}";
                    client.Add(FormatModifierChip(label,
                        SourceKey(ModifierSourceKind.CounterTrait,
                            modifier.SourceId), highlighted));
                }
                if (snapshots.Count == 0)
                    client.Add("<color=#87938C>[NO KNOWN TRAITS]</color>");
            }

            _officeModifiersText.text = "<b>OFFICE MODIFIERS</b>\n" +
                (office.Count > 0 ? string.Join(" ", office) : "<color=#87938C>[NONE]</color>");
            _clientModifiersText.text = "<b>CLIENT MODIFIERS</b>\n" +
                (client.Count > 0 ? string.Join(" ", client) : "<color=#87938C>[NO KNOWN TRAITS]</color>");
            _officeModifiersPanel.SetActive(true);
            _clientModifiersPanel.SetActive(true);
        }

        private static void AddFactionChip(List<string> output,
            RunStateController run, FactionID faction, string label,
            HashSet<string> highlighted)
        {
            float reputation = run.GetFactionRep(faction);
            if (reputation <= 50f && reputation >= -50f) return;
            string standing = reputation > 50f ? "FAVOUR" : "DISFAVOUR";
            output.Add(FormatModifierChip($"{label} {standing}",
                SourceKey(ModifierSourceKind.Faction,
                    faction.ToString().ToLowerInvariant()), highlighted));
        }

        private static string FormatModifierChip(string label, string sourceKey,
            HashSet<string> highlighted)
        {
            string safeLabel = FormatIdentifier(label);
            return highlighted.Contains(sourceKey)
                ? $"<color=#F4D35E><b>[{safeLabel}]</b></color>"
                : $"<color=#B7C5BB>[{safeLabel}]</color>";
        }

        private static string SourceKey(ModifierSourceKind kind, string sourceId)
            => $"{kind}:{sourceId}";

        private static string FormatIdentifier(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "MODIFIER";
            var result = new System.Text.StringBuilder(value.Length + 8);
            for (int i = 0; i < value.Length; i++)
            {
                char current = value[i] == '_' ? ' ' : value[i];
                if (i > 0 && char.IsUpper(current)
                    && char.IsLower(value[i - 1]))
                    result.Append(' ');
                result.Append(char.ToUpperInvariant(current));
            }
            return result.ToString();
        }

        // ── Toast Container ───────────────────────────────────

        private void EnsureCanvas()
        {
            var parentCanvas = GetComponentInParent<Canvas>();
            if (parentCanvas != null)
            {
                // Scene repair may leave this component on a scaled/off-screen
                // RectTransform. Parent generated UI to the full canvas itself.
                _uiRoot = parentCanvas.transform;
                return;
            }

            var canvasObject = new GameObject("ShiftFeedbackCanvas");
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 625;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasObject.AddComponent<GraphicRaycaster>();
            _uiRoot = canvasObject.transform;
        }

        private void BuildToastContainer()
        {
            var go = new GameObject("ToastContainer");
            go.transform.SetParent(_uiRoot, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(500, 300);
            rt.anchoredPosition = Vector2.zero;

            var vlg = go.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment        = TextAnchor.MiddleCenter;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.spacing = 8;
            vlg.padding = new RectOffset(0, 0, 0, 0);

            _toastContainer = go.transform;
        }

        // ── Event Handlers ────────────────────────────────────

        private void HandleClaimResolved(ClaimResolvedEvent e)
        {
#if DEVELOPMENT_BUILD
            Debugging.ConsensusAudit.MarkFeedbackOverlay();
#endif
            _activeProjectionKind = ClaimResolutionKind.Unspecified;
            ShowClaimReceipt(e.Result);
        }

        private static string FormatSignedCredits(int delta)
            => delta > 0 ? $"+¢{delta}" : delta < 0 ? $"-¢{-delta}" : "¢0";

        private void BuildReceiptPanel()
        {
            _receiptPanel = new GameObject("LatestClaimReceipt");
            _receiptPanel.transform.SetParent(_uiRoot, false);
            var rt = _receiptPanel.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.sizeDelta = new Vector2(420f, 248f);
            rt.anchoredPosition = new Vector2(-24f, -138f);

            var paper = _receiptPanel.AddComponent<Image>();
            paper.color = new Color(0.90f, 0.87f, 0.75f, 0.97f);
            paper.raycastTarget = false;
            var outline = _receiptPanel.AddComponent<Outline>();
            outline.effectColor = new Color(0.12f, 0.17f, 0.18f, 0.95f);
            outline.effectDistance = new Vector2(2f, -2f);

            var textObject = new GameObject("ReceiptText");
            textObject.transform.SetParent(_receiptPanel.transform, false);
            var textRt = textObject.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(20f, 16f);
            textRt.offsetMax = new Vector2(-20f, -16f);

            _receiptText = textObject.AddComponent<TextMeshProUGUI>();
            _receiptText.fontSize = Desk42.Accessibility.AccessibilitySettings.Scaled(21f);
            _receiptText.color = new Color(0.08f, 0.13f, 0.14f, 1f);
            _receiptText.alignment = TextAlignmentOptions.TopLeft;
            _receiptText.enableWordWrapping = true;
            _receiptText.raycastTarget = false;
            _receiptPanel.SetActive(false);
        }

        private void BuildObligationsPanel()
        {
            _obligationsPanel = new GameObject("PersonalObligations");
            _obligationsPanel.transform.SetParent(_uiRoot, false);
            var rt = _obligationsPanel.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(650f, 96f);
            rt.anchoredPosition = new Vector2(16f, -66f);

            var paper = _obligationsPanel.AddComponent<Image>();
            paper.color = new Color(0.08f, 0.15f, 0.16f, 0.96f);
            paper.raycastTarget = false;
            var outline = _obligationsPanel.AddComponent<Outline>();
            outline.effectColor = new Color(0.73f, 0.64f, 0.37f, 0.95f);
            outline.effectDistance = new Vector2(2f, -2f);

            var textObject = new GameObject("ObligationsText");
            textObject.transform.SetParent(_obligationsPanel.transform, false);
            var textRt = textObject.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(14f, 8f);
            textRt.offsetMax = new Vector2(-14f, -8f);

            _obligationsText = textObject.AddComponent<TextMeshProUGUI>();
            _obligationsText.fontSize =
                Desk42.Accessibility.AccessibilitySettings.Scaled(20f);
            _obligationsText.color = new Color(0.91f, 0.88f, 0.74f, 1f);
            _obligationsText.alignment = TextAlignmentOptions.TopLeft;
            _obligationsText.enableWordWrapping = true;
            _obligationsText.raycastTarget = false;
            _obligationsPanel.SetActive(false);
        }

        private void RefreshObligations(bool force)
        {
            var data = GameManager.Instance?.Run?.RawData;
            var obligations = data?.PersonalObligations;
            if (obligations == null || obligations.Count == 0)
            {
                if (_obligationsPanel != null) _obligationsPanel.SetActive(false);
                return;
            }

            if (!force
                && _lastObligationCredits == data.CorporateCredits
                && _lastObligationCount == obligations.Count
                && _lastObligationsApplied == data.ObligationsApplied)
            {
                return;
            }

            _lastObligationCredits = data.CorporateCredits;
            _lastObligationCount = obligations.Count;
            _lastObligationsApplied = data.ObligationsApplied;

            int totalDue = 0;
            int totalPaid = 0;
            int totalShort = 0;
            var labels = new List<string>();
            foreach (var obligation in obligations)
            {
                if (obligation == null) continue;
                totalDue += obligation.Amount;
                totalPaid += obligation.AmountPaid;
                totalShort += obligation.AmountShort;
                labels.Add($"{obligation.Label} ¢{obligation.Amount}");
            }

            if (data.ObligationsApplied)
            {
                _obligationsText.text =
                    "<b>CLOCK-OUT OBLIGATIONS — APPLIED</b>\n" +
                    $"Paid ¢{totalPaid}  ·  Short <color=#E84A4A>¢{totalShort}</color>";
            }
            else
            {
                int projectedShortfall = Mathf.Max(0, totalDue - data.CorporateCredits);
                string pressure = projectedShortfall > 0
                    ? $"SHORT <color=#E84A4A>¢{projectedShortfall}</color>"
                    : $"AFTER <color=#5DD58A>¢{data.CorporateCredits - totalDue}</color>";
                _obligationsText.text =
                    "<b>PERSONAL OBLIGATIONS · DUE AT CLOCK-OUT</b>\n" +
                    string.Join(" · ", labels) + "\n" +
                    $"Due ¢{totalDue}  ·  Credits ¢{data.CorporateCredits}  ·  {pressure}";
            }

            _obligationsPanel.SetActive(true);
            _obligationsPanel.transform.SetAsLastSibling();
        }

        private void WireDecisionPreviewTriggers()
        {
            WireDecisionPreviewTrigger("ApproveBtn", ClaimResolutionKind.Approve);
            WireDecisionPreviewTrigger("DenyBtn", ClaimResolutionKind.Deny);
        }

        private void WireDecisionPreviewTrigger(
            string objectName, ClaimResolutionKind kind)
        {
            var buttonObject = GameObject.Find(objectName);
            if (buttonObject == null) return;

            var trigger = buttonObject.GetComponent<ClaimDecisionPreviewTrigger>();
            if (trigger == null)
                trigger = buttonObject.AddComponent<ClaimDecisionPreviewTrigger>();
            trigger.Initialize(this, kind);
        }

        public void BeginClaimProjection(ClaimResolutionKind kind)
        {
            _activeProjectionKind = kind;
            RefreshClaimProjection();
        }

        public void EndClaimProjection(ClaimResolutionKind kind)
        {
            if (_activeProjectionKind != kind) return;
            _activeProjectionKind = ClaimResolutionKind.Unspecified;
            _receiptText.text = _latestReceiptText ?? "";
            _receiptPanel.SetActive(!string.IsNullOrWhiteSpace(_latestReceiptText));
        }

        private void RefreshClaimProjection()
        {
            if (_activeProjectionKind == ClaimResolutionKind.Unspecified) return;
            if (_receiptPanel == null) BuildReceiptPanel();

            if (_encounter == null)
                _encounter = Desk42Services.Get<EncounterManager>()
                    ?? FindObjectOfType<EncounterManager>();

            string unavailableReason = null;
            if (_encounter == null
                || !_encounter.TryPreviewResolution(
                    _activeProjectionKind,
                    out var projection,
                    out unavailableReason))
            {
                _receiptText.text =
                    $"<b>{_activeProjectionKind.ToString().ToUpperInvariant()} · UNAVAILABLE</b>\n" +
                    (string.IsNullOrWhiteSpace(unavailableReason)
                        ? "No active claim."
                        : unavailableReason);
                _receiptPanel.SetActive(true);
                return;
            }

            ShowClaimProjection(projection);
        }

        private void ShowClaimProjection(ProjectedClaimResolution result)
        {
            string quota = result.QuotaRequired > 0
                ? $"{result.QuotaAfter}/{result.QuotaRequired}"
                : result.QuotaAfter.ToString();
            string streak = Mathf.Approximately(
                    result.ComplianceStreakBefore, result.ComplianceStreakAfter)
                ? $"{result.ComplianceStreakAfter:0.0}x"
                : $"{result.ComplianceStreakBefore:0.0}x → {result.ComplianceStreakAfter:0.0}x";

            _receiptText.text =
                $"<b>{result.Kind.ToString().ToUpperInvariant()} · EXPECTED</b>\n" +
                $"Corporate credits  {FormatSignedCredits(result.CreditsDelta)}\n" +
                $"Sanity  {FormatSignedAmount(result.SanityDelta)}\n" +
                $"Soul integrity  {FormatSignedAmount(result.SoulIntegrityDelta)}\n" +
                $"Dark intelligence  {FormatSignedAmount(result.DarkIntelligenceDelta)}\n" +
                $"Quota  {quota}\n" +
                $"Compliance streak  {streak}\n" +
                "<i>Projection — receipt confirms what lands.</i>";
            _receiptPanel.SetActive(true);
            _receiptPanel.transform.SetAsLastSibling();
        }

        private void ShowClaimReceipt(AppliedClaimResolution result)
        {
            if (_receiptPanel == null) BuildReceiptPanel();

            string heading = result.Kind switch
            {
                ClaimResolutionKind.Approve => "CLAIM APPROVED",
                ClaimResolutionKind.Deny => "CLAIM DENIED",
                ClaimResolutionKind.Liquify => "CLAIM LIQUIFIED",
                _ => "CLAIM RESOLVED",
            };

            string quota = result.QuotaRequired > 0
                ? $"{result.QuotaAfter}/{result.QuotaRequired}"
                : result.QuotaAfter.ToString();
            string streak = Mathf.Approximately(
                    result.ComplianceStreakBefore, result.ComplianceStreakAfter)
                ? $"{result.ComplianceStreakAfter:0.0}x"
                : $"{result.ComplianceStreakBefore:0.0}x → {result.ComplianceStreakAfter:0.0}x";

            _latestReceiptText =
                $"<b>{heading}</b>\n" +
                $"Corporate credits  {FormatSignedCredits(result.CreditsDelta)}\n" +
                $"Sanity  {FormatSignedAmount(result.SanityDelta)}\n" +
                $"Soul integrity  {FormatSignedAmount(result.SoulIntegrityDelta)}\n" +
                $"Dark intelligence  {FormatSignedAmount(result.DarkIntelligenceDelta)}\n" +
                $"Quota  {quota}\n" +
                $"Compliance streak  {streak}";
            _receiptText.text = _latestReceiptText;
            _receiptPanel.SetActive(true);
            _receiptPanel.transform.SetAsLastSibling();
        }

        private static string FormatSignedAmount(float delta)
        {
            if (Mathf.Approximately(delta, 0f)) return "0";
            string sign = delta > 0f ? "+" : "−";
            return $"{sign}{Mathf.Abs(delta):0.##}";
        }

        private void HandleCounterTrait(CounterTraitGeneratedEvent e)
        {
            ShowToast($"⚡ MUTATION: {e.CounterTraitId}", BlockColor, 24);
        }

        private void HandlePhaseChanged(ShiftPhaseChangedEvent e)
        {
            string phaseName = e.Current switch
            {
                ShiftPhase.MorningBlock   => "☀ MORNING BLOCK",
                ShiftPhase.LunchBreak     => "☕ LUNCH BREAK",
                ShiftPhase.AfternoonBlock => "◑ AFTERNOON BLOCK",
                ShiftPhase.Overtime       => "⚡ OVERTIME",
                ShiftPhase.ClockOut       => "🏁 SHIFT COMPLETE",
                _                         => e.Current.ToString().ToUpper(),
            };

            ShowToast(phaseName, PhaseColor, 32);
        }

        // ── Toast Spawning ────────────────────────────────────

        private void ShowToast(string message, Color color, float fontSize)
        {
            // Throttle: at most one toast per FeedbackBudget window.
            // Drops the toast (doesn't queue) — toasts that lose this
            // gate are typically the duplicate "card filed" type which
            // are also covered by CardSlamFeedback's own flash.
            if (!FeedbackBudget.RequestBurst(FeedbackKind.Toast)) return;

            if (_toastContainer == null) return;

            // Cap active toasts
            while (_activeToasts.Count >= MAX_ACTIVE_TOASTS)
            {
                var old = _activeToasts.Dequeue();
                if (old != null) Destroy(old);
            }

            var go = new GameObject("Toast");
            go.transform.SetParent(_toastContainer, false);

            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(480, fontSize + 20);

            // Semi-transparent background pill
            var bg = go.AddComponent<Image>();
            bg.color = new Color(0, 0, 0, 0.7f);
            bg.raycastTarget = false;

            // Add rounded corners look via outline
            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(color.r, color.g, color.b, 0.5f);
            outline.effectDistance = new Vector2(2, 2);

            // Text label
            var labelGO = new GameObject("Label");
            labelGO.transform.SetParent(go.transform, false);
            var lrt = labelGO.AddComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(12, 4);
            lrt.offsetMax = new Vector2(-12, -4);

            var tmp = labelGO.AddComponent<TextMeshProUGUI>();
            tmp.text      = message;
            tmp.fontSize  = Desk42.Accessibility.AccessibilitySettings.Scaled(fontSize);
            tmp.color     = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = FontStyles.Bold;
            tmp.enableWordWrapping = false;
            tmp.overflowMode = TextOverflowModes.Truncate;
            tmp.raycastTarget = false;

            var cg = go.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            cg.blocksRaycasts = false;
            cg.interactable   = false;

            _activeToasts.Enqueue(go);
            StartCoroutine(AnimateToast(go, cg));
        }

        private IEnumerator AnimateToast(GameObject go, CanvasGroup cg)
        {
            if (go == null || cg == null) yield break;

            // Fade in
            float t = 0f;
            while (t < TOAST_FADE_TIME)
            {
                t += Time.deltaTime;
                if (cg != null) cg.alpha = Mathf.Lerp(0f, 1f, t / TOAST_FADE_TIME);
                yield return null;
            }
            if (cg != null) cg.alpha = 1f;

            // Hold
            yield return new WaitForSeconds(TOAST_DURATION - TOAST_FADE_TIME * 2f);

            // Fade out + slide up
            t = 0f;
            var rt = go?.GetComponent<RectTransform>();
            Vector2 startPos = rt != null ? rt.anchoredPosition : Vector2.zero;
            while (t < TOAST_FADE_TIME)
            {
                t += Time.deltaTime;
                float p = t / TOAST_FADE_TIME;
                if (cg != null) cg.alpha = Mathf.Lerp(1f, 0f, p);
                if (rt != null) rt.anchoredPosition = startPos + new Vector2(0, p * 30f);
                yield return null;
            }

            if (go != null) Destroy(go);
        }
    }

    /// <summary>
    /// Lightweight hover/focus bridge attached to the existing Approve and
    /// Deny buttons at runtime by ShiftFeedbackOverlay.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ClaimDecisionPreviewTrigger : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler,
        ISelectHandler, IDeselectHandler
    {
        private ShiftFeedbackOverlay _overlay;
        private ClaimResolutionKind _kind;
        private bool _visible;

        public void Initialize(
            ShiftFeedbackOverlay overlay, ClaimResolutionKind kind)
        {
            _overlay = overlay;
            _kind = kind;
        }

        public void OnPointerEnter(PointerEventData eventData) => Show();
        public void OnPointerExit(PointerEventData eventData) => Hide();
        public void OnSelect(BaseEventData eventData) => Show();
        public void OnDeselect(BaseEventData eventData) => Hide();

        private void OnDisable() => Hide();

        private void Show()
        {
            if (_overlay == null) return;
            _visible = true;
            _overlay.BeginClaimProjection(_kind);
        }

        private void Hide()
        {
            if (!_visible || _overlay == null) return;
            _visible = false;
            _overlay.EndClaimProjection(_kind);
        }
    }
}
