// ============================================================
// DESK 42 — Game Manager
//
// The root singleton that bootstraps all global systems and
// owns the scene-flow state machine.
//
// Scene names (match your Build Settings exactly):
//   Boot          — loads meta, shows logo, transitions to MainMenu
//   MainMenu      — new game / continue / settings / quit
//   Shift         — core gameplay desk scene
//   InternalAudit — between-run meta-hub
//
// GameManager lives in the Boot scene and uses
// DontDestroyOnLoad. All other managers are children of this
// GameObject. Access them via GameManager.Instance.*
//
// Usage:
//   GameManager.Instance.StartNewRun();
//   GameManager.Instance.Run.ModifySanity(-10f);
//   GameManager.Instance.LoadScene(SceneID.InternalAudit);
// ============================================================

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Desk42.Cards;
using Desk42.OfficeSupplies;
using Desk42.MoralInjury;

namespace Desk42.Core
{
    public enum SceneID
    {
        Boot,
        MainMenu,
        Shift,
        InternalAudit,
    }

    [DisallowMultipleComponent]
    public sealed class GameManager : MonoBehaviour
    {
        // ── Singleton ─────────────────────────────────────────

        public static GameManager Instance { get; private set; }

        // ── Child System References ───────────────────────────
        // These are MonoBehaviours on child GameObjects of this GO.
        // They are created and attached in Awake if not already present.

        public RunStateController  Run      { get; private set; }
        public RumorMillDriver     EventBus { get; private set; }
        public OfficeSupplyManager Supplies { get; private set; }

        // Card library — assign in Inspector (Boot scene GameManager prefab)
        [SerializeField] private CardLibrary _cardLibrary;
        public CardLibrary Cards => _cardLibrary;

        // Loaded from disk in Boot and held for the session
        public MetaProgressData Meta { get; private set; }

        // ── Scene Flow State ──────────────────────────────────

        private SceneID _currentScene = SceneID.Boot;
        private bool    _isTransitioning;

        // ── Scene Name Mapping ────────────────────────────────
        // Keep in sync with Unity Build Settings scene names.

        private static readonly string[] _sceneNames =
        {
            "Boot",
            "MainMenu",
            "Shift",
            "InternalAudit",
        };

        // ── Unity Lifecycle ───────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                // A second GameManager was loaded — destroy the duplicate.
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            EnsureChildSystems();
            BootSequence();
        }

