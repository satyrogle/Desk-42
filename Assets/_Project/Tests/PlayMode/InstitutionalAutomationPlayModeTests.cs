using System.Collections;
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

            Assert.That(bootstrap.BindProcedure(5), Is.True);
            Assert.That(bootstrap.ProceduresBound, Is.EqualTo(1));
            Assert.That(bootstrap.AppealHandling, Is.EqualTo("FAST TRACK"));
            Assert.That(bootstrap.UpgradeCredits, Is.EqualTo(creditsBefore - 4));

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
                Time.timeScale = 12f;

                float timeout = Time.realtimeSinceStartup + 12f;
                while (bootstrap.AppealsResolved < 1 &&
                       Time.realtimeSinceStartup < timeout)
                    yield return null;

                Assert.That(bootstrap.ClaimsCompleted, Is.GreaterThanOrEqualTo(1));
                Assert.That(bootstrap.AppealsReturned, Is.GreaterThanOrEqualTo(1));
                Assert.That(bootstrap.AppealsResolved, Is.GreaterThanOrEqualTo(1));
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
    }
}
