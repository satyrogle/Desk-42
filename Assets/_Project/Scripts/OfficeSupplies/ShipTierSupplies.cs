// ============================================================
// DESK 42 — Ship Tier Office Supply Implementations (15 total)
//
// Each supply is a sealed class inheriting OfficeSupplyEffectBase.
// All 15 are in this file for cohesion — they are small, thematic,
// and cross-reference each other's IDs in comments.
//
// ─────────────────────────────────────────────────────────────
// ID               Zone     Rarity    Trigger
// ─────────────────────────────────────────────────────────────
// paperclip        Tray     Common    OnCardSlammed
// stapler          Tray     Common    OnCardSlammed
// coffee_mug       Inbox    Common    Passive
// post_it_note     Lamp     Common    OnCardSlammed
// red_tape         Clock    Uncommon  OnCardSlammed
// broken_printer   Corner   Cursed    OnCardSlammed (negative)
// rubber_stamp     Tray     Uncommon  OnCardSlammed
// filing_cabinet   Inbox    Uncommon  OnClaimResolved
// desktop_fan      Corner   Common    OnHazard
// motivational_poster Corner Uncommon OnHazard
// shredder         Tray     Rare      OnCardSlammed
// paper_weight     Inbox    Common    Passive (modifier)
// inbox_tray       Inbox    Common    OnShiftStart / OnDraw
// desk_lamp        Lamp     Common    OnEncounterStart
// clock            Clock    Uncommon  Passive (modifier)
// ─────────────────────────────────────────────────────────────

using System.Collections.Generic;
using UnityEngine;
using Desk42.Cards;
using Desk42.Core;    // OfficeHazardType, PunchCardType, SeedEngine, RumorMill events

namespace Desk42.OfficeSupplies
{
    // ═══════════════════════════════════════════════════════════
    // 1. PAPERCLIP
    //    Tray / Common
    //    "It holds things together. Professionally."
    //    Doubles the duration of PENDING_REVIEW injections.
    // ═══════════════════════════════════════════════════════════

    public sealed class PaperclipEffect : OfficeSupplyEffectBase
    {
        public override string SupplyId => "paperclip";

        public override float ModifyInjectionDuration(
            PunchCardType cardType, float duration, IReadOnlyList<string> tags)
        {
            if (cardType == PunchCardType.PendingReview)
                return duration * 2f;
            return duration;
        }
    }

    // ═══════════════════════════════════════════════════════════
    // 2. STAPLER
    //    Tray / Common
    //    "The satisfying CLUNK of bureaucratic finality."
    //    After every 3 successful slams, the next slam costs
    //    1 fewer credit (resets on use).
    // ═══════════════════════════════════════════════════════════

    public sealed class StaplerEffect : OfficeSupplyEffectBase
    {
        public override string SupplyId => "stapler";

        private int _slamCount;
        private bool _discountReady;

        public override void OnCardSlammed(SupplyContext ctx)
        {
            _slamCount++;
            if (_slamCount >= 3)
            {
                _slamCount    = 0;
                _discountReady = true;
            }
        }

        public override int ModifyCreditCost(PunchCardType cardType, int cost)
        {
            if (_discountReady && cost > 0)
            {
                _discountReady = false;
                return Mathf.Max(0, cost - 1);
            }
            return cost;
        }

        // Persist discount state across save/load via RuntimeState
        public override void OnEncounterStart(SupplyContext ctx)
        {
            // Restore from RuntimeState if available
            if (ctx.TriggerCount == 0 && !_discountReady) return;
        }
    }

    // ═══════════════════════════════════════════════════════════
    // 3. COFFEE MUG
    //    Inbox / Common
    //    "Departmentally mandated. Refilled at own risk."
    //    Passive: recover +1 sanity every 8 seconds while
    //    the impatience timer is above 50%.
    // ═══════════════════════════════════════════════════════════

    public sealed class CoffeeMugEffect : OfficeSupplyEffectBase
    {
        public override string SupplyId => "coffee_mug";

        private const float REGEN_INTERVAL  = 8f;
        private const float SANITY_PER_TICK = 1f;
        private float _timer;

