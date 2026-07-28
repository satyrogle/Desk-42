#if UNITY_EDITOR && DESK42_FMOD
using System;
using System.IO;
using FMODUnity;
using UnityEditor;
using UnityEngine;

namespace Desk42.Editor
{
    /// <summary>
    /// D1 — points the local FMOD for Unity integration at the tracked Studio
    /// project so banks resolve.
    ///
    ///   Unity.exe -batchmode -quit -nographics -projectPath &lt;repo&gt; \
    ///             -executeMethod Desk42.Editor.FmodLocalSettings.Configure
    ///
    /// WHY THIS IS A SCRIPT AND NOT A COMMITTED ASSET.
    /// FMODStudioSettings.asset is a ScriptableObject whose script reference
    /// lives in the vendor FMODUnity assembly. Desk-42 is a public repository
    /// that deliberately does not ship that assembly, so committing the asset
    /// would leave every clean clone holding a ScriptableObject with a missing
    /// MonoScript — noise at best, a broken import at worst, in exactly the
    /// State A configuration that has to stay clean. The asset is therefore
    /// LOCAL configuration, generated into the already-ignored vendor
    /// Resources folder, and reproducible from this tracked script.
    ///
    /// Guarded by DESK42_FMOD so it compiles out entirely without the SDK.
    /// </summary>
    public static class FmodLocalSettings
    {
        // Repository-relative. The Unity project root is the repository root,
        // and the Studio project sits alongside Assets/, not inside it.
        private const string StudioProjectRelativePath =
            "FMODAssets/Desk42/Desk42.fspro";

        public static void Configure()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string fsproAbsolute = Path.Combine(projectRoot, StudioProjectRelativePath);

            if (!File.Exists(fsproAbsolute))
            {
                Fail($"Studio project not found at {fsproAbsolute}. " +
                     "Create it before configuring FMOD.");
                return;
            }

            // Touching Instance creates the default asset if none exists.
            Settings settings = Settings.Instance;
            if (settings == null)
            {
                Fail("FMODUnity.Settings.Instance was null — the integration " +
                     "could not create its settings asset.");
                return;
            }

            settings.HasSourceProject = true;
            settings.HasPlatforms = true;
            settings.SourceProjectPath = StudioProjectRelativePath;

            // The Settings inspector caches this for runtime access in
            // play-in-editor; setting the project path alone leaves it empty
            // and no bank ever resolves. Mirrors SettingsEditor.GetBankDirectory,
            // whose build-folder constant is "Build".
            //
            // ABSOLUTE on purpose. EventManager.CopyToStreamingAssets resolves
            // this against the PROCESS working directory, which in batch mode is
            // not the Unity project root — a relative path silently copies zero
            // banks and leaves a working-looking but empty StreamingAssets.
            // This asset is local, gitignored configuration, so an absolute
            // machine path is safe here.
            settings.SourceBankPath = RuntimeUtils.GetCommonPlatformPath(
                Path.Combine(projectRoot,
                    Path.GetDirectoryName(StudioProjectRelativePath), "Build"));

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Pull the built banks in and stage them for the player.
            //
            // Reached by reflection on purpose: EventManager lives in the
            // editor-only FMODUnityEditor assembly, which a RUNTIME asmdef
            // (Desk42.Core) cannot legally reference. Adding an Editor asmdef
            // just for this would need an FMODUnityEditor reference that a
            // clean State A clone could not resolve. Non-fatal — FMOD's own
            // build processor also stages banks at player-build time.
            InvokeEditorStatic("FMODUnity.EventManager", "RefreshBanks", null);
            InvokeEditorStatic("FMODUnity.EventManager", "CopyToStreamingAssets",
                new object[] { BuildTarget.StandaloneWindows64 });

            StageBanksDirectly(settings);
            AssetDatabase.Refresh();

            string assetPath = AssetDatabase.GetAssetPath(settings);
            Console.WriteLine($"[FmodLocalSettings] asset      : {assetPath}");
            Console.WriteLine($"[FmodLocalSettings] sourceProj : {settings.SourceProjectPath}");
            Console.WriteLine($"[FmodLocalSettings] bankPath   : {settings.SourceBankPath}");

