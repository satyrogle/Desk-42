using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Desk42.Institutional.Player;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Desk42.Product.OfficeSlice
{
    [DisallowMultipleComponent]
    public sealed class OfficeSliceBootstrap : MonoBehaviour
    {
        private const string OfficeSliceArgument = "--desk42-office-slice";
        private const string CaptureArgument = "--desk42-office-slice-capture";
        private const string CaptureShiftArgument =
            "--desk42-office-slice-capture-shift";
        private const string CaptureStateArgument =
            "--desk42-office-slice-capture-state";
        private const string PerformanceArgument =
            "--desk42-office-slice-performance-smoke";
        private const string CaptureDistributionArgument =
            "--desk42-office-slice-capture-distribution";
        private const string ReducedFlashArgument =
            "--desk42-office-slice-reduced-flash";
        private const string TutorialCompleteKey =
            "desk42.office-slice.m6.tutorial-complete";

        private readonly Dictionary<string, Transform> _folderViews =
            new(StringComparer.Ordinal);
        private readonly Dictionary<string, TextMesh> _folderLabels =
            new(StringComparer.Ordinal);
        private readonly Dictionary<string, Renderer> _folderRenderers =
            new(StringComparer.Ordinal);
        private readonly Dictionary<string, Transform> _customerViews =
            new(StringComparer.Ordinal);
        private readonly Dictionary<string, Transform> _staffViews =
            new(StringComparer.Ordinal);
        private readonly List<Material> _runtimeMaterials = new();

        private OfficeCaseRepository _caseRepository;
        private OfficeCampaignState _campaignState;
        private OfficeSimulationState _simulationState;
        private OfficeTickDriver _tickDriver;
        private Transform _runtimeRoot;
        private Transform _wardenView;
        private Transform _supervisorStampView;
        private Camera _camera;
        private OfficeSpriteCatalog _m4Catalog;
        private OfficeVisualDirector _m4Director;
        private OfficeVisualStateProjector _m4Projector;
        private OfficeVisualSnapshot _m4Snapshot;
        private OfficeAudioSettings _m5AudioSettings;
        private OfficeAudioDirector _m5AudioDirector;
        private OfficeFeedbackDirector _m5FeedbackDirector;
        private readonly OfficeM4HudPresenter _m4HudPresenter = new();
        private readonly OfficeM6HudPresenter _m6HudPresenter = new();
        private OfficeM6HudModel _m6HudModel;
        private OfficeM6Onboarding _m6Onboarding;
        private OfficeM6ControlScheme _m6ControlScheme =
            OfficeM6ControlScheme.Keyboard;
        private bool _m6TutorialCompletionSaved;
        private GUIStyle _m4TitleStyle;
        private GUIStyle _m4ActionStyle;
        private GUIStyle _m4BodyStyle;
        private string _captureStateName = "interactive";
        private bool _built;
        private string _lastDebugMessage = "BOOTING OFFICE SLICE";

        public OfficeCaseRepository CaseRepository => _caseRepository;
        public OfficeCampaignState CampaignState => _campaignState;
        public OfficeSimulationState SimulationState => _simulationState;
        public bool Ready => _built && _simulationState != null && _caseRepository != null;
        public bool CriticalRoutesValid => Ready && ValidateCriticalRoutes();
        public int VisibleFolderCount => _folderViews.Count;
        public OfficeVisualDirector VisualDirector => _m4Director;
        public OfficeVisualSnapshot VisualSnapshot => _m4Snapshot;
        public OfficeM4HudPresenter HudPresenter => _m4HudPresenter;
        public OfficeM6HudPresenter M6HudPresenter => _m6HudPresenter;
        public OfficeM6HudModel M6HudModel => _m6HudModel;
        public OfficeM6Onboarding Onboarding => _m6Onboarding;
        public OfficeAudioDirector AudioDirector => _m5AudioDirector;
        public OfficeAudioSettings AudioSettings => _m5AudioSettings;
        public OfficeFeedbackDirector FeedbackDirector => _m5FeedbackDirector;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RouteDevelopmentPlayerToOfficeSlice()
        {
            string[] arguments = Environment.GetCommandLineArgs();
            if (!HasArgument(arguments, OfficeSliceArgument)) return;

            int sceneIndex = SceneUtility.GetBuildIndexByScenePath(
                "Assets/_Project/Scenes/OfficeSlice.unity");
            if (sceneIndex >= 0 && SceneManager.GetActiveScene().buildIndex != sceneIndex)
                SceneManager.LoadScene(sceneIndex);
        }

        private void Awake()
        {
            if (_built) return;
            name = "Office Slice Bootstrap";
            _m5AudioSettings = OfficeAudioSettings.Load();
            _m5AudioSettings.ApplyCommandLine(Environment.GetCommandLineArgs());
            _m6Onboarding = new OfficeM6Onboarding(
                PlayerPrefs.GetInt(TutorialCompleteKey, 0) != 0);
            _campaignState = OfficeCampaignState.Create();
            _simulationState = _campaignState.CurrentSimulation;
            _caseRepository = _simulationState.Cases;
            RebuildRuntimePresentation();
            _tickDriver = gameObject.AddComponent<OfficeTickDriver>();
            _tickDriver.Initialize(this, _simulationState);
            _built = true;
            RefreshPresentation();
            _lastDebugMessage = "SIX PUBLIC CASES READY";
        }

        private IEnumerator Start()
        {
            string[] arguments = Environment.GetCommandLineArgs();
            _m4HudPresenter.SetReducedFlash(_m5AudioSettings.ReducedFlash ||
                HasArgument(arguments, ReducedFlashArgument));
            string capturePath = ArgumentValue(arguments, CaptureArgument);
            string performancePath = ArgumentValue(arguments, PerformanceArgument);
            if (string.IsNullOrWhiteSpace(capturePath) &&
                string.IsNullOrWhiteSpace(performancePath)) yield break;

            if (!string.IsNullOrWhiteSpace(performancePath))
            {
                OfficeCampaignCaptureDriver.Prepare(
                    _campaignState,
                    3,
                    "promotion-cascade");
                SynchronizeCampaignState();
                RefreshPresentation();
            }
            else if (!HasArgument(arguments, CaptureDistributionArgument))
            {
                string shiftValue = ArgumentValue(arguments, CaptureShiftArgument);
                int shiftOrdinal = 1;
                if (!string.IsNullOrWhiteSpace(shiftValue) &&
                    (!int.TryParse(shiftValue, NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out shiftOrdinal) ||
                     shiftOrdinal < 1 || shiftOrdinal > 3))
                    throw new ArgumentException(
                        "Capture shift must be 1, 2, or 3.",
                        CaptureShiftArgument);
                string stateName = ArgumentValue(arguments, CaptureStateArgument);
                _captureStateName = string.IsNullOrWhiteSpace(stateName)
                    ? "opening" : stateName.Trim().ToLowerInvariant();
                OfficeCampaignCaptureDriver.Prepare(
                    _campaignState,
                    shiftOrdinal,
                    _captureStateName);
                SynchronizeCampaignState();
                _m5AudioDirector?.ResetForState(
                    _simulationState, _campaignState);
                string reviewCue = M5CaptureReviewCue(_captureStateName);
                if (!string.IsNullOrEmpty(reviewCue))
                    _m5AudioDirector?.PlayCue(reviewCue);
                RefreshPresentation();
            }

            yield return null;
            if (!string.IsNullOrWhiteSpace(performancePath))
            {
                yield return RunPerformanceSmoke(performancePath);
                yield break;
            }
            if (HasArgument(arguments, CaptureDistributionArgument))
                PrepareCaptureDistribution();
            yield return new WaitForSecondsRealtime(0.5f);
            yield return new WaitForEndOfFrame();

            string fullPath = Path.GetFullPath(capturePath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath) ?? ".");
            if (File.Exists(fullPath)) File.Delete(fullPath);
            ScreenCapture.CaptureScreenshot(fullPath);
            for (int frame = 0; frame < 600 &&
                (!File.Exists(fullPath) || new FileInfo(fullPath).Length == 0); frame++)
                yield return null;
            if (!File.Exists(fullPath) || new FileInfo(fullPath).Length == 0)
            {
                Debug.LogError("OFFICE_SLICE_CAPTURE_FAILED " + fullPath, this);
                Application.Quit(1);
                yield break;
            }
            var assetIds = new List<string>(_m4Director?.ActiveAssetIds ??
                Array.Empty<string>());
            assetIds.Sort(StringComparer.Ordinal);
            int activeVfx = _m4Director?.VfxPool?.ActiveCount ?? 0;
            int vfxCapacity = _m4Director?.VfxPool?.Capacity ?? 0;
            OfficeAudioVoicePool voices = _m5AudioDirector?.VoicePool;
            Debug.Log("OFFICE_M4_CAPTURE_OK state=" + _captureStateName +
                " assets=" + string.Join(",", assetIds) +
                " visual_count=" + (_m4Director?.ActiveVisualObjectCount ?? 0) +
                " vfx_active=" + activeVfx +
                " vfx_capacity=" + vfxCapacity +
                " audio_assets=" + (_m5AudioDirector?.Catalog.AssetCount ?? 0) +
                " audio_missing=" + (_m5AudioDirector?.Catalog.MissingClipCount ?? 0) +
                " audio_oneshot=" + (voices?.ActiveOneShotCount ?? 0) +
                " audio_continuous=" + (voices?.ActiveContinuousCount ?? 0) +
                " audio_music=" + (voices?.ActiveMusicCount ?? 0) +
                " audio_sources=" + (voices?.TotalSourceCount ?? 0) +
                " audio_roots=" + OfficeAudioVoicePool.ActiveRootCount() +
                " feedback_roots=" + OfficeFeedbackDirector.ActiveRootCount() +
                " checksum=" + _campaignState.Checksum +
                " path=" + fullPath, this);
            Application.Quit(0);
        }

        private IEnumerator RunPerformanceSmoke(string outputPath)
        {
            const int warmupFrames = 60;
            const int sampleFrames = 600;
            const double targetFps = 60d;
            const double maximumP95Milliseconds = 25d;
            const double maximumWorstMilliseconds = 50d;

            Application.runInBackground = true;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 120;
            var endOfFrame = new WaitForEndOfFrame();
            for (int frame = 0; frame < warmupFrames; frame++)
                yield return endOfFrame;

            int initialGameObjects = ActiveSceneGameObjectCount();
            int initialLogicalFolders = _simulationState.Queues.FolderIds.Count;
            int initialMaterials = DistinctM4MaterialCount();
            int initialPoolGrowth = _m4Director?.VfxPool?.GrowthCount ?? 0;
            int initialAudioClipCount = Resources.FindObjectsOfTypeAll<AudioClip>().Length;
            long initialTick = _simulationState.CurrentTick;
            var gcAllocatedRecorder = ProfilerRecorder.StartNew(
                ProfilerCategory.Memory, "GC Allocated In Frame", 32);
            var drawCallsRecorder = ProfilerRecorder.StartNew(
                ProfilerCategory.Render, "Draw Calls Count", 32);
            var batchesRecorder = ProfilerRecorder.StartNew(
                ProfilerCategory.Render, "Batches Count", 32);
            var trianglesRecorder = ProfilerRecorder.StartNew(
                ProfilerCategory.Render, "Triangles Count", 32);
            var verticesRecorder = ProfilerRecorder.StartNew(
                ProfilerCategory.Render, "Vertices Count", 32);
            var textureMemoryRecorder = ProfilerRecorder.StartNew(
                ProfilerCategory.Memory, "Texture Memory", 32);
            var frameMilliseconds = new double[sampleFrames];
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            double previousSeconds = stopwatch.Elapsed.TotalSeconds;
            double worstFrameSeconds = 0d;
            long peakGcAllocated = 0L;
            for (int frame = 0; frame < sampleFrames; frame++)
            {
                yield return endOfFrame;
                double currentSeconds = stopwatch.Elapsed.TotalSeconds;
                double frameSeconds = currentSeconds - previousSeconds;
                frameMilliseconds[frame] = frameSeconds * 1000d;
                if (frameSeconds > worstFrameSeconds)
                    worstFrameSeconds = frameSeconds;
                if (gcAllocatedRecorder.Valid &&
                    gcAllocatedRecorder.LastValue > peakGcAllocated)
                    peakGcAllocated = gcAllocatedRecorder.LastValue;
                previousSeconds = currentSeconds;
            }
            stopwatch.Stop();

            double elapsedSeconds = stopwatch.Elapsed.TotalSeconds;
            double averageFps = sampleFrames / elapsedSeconds;
            long sampledTicks = _simulationState.CurrentTick - initialTick;
            double simulationHz = sampledTicks / elapsedSeconds;
            Array.Sort(frameMilliseconds);
            int p95Index = Math.Max(0,
                (int)Math.Ceiling(sampleFrames * 0.95d) - 1);
            double p95Milliseconds = frameMilliseconds[p95Index];
            int gameObjectGrowth = ActiveSceneGameObjectCount() - initialGameObjects;
            int logicalFolderGrowth = _simulationState.Queues.FolderIds.Count -
                initialLogicalFolders;
            int temporaryGameObjectGrowth = gameObjectGrowth - logicalFolderGrowth;
            int materialGrowth = DistinctM4MaterialCount() - initialMaterials;
            int poolGrowth = (_m4Director?.VfxPool?.GrowthCount ?? 0) -
                initialPoolGrowth;
            OfficeVisualSnapshot steadySnapshot =
                _m4Projector.Project(_simulationState, _campaignState);
            _m4Director.Apply(steadySnapshot);
            long steadyBefore = GC.GetAllocatedBytesForCurrentThread();
            for (int iteration = 0; iteration < 10000; iteration++)
            {
                _m4Director.Apply(steadySnapshot);
                _m5AudioDirector?.Apply(
                    _simulationState, _campaignState, 1f / 60f);
                _m5FeedbackDirector?.Update(1f / 60f);
            }
            long steadyAllocated = GC.GetAllocatedBytesForCurrentThread() -
                steadyBefore;
            double steadyBytesPerUpdate = steadyAllocated / 10000d;
            long drawCalls = LastProfilerValue(drawCallsRecorder);
            long batches = LastProfilerValue(batchesRecorder);
            long triangles = LastProfilerValue(trianglesRecorder);
            long vertices = LastProfilerValue(verticesRecorder);
            long textureMemory = LastProfilerValue(textureMemoryRecorder);
            if (textureMemory < 0L) textureMemory = M4TextureMemoryBytes();
            int audioClipGrowth = Resources.FindObjectsOfTypeAll<AudioClip>().Length -
                initialAudioClipCount;
            gcAllocatedRecorder.Dispose();
            drawCallsRecorder.Dispose();
            batchesRecorder.Dispose();
            trianglesRecorder.Dispose();
            verticesRecorder.Dispose();
            textureMemoryRecorder.Dispose();

            OfficeCampaignState verification = OfficeCampaignState.Create();
            OfficeCampaignCaptureDriver.Prepare(
                verification, 3, "15-final-campaign-result");
            OfficeCampaignState replay = OfficeCampaignReplayRunner.ReplayToResult(
                verification.CreateReplayTape());
            bool replayChecksumMatch = string.Equals(
                verification.Checksum, replay.Checksum, StringComparison.Ordinal);
            bool ownershipValid =
                _simulationState.Queues.HasSingleLogicalOwnerForEveryFolder();
            int activeRoots = OfficeVisualDirector.ActiveRootCount();
            OfficeAudioVoicePool audioVoices = _m5AudioDirector?.VoicePool;
            int activeAudioRoots = OfficeAudioVoicePool.ActiveRootCount();
            int activeFeedbackRoots = OfficeFeedbackDirector.ActiveRootCount();
            bool audioBoundsValid = audioVoices != null &&
                audioVoices.ActiveOneShotCount <= OfficeAudioVoicePool.OneShotCapacity &&
                audioVoices.ActiveContinuousCount <= OfficeAudioVoicePool.ContinuousCapacity &&
                audioVoices.ActiveMusicCount <= OfficeAudioVoicePool.MusicCapacity &&
                audioVoices.TotalSourceCount == 44 && audioVoices.GrowthCount == 0 &&
                activeAudioRoots == 1 && activeFeedbackRoots == 1 &&
                audioClipGrowth == 0 &&
                _m5AudioDirector.Catalog.MissingClipCount == 0;
            bool performancePass = averageFps >= targetFps &&
                p95Milliseconds <= maximumP95Milliseconds &&
                worstFrameSeconds * 1000d <= maximumWorstMilliseconds &&
                simulationHz >= 29d && simulationHz <= 31d &&
                steadyAllocated == 0L && activeRoots == 1 && poolGrowth == 0 &&
                materialGrowth == 0 && temporaryGameObjectGrowth == 0 &&
                ownershipValid && replayChecksumMatch && audioBoundsValid;
            string fullPath = Path.GetFullPath(outputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath) ?? ".");
            var report = new StringBuilder(1024);
            report.AppendLine("OFFICE_SLICE_M5_PERFORMANCE_V1");
            report.Append("resolution=").Append(Screen.width).Append('x')
                .AppendLine(Screen.height.ToString(CultureInfo.InvariantCulture));
            report.Append("frames=").AppendLine(
                sampleFrames.ToString(CultureInfo.InvariantCulture));
            report.Append("elapsed_seconds=").AppendLine(
                elapsedSeconds.ToString("F6", CultureInfo.InvariantCulture));
            report.Append("average_fps=").AppendLine(
                averageFps.ToString("F2", CultureInfo.InvariantCulture));
            report.Append("p95_frame_ms=").AppendLine(
                p95Milliseconds.ToString("F2", CultureInfo.InvariantCulture));
            report.Append("worst_frame_ms=").AppendLine(
                (worstFrameSeconds * 1000d).ToString("F2", CultureInfo.InvariantCulture));
            report.Append("simulation_sample_ticks=").AppendLine(
                sampledTicks.ToString(CultureInfo.InvariantCulture));
            report.Append("simulation_hz=").AppendLine(
                simulationHz.ToString("F2", CultureInfo.InvariantCulture));
            report.Append("profiler_gc_peak_bytes_per_frame=").AppendLine(
                peakGcAllocated.ToString(CultureInfo.InvariantCulture));
            report.Append("steady_visual_total_allocated_bytes=").AppendLine(
                steadyAllocated.ToString(CultureInfo.InvariantCulture));
            report.Append("steady_visual_bytes_per_update=").AppendLine(
                steadyBytesPerUpdate.ToString("F2", CultureInfo.InvariantCulture));
            report.Append("draw_calls=").AppendLine(
                drawCalls.ToString(CultureInfo.InvariantCulture));
            report.Append("batches=").AppendLine(
                batches.ToString(CultureInfo.InvariantCulture));
            report.Append("triangles=").AppendLine(
                triangles.ToString(CultureInfo.InvariantCulture));
            report.Append("vertices=").AppendLine(
                vertices.ToString(CultureInfo.InvariantCulture));
            report.Append("texture_memory_bytes=").AppendLine(
                textureMemory.ToString(CultureInfo.InvariantCulture));
            report.Append("active_visual_roots=").AppendLine(
                activeRoots.ToString(CultureInfo.InvariantCulture));
            report.Append("active_audio_roots=").AppendLine(
                activeAudioRoots.ToString(CultureInfo.InvariantCulture));
            report.Append("active_feedback_roots=").AppendLine(
                activeFeedbackRoots.ToString(CultureInfo.InvariantCulture));
            report.Append("active_one_shot_voices=").AppendLine(
                (audioVoices?.ActiveOneShotCount ?? 0).ToString(
                    CultureInfo.InvariantCulture));
            report.Append("peak_one_shot_voices=").AppendLine(
                (audioVoices?.PeakOneShotVoices ?? 0).ToString(
                    CultureInfo.InvariantCulture));
            report.Append("active_continuous_sources=").AppendLine(
                (audioVoices?.ActiveContinuousCount ?? 0).ToString(
                    CultureInfo.InvariantCulture));
            report.Append("active_music_sources=").AppendLine(
                (audioVoices?.ActiveMusicCount ?? 0).ToString(
                    CultureInfo.InvariantCulture));
            report.Append("audio_source_objects=").AppendLine(
                (audioVoices?.TotalSourceCount ?? 0).ToString(
                    CultureInfo.InvariantCulture));
            report.Append("audio_source_growth=").AppendLine(
                (audioVoices?.GrowthCount ?? 0).ToString(
                    CultureInfo.InvariantCulture));
            report.Append("runtime_audio_clip_growth=").AppendLine(
                audioClipGrowth.ToString(CultureInfo.InvariantCulture));
            report.Append("audio_pcm_memory_estimate_bytes=").AppendLine(
                (_m5AudioDirector?.Catalog.PcmMemoryEstimateBytes ?? 0L).ToString(
                    CultureInfo.InvariantCulture));
            report.Append("audio_assets=").AppendLine(
                (_m5AudioDirector?.Catalog.AssetCount ?? 0).ToString(
                    CultureInfo.InvariantCulture));
            report.Append("audio_missing_clips=").AppendLine(
                (_m5AudioDirector?.Catalog.MissingClipCount ?? 0).ToString(
                    CultureInfo.InvariantCulture));
            report.Append("active_visual_objects=").AppendLine(
                (_m4Director?.ActiveVisualObjectCount ?? 0).ToString(
                    CultureInfo.InvariantCulture));
            report.Append("vfx_active=").AppendLine(
                (_m4Director?.VfxPool?.ActiveCount ?? 0).ToString(
                    CultureInfo.InvariantCulture));
            report.Append("vfx_capacity=").AppendLine(
                (_m4Director?.VfxPool?.Capacity ?? 0).ToString(
                    CultureInfo.InvariantCulture));
            report.Append("vfx_pool_growth=").AppendLine(
                poolGrowth.ToString(CultureInfo.InvariantCulture));
            report.Append("runtime_material_growth=").AppendLine(
                materialGrowth.ToString(CultureInfo.InvariantCulture));
            report.Append("game_object_growth=").AppendLine(
                gameObjectGrowth.ToString(CultureInfo.InvariantCulture));
            report.Append("logical_folder_visual_growth=").AppendLine(
                logicalFolderGrowth.ToString(CultureInfo.InvariantCulture));
            report.Append("temporary_game_object_growth=").AppendLine(
                temporaryGameObjectGrowth.ToString(CultureInfo.InvariantCulture));
            report.Append("active_folders=").AppendLine(
                _simulationState.Queues.FolderIds.Count.ToString(
                    CultureInfo.InvariantCulture));
            report.Append("active_copies=").AppendLine(
                _simulationState.Queues.ActiveCopyCount.ToString(
                    CultureInfo.InvariantCulture));
            report.Append("active_time_slips=").AppendLine(
                _simulationState.GhostClock.SlipIds.Count.ToString(
                    CultureInfo.InvariantCulture));
            report.Append("active_promotion_forms=").AppendLine(
                _simulationState.PromotionCascade.PromotionFormIds.Count.ToString(
                    CultureInfo.InvariantCulture));
            report.Append("causal_events=").AppendLine(
                _simulationState.CausalEvents.Events.Count.ToString(
                    CultureInfo.InvariantCulture));
            report.Append("commands=").AppendLine(
                _simulationState.CommandLog.Commands.Count.ToString(
                    CultureInfo.InvariantCulture));
            report.Append("customers=").AppendLine(
                _simulationState.Customers.Customers.Count.ToString(
                    CultureInfo.InvariantCulture));
            report.Append("staff=").AppendLine(
                _simulationState.Staff.Staff.Count.ToString(
                    CultureInfo.InvariantCulture));
            report.Append("ownership_valid=").AppendLine(ownershipValid.ToString());
            report.Append("final_campaign_checksum=").AppendLine(
                verification.Checksum);
            report.Append("replay_campaign_checksum=").AppendLine(replay.Checksum);
            report.Append("replay_checksum_match=").AppendLine(
                replayChecksumMatch.ToString());
            report.Append("target_fps=").AppendLine(
                targetFps.ToString("F0", CultureInfo.InvariantCulture));
            report.Append("performance_pass=").AppendLine(performancePass.ToString());
            File.WriteAllText(fullPath, report.ToString());

            Debug.Log("OFFICE_M5_PERFORMANCE_" +
                (performancePass ? "OK " : "FAILED ") +
                averageFps.ToString("F2", CultureInfo.InvariantCulture) +
                " FPS " + fullPath, this);
            Application.Quit(performancePass ? 0 : 2);
        }

        private static string M5CaptureReviewCue(string stateName)
        {
            return stateName switch
            {
                "02-shift-1-paper-check" => "paper.correct",
                "03-shift-1-money-trace" => "money.correct",
                "04-shift-1-copy-echo-warning" => "automation.match",
                "05-shift-1-copy-echo-break" => "event.copy-echo-trigger",
                "06-shift-1-upgrade-choice" => "event.shift-close",
                "08-shift-2-ghost-clock" => "event.ghost-clock",
                "09-shift-2-missing-room-access" => "event.missing-room",
                "10-shift-2-second-upgrade-choice" => "event.shift-close",
                "12-shift-3-promotion-warning" => "automation.copied-accepted",
                "13-shift-3-promotion-cascade" => "event.promotion-trigger",
                "14-shift-3-recovery" => "event.recovery-complete",
                "15-final-campaign-result" => "event.final-result",
                "16-next-day-tease" => "event.next-day-tease",
                _ => string.Empty,
            };
        }

        private static long LastProfilerValue(ProfilerRecorder recorder)
        {
            return recorder.Valid ? recorder.LastValue : -1L;
        }

        private int DistinctM4MaterialCount()
        {
            if (_runtimeRoot == null) return 0;
            var ids = new HashSet<int>();
            Renderer[] renderers = _runtimeRoot.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i].sharedMaterial != null)
                    ids.Add(renderers[i].sharedMaterial.GetInstanceID());
            return ids.Count;
        }

        private static int ActiveSceneGameObjectCount()
        {
            GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();
            int count = 0;
            for (int i = 0; i < objects.Length; i++)
                if (objects[i].scene.IsValid()) count++;
            return count;
        }

        private long M4TextureMemoryBytes()
        {
            if (_m4Catalog == null) return 0L;
            var textureIds = new HashSet<int>();
            long bytes = 0L;
            for (int i = 0; i < _m4Catalog.Entries.Count; i++)
            {
                Sprite sprite = _m4Catalog.Entries[i].Sprite;
                Texture2D texture = sprite == null ? null : sprite.texture;
                if (texture == null || !textureIds.Add(texture.GetInstanceID())) continue;
                long measured = UnityEngine.Profiling.Profiler
                    .GetRuntimeMemorySizeLong(texture);
                long rgbaEstimate = (long)texture.width * texture.height * 4L;
                bytes += Math.Max(measured, rgbaEstimate);
            }
            return bytes;
        }

        private void LateUpdate()
        {
            SynchronizeCampaignState();
            RefreshPresentation();
        }

        public bool SynchronizeCampaignState()
        {
            if (_campaignState == null ||
                ReferenceEquals(_simulationState,
                    _campaignState.CurrentSimulation)) return false;
            _simulationState = _campaignState.CurrentSimulation;
            _caseRepository = _simulationState.Cases;
            RebuildRuntimePresentation();
            if (_tickDriver != null)
                _tickDriver.ReplaceState(_simulationState, paused: false);
            _lastDebugMessage = "SHIFT " +
                _campaignState.CurrentShiftOrdinal + " READY / SIX PUBLIC CASES";
            return true;
        }

        public void RefreshPresentation()
        {
            if (!Ready) return;
            OfficeCell wardenCell = _simulationState.Warden.Cell(_simulationState.Grid);
            if (_wardenView != null)
            {
                float wardenX = _simulationState.Warden.XSubunits /
                    (float)OfficeGrid.LogicalSubunitsPerCell;
                float wardenZ = _simulationState.Warden.ZSubunits /
                    (float)OfficeGrid.LogicalSubunitsPerCell;
                _wardenView.position = PresentationPosition(wardenX, wardenZ, 0.52f);
                if (_wardenView.TryGetComponent(out SpriteRenderer wardenRenderer))
                    wardenRenderer.sortingOrder = OfficeVisualDirector.SortingOrder(wardenZ);
            }

            IReadOnlyList<string> folderIds = _simulationState.Queues.FolderIds;
            for (int i = 0; i < folderIds.Count; i++)
            {
                string caseId = folderIds[i];
                OfficeFolderState folder = _simulationState.Queues.GetFolder(caseId);
                if (folder.IsCopy && !_folderViews.ContainsKey(caseId))
                    CreateCopyFolderView(folder);
                if (!_folderViews.TryGetValue(caseId, out Transform view)) continue;
                view.gameObject.SetActive(
                    folder.OwnerKind != OfficeFolderOwnerKind.Cleared);
                if (folder.OwnerKind == OfficeFolderOwnerKind.Cleared) continue;

                int queueIndex = QueueIndex(folder.CurrentRoom, caseId);
                Vector3 destination = SocketWorldPosition(folder.CurrentRoom, queueIndex);
                if (folder.OwnerKind == OfficeFolderOwnerKind.Warden &&
                    _wardenView != null)
                {
                    destination = _wardenView.position + new Vector3(0.55f, 0.15f, 0f);
                }
                else if (folder.OwnerKind == OfficeFolderOwnerKind.Runner &&
                    _staffViews.TryGetValue(folder.OwnerId, out Transform staffView))
                {
                    destination = staffView.position + new Vector3(0.55f, 0.15f, 0f);
                }
                else if (folder.IsMoving)
                {
                    Vector3 source = SocketWorldPosition(folder.SourceRoom, 0);
                    Vector3 target = SocketWorldPosition(folder.DestinationRoom, 0);
                    destination = Vector3.Lerp(
                        source,
                        target,
                        folder.ProgressAt(_simulationState.CurrentTick));
                }
                view.position = destination;
                if (_folderLabels.TryGetValue(caseId, out TextMesh label))
                    label.text = folder.IsCopy
                        ? _simulationState.PromotionCascade.IsPromotionForm(caseId)
                            ? "PROMOTION FORM"
                            : caseId.StartsWith("time-slip.", StringComparison.Ordinal)
                            ? "TIME SLIP"
                            : "COPY"
                        : _caseRepository.Get(caseId)?.DisplayId ?? caseId;
                if (_folderRenderers.TryGetValue(caseId, out Renderer renderer))
                {
                    OfficeCase sourceCase = _caseRepository.Get(folder.SourceCaseId);
                    Color folderColour = IsHighlightedFolder(folder)
                        ? new Color(1f, 0.88f, 0.22f)
                        : folder.IsCopy
                            ? caseId.StartsWith("time-slip.", StringComparison.Ordinal)
                                ? new Color(0.52f, 0.72f, 0.95f)
                                : new Color(0.92f, 0.35f, 0.32f)
                            : FolderColor(sourceCase.Urgency);
                    if (renderer is SpriteRenderer spriteRenderer)
                    {
                        _m4Director?.SetSprite(view, M4FolderVisualId(folder));
                        spriteRenderer.color = _m4Director == null
                            ? folderColour : Color.white;
                    }
                    else
                        renderer.sharedMaterial.color = folderColour;
                }
            }

            if (_supervisorStampView != null)
                _supervisorStampView.gameObject.SetActive(
                    _simulationState.PromotionCascade.SupervisorStampActive);

            RefreshCustomerViews();
            RefreshStaffViews();
            RefreshM4CharacterSprites();
            UpdateBillboards(wardenCell);
            _m6Onboarding?.Observe(_simulationState, _campaignState);
            if (_m6Onboarding != null && _m6Onboarding.Complete &&
                !_m6TutorialCompletionSaved)
            {
                PlayerPrefs.SetInt(TutorialCompleteKey, 1);
                PlayerPrefs.Save();
                _m6TutorialCompletionSaved = true;
            }
            if (_m4Projector != null &&
                (_m4Snapshot == null || _m4Snapshot.Tick != _simulationState.CurrentTick))
                _m4Snapshot = _m4Projector.Project(_simulationState, _campaignState);
            _m4Director?.Apply(_m4Snapshot);
            _m5AudioDirector?.Apply(
                _simulationState, _campaignState, Time.unscaledDeltaTime);
            _m5FeedbackDirector?.Update(Time.unscaledDeltaTime);
            if (_m6HudModel == null ||
                _m6HudModel.Tick != _simulationState.CurrentTick ||
                _m6HudModel.DevelopmentHudVisible !=
                    _m6HudPresenter.DevelopmentHudVisible ||
                _m6HudModel.TutorialText !=
                    (_m6Onboarding?.CurrentSentence ?? string.Empty))
            {
                _m6HudModel = _m6HudPresenter.Project(
                    _simulationState, _campaignState, _m6ControlScheme);
                _m6Onboarding?.ApplyTo(_m6HudModel);
            }
        }

        public void NotifyPresentationInteractionAttempt(bool valid)
        {
            _m5AudioDirector?.NotifyInteractionAttempt(valid);
        }

        public void NotifyControlScheme(OfficeM6ControlScheme scheme)
        {
            if (_m6ControlScheme == scheme) return;
            _m6ControlScheme = scheme;
            _m6HudModel = null;
        }

        public void SetTutorialHintsEnabled(bool enabled)
        {
            _m6Onboarding?.SetHintsEnabled(enabled);
            _m6HudModel = null;
        }

        public bool ToggleWhatHappened()
        {
            if (_m6HudModel == null || !_m6HudModel.WhatHappenedAvailable)
                return false;
            _m6HudPresenter.ToggleWhatHappened();
            _m6HudModel = null;
            RefreshPresentation();
            return true;
        }

        public void ForceAllFoldersThroughM1Route()
        {
            _simulationState.ForceAllFoldersThroughM1Route();
            _lastDebugMessage = "FORCED ROUTE COMPLETE / FRONT > PAPER > MONEY > WEIRD > FRONT";
            RefreshPresentation();
        }

        public void PrepareCaptureDistribution()
        {
            IReadOnlyList<string> folderIds = _simulationState.Queues.FolderIds;
            for (int i = 0; i < folderIds.Count; i++)
            {
                int stages = i % 4;
                for (int stage = 0; stage < stages; stage++)
                {
                    OfficeCommand command = _simulationState.CreateSendCommand(folderIds[i]);
                    if (!_simulationState.TryQueueCommand(command, out OfficeCommandFailure failure))
                        throw new InvalidOperationException(failure.ToString());
                    _simulationState.AdvanceOneTick();
                    _simulationState.AdvanceTicks(OfficeQueueService.DefaultTransferDurationTicks);
                }
            }
            _lastDebugMessage = "CAPTURE DISTRIBUTION / SIX STABLE FOLDERS";
            RefreshPresentation();
        }

        public void ReplayRecordedCommands()
        {
            OfficeCommandLog source = _simulationState.CommandLog;
            _simulationState = OfficeSimulationState.CreateM2Replay(source);
            _caseRepository = _simulationState.Cases;
            _tickDriver.ReplaceState(_simulationState);
            _lastDebugMessage = "REPLAY MODE / LIVE INPUT DISABLED";
            RefreshPresentation();
        }

        public bool RestartShift()
        {
            if (!Ready || !_simulationState.Shift.RestartRequested) return false;
            if (_campaignState != null)
            {
                if (!_campaignState.TryRestartCurrentShift()) return false;
                _simulationState = _campaignState.CurrentSimulation;
                _caseRepository = _simulationState.Cases;
                RebuildRuntimePresentation();
                _tickDriver.ReplaceState(_simulationState, paused: false);
                _lastDebugMessage = "SHIFT RESTARTED FROM CAMPAIGN CHECKPOINT";
                RefreshPresentation();
                return true;
            }
            RebuildAsStandaloneM2();
            return true;
        }

        private void RebuildAsStandaloneM2()
        {
            _simulationState = OfficeSimulationState.CreateM2();
            _caseRepository = _simulationState.Cases;
            RebuildRuntimePresentation();
            _tickDriver.ReplaceState(_simulationState, paused: false);
            _lastDebugMessage = "SHIFT RESTARTED FROM CLEAN CHECKPOINT";
            RefreshPresentation();
        }

        private void RebuildRuntimePresentation()
        {
            _m5AudioDirector?.Dispose();
            _m5AudioDirector = null;
            _m5FeedbackDirector?.Dispose();
            _m5FeedbackDirector = null;
            if (_runtimeRoot != null)
            {
                _runtimeRoot.gameObject.SetActive(false);
                Destroy(_runtimeRoot.gameObject);
            }
            for (int i = 0; i < _runtimeMaterials.Count; i++)
                if (_runtimeMaterials[i] != null) Destroy(_runtimeMaterials[i]);
            _runtimeMaterials.Clear();
            _folderViews.Clear();
            _folderLabels.Clear();
            _folderRenderers.Clear();
            _customerViews.Clear();
            _staffViews.Clear();
            _supervisorStampView = null;
            _m4Catalog = null;
            _m4Director = null;
            _m4Snapshot = null;
            _runtimeRoot = new GameObject("Office Slice Runtime").transform;
            _runtimeRoot.SetParent(transform, false);
            BuildGreybox();
            BuildM5AudioPresentation();
        }

        private void BuildM5AudioPresentation()
        {
            _m5AudioSettings ??= OfficeAudioSettings.Load();
            _m5FeedbackDirector = new OfficeFeedbackDirector(
                _runtimeRoot,
                _camera == null ? null : _camera.transform,
                _m4Director,
                _m5AudioSettings);
            _m5AudioDirector = new OfficeAudioDirector(
                _runtimeRoot,
                OfficeAudioCueCatalog.Load(),
                _m5AudioSettings);
            _m5AudioDirector.CueRouted += _m5FeedbackDirector.RouteCue;
            _m5AudioDirector.ResetForState(_simulationState, _campaignState);
        }

        private void OnDestroy()
        {
            _m5AudioDirector?.Dispose();
            _m5FeedbackDirector?.Dispose();
        }

        public void SaveCommandLog()
        {
            string path = Path.Combine(
                Application.persistentDataPath,
                "desk42-office-slice-m1-commands.json");
            File.WriteAllText(path, _simulationState.CommandLog.ToJson());
            _lastDebugMessage = "COMMAND LOG SAVED / " + path;
        }

        public bool ValidateCriticalRoutes()
        {
            OfficeGrid grid = _simulationState.Grid;
            if (!grid.TryFindPath(grid.SpawnCell, grid.InteractionPoints[0].Cell,
                    out List<OfficeCell> ignored)) return false;
            for (int i = 0; i < grid.InteractionPoints.Count; i++)
            {
                OfficeInteractionPoint point = grid.InteractionPoints[i];
                if (!grid.TryFindPath(grid.SpawnCell, point.Cell, out ignored)) return false;
            }
            return true;
        }

        public string QueueSummary()
        {
            var builder = new System.Text.StringBuilder();
            foreach (OfficeRoomId room in Enum.GetValues(typeof(OfficeRoomId)))
            {
                if (builder.Length > 0) builder.Append(" / ");
                builder.Append(room).Append(':');
                IReadOnlyList<string> ids = _simulationState.Queues.GetQueue(room).CaseIds;
                for (int i = 0; i < ids.Count; i++)
                {
                    if (i > 0) builder.Append(',');
                    builder.Append(ids[i]);
                }
            }
            return builder.ToString();
        }

        private void BuildGreybox()
        {
            _m4Catalog = OfficeSpriteCatalog.LoadRequired();
            if (_m4Catalog != null)
            {
                BuildM4TargetFrame();
                return;
            }
            Debug.LogWarning("OFFICE_M4_CATALOG_UNAVAILABLE LEGACY_GREYBOX_FALLBACK", this);
            CreateCamera();
            CreateLighting();
            CreateFloorAndWalls();
            CreateRooms();
            CreateMachineViews();
            CreateWarden();
            CreateFolderViews();
            CreateCustomerViews();
            CreateStaffViews();
        }

        private void BuildM4TargetFrame()
        {
            Transform visualRoot = new GameObject(
                OfficeVisualDirector.RootName).transform;
            visualRoot.SetParent(_runtimeRoot, false);
            _m4Director = new OfficeVisualDirector(visualRoot, _m4Catalog);
            _m4Projector ??= new OfficeVisualStateProjector();
            CreateM4Camera();
            _m4Director.BuildEnvironment();
            CreateM4ZoneLabels();
            CreateWarden();
            CreateFolderViews();
            CreateCustomerViews();
            CreateStaffViews();
        }

        private void CreateM4Camera()
        {
            GameObject cameraObject = new("Office Slice M4 Camera");
            cameraObject.transform.SetParent(_runtimeRoot, false);
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            _camera = cameraObject.AddComponent<Camera>();
            _camera.orthographic = true;
            _camera.orthographicSize = 4.5f;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.91f, 0.85f, 0.71f);
            if (Camera.main == null) cameraObject.tag = "MainCamera";
        }

        private void CreateM4ZoneLabels()
        {
            CreateLabel("FRONT DESK", new Vector3(-9f, 0f, 6.9f), 0.055f);
            CreateLabel("WAITING AREA", new Vector3(-1f, 0f, -6.5f), 0.055f);
            CreateLabel("PAPER ROOM", new Vector3(0f, 0f, 6.9f), 0.055f);
            CreateLabel("MONEY ROOM", new Vector3(8.5f, 0f, 6.9f), 0.055f);
            CreateLabel("WEIRD ROOM", new Vector3(8.5f, 0f, -6.5f), 0.055f);
        }

        private void CreateCamera()
        {
            GameObject cameraObject = new("Office Slice Camera");
            cameraObject.transform.SetParent(_runtimeRoot, false);
            cameraObject.transform.position = new Vector3(12f, 18f, -16f);
            cameraObject.transform.LookAt(new Vector3(0f, 0f, 0f));
            _camera = cameraObject.AddComponent<Camera>();
            _camera.orthographic = true;
            _camera.orthographicSize = 12f;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.035f, 0.045f, 0.06f);
            if (Camera.main == null) cameraObject.tag = "MainCamera";
        }

        private void CreateLighting()
        {
            GameObject lightObject = new("Office Slice Key Light");
            lightObject.transform.SetParent(_runtimeRoot, false);
            lightObject.transform.rotation = Quaternion.Euler(55f, -35f, 0f);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.15f;
            light.color = new Color(0.84f, 0.9f, 1f);
        }

        private void CreateFloorAndWalls()
        {
            CreateCube("Greybox Floor", new Vector3(0f, -0.35f, 0f),
                new Vector3(29f, 0.5f, 19f), new Color(0.09f, 0.12f, 0.16f));
            Color wall = new(0.24f, 0.28f, 0.34f);
            CreateCube("North Wall", new Vector3(0f, 0.6f, 8.7f),
                new Vector3(28f, 1.4f, 0.35f), wall);
            CreateCube("South Wall", new Vector3(0f, 0.6f, -8.7f),
                new Vector3(28f, 1.4f, 0.35f), wall);
            CreateCube("West Wall", new Vector3(-13.7f, 0.6f, 0f),
                new Vector3(0.35f, 1.4f, 17f), wall);
            CreateCube("East Wall", new Vector3(13.7f, 0.6f, 0f),
                new Vector3(0.35f, 1.4f, 17f), wall);
        }

        private void CreateRooms()
        {
            CreateRoom(OfficeRoomId.FrontDesk, new Vector3(-9f, 0.03f, 5f),
                new Vector3(9f, 0.18f, 5.5f), new Color(0.14f, 0.29f, 0.31f));
            CreateRoom(OfficeRoomId.PaperRoom, new Vector3(0f, 0.03f, 5f),
                new Vector3(7f, 0.18f, 5.5f), new Color(0.27f, 0.29f, 0.18f));
            CreateRoom(OfficeRoomId.MoneyRoom, new Vector3(8.5f, 0.03f, 5f),
                new Vector3(6f, 0.18f, 5.5f), new Color(0.2f, 0.3f, 0.23f));
            CreateRoom(OfficeRoomId.WeirdRoom, new Vector3(8.5f, 0.03f, -3.5f),
                new Vector3(6f, 0.18f, 5.5f), new Color(0.3f, 0.2f, 0.3f));
            CreateRoom(OfficeRoomId.WaitingArea, new Vector3(-1f, 0.03f, -3.5f),
                new Vector3(7f, 0.18f, 5.5f), new Color(0.24f, 0.24f, 0.28f));

            for (int i = 0; i < _simulationState.Grid.InteractionPoints.Count; i++)
            {
                OfficeInteractionPoint point = _simulationState.Grid.InteractionPoints[i];
                Vector3 position = CellToWorld(point.Cell, 0.2f);
                CreateCube(point.Id + " / interaction", position,
                    new Vector3(0.5f, 0.12f, 0.5f), new Color(0.9f, 0.72f, 0.28f));
                CreateLabel(point.Id, position + Vector3.up * 0.25f, 0.055f);
            }

            for (int roomIndex = 0; roomIndex < 4; roomIndex++)
            {
                OfficeRoomId room = (OfficeRoomId)roomIndex;
                for (int socket = 0; socket < _caseRepository.Cases.Count; socket++)
                {
                    Vector3 position = CellToWorld(
                        _simulationState.Grid.SocketCell(room, socket), 0.22f);
                    CreateCube(room + " socket " + socket, position,
                        new Vector3(0.46f, 0.1f, 0.46f), new Color(0.22f, 0.55f, 0.56f));
                }
            }
        }

        private void CreateRoom(
            OfficeRoomId room,
            Vector3 position,
            Vector3 scale,
            Color color)
        {
            CreateCube(room + " greybox", position, scale, color);
            CreateLabel(RoomLabel(room), position + Vector3.up * 0.18f, 0.12f);
        }

        private void CreateWarden()
        {
            if (_m4Director != null)
            {
                GameObject m4Warden = _m4Director.CreateSpriteObject(
                    "Warden", "character.warden.idle", Vector3.zero,
                    Vector3.one, 100);
                _wardenView = m4Warden.transform;
                return;
            }
            GameObject warden = CreateCube("Warden", Vector3.zero,
                new Vector3(0.55f, 0.9f, 0.55f), new Color(0.91f, 0.72f, 0.25f));
            _wardenView = warden.transform;
            CreateLabel("WARDEN", Vector3.up * 1.1f, 0.09f, _wardenView);
        }

        private void CreateFolderViews()
        {
            IReadOnlyList<string> ids = _simulationState.Queues.FolderIds;
            for (int i = 0; i < ids.Count; i++)
            {
                string caseId = ids[i];
                OfficeFolderState folderState =
                    _simulationState.Queues.GetFolder(caseId);
                if (folderState != null && folderState.IsCopy)
                {
                    CreateCopyFolderView(folderState);
                    continue;
                }
                OfficeCase officeCase = _caseRepository.Get(caseId);
                if (officeCase == null)
                    throw new InvalidOperationException(
                        "Authored folder has no public case: " + caseId);
                GameObject folder = _m4Director != null
                    ? _m4Director.CreateSpriteObject(
                        "Folder " + officeCase.DisplayId,
                        string.Equals(caseId,
                            _simulationState.PromotionCascade.MaraCaseId,
                            StringComparison.Ordinal)
                            ? "folder.original" : "folder.normal",
                        Vector3.zero, Vector3.one, 120)
                    : CreateCube("Folder " + officeCase.DisplayId,
                        Vector3.zero, new Vector3(0.62f, 0.16f, 0.42f),
                        FolderColor(officeCase.Urgency));
                _folderViews.Add(caseId, folder.transform);
                if (_m4Director == null)
                {
                    TextMesh label = CreateLabel(officeCase.DisplayId,
                        Vector3.up * 0.18f, 0.055f, folder.transform);
                    _folderLabels.Add(caseId, label);
                }
                _folderRenderers.Add(caseId, folder.GetComponent<Renderer>());
            }
        }

        private void CreateCopyFolderView(OfficeFolderState folder)
        {
            if (_m4Director != null)
            {
                GameObject m4Copy = _m4Director.CreateSpriteObject(
                    "Copied Folder " + folder.CaseId,
                    M4FolderVisualId(folder),
                    Vector3.zero,
                    Vector3.one,
                    121);
                _folderViews.Add(folder.CaseId, m4Copy.transform);
                _folderRenderers.Add(folder.CaseId, m4Copy.GetComponent<Renderer>());
                return;
            }
            GameObject copy = CreateCube("Copied Folder " + folder.CaseId,
                Vector3.zero, new Vector3(0.62f, 0.16f, 0.42f),
                folder.CaseId.StartsWith("time-slip.", StringComparison.Ordinal)
                    ? new Color(0.52f, 0.72f, 0.95f)
                    : new Color(0.92f, 0.35f, 0.32f));
            _folderViews.Add(folder.CaseId, copy.transform);
            TextMesh label = CreateLabel(
                _simulationState.PromotionCascade.IsPromotionForm(folder.CaseId)
                    ? "PROMOTION FORM"
                    : folder.CaseId.StartsWith("time-slip.", StringComparison.Ordinal)
                    ? "TIME SLIP"
                    : "COPY",
                Vector3.up * 0.18f,
                0.055f, copy.transform);
            _folderLabels.Add(folder.CaseId, label);
            _folderRenderers.Add(folder.CaseId, copy.GetComponent<Renderer>());
            if (_campaignState?.Upgrades.RedLabelsTier > 0)
            {
                GameObject redLabel = CreateCube(
                    "Red Label " + folder.CaseId,
                    Vector3.zero,
                    new Vector3(0.22f, 0.03f, 0.16f),
                    new Color(0.95f, 0.08f, 0.08f));
                redLabel.transform.SetParent(copy.transform, false);
                redLabel.transform.localPosition = new Vector3(0f, 0.1f, 0f);
            }
        }

        private string M4FolderVisualId(OfficeFolderState folder)
        {
            if (folder == null) return "folder.normal";
            if (folder.OwnerKind == OfficeFolderOwnerKind.Warden)
                return "folder.carried";
            if (!folder.IsCopy &&
                _simulationState.PromotionCascade.OriginalBadgeReturned &&
                string.Equals(folder.CaseId,
                    _simulationState.PromotionCascade.MaraCaseId,
                    StringComparison.Ordinal))
                return "folder.returned";
            if (_simulationState.AutomationRule.Accepted(folder.CaseId) ||
                _simulationState.PayrollRule.Accepted(folder.CaseId))
                return "folder.rule-matched";
            if (folder.CaseId.StartsWith("time-slip.", StringComparison.Ordinal))
                return "folder.time-slip";
            if (_simulationState.PromotionCascade.IsPromotionForm(folder.CaseId))
                return "folder.promotion-form";
            if (folder.IsCopy)
            {
                int tier = Mathf.Clamp(
                    _campaignState?.Upgrades.RedLabelsTier ?? 0, 0, 2);
                return "folder.copy.tier-" + tier;
            }
            return string.Equals(folder.CaseId,
                _simulationState.PromotionCascade.MaraCaseId,
                StringComparison.Ordinal)
                ? "folder.original" : "folder.normal";
        }

        private void CreateMachineViews()
        {
            CreateCube("Auto Sorter", new Vector3(10f, 0.65f, -3.5f),
                new Vector3(1.3f, 1.3f, 1.3f), new Color(0.25f, 0.65f, 0.62f));
            CreateLabel("AUTO SORTER", new Vector3(10f, 1.55f, -3.5f), 0.07f);
            CreateCube("Copy Echo", new Vector3(5.5f, 0.65f, -3.5f),
                new Vector3(1.3f, 1.3f, 1.3f), new Color(0.62f, 0.35f, 0.58f));
            CreateLabel("COPY ECHO", new Vector3(5.5f, 1.55f, -3.5f), 0.07f);
            if (_campaignState != null && _campaignState.CurrentShiftOrdinal >= 2)
            {
                CreateCube("Clock Terminal", new Vector3(2.2f, 0.55f, 5f),
                    new Vector3(0.8f, 1.1f, 0.8f), new Color(0.3f, 0.52f, 0.78f));
                CreateLabel("GHOST CLOCK", new Vector3(2.2f, 1.35f, 5f), 0.06f);
                CreateCube("Missing Room Door", new Vector3(12f, 0.9f, -3.5f),
                    new Vector3(0.25f, 1.8f, 1.5f), new Color(0.5f, 0.4f, 0.25f));
                CreateLabel("MISSING ROOM", new Vector3(12f, 2f, -3.5f), 0.06f);
            }
            if (_campaignState != null && _campaignState.CurrentShiftOrdinal >= 3)
            {
                GameObject stamp = CreateCube(
                    "Supervisor Stamp",
                    new Vector3(5.5f, 1.55f, -3.5f),
                    new Vector3(0.72f, 0.22f, 0.72f),
                    new Color(0.9f, 0.18f, 0.18f));
                CreateLabel("SUPERVISOR", Vector3.up * 0.38f,
                    0.06f, stamp.transform);
                _supervisorStampView = stamp.transform;
                stamp.SetActive(false);
            }
            if (_campaignState?.Upgrades.FastTraysTier > 0)
            {
                CreateCube("Fast Tray Upgrade", new Vector3(-6.5f, 0.3f, 5f),
                    new Vector3(1.5f, 0.25f, 0.8f), new Color(0.2f, 0.75f, 0.72f));
                CreateLabel("FAST TRAYS", new Vector3(-6.5f, 0.75f, 5f), 0.055f);
            }
            if (_campaignState?.Upgrades.CalmChairsTier > 0)
            {
                CreateCube("Calm Chair Upgrade", new Vector3(-2f, 0.4f, -3.5f),
                    new Vector3(0.8f, 0.8f, 0.8f), new Color(0.4f, 0.62f, 0.45f));
                CreateLabel("CALM CHAIRS", new Vector3(-2f, 1.05f, -3.5f), 0.055f);
            }
        }

        private void CreateCustomerViews()
        {
            IReadOnlyList<OfficeCustomerState> customers =
                _simulationState.Customers.Customers;
            for (int i = 0; i < customers.Count; i++)
            {
                OfficeCustomerState customer = customers[i];
                GameObject body;
                if (_m4Director != null)
                    body = _m4Director.CreateSpriteObject(
                        "Customer " + customer.DisplayName,
                        CustomerVisualId(customer.DisplayName, OfficeVisibleMoodState.Calm),
                        Vector3.zero, new Vector3(0.8f, 0.8f, 1f), 105);
                else
                {
                    body = CreateCube("Customer " + customer.DisplayName,
                        Vector3.zero, new Vector3(0.62f, 1.05f, 0.62f),
                        new Color(0.42f, 0.63f, 0.78f));
                    CreateLabel(customer.DisplayName, Vector3.up * 1.25f,
                        0.065f, body.transform);
                }
                _customerViews.Add(customer.CustomerId, body.transform);
            }
            RefreshCustomerViews();
        }

        private void RefreshCustomerViews()
        {
            if (_simulationState.Customers == null) return;
            IReadOnlyList<OfficeCustomerState> customers =
                _simulationState.Customers.Customers;
            int waitingIndex = 0;
            for (int i = 0; i < customers.Count; i++)
            {
                OfficeCustomerState customer = customers[i];
                if (!_customerViews.TryGetValue(customer.CustomerId,
                        out Transform view)) continue;
                bool visible = customer.QueueState == OfficeCustomerQueueState.AtDesk ||
                    customer.QueueState == OfficeCustomerQueueState.Waiting;
                view.gameObject.SetActive(visible);
                if (!visible) continue;
                float x = customer.QueueState == OfficeCustomerQueueState.AtDesk
                    ? -10f : -4f + waitingIndex++ * 1.6f;
                float z = customer.QueueState == OfficeCustomerQueueState.AtDesk ? 7f : -3f;
                view.position = PresentationPosition(x, z, 0.55f);
                if (view.TryGetComponent(out SpriteRenderer renderer))
                    renderer.sortingOrder = OfficeVisualDirector.SortingOrder(z);
            }
        }

        private bool IsHighlightedFolder(OfficeFolderState folder)
        {
            OfficeCustomerState active = _simulationState.Customers.ActiveDeskCustomer;
            if (active == null || !string.Equals(active.LinkedAutomationClaimId,
                    folder.CaseId, StringComparison.Ordinal)) return false;
            if (folder.OwnerKind == OfficeFolderOwnerKind.Warden) return true;
            OfficeInteractionPoint point = _simulationState.Grid.ChooseClosestInteractionPoint(
                _simulationState.Warden.Cell(_simulationState.Grid));
            return point != null && !folder.IsMoving &&
                folder.OwnerKind == OfficeFolderOwnerKind.RoomQueue &&
                folder.CurrentRoom == point.Room;
        }

        private void CreateStaffViews()
        {
            for (int i = 0; i < _simulationState.Staff.Staff.Count; i++)
            {
                OfficeStaffState staff = _simulationState.Staff.Staff[i];
                if (_m4Director != null)
                {
                    string id = staff.Role == OfficeStaffRole.Runner
                        ? "character.runner.idle"
                        : "character.talker.idle";
                    GameObject m4Body = _m4Director.CreateSpriteObject(
                        staff.DisplayName, id, Vector3.zero, Vector3.one, 104);
                    _staffViews.Add(staff.StaffId, m4Body.transform);
                    continue;
                }
                Color color = staff.Role == OfficeStaffRole.Runner
                    ? new Color(0.38f, 0.75f, 0.45f)
                    : new Color(0.72f, 0.48f, 0.82f);
                GameObject body = CreateCube(staff.DisplayName,
                    Vector3.zero, new Vector3(0.52f, 0.82f, 0.52f), color);
                CreateLabel(staff.DisplayName, Vector3.up * 1.02f,
                    0.06f, body.transform);
                _staffViews.Add(staff.StaffId, body.transform);
            }
            RefreshStaffViews();
        }

        private void RefreshStaffViews()
        {
            if (_simulationState.Staff == null) return;
            for (int i = 0; i < _simulationState.Staff.Staff.Count; i++)
            {
                OfficeStaffState staff = _simulationState.Staff.Staff[i];
                if (!_staffViews.TryGetValue(staff.StaffId, out Transform view)) continue;
                float x = staff.XSubunits / (float)OfficeGrid.LogicalSubunitsPerCell;
                float z = staff.ZSubunits / (float)OfficeGrid.LogicalSubunitsPerCell;
                view.position = PresentationPosition(x, z, 0.43f);
                if (view.TryGetComponent(out SpriteRenderer renderer))
                    renderer.sortingOrder = OfficeVisualDirector.SortingOrder(z);
            }
        }

        private void RefreshM4CharacterSprites()
        {
            if (_m4Director == null) return;
            if (_wardenView != null)
            {
                string wardenId;
                string action = _simulationState.PrimaryActionLabel;
                if (_simulationState.CustomerPressure.CalmActive)
                    wardenId = "character.warden.calm";
                else if (_simulationState.ManualTasks.IsActive)
                    wardenId = "character.warden.interact";
                else if (action == "STOP COPIER" || action == "STOP CLOCK" ||
                    action == "REMOVE SUPERVISOR STAMP" ||
                    action == "CLEAR PROMOTION FORM" || action == "CLEAR COPY")
                    wardenId = "character.warden.fix";
                else
                    wardenId = OfficeTickAnimationDriver.WardenMovementAssetId(
                        _tickDriver?.VisualMovement ?? OfficeInputDirection.None,
                        _simulationState.Carry.IsCarrying);
                _m4Director.SetSprite(_wardenView, wardenId);
            }

            IReadOnlyList<OfficeCustomerState> customers =
                _simulationState.Customers.Customers;
            for (int i = 0; i < customers.Count; i++)
            {
                OfficeCustomerState customer = customers[i];
                if (_customerViews.TryGetValue(customer.CustomerId, out Transform view))
                    _m4Director.SetSprite(view,
                        CustomerVisualId(customer.DisplayName, customer.VisibleMoodState));
            }

            for (int i = 0; i < _simulationState.Staff.Staff.Count; i++)
            {
                OfficeStaffState staff = _simulationState.Staff.Staff[i];
                if (_staffViews.TryGetValue(staff.StaffId, out Transform view))
                    _m4Director.SetSprite(view, StaffVisualId(staff));
            }
        }

        private static string StaffVisualId(OfficeStaffState staff)
        {
            if (staff.Role == OfficeStaffRole.Runner)
            {
                if (staff.IsBlocked) return "character.runner.blocked";
                if (staff.VisibleIntent.StartsWith("COPIER ORDER", StringComparison.Ordinal) ||
                    staff.VisibleIntent.StartsWith("FOLLOWING COPIER", StringComparison.Ordinal))
                    return "character.runner.obey-copier";
                if (staff.VisibleIntent.StartsWith("CARRYING", StringComparison.Ordinal))
                    return "character.runner.carry";
                if (staff.VisibleIntent.StartsWith("CHECKING", StringComparison.Ordinal))
                    return "character.runner.work";
                return "character.runner.idle";
            }
            if (staff.IsBlocked) return "character.talker.blocked";
            if (staff.VisibleIntent.StartsWith("CALMING", StringComparison.Ordinal))
                return "character.talker.calm-customer";
            if (staff.VisibleIntent.StartsWith("CHECKING", StringComparison.Ordinal))
                return "character.talker.work";
            return "character.talker.idle";
        }

        private static string CustomerVisualId(
            string displayName,
            OfficeVisibleMoodState mood)
        {
            string calm;
            string worried;
            string upset;
            string strange;
            switch (displayName)
            {
                case "NIA BELL":
                    calm = "character.nia-bell.calm"; worried = "character.nia-bell.worried";
                    upset = "character.nia-bell.upset"; strange = "character.nia-bell.strange";
                    break;
                case "OWEN PIKE":
                    calm = "character.owen-pike.calm"; worried = "character.owen-pike.worried";
                    upset = "character.owen-pike.upset"; strange = "character.owen-pike.strange";
                    break;
                case "MARA VALE":
                    calm = "character.mara-vale.calm"; worried = "character.mara-vale.worried";
                    upset = "character.mara-vale.upset"; strange = "character.mara-vale.strange";
                    break;
                case "IRIS COLE":
                    calm = "character.iris-cole.calm"; worried = "character.iris-cole.worried";
                    upset = "character.iris-cole.upset"; strange = "character.iris-cole.strange";
                    break;
                case "TOMAS REED":
                    calm = "character.tomas-reed.calm"; worried = "character.tomas-reed.worried";
                    upset = "character.tomas-reed.upset"; strange = "character.tomas-reed.strange";
                    break;
                default:
                    calm = "character.june-hart.calm"; worried = "character.june-hart.worried";
                    upset = "character.june-hart.upset"; strange = "character.june-hart.strange";
                    break;
            }
            return mood switch
            {
                OfficeVisibleMoodState.Worried => worried,
                OfficeVisibleMoodState.Upset => upset,
                OfficeVisibleMoodState.Strange => strange,
                OfficeVisibleMoodState.Break => strange,
                _ => calm,
            };
        }

        private GameObject CreateCube(
            string objectName,
            Vector3 position,
            Vector3 scale,
            Color color)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = objectName;
            cube.transform.SetParent(_runtimeRoot, false);
            cube.transform.position = position;
            cube.transform.localScale = scale;
            Collider collider = cube.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            cube.GetComponent<Renderer>().sharedMaterial = CreateMaterial(color);
            return cube;
        }

        private TextMesh CreateLabel(
            string text,
            Vector3 position,
            float characterSize,
            Transform parent = null)
        {
            GameObject labelObject = new(text);
            labelObject.transform.SetParent(parent ?? _runtimeRoot, false);
            if (_m4Director != null)
            {
                labelObject.transform.position = parent == null
                    ? PresentationPosition(position.x, position.z, -0.1f)
                    : parent.position + new Vector3(0f, 0.7f, -0.1f);
            }
            else
            {
                labelObject.transform.position = position;
            }
            TextMesh label = labelObject.AddComponent<TextMesh>();
            label.text = text;
            label.fontSize = 48;
            label.characterSize = characterSize;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.color = _m4Director == null ? Color.white : new Color(0.08f, 0.08f, 0.1f);
            if (_m4Director != null)
            {
                label.fontStyle = FontStyle.Bold;
                MeshRenderer meshRenderer = label.GetComponent<MeshRenderer>();
                meshRenderer.sortingOrder = 210;
            }
            return label;
        }

        private Material CreateMaterial(Color color)
        {
            Shader shader = Shader.Find("Desk42/AutomationLit") ??
                Shader.Find("Unlit/Color") ?? Shader.Find("Standard");
            if (shader == null)
                throw new MissingReferenceException(
                    "A runtime-safe greybox shader could not be resolved.");
            var material = new Material(shader) { color = color };
            _runtimeMaterials.Add(material);
            return material;
        }

        private Vector3 SocketWorldPosition(OfficeRoomId room, int queueIndex)
        {
            return CellToWorld(_simulationState.Grid.SocketCell(room, queueIndex), 0.5f);
        }

        private Vector3 CellToWorld(OfficeCell cell, float y)
        {
            return PresentationPosition(cell.X, cell.Z, y);
        }

        private Vector3 PresentationPosition(float x, float z, float legacyHeight)
        {
            return _m4Director == null
                ? new Vector3(x, legacyHeight, z)
                : OfficeVisualDirector.SimulationToVisual(x, z, 0f);
        }

        private int QueueIndex(OfficeRoomId room, string caseId)
        {
            IReadOnlyList<string> ids = _simulationState.Queues.GetQueue(room).CaseIds;
            for (int i = 0; i < ids.Count; i++)
                if (string.Equals(ids[i], caseId, StringComparison.Ordinal)) return i;
            return 0;
        }

        private void UpdateBillboards(OfficeCell wardenCell)
        {
            if (_m4Director != null) return;
            if (_camera == null) return;
            for (int i = 0; i < _runtimeRoot.childCount; i++)
            {
                Transform child = _runtimeRoot.GetChild(i);
                TextMesh text = child.GetComponent<TextMesh>();
                if (text == null) continue;
                child.rotation = Quaternion.LookRotation(child.position - _camera.transform.position);
            }
        }

        private static string RoomLabel(OfficeRoomId room)
        {
            return room switch
            {
                OfficeRoomId.FrontDesk => "FRONT DESK",
                OfficeRoomId.PaperRoom => "PAPER ROOM",
                OfficeRoomId.MoneyRoom => "MONEY ROOM",
                OfficeRoomId.WeirdRoom => "WEIRD ROOM",
                OfficeRoomId.WaitingArea => "WAITING AREA",
                _ => room.ToString().ToUpperInvariant(),
            };
        }

        private static Color FolderColor(OfficeCaseUrgency urgency)
        {
            return urgency switch
            {
                OfficeCaseUrgency.Critical => new Color(0.9f, 0.28f, 0.22f),
                OfficeCaseUrgency.Urgent => new Color(0.95f, 0.55f, 0.2f),
                OfficeCaseUrgency.Elevated => new Color(0.75f, 0.78f, 0.3f),
                _ => new Color(0.45f, 0.74f, 0.78f),
            };
        }

        private void OnGUI()
        {
            if (!Ready) return;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (Event.current.type == EventType.KeyDown &&
                Event.current.keyCode == KeyCode.F9)
            {
                _m4HudPresenter.SetDevelopmentHudVisible(
                    !_m4HudPresenter.DevelopmentHudVisible);
                _m6HudPresenter.SetDevelopmentHudVisible(
                    _m4HudPresenter.DevelopmentHudVisible);
                _m6HudModel = _m6HudPresenter.Project(
                    _simulationState, _campaignState, _m6ControlScheme);
                Event.current.Use();
            }
#endif
            if (_m4Director != null)
            {
                DrawM6Hud();
                return;
            }
            DrawLegacyHud();
        }

        private void DrawM6Hud()
        {
            EnsureM4HudStyles();
            _m6HudModel ??= _m6HudPresenter.Project(
                _simulationState, _campaignState, _m6ControlScheme);

            if (_m6HudModel.ResultVisible)
            {
                Rect result = _m6HudPresenter.ResultRect(
                    Screen.width, Screen.height);
                DrawM4PaperCard(result);
                GUILayout.BeginArea(Inset(result, 18f));
                GUILayout.Label(_m6HudModel.ResultTitle, _m4TitleStyle);
                GUILayout.Space(18f);
                GUILayout.Label(_m6HudModel.ResultSummary, _m4ActionStyle);
                GUILayout.FlexibleSpace();
                GUILayout.Label(_m6HudModel.TomorrowText, _m4BodyStyle);
                GUILayout.EndArea();
                DrawM4DevelopmentHud();
                return;
            }

            if (_m6HudModel.TutorialVisible)
            {
                Rect tutorial = _m6HudPresenter.TutorialRect(
                    Screen.width, Screen.height);
                DrawM4PaperCard(tutorial);
                GUILayout.BeginArea(Inset(tutorial, 10f));
                GUILayout.Label(_m6HudModel.TutorialText, _m4TitleStyle);
                GUILayout.EndArea();
            }

            Rect top = _m6HudPresenter.TopBarRect(Screen.width, Screen.height);
            DrawM4PaperCard(top);
            GUILayout.BeginArea(Inset(top, 10f));
            GUILayout.BeginHorizontal();
            GUILayout.Label(_m6HudModel.ShiftText, _m4TitleStyle);
            GUILayout.FlexibleSpace();
            GUILayout.Label(_m6HudModel.TimeText + "   " +
                _m6HudModel.WaitingText + "   " +
                _m6HudModel.DangerText, _m4BodyStyle);
            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            Rect action = _m6HudPresenter.ActionRect(
                Screen.width, Screen.height);
            DrawM4PaperCard(action);
            GUILayout.BeginArea(Inset(action, 10f));
            GUILayout.Label(OfficeM6PlayerCopyCatalog.DoThis, _m4TitleStyle);
            GUILayout.Label(_m6HudModel.ActionPrompt, _m4ActionStyle);
            if (_m6HudModel.ManualChoicesVisible)
                GUILayout.Label(
                    OfficeM6PlayerCopyCatalog.TutorialChoice, _m4BodyStyle);
            else if (_m6HudModel.DecisionChoicesVisible)
                GUILayout.Label(
                    OfficeM6PlayerCopyCatalog.DecisionChoice, _m4BodyStyle);
            if (_m6HudModel.CarriedFileVisible)
                GUILayout.Label(_m6HudModel.CarriedFileText, _m4BodyStyle);
            if (!string.IsNullOrWhiteSpace(_m6HudModel.OriginalCopyLegend))
                GUILayout.Label(_m6HudModel.OriginalCopyLegend, _m4BodyStyle);
            if (_m6HudModel.WhatHappenedAvailable)
                GUILayout.Label(_m6HudModel.WhatHappenedPrompt, _m4BodyStyle);
            GUILayout.EndArea();

            if (_m6HudModel.CustomerCardVisible)
            {
                Rect customer = _m6HudPresenter.CustomerCardRect(
                    Screen.width, Screen.height);
                DrawM4PaperCard(customer);
                GUILayout.BeginArea(new Rect(
                    customer.x + 10f, customer.y + 10f,
                    customer.width - 98f, customer.height - 20f));
                GUILayout.Label(
                    OfficeM6PlayerCopyCatalog.AtTheDesk, _m4TitleStyle);
                GUILayout.Label(_m6HudModel.CustomerName, _m4ActionStyle);
                GUILayout.Label(_m6HudModel.CustomerProblem, _m4BodyStyle);
                GUILayout.Label(_m6HudModel.CustomerMood, _m4BodyStyle);
                GUILayout.EndArea();
                DrawM6Portrait();
            }

            if (_m6HudModel.CaseCardVisible)
            {
                Rect caseCard = _m6HudPresenter.CaseCardRect(
                    Screen.width, Screen.height);
                DrawM4PaperCard(caseCard);
                GUILayout.BeginArea(Inset(caseCard, 10f));
                GUILayout.Label(
                    OfficeM6PlayerCopyCatalog.ThisFile, _m4TitleStyle);
                GUILayout.Label(_m6HudModel.WhatWeKnow, _m4BodyStyle);
                GUILayout.Label(_m6HudModel.WhatNeedsChecking, _m4BodyStyle);
                GUILayout.Label(_m6HudModel.NextUsefulAction, _m4BodyStyle);
                GUILayout.EndArea();
            }

            if (_m6HudModel.RuleCardVisible)
            {
                Rect rule = _m6HudPresenter.RuleCardRect(
                    Screen.width, Screen.height);
                DrawM4PaperCard(rule);
                GUILayout.BeginArea(Inset(rule, 10f));
                GUILayout.Label(
                    OfficeM6PlayerCopyCatalog.YourMachines, _m4TitleStyle);
                if (!string.IsNullOrWhiteSpace(_m6HudModel.RuleOneText))
                    GUILayout.Label(_m6HudModel.RuleOneText, _m4BodyStyle);
                if (!string.IsNullOrWhiteSpace(_m6HudModel.RuleTwoText))
                    GUILayout.Label(_m6HudModel.RuleTwoText, _m4BodyStyle);
                GUILayout.EndArea();
            }

            if (_m6HudModel.BreakCardVisible)
            {
                Rect breakCard = _m6HudPresenter.BreakCardRect(
                    Screen.width, Screen.height);
                DrawM4PaperCard(breakCard);
                GUILayout.BeginArea(Inset(breakCard, 10f));
                GUILayout.Label(_m6HudModel.BreakTitle, _m4TitleStyle);
                GUILayout.Label(_m6HudModel.BreakCause, _m4BodyStyle);
                GUILayout.Label(
                    _m6HudModel.ActionableProblemRoom, _m4BodyStyle);
                for (int i = 0; i < _m6HudModel.RecoveryItems.Count; i++)
                    GUILayout.Label(_m6HudModel.RecoveryItems[i], _m4BodyStyle);
                GUILayout.EndArea();
            }

            if (_m6HudModel.WhatHappenedVisible)
            {
                Rect happened = _m6HudPresenter.WhatHappenedRect(
                    Screen.width, Screen.height);
                DrawM4PaperCard(happened);
                GUILayout.BeginArea(Inset(happened, 18f));
                GUILayout.Label(
                    OfficeM6PlayerCopyCatalog.WhatHappenedTitle,
                    _m4TitleStyle);
                GUILayout.Space(10f);
                GUILayout.Label(_m6HudModel.WhatHappenedText, _m4ActionStyle);
                GUILayout.FlexibleSpace();
                GUILayout.Label(_m6HudModel.WhatHappenedPrompt + " TO CLOSE",
                    _m4BodyStyle);
                GUILayout.EndArea();
            }

            DrawM4DevelopmentHud();
        }

        private void DrawM6Portrait()
        {
            OfficeCustomerState customer =
                _simulationState.Customers.ActiveDeskCustomer;
            if (customer == null || _m4Catalog == null) return;
            string id = CustomerVisualId(
                customer.DisplayName, customer.VisibleMoodState)
                .Replace("character.", "portrait.");
            if (customer.DisplayName == "MARA VALE" &&
                _simulationState.PromotionCascade.Active)
                id = "portrait.mara-vale.promotion-cascade";
            else if (customer.DisplayName == "TOMAS REED" &&
                _simulationState.GhostClock.Active)
                id = "portrait.tomas-reed.ghost-clock";
            if (_m4Catalog.TryResolve(id, out Sprite portrait) && portrait != null)
                GUI.DrawTexture(
                    _m6HudPresenter.CustomerPortraitRect(
                        Screen.width, Screen.height),
                    portrait.texture, ScaleMode.ScaleToFit, true);
        }

        private void DrawM4Hud()
        {
            EnsureM4HudStyles();
            if (_campaignState != null &&
                _campaignState.Phase == OfficeCampaignPhase.CampaignResult)
            {
                Rect resultPanel = _m4HudPresenter.ResultPanelRect(
                    Screen.width, Screen.height);
                DrawM4PaperCard(resultPanel);
                GUILayout.BeginArea(Inset(resultPanel, 10f));
                GUILayout.Label("DESK 42 / THREE-DAY RESULT", _m4TitleStyle);
                if (_captureStateName == "16-next-day-tease")
                    GUILayout.Label("TOMORROW'S DESK", _m4ActionStyle);
                Color previousContent = GUI.contentColor;
                GUI.contentColor = new Color(0.08f, 0.08f, 0.1f, 1f);
                DrawCampaignResult();
                GUI.contentColor = previousContent;
                GUILayout.EndArea();
                DrawM4DevelopmentHud();
                return;
            }

            Rect panel = _m4HudPresenter.PlayerPanelRect(Screen.width, Screen.height);
            DrawM4PaperCard(panel);
            GUILayout.BeginArea(Inset(panel, 10f));
            GUILayout.Label("DESK 42 / DAY " + _campaignState.CurrentShiftOrdinal,
                _m4TitleStyle);
            GUILayout.Label(_campaignState.CurrentShift.Title + "  •  " +
                _simulationState.Shift.Phase.ToString().ToUpperInvariant(),
                _m4BodyStyle);
            OfficeCustomerState customer =
                _simulationState.Customers.ActiveDeskCustomer;
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(panel.width - 112f));
            if (customer == null)
                GUILayout.Label("NO CUSTOMER AT THE DESK", _m4BodyStyle);
            else
            {
                GUILayout.Label(customer.DisplayName + "  •  " +
                    customer.VisibleMoodState.ToString().ToUpperInvariant(),
                    _m4BodyStyle);
                GUILayout.Label(customer.Problem, _m4BodyStyle);
            }
            GUILayout.EndVertical();
            GUILayout.Label(string.Empty, GUILayout.Width(72f), GUILayout.Height(68f));
            GUILayout.EndHorizontal();

            GUILayout.Space(4f);
            GUILayout.Label("DO NOW", _m4TitleStyle);
            GUILayout.Label(M4CurrentTaskLabel(), _m4ActionStyle);
            GUILayout.Label(M4WorkSummary(customer), _m4BodyStyle);
            GUILayout.Space(3f);
            GUILayout.Label("AUTO SORTER " + RuleState(
                    _simulationState.AutomationRule.Unlocked,
                    _simulationState.AutomationRule.Enabled) +
                "  •  PAY RULE " + RuleState(
                    _simulationState.PayrollRule.Unlocked,
                    _simulationState.PayrollRule.Enabled), _m4BodyStyle);
            GUILayout.Label(M4QueueSummary(), _m4BodyStyle);
            string recovery = M4RecoveryChecklist();
            if (!string.IsNullOrWhiteSpace(recovery))
            {
                GUILayout.Space(3f);
                GUILayout.Label("FIX THE OFFICE", _m4TitleStyle);
                GUILayout.Label(recovery, _m4BodyStyle);
            }
            if (_campaignState.Phase == OfficeCampaignPhase.ChooseUpgrade)
                GUILayout.Label("DECIDE: 1 FAST TRAYS  •  2 CALM CHAIRS  •  3 RED LABELS",
                    _m4BodyStyle);
            GUILayout.FlexibleSpace();
            GUILayout.Label("MOVE WASD / STICK  •  ACT E / A  •  PUT DOWN Q / B",
                _m4BodyStyle);
            GUILayout.EndArea();

            DrawM4Portrait(customer);
            DrawM4DevelopmentHud();
        }

        private void EnsureM4HudStyles()
        {
            if (_m4TitleStyle != null) return;
            _m4TitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
            };
            _m4ActionStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
            };
            _m4BodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                wordWrap = true,
            };
            Color ink = new(0.08f, 0.08f, 0.1f, 1f);
            _m4TitleStyle.normal.textColor = ink;
            _m4ActionStyle.normal.textColor = ink;
            _m4BodyStyle.normal.textColor = ink;
        }

        private static Rect Inset(Rect rect, float amount)
        {
            return new Rect(rect.x + amount, rect.y + amount,
                rect.width - amount * 2f, rect.height - amount * 2f);
        }

        private static void DrawM4PaperCard(Rect rect)
        {
            Color previous = GUI.color;
            GUI.color = new Color(0.91f, 0.85f, 0.71f, 0.94f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = new Color(0.42f, 0.31f, 0.24f, 1f);
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 3f),
                Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - 3f, rect.width, 3f),
                Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.y, 3f, rect.height),
                Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.xMax - 3f, rect.y, 3f, rect.height),
                Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private void DrawM4Portrait(OfficeCustomerState customer)
        {
            if (customer == null || _m4Catalog == null) return;
            string id = CustomerVisualId(
                customer.DisplayName, customer.VisibleMoodState)
                .Replace("character.", "portrait.");
            if (customer.DisplayName == "MARA VALE" &&
                _simulationState.PromotionCascade.Active)
                id = "portrait.mara-vale.promotion-cascade";
            else if (customer.DisplayName == "TOMAS REED" &&
                _simulationState.GhostClock.Active)
                id = "portrait.tomas-reed.ghost-clock";
            if (_m4Catalog.TryResolve(id, out Sprite portrait) && portrait != null)
                GUI.DrawTexture(
                    _m4HudPresenter.PortraitRect(Screen.width, Screen.height),
                    portrait.texture, ScaleMode.ScaleToFit, true);
        }

        private string M4CurrentTaskLabel()
        {
            if (_campaignState.Phase == OfficeCampaignPhase.ChooseUpgrade)
                return "DECIDE";
            if (_campaignState.Phase == OfficeCampaignPhase.ReadyForNextShift)
                return "OPEN TOMORROW'S DESK";
            if (_simulationState.ManualTasks.IsActive)
                return _simulationState.ManualTasks.ActiveKind switch
                {
                    OfficeManualTaskKind.Compare => "CHECK PAPERS",
                    OfficeManualTaskKind.Trace => "TRACE MONEY",
                    _ => "CHECK WEIRD STUFF",
                };
            return _simulationState.PrimaryActionLabel;
        }

        private string M4WorkSummary(OfficeCustomerState customer)
        {
            if (_simulationState.ManualTasks.IsActive)
                return "Choose the matching public record with 1–4.";
            if (customer == null) return "Wait for the next person.";
            return FolderStatus(customer.LinkedAutomationClaimId);
        }

        private string M4RecoveryChecklist()
        {
            if (_simulationState.PromotionCascade.Active)
                return "STOP MACHINE  •  REMOVE STAMP  •  CLEAR FORMS  •  CALM MARA  •  FIND ORIGINAL  •  REASSIGN RUNNER";
            if (_simulationState.GhostClock.Active)
                return "STOP CLOCK  •  CLEAR TIME SLIPS  •  CALM TOMAS";
            if (_simulationState.MissingRoomAccess.Active)
                return "CLOSE MISSING ROOM  •  HELP IRIS";
            if (_simulationState.BreakState.Active &&
                !_simulationState.BreakState.Recovered)
                return "STOP MACHINE  •  CLEAR COPIES  •  CALM CUSTOMER  •  FIND ORIGINAL";
            if (_simulationState.BreakState.Recovered ||
                _simulationState.GhostClock.Recovered ||
                _simulationState.MissingRoomAccess.Recovered ||
                _simulationState.PromotionCascade.Recovered)
                return "RECOVERY COMPLETE";
            return string.Empty;
        }

        private string M4QueueSummary()
        {
            return "FILES  FRONT " +
                _simulationState.Queues.GetQueue(OfficeRoomId.FrontDesk).Count +
                "  PAPER " + _simulationState.Queues.GetQueue(OfficeRoomId.PaperRoom).Count +
                "  MONEY " + _simulationState.Queues.GetQueue(OfficeRoomId.MoneyRoom).Count +
                "  WEIRD " + _simulationState.Queues.GetQueue(OfficeRoomId.WeirdRoom).Count;
        }

        private static string RuleState(bool unlocked, bool enabled)
        {
            return enabled ? "ON" : unlocked ? "OFF" : "LOCKED";
        }

        private void DrawM4DevelopmentHud()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!_m4HudPresenter.DevelopmentHudVisible) return;
            GUILayout.BeginArea(
                _m4HudPresenter.DevelopmentPanelRect(Screen.width, Screen.height),
                GUI.skin.box);
            GUILayout.Label("M4 DEVELOPMENT / F9 TO HIDE", _m4TitleStyle);
            GUILayout.Label("TICK " + _simulationState.CurrentTick +
                "  CHECKSUM " + _simulationState.Checksum, _m4BodyStyle);
            GUILayout.Label("ROOTS " + OfficeVisualDirector.ActiveRootCount() +
                "  VISUALS " + (_m4Director?.ActiveVisualObjectCount ?? 0) +
                "  VFX " + (_m4Director?.VfxPool?.ActiveCount ?? 0) + "/" +
                (_m4Director?.VfxPool?.Capacity ?? 0), _m4BodyStyle);
            GUILayout.EndArea();
