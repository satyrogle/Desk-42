// ============================================================
// DESK 42 — Run State Controller
//
// The single source of truth for all mutable state within
// an active run. Lives on the GameManager GameObject and
// persists for the duration of a run.
//
// Responsibilities:
//   - Owns the canonical RunData instance.
//   - Exposes clean read/write API so other systems never
//     touch RunData fields directly.
//   - Listens to RumorMill events that modify run state.
//   - Fires RumorMill events when key gauges cross thresholds.
//   - Auto-saves between encounters via SaveSystem.
//
// Threading model: single-threaded Unity main thread only.
// ============================================================

using System;
using UnityEngine;
using Desk42.Archetypes;
using Desk42.Cards;
using Desk42.MoralInjury;

namespace Desk42.Core
{
    [DisallowMultipleComponent]
    public sealed class RunStateController : MonoBehaviour
    {
        // ── Singleton-style access via GameManager ─────────────
        // Not a standalone singleton — access via GameManager.Run.

        private RunData           _data;
        private IArchetype        _archetype;
        private Deck              _deck;
        private Hand              _hand;
        private MoralInjurySystem _moralInjury;

        // ── Public Access ─────────────────────────────────────

        public IArchetype        Archetype    => _archetype;
        public Deck              Deck         => _deck;
        public Hand              Hand         => _hand;
        public MoralInjurySystem MoralInjury  => _moralInjury;

        // ── Init ──────────────────────────────────────────────

        /// <summary>
        /// Start a brand new run. Initialises the SeedEngine,
        /// builds a fresh RunData, and fires the ShiftLifecycle event.
        /// </summary>
        public void BeginNewRun(int masterSeed, string archetypeId,
            int shiftNumber, MetaProgressData meta)
        {
            SeedEngine.Init(masterSeed);

            _data = new RunData
            {
                MasterSeed       = masterSeed,
                SeedCode         = SeedEngine.CurrentSeedCode,
                ShiftNumber      = shiftNumber,
                ArchetypeId      = archetypeId,
                Sanity           = 100f,
                SoulIntegrity    = 100f,
                CorporateCredits = ComputeStartingCredits(meta),
                CurrentPhase     = ShiftPhase.ClockIn,
                CurrentAnteNumber = 1,
                ImpatenceTimerRemaining = ComputeShiftDuration(meta),
            };

            ApplyStartingFactionDispositions(meta);
            BuildArchetypeAndDeck(archetypeId, meta);
            BuildMoralInjurySystem(meta);
            SubscribeToRumorMill();

            RumorMill.PublishDeferred(new ShiftLifecycleEvent(
                shiftNumber, isStart: true, ShiftPhase.ClockIn, masterSeed));
        }

        /// <summary>
        /// Resume from a saved RunData (mid-run resume after quit).
        /// Rebuilds transient state (archetype, deck, moral injury, supplies) from the saved snapshot.
        /// </summary>
        public void ResumeRun(RunData savedData, MetaProgressData meta)
        {
            _data = savedData ?? throw new ArgumentNullException(nameof(savedData));
            SeedEngine.Init(_data.MasterSeed);

            BuildArchetypeAndDeck(_data.ArchetypeId, meta, restoreFromSave: true);
            BuildMoralInjurySystem(meta);

            // Restore office supplies (must happen after SupplyManager is initialized)
            var savedSupplies = _data.OfficeSuppplies;
            if (savedSupplies != null && savedSupplies.Count > 0)
                GameManager.Instance?.Supplies?.RestoreFrom(savedSupplies);

            SubscribeToRumorMill();

            Debug.Log($"[RunStateController] Resumed run " +
                      $"(seed: {_data.SeedCode}, shift: {_data.ShiftNumber}, " +
                      $"phase: {_data.CurrentPhase})");
        }

        // ── Public Read-Only Access ───────────────────────────
        // Expose data as properties rather than the raw RunData
        // so we can enforce validation and fire events on changes.