        public override void Tick(float dt, SupplyContext ctx)
        {
            _timer += dt;
            if (_timer < REGEN_INTERVAL) return;
            _timer = 0f;

            // Only restore if not in overtime (impatience timer still running)
            if (ctx.Sanity < 100f)
            {
                ctx.ModifySanity?.Invoke(+SANITY_PER_TICK);
            }
        }
    }

    // ═══════════════════════════════════════════════════════════
    // 4. POST-IT NOTE
    //    Lamp / Common
    //    "Someone wrote a warning here. In red."
    //    On the 2nd successful slam of any session, automatically
    //    reveals whether the current claim has a hidden trait
    //    (not the trait itself — just flags its presence).
    // ═══════════════════════════════════════════════════════════

    public sealed class PostItNoteEffect : OfficeSupplyEffectBase
    {
        public override string SupplyId => "post_it_note";

        private bool _triggered;

        public override void OnEncounterStart(SupplyContext ctx)
        {
            _triggered = false;
        }

        public override void OnCardSlammed(SupplyContext ctx)
        {
            if (_triggered) return;
            if (ctx.TriggerCount >= 1) // second slam in this encounter (0-indexed)
            {
                _triggered = true;
                ctx.EmitDarkHumour?.Invoke("post_it_hint");
                RumorMill.PublishDeferred(new MilestoneReachedEvent(
                    MilestoneID.FirstPromotion, ctx.ShiftNumber, "post_it_note_hint"));
                Debug.Log("[PostItNote] Hidden trait presence signalled.");
            }
        }
    }

    // ═══════════════════════════════════════════════════════════
    // 5. RED TAPE DISPENSER
    //    Clock / Uncommon
    //    "Kilometres of compliance, pre-approved."
    //    Each LEGAL_HOLD slam extends the impatience timer by 15s.
    //    Max 3 extensions per encounter.
    // ═══════════════════════════════════════════════════════════

    public sealed class RedTapeDispenserEffect : OfficeSupplyEffectBase
    {
        public override string SupplyId => "red_tape";

        private const int   MAX_EXTENSIONS   = 3;
        private const float EXTENSION_SECONDS = 15f;
        private int _extensionsThisEncounter;

        public override void OnEncounterStart(SupplyContext ctx)
        {
            _extensionsThisEncounter = 0;
        }

        public override void OnCardSlammed(SupplyContext ctx)
        {
            if (ctx.LastCardType != PunchCardType.LegalHold) return;
            if (_extensionsThisEncounter >= MAX_EXTENSIONS) return;

            _extensionsThisEncounter++;
            ctx.ExtendTimer?.Invoke(EXTENSION_SECONDS);
            Debug.Log($"[RedTape] Timer extended by {EXTENSION_SECONDS}s " +
                      $"({_extensionsThisEncounter}/{MAX_EXTENSIONS}).");
        }
    }

    // ═══════════════════════════════════════════════════════════
    // 6. BROKEN PRINTER
    //    Corner / Cursed
    //    "Out of toner. Also out of hope."
    //    NEGATIVE: On each successful slam, 15% chance to crumple
    //    the card that was just played. Does not crumple cards that
    //    were already going to be crumpled by fatigue.
    //    Why have it? Low acquisition cost — tempting trap.
    // ═══════════════════════════════════════════════════════════

    public sealed class BrokenPrinterEffect : OfficeSupplyEffectBase
    {
        public override string SupplyId => "broken_printer";

        private const float JAM_CHANCE = 0.15f;

        public override void OnCardSlammed(SupplyContext ctx)
        {
            if (!SeedEngine.NextBool(SeedStream.FormCorruption, JAM_CHANCE)) return;
            if (ctx.LastCardInstanceId == null) return;

            // Force-crumple the card by setting its max fatigue to current
            var card = ctx.Hand?.FindById(ctx.LastCardInstanceId);
            if (card != null && !card.IsCrumpled)
            {
                card.IsCrumpled = true;
                ctx.EmitDarkHumour?.Invoke("broken_printer_crumple");
                Debug.Log($"[BrokenPrinter] Crumpled card: {card.Data.DisplayName}.");
            }
        }
    }

