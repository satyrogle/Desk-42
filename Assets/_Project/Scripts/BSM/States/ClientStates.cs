// ============================================================
// DESK 42 — All 9 Client BSM State Implementations
//
// Each state is a plain C# class implementing IClientState.
// They drive client-side behaviour, animations, audio, and
// dark humour output via the ClientContext callbacks.
//
// States split into two groups:
//   Organic states  — live in the base BT, transitions are
//                     evaluated by the TransitionTable.
//   Injected states — pushed onto the StateStack by punch cards.
//                     They override organic behaviour for Duration.
//
// States are stateless between instantiations — all runtime
// data lives in the ClientContext passed each tick.
// ============================================================

using Desk42.Core;
using UnityEngine;

namespace Desk42.BSM.States
{
    // ── Shared Base ───────────────────────────────────────────

    public abstract class ClientStateBase : IClientState
    {
        public abstract ClientStateID StateID { get; }
        public virtual float          Duration    => 0f;  // 0 = no timer (organic)
        public virtual bool           IsInjected  => false;

        public virtual void Enter(ClientContext ctx) { }
        public virtual bool Tick(ClientContext ctx, float dt) => true;
        public virtual void Exit(ClientContext ctx) { }
    }

    public abstract class InjectedStateBase : ClientStateBase
    {
        private readonly float _duration;
        public override  float Duration   => _duration;
        public override  bool  IsInjected => true;

        protected InjectedStateBase(float duration) => _duration = duration;
    }

    // ─────────────────────────────────────────────────────────
    // 1. PENDING — Default / Reset
    // Client is calm, waiting for the file to be opened.
    // Dark humour: "Please take a number."
    // ─────────────────────────────────────────────────────────

    public sealed class PendingState : ClientStateBase
    {
        public override ClientStateID StateID => ClientStateID.Pending;

        private float _waitTimer;

        public override void Enter(ClientContext ctx)
        {
            ctx.EmitDarkHumour?.Invoke("pending_entry");
            // Signal animator: calm idle, occasional watch-check
        }

        public override bool Tick(ClientContext ctx, float dt)
        {
            _waitTimer += dt;

            // After 15s in PENDING, client naturally becomes AGITATED
            if (_waitTimer > 15f)
            {
                ctx.TriggerTell?.Invoke("ApproachingAgitated", 0.5f);
                ctx.RequestTransition?.Invoke(ClientStateID.Agitated);
                return false; // self-terminate
            }

            return true;
        }

        public override void Exit(ClientContext ctx) => _waitTimer = 0f;
    }

    // ─────────────────────────────────────────────────────────
    // 2. AGITATED — Loud typing, ignored eye contact, delays
    // Faster BT ticks, aggressive posture.
    // Dark humour: desk items rattle, coffee ripples.
    // ─────────────────────────────────────────────────────────

    public sealed class AgitatedState : ClientStateBase
    {
        public override ClientStateID StateID => ClientStateID.Agitated;

        private float _escalationTimer;
        private const float ESCALATION_THRESHOLD = 20f;

        public override void Enter(ClientContext ctx)
        {
            ctx.EmitDarkHumour?.Invoke("agitated_entry");
            ctx.TriggerTell?.Invoke("DeskRattle", 0.8f);
        }

        public override bool Tick(ClientContext ctx, float dt)
        {
            _escalationTimer += dt;

            // High impatience accelerates escalation to LITIGIOUS
            float impatienceMult = 1f + ctx.ImpatienceTimerRatio;
            if (_escalationTimer * impatienceMult > ESCALATION_THRESHOLD)
            {
                ctx.TriggerTell?.Invoke("ApproachingLitigious", 1f);
                ctx.EmitDarkHumour?.Invoke("agitated_escalating");
                ctx.RequestTransition?.Invoke(ClientStateID.Litigious);
                return false;
            }

            return true;
        }

        public override void Exit(ClientContext ctx) => _escalationTimer = 0f;
    }

    // ─────────────────────────────────────────────────────────
    // 3. LITIGIOUS — HR violations, repeated hostility
    // Files complaint, blocks REASSIGN slot with Legal Hold.
    // Dark humour: "My lawyer will hear about this."
    // ─────────────────────────────────────────────────────────

    public sealed class LitigiousState : ClientStateBase
    {
        public override ClientStateID StateID => ClientStateID.Litigious;

        // Which card types are currently blocked by Legal Hold
        private System.Collections.Generic.HashSet<string> _blockedCardTypes
            = new() { nameof(PunchCardType.Expedite) };

        public override void Enter(ClientContext ctx)
        {
            ctx.EmitDarkHumour?.Invoke("litigious_entry");
            // Faction: Legal rep slightly improves (they're impressed)
            // Block EXPEDITE — can't rush a litigious client
        }

        public override bool Tick(ClientContext ctx, float dt)
        {
            // Litigious clients don't self-resolve — they wait for
            // the player to deal with them. No auto-transition.
            return true;
        }
    }

