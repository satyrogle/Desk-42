using System.Collections;
using System.Reflection;
using Desk42.Product.OfficeSlice;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Desk42.Tests.PlayMode.OfficeSlice
{
    public sealed class OfficeM4VisualPlayModeTests
    {
        [UnityTest]
        public IEnumerator RestartBuildsExactlyOneActiveVisualRoot()
        {
            SceneManager.LoadScene("OfficeSlice");
            yield return null;
            yield return null;
            OfficeSliceBootstrap bootstrap = Object.FindObjectOfType<OfficeSliceBootstrap>();
            Assert.That(bootstrap, Is.Not.Null);
            Assert.That(bootstrap.VisualDirector, Is.Not.Null);
            Assert.That(OfficeVisualDirector.ActiveRootCount(), Is.EqualTo(1));

            MethodInfo rebuild = typeof(OfficeSliceBootstrap).GetMethod(
                "RebuildRuntimePresentation", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(rebuild, Is.Not.Null);
            rebuild.Invoke(bootstrap, null);
            rebuild.Invoke(bootstrap, null);
            yield return null;

            Assert.That(OfficeVisualDirector.ActiveRootCount(), Is.EqualTo(1));
            Assert.That(bootstrap.VisualDirector.UsedFallback, Is.False);
        }
    }
}
