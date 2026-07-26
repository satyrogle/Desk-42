using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace Desk42.Tests.EditMode
{
    /// <summary>
    /// Regression guard for the duplicate-listener defect.
    ///
    /// Shift.unity had accumulated 11 identical Approve and 11 identical Deny
    /// persistent onClick listeners, so a single player click invoked
    /// EncounterManager.Approve()/Deny() eleven times. Only an in-memory bool
    /// stopped an 11x payout — and any non-persistence side effect on the
    /// resolution path ran eleven times regardless.
    ///
    /// Parsing the scene asset is deliberate: the defect lives in serialized
    /// scene data, not in code, so only reading the asset can catch a
    /// reintroduction.
    /// </summary>
    public sealed class ShiftSceneListenerTests
    {
        private const string ScenePath = "Assets/_Project/Scenes/Shift.unity";

        private static List<string> ReadScene()
        {
            Assert.IsTrue(File.Exists(ScenePath), $"Scene not found: {ScenePath}");
            return new List<string>(File.ReadAllLines(ScenePath));
        }

        private static int CountListeners(string methodName)
        {
            int count = 0;
            foreach (string line in ReadScene())
            {
                if (line.Trim() == $"m_MethodName: {methodName}")
                    count++;
            }
            return count;
        }

        [Test]
        public void ApproveButton_HasExactlyOneResolutionListener()
        {
            Assert.AreEqual(1, CountListeners("Approve"),
                "One click must produce exactly one Approve() call.");
        }

        [Test]
        public void DenyButton_HasExactlyOneResolutionListener()
        {
            Assert.AreEqual(1, CountListeners("Deny"),
                "One click must produce exactly one Deny() call.");
        }

        [Test]
        public void NoPersistentListenerIsDuplicatedAnywhereInTheScene()
        {
            var lines = ReadScene();
            var seenPerCallList = new HashSet<string>();
            var duplicates = new List<string>();

            bool inCalls = false;
            string current = null;

            for (int i = 0; i < lines.Count; i++)
            {
                string trimmed = lines[i].Trim();

                if (trimmed == "m_Calls:")
                {
                    inCalls = true;
                    seenPerCallList.Clear();
                    current = null;
                    continue;
                }

                if (!inCalls) continue;

                // A new listener entry begins at "- m_Target:".
                if (trimmed.StartsWith("- m_Target:"))
                {
                    current = trimmed;
                    continue;
                }

                if (trimmed.StartsWith("m_MethodName:") && current != null)
                {
                    string key = $"{current}|{trimmed}";
                    if (!seenPerCallList.Add(key))
                        duplicates.Add(key);
                    current = null;
                    continue;
                }

                // Any line that dedents out of the list ends it.
                if (trimmed.Length > 0
                    && !lines[i].StartsWith("      ")
                    && !lines[i].StartsWith("        "))
                {
                    inCalls = false;
                    current = null;
                }
            }

            Assert.IsEmpty(duplicates,
                "Duplicate persistent listeners found — run " +
                "tools/Dedupe-SceneButtonListeners.py to repair:\n" +
                string.Join("\n", duplicates));
        }
    }
}