        public int     DrawsPerTurn     => _data.DrawsPerTurn;
        public float   Sanity           => _data.Sanity;
        public float   SoulIntegrity    => _data.SoulIntegrity;
        public int     Credits          => _data.CorporateCredits;
        public ShiftPhase Phase         => _data.CurrentPhase;
        public string  ArchetypeId      => _data.ArchetypeId;
        public string  SeedCode         => _data.SeedCode;
        public int     ShiftNumber      => _data.ShiftNumber;
        public float   ImpatenceTimer   => _data.ImpatenceTimerRemaining;
        public int     CurrentAnte      => _data.CurrentAnteNumber;
        public float   DeskEntropy      => _data.DeskEntropy;
        public RunData RawData          => _data; // needed by SaveSystem only

        // Derived
        public bool    IsInFugueState   => _data.Sanity <= 0f;
        public NarratorReliability NarratorTone => ComputeNarratorTone();

        // ── Sanity ────────────────────────────────────────────

        /// <param name="delta">Negative to drain, positive to recover.</param>
        public void ModifySanity(float delta)
        {
            float prev = _data.Sanity;
            _data.Sanity = Mathf.Clamp(_data.Sanity + delta, 0f, 100f);

            if (Mathf.Approximately(prev, _data.Sanity)) return;

            bool triggeredFugue = prev > 0f && _data.Sanity <= 0f;
            RumorMill.Publish(new SanityChangedEvent(prev, _data.Sanity, triggeredFugue));

            if (triggeredFugue)
                Debug.Log("[RunStateController] Fugue State triggered.");
        }

        // ── Soul Integrity ────────────────────────────────────

        /// <param name="delta">Negative to lose soul, positive to restore.</param>
        public void ModifySoulIntegrity(float delta)
        {
            float prev = _data.SoulIntegrity;
            _data.SoulIntegrity = Mathf.Clamp(_data.SoulIntegrity + delta, 0f, 100f);

            if (Mathf.Approximately(prev, _data.SoulIntegrity)) return;

            RumorMill.PublishDeferred(
                new SoulIntegrityChangedEvent(prev, _data.SoulIntegrity));

            CheckNarratorToneThreshold(prev, _data.SoulIntegrity);
        }

        // ── Credits ───────────────────────────────────────────

        public void AddCredits(int amount)
        {
            _data.CorporateCredits += amount;
            _data.Stats.CreditsEarned += Mathf.Max(0, amount);
        }

        /// <returns>True if credits were deducted; false if insufficient funds.</returns>
        public bool SpendCredits(int amount)
        {
            if (_data.CorporateCredits < amount) return false;
            _data.CorporateCredits -= amount;
            _data.Stats.CreditsSpent += amount;
            return true;
        }

        // ── Shift Phase ───────────────────────────────────────

        public void AdvancePhase(ShiftPhase newPhase)
        {
            if (newPhase == _data.CurrentPhase) return;

            var prev = _data.CurrentPhase;
            _data.CurrentPhase = newPhase;

            RumorMill.PublishDeferred(new ShiftPhaseChangedEvent(prev, newPhase));
        }

        // ── Impatience Timer ──────────────────────────────────

        /// <summary>
        /// Called every frame by the ShiftManager with Time.deltaTime.
        /// Returns true when the timer hits zero (triggers Overtime).
        /// Applies the Office Clock 10% slow-down multiplier when active.
        /// Consumes the Clock's once-per-shift grace period before triggering Overtime.
        /// </summary>
        public bool TickTimer(float dt)
        {
            if (_data.CurrentPhase == ShiftPhase.LunchBreak) return false;

            // Apply clock multiplier: 0.9 with Office Clock, 1.0 otherwise
            var resolver = GameManager.Instance?.Supplies?.Resolver;
            float mult = resolver?.GetTimerMultiplier() ?? 1f;
            _data.ImpatenceTimerRemaining -= dt * mult;

            if (_data.ImpatenceTimerRemaining <= 0f)
            {
                // Offer the Office Clock's grace period before triggering Overtime
                if (resolver != null)
                {
                    var ctx = BuildSupplyContext();
                    if (resolver.TryConsumeClockGracePeriod(ctx))
                        return false; // grace period consumed — timer extended, no overtime yet
                }

                _data.ImpatenceTimerRemaining = 0f;
                return true;
            }
            return false;
        }

        public void ExtendTimer(float seconds)
            => _data.ImpatenceTimerRemaining += seconds;

        // ── Desk Entropy ──────────────────────────────────────

        public void AddEntropy(float amount)
        {
            _data.DeskEntropy = Mathf.Clamp01(_data.DeskEntropy + amount);
        }

