// ============================================================
// DESK 42 — Rumor Mill Event Definitions
// Every domain event in the game is defined here as a
// readonly struct.  The bus routes them; nothing else imports
// anything except the event type it cares about.
// ============================================================

using System;
using UnityEngine;

namespace Desk42.Core
{
    // ── Enumerations ─────────────────────────────────────────

    public enum ClientStateID
    {
        Pending, Agitated, Litigious, Cooperative,
        Suspicious, Resigned, Paranoid, Dissociating, Smug
    }

    public enum PunchCardType
    {
        PendingReview, ThreatAudit, Expedite, Redact,
        LegalHold, DebugTrace, AutoFile, RecursiveFiling,
        Analyse, Forget, CooperationRoute, Escalate,
        Gaslight, Transfer, NonDisclosure, SmallTalk, 
        Empathise, Interrogate, Reeducate, AppointCouncil, 
        Legal
    }

    public enum UnethicalActionType
    {
        TaxFraud, ExcessiveNDA, EmpathyDenied,
        EvidenceDestroyed, FalseReport, WrongfulTermination
    }

    public enum OfficeHazardType
    {
        PrinterJam, MandatoryMeeting, SystemCrash,
        FireDrill, UnscheduledAudit, CoffeeMachineDown
    }

    public enum FactionID
    {
        Filing, Legal, OccultContainment, Accounting, Management
    }

    public enum MilestoneID
    {
        FirstPromotion, RedactedTruth, TheBreach, TheGreatAudit, TheCompanyMan,
        TheWhistleblower,
        // Archetype ability milestones
        DeepAuditUsed, DistortionThreshold, PlausibleDeniabilityUsed,
        EmergencyProcedureUsed, HardResetUsed,
    }

    public enum NarratorReliability
    {
        Professional,       // 100–76% soul
        SlightlyHonest,     // 75–51%
        Honest,             // 50–26%
        Doublespeak         // 25–0%
    }

    public enum ShiftPhase
    {
        ClockIn, MorningBlock, LunchBreak,
        AfternoonBlock, Overtime, ClockOut
    }

    // ── Event Structs ─────────────────────────────────────────
    // All events are readonly structs — zero GC allocation.

    /// <summary>Player slammed a punch card into the machine.</summary>
    public readonly struct CardSlammedEvent
    {
        public readonly PunchCardType CardType;
        public readonly string        CardInstanceId;   // per-run GUID (fatigue key)
        public readonly string        ClientVariantId;
        public readonly ClientStateID StateBefore;
        public readonly int           CurrentFatigue;   // how many times used this shift

        // Backward-compat alias
        public string CardId => CardInstanceId;

        public CardSlammedEvent(PunchCardType type, string cardInstanceId,
            string clientId, ClientStateID before, int fatigue)
        {
            CardType        = type;
            CardInstanceId  = cardInstanceId;
            ClientVariantId = clientId;
            StateBefore     = before;
            CurrentFatigue  = fatigue;
        }
    }

    /// <summary>Client BSM transitioned to a new state.</summary>
    public readonly struct StateTransitionEvent
    {
        public readonly string        ClientVariantId;
        public readonly ClientStateID From;
        public readonly ClientStateID To;
        public readonly bool          WasForcedByCard;  // injected vs organic
        public readonly bool          WasMutated;       // BT adapted

        public StateTransitionEvent(string clientId, ClientStateID from,
            ClientStateID to, bool byCard, bool mutated)
        {
            ClientVariantId = clientId;
            From            = from;
            To              = to;
            WasForcedByCard = byCard;
            WasMutated      = mutated;
        }
    }

    /// <summary>A claim was resolved — includes the moral choice made.</summary>
    public readonly struct ClaimResolvedEvent
    {
        public readonly string  ClaimId;
        public readonly bool    ResolvedCorrectly;  // true = bureaucratic, false = humane
        public readonly int     CreditsEarned;
        public readonly float   SoulCost;           // positive = soul lost
        public readonly string  ClientVariantId;
        public readonly string  ClientSpeciesId;

        public ClaimResolvedEvent(string claimId, bool correct,
            int credits, float soulCost, string clientId, string speciesId)
        {
            ClaimId           = claimId;
            ResolvedCorrectly = correct;
            CreditsEarned     = credits;
            SoulCost          = soulCost;
            ClientVariantId   = clientId;
            ClientSpeciesId   = speciesId;
        }
    }

