// ============================================================
// DESK 42 — Run Data
//
// Per-run serializable state. Saved mid-run to run.json so the
// player can resume. Deleted on run completion or failure.
//
// Contains everything needed to reconstruct a run in progress:
//   - Deck state, hand, discard
//   - Sanity, soul integrity, credits
//   - Active office supplies (relics)
//   - Claim queue state
//   - NDA overlay positions
//   - Shift phase and timer state
//   - Faction standings
//   - Active compliance vows
// ============================================================

using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Desk42.Core
{
    // ── Card State ────────────────────────────────────────────

    [Serializable]
    public sealed class CardInstanceData
    {
        [JsonProperty] public string CardId;        // SO asset name (stable across builds)
        [JsonProperty] public string InstanceId;    // per-run GUID for fatigue tracking
        [JsonProperty] public int    Fatigue;        // times played this shift
        [JsonProperty] public bool   IsJammed;       // jammed at fatigue 3
        [JsonProperty] public bool   IsCrumpled;     // removed at fatigue 5
    }

    [Serializable]
    public sealed class DeckState
    {
        [JsonProperty] public List<CardInstanceData> DrawPile    = new();
        [JsonProperty] public List<CardInstanceData> Hand        = new();
        [JsonProperty] public List<CardInstanceData> DiscardPile = new();
        [JsonProperty] public List<CardInstanceData> Archive     = new(); // Archivist archetype
        [JsonProperty] public int                    DrawsPerTurn;
    }

    // ── Active Compliance Vows ────────────────────────────────

    [Serializable]
    public sealed class ActiveVow
    {
        [JsonProperty] public string VowId;   // SO GUID
        [JsonProperty] public int    Rank;    // 1, 2, or 3
    }

    // ── Claim Queue State ─────────────────────────────────────

    [Serializable]
    public sealed class ActiveClaimData
    {
        [JsonProperty] public string  ClaimId;
        [JsonProperty] public string  ClientVariantId;
        [JsonProperty] public string  ClientSpeciesId;
        [JsonProperty] public string  TemplateId;
        [JsonProperty] public string[] AnomalyTagIds;    // SO GUIDs

        // Hidden trait state
        [JsonProperty] public string  HiddenTraitId;    // null until ANALYSEd
        [JsonProperty] public bool    TraitRevealed;

        // Corruption state (form fighting back)
        [JsonProperty] public float   CorruptionLevel;  // 0-1
        [JsonProperty] public int     CorruptionSeed;   // for reproducible patterns

        // NDA state
        [JsonProperty] public bool    NDARequired;
        [JsonProperty] public bool    NDASigned;

        // Resolution
        [JsonProperty] public bool    IsResolved;
        [JsonProperty] public bool    WasHumane;

        // Generated text content (so the same claim text shows on resume)
        [JsonProperty] public string  IncidentText;
        [JsonProperty] public string  ClaimantName;
        [JsonProperty] public int     ClaimAmount;
    }

    // ── NDA Overlay State ─────────────────────────────────────

    [Serializable]
    public sealed class NDAOverlayData
    {
        [JsonProperty] public string ClaimId;
        [JsonProperty] public float  AnchorX;     // 0-1 normalised
        [JsonProperty] public float  AnchorY;
        [JsonProperty] public float  Width;
        [JsonProperty] public float  Height;
        [JsonProperty] public float  RotationDeg;
    }

    // ── Faction Standing ─────────────────────────────────────

    [Serializable]
    public sealed class FactionStandingData
    {
        [JsonProperty] public FactionID Faction;
        [JsonProperty] public float     Reputation; // -100 to 100
    }

    // ── Office Supply (Relic) State ───────────────────────────

    [Serializable]
    public sealed class ActiveSupplyData
    {
        [JsonProperty] public string SupplyId;       // SO GUID
        [JsonProperty] public string ZoneId;         // which desk zone it's in
        [JsonProperty] public int    EvolutionLevel; // 0 = base, 1+ = evolved (Expansion)
        [JsonProperty] public int    TriggerCount;   // for evolution threshold tracking

        // Supply-specific runtime state (arbitrary key-value bag)
        [JsonProperty] public Dictionary<string, float> RuntimeState = new();
    }

    // ── Run Statistics (for end-of-run screen) ────────────────

    [Serializable]
    public sealed class RunStatistics
    {
        [JsonProperty] public int   ClaimsProcessed;
        [JsonProperty] public int   HumaneResolutions;
        [JsonProperty] public int   BureaucraticResolutions;
        [JsonProperty] public int   NDAsSignedThisRun;
        [JsonProperty] public int   CardSlamsTotal;
        [JsonProperty] public int   FugueStatesSurvived;
        [JsonProperty] public float PeakMoralInjury;
        [JsonProperty] public float LowestSanity;
        [JsonProperty] public int   CreditsEarned;
        [JsonProperty] public int   CreditsSpent;
        [JsonProperty] public float EfficiencyRating;  // computed at shift end
        [JsonProperty] public List<string> BadgesEarnedIds = new();
    }

    // ── Root Run Data ─────────────────────────────────────────

    [Serializable]
    public sealed class RunData
    {
        [JsonProperty] public int    SaveVersion     = 1;
        [JsonProperty] public string SavedAtUtc;
        [JsonProperty] public bool   IsComplete;       // false = mid-run resume
        [JsonProperty] public bool   IsAbandoned;

        // Identity
        [JsonProperty] public int    MasterSeed;
        [JsonProperty] public string SeedCode;
        [JsonProperty] public int    ShiftNumber;       // which shift in the meta-run
        [JsonProperty] public string ArchetypeId;       // SO GUID of chosen archetype

        // Core gauges
        [JsonProperty] public float  Sanity;            // 0-100
        [JsonProperty] public float  SoulIntegrity;     // 0-100
        [JsonProperty] public int    CorporateCredits;
        [JsonProperty] public int    PersonalExpenseDebt;

        // Shift structure
        [JsonProperty] public ShiftPhase CurrentPhase;
        [JsonProperty] public float      ImpatenceTimerRemaining; // seconds
        [JsonProperty] public int        CurrentAnteNumber;       // which quarter/ante
        [JsonProperty] public int        ClaimsProcessedThisAnte;
        [JsonProperty] public int        QuotaForCurrentAnte;

        // Card system
        [JsonProperty] public DeckState Deck         = new();
        [JsonProperty] public int       DrawsPerTurn = 5;   // from archetype; saved for resume

        // Claim queue
        [JsonProperty] public List<ActiveClaimData>   PendingClaims  = new();
        [JsonProperty] public ActiveClaimData         ActiveClaim;   // currently at desk
        [JsonProperty] public List<ActiveClaimData>   ResolvedClaims = new();

        // Desk
        [JsonProperty] public List<ActiveSupplyData>  OfficeSuppplies = new();
        [JsonProperty] public List<NDAOverlayData>    NDAOverlays    = new();
        [JsonProperty] public float                   DeskEntropy;   // 0-1

        // Factions
        [JsonProperty] public List<FactionStandingData> FactionStandings
            = new()
            {
                new() { Faction = FactionID.Filing,            Reputation =  0f },
                new() { Faction = FactionID.Legal,             Reputation =  0f },
                new() { Faction = FactionID.OccultContainment, Reputation =  0f },
                new() { Faction = FactionID.Accounting,        Reputation =  0f },
                new() { Faction = FactionID.Management,        Reputation =  0f },
            };

        // Compliance vows active this run
        [JsonProperty] public List<ActiveVow>      ActiveVows = new();

        // Rum or mill state — memos generated so far
        [JsonProperty] public List<string>         GeneratedMemoIds = new();

        // Stats for end-of-run screen
        [JsonProperty] public RunStatistics        Stats = new();

        // ── Helpers ───────────────────────────────────────────

        public float GetFactionReputation(FactionID faction)
        {
            foreach (var f in FactionStandings)
                if (f.Faction == faction) return f.Reputation;
            return 0f;
        }

        public void ModifyFactionReputation(FactionID faction, float delta)
        {
            foreach (var f in FactionStandings)
            {
                if (f.Faction != faction) continue;
                f.Reputation = Math.Clamp(f.Reputation + delta, -100f, 100f);
                return;
            }
        }

        public void RefreshTimestamp()
            => SavedAtUtc = DateTime.UtcNow.ToString("o");
    }
}
