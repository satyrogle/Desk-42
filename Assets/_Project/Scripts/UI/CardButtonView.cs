// ============================================================
// DESK 42 — Card Button View (MonoBehaviour)
//
// One card slot in the hand. Displays card name, type, and
// fatigue state. Clicking calls PunchCardMachine.SlamCard().
//
// Instantiated by CardHandView. Wire all UI refs in the prefab.
// ============================================================

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using Desk42.Cards;
using Desk42.Core;
using System.Collections.Generic;

namespace Desk42.UI
{
    [DisallowMultipleComponent]
    public sealed class CardButtonView : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler,
        ISelectHandler, IDeselectHandler
    {
        // ── Inspector ─────────────────────────────────────────

        [SerializeField] private TMP_Text _nameLabel;
        [SerializeField] private TMP_Text _typeLabel;
        [SerializeField] private TMP_Text _fatigueLabel;
        [SerializeField] private TMP_Text _costLabel;
        [SerializeField] private Button   _button;
        [SerializeField] private Image    _background;

        [Header("Colors")]
        [SerializeField] private Color _normalColor   = Color.white;
        [SerializeField] private Color _jammedColor   = new Color(1f, 0.5f, 0.1f);
        [SerializeField] private Color _crumpledColor = new Color(0.4f, 0.4f, 0.4f);

        // ── State ─────────────────────────────────────────────

        private CardInstance             _card;
        private RedTape.PunchCardMachine _machine;
        private bool _previewVisible;

        public CardInstance Card => _card;
        public string RenderedEffectText => _typeLabel != null ? _typeLabel.text : "";
        public string RenderedCertaintyText => _fatigueLabel != null ? _fatigueLabel.text : "";

        // ── Init ──────────────────────────────────────────────

        public void Initialize(CardInstance card, RedTape.PunchCardMachine machine)
        {
            _card    = card;
            _machine = machine;

            RefreshFace();

            if (_button != null)
            {
                _button.onClick.RemoveAllListeners();
                _button.onClick.AddListener(OnClicked);
            }
        }

        // ── Display ───────────────────────────────────────────

        public void RefreshFace()
        {
            if (_card == null) return;

            if (_nameLabel) _nameLabel.text = FormatIdentifier(_card.Data.CardType.ToString());

            ProjectedCardResolution projection = _machine != null
                ? _machine.PreviewCard(_card.Data, _card.InstanceId)
                : default;
            ApplyProjectionFace(projection);

            if (_fatigueLabel)
            {
                if (_machine == null)
                {
                    _fatigueLabel.text = _card.IsCrumpled ? "CRUMPLED"
                        : _card.IsJammed               ? "JAMMED"
                        : _card.Fatigue > 0            ? $"FATIGUE {_card.Fatigue}"
                        :                                "";
                }
            }

            if (_background)
            {
                _background.color = _card.IsCrumpled ? _crumpledColor
                    : _card.IsJammed               ? _jammedColor
                    : _machine != null && projection.HasConcealedBlockRisk
                        ? Color.Lerp(_normalColor,
                            new Color(0.95f, 0.68f, 0.18f, 1f), 0.24f)
                    : _machine != null && !projection.IsExpectedSuccess
                        ? Color.Lerp(_normalColor,
                            new Color(0.82f, 0.25f, 0.2f, 1f), 0.16f)
                    :                                _normalColor;
            }

            if (_button)
            {
                bool weaponizedEntropy = GameManager.Instance?.Meta?.DarkIntelligenceUnlocks?.Contains("WEAPONIZED_ENTROPY") == true;
                _button.interactable = weaponizedEntropy || (!_card.IsJammed && !_card.IsCrumpled);
            }
        }

        private void ApplyProjectionFace(ProjectedCardResolution projection)
        {
            if (_machine == null)
            {
                if (_typeLabel) _typeLabel.text = FormatIdentifier(_card.Data.CardType.ToString());
                if (_costLabel) _costLabel.text = _card.Data.CreditCost > 0
                    ? $"¢{_card.Data.CreditCost}"
                    : "FREE";
                return;
            }

            if (_typeLabel) _typeLabel.text = FormatExpectedEffect(projection);
            if (_fatigueLabel) _fatigueLabel.text = FormatCertainty(projection);
            if (_costLabel) _costLabel.text = FormatFacts(projection);
        }

        private static string FormatExpectedEffect(ProjectedCardResolution projection)
        {
            if (!projection.IsExpectedSuccess)
            {
                return projection.Outcome switch
                {
                    CardSlamOutcome.BlockedByExemption
                        => $"BLOCKED BY\n{FormatIdentifier(projection.BlockingModifierId)}",
                    CardSlamOutcome.BlockedByState
                        => projection.StateBefore.HasValue
                            ? $"NO EFFECT FROM\n{FormatIdentifier(projection.StateBefore.Value.ToString())}"
                            : "NO APPLICABLE\nPROCEDURE",
                    CardSlamOutcome.InsufficientCredits
                        => $"REQUIRES ¢{projection.RequiredCredits}",
                    CardSlamOutcome.CardJammed => "CARD JAMMED",
                    CardSlamOutcome.CardCrumpled => "CARD CRUMPLED",
                    CardSlamOutcome.ClientNotResponding => "CLIENT NOT\nRESPONDING",
                    CardSlamOutcome.NoActiveClient => "NO ACTIVE CLIENT",
                    _ => "NO EFFECT",
                };
            }

            if (!string.IsNullOrWhiteSpace(projection.ClientEffect))
                return $"{projection.ClientEffect}\n{projection.ClientEffectDuration:0.##}s";

            return projection.StateBefore.HasValue && projection.StateAfter.HasValue
                ? $"{FormatIdentifier(projection.StateBefore.Value.ToString())} →\n" +
                  FormatIdentifier(projection.StateAfter.Value.ToString())
                : "STATE CHANGE";
        }

        private static string FormatCertainty(ProjectedCardResolution projection)
        {
            if (projection.HasConcealedBlockRisk)
                return "EXPECTED · RISK";
            if (!projection.IsExpectedSuccess)
                return projection.Outcome == CardSlamOutcome.BlockedByState
                    ? "NO PROCEDURE"
                    : "UNAVAILABLE";
            return "EXPECTED";
        }

        private static string FormatFacts(ProjectedCardResolution projection)
        {
            var facts = new List<string>(3);
            if (projection.CreditsDelta < 0)
                facts.Add($"¢{Mathf.Abs(projection.CreditsDelta)}");
            else if (projection.RequiredCredits > 0)
                facts.Add($"NEED ¢{projection.RequiredCredits}");
            else
                facts.Add("FREE");

            int fatigueDelta = projection.FatigueAfter - projection.FatigueBefore;
            if (fatigueDelta != 0)
                facts.Add($"FAT +{fatigueDelta}");
            if (!Mathf.Approximately(projection.SanityDelta, 0f))
                facts.Add($"SAN {FormatSigned(projection.SanityDelta)}");
            if (!Mathf.Approximately(projection.SoulIntegrityDelta, 0f))
                facts.Add($"SOUL {FormatSigned(projection.SoulIntegrityDelta)}");
            return string.Join(" · ", facts);
        }

        private static string FormatIdentifier(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "CLIENT TRAIT";
            var chars = new List<char>(value.Length + 4);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i] == '_' ? ' ' : value[i];
                if (i > 0 && char.IsUpper(c) && char.IsLower(value[i - 1]))
                    chars.Add(' ');
                chars.Add(char.ToUpperInvariant(c));
            }
            return new string(chars.ToArray());
        }

        private static string FormatSigned(float value)
            => value > 0f ? $"+{value:0.##}" : $"−{Mathf.Abs(value):0.##}";

        // ── Input ─────────────────────────────────────────────

        private void OnClicked()
        {
            if (_card == null || _machine == null) return;
            
            bool weaponizedEntropy = GameManager.Instance?.Meta?.DarkIntelligenceUnlocks?.Contains("WEAPONIZED_ENTROPY") == true;
            if (!weaponizedEntropy && (_card.IsJammed || _card.IsCrumpled)) return;

            _machine.SlamCard(_card.Data, _card.InstanceId);
        }

        public void OnPointerEnter(PointerEventData eventData) => ShowPreview();
        public void OnPointerExit(PointerEventData eventData) => HidePreview();
        public void OnSelect(BaseEventData eventData) => ShowPreview();
        public void OnDeselect(BaseEventData eventData) => HidePreview();

        private void OnDisable() => HidePreview();

        private void ShowPreview()
        {
            if (_card == null || _machine == null) return;
            _previewVisible = true;
            RumorMill.Publish(new CardPreviewEvent(
                _machine.PreviewCard(_card.Data, _card.InstanceId)));
        }

        private void HidePreview()
        {
            if (!_previewVisible || _card == null) return;
            _previewVisible = false;
            RumorMill.Publish(new CardPreviewEvent(_card.InstanceId));
        }
    }
}
