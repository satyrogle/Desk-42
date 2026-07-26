using Desk42.Core;
using NUnit.Framework;
using UnityEngine;

namespace Desk42.Tests.EditMode
{
    /// <summary>
    /// Bucket 2 — proof-session lifecycle boundary, and the audit that
    /// EncounterBaseline was not interposed in front of policies that must
    /// react to same-encounter mutations immediately.
    /// </summary>
    public sealed class ProofLifecycleTests
    {
        private GameObject _host;
        private EliasProofSessionController _controller;

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject(nameof(ProofLifecycleTests));
            _controller = _host.AddComponent<EliasProofSessionController>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null) Object.DestroyImmediate(_host);
        }

        // ── Same-frame registration still unlocks disposition ───

        [Test]
        public void Registration_AppliedThisEncounter_UnlocksDispositionImmediately()
        {
            _controller.BeginProofSession("baseline-audit");
            _controller.RecordAppearance(
                EliasProofContent.CanonicalClaimantId,
                EliasProofContent.Shift1AppearanceKey);
            _controller.RecordAppearance(
                EliasProofContent.CanonicalClaimantId,
                EliasProofContent.Shift2AppearanceKey);

            // Before the procedure, Shift 2 disposition is gated.
            bool blocked = _controller.TryValidateDisposition(
                EliasProofContent.CanonicalClaimantId,
                EliasProofContent.Shift2AppearanceKey,
                ClaimResolutionKind.Approve,
                out string reason);

            Assert.IsFalse(blocked,
                "Shift 2 disposition must be gated until the procedure is applied.");
            Assert.AreEqual(
                EliasProofSessionController.ProcedureRequiredFailureReason, reason);
        }

        [Test]
        public void DispositionGate_ReadsLiveState_NotAnEntrySnapshot()
        {
            _controller.BeginProofSession("live-state-audit");
            _controller.RecordAppearance(
                EliasProofContent.CanonicalClaimantId,
                EliasProofContent.Shift1AppearanceKey);

            // Shift 1 needs no procedure, so it validates on live state.
            bool ok = _controller.TryValidateDisposition(
                EliasProofContent.CanonicalClaimantId,
                EliasProofContent.Shift1AppearanceKey,
                ClaimResolutionKind.Approve,
                out string reason);

            Assert.IsTrue(ok, $"Expected Shift 1 disposition to validate, got '{reason}'.");

            // The gate must observe mutations made during THIS encounter. If a
            // frozen entry snapshot were interposed, the appearance recorded
            // moments ago would be invisible and this would fail.
            _controller.RecordAppearance(
                EliasProofContent.CanonicalClaimantId,
                EliasProofContent.Shift2AppearanceKey);

            Assert.IsTrue(_controller.State.RecordedAppearanceKeys.Contains(
                    EliasProofContent.Shift2AppearanceKey),
                "Same-encounter mutations must be visible on the live state.");
        }

        // ── EndProofSession boundary ─────────────────────────

        [Test]
        public void TryEndCompletedSession_DoesNothingBeforeShift5Disposition()
        {
            _controller.BeginProofSession("not-finished");

            Assert.IsFalse(_controller.TryEndCompletedSession(),
                "The proof must not end before Shift 5 has a terminal disposition.");
            Assert.IsTrue(_controller.HasActiveSession);
        }

        [Test]
        public void TryEndCompletedSession_IsSafeWithNoActiveSession()
        {
            Assert.IsFalse(_controller.TryEndCompletedSession());
            Assert.DoesNotThrow(() => _controller.TryEndCompletedSession());
        }

        [Test]
        public void EndProofSession_ClearsTheLiveSlot()
        {
            _controller.BeginProofSession("to-be-ended");
            _controller.RecordAppearance(
                EliasProofContent.CanonicalClaimantId,
                EliasProofContent.Shift1AppearanceKey);

            Assert.IsTrue(_controller.HasActiveSession);

            _controller.EndProofSession();

            Assert.IsFalse(_controller.HasActiveSession,
                "Clearing the live slot is what stops aftermath and appearance " +
                "keys leaking into the next session.");
            Assert.IsEmpty(_controller.State.RecordedAppearanceKeys);
        }

        [Test]
        public void EndProofSession_IsIdempotent()
        {
            _controller.BeginProofSession("double-end");
            _controller.EndProofSession();

            Assert.DoesNotThrow(() => _controller.EndProofSession());
            Assert.IsFalse(_controller.HasActiveSession);
        }

        [Test]
        public void ProductionBoundary_ExistsOnRunCompletion()
        {
            // The locked contract requires a real production caller, not a
            // test-only one. RunStateController.CompleteRun owns the boundary.
            string source = System.IO.File.ReadAllText(
                "Assets/_Project/Scripts/Core/RunStateController.cs");

            Assert.IsTrue(source.Contains("TryEndCompletedSession"),
                "RunStateController.CompleteRun must own the proof-session end " +
                "boundary — EndProofSession previously had no production caller.");
        }
    }
}
