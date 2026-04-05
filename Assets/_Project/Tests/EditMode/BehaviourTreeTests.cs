// ============================================================
// DESK 42 — Behaviour Tree Unit Tests (Edit Mode)
// ============================================================

using NUnit.Framework;
using Desk42.BehaviourTrees;

namespace Desk42.Tests.EditMode
{
    [TestFixture]
    public sealed class BehaviourTreeTests
    {
        private BTContext _ctx;

        [SetUp]
        public void SetUp()
        {
            _ctx = new BTContext { DeltaTime = 0.016f };
        }

        // ── BTActionNode ──────────────────────────────────────

        [Test]
        public void ActionNode_ReturnsCorrectStatus()
        {
            var success = new BTActionNode("success", _ => BTStatus.Success);
            var failure = new BTActionNode("failure", _ => BTStatus.Failure);
            var running = new BTActionNode("running", _ => BTStatus.Running);

            Assert.AreEqual(BTStatus.Success, success.Tick(_ctx));
            Assert.AreEqual(BTStatus.Failure, failure.Tick(_ctx));
            Assert.AreEqual(BTStatus.Running, running.Tick(_ctx));
        }

        // ── BTSelector ────────────────────────────────────────

        [Test]
        public void Selector_ReturnsFirstSuccess()
        {
            var sel = new BTSelector();
            sel.AddChild(new BTActionNode("fail1", _ => BTStatus.Failure));
            sel.AddChild(new BTActionNode("success", _ => BTStatus.Success));
            sel.AddChild(new BTActionNode("fail2", _ => BTStatus.Failure));

            Assert.AreEqual(BTStatus.Success, sel.Tick(_ctx));
        }

        [Test]
        public void Selector_FailsIfAllChildrenFail()
        {
            var sel = new BTSelector();
            sel.AddChild(new BTActionNode("f1", _ => BTStatus.Failure));
            sel.AddChild(new BTActionNode("f2", _ => BTStatus.Failure));

            Assert.AreEqual(BTStatus.Failure, sel.Tick(_ctx));
        }

        [Test]
        public void Selector_ResumesFromRunningChild()
        {
            int callCount = 0;
            var sel = new BTSelector();
            sel.AddChild(new BTActionNode("fail", _ => BTStatus.Failure));
            sel.AddChild(new BTActionNode("running", _ =>
            {
                callCount++;
                return BTStatus.Running;
            }));

            sel.Tick(_ctx);
            sel.Tick(_ctx);

            Assert.AreEqual(2, callCount, "Running child should be ticked each frame.");
        }

        // ── BTSequence ────────────────────────────────────────

        [Test]
        public void Sequence_SucceedsOnlyIfAllChildrenSucceed()
        {
            var seq = new BTSequence();
            seq.AddChild(new BTActionNode("s1", _ => BTStatus.Success));
            seq.AddChild(new BTActionNode("s2", _ => BTStatus.Success));

            Assert.AreEqual(BTStatus.Success, seq.Tick(_ctx));
        }

        [Test]
        public void Sequence_FailsOnFirstFailure()
        {
            int secondChildCalls = 0;
            var seq = new BTSequence();
            seq.AddChild(new BTActionNode("fail", _ => BTStatus.Failure));
            seq.AddChild(new BTActionNode("second", _ =>
            {
                secondChildCalls++;
                return BTStatus.Success;
            }));

            seq.Tick(_ctx);
            Assert.AreEqual(0, secondChildCalls, "Second child must NOT be called if first fails.");
        }

        // ── BTInverter ────────────────────────────────────────

        [Test]
        public void Inverter_FlipsSuccessToFailure()
        {
            var inv = new BTInverter(new BTActionNode("s", _ => BTStatus.Success));
            Assert.AreEqual(BTStatus.Failure, inv.Tick(_ctx));
        }

        [Test]
        public void Inverter_FlipsFailureToSuccess()
        {
            var inv = new BTInverter(new BTActionNode("f", _ => BTStatus.Failure));
            Assert.AreEqual(BTStatus.Success, inv.Tick(_ctx));
        }

        [Test]
        public void Inverter_PassesRunningThrough()
        {
            var inv = new BTInverter(new BTActionNode("r", _ => BTStatus.Running));
            Assert.AreEqual(BTStatus.Running, inv.Tick(_ctx));
        }

        // ── BTWaitNode ────────────────────────────────────────

        [Test]
        public void WaitNode_ReturnsRunningBeforeDurationElapses()
        {
            var wait = new BTWaitNode(1.0f);
            _ctx.DeltaTime = 0.4f;

            Assert.AreEqual(BTStatus.Running, wait.Tick(_ctx)); // 0.4s elapsed
            Assert.AreEqual(BTStatus.Running, wait.Tick(_ctx)); // 0.8s elapsed
        }

        [Test]
        public void WaitNode_ReturnsSuccessAfterDuration()
        {
            var wait = new BTWaitNode(1.0f);
            _ctx.DeltaTime = 0.6f;

            wait.Tick(_ctx);  // 0.6s
            var result = wait.Tick(_ctx);  // 1.2s → Success

            Assert.AreEqual(BTStatus.Success, result);
        }

