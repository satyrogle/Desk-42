// ============================================================
// DESK 42 — Click Cost Interceptor (MonoBehaviour)
//
// Vow 7: Micro-Transactions. Every click costs Corporate Credits
// (or Sanity at 0 credits). Attach to the Shift Canvas root.
//
// Rank 1: only card plays cost 1 credit (handled by PunchCardMachine).
// Rank 2: ALL clicks cost 1 credit.
// Rank 3: ALL clicks cost 2 credits.
//
// This component intercepts pointer events globally via
// UnityEngine.EventSystems.
// ============================================================

using UnityEngine;
using UnityEngine.EventSystems;

namespace Desk42.Core
{
    [DisallowMultipleComponent]
    public sealed class ClickCostInterceptor : MonoBehaviour
    {
        private void Update()
        {
            if (!ComplianceVowSystem.AllClicksCost()) return;
            if (!Input.GetMouseButtonDown(0)) return;

            // ── Weaponised Entropy Bypass ──
            if (EventSystem.current != null)
            {
                var pointerData = new PointerEventData(EventSystem.current)
                {
                    position = Input.mousePosition
                };
                var results = new System.Collections.Generic.List<RaycastResult>();
                EventSystem.current.RaycastAll(pointerData, results);

                foreach (var result in results)
                {
                    // Bypass cost when physically sweeping junk
                    if (result.gameObject.GetComponent<Desk42.UI.JunkItem>() != null)
                        return;

                    // Bypass cost when clicking on a jammed/crumpled card
                    var cardView = result.gameObject.GetComponentInParent<Desk42.UI.CardButtonView>();
                    if (cardView != null && cardView.Card != null && (cardView.Card.IsJammed || cardView.Card.IsCrumpled))
                        return;
                }
            }

            int cost = ComplianceVowSystem.GetClickCost();
            if (cost <= 0) return;

            var run = GameManager.Instance?.Run;
            if (run == null) return;

            if (run.Credits >= cost)
            {
                run.RawData.CorporateCredits -= cost;
            }
            else
            {
                // Out of credits — clicks cost sanity instead
                run.ModifySanity(-cost * 2f);
            }
        }
    }
}