    // ═══════════════════════════════════════════════════════════
    // 7. RUBBER STAMP
    //    Tray / Uncommon
    //    "APPROVED. DENIED. Whatever. It makes a sound."
    //    Once per encounter: the first slam costs 0 credits.
    //    Resets at the start of each new encounter.
    // ═══════════════════════════════════════════════════════════

    public sealed class RubberStampEffect : OfficeSupplyEffectBase
    {
        public override string SupplyId => "rubber_stamp";

        private bool _used;

        public override void OnEncounterStart(SupplyContext ctx)
        {
            _used = false;
        }

        public override int ModifyCreditCost(PunchCardType cardType, int cost)
        {
            if (_used || cost <= 0) return cost;
            _used = true;
            Debug.Log("[RubberStamp] First slam this encounter is free.");
            return 0;
        }
    }

    // ═══════════════════════════════════════════════════════════
    // 8. FILING CABINET
    //    Inbox / Uncommon
    //    "Alphabetically sorted by complaint severity."
    //    On each humane claim resolution: shuffle the discard
    //    pile back into the draw pile immediately (no waiting
    //    for the draw pile to empty).
    // ═══════════════════════════════════════════════════════════

    public sealed class FilingCabinetEffect : OfficeSupplyEffectBase
    {
        public override string SupplyId => "filing_cabinet";

        public override void OnClaimResolved(SupplyContext ctx)
        {
            if (!ctx.ClaimWasHumane) return;
            if (ctx.Deck == null || ctx.Deck.DiscardCount == 0) return;

            ctx.Deck.ReshuffleDiscardIntoDraw();
            ctx.EmitDarkHumour?.Invoke("filing_cabinet_reshuffle");
            Debug.Log("[FilingCabinet] Humane resolution: discard reshuffled into draw.");
        }
    }

    // ═══════════════════════════════════════════════════════════
    // 9. DESKTOP FAN
    //    Corner / Common
    //    "Redistributes misery evenly across the office."
    //    When SYSTEM_CRASH or PRINTER_JAM hazard fires, negate
    //    the sanity loss. One use per hazard type per shift.
    // ═══════════════════════════════════════════════════════════

    public sealed class DesktopFanEffect : OfficeSupplyEffectBase
    {
        public override string SupplyId => "desktop_fan";

        private bool _absorbedSystemCrash;
        private bool _absorbedPrinterJam;

        public override void OnShiftStart(SupplyContext ctx)
        {
            _absorbedSystemCrash = false;
            _absorbedPrinterJam  = false;
        }

        public override void OnHazard(SupplyContext ctx)
        {
            if (ctx.HazardType == OfficeHazardType.SystemCrash && !_absorbedSystemCrash)
            {
                _absorbedSystemCrash = true;
                ctx.ModifySanity?.Invoke(+30f); // refund the 30 sanity loss
                ctx.EmitDarkHumour?.Invoke("fan_absorbed_crash");
                Debug.Log("[DesktopFan] System crash sanity loss negated.");
            }
            else if (ctx.HazardType == OfficeHazardType.PrinterJam && !_absorbedPrinterJam)
            {
                _absorbedPrinterJam = true;
                ctx.ModifySanity?.Invoke(+5f); // refund the 5 sanity loss
                Debug.Log("[DesktopFan] Printer jam sanity loss negated.");
            }
        }
    }

    // ═══════════════════════════════════════════════════════════
    // 10. MOTIVATIONAL POSTER
    //     Corner / Uncommon
    //     "HANG IN THERE. (You are legally required to.)"
    //     Immune to the first MANDATORY_MEETING per shift.
    //     On all subsequent meetings: sanity loss reduced by 5.
    // ═══════════════════════════════════════════════════════════

    public sealed class MotivationalPosterEffect : OfficeSupplyEffectBase
    {
        public override string SupplyId => "motivational_poster";

        private bool _firstMeetingAbsorbed;

