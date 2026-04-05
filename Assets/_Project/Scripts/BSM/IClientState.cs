// ============================================================
// DESK 42 — IClientState Interface
//
// Each of the 9 BSM states implements this interface.
// States are plain C# objects (not MonoBehaviours).
// The ClientStateMachine creates and pools them.
//
// States fall into two categories:
//   BaseState     — the client's organic behaviour (driven by BT)
//   InjectedState — pushed by a punch card, runs for a duration
//
// An InjectedState pauses the base BT for its duration.
// ============================================================

namespace Desk42.BSM
{
    public interface IClientState
    {
        /// <summary>The BSM state ID this object represents.</summary>
        Core.ClientStateID StateID { get; }

        /// <summary>
        /// For injected states: how long (seconds) before the injection
        /// expires and the base BT resumes.
        /// 0 or negative means the state has no timer — it runs until
        /// an external condition clears it.
        /// </summary>
        float Duration { get; }

        bool IsInjected { get; }

        /// <summary>Called once when this state becomes the active state.</summary>
        void Enter(ClientContext ctx);

        /// <summary>
        /// Called every frame while this state is active.
        /// Return false to request early exit (state self-terminates).
        /// </summary>
        bool Tick(ClientContext ctx, float deltaTime);

        /// <summary>Called once when this state is exited.</summary>
        void Exit(ClientContext ctx);
    }
}
