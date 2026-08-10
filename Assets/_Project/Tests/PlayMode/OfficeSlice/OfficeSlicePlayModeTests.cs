using System.Collections;
using Desk42.Product.OfficeSlice;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Desk42.Tests.PlayMode
{
    public sealed class OfficeSlicePlayModeTests
    {
        [UnityTest]
        public IEnumerator OfficeSliceSceneBootsAsOneRootWithSixCases()
        {
            yield return SceneManager.LoadSceneAsync("OfficeSlice");
            yield return null;

            Scene scene = SceneManager.GetActiveScene();
            Assert.That(scene.name, Is.EqualTo("OfficeSlice"));
            GameObject[] roots = scene.GetRootGameObjects();
            Assert.That(roots, Has.Length.EqualTo(1));
            Assert.That(roots[0].name, Is.EqualTo("Office Slice Bootstrap"));

            OfficeSliceBootstrap bootstrap = roots[0].GetComponent<OfficeSliceBootstrap>();
            Assert.That(bootstrap, Is.Not.Null);
            Assert.That(bootstrap.Ready, Is.True);
            Assert.That(bootstrap.CaseRepository.Cases, Has.Count.EqualTo(6));
            Assert.That(bootstrap.VisibleFolderCount, Is.EqualTo(6));
            Assert.That(bootstrap.CriticalRoutesValid, Is.True);
        }

        [UnityTest]
        public IEnumerator OfficeSliceRoutesFoldersWithoutDuplicateOwnership()
        {
            yield return SceneManager.LoadSceneAsync("OfficeSlice");
            yield return null;
            OfficeSliceBootstrap bootstrap =
                Object.FindObjectOfType<OfficeSliceBootstrap>();

            bootstrap.ForceAllFoldersThroughM1Route();

            Assert.That(bootstrap.SimulationState.Queues.AllFoldersAtFrontDesk(), Is.True);
            Assert.That(bootstrap.SimulationState.Queues.HasSingleLogicalOwnerForEveryFolder(),
                Is.True);
            Assert.That(bootstrap.QueueSummary(), Does.Contain("FrontDesk:"));
        }
    }
}