        private void OnApplicationQuit()
        {
            // Auto-save mid-run state on quit
            if (Run != null && !Run.RawData?.IsComplete == true)
                Run.AutoSave();

            SaveSystem.SaveMeta(Meta);
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                Run?.AutoSave();
                SaveSystem.SaveMeta(Meta);
            }
        }

        // ── Initialisation ────────────────────────────────────

        private void EnsureChildSystems()
        {
            // RumorMill driver — one per session, drains the deferred queue
            EventBus = GetComponentInChildren<RumorMillDriver>();
            if (EventBus == null)
            {
                var go = new GameObject("RumorMillDriver");
                go.transform.SetParent(transform);
                EventBus = go.AddComponent<RumorMillDriver>();
            }

            // RunStateController — created fresh when a run begins
            Run = GetComponentInChildren<RunStateController>();
            if (Run == null)
            {
                var go = new GameObject("RunStateController");
                go.transform.SetParent(transform);
                Run = go.AddComponent<RunStateController>();
            }

            // OfficeSupplyManager — manages active desk supplies
            Supplies = GetComponentInChildren<OfficeSupplyManager>();
            if (Supplies == null)
            {
                var go = new GameObject("OfficeSupplyManager");
                go.transform.SetParent(transform);
                Supplies = go.AddComponent<OfficeSupplyManager>();
            }
            Supplies.Initialize(() => Run?.BuildSupplyContext());
        }

        private void BootSequence()
        {
            Meta = SaveSystem.LoadMeta();
            Meta = SaveSystem.MigrateMetaIfNeeded(Meta);

            Debug.Log($"[GameManager] Boot complete. " +
                      $"Global shift: {Meta.GlobalShiftNumber}. " +
                      $"Active run: {SaveSystem.HasActiveRun()}.");

            // Boot scene can show a logo/splash — then auto-advance to MainMenu.
            StartCoroutine(BootToMainMenu());
        }

        private IEnumerator BootToMainMenu()
        {
            // Give one frame for all Awake() calls to finish
            yield return null;

            // TODO: Show studio logo here (yield return a Coroutine for logo duration)
            // For now, go straight to MainMenu.
            LoadScene(SceneID.MainMenu);
        }

        // ── Public Scene-Flow API ─────────────────────────────

        /// <summary>Start a new run from the MainMenu.</summary>
        public void StartNewRun(string archetypeId = null)
        {
            if (_isTransitioning) return;

            Meta.GlobalShiftNumber++;
            SaveSystem.SaveMeta(Meta);

            int seed = new System.Random().Next();
            Run.BeginNewRun(seed, archetypeId ?? "auditor",
                Meta.GlobalShiftNumber, Meta);

            LoadScene(SceneID.Shift);
        }

        /// <summary>Start a new run with a specific seed (from share code).</summary>
        public void StartSeededRun(string seedCode, string archetypeId = null)
        {
            if (_isTransitioning) return;

            if (!SeedEngine.TryParseSeedCode(seedCode, out int seed))
            {
                Debug.LogError($"[GameManager] Invalid seed code: {seedCode}");
                return;
            }

            Meta.GlobalShiftNumber++;
            SaveSystem.SaveMeta(Meta);

            Run.BeginNewRun(seed, archetypeId ?? "auditor",
                Meta.GlobalShiftNumber, Meta);

            LoadScene(SceneID.Shift);
        }

        /// <summary>Continue a mid-run that was saved to disk.</summary>
        public void ContinueRun()
        {
            if (_isTransitioning) return;

            var savedRun = SaveSystem.LoadRun();
            if (savedRun == null || savedRun.IsComplete)
            {
                Debug.LogWarning("[GameManager] No active run to continue.");
                return;
            }

            Run.ResumeRun(savedRun, Meta);
            LoadScene(SceneID.Shift);
        }

        /// <summary>End the current shift and transition to Internal Audit.</summary>
        public void EndShift()
        {
            if (_isTransitioning || Run == null) return;

            // Update Repeat Offender DB and meta-progress from the completed run
            CommitRunResultsToMeta();

            Run.CompleteRun();
            SaveSystem.SaveMeta(Meta);

            LoadScene(SceneID.InternalAudit);
        }

        /// <summary>Return to the desk from Internal Audit.</summary>
        public void StartNextShift(string archetypeId = null)
        {
            if (_isTransitioning) return;

            Meta.GlobalShiftNumber++;
            SaveSystem.SaveMeta(Meta);

            int seed = new System.Random().Next();
            Run.BeginNewRun(seed, archetypeId ?? Run.ArchetypeId,
                Meta.GlobalShiftNumber, Meta);

            LoadScene(SceneID.Shift);
        }

        /// <summary>Return to main menu (abandons current run if any).</summary>
        public void ReturnToMainMenu(bool saveRunFirst = true)
        {
            if (_isTransitioning) return;

            if (saveRunFirst) Run?.AutoSave();
            SaveSystem.SaveMeta(Meta);

            LoadScene(SceneID.MainMenu);
        }

        // ── Scene Loading ─────────────────────────────────────

        public void LoadScene(SceneID scene)
        {
            if (_isTransitioning)
            {
                Debug.LogWarning($"[GameManager] Already transitioning, ignoring LoadScene({scene}).");
                return;
            }

            StartCoroutine(LoadSceneAsync(scene));
        }

        private IEnumerator LoadSceneAsync(SceneID scene)
        {
            _isTransitioning = true;
            _currentScene    = scene;

            // TODO: Play transition animation / fade out here

            var op = SceneManager.LoadSceneAsync(_sceneNames[(int)scene],
                LoadSceneMode.Single);

            op.allowSceneActivation = false;

            // Wait until load is 90% (Unity holds at 0.9 before activation)
            while (op.progress < 0.9f)
                yield return null;

            // TODO: Signal transition controller to fade in when ready
            op.allowSceneActivation = true;

            yield return op;

            _isTransitioning = false;

            Debug.Log($"[GameManager] Scene loaded: {scene}");
        }

        // ── Meta Commit ───────────────────────────────────────

        /// <summary>
        /// Transfer run-end statistics and Repeat Offender data from
        /// the completed RunData into the persistent MetaProgressData.
        /// </summary>
        private void CommitRunResultsToMeta()
        {
            if (Run?.RawData == null) return;

            var run = Run.RawData;

            // Lifetime stats
            Meta.LifetimeStats.TotalShiftsCompleted++;
            Meta.LifetimeStats.TotalClaimsProcessed    += run.Stats.ClaimsProcessed;
            Meta.LifetimeStats.TotalHumaneResolutions  += run.Stats.HumaneResolutions;
            Meta.LifetimeStats.TotalBureaucraticResolutions += run.Stats.BureaucraticResolutions;
            Meta.LifetimeStats.TotalNDAsSigned         += run.Stats.NDAsSignedThisRun;
            Meta.LifetimeStats.TotalCardSlams          += run.Stats.CardSlamsTotal;
            Meta.LifetimeStats.TotalFugueStatesSurvived += run.Stats.FugueStatesSurvived;

            if (run.Stats.LowestSanity < Meta.LifetimeStats.LowestSoulIntegrityReached)
                Meta.LifetimeStats.LowestSoulIntegrityReached = run.Stats.LowestSanity;

            // Bank: carry over unspent credits (partial — 50% of unspent carries over)
            int carryover = Mathf.RoundToInt(run.CorporateCredits * 0.5f);
            Meta.BankBalance += carryover;

            // Persist moral injury scars to meta
            Run?.MoralInjury?.PersistScarsToMeta(Meta);

            Debug.Log($"[GameManager] Run results committed. " +
                      $"Claims: {run.Stats.ClaimsProcessed}, " +
                      $"Credits carried over: {carryover}");
        }

        // ── Debug Console ─────────────────────────────────────

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void Update()
        {
            // Quick-access debug shortcuts
            if (Input.GetKeyDown(KeyCode.F1)) SaveSystem.LogSavePaths();
            if (Input.GetKeyDown(KeyCode.F9)) Run?.AutoSave();
        }
#endif
    }
}