    // ─────────────────────────────────────────────────────────
    // 4. COOPERATIVE — Caffeine sharing, empathy routing
    // Reveals hidden traits without NDA required.
    // Dark humour: awkward small talk attempts.
    // ─────────────────────────────────────────────────────────

    public sealed class CooperativeState : ClientStateBase
    {
        public override ClientStateID StateID => ClientStateID.Cooperative;

        private float _smallTalkTimer;

        public override void Enter(ClientContext ctx)
        {
            ctx.EmitDarkHumour?.Invoke("cooperative_entry");
            // Auto-reveal hidden trait (no NDA needed)
            // but flag it as revealed via a "voluntary disclosure"
        }

        public override bool Tick(ClientContext ctx, float dt)
        {
            _smallTalkTimer += dt;

            // Cooperative clients occasionally attempt small talk
            if (_smallTalkTimer > 8f)
            {
                ctx.TriggerTell?.Invoke("SmallTalkAttempt", 0.4f);
                ctx.EmitDarkHumour?.Invoke("cooperative_smalltalk");
                _smallTalkTimer = 0f;
            }

            // If impatience is very high, cooperative client becomes resigned
            if (ctx.ImpatienceTimerRatio > 0.8f)
            {
                ctx.RequestTransition?.Invoke(ClientStateID.Resigned);
                return false;
            }

            return true;
        }

        public override void Exit(ClientContext ctx) => _smallTalkTimer = 0f;
    }

    // ─────────────────────────────────────────────────────────
    // 5. SUSPICIOUS — Contradictions in paperwork, Repeat Offender flag
    // Withholds info, resists cooperation cards.
    // Dark humour: squints at Post-it notes.
    // ─────────────────────────────────────────────────────────

    public sealed class SuspiciousState : ClientStateBase
    {
        public override ClientStateID StateID => ClientStateID.Suspicious;

        public override void Enter(ClientContext ctx)
        {
            ctx.EmitDarkHumour?.Invoke("suspicious_entry");
            ctx.TriggerTell?.Invoke("GlanceAtPostIts", 0.6f);
        }

        public override bool Tick(ClientContext ctx, float dt)
        {
            // COOPERATION_ROUTE cards have reduced effectiveness in this state
            // (handled by StateInjector checking current state before modifying)

            // Repeat offenders are harder to push out of SUSPICIOUS
            // (transition resistance handled by TransitionTable weight)
            return true;
        }
    }

    // ─────────────────────────────────────────────────────────
    // 6. RESIGNED — Overwhelming bureaucratic pressure
    // Complies with everything, reveals nothing extra.
    // Dark humour: thousand-yard stare, heavy sighs.
    // ─────────────────────────────────────────────────────────

    public sealed class ResignedState : ClientStateBase
    {
        public override ClientStateID StateID => ClientStateID.Resigned;

        private float _sighTimer;

        public override void Enter(ClientContext ctx)
        {
            ctx.EmitDarkHumour?.Invoke("resigned_entry");
        }

        public override bool Tick(ClientContext ctx, float dt)
        {
            _sighTimer += dt;
            if (_sighTimer > 12f)
            {
                ctx.TriggerTell?.Invoke("HeavySigh", 0.3f);
                ctx.EmitDarkHumour?.Invoke("resigned_sigh");
                _sighTimer = 0f;
            }
            return true;
        }

        public override void Exit(ClientContext ctx) => _sighTimer = 0f;
    }

    // ─────────────────────────────────────────────────────────
    // 7. PARANOID — High Exposure, caught lying
    // Counter-complaints, blocks card slots preemptively.
    // Dark humour: covers mouth when speaking.
    // ─────────────────────────────────────────────────────────

    public sealed class ParanoidState : ClientStateBase
    {
        public override ClientStateID StateID => ClientStateID.Paranoid;

        public override void Enter(ClientContext ctx)
        {
            ctx.EmitDarkHumour?.Invoke("paranoid_entry");
            ctx.TriggerTell?.Invoke("GlanceAtDoor", 0.9f);
            ctx.TriggerTell?.Invoke("CoverMouth", 0.7f);
        }

        public override bool Tick(ClientContext ctx, float dt)
        {
            // Paranoid clients pre-read Post-it notes and file counter-complaints
            // Card effectiveness reduced for ANALYSE and REDACT types
            return true;
        }
    }

    // ─────────────────────────────────────────────────────────
    // 8. DISSOCIATING — Extreme cascading stress
    // Stops responding to inputs for short periods.
    // Dark humour: stares through you at the wall.
    // ─────────────────────────────────────────────────────────

    public sealed class DissociatingState : ClientStateBase
    {
        public override ClientStateID StateID => ClientStateID.Dissociating;

        private float _dissociationTimer;
        private const float DISSOCIATION_DURATION = 8f;

        public override void Enter(ClientContext ctx)
        {
            ctx.EmitDarkHumour?.Invoke("dissociating_entry");
            ctx.TriggerTell?.Invoke("StareThrough", 1f);
        }

