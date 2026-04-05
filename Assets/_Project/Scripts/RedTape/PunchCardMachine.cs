// ============================================================
// DESK 42 — Punch Card Machine (MonoBehaviour)
//
// The physical machine the player interacts with.
// Handles drag-to-slot input, animation, audio, and tactile
// feedback — then delegates to StateInjector for logic.
//
// The THUNK is everything. This is the game's signature
// interaction. Every aspect of the feedback is tuned to feel
// satisfying: the mechanical CLUNK of the card entering,
// the gear rotation visual, the client's visible behaviour
// change. If this doesn't feel good, nothing else matters.
//
// Machine state machine:
//   Idle → CardHovering → CardInserting → Processing → Resolved
//                     ↑_______________ Rejected ___________↑
//
// For vertical slice: click-to-slam (drag-to-slot Expansion Tier).
// ============================================================

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using Desk42.Cards;
using Desk42.Core;

namespace Desk42.RedTape
{
    [DisallowMultipleComponent]
    public sealed class PunchCardMachine : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler
    {
        // ── Inspector ─────────────────────────────────────────

        [Header("Slot")]
        [SerializeField] private Transform    _slotTransform;
        [SerializeField] private Collider     _slotCollider;

        [Header("Animation")]
        [SerializeField] private Animator     _machineAnimator;
        [SerializeField] private string       _animInsert   = "Insert";
        [SerializeField] private string       _animProcess  = "Process";
        [SerializeField] private string       _animReject   = "Reject";
        [SerializeField] private string       _animJam      = "Jam";
        [SerializeField] private float        _processDelay = 0.4f; // seconds of gear animation

        [Header("Audio")]
        [SerializeField] private AudioSource  _audioSource;
        [SerializeField] private AudioClip    _clipSlam;
        [SerializeField] private AudioClip    _clipProcess;
        [SerializeField] private AudioClip    _clipReject;
        [SerializeField] private AudioClip    _clipJam;
        [SerializeField] private AudioClip    _clipCrumple;

        [Header("Feedback")]
        [SerializeField] private ParticleSystem _processParticles;
        [SerializeField] private ParticleSystem _rejectParticles;
        [SerializeField] private float          _screenShakeMagnitude = 0.15f;

        // ── State ─────────────────────────────────────────────

        public enum MachineState
        {
            Idle,
            CardHovering,
            CardInserting,
            Processing,
            Resolved,
        }

        private MachineState _state = MachineState.Idle;
        private bool         _isCardSlotHighlighted;

        // ── Dependencies ──────────────────────────────────────

        private StateInjector      _injector;
        private CardFatigueTracker _fatigue;
        private MutationEngine     _mutation;

        // ── Events ────────────────────────────────────────────

        /// <summary>Fired after slam resolves — carries full result for UI/audio layers.</summary>
        public event Action<SlamResult> OnSlamResolved;

        // ── Init ──────────────────────────────────────────────

        private void Awake()
        {
            _fatigue  = new CardFatigueTracker();
            _mutation = new MutationEngine();
            _injector = GetComponent<StateInjector>()
                        ?? gameObject.AddComponent<StateInjector>();
        }

        public void SetActiveClient(BSM.ClientStateMachine client)
        {
            _injector.Initialize(client, _fatigue, _mutation);
        }

        public void ClearActiveClient()
        {
            _injector.ClearClient();
        }

        // ── Update: fatigue timers ────────────────────────────

        private void Update() => _fatigue.Tick(Time.deltaTime);

        // ── Input: Drag-to-Slot (Card View calls this) ────────

        /// <summary>
        /// Called by a CardView when it's released over the machine slot.
        /// This is the entry point from the drag-and-drop system.
        /// </summary>
        public void OnCardDropped(CardView droppedCard)
        {
            if (_state != MachineState.Idle && _state != MachineState.CardHovering)
                return;

            StartCoroutine(SlamSequence(droppedCard));
        }

        // ── Hover highlight (from IPointerHandler) ────────────

        public void OnPointerEnter(PointerEventData eventData)
        {
            _isCardSlotHighlighted = true;
            // TODO: Glow material on slot
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _isCardSlotHighlighted = false;
        }

        // ── Slam Coroutine ────────────────────────────────────