        public void ResetEntropy()
        {
            _data.DeskEntropy = 0f;
            Debug.Log("[RunStateController] Desk entropy reset (Internal Audit clean).");
        }

        // ── Faction ───────────────────────────────────────────

        public float GetFactionRep(FactionID faction)
            => _data.GetFactionReputation(faction);

        public void ModifyFaction(FactionID faction, float delta)
        {
            float prev = GetFactionRep(faction);
            _data.ModifyFactionReputation(faction, delta);
            float current = GetFactionRep(faction);

            if (!Mathf.Approximately(prev, current))
                RumorMill.PublishDeferred(new FactionShiftEvent(faction, delta, current));
        }

        // ── NDA ───────────────────────────────────────────────

        public int NDACount => _data.NDAOverlays.Count;

        public void AddNDA(string claimId, UnityEngine.Vector2 region,
            float x, float y, float w, float h, float rot)
        {
            _data.NDAOverlays.Add(new NDAOverlayData
            {
                ClaimId    = claimId,
                AnchorX    = x,   AnchorY = y,
                Width      = w,   Height  = h,
                RotationDeg = rot,
            });

            _data.Stats.NDAsSignedThisRun++;
            RumorMill.PublishDeferred(
                new NDASignedEvent(claimId, NDACount, region));
        }

        // ── Claim Stats ───────────────────────────────────────

        public void RecordClaimResolved(bool humane)
        {
            _data.Stats.ClaimsProcessed++;
            if (humane) _data.Stats.HumaneResolutions++;
            else        _data.Stats.BureaucraticResolutions++;
            _data.ClaimsProcessedThisAnte++;
        }

        public void RecordCardSlam()
            => _data.Stats.CardSlamsTotal++;

        public void RecordFugueStateEntered()
        {
            _data.Stats.FugueStatesSurvived++;
            _data.Stats.LowestSanity = Mathf.Min(_data.Stats.LowestSanity, _data.Sanity);
        }

        // ── Auto-Save ─────────────────────────────────────────

        /// <summary>
        /// Save mid-run state. Call between claims and on pause.
        /// Syncs live deck and supply state into RunData before writing to disk.
        /// </summary>
        public void AutoSave()
        {
            if (_data == null || _data.IsComplete) return;

            // Sync deck: return hand cards to discard so they survive the round-trip
            if (_deck != null)
            {
                _hand?.DiscardAll(_deck);
                _data.Deck.DrawPile    = _deck.SerializeDrawPile();
                _data.Deck.DiscardPile = _deck.SerializeDiscardPile();
                _data.Deck.Archive     = _deck.SerializeArchive();
                _data.Deck.DrawsPerTurn = _data.DrawsPerTurn;
            }

            // Sync office supplies
            var supplies = GameManager.Instance?.Supplies;
            if (supplies != null)
                _data.OfficeSuppplies = supplies.Serialize();

            SaveSystem.SaveRun(_data);
        }

        /// <summary>
        /// Mark run as complete, save final state, delete mid-run save.
        /// </summary>
        public void CompleteRun()
        {
            _data.IsComplete = true;
            _data.Stats.EfficiencyRating = ComputeEfficiencyRating();
            AutoSave();
            SaveSystem.DeleteRun();
        }

        // ── Rumor Mill Subscriptions ──────────────────────────

        private void SubscribeToRumorMill()
        {
            RumorMill.OnClaimResolved       += HandleClaimResolved;
            RumorMill.OnMoralChoice         += HandleMoralChoice;
            RumorMill.OnOfficeHazard        += HandleOfficeHazard;
            RumorMill.OnExpenseUnmet        += HandleExpenseUnmet;
        }

        private void OnDestroy()
        {
            RumorMill.OnClaimResolved       -= HandleClaimResolved;
            RumorMill.OnMoralChoice         -= HandleMoralChoice;
            RumorMill.OnOfficeHazard        -= HandleOfficeHazard;
            RumorMill.OnExpenseUnmet        -= HandleExpenseUnmet;
        }

        private void HandleClaimResolved(ClaimResolvedEvent e)
        {
            RecordClaimResolved(!e.ResolvedCorrectly); // humane = not correct
            AddCredits(e.CreditsEarned);

            if (e.SoulCost > 0f)
                ModifySoulIntegrity(-e.SoulCost);
        }