        public override bool Tick(ClientContext ctx, float dt)
        {
            _dissociationTimer += dt;

            // During dissociation, all inputs are ignored (cards return no-effect)
            // After the duration, client snaps back — usually to RESIGNED
            if (_dissociationTimer >= DISSOCIATION_DURATION)
            {
                _dissociationTimer = 0f;
                ctx.RequestTransition?.Invoke(ClientStateID.Resigned);
                return false;
            }

            return true;
        }

        public override void Exit(ClientContext ctx) => _dissociationTimer = 0f;
    }

    // ─────────────────────────────────────────────────────────
    // 9. SMUG — Client reaches dominant encounter position
    // Slows down, monologues, reveals info voluntarily (trap).
    // Dark humour: leans back, puts feet on desk.
    // ─────────────────────────────────────────────────────────

    public sealed class SmugState : ClientStateBase
    {
        public override ClientStateID StateID => ClientStateID.Smug;

        private float _monologueTimer;
        private bool  _trapActivated;

        public override void Enter(ClientContext ctx)
        {
            ctx.EmitDarkHumour?.Invoke("smug_entry");
            ctx.TriggerTell?.Invoke("FeetOnDesk", 1f);
        }

        public override bool Tick(ClientContext ctx, float dt)
        {
            _monologueTimer += dt;

            // After a few seconds, reveal a piece of "info" voluntarily
            // This info is actually a TRAP — acting on it directly costs soul or time
            if (_monologueTimer > 5f && !_trapActivated)
            {
                ctx.TriggerTell?.Invoke("VoluntaryReveal_Trap", 0.8f);
                ctx.EmitDarkHumour?.Invoke("smug_trap_reveal");
                _trapActivated = true;
            }

            return true;
        }

        public override void Exit(ClientContext ctx)
        {
            _monologueTimer = 0f;
            _trapActivated  = false;
        }
    }

    // ─────────────────────────────────────────────────────────
    // INJECTED STATES (pushed by punch cards)
    // These run on the StateStack and pause the base BT.
    // ─────────────────────────────────────────────────────────

    /// <summary>
    /// PENDING_REVIEW injection — client must fill out requisition form.
    /// Base BT paused for `duration` seconds.
    /// Default duration: 10s. Paperclip supply doubles it to 20s.
    /// </summary>
    public sealed class PendingReviewInjectedState : InjectedStateBase
    {
        public override ClientStateID StateID => ClientStateID.Pending;

        public PendingReviewInjectedState(float duration = 10f) : base(duration) { }

        public override void Enter(ClientContext ctx)
        {
            ctx.EmitDarkHumour?.Invoke("pending_review_inject");
            // Visual: client picks up requisition form, starts filling
        }

        public override bool Tick(ClientContext ctx, float dt)
        {
            // During this state: client is compliant, can't take hostile actions
            // Ideal window to stack additional injections
            return true; // timer will pop us off naturally
        }

        public override void Exit(ClientContext ctx)
        {
            ctx.EmitDarkHumour?.Invoke("pending_review_complete");
        }
    }

    /// <summary>
    /// LEGAL_HOLD injection — all client actions suspended pending review.
    /// Longer duration, lower card count efficiency.
    /// </summary>
    public sealed class LegalHoldInjectedState : InjectedStateBase
    {
        public override ClientStateID StateID => ClientStateID.Litigious;

        public LegalHoldInjectedState(float duration = 15f) : base(duration) { }

        public override void Enter(ClientContext ctx)
        {
            ctx.EmitDarkHumour?.Invoke("legal_hold_inject");
        }

        public override bool Tick(ClientContext ctx, float dt) => true;

        public override void Exit(ClientContext ctx)
        {
            ctx.EmitDarkHumour?.Invoke("legal_hold_lifted");
        }
    }

    /// <summary>
    /// COOPERATIVE_ROUTE injection — forces the client to be helpful briefly.
    /// Less effective on SUSPICIOUS/PARANOID clients (TransitionTable resistance).
    /// </summary>
    public sealed class CooperativeRouteInjectedState : InjectedStateBase
    {
        public override ClientStateID StateID => ClientStateID.Cooperative;

        public CooperativeRouteInjectedState(float duration = 8f) : base(duration) { }

        public override void Enter(ClientContext ctx)
        {
            ctx.EmitDarkHumour?.Invoke("cooperative_route_inject");
            ctx.TriggerTell?.Invoke("ForcedSmile", 0.5f);
        }

        public override bool Tick(ClientContext ctx, float dt) => true;
    }

    /// <summary>
    /// EXPEDITE injection — client stops all current actions and
    /// processes their part of the claim immediately.
    /// Short duration, high efficiency for Bureaucrat archetype.
    /// </summary>
    public sealed class ExpediteInjectedState : InjectedStateBase
    {
        public override ClientStateID StateID => ClientStateID.Resigned; // temporarily resigned

        public ExpediteInjectedState(float duration = 5f) : base(duration) { }

        public override void Enter(ClientContext ctx)
        {
            ctx.EmitDarkHumour?.Invoke("expedite_inject");
        }

        public override bool Tick(ClientContext ctx, float dt) => true;
    }
}
