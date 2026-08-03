using System.Collections;
using System.IO;
using Desk42.Product.Automation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Desk42.Tests.PlayMode
{
    public sealed class InstitutionalAutomationPlayModeTests
    {
        private const string SceneName = "InstitutionalAutomation";

        [UnityTest]
        public IEnumerator AutomationSceneBootsAndAcceptsPlayerBuildChanges()
        {
            yield return LoadAutomationScene();
            AutomationBootstrap bootstrap =
                Object.FindObjectOfType<AutomationBootstrap>();

            Assert.That(bootstrap, Is.Not.Null);
            Assert.That(bootstrap.Ready, Is.True);
            bootstrap.InstallAuxVerifier();
            bootstrap.SelectPolicy(1);
            int creditsBeforeUpgrade = bootstrap.UpgradeCredits;
            bootstrap.CyclePriority();
            bootstrap.CycleAppealHandling();
            bool upgraded = bootstrap.UpgradeSelectedThroughput();
            yield return null;

            Assert.That(bootstrap.AuxVerifierInstalled, Is.True);
            Assert.That(bootstrap.CurrentPolicyNumber, Is.EqualTo(1));
            Assert.That(bootstrap.RoutePriority, Is.EqualTo("URGENT FIRST"));
            Assert.That(bootstrap.AppealHandling, Is.EqualTo("FAST TRACK"));
            Assert.That(upgraded, Is.True);
            Assert.That(bootstrap.UpgradeCredits, Is.LessThan(creditsBeforeUpgrade));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator SustainedUnmanagedPressureCreatesVisibleOperationalFailures()
        {
            float originalTimeScale = Time.timeScale;
            try
            {
                yield return LoadAutomationScene();
                AutomationBootstrap bootstrap =
                    Object.FindObjectOfType<AutomationBootstrap>();
                Assert.That(bootstrap, Is.Not.Null);
                bootstrap.SelectPolicy(2);
                Time.timeScale = 18f;

                float timeout = Time.realtimeSinceStartup + 8f;
                while ((bootstrap.MachineJams < 1 || bootstrap.OverdueClaims < 1) &&
                       Time.realtimeSinceStartup < timeout)
                    yield return null;

                Assert.That(bootstrap.ClaimsInFlight, Is.GreaterThan(0));
                Assert.That(bootstrap.MachineJams, Is.GreaterThanOrEqualTo(1));
                Assert.That(bootstrap.OverdueClaims, Is.GreaterThanOrEqualTo(1));
                Assert.That(bootstrap.SelectFirstJammedStation(), Is.True);
                Assert.That(bootstrap.RepairSelectedStation(), Is.True,
                    "A jammed station should be repairable after overload.");
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                Time.timeScale = originalTimeScale;
            }
        }

        [UnityTest]
        public IEnumerator ProcedureLoadoutBindsToThePhysicalAppealRoute()
        {
            yield return LoadAutomationScene();
            AutomationBootstrap bootstrap =
                Object.FindObjectOfType<AutomationBootstrap>();
            Assert.That(bootstrap, Is.Not.Null);
            int creditsBefore = bootstrap.UpgradeCredits;
            bootstrap.SelectPolicy(2);

            Assert.That(bootstrap.BindProcedure(5), Is.True);
            Assert.That(bootstrap.ProceduresBound, Is.EqualTo(1));
            Assert.That(bootstrap.AppealHandling, Is.EqualTo("FAST TRACK"));
            Assert.That(bootstrap.UpgradeCredits, Is.EqualTo(creditsBefore),
                "Institutional procedures must not spend machine-upgrade credits.");

            bootstrap.CycleAppealHandling();
            Assert.That(bootstrap.AppealHandling, Is.EqualTo("FAST TRACK"),
                "A binding fast-track procedure should override manual appeal routing.");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator MandatorySecondaryVerificationAddsASecondPhysicalPass()
        {
            float originalTimeScale = Time.timeScale;
            try
            {
                yield return LoadAutomationScene();
                AutomationBootstrap bootstrap =
                    Object.FindObjectOfType<AutomationBootstrap>();
                Assert.That(bootstrap, Is.Not.Null);
                bootstrap.InstallAuxVerifier();
                bootstrap.SelectPolicy(2);
                Assert.That(bootstrap.BindProcedure(1), Is.True);
                Time.timeScale = 12f;

                float timeout = Time.realtimeSinceStartup + 8f;
                while (bootstrap.SecondaryVerificationChecks < 1 &&
                       Time.realtimeSinceStartup < timeout)
                    yield return null;

                Assert.That(bootstrap.SecondaryVerificationChecks,
                    Is.GreaterThanOrEqualTo(1));
                Assert.That(bootstrap.ClaimsInFlight, Is.GreaterThan(0));
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                Time.timeScale = originalTimeScale;
            }
        }

        [UnityTest]
        public IEnumerator DoctrineSelectionLocksInstitutionalIdentityForRun()
        {
            yield return LoadAutomationScene();
            AutomationBootstrap bootstrap =
                Object.FindObjectOfType<AutomationBootstrap>();
            Assert.That(bootstrap, Is.Not.Null);
            Assert.That(bootstrap.RunPhase, Is.EqualTo("DoctrineSelection"));

            bootstrap.SelectPolicy(1);
            bootstrap.SelectPolicy(3);

            Assert.That(bootstrap.DoctrineLocked, Is.True);
            Assert.That(bootstrap.CurrentPolicyNumber, Is.EqualTo(1));
            Assert.That(bootstrap.RunPhase, Is.EqualTo("ActiveProcessing"));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator ShiftCloseDraftCreatesCompoundingProcedureBuild()
        {
            float originalTimeScale = Time.timeScale;
            try
            {
                yield return LoadAutomationScene();
                AutomationBootstrap bootstrap =
                    Object.FindObjectOfType<AutomationBootstrap>();
                bootstrap.SelectPolicy(1);
                bootstrap.InstallAuxVerifier();
                Time.timeScale = 24f;

                float timeout = Time.realtimeSinceStartup + 20f;
                while (bootstrap.RunPhase != "ShiftClose" &&
                       Time.realtimeSinceStartup < timeout)
                {
                    if (bootstrap.SelectFirstJammedStation())
                        bootstrap.RepairSelectedStation();
                    yield return null;
                }

                Assert.That(bootstrap.RunPhase, Is.EqualTo("ShiftClose"));
                Assert.That(bootstrap.DraftChoiceCount,
                    Is.GreaterThanOrEqualTo(1));
                Assert.That(bootstrap.ChooseDraft(0), Is.True);
                Assert.That(bootstrap.ProceduresBound, Is.EqualTo(1));
                Assert.That(bootstrap.CurrentShift, Is.EqualTo(2));
                Assert.That(bootstrap.RunPhase, Is.EqualTo("ActiveProcessing"));
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                Time.timeScale = originalTimeScale;
            }
        }

        [UnityTest]
        public IEnumerator CollectiveGrievanceTravelsThroughSharedFactoryFloor()
        {
            float originalTimeScale = Time.timeScale;
            try
            {
                yield return LoadAutomationScene();
                AutomationBootstrap bootstrap =
                    Object.FindObjectOfType<AutomationBootstrap>();
                bootstrap.SelectPolicy(2);
                bootstrap.InstallAuxVerifier();
                Time.timeScale = 18f;

                float timeout = Time.realtimeSinceStartup + 14f;
                while (bootstrap.CollectiveGrievancesProcessed < 1 &&
                       Time.realtimeSinceStartup < timeout)
                {
                    if (bootstrap.SelectFirstJammedStation())
                        bootstrap.RepairSelectedStation();
                    yield return null;
                }

                Assert.That(bootstrap.CollectiveGrievancesProcessed,
                    Is.GreaterThanOrEqualTo(1));
                Assert.That(bootstrap.InstitutionalRulings,
                    Is.GreaterThanOrEqualTo(1));
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                Time.timeScale = originalTimeScale;
            }
        }

        [UnityTest]
        public IEnumerator ActiveRunSaveRestoresFactorySocietyAppealAndHolding()
        {
            float originalTimeScale = Time.timeScale;
            string path = Path.Combine(
                Application.temporaryCachePath,
                "desk42-v0.4-playmode-save.json");
            DeleteSave(path);
            try
            {
                yield return LoadAutomationScene();
                AutomationBootstrap bootstrap =
                    Object.FindObjectOfType<AutomationBootstrap>();
                bootstrap.SelectPolicy(3);
                bootstrap.InstallAuxVerifier();
                Assert.That(bootstrap.BindProcedure(2), Is.True);
                Assert.That(bootstrap.BindProcedure(6), Is.True);
                Time.timeScale = 18f;

                float timeout = Time.realtimeSinceStartup + 24f;
                while ((bootstrap.PrecedentsInstalled < 1 ||
                        bootstrap.PendingAppeals < 1 ||
                        bootstrap.ClaimsInFlight < 1) &&
                       Time.realtimeSinceStartup < timeout)
                {
                    if ((bootstrap.PrecedentsInstalled < 1 ||
                         bootstrap.PendingAppeals < 1) &&
                        bootstrap.SelectFirstJammedStation())
                        bootstrap.RepairSelectedStation();
                    yield return null;
                }

                Assert.That(bootstrap.PrecedentsInstalled, Is.GreaterThanOrEqualTo(1));
                Assert.That(bootstrap.PendingAppeals, Is.GreaterThanOrEqualTo(1));
                if (bootstrap.ActiveMachineJams == 0)
                    Assert.That(
                        bootstrap.CreateValidationJamOnSelectedStation(),
                        Is.True);
                Assert.That(bootstrap.ActiveMachineJams, Is.GreaterThanOrEqualTo(1));
                Assert.That(bootstrap.ProceduresBound, Is.EqualTo(2));
                int inFlight = bootstrap.ClaimsInFlight;
                int rulings = bootstrap.InstitutionalRulings;
                long societyTick = bootstrap.SocietyTick;
                string mode = bootstrap.FirstPrecedentMode;
                Time.timeScale = 0f;
                bootstrap.SaveRun(path);
                Assert.That(File.Exists(path), Is.True);

                bootstrap.CyclePriority();
                bootstrap.CycleFirstPrecedentMode();
                bootstrap.LoadRun(path);
                yield return null;

                Assert.That(bootstrap.ClaimsInFlight, Is.EqualTo(inFlight));
                Assert.That(bootstrap.InstitutionalRulings, Is.EqualTo(rulings));
                Assert.That(bootstrap.SocietyTick, Is.EqualTo(societyTick));
                Assert.That(bootstrap.FirstPrecedentMode, Is.EqualTo(mode));
                Assert.That(bootstrap.ProceduresBound, Is.EqualTo(2));
                Assert.That(bootstrap.PrecedentsInstalled, Is.GreaterThanOrEqualTo(1));
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                Time.timeScale = originalTimeScale;
                DeleteSave(path);
            }
        }

        [UnityTest]
        [Category("LongRunningProduct")]
        public IEnumerator EightShiftRunEndsInDerivedBranchReview()
        {
            float originalTimeScale = Time.timeScale;
            float originalMaximumDeltaTime = Time.maximumDeltaTime;
            float originalCaptureDeltaTime = Time.captureDeltaTime;
            try
            {
                yield return LoadAutomationScene();
                AutomationBootstrap bootstrap =
                    Object.FindObjectOfType<AutomationBootstrap>();
                Assert.That(bootstrap, Is.Not.Null);
                bootstrap.SelectPolicy(1);
                bootstrap.InstallAuxVerifier();
                Time.maximumDeltaTime = 2f;
                Time.timeScale = 1f;
                Time.captureDeltaTime = 1f;

                float timeout = Time.realtimeSinceStartup + 90f;
                while (bootstrap.RunPhase != "BranchReview" &&
                       Time.realtimeSinceStartup < timeout)
                {
                    if (bootstrap.SelectFirstJammedStation())
                        bootstrap.RepairSelectedStation();
                    if (bootstrap.RunPhase == "ShiftClose")
                    {
                        if (bootstrap.DraftChoiceCount > 0)
                            Assert.That(bootstrap.ChooseDraft(0), Is.True);
                        else
                            Assert.That(bootstrap.ContinueAfterShift(), Is.True);
                    }
                    yield return null;
                }

                Assert.That(bootstrap.RunPhase, Is.EqualTo("BranchReview"),
                    "Timed out at shift " + bootstrap.CurrentShift +
                    " with " + bootstrap.ClaimsCompleted + " completed, " +
                    bootstrap.ClaimsInFlight + " in flight, " +
                    bootstrap.ActiveMachineJams + " active jams and " +
                    bootstrap.InstitutionalRulings + " rulings.");
                Assert.That(bootstrap.CurrentShift, Is.EqualTo(8));
                Assert.That(bootstrap.ClaimsCompleted, Is.GreaterThanOrEqualTo(96));
                Assert.That(bootstrap.InstitutionalRulings,
                    Is.GreaterThanOrEqualTo(96));
                Assert.That(bootstrap.BranchOutcome, Is.Not.Empty);
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                Time.timeScale = originalTimeScale;
                Time.maximumDeltaTime = originalMaximumDeltaTime;
                Time.captureDeltaTime = originalCaptureDeltaTime;
            }
        }

        [UnityTest]
        public IEnumerator AppealRefineryProducesAndResolvesPhysicalReturnWork()
        {
            float originalTimeScale = Time.timeScale;
            try
            {
                yield return LoadAutomationScene();
                AutomationBootstrap bootstrap =
                    Object.FindObjectOfType<AutomationBootstrap>();
                Assert.That(bootstrap, Is.Not.Null);
                bootstrap.InstallAuxVerifier();
                bootstrap.SelectPolicy(3);
                bootstrap.CycleAppealHandling();
                Time.timeScale = 12f;

                float timeout = Time.realtimeSinceStartup + 12f;
                while (bootstrap.AppealsResolved < 1 &&
                       Time.realtimeSinceStartup < timeout)
                    yield return null;

                Assert.That(bootstrap.ClaimsCompleted, Is.GreaterThanOrEqualTo(1));
                Assert.That(bootstrap.AppealsReturned, Is.GreaterThanOrEqualTo(1));
                Assert.That(bootstrap.AppealsResolved, Is.GreaterThanOrEqualTo(1));
                Assert.That(bootstrap.PrecedentsInstalled, Is.GreaterThanOrEqualTo(1),
                    "The physical Legal route should install a backend holding.");
                Assert.That(bootstrap.InstitutionalRulings, Is.GreaterThanOrEqualTo(2));
                Assert.That(bootstrap.SocietyTick, Is.GreaterThan(1));
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                Time.timeScale = originalTimeScale;
            }
        }

        private static IEnumerator LoadAutomationScene()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync(
                SceneName, LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            while (!load.isDone) yield return null;
            yield return null;
        }

        private static void DeleteSave(string path)
        {
            if (File.Exists(path)) File.Delete(path);
            if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            if (File.Exists(path + ".tmp")) File.Delete(path + ".tmp");
        }
    }
}