        private void HandleMoralChoice(MoralChoiceEvent e)
        {
            if (e.MoralInjuryDelta > 0f)
            {
                ModifySoulIntegrity(-e.MoralInjuryDelta);
                _data.Stats.PeakMoralInjury =
                    Mathf.Max(_data.Stats.PeakMoralInjury, 100f - _data.SoulIntegrity);
                AddEntropy(0.05f);

                // Cap soul at the moral injury system's effective maximum
                if (_moralInjury != null)
                {
                    float cap = _moralInjury.EffectiveSoulCap;
                    if (_data.SoulIntegrity > cap)
                    {
                        _data.SoulIntegrity = cap;
                        RumorMill.PublishDeferred(
                            new SoulIntegrityChangedEvent(_data.SoulIntegrity, cap));
                    }
                }
            }
        }

        private void HandleOfficeHazard(OfficeHazardEvent e)
        {
            // Hazard-specific sanity costs handled here
            switch (e.HazardType)
            {
                case OfficeHazardType.MandatoryMeeting:
                    ModifySanity(-15f);
                    ExtendTimer(-30f);  // time lost to the meeting
                    break;
                case OfficeHazardType.PrinterJam:
                    ModifySanity(-5f);
                    break;
                case OfficeHazardType.SystemCrash:
                    ModifySanity(-30f);
                    break;
            }
        }

        private void HandleExpenseUnmet(ExpenseUnmetEvent e)
        {
            _data.PersonalExpenseDebt += e.AmountShort;
            AddEntropy(0.1f * e.AmountShort / 10f); // desk deteriorates
        }

        // ── Private Helpers ───────────────────────────────────

        private NarratorReliability ComputeNarratorTone()
        {
            return _data.SoulIntegrity switch
            {
                >= 76f => NarratorReliability.Professional,
                >= 51f => NarratorReliability.SlightlyHonest,
                >= 26f => NarratorReliability.Honest,
                _      => NarratorReliability.Doublespeak,
            };
        }

        private NarratorReliability _lastNarratorTone = NarratorReliability.Professional;

        private void CheckNarratorToneThreshold(float prevSoul, float newSoul)
        {
            var newTone = ComputeNarratorTone();
            if (newTone != _lastNarratorTone)
            {
                RumorMill.Publish(new NarratorToneChangedEvent(_lastNarratorTone, newTone));
                _lastNarratorTone = newTone;
            }
        }

        private float ComputeEfficiencyRating()
        {
            if (_data.Stats.ClaimsProcessed == 0) return 0f;

            // Base score from claims processed
            float score = _data.Stats.ClaimsProcessed * 100f;

            // Penalise soul damage
            score -= (100f - _data.SoulIntegrity) * 0.5f;

            // Penalise unmet expenses
            score -= _data.PersonalExpenseDebt * 2f;

            // Bonus for maintaining high sanity
            score += _data.Sanity * 0.2f;

            return Mathf.Max(0f, score);
        }

        private int ComputeStartingCredits(MetaProgressData meta)
        {
            // Base credits; Employee Handbook benefits can increase this
            int base_ = 20;
            // TODO: check meta.EmployeeHandbook for credit-boosting benefits
            return base_;
        }

        private float ComputeShiftDuration(MetaProgressData meta)
        {
            // Default ~24-minute shift in seconds
            float duration = 24f * 60f;
            // TODO: Compliance Vow "Mandatory Overtime" reduces this
            return duration;
        }

        private void ApplyStartingFactionDispositions(MetaProgressData meta)
        {
            // Seed-based starting dispositions for faction variety
            foreach (FactionID faction in System.Enum.GetValues(typeof(FactionID)))
            {
                float offset = SeedEngine.NextFloat(
                    SeedStream.FactionDispositions, -20f, 20f);
                _data.ModifyFactionReputation(faction, offset);
            }
        }

        // ── Archetype + Deck Init ─────────────────────────────