        [Test]
        public void WaitNode_ResetsElapsedOnReset()
        {
            var wait = new BTWaitNode(1.0f);
            _ctx.DeltaTime = 0.6f;
            wait.Tick(_ctx);

            wait.Reset();
            _ctx.DeltaTime = 0.1f;
            Assert.AreEqual(BTStatus.Running, wait.Tick(_ctx), "After reset, timer should restart.");
        }

        // ── BTConditionalGate ────────────────────────────────

        [Test]
        public void ConditionalGate_BlocksChildWhenConditionFalse()
        {
            int childCalls = 0;
            var gate = new BTConditionalGate("AlwaysFalse", _ => false,
                new BTActionNode("child", _ => { childCalls++; return BTStatus.Success; }));

            var result = gate.Tick(_ctx);
            Assert.AreEqual(BTStatus.Failure, result);
            Assert.AreEqual(0, childCalls);
        }

        [Test]
        public void ConditionalGate_AllowsChildWhenConditionTrue()
        {
            int childCalls = 0;
            var gate = new BTConditionalGate("AlwaysTrue", _ => true,
                new BTActionNode("child", _ => { childCalls++; return BTStatus.Success; }));

            var result = gate.Tick(_ctx);
            Assert.AreEqual(BTStatus.Success, result);
            Assert.AreEqual(1, childCalls);
        }

        // ── BTBlockerNode ─────────────────────────────────────

        [Test]
        public void BlockerNode_AlwaysReturnFailure()
        {
            var blocker = new BTBlockerNode("ThreatAudit", "retained_counsel");
            Assert.AreEqual(BTStatus.Failure, blocker.Tick(_ctx));
            Assert.AreEqual(BTStatus.Failure, blocker.Tick(_ctx));
        }

        [Test]
        public void BlockerNode_StopsSequenceFromProgressing()
        {
            int childCallCount = 0;
            var seq = new BTSequence();
            seq.AddChild(new BTBlockerNode("ThreatAudit", "pre_filed_exemption"));
            seq.AddChild(new BTActionNode("shouldNotRun", _ =>
            {
                childCallCount++;
                return BTStatus.Success;
            }));

            seq.Tick(_ctx);
            Assert.AreEqual(0, childCallCount, "Blocker must stop Sequence execution.");
        }

        // ── BehaviourTree (wrapper) ───────────────────────────

        [Test]
        public void BehaviourTree_AutoResets_AfterCompletion()
        {
            int runCount = 0;
            var node = new BTActionNode("counter", _ =>
            {
                runCount++;
                return BTStatus.Success; // completes immediately
            });
            var bt = new BehaviourTree(node, null);

            // Tick 3 times — should auto-reset and run 3 times
            bt.Tick(0.016f, 100f, 100f, 0f);
            bt.Tick(0.016f, 100f, 100f, 0f);
            bt.Tick(0.016f, 100f, 100f, 0f);

            Assert.AreEqual(3, runCount, "BT should auto-reset and re-run each tick.");
        }

        [Test]
        public void BehaviourTree_Pause_StopsExecution()
        {
            int runCount = 0;
            var bt = new BehaviourTree(
                new BTActionNode("counter", _ => { runCount++; return BTStatus.Success; }),
                null);

            bt.Pause();
            bt.Tick(0.016f, 100f, 100f, 0f);
            bt.Tick(0.016f, 100f, 100f, 0f);

            // Paused BT returns Running without executing
            Assert.AreEqual(0, runCount, "Paused BT must not execute.");
        }

        [Test]
        public void BehaviourTree_InsertBlocker_PreventsCardFromWorking()
        {
            var root = new BTSelector();
            root.AddChild(new BTActionNode("always", _ => BTStatus.Success));

            var bt = new BehaviourTree(root, null);
            Assert.IsFalse(bt.HasBlockerForCard("ThreatAudit"));

            bt.InsertBlockerForCard("ThreatAudit", "retained_counsel");
            Assert.IsTrue(bt.HasBlockerForCard("ThreatAudit"));
        }

        // ── BTContext Blackboard ──────────────────────────────

        [Test]
        public void BTContext_Blackboard_SetAndGet()
        {
            _ctx.Set("myInt", 42);
            _ctx.Set("myStr", "hello");

            Assert.AreEqual(42,      _ctx.Get<int>("myInt"));
            Assert.AreEqual("hello", _ctx.Get<string>("myStr"));
        }

        [Test]
        public void BTContext_Blackboard_ClearRemovesAllEntries()
        {
            _ctx.Set("key", 99);
            _ctx.Clear();

            Assert.AreEqual(0, _ctx.Get<int>("key", 0));
            Assert.IsFalse(_ctx.Has("key"));
        }

        [Test]
        public void BTContext_Blackboard_DefaultValueOnMissing()
        {
            Assert.AreEqual(-1, _ctx.Get<int>("missing", -1));
        }
    }
}