#endif
        }

        private void DrawLegacyHud()
        {
            if (!Ready) return;
            if (_campaignState != null &&
                _campaignState.Phase == OfficeCampaignPhase.CampaignResult)
            {
                GUILayout.BeginArea(
                    new Rect(16f, 16f, 620f, 680f),
                    GUI.skin.box);
                GUILayout.Label("DESK 42 / THREE-DAY RESULT");
                GUILayout.Label("DAY 3 / " + _campaignState.CurrentShift.Title);
                DrawCampaignResult();
                GUILayout.EndArea();
                return;
            }
            GUILayout.BeginArea(new Rect(16f, 16f, 540f, 440f), GUI.skin.box);
            GUILayout.Label("DESK 42 / TODAY'S DESK");
            if (_campaignState != null)
                GUILayout.Label("DAY " + _campaignState.CurrentShiftOrdinal +
                    " / " + _campaignState.CurrentShift.Title);
            GUILayout.Label("SHIFT: " +
                _simulationState.Shift.Phase.ToString().ToUpperInvariant());
            if (_campaignState != null &&
                _campaignState.Phase == OfficeCampaignPhase.ChooseUpgrade)
            {
                if (_campaignState.CurrentShiftOrdinal == 1)
                {
                    GUILayout.Label("THE OFFICE SURVIVED.");
                    GUILayout.Label("CHOOSE ONE CHANGE FOR TOMORROW.");
                }
                else
                {
                    GUILayout.Label("THE MACHINE NOW KNOWS TWO RULES.");
                    GUILayout.Label("CHOOSE WHAT THIS OFFICE BECOMES BETTER AT.");
                }
                GUILayout.Label("1 FAST TRAYS     2 CALM CHAIRS     3 RED LABELS");
            }
            else if (_campaignState != null &&
                _campaignState.Phase == OfficeCampaignPhase.ReadyForNextShift)
            {
                GUILayout.Label("OFFICE UPGRADE CHOSEN");
                GUILayout.Label("E / SPACE / A: NEXT SHIFT");
            }
            OfficeCustomerState customer =
                _simulationState.Customers.ActiveDeskCustomer;
            if (customer == null)
            {
                GUILayout.Label("NO CUSTOMER AT THE DESK");
            }
            else
            {
                GUILayout.Label("CUSTOMER: " + customer.DisplayName);
                GUILayout.Label(customer.Problem);
                GUILayout.Label("MOOD: " + customer.VisibleMoodState.ToString().ToUpperInvariant());
                OfficeCustomerPressureRecord pressure =
                    _simulationState.CustomerPressure.RecordFor(customer.CustomerId);
                GUILayout.Label("WHY: " + pressure.LastAuthoredCause);
                GUILayout.Label("FOLDER: " + FolderStatus(customer.LinkedAutomationClaimId));
                OfficeCaseWorkDefinition activeWork =
                    _simulationState.WorkDefinitionFor(
                        customer.LinkedAutomationClaimId);
                if (!string.IsNullOrWhiteSpace(
                        activeWork?.PriorObservableRecord))
                    GUILayout.Label(activeWork.PriorObservableRecord);
            }
            GUILayout.Space(8f);
            DrawCurrentWork(customer);
            GUILayout.Space(8f);
            GUILayout.Label("E / SPACE / A: " + _simulationState.PrimaryActionLabel);
            GUILayout.Label("WASD / ARROWS / LEFT STICK: MOVE");
            GUILayout.Label("CHOICES: 1-4 / X-Y-LB-RB");
            GUILayout.Label("Q / B: PUT DOWN");
            GUILayout.Label("R / VIEW: AUTO SORTER " +
                (_simulationState.AutomationRule.Enabled ? "ON" :
                    _simulationState.AutomationRule.Unlocked ? "OFF" : "LOCKED"));
            if (_simulationState.AutomationRule.Unlocked)
                GUILayout.Label(OfficeAutomationRuleState.PlayerRule);
            if (_campaignState != null && _campaignState.CurrentShiftOrdinal >= 2)
            {
                GUILayout.Label("T / RIGHT STICK: PAY RULE " +
                    (_simulationState.PayrollRule.Enabled ? "ON" :
                        _simulationState.PayrollRule.Unlocked ? "OFF" : "LOCKED"));
                if (_simulationState.PayrollRule.Unlocked)
                    GUILayout.Label(OfficePayrollRuleState.PlayerRule);
            }
            GUILayout.Label("3 RUNNER     4 TALKER");
            for (int i = 0; i < _simulationState.Staff.Staff.Count; i++)
            {
                OfficeStaffState staff = _simulationState.Staff.Staff[i];
                GUILayout.Label(staff.DisplayName + ": " + staff.VisibleIntent);
            }
            if (_campaignState != null &&
                _campaignState.CurrentShiftOrdinal >= 3)
                GUILayout.Label("RUNNER TASK SOURCE: " +
                    _simulationState.Staff.RunnerTaskSourceId.ToUpperInvariant());
            if (_simulationState.CustomerPressure.CalmActive)
                GUILayout.Label("CALMING: " +
                    _simulationState.CustomerPressure.CalmRemainingTicks + " TICKS");
            if (_simulationState.BreakState.Active)
                GUILayout.Label(_simulationState.BreakState.Recovered
                    ? "OFFICE FIXED"
                    : "COPY ECHO: FIX MACHINE / CLEAR COPIES / FIND ORIGINAL");
            if (_simulationState.GhostClock.Active)
                GUILayout.Label("GHOST CLOCK: KEEP TOMAS CALM / STOP CLOCK / CLEAR SLIPS");
            if (_simulationState.MissingRoomAccess.Active)
                GUILayout.Label("MISSING ROOM OPEN: CLOSE DOOR OR FINISH IRIS'S CASE");
            if (_simulationState.PromotionCascade.Active)
            {
                GUILayout.Label("PROMOTION CASCADE: COPIER " +
                    (_simulationState.PromotionCascade.CopierActive ? "ON" : "OFF") +
                    " / STAMP " +
                    (_simulationState.PromotionCascade.SupervisorStampActive
                        ? "ON" : "REMOVED"));
                GUILayout.Label("STOP COPIER / REMOVE STAMP / CLEAR FORMS / " +
                    "CALM MARA / FIND AND RETURN ORIGINAL / REASSIGN RUNNER");
            }
            if (_simulationState.Shift.Failed)
                GUILayout.Label(_simulationState.Shift.FailureReason +
                    " / ENTER OR START TO RESTART");
            if (_simulationState.Shift.Phase == OfficeShiftPhase.Result)
                DrawCausalRecap();
            GUILayout.EndArea();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            GUILayout.BeginArea(new Rect(16f, 470f, 700f, 205f), GUI.skin.box);
            GUILayout.Label(_campaignState == null
                ? "M2 ENGINEERING EVIDENCE"
                : "M3 CAMPAIGN ENGINEERING EVIDENCE");
            if (_campaignState != null)
                GUILayout.Label("CAMPAIGN " + _campaignState.Phase +
                    " / " + _campaignState.Checksum);
            GUILayout.Label("TICK " + _simulationState.CurrentTick +
                " / CHECKSUM " + _simulationState.Checksum);
            GUILayout.Label("WARDEN " + _simulationState.Warden.Cell(_simulationState.Grid) +
                " / COMMANDS " + _simulationState.CommandLog.Commands.Count);
            GUILayout.Label("ROUTES " + (CriticalRoutesValid ? "VALID" : "INVALID"));
            GUILayout.Label("QUEUES " + QueueSummary());
            GUILayout.Label("STATUS " + _lastDebugMessage);
            GUILayout.Label("P PAUSE | N STEP | F5 SAVE LOG | F7 REPLAY");
            GUILayout.EndArea();
#endif
        }

        private void DrawCurrentWork(OfficeCustomerState customer)
        {
            if (_simulationState.ManualTasks.IsActive)
            {
                string caseId = _simulationState.ManualTasks.ActiveCaseId;
                OfficeCaseWorkDefinition work =
                    _simulationState.WorkDefinitionFor(caseId);
                if (_simulationState.ManualTasks.ActiveKind ==
                    OfficeManualTaskKind.Compare)
                {
                    GUILayout.Label("CHECK PAPERS");
                    GUILayout.Label("1 CUSTOMER NAME: " + work.CustomerNameOnPaper);
                    GUILayout.Label("2 PAYMENT DATE: " + work.PaymentDateOnPaper);
                    GUILayout.Label("3 ACCOUNT MARK: " + work.AccountMarkOnPaper);
                    GUILayout.Label("4 THE PAPERS MATCH");
                }
                else if (_simulationState.ManualTasks.ActiveKind ==
                    OfficeManualTaskKind.Trace)
                {
                    GUILayout.Label("TRACE MONEY");
                    GUILayout.Label("1 COMPANY > PAYMENT RECORD > CUSTOMER ACCOUNT");
                    GUILayout.Label("2 COMPANY > PAYMENT RECORD > HOLDING ACCOUNT");
                    GUILayout.Label("3 COPIED FILE > NO PAYMENT RECORD > NO ACCOUNT");
                }
                else
                {
                    GUILayout.Label("CHECK WEIRD STUFF");
                    GUILayout.Label("1 CHECK THE OFFICE MARK");
                    GUILayout.Label("2 CHECK THE CLOCK MARK");
                    GUILayout.Label("3 CHECK THE ACCESS MARK");
                    GUILayout.Label("4 CHECK THE COPIER MARK");
                }
                return;
            }

            if (_simulationState.RoomWork.HelpActive)
            {
                OfficeRoomWorkJobState job = _simulationState.RoomWork.Job(
                    _simulationState.RoomWork.HelpJobId);
                if (job != null)
                    GUILayout.Label("HELPING: " + job.RemainingTicks + " TICKS LEFT");
            }

            if (customer == null) return;
            OfficeCaseWorkRecord record = _simulationState.ManualTasks.RecordFor(
                customer.LinkedAutomationClaimId);
            if (record.CompareAttempts > 0)
                GUILayout.Label("PAPERS: " + record.CompareReason);
            if (record.TraceAttempts > 0)
            {
                GUILayout.Label("MONEY: " + record.TraceResult);
                if (!string.IsNullOrWhiteSpace(record.TracePathSummary))
                    GUILayout.Label(record.TracePathSummary);
            }
            if (record.WeirdAttempts > 0)
                GUILayout.Label("WEIRD: " + record.WeirdResult);
            OfficeFolderState folder = _simulationState.Queues.GetFolder(
                customer.LinkedAutomationClaimId);
            if (_simulationState.ManualTasks.IsCaseComplete(
                    customer.LinkedAutomationClaimId) &&
                folder != null && !folder.IsMoving &&
                folder.OwnerKind == OfficeFolderOwnerKind.RoomQueue &&
                folder.CurrentRoom == OfficeRoomId.FrontDesk)
            {
                GUILayout.Label("DECIDE");
                GUILayout.Label("1 HELP CUSTOMER     2 REJECT CASE");
            }
            OfficeDecisionRecord decision = _simulationState.Decisions.RecordFor(
                customer.LinkedAutomationClaimId);
            if (decision != null) GUILayout.Label("STAMP: " + decision.Stamp);
            else if (_simulationState.Decisions.LastRecord != null)
                GUILayout.Label("LAST STAMP: " +
                    _simulationState.Decisions.LastRecord.Stamp);
        }

        private string FolderStatus(string caseId)
        {
            OfficeFolderState folder = _simulationState.Queues.GetFolder(caseId);
            if (folder == null) return "NOT HERE";
            if (folder.OwnerKind == OfficeFolderOwnerKind.Warden) return "CARRIED";
            if (folder.IsMoving) return "ON THE WAY TO " + RoomLabel(folder.DestinationRoom);
            return "AT " + RoomLabel(folder.CurrentRoom);
        }

        private void DrawCausalRecap()
        {
            GUILayout.Space(6f);
            GUILayout.Label("WHAT HAPPENED");
            for (int i = 0; i < _simulationState.CausalEvents.Events.Count; i++)
                GUILayout.Label("→ " +
                    _simulationState.CausalEvents.Events[i].PlayerText);
        }

        private void DrawCampaignResult()
        {
            OfficeCampaignResult result = _campaignState?.Result;
            if (result == null) return;
            GUILayout.Space(6f);
            GUILayout.Label("WHAT HAPPENED?");
            for (int shiftIndex = 0;
                shiftIndex < _campaignState.CompletedShiftSummaries.Count;
                shiftIndex++)
            {
                OfficeCampaignShiftSummary shift =
                    _campaignState.CompletedShiftSummaries[shiftIndex];
                string line = shift.ObservableRecapLines.Count == 0
                    ? "THE OFFICE CLOSED WITHOUT A RECORDED FAILURE."
                    : shift.ObservableRecapLines[
                        shift.ObservableRecapLines.Count - 1];
                GUILayout.Label("DAY " + shift.ShiftOrdinal + " -> " + line);
            }
            GUILayout.Label("CUSTOMERS HELPED: " + result.CustomersHelped);
            GUILayout.Label("CUSTOMERS REJECTED: " + result.CustomersRejected);
            GUILayout.Label("RULES TAUGHT: " + result.RulesTaught);
            GUILayout.Label("RULE MATCHES: " + result.RuleMatches);
            GUILayout.Label("COPIES CLEARED: " + result.CopiesCleared);
            GUILayout.Label("OFFICE FAILURES RECOVERED: " +
                result.OfficeFailuresRecovered);
            GUILayout.Label("UPGRADES CHOSEN: " + result.UpgradesChosen);
            GUILayout.Label("AVERAGE WAIT: " + result.AverageWaitTicks + " TICKS");
            GUILayout.Label("MISROUTED FILES: " + result.MisroutedFiles);
            GUILayout.Label("KNOWN CUSTOMER FOLLOW-UPS: " +
                result.KnownCustomerFollowUps);
            GUILayout.Label(OfficeCampaignResult.NextDayTease);
        }

        private static bool HasArgument(string[] arguments, string expected)
        {
            for (int i = 0; i < arguments.Length; i++)
                if (string.Equals(arguments[i], expected, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        private static string ArgumentValue(string[] arguments, string key)
        {
            string prefix = key + "=";
            for (int i = 0; i < arguments.Length; i++)
            {
                if (string.Equals(arguments[i], key, StringComparison.OrdinalIgnoreCase) &&
                    i + 1 < arguments.Length)
                    return arguments[i + 1];
                if (arguments[i].StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return arguments[i].Substring(prefix.Length);
            }
            return string.Empty;
        }
    }
}