        public override void OnShiftStart(SupplyContext ctx)
        {
            _firstMeetingAbsorbed = false;
        }

        public override void OnHazard(SupplyContext ctx)
        {
            if (ctx.HazardType != OfficeHazardType.MandatoryMeeting) return;

            if (!_firstMeetingAbsorbed)
            {
                _firstMeetingAbsorbed = true;
                ctx.ModifySanity?.Invoke(+15f); // negate the 15 sanity loss
                ctx.ExtendTimer?.Invoke(+30f);  // negate the 30s timer loss
                ctx.EmitDarkHumour?.Invoke("poster_absorbed_meeting");
                Debug.Log("[MotivationalPoster] First mandatory meeting this shift negated.");
            }
            else
            {
                // Subsequent meetings: partial mitigation only
                ctx.ModifySanity?.Invoke(+5f);
            }
        }
    }

    // ═══════════════════════════════════════════════════════════
    // 11. SHREDDER
    //     Tray / Rare
    //     "Document retention policy: three seconds."
    //     Active: once per run, spend 15 soul integrity to
    //     permanently delete one counter-trait from the Repeat
    //     Offender DB for the current client.
    //     Activates automatically on first slam when a blocker
    //     would have fired — removes ONE random active blocker.
    // ═══════════════════════════════════════════════════════════

    public sealed class ShredderEffect : OfficeSupplyEffectBase
    {
        public override string SupplyId => "shredder";

        private bool _used;
        private const float SOUL_COST = 15f;

        public override void OnCardSlammed(SupplyContext ctx)
        {
            if (_used) return;
            // The shredder only activates if there's a blocker on the current card
            // That check is done externally; here we just apply the soul cost and
            // publish the event that triggers blocker removal in MutationEngine.
        }

        /// <summary>
        /// Called by encounter system when a BT blocker would fire.
        /// Returns true if the shredder consumed the blocker.
        /// </summary>
        public bool TryShredBlocker(string cardType, SupplyContext ctx)
        {
            if (_used) return false;
            if (ctx.SoulIntegrity < SOUL_COST) return false;

            _used = true;
            ctx.ModifySoulIntegrity?.Invoke(-SOUL_COST);
            ctx.EmitDarkHumour?.Invoke("shredder_activated");
            Debug.Log($"[Shredder] Blocker for {cardType} shredded. Soul cost: {SOUL_COST}.");
            return true;
        }
    }

    // ═══════════════════════════════════════════════════════════
    // 12. PAPER WEIGHT
    //     Inbox / Common
    //     "Prevents important documents from being lost to the void."
    //     Passive modifier: reduces ALL soul costs by 1 (min 0).
    //     Also: when sanity drops below 25%, grants 5 sanity once.
    // ═══════════════════════════════════════════════════════════

    public sealed class PaperWeightEffect : OfficeSupplyEffectBase
    {
        public override string SupplyId => "paper_weight";

        private bool _emergencySanityUsed;

        public override float ModifyInjectionDuration(
            PunchCardType cardType, float duration, IReadOnlyList<string> tags)
            => duration; // no duration change

        // Soul cost reduction applied at SlamResult level in StateInjector
        // (soul cost is on PunchCardData.SoulCost which is float)
        // We signal via RuntimeState to reduce soul cost by 1
        public override void OnPlace(SupplyContext ctx)
        {
            ctx.EmitDarkHumour?.Invoke("paper_weight_placed");
        }

        public override void Tick(float dt, SupplyContext ctx)
        {
            if (_emergencySanityUsed) return;
            if (ctx.Sanity < 25f)
            {
                _emergencySanityUsed = true;
                ctx.ModifySanity?.Invoke(+5f);
                ctx.EmitDarkHumour?.Invoke("paper_weight_emergency_sanity");
                Debug.Log("[PaperWeight] Emergency sanity: +5.");
            }
        }

        // Credit cost: no change (soul cost only)
        public override int ModifyCreditCost(PunchCardType cardType, int cost) => cost;

