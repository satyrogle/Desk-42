// ============================================================
// DESK 42 — BSM Unit Tests (Edit Mode)
// ============================================================

using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Desk42.BSM;
using Desk42.BSM.States;
using Desk42.BSM.Transitions;
using Desk42.Core;

namespace Desk42.Tests.EditMode
{
    [TestFixture]
    public sealed class BSMTests
    {
        // ── Stubs ──────────────────────────────────────────────

        /// <summary>Minimal IClientState stub for stack tests.</summary>
        private sealed class StubState : IClientState
        {
            public ClientStateID StateID    => ClientStateID.Pending;
            public float         Duration   { get; }
            public bool          IsInjected => false;

            public int  EnterCount;
            public int  TickCount;
            public int  ExitCount;
            public bool TickReturnValue = true;

            public StubState(float duration = 0f) { Duration = duration; }

            public void Enter(ClientContext ctx) => EnterCount++;
            public bool Tick(ClientContext ctx, float dt) { TickCount++; return TickReturnValue; }
            public void Exit(ClientContext ctx)  => ExitCount++;
        }

        private static ClientContext MakeCtx() => new ClientContext
        {
            ClientVariantId     = "test_client",
            ClientSpeciesId     = "human",
            VisitCount          = 0,
            ActiveCounterTraits = new List<string>(),
            RequestTransition   = _ => { },
            RequestIdle         = () => { },
            TriggerTell         = (_, __) => { },
            EmitDarkHumour      = _ => { },
        };

