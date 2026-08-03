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
            yield return null;

            Assert.That(bootstrap.AuxVerifierInstalled, Is.True);
            Assert.That(bootstrap.CurrentPolicyNumber, Is.EqualTo(1));
            LogAssert.NoUnexpectedReceived();
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