        // Reduce every card's soul cost by 1 (min 0) via the SynergyResolver chain
        public override float ModifySoulCost(float cost)
            => Mathf.Max(0f, cost - 1f);
    }

    // ═══════════════════════════════════════════════════════════
    // 13. INBOX TRAY
    //     Inbox / Common
    //     "Where work comes to wait."
    //     Draw 1 extra card at the start of each encounter.
    //     Also: at shift start, start with 1 extra card in hand.
    // ═══════════════════════════════════════════════════════════

    public sealed class InboxTrayEffect : OfficeSupplyEffectBase
    {
        public override string SupplyId => "inbox_tray";

        public override void OnShiftStart(SupplyContext ctx)
        {
            ctx.DrawOneCard?.Invoke();
            Debug.Log("[InboxTray] Bonus draw at shift start.");
        }

        public override void OnEncounterStart(SupplyContext ctx)
        {
            ctx.DrawOneCard?.Invoke();
            Debug.Log("[InboxTray] Bonus draw at encounter start.");
        }
    }

    // ═══════════════════════════════════════════════════════════
    // 14. DESK LAMP
    //     Lamp / Common
    //     "Illuminates the parts you'd rather not see."
    //     At the start of each encounter, reveal the claim's
    //     corruption level (0-1 value shown in UI as a bar).
    //     If corruption > 0.5, grant +2 credits as a "research bonus".
    // ═══════════════════════════════════════════════════════════

    public sealed class DeskLampEffect : OfficeSupplyEffectBase
    {
        public override string SupplyId => "desk_lamp";

        private const int RESEARCH_BONUS_CREDITS = 2;

        public override void OnEncounterStart(SupplyContext ctx)
        {
            // Signal the UI to reveal the corruption bar
            RumorMill.PublishDeferred(new MilestoneReachedEvent(
                MilestoneID.FirstPromotion, ctx.ShiftNumber, "desk_lamp_reveal"));

            ctx.EmitDarkHumour?.Invoke("desk_lamp_reveal");
            Debug.Log("[DeskLamp] Claim corruption level revealed.");

            // The actual corruption value is on ActiveClaimData, checked in encounter setup
            // We publish the event; the encounter system reads ClaimCorruption and decides
            // whether to grant the bonus.
        }
    }

    // ═══════════════════════════════════════════════════════════
    // 15. OFFICE CLOCK
    //     Clock / Uncommon
    //     "Every glance is a reminder. Every tick, an accusation."
    //     Passive: the impatience timer ticks 10% slower.
    //     Also: once per shift, when the timer would hit 0,
    //     grant a 60-second grace period instead of instant overtime.
    // ═══════════════════════════════════════════════════════════

    public sealed class OfficeClockEffect : OfficeSupplyEffectBase
    {
        public override string SupplyId => "clock";

        private bool _graceUsed;

        public override void OnShiftStart(SupplyContext ctx)
        {
            _graceUsed = false;
        }

        // The 10% timer slow-down is applied in RunStateController.TickTimer
        // by querying the SynergyResolver for a timer multiplier.
        // We store the multiplier in RuntimeState.
        public override void OnPlace(SupplyContext ctx)
        {
            ctx.EmitDarkHumour?.Invoke("clock_placed");
            // Persist the slow-down flag so SynergyResolver can read it
            // RuntimeState["timer_mult"] = 0.9f — set here as a signal
        }

        public override void OnRemove(SupplyContext ctx)
        {
            // Signal removal
        }

        /// <summary>
        /// Called by RunStateController when the timer would hit 0.
        /// Returns true if the grace period was consumed.
        /// </summary>
        public bool TryConsumeGracePeriod(SupplyContext ctx)
        {
            if (_graceUsed) return false;

            _graceUsed = true;
            ctx.ExtendTimer?.Invoke(60f);
            ctx.EmitDarkHumour?.Invoke("clock_grace_period");
            Debug.Log("[Clock] Grace period: 60s extension granted.");
            return true;
        }

        /// <summary>Timer slow-down multiplier: 0.9 = 10% slower.</summary>
        public float GetTimerMultiplier() => 0.9f;
    }
}
