// ============================================================
// DESK 42 — Card Button View (MonoBehaviour)
//
// One card slot in the hand. Displays card name, type, and
// fatigue state. Clicking calls PunchCardMachine.SlamCard().
//
// Instantiated by CardHandView. Wire all UI refs in the prefab.
// ============================================================

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Desk42.Cards;

namespace Desk42.UI
{
    [DisallowMultipleComponent]
    public sealed class CardButtonView : MonoBehaviour
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
                _button.interactable = !_card.IsJammed && !_card.IsCrumpled;
        }

        // ── Input ─────────────────────────────────────────────

        private void OnClicked()
        {
            if (_card == null || _machine == null) return;
            if (_card.IsJammed || _card.IsCrumpled) return;

            _machine.SlamCard(_card.Data, _card.InstanceId);
        }
    }
}