        private IEnumerator SlamSequence(CardView cardView)
        {
            _state = MachineState.CardInserting;

            // ── Phase 1: Insertion animation ─────────────────
            if (_machineAnimator != null)
                _machineAnimator.SetTrigger(_animInsert);

            // Snap card to slot position
            if (cardView != null)
                cardView.SnapToSlot(_slotTransform.position);

            PlaySound(_clipSlam);
            yield return new WaitForSeconds(_processDelay * 0.3f);

            // ── Phase 2: Processing ───────────────────────────
            _state = MachineState.Processing;

            if (_machineAnimator != null)
                _machineAnimator.SetTrigger(_animProcess);

            PlaySound(_clipProcess);
            _processParticles?.Play();

            yield return new WaitForSeconds(_processDelay);

            // ── Phase 3: Inject ───────────────────────────────
            var result = _injector.TrySlam(
                cardView?.CardData,
                cardView?.CardInstanceId ?? "");

            // ── Phase 4: Resolve feedback ─────────────────────
            _state = MachineState.Resolved;
            ApplyResultFeedback(result, cardView);

            OnSlamResolved?.Invoke(result);

            // Brief pause, then return to idle
            yield return new WaitForSeconds(0.2f);
            _state = MachineState.Idle;
        }

        private void ApplyResultFeedback(SlamResult result, CardView cardView)
        {
            switch (result.Outcome)
            {
                case SlamOutcome.Success:
                    // Screen shake scaled by card impact
                    CameraShake(_screenShakeMagnitude);
                    cardView?.PlaySlamSuccess();
                    break;

                case SlamOutcome.CardJammed:
                    if (_machineAnimator != null)
                        _machineAnimator.SetTrigger(_animJam);
                    PlaySound(_clipJam);
                    cardView?.PlayJam();
                    break;

                case SlamOutcome.CardCrumpled:
                    PlaySound(_clipCrumple);
                    cardView?.PlayCrumple();
                    break;

                case SlamOutcome.BlockedByPreFiledExemption:
                    if (_machineAnimator != null)
                        _machineAnimator.SetTrigger(_animReject);
                    PlaySound(_clipReject);
                    _rejectParticles?.Play();
                    cardView?.PlayReject();
                    // TODO: Show "Pre-Filed Exemption" stamp animation on card
                    break;

                default:
                    if (_machineAnimator != null)
                        _machineAnimator.SetTrigger(_animReject);
                    PlaySound(_clipReject);
                    cardView?.PlayReject();
                    break;
            }
        }

        // ── Audio ─────────────────────────────────────────────

        private void PlaySound(AudioClip clip)
        {
            if (_audioSource != null && clip != null)
                _audioSource.PlayOneShot(clip);
        }

        // ── Camera Shake ──────────────────────────────────────

        private void CameraShake(float magnitude)
        {
            // TODO: Wire to CameraShakeManager
            // Simple version: use Cinemachine impulse or coroutine
        }

        // ── New Shift Handshake ───────────────────────────────

        public void OnNewShift()
        {
            _fatigue.ResetForNewShift();
            _state = MachineState.Idle;
        }
    }

    // ── Card View (Stub) ──────────────────────────────────────
    // Full implementation in next phase (deck building + UI).
    // Placed here as a forward reference for PunchCardMachine.

    public sealed class CardView : MonoBehaviour
    {
        public PunchCardData CardData       { get; private set; }
        public string        CardInstanceId { get; private set; } = System.Guid.NewGuid().ToString();

        private PunchCardMachine _machine;

        public void Initialize(PunchCardData data, PunchCardMachine machine)
        {
            CardData = data;
            _machine = machine;
        }

        public void SnapToSlot(Vector3 worldPos)
        {
            transform.position = worldPos;
        }

        public void PlaySlamSuccess()
        {
            // TODO: animate card going into machine
        }

        public void PlayJam()
        {
            // TODO: shake + red tint
        }

        public void PlayCrumple()
        {
            // TODO: scrunch animation, remove from hand
        }

        public void PlayReject()
        {
            // TODO: card bounces back to hand
        }

        // Drag interaction — fires when released over machine
        private void OnMouseUp()
        {
            if (_machine != null)
                _machine.OnCardDropped(this);
        }
    }
}
