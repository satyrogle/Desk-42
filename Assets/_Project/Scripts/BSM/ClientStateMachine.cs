// ============================================================
// DESK 42 — Client State Machine (MonoBehaviour)
//
// The root controller for a single client encounter.
// Lives on the Client prefab root GameObject.
//
// Architecture:
//   _baseBT          — the organic BT that drives normal behaviour
//   _stateStack      — injected states pushed by punch cards
//   _tells           — pre-transition signal system
//   _transitions     — conditional rule table (SO)
//   _btContext       — frame context passed to the BT each tick
//
// Each frame:
//   1. Build ClientContext from world state.
//   2. Tick StateStack. If a state is active, base BT is paused.
//   3. If stack is empty, tick _baseBT.
//   4. Process pending tells.
//
// Injection flow (called by StateInjector):
//   1. Check _baseBT.HasBlockerForCard(cardType) — if blocked,
//      return "Pre-Filed Exemption" failure.
//   2. Determine target state via TransitionTable.
//   3. Instantiate correct InjectedState.
//   4. Push onto _stateStack.
//   5. Pause base BT.
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using Desk42.Core;
using Desk42.BSM.States;
using Desk42.BSM.Transitions;
using Desk42.BehaviourTrees;

namespace Desk42.BSM
{
    [DisallowMultipleComponent]
    public sealed class ClientStateMachine : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────

        [Header("Client Identity")]
        [SerializeField] private string _clientVariantId;
        [SerializeField] private string _clientSpeciesId;
        [SerializeField] private int    _visitCount;

        [Header("BSM")]
        [SerializeField] private TransitionTable _transitionTable;
        [SerializeField] private ClientStateID   _initialState = ClientStateID.Pending;

        // ── State ─────────────────────────────────────────────

        private ClientStateID   _currentMoodState;
        private ClientStateStack _stateStack   = new();
        private BehaviourTree   _baseBT;
        private ClientContext   _context;
        private ClientTellSystem _tells;

        // Counter-traits loaded from RepeatOffenderDB at spawn
        private List<string> _activeCounterTraits = new();
        private readonly HashSet<string> _revealedCounterTraits = new();

        // ── Events ────────────────────────────────────────────

        /// <summary>Fires when the BSM mood state changes.</summary>
        public event Action<ClientStateID, ClientStateID> OnStateChanged;

        /// <summary>Fires when a tell animates (animator + audio side).</summary>
        public event Action<TellDefinition> OnTellFired;

        /// <summary>Fires dark humour output for the Rumor Mill / UI layer.</summary>
        public event Action<string> OnDarkHumour;

        // ── Public Properties ─────────────────────────────────

        public ClientStateID CurrentMoodState => _currentMoodState;
        public string        ClientVariantId  => _clientVariantId;
        public string        ClientSpeciesId  => _clientSpeciesId;
        public int           VisitCount       => _visitCount;
        public BehaviourTree BaseBT           => _baseBT;
        public bool          IsInInjectedState => !_stateStack.IsEmpty;

        // ── Impatience (ATB) ───────────────────────────────────

        public const float MaxImpatience = 100f;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>Test hook — forces Impatience to a fixed value. Null = use live value.</summary>
        public float? ImpatienceOverride { get; set; }
#endif

