// ============================================================
// DESK 42 — RumorMill Listener Base
//
// Abstract MonoBehaviour that enforces the subscribe-in-OnEnable /
// unsubscribe-in-OnDisable pattern. Inherit instead of writing
// the pair by hand in every new listener class.
//
// Usage:
//   public sealed class MyUI : RumorMillListener
//   {
//       protected override void Subscribe()
//       {
//           RumorMill.OnClaimResolved += Handle;
//       }
//       protected override void Unsubscribe()
//       {
//           RumorMill.OnClaimResolved -= Handle;
//       }
//       private void Handle(ClaimResolvedEvent e) { … }
//   }
//
// If you need custom OnEnable/OnDisable logic alongside the
// subscription wiring, override the hooks:
//
//   protected override void OnEnable()
//   {
//       base.OnEnable();  // subscribes
//       DoMyEnableWork();
//   }
// ============================================================

using UnityEngine;

namespace Desk42.Core
{
    public abstract class RumorMillListener : MonoBehaviour
    {
        protected virtual void OnEnable()  => Subscribe();
        protected virtual void OnDisable() => Unsubscribe();

        protected abstract void Subscribe();
        protected abstract void Unsubscribe();
    }
}
