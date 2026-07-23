using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Desk42.Core;
using Desk42.BSM;
using Desk42.UI;

namespace Desk42.Tests.PlayMode
{
    public sealed class EliasProofSessionPlayModeTests
    {
        private string _testSaveDirectory;

        [SetUp]
        public void SetUp()
        {
            if (GameManager.Instance != null)
                UnityEngine.Object.DestroyImmediate(
                    GameManager.Instance.gameObject);

            _testSaveDirectory = Path.Combine(
                Path.GetTempPath(), $"Desk42_EliasProof_{Guid.NewGuid():N}");
            SaveSystem.SetSaveDirectoryOverrideForTests(_testSaveDirectory);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (GameManager.Instance != null)
                UnityEngine.Object.DestroyImmediate(
                    GameManager.Instance.gameObject);

            SaveSystem.WipeAllSaveData();
            SaveSystem.ClearSaveDirectoryOverrideForTests();
            if (Directory.Exists(_testSaveDirectory))
                Directory.Delete(_testSaveDirectory, recursive: true);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ProofState_SurvivesFreshRunsAndSceneReconstruction()
        {
            SceneManager.LoadScene("Boot");
            float deadline = Time.realtimeSinceStartup + 20f;
            while ((GameManager.Instance == null
                    || SceneManager.GetActiveScene().name != "MainMenu")
                && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            GameManager manager = GameManager.Instance;
            Assert.IsNotNull(manager);
            Assert.IsNotNull(manager.EliasProof);

            var fixtureMeta = new MetaProgressData
            {
                TutorialCompleted = true,
                HighestPhaseReached = 4,
            };
            manager.SetMetaForTesting(fixtureMeta);
            EliasProofSessionController proof = manager.EliasProof;
            EliasProofSessionState state =
                proof.BeginProofSession("scene-continuity");

            manager.Run.BeginNewRun(101, "auditor", 1, fixtureMeta);
            EliasVisitTransaction shift1 = proof.RecordAppearance(
                EliasProofContent.CanonicalClaimantId,
                EliasProofContent.Shift1AppearanceKey);
            AssertBsmConsumesPriorVisits(shift1, expectedPriorVisits: 0);
            Assert.AreSame(state, proof.State);

            SceneManager.LoadScene("InternalAudit");
            yield return null;
            yield return null;

            Assert.AreSame(manager, GameManager.Instance);
            Assert.AreSame(proof, manager.EliasProof);
            Assert.AreEqual("scene-continuity",
                manager.EliasProof.State.ProofSessionId);

            manager.Run.BeginNewRun(202, "auditor", 2, fixtureMeta);
            EliasVisitTransaction shift2 = proof.RecordAppearance(
                EliasProofContent.CanonicalClaimantId,
                EliasProofContent.Shift2AppearanceKey);
            AssertBsmConsumesPriorVisits(shift2, expectedPriorVisits: 1);
            Assert.AreSame(state, manager.EliasProof.State);
            Assert.AreEqual("scene-continuity",
                manager.EliasProof.State.ProofSessionId);

            manager.EliasProof.EndProofSession();
            Assert.IsFalse(manager.EliasProof.HasActiveSession);
        }

        [UnityTest]
        public IEnumerator Shift1Scene_WiresContentAndSchedulesOneEliasAtClaimTwo()
        {
            SceneManager.LoadScene("Boot");
            float deadline = Time.realtimeSinceStartup + 20f;
            while ((GameManager.Instance == null
                    || SceneManager.GetActiveScene().name != "MainMenu")
                && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            GameManager manager = GameManager.Instance;
            Assert.IsNotNull(manager);
            Assert.IsNotNull(manager.EliasContent,
                "Boot must wire the authored Elias content asset.");

            var fixtureMeta = new MetaProgressData
            {
                TutorialCompleted = true,
                HighestPhaseReached = 4,
            };
            manager.SetMetaForTesting(fixtureMeta);
            manager.Run.BeginNewRun(421001, "auditor", 1, fixtureMeta);

            SceneManager.LoadScene("Shift");
            deadline = Time.realtimeSinceStartup + 20f;
            while ((SceneManager.GetActiveScene().name != "Shift"
                    || manager.Run.RawData.ActiveClaim == null)
                && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.AreEqual("Shift",
                SceneManager.GetActiveScene().name);
            Assert.IsNotNull(manager.Run.RawData.ActiveClaim);
            Assert.IsTrue(manager.EliasProof.HasActiveSession);

            ActiveClaimData[] orderedClaims =
                new[] { manager.Run.RawData.ActiveClaim }
                    .Concat(manager.Run.RawData.PendingClaims)
                    .ToArray();
            Assert.GreaterOrEqual(orderedClaims.Length, 2);
            Assert.AreEqual(
                EliasProofContent.CanonicalClaimantId,
                orderedClaims[1].ClientVariantId);
            Assert.AreEqual(
                EliasProofContent.Shift1AppearanceKey,
                orderedClaims[1].AuthoredAppearanceKey);
            Assert.AreEqual(1, orderedClaims.Count(claim =>
                claim.ClientVariantId
                    == EliasProofContent.CanonicalClaimantId));

            EliasProcedurePanel panel =
                Resources.FindObjectsOfTypeAll<EliasProcedurePanel>()
                    .FirstOrDefault(candidate =>
                        candidate.gameObject.scene
                            == SceneManager.GetActiveScene());
            Assert.IsNotNull(panel,
                "Shift UI must construct the authored procedure panel.");
            Assert.IsFalse(panel.gameObject.activeSelf,
                "Shift 1 has no procedure stage, so the panel stays hidden.");
        }

        [UnityTest]
        public IEnumerator ProcedureReceipt_PresentsAnchorBeforeReward_AndProtectsInput()
        {
            var host = new GameObject("EliasProcedureReceiptTest");
            var presenter =
                host.AddComponent<EliasProcedureReceiptPresenter>();
            SetPrivateFloat(presenter, "_standardBeatSeconds", 0.01f);
            SetPrivateFloat(presenter, "_memoryAnchorSeconds", 0.01f);
            SetPrivateFloat(presenter, "_rewardBeatSeconds", 0.01f);
            SetPrivateFloat(presenter, "_finalHoldSeconds", 0f);

            var presented = new List<EliasProcedureReceiptBeatKind>();
            presenter.BeatPresented += (_, beat) =>
                presented.Add(beat.Kind);
            presenter.Present(new AppliedEliasProcedure(
                "receipt-playmode",
                EliasProofContent.Shift2AppearanceKey,
                EliasProofContent.CanonicalClaimantId,
                EliasProcedureActionId.AmendRecord,
                EliasShift2Branch.NormalisedAddress,
                1,
                2,
                0,
                0f,
                0f,
                1f,
                EliasProcedurePolicy.OriginalAddress,
                EliasProcedurePolicy.AmendedAddress,
                EliasProcedurePolicy.MiriamRegisteredAt18A,
                "elias_shift2_amend_record"));

            Assert.IsTrue(presenter.IsPresenting);
            CanvasGroup inputShield =
                host.GetComponentInChildren<CanvasGroup>();
            if (inputShield == null)
            {
                inputShield = Resources
                    .FindObjectsOfTypeAll<CanvasGroup>()
                    .FirstOrDefault(candidate =>
                        candidate.gameObject.name
                            == "EliasProcedureReceiptCanvas");
            }
            Assert.IsNotNull(inputShield);
            Assert.IsNull(
                inputShield.transform.parent,
                "Protected receipt must be a root screen-space canvas.");
            Assert.IsTrue(inputShield.blocksRaycasts);

            float deadline = Time.realtimeSinceStartup + 2f;
            while (presenter.IsPresenting
                && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.IsFalse(presenter.IsPresenting);
            Assert.IsFalse(inputShield.blocksRaycasts);
            CollectionAssert.AreEqual(
                new[]
                {
                    EliasProcedureReceiptBeatKind.Action,
                    EliasProcedureReceiptBeatKind.RecordChange,
                    EliasProcedureReceiptBeatKind.MemoryAnchor,
                    EliasProcedureReceiptBeatKind.Processing,
                    EliasProcedureReceiptBeatKind.AppliedDelta,
                },
                presented);
            UnityEngine.Object.Destroy(host);
        }

        [UnityTest]
        public IEnumerator AuthoredAftermath_ConsumesOnEncounter_AndRemainsVisible()
        {
            SceneManager.LoadScene("Boot");
            float deadline = Time.realtimeSinceStartup + 20f;
            while ((GameManager.Instance == null
                    || SceneManager.GetActiveScene().name != "MainMenu")
                && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            GameManager manager = GameManager.Instance;
            var fixtureMeta = new MetaProgressData
            {
                TutorialCompleted = true,
                HighestPhaseReached = 4,
            };
            manager.SetMetaForTesting(fixtureMeta);
            manager.Run.BeginNewRun(
                421005, "auditor", 5, fixtureMeta);
            AdvanceProofToShift5(manager);
            EliasAftermathModifierState modifier =
                manager.EliasProof.ActivateShift5Aftermath(
                    manager.EliasContent);
            string claimId =
                modifier.PendingClaimIds.OrderBy(id => id).First();

            var overlayHost = new GameObject("AftermathOverlay");
            var overlay =
                overlayHost.AddComponent<ShiftFeedbackOverlay>();
            var encounterHost = new GameObject("AftermathEncounter");
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
            var encounter =
                encounterHost.AddComponent<Desk42.Encounter.EncounterManager>();
            var claim = new ActiveClaimData
            {
                ClaimId = claimId,
                ClientVariantId = "authored_aftermath_test",
                ClientSpeciesId = "moth_accountant",
                ClaimantName = "Aftermath Test",
                IncidentText = "Authored aftermath integration.",
            };

            RumorMill.PublishDeferred(
                new ClaimQueuedEvent(claim, remaining: 0));
            deadline = Time.realtimeSinceStartup + 3f;
            while ((!modifier.AppliedClaimIds.Contains(claimId)
                    || !overlay.RenderedClientModifiers.Contains(
                        "HOUSEHOLD DUPLICATE REVIEW"))
                && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.AreSame(claim, encounter.ActiveClaim);
            Assert.IsTrue(modifier.AppliedClaimIds.Contains(claimId));
            StringAssert.Contains(
                "HOUSEHOLD DUPLICATE REVIEW",
                overlay.RenderedClientModifiers);

            UnityEngine.Object.Destroy(encounterHost);
            UnityEngine.Object.Destroy(overlayHost);
        }

        private static void AssertBsmConsumesPriorVisits(
            EliasVisitTransaction transaction, int expectedPriorVisits)
        {
            var clientObject = new GameObject("EliasVisitBSM");
            try
            {
                var stateMachine =
                    clientObject.AddComponent<ClientStateMachine>();
                stateMachine.Initialize(
                    transaction.StableClaimantId,
                    "moth_accountant",
                    transaction.PriorVisits,
                    counterTraits: null);
                Assert.AreEqual(expectedPriorVisits,
                    stateMachine.VisitCount);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(clientObject);
            }
        }

        private static void SetPrivateFloat(
            object target, string fieldName, float value)
            => target.GetType()
                .GetField(fieldName,
                    BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(target, value);

        private static void AdvanceProofToShift5(
            GameManager manager)
        {
            EliasProofSessionController proof =
                manager.EliasProof;
            proof.BeginProofSession("aftermath-playmode");
            proof.RecordAppearance(
                EliasProofContent.CanonicalClaimantId,
                EliasProofContent.Shift1AppearanceKey);
            proof.RecordDisposition(
                EliasProofContent.Shift1AppearanceKey,
                BuildDisposition(ClaimResolutionKind.Approve));
            proof.RecordAppearance(
                EliasProofContent.CanonicalClaimantId,
                EliasProofContent.Shift2AppearanceKey);
            Assert.IsTrue(proof.TryApplyProcedure(
                manager.Run,
                EliasProofContent.CanonicalClaimantId,
                EliasProofContent.Shift2AppearanceKey,
                EliasProcedureActionId.AmendRecord,
                out _,
                out _));
            proof.RecordDisposition(
                EliasProofContent.Shift2AppearanceKey,
                BuildDisposition(ClaimResolutionKind.Approve));
            proof.RecordAppearance(
                EliasProofContent.CanonicalClaimantId,
                EliasProofContent.Shift5AppearanceKey);
            Assert.IsTrue(proof.TryApplyProcedure(
                manager.Run,
                EliasProofContent.CanonicalClaimantId,
                EliasProofContent.Shift5AppearanceKey,
                EliasProcedureActionId.RequestClarification,
                out _,
                out _));
            proof.RecordDisposition(
                EliasProofContent.Shift5AppearanceKey,
                BuildDisposition(ClaimResolutionKind.Deny));
        }

        private static AppliedClaimResolution BuildDisposition(
            ClaimResolutionKind kind)
            => new(
                "elias-playmode-claim",
                EliasProofContent.CanonicalClaimantId,
                "moth_accountant",
                kind,
                0,
                0f,
                0f,
                0,
                0,
                1,
                3,
                1f,
                1f);
    }
}
