// ============================================================
// DESK 42 — Behaviour Tree Status
// ============================================================

namespace Desk42.BehaviourTrees
{
    public enum BTStatus
    {
        /// <summary>Node is still executing — call Tick again next frame.</summary>
        Running,
        /// <summary>Node completed successfully.</summary>
        Success,
        /// <summary>Node completed with failure.</summary>
        Failure,
    }
}