        // Tracks GameObjects created per-test for clean teardown
        private readonly List<GameObject> _toDestroy = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _toDestroy)
                if (go != null) Object.DestroyImmediate(go);
            _toDestroy.Clear();
        }

        // ── ClientStateStack ──────────────────────────────────

        [Test]
        public void Stack_IsEmpty_Initially()
        {
            var stack = new ClientStateStack();
            Assert.IsTrue(stack.IsEmpty);
            Assert.AreEqual(0, stack.Depth);
            Assert.AreEqual(0f, stack.TimeRemaining);
        }

        [Test]
        public void Stack_Push_EntersStateAndSetsActive()
        {
            var stack = new ClientStateStack();
            var state = new StubState();
            stack.Push(state, MakeCtx());

            Assert.IsFalse(stack.IsEmpty);
            Assert.AreEqual(1, stack.Depth);
            Assert.AreEqual(1, state.EnterCount);
            Assert.AreEqual(state, stack.ActiveState);
        }

        [Test]
        public void Stack_Tick_ReturnsTrueWhileActive()
        {
            var stack = new ClientStateStack();
            stack.Push(new StubState(), MakeCtx());

            bool result = stack.Tick(MakeCtx(), 0.1f);
            Assert.IsTrue(result, "Stack must return true while a state is active.");
        }

        [Test]
        public void Stack_Tick_ReturnsFalse_WhenEmpty()
        {
            var stack  = new ClientStateStack();
            bool result = stack.Tick(MakeCtx(), 0.1f);
            Assert.IsFalse(result, "Empty stack must return false.");
        }

        [Test]
        public void Stack_TimerExpiry_PopsAndCallsExit()
        {
            var ctx   = MakeCtx();
            var stack = new ClientStateStack();
            var state = new StubState(duration: 0.5f);
            stack.Push(state, ctx);

            stack.Tick(ctx, 0.3f); // 0.3s — not expired
            Assert.IsFalse(stack.IsEmpty, "State should still be active at 0.3 s.");

            stack.Tick(ctx, 0.3f); // 0.6s — expired
            Assert.IsTrue(stack.IsEmpty, "State should be popped after timer expires.");
            Assert.AreEqual(1, state.ExitCount, "Exit must be called exactly once.");
        }

        [Test]
        public void Stack_TimeRemaining_DecreasesWithTick()
        {
            var ctx   = MakeCtx();
            var stack = new ClientStateStack();
            stack.Push(new StubState(duration: 1.0f), ctx);

            float before = stack.TimeRemaining;
            stack.Tick(ctx, 0.4f);
            float after = stack.TimeRemaining;

            Assert.Less(after, before);
            Assert.AreEqual(0.6f, after, 0.001f);
        }

        [Test]
        public void Stack_NoTimer_TimeRemainingIsInfinity()
        {
            var stack = new ClientStateStack();
            stack.Push(new StubState(duration: 0f), MakeCtx());

            Assert.AreEqual(float.PositiveInfinity, stack.TimeRemaining);
        }

        [Test]
        public void Stack_SelfTermination_PopsAndCallsExit()
        {
            var ctx   = MakeCtx();
            var stack = new ClientStateStack();
            var state = new StubState(duration: 0f) { TickReturnValue = false };
            stack.Push(state, ctx);

            bool result = stack.Tick(ctx, 0.1f);

            Assert.IsFalse(result, "Stack should be empty after self-terminating state.");
            Assert.IsTrue(stack.IsEmpty);
            Assert.AreEqual(1, state.ExitCount);
        }

        [Test]
        public void Stack_ForcePopTop_ExitsAndRemovesState()
        {
            var ctx   = MakeCtx();
            var stack = new ClientStateStack();
            var state = new StubState();
            stack.Push(state, ctx);

            stack.ForcePopTop(ctx);

            Assert.IsTrue(stack.IsEmpty);
            Assert.AreEqual(1, state.ExitCount);
        }

        [Test]
        public void Stack_ForcePopTop_OnEmptyStack_DoesNothing()
        {
            var stack = new ClientStateStack();
            Assert.DoesNotThrow(() => stack.ForcePopTop(MakeCtx()));
        }

        [Test]
        public void Stack_ClearAll_ExitsAllStates()
        {
            var ctx   = MakeCtx();
            var stack = new ClientStateStack();
            var a     = new StubState();
            var b     = new StubState();
            stack.Push(a, ctx);
            stack.Push(b, ctx);

            stack.ClearAll(ctx);

            Assert.IsTrue(stack.IsEmpty);
            Assert.AreEqual(1, a.ExitCount, "Bottom state must be exited.");
            Assert.AreEqual(1, b.ExitCount, "Top state must be exited.");
        }

        [Test]
        public void Stack_OnlyTopStateTicks()
        {
            var ctx    = MakeCtx();
            var stack  = new ClientStateStack();
            var bottom = new StubState();
            var top    = new StubState();
            stack.Push(bottom, ctx);
            stack.Push(top, ctx);

            stack.Tick(ctx, 0.1f);

            Assert.AreEqual(1, top.TickCount,   "Top state must be ticked.");
            Assert.AreEqual(0, bottom.TickCount, "Bottom state must NOT tick while top is active.");
        }

        [Test]
        public void Stack_AfterTopPops_BottomBecomesActive()
        {
            var ctx    = MakeCtx();
            var stack  = new ClientStateStack();
            var bottom = new StubState();
            var top    = new StubState(duration: 0.1f);
            stack.Push(bottom, ctx);
            stack.Push(top, ctx);

            stack.Tick(ctx, 0.2f); // expires top
            Assert.AreEqual(bottom, stack.ActiveState);
        }

        // ── TransitionRule Conditions ─────────────────────────

        [Test]
        public void IsRepeatOffender_FalseOnFirstVisit()
        {
            var cond = new IsRepeatOffenderCondition();
            Assert.IsFalse(cond.Evaluate(new TransitionContext { VisitCount = 0 }));
        }

        [Test]
        public void IsRepeatOffender_TrueAfterFirstVisit()
        {
            var cond = new IsRepeatOffenderCondition();
            Assert.IsTrue(cond.Evaluate(new TransitionContext { VisitCount = 1 }));
            Assert.IsTrue(cond.Evaluate(new TransitionContext { VisitCount = 5 }));
        }

        [Test]
        public void SoulIntegrityBelow_EvaluatesThreshold()
        {
            var cond = new SoulIntegrityBelowCondition { Threshold = 40f };

            Assert.IsTrue(cond.Evaluate(new TransitionContext { SoulIntegrity = 30f }));
            Assert.IsFalse(cond.Evaluate(new TransitionContext { SoulIntegrity = 40f })); // strict <
            Assert.IsFalse(cond.Evaluate(new TransitionContext { SoulIntegrity = 50f }));
        }

        [Test]
        public void SoulIntegrityAbove_EvaluatesThreshold()
        {
            var cond = new SoulIntegrityAboveCondition { Threshold = 60f };

            Assert.IsTrue(cond.Evaluate(new TransitionContext { SoulIntegrity = 60f }));  // >=
            Assert.IsTrue(cond.Evaluate(new TransitionContext { SoulIntegrity = 80f }));
            Assert.IsFalse(cond.Evaluate(new TransitionContext { SoulIntegrity = 59f }));
        }

        [Test]
        public void ImpatienceAbove_StrictGreaterThan()
        {
            var cond = new ImpatienceAboveCondition { Threshold = 0.7f };

            Assert.IsTrue(cond.Evaluate(new TransitionContext { ImpatienceRatio = 0.8f }));
            Assert.IsFalse(cond.Evaluate(new TransitionContext { ImpatienceRatio = 0.7f })); // strict >
            Assert.IsFalse(cond.Evaluate(new TransitionContext { ImpatienceRatio = 0.5f }));
        }

        [Test]
        public void HasCounterTrait_TrueIfPresent()
        {
            var cond = new HasCounterTraitCondition { TraitId = "retained_counsel" };
            var ctx  = new TransitionContext
            {
                ActiveCounterTraits = new List<string> { "retained_counsel", "loud_chewer" }
            };
            Assert.IsTrue(cond.Evaluate(ctx));
        }

        [Test]
        public void HasCounterTrait_FalseIfAbsent()
        {
            var cond = new HasCounterTraitCondition { TraitId = "retained_counsel" };
            var ctx  = new TransitionContext
            {
                ActiveCounterTraits = new List<string> { "loud_chewer" }
            };
            Assert.IsFalse(cond.Evaluate(ctx));
        }

        [Test]
        public void HasCounterTrait_FalseIfListNull()
        {
            var cond = new HasCounterTraitCondition { TraitId = "retained_counsel" };
            Assert.IsFalse(cond.Evaluate(new TransitionContext { ActiveCounterTraits = null }));
        }

        [Test]
        public void SpeciesIs_CaseInsensitive()
        {
            var cond = new SpeciesIsCondition { SpeciesId = "Human" };

            Assert.IsTrue(cond.Evaluate(new TransitionContext { ClientSpeciesId = "human" }));
            Assert.IsTrue(cond.Evaluate(new TransitionContext { ClientSpeciesId = "HUMAN" }));
            Assert.IsFalse(cond.Evaluate(new TransitionContext { ClientSpeciesId = "phantom" }));
        }

        [Test]
        public void CurrentStateIs_MatchesCorrectly()
        {
            var cond = new CurrentStateIsCondition { RequiredState = ClientStateID.Agitated };

            Assert.IsTrue(cond.Evaluate(new TransitionContext
                { CurrentState = ClientStateID.Agitated }));
            Assert.IsFalse(cond.Evaluate(new TransitionContext
                { CurrentState = ClientStateID.Pending }));
        }

        [Test]
        public void OfficeTempIs_CaseInsensitive()
        {
            var cond = new OfficeTempIsCondition { RequiredTemp = "PEAK_EFFICIENCY" };

            Assert.IsTrue(cond.Evaluate(new TransitionContext
                { OfficeTemp = "peak_efficiency" }));
            Assert.IsFalse(cond.Evaluate(new TransitionContext
                { OfficeTemp = "LUNCH_BREAK" }));
        }

        [Test]
        public void FactionReputation_AboveThreshold()
        {
            var cond = new FactionReputationCondition
            {
                Faction      = FactionID.Legal,
                Threshold    = 50f,
                RequireAbove = true,
            };

            Assert.IsTrue(cond.Evaluate(new TransitionContext { LegalReputation = 60f }));
            Assert.IsTrue(cond.Evaluate(new TransitionContext { LegalReputation = 50f }));  // >=
            Assert.IsFalse(cond.Evaluate(new TransitionContext { LegalReputation = 40f }));
        }

        [Test]
        public void FactionReputation_BelowThreshold()
        {
            var cond = new FactionReputationCondition
            {
                Faction      = FactionID.Management,
                Threshold    = 30f,
                RequireAbove = false,
            };

            Assert.IsTrue(cond.Evaluate(new TransitionContext { ManagementReputation = 20f }));
            Assert.IsFalse(cond.Evaluate(new TransitionContext { ManagementReputation = 30f })); // <
        }

        // ── TransitionRule ────────────────────────────────────

        [Test]
        public void TransitionRule_ActionMismatch_ReturnsFalse()
        {
            var rule = new TransitionRule
            {
                TriggerAction = "ThreatAudit",
                TargetState   = ClientStateID.Agitated,
                Conditions    = new List<ITransitionCondition>(),
            };
            Assert.IsFalse(rule.Evaluate(new TransitionContext { TriggerAction = "Redact" }));
        }

        [Test]
        public void TransitionRule_ActionMatch_NoConditions_ReturnsTrue()
        {
            var rule = new TransitionRule
            {
                TriggerAction = "ThreatAudit",
                TargetState   = ClientStateID.Agitated,
                Conditions    = new List<ITransitionCondition>(),
            };
            Assert.IsTrue(rule.Evaluate(new TransitionContext { TriggerAction = "ThreatAudit" }));
        }

        [Test]
        public void TransitionRule_EmptyTrigger_MatchesAnyAction()
        {
            // TriggerAction = "" means the rule fires for ANY action if conditions pass
            var rule = new TransitionRule
            {
                TriggerAction = "",
                TargetState   = ClientStateID.Pending,
                Conditions    = new List<ITransitionCondition>(),
            };
            Assert.IsTrue(rule.Evaluate(new TransitionContext { TriggerAction = "Anything" }));
        }

        [Test]
        public void TransitionRule_ConditionFails_ReturnsFalse()
        {
            var rule = new TransitionRule
            {
                TriggerAction = "ThreatAudit",
                TargetState   = ClientStateID.Litigious,
                Conditions    = new List<ITransitionCondition>
                {
                    new IsRepeatOffenderCondition() // requires VisitCount > 0
                },
            };
            Assert.IsFalse(rule.Evaluate(new TransitionContext
            {
                TriggerAction = "ThreatAudit",
                VisitCount    = 0, // first visit → condition fails
            }));
        }

        [Test]
        public void TransitionRule_AllConditionsPass_ReturnsTrue()
        {
            var rule = new TransitionRule
            {
                TriggerAction = "ThreatAudit",
                TargetState   = ClientStateID.Litigious,
                Conditions    = new List<ITransitionCondition>
                {
                    new IsRepeatOffenderCondition(),
                    new SoulIntegrityBelowCondition { Threshold = 80f },
                },
            };
            Assert.IsTrue(rule.Evaluate(new TransitionContext
            {
                TriggerAction = "ThreatAudit",
                VisitCount    = 1,
                SoulIntegrity = 50f,
            }));
        }

        [Test]
        public void TransitionRule_SecondConditionFails_ReturnsFalse()
        {
            var rule = new TransitionRule
            {
                TriggerAction = "ThreatAudit",
                TargetState   = ClientStateID.Litigious,
                Conditions    = new List<ITransitionCondition>
                {
                    new IsRepeatOffenderCondition(),           // passes
                    new SoulIntegrityBelowCondition { Threshold = 40f }, // fails
                },
            };
            Assert.IsFalse(rule.Evaluate(new TransitionContext
            {
                TriggerAction = "ThreatAudit",
                VisitCount    = 1,
                SoulIntegrity = 80f, // above threshold — fails
            }));
        }

        // ── TransitionTable ───────────────────────────────────

        [Test]
        public void TransitionTable_RepeatOffender_ThreatAudit_ResolvesToLitigious()
        {
            var table = TransitionTable.CreateDefault();

            // VisitCount > 0 → IsRepeatOffender passes → Litigious (priority 20)
            var ctx = new TransitionContext
            {
                TriggerAction = "ThreatAudit",
                VisitCount    = 1,
            };
            Assert.AreEqual(ClientStateID.Litigious, table.Resolve(ctx));
        }

        [Test]
        public void TransitionTable_FirstVisit_ThreatAudit_FallsBackToPending()
        {
            var table = TransitionTable.CreateDefault();

            // VisitCount = 0 → both ThreatAudit rules require IsRepeatOffender → no match → fallback
            var ctx = new TransitionContext
            {
                TriggerAction = "ThreatAudit",
                VisitCount    = 0,
            };
            Assert.AreEqual(ClientStateID.Pending, table.Resolve(ctx));
        }

        [Test]
        public void TransitionTable_LowSoul_Redact_ResolvesToParanoid()
        {
            var table = TransitionTable.CreateDefault();

            var ctx = new TransitionContext
            {
                TriggerAction = "Redact",
                SoulIntegrity = 20f, // below 40 threshold
            };
            Assert.AreEqual(ClientStateID.Paranoid, table.Resolve(ctx));
        }

        [Test]
        public void TransitionTable_HighSoul_Redact_ResolvesToSuspicious()
        {
            var table = TransitionTable.CreateDefault();

            var ctx = new TransitionContext
            {
                TriggerAction = "Redact",
                SoulIntegrity = 80f, // above threshold → paranoid rule skipped
            };
            Assert.AreEqual(ClientStateID.Suspicious, table.Resolve(ctx));
        }

        [Test]
        public void TransitionTable_NoMatch_ReturnsFallback()
        {
            var table = TransitionTable.CreateDefault();

            var ctx = new TransitionContext { TriggerAction = "CooperationRoute" };
            Assert.AreEqual(ClientStateID.Pending, table.Resolve(ctx));
        }

        [Test]
        public void TransitionTable_AddRuntimeRule_IsEvaluated()
        {
            var table = TransitionTable.CreateDefault();
            table.AddRuntimeRule(new TransitionRule
            {
                TriggerAction = "CooperationRoute",
                TargetState   = ClientStateID.Cooperative,
                Priority      = 100,
                Conditions    = new List<ITransitionCondition>(),
            });

            var ctx = new TransitionContext { TriggerAction = "CooperationRoute" };
            Assert.AreEqual(ClientStateID.Cooperative, table.Resolve(ctx));
        }

        [Test]
        public void TransitionTable_RuntimeRuleOutprioritisesBase()
        {
            var table = TransitionTable.CreateDefault();
            // Add runtime rule with very high priority for ThreatAudit
            table.AddRuntimeRule(new TransitionRule
            {
                TriggerAction = "ThreatAudit",
                TargetState   = ClientStateID.Litigious,
                Priority      = 999,
                Conditions    = new List<ITransitionCondition>(),
            });

            // Even first-visit should now get Litigious due to high-priority runtime rule
            var ctx = new TransitionContext
            {
                TriggerAction = "ThreatAudit",
                VisitCount    = 0,
            };
            Assert.AreEqual(ClientStateID.Litigious, table.Resolve(ctx));
        }

        [Test]
        public void TransitionTable_ClearRuntimeRules_FallsBackToBase()
        {
            var table = TransitionTable.CreateDefault();
            table.AddRuntimeRule(new TransitionRule
            {
                TriggerAction = "CooperationRoute",
                TargetState   = ClientStateID.Cooperative,
                Priority      = 100,
                Conditions    = new List<ITransitionCondition>(),
            });
            table.ClearRuntimeRules();

            var ctx = new TransitionContext { TriggerAction = "CooperationRoute" };
            Assert.AreEqual(ClientStateID.Pending, table.Resolve(ctx),
                "After clearing runtime rules, fallback should apply.");
        }

        // ── ClientStateMachine (MonoBehaviour) ────────────────

        private ClientStateMachine CreateCSM(
            string       variantId  = "test_client",
            int          visitCount = 0,
            List<string> traits     = null)
        {
            var go  = new GameObject($"CSM_{variantId}");
            _toDestroy.Add(go);
            var csm = go.AddComponent<ClientStateMachine>();
            csm.Initialize(variantId, "human", visitCount,
                traits ?? new List<string>());
            return csm;
        }

        [Test]
        public void CSM_Initializes_WithPendingState()
        {
            var csm = CreateCSM();
            Assert.AreEqual(ClientStateID.Pending, csm.CurrentMoodState);
        }

        [Test]
        public void CSM_Initializes_BaseBTNotNull()
        {
            var csm = CreateCSM();
            Assert.IsNotNull(csm.BaseBT);
        }

        [Test]
        public void CSM_Initializes_BaseBTNotPaused()
        {
            var csm = CreateCSM();
            Assert.IsFalse(csm.BaseBT.IsPaused);
        }

        [Test]
        public void CSM_IsNotInInjectedState_Initially()
        {
            var csm = CreateCSM();
            Assert.IsFalse(csm.IsInInjectedState);
        }

        [Test]
        public void CSM_TryInject_BlockedByBTBlocker()
        {
            var csm = CreateCSM();
            csm.BaseBT.InsertBlockerForCard("ThreatAudit", "retained_counsel");

            var result = csm.TryInject("ThreatAudit");

            Assert.AreEqual(
                ClientStateMachine.InjectionResult.BlockedByCounterTrait, result);
            Assert.IsFalse(csm.IsInInjectedState);
        }

        [Test]
        public void CSM_TryInject_DissociatingClient_Returns_ClientDissociating()
        {
            var csm = CreateCSM();

            // Force internal state to Dissociating via reflection
            var field = typeof(ClientStateMachine).GetField(
                "_currentMoodState",
                BindingFlags.NonPublic | BindingFlags.Instance);
            field.SetValue(csm, ClientStateID.Dissociating);

            var result = csm.TryInject("PendingReview", 5f);

            Assert.AreEqual(
                ClientStateMachine.InjectionResult.ClientDissociating, result);
        }

        [Test]
        public void CSM_TryInject_PendingReview_Success_PushesStack()
        {
            var csm = CreateCSM();

            var result = csm.TryInject("PendingReview", 5f);

            Assert.AreEqual(ClientStateMachine.InjectionResult.Success, result);
            Assert.IsTrue(csm.IsInInjectedState);
        }

        [Test]
        public void CSM_TryInject_PausesBaseBT()
        {
            var csm = CreateCSM();

            csm.TryInject("PendingReview", 5f);

            Assert.IsTrue(csm.BaseBT.IsPaused, "Base BT must pause during injected state.");
        }

        [Test]
        public void CSM_TryInject_SetsNewMoodState()
        {
            var csm = CreateCSM();

            // Build a table that maps PendingReview → Suspicious
            var table = TransitionTable.CreateDefault();
            table.AddRuntimeRule(new TransitionRule
            {
                TriggerAction = "PendingReview",
                TargetState   = ClientStateID.Suspicious,
                Priority      = 999,
                Conditions    = new List<ITransitionCondition>(),
            });

            var go  = new GameObject("CSM_moody");
            _toDestroy.Add(go);
            var csm2 = go.AddComponent<ClientStateMachine>();
            csm2.Initialize("moody", "human", 0, new List<string>(), table);

            csm2.TryInject("PendingReview", 5f);

            Assert.AreEqual(ClientStateID.Suspicious, csm2.CurrentMoodState);
        }

        [Test]
        public void CSM_TryInject_OrganicCard_ReturnsBlockedByCurrentState()
        {
            // ThreatAudit → CreateInjectedState returns null → BlockedByCurrentState
            var csm    = CreateCSM();
            var result = csm.TryInject("ThreatAudit");

            Assert.AreEqual(
                ClientStateMachine.InjectionResult.BlockedByCurrentState, result);
            Assert.IsFalse(csm.IsInInjectedState,
                "Organic-only cards must not push to the injection stack.");
        }

        [Test]
        public void CSM_TryInject_FiresOnStateChangedEvent()
        {
            var table = TransitionTable.CreateDefault();
            table.AddRuntimeRule(new TransitionRule
            {
                TriggerAction = "PendingReview",
                TargetState   = ClientStateID.Cooperative,
                Priority      = 999,
                Conditions    = new List<ITransitionCondition>(),
            });

            var go = new GameObject("CSM_event");
            _toDestroy.Add(go);
            var csm = go.AddComponent<ClientStateMachine>();
            csm.Initialize("event_test", "human", 0, new List<string>(), table);

            ClientStateID capturedFrom = ClientStateID.Smug; // sentinel
            ClientStateID capturedTo   = ClientStateID.Smug;
            csm.OnStateChanged += (from, to) => { capturedFrom = from; capturedTo = to; };

            csm.TryInject("PendingReview", 5f);

            Assert.AreEqual(ClientStateID.Pending,      capturedFrom);
            Assert.AreEqual(ClientStateID.Cooperative,  capturedTo);
        }

        [Test]
        public void CSM_RepeatOffender_ThreatAudit_BlockedByBlocker()
        {
            // Simulate a repeat offender who has the retained_counsel trait
            var csm = CreateCSM(visitCount: 1, traits: new List<string> { "retained_counsel" });
            csm.BaseBT.InsertBlockerForCard("ThreatAudit", "retained_counsel");

            var result = csm.TryInject("ThreatAudit");

            Assert.AreEqual(
                ClientStateMachine.InjectionResult.BlockedByCounterTrait, result,
                "Repeat offender with retained_counsel must block ThreatAudit.");
        }

        [Test]
        public void CSM_LegalHold_Inject_Success()
        {
            var csm    = CreateCSM();
            var result = csm.TryInject("LegalHold", 8f);

            Assert.AreEqual(ClientStateMachine.InjectionResult.Success, result);
            Assert.IsTrue(csm.IsInInjectedState);
        }

        [Test]
        public void CSM_Expedite_Inject_Success()
        {
            var csm    = CreateCSM();
            var result = csm.TryInject("Expedite", 3f);

            Assert.AreEqual(ClientStateMachine.InjectionResult.Success, result);
            Assert.IsTrue(csm.IsInInjectedState);
        }
    }
}