        /// <summary>0-100 fill of this client's impatience gauge, derived from the shift timer.</summary>
        public float Impatience
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (ImpatienceOverride.HasValue) return ImpatienceOverride.Value;
#endif
                return (_context?.ImpatienceTimerRatio ?? 0f) * MaxImpatience;
            }
        }

        // ── Init ──────────────────────────────────────────────

        public void Initialize(
            string clientVariantId,
            string clientSpeciesId,
            int    visitCount,
            List<string> counterTraits,
            TransitionTable transitionTable = null)
        {
            _clientVariantId     = clientVariantId;
            _clientSpeciesId     = clientSpeciesId;
            _visitCount          = visitCount;
            // Keep encounter state independent from the persisted profile list;
            // MutationEngine persists explicitly after a generated trait lands.
            _activeCounterTraits = counterTraits != null
                ? new List<string>(counterTraits)
                : new();
            _revealedCounterTraits.Clear();
            foreach (string traitId in _activeCounterTraits)
            {
                if (!string.IsNullOrWhiteSpace(traitId))
                    _revealedCounterTraits.Add(traitId);
            }
            _transitionTable     = transitionTable ?? TransitionTable.CreateDefault();

            _tells   = new ClientTellSystem(null, visitCount);
            _baseBT  = BuildBaseBehaviourTree();
            _context = BuildContext();

            TransitionToState(_initialState, fireEvent: false);
        }

        // ── Unity Lifecycle ───────────────────────────────────

        private void Update()
        {
            if (_context == null) return; // not initialized yet

            RefreshContext();

            float dt = Time.deltaTime;

            Core.OfficeEnvironmentState.ApplyClientStateTick(CurrentMoodState, dt);

            // Tick the state stack first
            bool stackActive = _stateStack.Tick(_context, dt);

            if (!stackActive && !_baseBT.IsPaused)
            {
                // No injected state — tick the base BT
                // World state for BT context is already in _context
                _baseBT.Tick(dt,
                    RunStateController_SanityRef(),
                    RunStateController_SoulRef(),
                    ImpatenceTimerRef(),
                    _context.LastCardTypeSlammed);
            }

            // Process pending tells
            _tells.Tick(dt, tell => OnTellFired?.Invoke(tell));

            // Clear last card action after it's been processed this frame
            _context.LastCardTypeSlammed = "";
        }

        // ── Injection API (called by StateInjector) ───────────

        public enum InjectionResult
        {
            Success,
            BlockedByCounterTrait,   // Pre-Filed Exemption
            BlockedByCurrentState,   // Can't inject into this state
            ClientDissociating,      // Client not responding
        }

        public InjectionResult TryInject(string cardType, float overrideDuration = 0f)
        {
            RefreshContext();
            _context.TriggerAction = cardType;

            var evaluation = PreviewInject(cardType, out var targetState);
            if (evaluation == InjectionResult.BlockedByCounterTrait)
            {
                OnDarkHumour?.Invoke("pre_filed_exemption");
                return evaluation;
            }

            if (evaluation != InjectionResult.Success)
                return evaluation;

            // Build the correct injected state object
            var injectedState = CreateInjectedState(cardType, targetState, overrideDuration);

            // If the card doesn't push a temporary state AND doesn't shift the mood, it's invalid.
            if (injectedState == null && targetState == _currentMoodState)
                return InjectionResult.BlockedByCurrentState;
                
            if (injectedState != null)
            {
                // Pause base BT, push injection
                _baseBT.Pause();
                _stateStack.Push(injectedState, _context);
            }

            // BSM mood state changes immediately
            TransitionToState(targetState, fireEvent: true,
                byCard: true, mutated: false);

            return InjectionResult.Success;
        }

        /// <summary>
        /// Evaluates an injection without changing mood, stack, BT pause state,
        /// events, or counters. stateOverride lets StateInjector account for a
        /// known impatience overflow before the real card resolves.
        /// </summary>
        public InjectionResult PreviewInject(string cardType,
            out ClientStateID targetState,
            ClientStateID? stateOverride = null,
            bool ignoreCounterTrait = false)
        {
            ClientStateID evaluationState = stateOverride ?? _currentMoodState;
            targetState = evaluationState;

            if (_baseBT == null || _transitionTable == null)
                return InjectionResult.BlockedByCurrentState;

            if (!ignoreCounterTrait && _baseBT.HasBlockerForCard(cardType))
                return InjectionResult.BlockedByCounterTrait;

            if (evaluationState == ClientStateID.Dissociating)
                return InjectionResult.ClientDissociating;

            targetState = _transitionTable.Resolve(
                BuildTransitionContext(cardType, evaluationState));

            if (!CreatesInjectedState(cardType) && targetState == evaluationState)
                return InjectionResult.BlockedByCurrentState;

            return InjectionResult.Success;
        }

        /// <summary>
        /// Returns the blocker installed for a card and whether the player is
        /// allowed to see its identity. Preview UI must not infer disclosure
        /// from the behaviour tree itself.
        /// </summary>
        public bool TryGetBlockingTrait(string cardType,
            out string traitId, out bool isRevealed)
        {
            traitId = null;
            isRevealed = false;
            if (_baseBT == null
                || !_baseBT.TryGetBlockerForCard(cardType, out var blocker))
                return false;

            traitId = blocker.CounterTraitId;
            isRevealed = !string.IsNullOrWhiteSpace(traitId)
                && _revealedCounterTraits.Contains(traitId);
            return true;
        }

        /// <summary>
        /// Registers a trait as active for this encounter. Persisted Repeat
        /// Offender traits and newly generated mutations are already known;
        /// future concealed traits can register with revealed=false.
        /// </summary>
        public void RegisterCounterTrait(string traitId, bool revealed)
        {
            if (string.IsNullOrWhiteSpace(traitId)) return;
            if (!_activeCounterTraits.Contains(traitId))
                _activeCounterTraits.Add(traitId);
            if (revealed)
                _revealedCounterTraits.Add(traitId);
        }

        public void RevealCounterTrait(string traitId)
        {
            if (!string.IsNullOrWhiteSpace(traitId))
                _revealedCounterTraits.Add(traitId);
        }

        // ── Runtime Rule Injection (called by MutationEngine) ────

        /// <summary>
        /// Add a runtime-only TransitionRule for this encounter.
        /// Delegates to the TransitionTable without modifying the shared SO asset.
        /// </summary>
        public void AddRuntimeTransitionRule(Transitions.TransitionRule rule)
            => _transitionTable?.AddRuntimeRule(rule);

        // ── Organic State Transition (from BT / state Tick) ───

        public void ForceState(ClientStateID newState)
        {
            // Bypasses BT and directly shifts mood (for systemic overrides)
            TransitionToState(newState, true);
        }

        private void TransitionToState(ClientStateID newState, bool fireEvent,
            bool byCard = false, bool mutated = false)
        {
            if (newState == _currentMoodState && fireEvent) return;

            var prev = _currentMoodState;
            _currentMoodState = newState;

            if (fireEvent)
            {
                OnStateChanged?.Invoke(prev, newState);
                RumorMill.Publish(new StateTransitionEvent(
                    _clientVariantId, prev, newState,
                    byCard, mutated));
            }
        }

        // ── Context Refresh ───────────────────────────────────

        private void RefreshContext()
        {
            // Refresh dynamic world-state values each frame
            // Static identity values set once in BuildContext()
            if (GameManager.Instance?.Run == null) return;

            var run = GameManager.Instance.Run;
            _context.OfficeSanity         = run.Sanity;
            _context.SoulIntegrity        = run.SoulIntegrity;
            _context.ImpatienceTimerRatio = 1f - (run.ImpatenceTimer / 1440f); // rough ratio
            _context.CurrentMoodState     = _currentMoodState;
        }

        private ClientContext BuildContext()
        {
            var ctx = new ClientContext
            {
                ClientVariantId      = _clientVariantId,
                ClientSpeciesId      = _clientSpeciesId,
                VisitCount           = _visitCount,
                ActiveCounterTraits  = _activeCounterTraits,
                CurrentMoodState     = _currentMoodState,
                BaseBT               = _baseBT,

                // Callbacks
                RequestTransition = newState => TransitionToState(newState, fireEvent: true),
                RequestIdle       = () => TransitionToState(ClientStateID.Pending, true),
                TriggerTell       = (type, intensity) => _tells.RequestTell(type, intensity),
                EmitDarkHumour    = key => OnDarkHumour?.Invoke(key),
            };
            return ctx;
        }

        private TransitionContext BuildTransitionContext(string triggerAction,
            ClientStateID? stateOverride = null)
        {
            var run = GameManager.Instance?.Run;
            float impatienceRatio = run != null
                ? 1f - (run.ImpatenceTimer / 1440f)
                : _context?.ImpatienceTimerRatio ?? 0f;
            return new TransitionContext
            {
                TriggerAction        = triggerAction,
                ClientSpeciesId      = _clientSpeciesId,
                ActiveCounterTraits  = _activeCounterTraits,
                CurrentState         = stateOverride ?? _currentMoodState,
                SoulIntegrity        = run?.SoulIntegrity ?? 100f,
                ImpatienceRatio      = impatienceRatio,
                LegalReputation      = run?.GetFactionRep(FactionID.Legal) ?? 0f,
                ManagementReputation = run?.GetFactionRep(FactionID.Management) ?? 0f,
                VisitCount           = _visitCount,
                HiddenTraitRevealed  = _context.HiddenTraitRevealed,
                ClaimCorruption      = _context.ClaimCorruption,
                OfficeTemp           = _context.OfficeTemperatureState,
            };
        }

        // ── BT Builder ────────────────────────────────────────

        /// <summary>
        /// Builds the default organic BT for this client.
        /// Species-specific BTs can override via ClientTemplate SO.
        /// </summary>
        private BehaviourTree BuildBaseBehaviourTree()
        {
            // Root: Selector tries each behavioural tier in order
            var root = new BTSelector { DebugLabel = "ClientBehaviourRoot" };

            // Tier 1: Hostile actions (only when agitated/litigious)
            var hostileSeq = new BTSequence { DebugLabel = "HostileActions" };
            hostileSeq.AddChild(new BTConditionNode("IsHostile", ctx =>
            {
                var state = ctx.Get<ClientStateID>("MoodState");
                return state == ClientStateID.Agitated || state == ClientStateID.Litigious;
            }));
            hostileSeq.AddChild(new BTCooldownDecorator(5f,
                new BTActionNode("DemandAttention", ctx =>
                {
                    // Signal animator + audio via RumorMill tell
                    ctx.Set("HostileActionFired", true);
                    return BTStatus.Success;
                })));

            // Tier 2: Cooperative disclosure (when cooperative)
            var cooperativeSeq = new BTSequence { DebugLabel = "CooperativeActions" };
            cooperativeSeq.AddChild(new BTConditionNode("IsCooperative", ctx =>
                ctx.Get<ClientStateID>("MoodState") == ClientStateID.Cooperative));
            cooperativeSeq.AddChild(new BTActionNode("AttemptSmallTalk", ctx =>
            {
                ctx.Set("SmallTalkAttempted", true);
                return BTStatus.Success;
            }));

            // Tier 3: Default idle — wait, check watch, subtle impatience
            var idleSeq = new BTSequence { DebugLabel = "IdleBehaviour" };
            idleSeq.AddChild(new BTWaitNode(3f, "IdlePause"));
            idleSeq.AddChild(new BTCooldownDecorator(12f,
                new BTActionNode("CheckWatch", ctx =>
                {
                    ctx.Set("CheckWatchFired", true);
                    return BTStatus.Success;
                })));

            root.AddChild(hostileSeq);
            root.AddChild(cooperativeSeq);
            root.AddChild(idleSeq);

            return new BehaviourTree(root, this);
        }

        // ── Injected State Factory ────────────────────────────

        private IClientState CreateInjectedState(
            string cardType, ClientStateID targetState, float overrideDuration)
        {
            float dur = overrideDuration > 0f ? overrideDuration : 0f;

            return cardType switch
            {
                nameof(PunchCardType.PendingReview) =>
                    new PendingReviewInjectedState(dur > 0 ? dur : 10f),
                nameof(PunchCardType.LegalHold) =>
                    new LegalHoldInjectedState(dur > 0 ? dur : 15f),
                nameof(PunchCardType.Expedite) =>
                    new ExpediteInjectedState(dur > 0 ? dur : 5f),
                nameof(PunchCardType.CooperationRoute) =>
                    new CooperativeRouteInjectedState(dur > 0 ? dur : 8f),

                // Cards that cause organic transitions (not injected states)
                // Return null — no stack push, just a mood shift
                nameof(PunchCardType.ThreatAudit) => null,
                nameof(PunchCardType.Redact)       => null,
                nameof(PunchCardType.Analyse)      => null,

                _ => null,
            };
        }

        private static bool CreatesInjectedState(string cardType)
        {
            return cardType == nameof(PunchCardType.PendingReview)
                || cardType == nameof(PunchCardType.LegalHold)
                || cardType == nameof(PunchCardType.Expedite)
                || cardType == nameof(PunchCardType.CooperationRoute);
        }

        // ── World State Accessors ─────────────────────────────
        // Thin wrappers so the BT doesn't need GameManager directly

        private float RunStateController_SanityRef()
            => GameManager.Instance?.Run?.Sanity ?? 100f;

        private float RunStateController_SoulRef()
            => GameManager.Instance?.Run?.SoulIntegrity ?? 100f;

        private float ImpatenceTimerRef()
            => GameManager.Instance?.Run?.ImpatenceTimer ?? 1440f;

        // ── OnDestroy ─────────────────────────────────────────

        private void OnDestroy()
        {
            _stateStack?.ClearAll(_context);
            _tells?.Clear();
            _transitionTable?.ClearRuntimeRules();
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [ContextMenu("Dump Behaviour Tree")]
        private void DumpBT() => Debug.Log(_baseBT?.DumpTree() ?? "No BT");

        [ContextMenu("Force State: Agitated")]
        private void DebugForceAgitated()
            => TransitionToState(ClientStateID.Agitated, true);
#endif
    }
}
