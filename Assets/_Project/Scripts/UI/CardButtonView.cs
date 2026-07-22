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

        // ── Init ──────────────────────────────────────────────

        public void Initialize(CardInstance card, RedTape.PunchCardMachine machine)
        {
            _card    = card;
            _machine = machine;

            Refresh();

            if (_button != null)
            {
                _button.onClick.RemoveAllListeners();
                _button.onClick.AddListener(OnClicked);
            }
        }

        // ── Display ───────────────────────────────────────────

        private void Refresh()
        {
            if (_card == null) return;

            if (_nameLabel) _nameLabel.text = _card.Data.DisplayName;
            if (_typeLabel) _typeLabel.text = _card.Data.CardType.ToString();
            if (_costLabel) _costLabel.text = _card.Data.CreditCost > 0
                ? $"¢{_card.Data.CreditCost}"
                : "Free";

            if (_fatigueLabel)
            {
                _fatigueLabel.text = _card.IsCrumpled ? "CRUMPLED"
                    : _card.IsJammed               ? "JAMMED"
                    : _card.Fatigue > 0            ? $"×{_card.Fatigue}"
                    :                                "";
            }

            if (_background)
            {
                _background.color = _card.IsCrumpled ? _crumpledColor
                    : _card.IsJammed               ? _jammedColor
                    :                                _normalColor;
            }

            if (_button)
            {
                bool weaponizedEntropy = GameManager.Instance?.Meta?.DarkIntelligenceUnlocks?.Contains("WEAPONIZED_ENTROPY") == true;
                _button.interactable = weaponizedEntropy || (!_card.IsJammed && !_card.IsCrumpled);
            }
        }

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