    /// <summary>Player made an active moral choice at a dilemma prompt.</summary>
    public readonly struct MoralChoiceEvent
    {
        public readonly string             ClaimId;
        public readonly UnethicalActionType ActionType;
        public readonly float              MoralInjuryDelta; // positive = more injury
        public readonly bool               WasUnethical;
        public readonly bool               WasInverted;

        public MoralChoiceEvent(string claimId, UnethicalActionType action,
            float injuryDelta, bool unethical, bool inverted = false)
        {
            ClaimId           = claimId;
            ActionType        = action;
            MoralInjuryDelta  = injuryDelta;
            WasUnethical      = unethical;
            WasInverted       = inverted;
        }
    }

    /// <summary>Player's soul integrity changed (any source).</summary>
    public readonly struct SoulIntegrityChangedEvent
    {
        public readonly float Previous;
        public readonly float Current;
        public readonly float Delta;  // negative = soul lost

        public SoulIntegrityChangedEvent(float prev, float curr)
        {
            Previous = prev;
            Current  = curr;
            Delta    = curr - prev;
        }
    }

    /// <summary>Sanity / Cognitive Budget changed.</summary>
    public readonly struct SanityChangedEvent
    {
        public readonly float Previous;
        public readonly float Current;
        public readonly bool  TriggeredFugue;

        public SanityChangedEvent(float prev, float curr, bool fugue = false)
        {
            Previous      = prev;
            Current       = curr;
            TriggeredFugue = fugue;
        }
    }

    /// <summary>Player signed an NDA. Covers part of the screen.</summary>
    public readonly struct NDASignedEvent
    {
        public readonly string  ClaimId;
        public readonly int     TotalNDACount;    // running total this shift
        public readonly Vector2 CoveredRegion;    // normalised screen coords (0-1)

        public NDASignedEvent(string claimId, int total, Vector2 region)
        {
            ClaimId       = claimId;
            TotalNDACount = total;
            CoveredRegion = region;
        }
    }

    /// <summary>An office hazard was triggered by The Tide.</summary>
    public readonly struct OfficeHazardEvent
    {
        public readonly OfficeHazardType HazardType;
        public readonly float            DurationSeconds;
        public readonly bool             IsOverperformancePunishment;

        public OfficeHazardEvent(OfficeHazardType type, float duration, bool overperf)
        {
            HazardType                 = type;
            DurationSeconds            = duration;
            IsOverperformancePunishment = overperf;
        }
    }

    /// <summary>A faction's reputation with the player shifted.</summary>
    public readonly struct FactionShiftEvent
    {
        public readonly FactionID Faction;
        public readonly float     Delta;      // positive = more friendly
        public readonly float     NewValue;   // -100 to 100

        public FactionShiftEvent(FactionID faction, float delta, float newValue)
        {
            Faction  = faction;
            Delta    = delta;
            NewValue = newValue;
        }
    }

    /// <summary>A shift started or ended.</summary>
    public readonly struct ShiftLifecycleEvent
    {
        public readonly int        ShiftNumber;
        public readonly bool       IsStart;       // false = end
        public readonly ShiftPhase Phase;
        public readonly int        Seed;

        public ShiftLifecycleEvent(int shift, bool isStart, ShiftPhase phase, int seed)
        {
            ShiftNumber = shift;
            IsStart     = isStart;
            Phase       = phase;
            Seed        = seed;
        }
    }

    /// <summary>Shift phase changed (Morning, Lunch, etc.).</summary>
    public readonly struct ShiftPhaseChangedEvent
    {
        public readonly ShiftPhase Previous;
        public readonly ShiftPhase Current;

        public ShiftPhaseChangedEvent(ShiftPhase prev, ShiftPhase curr)
        { Previous = prev; Current = curr; }
    }

    /// <summary>A narrative milestone was reached.</summary>
    public readonly struct MilestoneReachedEvent
    {
        public readonly MilestoneID MilestoneId;
        public readonly int         ShiftNumber;
        public readonly string      Tag;   // optional debug / sub-event label

        public MilestoneReachedEvent(MilestoneID m, int shift, string tag = "")
        { MilestoneId = m; ShiftNumber = shift; Tag = tag; }

        public MilestoneReachedEvent(MilestoneID m)
        { MilestoneId = m; ShiftNumber = 0; Tag = ""; }
    }

    /// <summary>A Repeat Offender was flagged with a new counter-trait.</summary>
    public readonly struct CounterTraitGeneratedEvent
    {
        public readonly string ClientVariantId;
        public readonly string CounterTraitId;    // SO GUID of the generated trait
        public readonly PunchCardType TriggeringCard;

        public CounterTraitGeneratedEvent(string clientId, string traitId, PunchCardType card)
        {
            ClientVariantId = clientId;
            CounterTraitId  = traitId;
            TriggeringCard  = card;
        }
    }