        private void BuildArchetypeAndDeck(string archetypeId, MetaProgressData meta,
            bool restoreFromSave = false)
        {
            _archetype = ArchetypeFactory.Create(archetypeId);
            _hand = new Hand(_archetype.MaxHandSize);
            _data.DrawsPerTurn = _archetype.DrawsPerTurn;

            var lib = GameManager.Instance?.Cards;

            if (restoreFromSave && _data.Deck != null)
            {
                // Restore deck from saved state (preserves fatigue/jam/crumple per card)
                _deck = Desk42.Cards.Deck.FromSaveState(
                    _data.Deck.DrawPile,
                    _data.Deck.DiscardPile,
                    _data.Deck.Archive,
                    id => lib?.Resolve(id));
            }
            else
            {
                // New run — build starting deck from archetype card IDs
                var cardIds = _archetype.BuildStartingDeckIds();
                var cards   = new System.Collections.Generic.List<PunchCardData>(cardIds.Count);

                foreach (var id in cardIds)
                {
                    var card = lib?.Resolve(id);
                    if (card != null)
                        cards.Add(card);
                    else
                        Debug.LogWarning($"[RunStateController] Starting deck card not found: '{id}'. " +
                                         $"Add it to the CardLibrary SO.");
                }

                _deck = Desk42.Cards.Deck.FromDataList(cards);
                _deck.Shuffle();

                var ctx = BuildArchetypeContext();
                _archetype.OnRunStart(ctx);
            }

            Debug.Log($"[RunStateController] Archetype: {_archetype.DisplayName}. " +
                      $"Deck: {_deck.TotalCards} cards, hand size: {_archetype.MaxHandSize}.");
        }

        private void BuildMoralInjurySystem(MetaProgressData meta)
        {
            _moralInjury = new MoralInjurySystem();
            _moralInjury.LoadScarsFromMeta(meta);
        }

        private ArchetypeContext BuildArchetypeContext() => new ArchetypeContext
        {
            Deck        = _deck,
            Hand        = _hand,
            Sanity      = _data.Sanity,
            SoulIntegrity    = _data.SoulIntegrity,
            Credits          = _data.CorporateCredits,
            ShiftNumber      = _data.ShiftNumber,
            TotalCardSlams   = _data.Stats.CardSlamsTotal,
            ActiveClientVariantId = "",

            ModifySanity         = delta  => ModifySanity(delta),
            ModifySoulIntegrity  = delta  => ModifySoulIntegrity(delta),
            AddCredits           = amount => AddCredits(amount),
            SpendCredits         = amount => SpendCredits(amount),
            EmitDarkHumour       = key   => Debug.Log($"[DarkHumour] {key}"),
        };

        /// <summary>
        /// Draw up to DrawsPerTurn cards into hand at the start of a new encounter.
        /// </summary>
        public void DrawForEncounter()
        {
            int drawn = _hand.FillFromDeck(_deck);
            Debug.Log($"[RunStateController] Drew {drawn} card(s). " +
                      $"Hand: {_hand.Count}/{_hand.MaxSize}.");
        }

        /// <summary>
        /// Build a SupplyContext snapshot for OfficeSupplyManager event dispatch.
        /// </summary>
        public OfficeSupplies.SupplyContext BuildSupplyContext() =>
            new OfficeSupplies.SupplyContext
            {
                Deck          = _deck,
                Hand          = _hand,
                Sanity        = _data?.Sanity        ?? 100f,
                SoulIntegrity = _data?.SoulIntegrity ?? 100f,
                Credits       = _data?.CorporateCredits ?? 0,
                ShiftNumber   = _data?.ShiftNumber   ?? 1,
                TotalCardSlams = _data?.Stats.CardSlamsTotal ?? 0,
                DeskEntropy   = _data?.DeskEntropy   ?? 0f,

                ModifySanity        = delta  => ModifySanity(delta),
                ModifySoulIntegrity = delta  => ModifySoulIntegrity(delta),
                AddCredits          = amount => AddCredits(amount),
                ExtendTimer         = secs   => ExtendTimer(secs),
                EmitDarkHumour      = key    => UnityEngine.Debug.Log($"[DarkHumour] {key}"),
                DrawOneCard         = ()     => _hand?.FillFromDeck(_deck),
            };

        /// <summary>
        /// Called by StateInjector after a successful slam — notifies the archetype.
        /// </summary>
        public void NotifyArchetypeOfSlam(PunchCardType cardType)
        {
            var ctx = BuildArchetypeContext();
            _archetype?.OnCardSlammed(cardType, ctx);

            // Vow check
            _archetype?.ActiveVow?.EvaluateOnSlam(cardType, ctx);
        }
    }
}