            string staged = Path.Combine(Application.dataPath, "StreamingAssets");
            if (Directory.Exists(staged))
            {
                foreach (string bank in Directory.GetFiles(
                             staged, "*.bank", SearchOption.AllDirectories))
                {
                    Console.WriteLine("[FmodLocalSettings] staged bank: " +
                        bank.Substring(staged.Length + 1) +
                        $" ({new FileInfo(bank).Length} bytes)");
                }
            }

            Console.WriteLine("[FmodLocalSettings] RESULT OK");
            EditorApplication.Exit(0);
        }

        /// <summary>
        /// Copies built banks into StreamingAssets, which is where
        /// RuntimeManager loads them from (Settings.TargetPath resolves to
        /// Application.streamingAssetsPath with an empty TargetBankFolder).
        ///
        /// WHY THIS EXISTS. EventManager.CopyToStreamingAssets iterates
        /// eventCache.EditorBanks, and UpdateCache refuses to build that cache
        /// while EditorUtils.StagingSystem.SourceLibsExist is true — FMOD's
        /// platform-library staging gate. In a headless batch run the gate is
        /// never cleared, so the helper NullReferences on a null cache and
        /// silently leaves StreamingAssets empty. This performs the same flat
        /// copy the helper would, so bank loading does not depend on an
        /// interactive staging step.
        ///
        /// Both source and destination are gitignored build product.
        /// </summary>
        private static void StageBanksDirectly(Settings settings)
        {
            try
            {
                string source = Path.Combine(settings.SourceBankPath, "Desktop");
                string target = Application.streamingAssetsPath;

                if (!Directory.Exists(source))
                {
                    Console.WriteLine($"[FmodLocalSettings] no built banks at {source}");
                    return;
                }

                Directory.CreateDirectory(target);

                var masterBanks = new System.Collections.Generic.List<string>();
                var banks = new System.Collections.Generic.List<string>();

                foreach (string src in Directory.GetFiles(source, "*.bank"))
                {
                    string dst = Path.Combine(target, Path.GetFileName(src));
                    File.Copy(src, dst, true);
                    new FileInfo(dst).IsReadOnly = false;

                    // RuntimeManager.BanksToLoad(BankLoadType.All) walks these
                    // two cached lists, NOT the files on disk. RefreshBanks
                    // normally fills them; blocked by the staging gate above,
                    // they stay empty and nothing loads even with every bank
                    // sitting in StreamingAssets. Master.strings is derived by
                    // RuntimeManager from the master bank name, so it is
                    // deliberately not listed.
                    string name = Path.GetFileNameWithoutExtension(src);
                    if (name.EndsWith(".strings", StringComparison.Ordinal)) continue;

                    if (name == "Master") masterBanks.Add(name);
                    else banks.Add(name);
                }

                settings.MasterBanks = masterBanks;
                settings.Banks = banks;
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();

                Console.WriteLine("[FmodLocalSettings] MasterBanks: " +
                                  string.Join(", ", masterBanks));
                Console.WriteLine("[FmodLocalSettings] Banks      : " +
                                  string.Join(", ", banks));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FmodLocalSettings] direct bank staging failed: " +
                                  $"{ex.GetType().Name}: {ex.Message}");
            }
        }

        /// <summary>
        /// Calls a static method on a type in the loaded editor assemblies.
        /// Reports rather than throws: staging banks here is a convenience,
        /// not a correctness requirement.
        /// </summary>
        private static void InvokeEditorStatic(
            string typeName, string methodName, object[] args)
        {
            try
            {
                Type type = null;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    type = asm.GetType(typeName);
                    if (type != null) break;
                }

                if (type == null)
                {
                    Console.WriteLine($"[FmodLocalSettings] {typeName} not found; skipped.");
                    return;
                }

                var method = type.GetMethod(methodName,
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.Static);

                if (method == null)
                {
                    Console.WriteLine($"[FmodLocalSettings] {typeName}.{methodName} not found; skipped.");
                    return;
                }

                method.Invoke(null, args);
                Console.WriteLine($"[FmodLocalSettings] invoked {typeName}.{methodName}");
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"[FmodLocalSettings] {typeName}.{methodName} failed: " +
                    $"{ex.GetType().Name}: {ex.Message} (non-fatal)");
            }
        }

        private static void Fail(string message)
        {
            Console.WriteLine("[FmodLocalSettings] RESULT FAILED " + message);
            EditorApplication.Exit(1);
        }
    }
}
#endif