    /// <summary>ShiftManager has dequeued a claim — the next encounter is ready.</summary>
    public readonly struct ClaimQueuedEvent
    {
        public readonly ActiveClaimData Claim;
        public readonly int             QueueRemaining; // pending claims after this one

        public ClaimQueuedEvent(ActiveClaimData claim, int remaining)
        { Claim = claim; QueueRemaining = remaining; }
    }

    /// <summary>The Tide's pressure level changed due to overperformance.</summary>
    public readonly struct TideEscalatedEvent
    {
        public readonly int  OldLevel;           // 0-3
        public readonly int  NewLevel;           // 0-3
        public readonly bool IsOverperformance;  // true = player is going too fast

        public TideEscalatedEvent(int oldLevel, int newLevel, bool overperf)
        { OldLevel = oldLevel; NewLevel = newLevel; IsOverperformance = overperf; }
    }

    /// <summary>Personal expense went unmet — desk will deteriorate.</summary>
    public readonly struct ExpenseUnmetEvent
    {
        public readonly string ExpenseId;
        public readonly int    AmountShort;

        public ExpenseUnmetEvent(string id, int amountShort)
        { ExpenseId = id; AmountShort = amountShort; }
    }

    /// <summary>Narrator tone changed as soul integrity crossed a threshold.</summary>
    public readonly struct NarratorToneChangedEvent
    {
        public readonly NarratorReliability Previous;
        public readonly NarratorReliability Current;

        public NarratorToneChangedEvent(NarratorReliability prev, NarratorReliability curr)
        { Previous = prev; Current = curr; }
    }

    /// <summary>An Office Supply triggered a unique mechanical signal (like a hint or reveal).</summary>
    public readonly struct SupplySignalEvent
    {
        public readonly string SupplyId; // SO GUID or shorthand
        public readonly string Tag;      // Sub-event label

        public SupplySignalEvent(string supplyId, string tag = "")
        { SupplyId = supplyId; Tag = tag; }
    }

    /// <summary>A memo was generated for a resolved claim.</summary>
    public readonly struct MemoGeneratedEvent
    {
        public readonly string MemoId;
        public readonly string ClaimId;
        public readonly string Headline;  // short summary line
        public readonly string Body;      // full text shown when player opens the memo

        public MemoGeneratedEvent(string memoId, string claimId, string headline, string body)
        { MemoId = memoId; ClaimId = claimId; Headline = headline; Body = body; }
    }

    /// <summary>A moral dilemma should be presented to the player.</summary>
    public readonly struct DilemmaTriggeredEvent
    {
        public readonly string Prompt;
        public readonly string EthicalLabel;
        public readonly string BureaucraticLabel;
        public readonly System.Action OnEthical;
        public readonly System.Action<bool> OnBureaucratic;

        public DilemmaTriggeredEvent(string prompt, string ethical, string bureaucratic,
            System.Action onEthical, System.Action<bool> onBureaucratic)
        {
            Prompt            = prompt;
            EthicalLabel      = ethical;
            BureaucraticLabel = bureaucratic;
            OnEthical         = onEthical;
            OnBureaucratic    = onBureaucratic;
        }
    }

    /// <summary>Fired by GameManager.EndShift after run state is finalised.</summary>
    public readonly struct RunCompletedEvent
    {
        public readonly int    ShiftNumber;
        public readonly int    ClaimsProcessed;
        public readonly int    CreditsEarned;
        public readonly float  EfficiencyRating;
        public readonly float  SoulIntegrity;
        public readonly float  Sanity;
        public readonly int    PersonalExpenseDebt;
        public readonly float  ComboMultiplier;
        public readonly string ArchetypeId;
        public readonly string SeedCode;
        public readonly bool   FugueTriggered;

        public RunCompletedEvent(int shift, int claims, int credits, float eff,
            float soul, float sanity, int debt, float combo,
            string archetype, string seed, bool fugue)
        {
            ShiftNumber         = shift;
            ClaimsProcessed     = claims;
            CreditsEarned       = credits;
            EfficiencyRating    = eff;
            SoulIntegrity       = soul;
            Sanity              = sanity;
            PersonalExpenseDebt = debt;
            ComboMultiplier     = combo;
            ArchetypeId         = archetype;
            SeedCode            = seed;
            FugueTriggered      = fugue;
        }
    }
}
