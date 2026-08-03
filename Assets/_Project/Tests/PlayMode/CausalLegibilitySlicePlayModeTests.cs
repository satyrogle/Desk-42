using System.Collections;
using System.IO;
using System.Linq;
using Desk42.Institutional.Player;
using Desk42.Product;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Desk42.Tests.PlayMode
{
    public sealed class CausalLegibilitySlicePlayModeTests
    {
        private const string SceneName = "InstitutionalProduct";

        [Test]
        public void ProductAssembly_ReferencesPlayerButNotInstitutionalDomain()
        {
            string[] references = typeof(InstitutionalProductBootstrap).Assembly
                .GetReferencedAssemblies()
                .Select(value => value.Name)
                .ToArray();
            CollectionAssert.Contains(references, "Desk42.Institutional.Player");
            CollectionAssert.DoesNotContain(references, "Desk42.Institutional.Domain");
            CollectionAssert.DoesNotContain(references, "Desk42.Institutional.Authority");
        }

        [UnityTest]
        public IEnumerator SceneBootsWithPublicCaseAndFiveNavigableSurfaces()
        {
            yield return LoadProductScene();

            InstitutionalProductBootstrap bootstrap =
                Object.FindObjectOfType<InstitutionalProductBootstrap>();
            Assert.That(bootstrap, Is.Not.Null);
            Assert.That(bootstrap.Ready, Is.True, bootstrap.LastStatus);
            Assert.That(bootstrap.CurrentView.Agents.Count, Is.EqualTo(8));
            Assert.That(bootstrap.CurrentView.Cases.Count, Is.EqualTo(1));
            Assert.That(bootstrap.CurrentView.Evidence.Count, Is.GreaterThan(0));

            bootstrap.SelectPanel(CausalLegibilityPanel.Society);
            bootstrap.SelectPanel(CausalLegibilityPanel.Docket);
            bootstrap.SelectPanel(CausalLegibilityPanel.Evidence);
            bootstrap.SelectPanel(CausalLegibilityPanel.Ruling);
            bootstrap.SelectPanel(CausalLegibilityPanel.Consequences);
            yield return null;

            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator BroadRulingSaveLoadAndReplayRemainPlayerOperable()
        {
            yield return LoadProductScene();

            InstitutionalProductBootstrap bootstrap =
                Object.FindObjectOfType<InstitutionalProductBootstrap>();
            Assert.That(bootstrap, Is.Not.Null);

            PlayerInstitutionView ruled = bootstrap.CommitSelection(
                PlayerScopeChoice.Broad,
                PlayerRulingDisposition.Recognised);
            Assert.That(ruled.Rulings.Count, Is.EqualTo(1));
            Assert.That(ruled.Cases.Count, Is.EqualTo(2));
            Assert.That(
                ruled.Timeline,
                Has.Some.Matches<PublicTimelineEntry>(entry =>
                    !string.IsNullOrWhiteSpace(entry.OriginatingRulingId) &&
                    !string.IsNullOrWhiteSpace(entry.ScopeMatchId)));

            string savePath = Path.Combine(
                Application.temporaryCachePath,
                "desk42-causal-legibility-playmode.json");
            DeleteSaveFiles(savePath);
            bootstrap.SaveTo(savePath);

            PlayerInstitutionView replayed = bootstrap.ReplayFromPreRuling();
            Assert.That(replayed.Rulings.Count, Is.Zero);
            Assert.That(replayed.Cases.Count, Is.EqualTo(1));

            PlayerInstitutionView loaded = bootstrap.LoadFrom(savePath);
            Assert.That(loaded.Rulings.Count, Is.EqualTo(1));
            Assert.That(loaded.Cases.Count, Is.EqualTo(2));
            DeleteSaveFiles(savePath);
            yield return null;

            LogAssert.NoUnexpectedReceived();
        }

        private static IEnumerator LoadProductScene()
        {
            LogAssert.Expect(
                LogType.Log,
                "[Desk42.Product] Causal Legibility Slice ready at cycle 1 " +
                "with 1 case(s).");
            AsyncOperation load = SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            while (!load.isDone) yield return null;
            yield return null;
        }

        private static void DeleteSaveFiles(string path)
        {
            string[] paths =
            {
                path,
                path + ".bak",
                path + ".tmp",
                path + ".pre-ruling",
                path + ".pre-ruling.bak",
                path + ".pre-ruling.tmp",
            };
            for (int i = 0; i < paths.Length; i++)
                if (File.Exists(paths[i])) File.Delete(paths[i]);
        }
    }
}
