#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Desk42.Editor
{
    /// <summary>
    /// D1 step 9 — headless Windows x64 DEVELOPMENT player build for the
    /// FMOD cold-start proof.
    ///
    ///   Unity.exe -batchmode -quit -nographics -projectPath &lt;repo&gt; \
    ///             -executeMethod Desk42.Editor.ProofStandaloneBuild.BuildWindows64
    ///
    /// Development (not release) on purpose: the dev player links FMOD's
    /// LOGGING native library, so a bank or event failure is reported instead
    /// of silently swallowed. That is the whole point of the cold-start pass.
    ///
    /// Output is build product and stays out of version control.
    /// </summary>
    public static class ProofStandaloneBuild
    {
        private const string OutputDir  = "Build/ProofStandalone";
        private const string OutputName = "Desk42-ProofStandalone.exe";

        public static void BuildWindows64()
        {
            string[] scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                Fail("No enabled scenes in EditorBuildSettings — nothing to build.");
                return;
            }

            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string outDir      = Path.Combine(projectRoot, OutputDir);
            Directory.CreateDirectory(outDir);

            var options = new BuildPlayerOptions
            {
                scenes           = scenes,
                locationPathName = Path.Combine(outDir, OutputName),
                target           = BuildTarget.StandaloneWindows64,
                targetGroup      = BuildTargetGroup.Standalone,
                // Development + script debugging so FMOD's logging library is
                // used and any native fault is visible in the player log.
                options          = BuildOptions.Development,
            };

            Console.WriteLine($"[ProofStandaloneBuild] scenes: {string.Join(", ", scenes)}");
            Console.WriteLine($"[ProofStandaloneBuild] output: {options.locationPathName}");

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            Console.WriteLine(
                $"[ProofStandaloneBuild] result={summary.result} " +
                $"errors={summary.totalErrors} warnings={summary.totalWarnings} " +
                $"size={summary.totalSize} bytes");

            if (summary.result != BuildResult.Succeeded)
            {
                foreach (var step in report.steps)
                {
                    foreach (var msg in step.messages)
                    {
                        if (msg.type == LogType.Error || msg.type == LogType.Exception)
                            Console.WriteLine($"[ProofStandaloneBuild] {msg.type}: {msg.content}");
                    }
                }
                Fail($"Build did not succeed: {summary.result}");
                return;
            }

            Console.WriteLine("[ProofStandaloneBuild] RESULT OK " + options.locationPathName);
            EditorApplication.Exit(0);
        }

        private static void Fail(string message)
        {
            Console.WriteLine("[ProofStandaloneBuild] RESULT FAILED " + message);
            EditorApplication.Exit(1);
        }
    }
}
#endif
