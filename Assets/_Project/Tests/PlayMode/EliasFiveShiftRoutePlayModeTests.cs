using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using Desk42.Core;
using Desk42.Encounter;
using Desk42.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Desk42.Tests.PlayMode
{
    /// <summary>
    /// Runs the complete five-shift proof through the real EncounterManager
    /// transaction for all three authored branches. Persistence is redirected
    /// before Boot and the player's real save directory is fingerprinted before
    /// and after every route.
    /// </summary>
    public sealed class EliasFiveShiftRoutePlayModeTests
    {
        private string _testSaveDirectory;
        private Dictionary<string, string> _realSaveFingerprint;
        private GameObject _encounterHost;
        private GameObject _overlayHost;

        [SetUp]
        public void SetUp()
        {
            if (GameManager.Instance != null)
            {
                UnityEngine.Object.DestroyImmediate(
                    GameManager.Instance.gameObject);
            }
            RumorMill.ClearAllSubscriptions();

            _realSaveFingerprint =
                FingerprintPlayerSaveFiles(
                    Application.persistentDataPath);
            _testSaveDirectory = Path.Combine(
                Path.GetTempPath(),
                $"Desk42_FiveShiftRoute_{Guid.NewGuid():N}");
            SaveSystem.SetSaveDirectoryOverrideForTests(
                _testSaveDirectory);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_encounterHost != null)
                UnityEngine.Object.DestroyImmediate(_encounterHost);
            if (_overlayHost != null)
                UnityEngine.Object.DestroyImmediate(_overlayHost);
            if (GameManager.Instance != null)
            {
                UnityEngine.Object.DestroyImmediate(
                    GameManager.Instance.gameObject);
            }

            RumorMill.ClearAllSubscriptions();
            SaveSystem.WipeAllSaveData();
            SaveSystem.ClearSaveDirectoryOverrideForTests();
            if (Directory.Exists(_testSaveDirectory))
                Directory.Delete(_testSaveDirectory, recursive: true);

            Dictionary<string, string> after =
                FingerprintPlayerSaveFiles(
                    Application.persistentDataPath);
            CollectionAssert.AreEquivalent(
                _realSaveFingerprint.Keys, after.Keys,
                "The proof harness changed the real save directory's files.");
            foreach (KeyValuePair<string, string> file
                     in _realSaveFingerprint)
            {
                Assert.AreEqual(
                    file.Value, after[file.Key],
                    $"The proof harness changed real save bytes: " +
                    $"{file.Key}");
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator RouteA_NormalisedAddress_CompletesFiveShiftProof()
            => RunRoute(RouteDefinition.NormalisedAddress);

        [UnityTest]
        public IEnumerator RouteB_LegacyException_CompletesFiveShiftProof()
            => RunRoute(RouteDefinition.LegacyException);

        [UnityTest]
        public IEnumerator RouteC_PhysicalVerification_CompletesFiveShiftProof()
            => RunRoute(RouteDefinition.PhysicalVerification);

        private IEnumerator RunRoute(RouteDefinition route)
        {
            SceneManager.LoadScene("Boot");
            yield return WaitForBoot();

            GameManager manager = GameManager.Instance;
            Assert.IsNotNull(manager);
            Assert.IsNotNull(manager.EliasContent);

            var fixtureMeta = new MetaProgressData
            {
                TutorialCompleted = true,
                HighestPhaseReached = 4,
            };
            manager.SetMetaForTesting(fixtureMeta);
            manager.EliasProof.BeginProofSession(route.TestId);

            // Initialize canonical run state before constructing UI that reads
            // faction and modifier values during Awake.
            List<ActiveClaimData> shift1 =
                BeginAndScheduleShift(manager, route, 1,
                    out ActiveClaimData shift1Elias);

            CreateLiveEncounterSurface(
                out EncounterManager encounter,
                out ShiftFeedbackOverlay overlay,
                out EliasProcedureReceiptPresenter presenter);

            var receiptBeats = new List<EliasProcedureReceiptBeat>();
            presenter.BeatPresented += (_, beat) =>
                receiptBeats.Add(beat);

            // Shift 1: first authored visit through the real encounter.
            Assert.AreEqual(5, shift1.Count);
            yield return QueueAndWait(encounter, shift1Elias);
            AssertEncounterIdentity(
                encounter, shift1Elias, expectedVisitCount: 0);
            Resolve(encounter, route.Shift1Disposition);
            Assert.AreEqual(
                route.Shift1Disposition
                    == ClaimResolutionKind.Approve
                    ? EliasShift1Disposition.Approved
                    : EliasShift1Disposition.Denied,
                manager.EliasProof.State.Shift1Disposition);

            // Shift 2: branch-writing procedure is nonterminal, receipt is
            // presented, then the ordinary disposition closes the claim.
            receiptBeats.Clear();
            List<ActiveClaimData> shift2 =
                BeginAndScheduleShift(manager, route, 2,
                    out ActiveClaimData shift2Elias);
            Assert.AreEqual(5, shift2.Count);
            yield return QueueAndWait(encounter, shift2Elias);
            AssertEncounterIdentity(
                encounter, shift2Elias, expectedVisitCount: 1);

            Assert.IsTrue(encounter.TryPreviewEliasProcedure(
                route.Shift2Action, out ProjectedEliasProcedure shift2Preview,
                out string shift2PreviewFailure), shift2PreviewFailure);
            Assert.IsFalse(shift2Preview.IsTerminal);
            Assert.AreEqual(route.Branch, shift2Preview.ResultingBranch);

            Assert.IsTrue(encounter.TryApplyEliasProcedure(
                route.Shift2Action, out AppliedEliasProcedure shift2Applied,
                out string shift2ApplyFailure), shift2ApplyFailure);
            Assert.IsFalse(shift2Applied.IsTerminal);
            Assert.AreSame(
                shift2Elias, encounter.ActiveClaim,
                "Procedure must not terminate the claim.");
            Assert.AreEqual(route.Branch, shift2Applied.ResultingBranch);
            Assert.AreEqual(route.Shift2ReceiptId, shift2Applied.ReceiptId);
            yield return WaitForReceipt(presenter);
            AssertShift2Receipt(route, shift2Applied, receiptBeats);

            Resolve(encounter, route.Shift2Disposition);
            Assert.AreEqual(
                route.Shift2Disposition,
                manager.EliasProof.State.Shift2FinalDisposition);

            // Shifts 3 and 4 deliberately contain no Elias appearance.
            List<ActiveClaimData> shift3 =
                BeginOrdinaryShift(manager, route, 3);
            Assert.IsFalse(EliasProofScheduler.TryReplaceScheduledClaim(
                shift3, 3, manager.EliasContent,
                manager.EliasProof.State, out _));
            Assert.IsFalse(shift3.Any(IsElias));

            List<ActiveClaimData> shift4 =
                BeginOrdinaryShift(manager, route, 4);
            Assert.IsFalse(EliasProofScheduler.TryReplaceScheduledClaim(
                shift4, 4, manager.EliasContent,
                manager.EliasProof.State, out _));
            Assert.IsFalse(shift4.Any(IsElias));

            // Shift 5: branch selects the authored claim, locks the exact
            // compromised tool, then activates the branch-specific aftermath.
            receiptBeats.Clear();
            List<ActiveClaimData> shift5 =
                BeginAndScheduleShift(manager, route, 5,
                    out ActiveClaimData shift5Elias);
            Assert.AreEqual(route.Shift5ClaimId, shift5Elias.ClaimId);
            Assert.AreSame(shift5Elias, shift5[2]);
            yield return QueueAndWait(encounter, shift5Elias);
            AssertEncounterIdentity(
                encounter, shift5Elias, expectedVisitCount: 2);

            AssertShift5ToolAvailability(encounter, route);
            Assert.IsTrue(encounter.TryApplyEliasProcedure(
                route.Shift5Action, out AppliedEliasProcedure shift5Applied,
                out string shift5ApplyFailure), shift5ApplyFailure);
            Assert.AreSame(shift5Elias, encounter.ActiveClaim);
            yield return WaitForReceipt(presenter);

            Resolve(encounter, route.Shift5Disposition);
            EliasAftermathModifierState modifier =
                manager.EliasProof.State.ActiveAftermathModifier;
            Assert.IsNotNull(modifier);
            Assert.AreEqual(route.AftermathModifierId, modifier.ModifierId);
            Assert.IsTrue(modifier.IsActive);

            EliasAftermathDefinition aftermath =
                EliasAftermathPolicy.ForBranch(
                    manager.EliasContent, route.Branch);
            CollectionAssert.AreEquivalent(
                aftermath.ClaimIds, modifier.PendingClaimIds);

            // An unrelated encounter must not consume the condition.
            int pendingBefore = modifier.PendingClaimIds.Count;
            var unrelated = BuildOrdinaryClaim(
                route.TestId, 5, 99);
            yield return QueueAndWait(encounter, unrelated);
            Assert.AreEqual(pendingBefore, modifier.PendingClaimIds.Count);
            Resolve(encounter, ClaimResolutionKind.Deny);

            foreach (string aftermathClaimId in aftermath.ClaimIds)
            {
                ActiveClaimData authored = shift5.Single(
                    claim => claim.ClaimId == aftermathClaimId);
                yield return QueueAndWait(encounter, authored);
                Assert.IsTrue(
                    modifier.AppliedClaimIds.Contains(aftermathClaimId));
                StringAssert.Contains(
                    FormatIdentifier(modifier.ModifierId),
                    overlay.RenderedClientModifiers);
                Resolve(encounter, ClaimResolutionKind.Approve);
            }

            Assert.IsTrue(modifier.IsExpired);
            Assert.IsFalse(modifier.IsActive);
            CollectionAssert.AreEquivalent(
                aftermath.ClaimIds, modifier.AppliedClaimIds);

            EliasProofRunRecord record =
                manager.EliasProof.CaptureInstrumentation();
            AssertInstrumentation(route, record);
        }

        private static IEnumerator WaitForBoot()
        {
            float deadline = Time.realtimeSinceStartup + 20f;
            while ((GameManager.Instance == null
                    || SceneManager.GetActiveScene().name != "MainMenu")
                && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.IsNotNull(GameManager.Instance);
            Assert.AreEqual(
                "MainMenu", SceneManager.GetActiveScene().name);
        }

        private void CreateLiveEncounterSurface(
            out EncounterManager encounter,
            out ShiftFeedbackOverlay overlay,
            out EliasProcedureReceiptPresenter presenter)
        {
            _overlayHost = new GameObject("FiveShiftProofOverlay");
            overlay = _overlayHost.AddComponent<ShiftFeedbackOverlay>();
            presenter =
                _overlayHost.GetComponent<EliasProcedureReceiptPresenter>();
            Assert.IsNotNull(presenter);
            SetPrivateFloat(
                presenter, "_standardBeatSeconds", 0.01f);
            SetPrivateFloat(
                presenter, "_memoryAnchorSeconds", 0.01f);
            SetPrivateFloat(
                presenter, "_rewardBeatSeconds", 0.01f);
            SetPrivateFloat(
                presenter, "_finalHoldSeconds", 0f);

            _encounterHost = new GameObject("FiveShiftProofEncounter");
            LogAssert.Expect(LogType.Error,
                "[EncounterManager] _punchCardMachine not assigned.");
            LogAssert.Expect(LogType.Error,
                "[EncounterManager] _clientView not assigned.");
            LogAssert.Expect(LogType.Error,
                "[EncounterManager] _claimPanel not assigned.");
            LogAssert.Expect(LogType.Error,
                "[EncounterManager] _cardHandView not assigned.");
            LogAssert.Expect(LogType.Error,
                "[EncounterManager] _clientAnchor not assigned.");
            encounter =
                _encounterHost.AddComponent<EncounterManager>();
        }

        private static List<ActiveClaimData> BeginAndScheduleShift(
            GameManager manager,
            RouteDefinition route,
            int shiftNumber,
            out ActiveClaimData elias)
        {
            List<ActiveClaimData> claims =
                BeginOrdinaryShift(manager, route, shiftNumber);
            Assert.IsTrue(
                EliasProofScheduler.TryReplaceScheduledClaim(
                    claims, shiftNumber, manager.EliasContent,
                    manager.EliasProof.State, out elias));
            Assert.AreEqual(1, claims.Count(IsElias));
            return claims;
        }

        private static List<ActiveClaimData> BeginOrdinaryShift(
            GameManager manager,
            RouteDefinition route,
            int shiftNumber)
        {
            manager.Run.BeginNewRun(
                route.Seed + shiftNumber,
                "auditor",
                shiftNumber,
                manager.Meta);
            return Enumerable.Range(1, 5)
                .Select(slot => BuildOrdinaryClaim(
                    route.TestId, shiftNumber, slot))
                .ToList();
        }

        private static ActiveClaimData BuildOrdinaryClaim(
            string routeId, int shiftNumber, int slot)
            => new()
            {
                ClaimId =
                    $"{routeId}_shift_{shiftNumber}_slot_{slot}",
                ClientVariantId =
                    $"{routeId}_claimant_{shiftNumber}_{slot}",
                ClientSpeciesId = "moth_accountant",
                TemplateId = "five_shift_route_fixture",
                IncidentText = "Deterministic route fixture claim.",
                ClaimantName = "Fixture Claimant",
                ClaimAmount = 42,
                AnomalyTagIds = Array.Empty<string>(),
                IsResolved = false,
                ResolutionKind = ClaimResolutionKind.Unspecified,
            };

        private static IEnumerator QueueAndWait(
            EncounterManager encounter, ActiveClaimData claim)
        {
            RumorMill.PublishDeferred(
                new ClaimQueuedEvent(claim, remaining: 0));
            float deadline = Time.realtimeSinceStartup + 3f;
            while (!ReferenceEquals(encounter.ActiveClaim, claim)
                && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.AreSame(
                claim, encounter.ActiveClaim,
                $"Encounter did not start for '{claim.ClaimId}'.");
        }

        private static IEnumerator WaitForReceipt(
            EliasProcedureReceiptPresenter presenter)
        {
            Assert.IsTrue(
                presenter.IsPresenting,
                "Applied procedure did not start its protected receipt.");
            float deadline = Time.realtimeSinceStartup + 3f;
            while (presenter.IsPresenting
                && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.IsFalse(
                presenter.IsPresenting,
                "Protected receipt did not complete.");
        }

        private static void Resolve(
            EncounterManager encounter,
            ClaimResolutionKind kind)
        {
            Assert.IsNotNull(encounter.ActiveClaim);
            if (kind == ClaimResolutionKind.Approve)
                encounter.Approve();
            else if (kind == ClaimResolutionKind.Deny)
                encounter.Deny();
            else
                Assert.Fail($"Unsupported route disposition: {kind}");
            Assert.IsNull(
                encounter.ActiveClaim,
                "The ordinary disposition did not terminate the claim.");
        }

        private static void AssertEncounterIdentity(
            EncounterManager encounter,
            ActiveClaimData claim,
            int expectedVisitCount)
        {
            Assert.AreEqual(
                EliasProofContent.CanonicalClaimantId,
                claim.ClientVariantId);
            Assert.IsNotNull(encounter.ActiveClient);
            Assert.AreEqual(
                expectedVisitCount, encounter.ActiveClient.VisitCount);
        }

        private static void AssertShift2Receipt(
            RouteDefinition route,
            AppliedEliasProcedure result,
            IReadOnlyList<EliasProcedureReceiptBeat> beats)
        {
            Assert.IsNotEmpty(beats);
            Assert.AreEqual(
                EliasProcedureReceiptBeatKind.Action,
                beats[0].Kind);
            Assert.AreEqual(
                result.ReceiptId, route.Shift2ReceiptId);

            if (route.Branch != EliasShift2Branch.NormalisedAddress)
                return;

            CollectionAssert.AreEqual(
                new[]
                {
                    "RECORD AMENDED",
                    "18B -> 18A",
                    EliasProcedurePolicy.MiriamRegisteredAt18A,
                    "CLAIM ACCEPTED FOR PROCESSING",
                    "COMPLIANCE STREAK +1",
                },
                beats.Select(beat => beat.Text).ToArray());
            int anchorIndex = beats.ToList().FindIndex(
                beat => beat.Kind
                    == EliasProcedureReceiptBeatKind.MemoryAnchor);
            int rewardIndex = beats.ToList().FindIndex(
                beat => beat.Kind
                    == EliasProcedureReceiptBeatKind.AppliedDelta);
            Assert.Greater(rewardIndex, anchorIndex);
        }

        private static void AssertShift5ToolAvailability(
            EncounterManager encounter,
            RouteDefinition route)
        {
            bool clarificationAvailable =
                encounter.TryPreviewEliasProcedure(
                    EliasProcedureActionId.RequestClarification,
                    out _, out string clarificationReason);
            bool referralAvailable =
                encounter.TryPreviewEliasProcedure(
                    EliasProcedureActionId.ReferForReview,
                    out _, out string referralReason);

            Assert.AreEqual(
                route.ClarificationAvailable,
                clarificationAvailable,
                clarificationReason);
            Assert.AreEqual(
                route.ReferralAvailable,
                referralAvailable,
                referralReason);

            if (route.LockedShift5Action
                == EliasProcedureActionId.Unspecified)
            {
                return;
            }

            Assert.IsFalse(encounter.TryApplyEliasProcedure(
                route.LockedShift5Action, out _,
                out string lockedReason));
            Assert.AreEqual(
                EliasProcedureFailureReason.ToolLockedByBranch.ToString(),
                lockedReason);
            Assert.IsFalse(
                GameManager.Instance.EliasProof.State
                    .AppliedProcedureAppearanceKeys.Contains(
                        EliasProofContent.Shift5AppearanceKey));
        }

        private static void AssertInstrumentation(
            RouteDefinition route,
            EliasProofRunRecord record)
        {
            Assert.AreEqual(route.TestId, record.AnonymisedTestId);
            Assert.AreEqual(
                EliasProofContent.CanonicalClaimantId,
                record.EliasStableId);
            Assert.AreEqual(1, record.Shift1VisitCount);
            Assert.AreEqual(2, record.Shift2VisitCount);
            Assert.AreEqual(3, record.Shift5VisitCount);
            Assert.AreEqual(
                route.Shift1Disposition
                    == ClaimResolutionKind.Approve
                    ? EliasShift1Disposition.Approved
                    : EliasShift1Disposition.Denied,
                record.Shift1Disposition);
            Assert.AreEqual(route.Branch, record.Shift2Branch);
            Assert.AreEqual(route.Branch, record.Shift5BranchLoaded);
            Assert.AreEqual(
                route.Shift2ReceiptId, record.Shift2ReceiptId);
            Assert.AreEqual(
                route.Shift2Disposition, record.Shift2Resolution);
            Assert.AreEqual(
                route.ExpectedComplianceStreakDelta,
                record.ComplianceStreakDelta,
                0.001f);
            Assert.AreEqual(0f, record.AuditRiskDelta, 0.001f);
            Assert.AreEqual(
                route.Shift5ClaimId, record.Shift5LoadedClaimId);
            Assert.AreEqual(
                route.CompromisedTool, record.CompromisedToolState);
            Assert.AreEqual(
                route.Shift5Action
                    == EliasProcedureActionId.RequestClarification
                    ? "elias_shift5_request_clarification"
                    : "elias_shift5_refer_for_review",
                record.Shift5ReceiptId);
            Assert.AreEqual(
                route.Shift5Disposition, record.Shift5Resolution);
            Assert.AreEqual(
                route.AftermathModifierId,
                record.TemporaryModifierApplied);
        }

        private static bool IsElias(ActiveClaimData claim)
            => string.Equals(
                claim?.ClientVariantId,
                EliasProofContent.CanonicalClaimantId,
                StringComparison.Ordinal);

        private static string FormatIdentifier(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;
            var chars = new List<char>(value.Length + 8);
            for (int i = 0; i < value.Length; i++)
            {
                char current = value[i];
                if (i > 0 && char.IsUpper(current)
                    && !char.IsWhiteSpace(value[i - 1]))
                {
                    chars.Add(' ');
                }
                chars.Add(char.ToUpperInvariant(current));
            }
            return new string(chars.ToArray());
        }

        private static void SetPrivateFloat(
            object target, string fieldName, float value)
            => target.GetType()
                .GetField(
                    fieldName,
                    BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(target, value);

        private static Dictionary<string, string> FingerprintPlayerSaveFiles(
            string directory)
        {
            var result = new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);
            if (!Directory.Exists(directory))
                return result;

            string[] saveFileNames =
            {
                "meta.json",
                "meta.json.bak",
                "run.json",
                "run.json.bak",
                "offender_db.json",
            };
            using SHA256 sha = SHA256.Create();
            foreach (string fileName in saveFileNames)
            {
                string path = Path.Combine(directory, fileName);
                if (!File.Exists(path))
                    continue;
                using FileStream stream = File.OpenRead(path);
                result[fileName] =
                    BitConverter.ToString(
                            sha.ComputeHash(stream))
                        .Replace("-", string.Empty);
            }
            return result;
        }

        private sealed class RouteDefinition
        {
            public static readonly RouteDefinition NormalisedAddress =
                new(
                    "five-shift-route-a",
                    421000,
                    EliasProcedureActionId.AmendRecord,
                    EliasShift2Branch.NormalisedAddress,
                    "elias_shift2_amend_record",
                    "elias_shift_5a_claim",
                    EliasProcedureActionId.RequestClarification,
                    EliasProcedureActionId.Unspecified,
                    clarificationAvailable: true,
                    referralAvailable: true,
                    EliasCompromisedToolState.None,
                    EliasAftermathPolicy.HouseholdDuplicateReview,
                    ClaimResolutionKind.Approve,
                    ClaimResolutionKind.Deny,
                    ClaimResolutionKind.Approve,
                    expectedComplianceStreakDelta: 1f);

            public static readonly RouteDefinition LegacyException =
                new(
                    "five-shift-route-b",
                    422000,
                    EliasProcedureActionId.RetainLegacyUnit,
                    EliasShift2Branch.LegacyException,
                    "elias_shift2_retain_legacy_unit",
                    "elias_shift_5b_claim",
                    EliasProcedureActionId.ReferForReview,
                    EliasProcedureActionId.RequestClarification,
                    clarificationAvailable: false,
                    referralAvailable: true,
                    EliasCompromisedToolState
                        .RequestClarificationLocked,
                    EliasAftermathPolicy.InternalAuditLockdown,
                    ClaimResolutionKind.Deny,
                    ClaimResolutionKind.Approve,
                    ClaimResolutionKind.Deny,
                    expectedComplianceStreakDelta: 0f);

            public static readonly RouteDefinition PhysicalVerification =
                new(
                    "five-shift-route-c",
                    423000,
                    EliasProcedureActionId.ReferForReview,
                    EliasShift2Branch.PhysicalVerification,
                    "elias_shift2_refer_for_review",
                    "elias_shift_5c_claim",
                    EliasProcedureActionId.RequestClarification,
                    EliasProcedureActionId.ReferForReview,
                    clarificationAvailable: true,
                    referralAvailable: false,
                    EliasCompromisedToolState.ReferForReviewLocked,
                    EliasAftermathPolicy.VerificationBacklog,
                    ClaimResolutionKind.Approve,
                    ClaimResolutionKind.Approve,
                    ClaimResolutionKind.Deny,
                    expectedComplianceStreakDelta: 0f);

            private RouteDefinition(
                string testId,
                int seed,
                EliasProcedureActionId shift2Action,
                EliasShift2Branch branch,
                string shift2ReceiptId,
                string shift5ClaimId,
                EliasProcedureActionId shift5Action,
                EliasProcedureActionId lockedShift5Action,
                bool clarificationAvailable,
                bool referralAvailable,
                EliasCompromisedToolState compromisedTool,
                string aftermathModifierId,
                ClaimResolutionKind shift1Disposition,
                ClaimResolutionKind shift2Disposition,
                ClaimResolutionKind shift5Disposition,
                float expectedComplianceStreakDelta)
            {
                TestId = testId;
                Seed = seed;
                Shift2Action = shift2Action;
                Branch = branch;
                Shift2ReceiptId = shift2ReceiptId;
                Shift5ClaimId = shift5ClaimId;
                Shift5Action = shift5Action;
                LockedShift5Action = lockedShift5Action;
                ClarificationAvailable = clarificationAvailable;
                ReferralAvailable = referralAvailable;
                CompromisedTool = compromisedTool;
                AftermathModifierId = aftermathModifierId;
                Shift1Disposition = shift1Disposition;
                Shift2Disposition = shift2Disposition;
                Shift5Disposition = shift5Disposition;
                ExpectedComplianceStreakDelta =
                    expectedComplianceStreakDelta;
            }

            public string TestId { get; }
            public int Seed { get; }
            public EliasProcedureActionId Shift2Action { get; }
            public EliasShift2Branch Branch { get; }
            public string Shift2ReceiptId { get; }
            public string Shift5ClaimId { get; }
            public EliasProcedureActionId Shift5Action { get; }
            public EliasProcedureActionId LockedShift5Action { get; }
            public bool ClarificationAvailable { get; }
            public bool ReferralAvailable { get; }
            public EliasCompromisedToolState CompromisedTool { get; }
            public string AftermathModifierId { get; }
            public ClaimResolutionKind Shift1Disposition { get; }
            public ClaimResolutionKind Shift2Disposition { get; }
            public ClaimResolutionKind Shift5Disposition { get; }
            public float ExpectedComplianceStreakDelta { get; }
        }
    }
}
