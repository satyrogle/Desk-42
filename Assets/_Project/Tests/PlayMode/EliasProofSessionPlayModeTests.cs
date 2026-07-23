using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Desk42.Core;

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
            Assert.AreSame(state, proof.State);

            SceneManager.LoadScene("InternalAudit");
            yield return null;
            yield return null;

            Assert.AreSame(manager, GameManager.Instance);
            Assert.AreSame(proof, manager.EliasProof);
            Assert.AreEqual("scene-continuity",
                manager.EliasProof.State.ProofSessionId);

            manager.Run.BeginNewRun(202, "auditor", 2, fixtureMeta);
            Assert.AreSame(state, manager.EliasProof.State);
            Assert.AreEqual("scene-continuity",
                manager.EliasProof.State.ProofSessionId);

            manager.EliasProof.EndProofSession();
            Assert.IsFalse(manager.EliasProof.HasActiveSession);
        }
    }
}
