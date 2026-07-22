// ============================================================
// DESK 42 — PLAYTEST HARNESS (NOT SHIPPED CODE)
//
// ShiftPlaythrough — playtest session 001, Phase 3 fallback path.
// Drives full shifts end-to-end the way a player would (approve/
// deny decisions, occasional card slams, dilemma choices) with a
// SEEDED policy so every run is reproducible, while recording
// every event on the RumorMill bus plus all console errors.
//
// This file is a TEST HARNESS for the playtest report:
//   * It is not gameplay code and must never ship.
//   * It changes no game state outside Play Mode.
//   * SaveSystem is redirected before Boot, so the developer's
//     real meta.json / run.json files are never opened or written.
//
// Artifacts (repo root, git-ignored by absence from Assets/):
//   PlaytestLogs/shift<N>_seed<SEED>.log   — full event log
//   PlaytestLogs/shift<N>_seed<SEED>_{start,mid,end}.png (best effort)
//
// Run from CLI:
//   Unity -projectPath . -runTests -testPlatform PlayMode
//         -testCategory PlaytestHarness -testResults results.xml
// ============================================================

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Desk42.Core;
using Desk42.Encounter;

namespace Desk42.Tests.PlayMode
{
    [TestFixture]
    [Category("PlaytestHarness")]
    [Timeout(900000)] // 15 min — UTF's 180 s default preempts the harness's own 600 s softlock guard
    public class ShiftPlaythrough
    {
        // ── Config ────────────────────────────────────────────

        private const float THINK_TIME_MIN   = 0.75f; // seconds before a decision
        private const float THINK_TIME_MAX   = 2.50f;
        private const float APPROVE_CHANCE   = 0.65f;
        private const float CARD_SLAM_CHANCE = 0.50f;
        private const float ETHICAL_CHANCE   = 0.50f;
        private const float TIME_SCALE       = 2f;     // recorded in the log
        private const float SHIFT_TIMEOUT_S  = 600f;   // realtime hard abort = softlock evidence

        private static string LogDir =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "PlaytestLogs"));

        // ── Save-data protection ──────────────────────────────

        private string _testSaveDirectory;

        [OneTimeSetUp]
        public void IsolateSaves()
        {
            _testSaveDirectory = Path.Combine(
                Path.GetTempPath(), $"Desk42_Playthrough_{Guid.NewGuid():N}");
            SaveSystem.SetSaveDirectoryOverrideForTests(_testSaveDirectory);
        }

        [OneTimeTearDown]
        public void ReleaseIsolatedSaves()
        {
            Time.timeScale = 1f;
            UnsubscribeAll();
            Application.logMessageReceived -= CaptureConsole;

            if (GameManager.Instance != null)
                UnityEngine.Object.DestroyImmediate(GameManager.Instance.gameObject);

            SaveSystem.WipeAllSaveData();
            SaveSystem.ClearSaveDirectoryOverrideForTests();
            if (Directory.Exists(_testSaveDirectory))
                Directory.Delete(_testSaveDirectory, recursive: true);
        }

        // ── The four shifts ───────────────────────────────────
        // Shift numbers picked to cover the loop's difficulty band:
        // 1 (onboarding), 2 (baseline 3/4), 3 (quota scaling kicks),
        // 5 (Unpaid Overtime loop threshold — ForceEscalate path).

        [UnityTest] public IEnumerator Shift1_Seed421001() => RunShift(1, 421001);
        [UnityTest] public IEnumerator Shift2_Seed421002() => RunShift(2, 421002);
        [UnityTest] public IEnumerator Shift3_Seed421003() => RunShift(3, 421003);
        [UnityTest] public IEnumerator Shift5_Seed421005() => RunShift(5, 421005);

        // ── Driver ────────────────────────────────────────────

        private System.Random _policy;          // harness decision RNG (separate from SeedEngine)
        private StringBuilder _log;
        private float         _shiftStartRealtime;
        private bool          _runCompleted;
        private int           _claimsResolved;
        private int           _errors;
        private readonly List<(EventInfo evt, Delegate del)> _subs = new();
        private Action<ClaimResolvedEvent> _claimResolvedHandler;
        private Action<RunCompletedEvent> _runCompletedHandler;
        private Action<DilemmaTriggeredEvent> _dilemmaHandler;
        private DilemmaTriggeredEvent? _pendingDilemma;
        private bool _pendingDilemmaEthical;

        private IEnumerator RunShift(int shiftNumber, int seed)
        {
            Directory.CreateDirectory(LogDir);
            _log           = new StringBuilder();
            _policy        = new System.Random(seed);
            _runCompleted  = false;
            _claimsResolved = 0;
            _errors        = 0;
            _pendingDilemma = null;

            Application.logMessageReceived += CaptureConsole;
            Line($"=== HARNESS SHIFT {shiftNumber} SEED {seed} timeScale={TIME_SCALE} {DateTime.Now:O} ===");

            // 1. Boot the game exactly as a player launch would.
            //    GameManager is DontDestroyOnLoad — it survives across the
            //    UnityTests in one session, so only boot on the first shift.
            float t0 = Time.realtimeSinceStartup;
            if (GameManager.Instance == null)
            {
                SceneManager.LoadScene("Boot");
                while (GameManager.Instance == null ||
                       SceneManager.GetActiveScene().name != "MainMenu")
                {
                    if (Time.realtimeSinceStartup - t0 > 60f)
                    { Fail("BOOT TIMEOUT: never reached MainMenu"); yield break; }
                    yield return null;
                }
            }

            // DEFECT PROBE: GameManager.Phase NREs when no run is active
            // (RunStateController.ArchetypeId dereferences null _data).
            // Recorded as evidence, not fixed here.
            string phaseStr;
            try { phaseStr = GameManager.Phase.ToString(); }
            catch (Exception ex) { phaseStr = $"<DEFECT: {ex.GetType().Name} reading Phase pre-run>"; }
            Line($"[boot] Ready. GameManager.Phase={phaseStr}");

            // 2. Wire the universal event logger BEFORE the run starts.
            UnsubscribeAll();
            SubscribeAll();
            _claimResolvedHandler = _ => _claimsResolved++;
            _runCompletedHandler = _ => _runCompleted = true;
            _dilemmaHandler = AutoResolveDilemma;
            RumorMill.OnClaimResolved += _claimResolvedHandler;
            RumorMill.OnRunCompleted += _runCompletedHandler;
            RumorMill.OnDilemmaTriggered += _dilemmaHandler;

            // 3. Fixed-seed run, fixed shift number (bypasses GlobalShiftNumber
            //    so the player's meta progression is not consumed).
            var gm = GameManager.Instance;
            var fixtureMeta = new MetaProgressData
            {
                TutorialCompleted = true,
                HighestPhaseReached = 4,
                GlobalShiftNumber = shiftNumber,
            };
            gm.SetMetaForTesting(fixtureMeta);
            gm.Run.BeginNewRun(seed, "auditor", shiftNumber, fixtureMeta);
            gm.LoadScene(SceneID.Shift);

            t0 = Time.realtimeSinceStartup;
            while (SceneManager.GetActiveScene().name != "Shift")
            {
                if (Time.realtimeSinceStartup - t0 > 60f)
                { Fail("LOAD TIMEOUT: Shift scene never activated"); yield break; }
                yield return null;
            }

            Time.timeScale = TIME_SCALE;
            _shiftStartRealtime = Time.realtimeSinceStartup;
            Line($"[shift] Scene active. Sanity={gm.Run.Sanity:F1}");
            yield return Screenshot(shiftNumber, seed, "start");
            bool midShotTaken = false;
            bool predictionShotsTaken = false;

            // 4. Play until the run completes or the hard timeout hits.
            // DEFECT (evidence in report): on Shift scene reload the new scene's
            // Awake registers services, then the OLD scene's OnDestroy unregisters
            // them — wiping the fresh registration. Re-fetch every iteration and
            // fall back to FindObjectOfType so the harness survives the registry bug.
            EncounterManager Encounters()
            {
                var e = Desk42Services.Get<EncounterManager>();
                if (e == null) e = UnityEngine.Object.FindObjectOfType<EncounterManager>();
                return e;
            }

            while (!_runCompleted)
            {
                if (_pendingDilemma.HasValue)
                {
                    ResolvePendingDilemma();
                    yield return null;
                    continue;
                }

                var encounters = Encounters();
                if (encounters == null) { yield return null; continue; }
                if (Time.realtimeSinceStartup - _shiftStartRealtime > SHIFT_TIMEOUT_S)
                {
                    Line($"[FAIL] SHIFT TIMEOUT after {SHIFT_TIMEOUT_S}s realtime — possible softlock. " +
                         $"Claims resolved so far: {_claimsResolved}. Phase: {gm.Run?.RawData?.CurrentPhase}.");
                    break;
                }

                if (!midShotTaken &&
                    gm.Run?.RawData?.CurrentPhase == ShiftPhase.LunchBreak)
                {
                    yield return Screenshot(shiftNumber, seed, "mid");
                    midShotTaken = true;
                }

                if (encounters.ActiveClient != null)
                {
                    if (shiftNumber == 1 && !predictionShotsTaken)
                    {
                        yield return CapturePredictionPreviews(shiftNumber, seed);
                        predictionShotsTaken = true;
                    }

                    // Think, optionally slam one card, then decide.
                    yield return Wait(NextFloat(THINK_TIME_MIN, THINK_TIME_MAX));
                    if (encounters.ActiveClient == null) continue; // resolved elsewhere (fugue etc.)

                    yield return TrySlamCard();
                    if (encounters.ActiveClient == null) continue;

                    bool approve = _policy.NextDouble() < APPROVE_CHANCE;
                    Line($"[input] {(approve ? "APPROVE" : "DENY")} " +
                         $"(sanity={gm.Run.Sanity:F1}, phase={gm.Run.RawData.CurrentPhase})");
                    if (approve) encounters.Approve(); else encounters.Deny();
                }
                yield return null;
            }

            // 5. Wrap up + artifacts.
            Time.timeScale = 1f;
            yield return Screenshot(shiftNumber, seed, "end");
            var data = gm.Run?.RawData;
            Line($"=== SHIFT END === completed={_runCompleted} " +
                 $"claims={_claimsResolved} sanity={data?.Sanity:F1} soul={data?.SoulIntegrity:F1} " +
                 $"credits={data?.CorporateCredits} debt={data?.PersonalExpenseDebt} " +
                 $"errors={_errors} realtime={(Time.realtimeSinceStartup - _shiftStartRealtime):F0}s");

            UnsubscribeAll();
            Application.logMessageReceived -= CaptureConsole;
            File.WriteAllText(Path.Combine(LogDir, $"shift{shiftNumber}_seed{seed}.log"),
                              _log.ToString());

            Assert.IsTrue(_runCompleted,
                $"Shift {shiftNumber} (seed {seed}) did not complete — see log.");
        }

        // ── Player-input simulation ───────────────────────────

        private IEnumerator CapturePredictionPreviews(int shiftNumber, int seed)
        {
            var cardButtons = UnityEngine.Object.FindObjectsOfType<UI.CardButtonView>();
            Assert.IsNotEmpty(cardButtons,
                "A rendered hand is required to verify at-rest card foresight.");
            foreach (var renderedCard in cardButtons)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(
                        renderedCard.RenderedEffectText),
                    "Every card face must name its projected effect without hover.");
                Assert.IsFalse(string.IsNullOrWhiteSpace(
                        renderedCard.RenderedCertaintyText),
                    "Every card face must state certainty or availability without hover.");
            }
            yield return Screenshot(shiftNumber, seed, "card-face");

            var cardButton = cardButtons[0];
            Assert.IsNotNull(cardButton,
                "A rendered hand card is required to verify the card preview.");
            cardButton.OnPointerEnter(null);
            yield return Screenshot(shiftNumber, seed, "card-preview");
            cardButton.OnPointerExit(null);

            var approveObject = GameObject.Find("ApproveBtn");
            var decisionTrigger = approveObject != null
                ? approveObject.GetComponent<UI.ClaimDecisionPreviewTrigger>()
                : null;
            Assert.IsNotNull(decisionTrigger,
                "ApproveBtn must receive its runtime decision-preview trigger.");
            decisionTrigger.OnPointerEnter(null);
            yield return Screenshot(shiftNumber, seed, "decision-preview");
            decisionTrigger.OnPointerExit(null);
        }

        private IEnumerator TrySlamCard()
        {
            if (_policy.NextDouble() >= CARD_SLAM_CHANCE) yield break;

            var run  = GameManager.Instance?.Run;
            var hand = run?.Hand;
            var machine = Desk42Services.Get<RedTape.PunchCardMachine>();
            if (machine == null) // Unity fake-null aware (?? would miss destroyed objects)
                machine = UnityEngine.Object.FindObjectOfType<RedTape.PunchCardMachine>();
            if (hand == null || hand.Count == 0 || machine == null) yield break;

            var card = hand.Cards[_policy.Next(hand.Count)];
            Line($"[input] SLAM card={card.Data?.name} id={card.InstanceId}");
            bool resolved = false;
            Action<RedTape.SlamResult> onResolved = result =>
            {
                resolved = true;
                Line($"[input] SLAM resolved={result}");
            };
            machine.OnSlamResolved += onResolved;
            bool slamFailed = false;
            try
            {
                machine.SlamCard(card.Data, card.InstanceId);
            }
            catch (Exception ex)
            {
                Line($"[harness-warn] card slam failed: {ex.Message}");
                machine.OnSlamResolved -= onResolved;
                slamFailed = true;
            }
            if (slamFailed) yield break;

            float started = Time.realtimeSinceStartup;
            while (!resolved && machine != null &&
                   Time.realtimeSinceStartup - started < 5f)
                yield return null;

            if (!resolved)
                Line("[harness-warn] card slam did not resolve before timeout");
            if (machine != null)
                machine.OnSlamResolved -= onResolved;
        }

        private void AutoResolveDilemma(DilemmaTriggeredEvent e)
        {
            bool ethical = _policy.NextDouble() < ETHICAL_CHANCE;
            Line($"[input] DILEMMA -> {(ethical ? "ETHICAL" : "BUREAUCRATIC")}: {Truncate(DumpEvent(e), 200)}");
            _pendingDilemma = e;
            _pendingDilemmaEthical = ethical;
        }

        private void ResolvePendingDilemma()
        {
            if (!_pendingDilemma.HasValue) return;

            var dilemma = _pendingDilemma.Value;
            bool ethical = _pendingDilemmaEthical;
            _pendingDilemma = null;

            try
            {
                // Exercise the real modal button path so it closes the panel
                // and restores time before invoking the consequence callback.
                var panel = UnityEngine.Object.FindObjectOfType<UI.MoralDilemmaPanel>();
                string methodName = ethical ? "OnEthicalClicked" : "OnBureaucraticClicked";
                var method = panel?.GetType().GetMethod(methodName,
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (method != null)
                    method.Invoke(panel, null);
                else if (ethical)
                    dilemma.OnEthical?.Invoke();
                else
                    dilemma.OnBureaucratic?.Invoke(false);

                Time.timeScale = TIME_SCALE;
            }
            catch (Exception ex)
            {
                Line($"[harness-warn] dilemma resolve failed: {ex.Message}");
                Time.timeScale = TIME_SCALE;
            }
        }

        // ── Universal RumorMill logger (reflection) ───────────

        private void SubscribeAll()
        {
            foreach (var evt in typeof(RumorMill).GetEvents(BindingFlags.Public | BindingFlags.Static))
            {
                var payloadType = evt.EventHandlerType.GetGenericArguments()[0];
                var method = typeof(ShiftPlaythrough)
                    .GetMethod(nameof(OnAnyEvent), BindingFlags.NonPublic | BindingFlags.Instance)
                    .MakeGenericMethod(payloadType);
                var del = Delegate.CreateDelegate(evt.EventHandlerType, this, method);
                evt.AddEventHandler(null, del);
                _subs.Add((evt, del));
            }
            Line($"[harness] subscribed to {_subs.Count} RumorMill events");
        }

        private void UnsubscribeAll()
        {
            foreach (var (evt, del) in _subs) evt.RemoveEventHandler(null, del);
            _subs.Clear();

            if (_claimResolvedHandler != null)
                RumorMill.OnClaimResolved -= _claimResolvedHandler;
            if (_runCompletedHandler != null)
                RumorMill.OnRunCompleted -= _runCompletedHandler;
            if (_dilemmaHandler != null)
                RumorMill.OnDilemmaTriggered -= _dilemmaHandler;

            _claimResolvedHandler = null;
            _runCompletedHandler = null;
            _dilemmaHandler = null;
        }

        private void OnAnyEvent<T>(T e)
            => Line($"[evt {typeof(T).Name}] {DumpEvent(e)}");

        private static string DumpEvent(object e)
        {
            if (e == null) return "(null)";
            var t  = e.GetType();
            var sb = new StringBuilder();
            foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Instance))
                sb.Append(f.Name).Append('=').Append(Fmt(f.GetValue(e))).Append(' ');
            foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (p.GetIndexParameters().Length > 0) continue;
                object v; try { v = p.GetValue(e); } catch { v = "<err>"; }
                sb.Append(p.Name).Append('=').Append(Fmt(v)).Append(' ');
            }
            return sb.ToString().TrimEnd();
        }

        private static string Fmt(object v) => v switch
        {
            null           => "null",
            float f        => f.ToString("F2"),
            Delegate       => "<callback>",
            _              => Truncate(v.ToString(), 120),
        };

        // ── Console capture ───────────────────────────────────

        private void CaptureConsole(string condition, string stackTrace, LogType type)
        {
            if (type is LogType.Error or LogType.Exception or LogType.Assert)
            {
                _errors++;
                string head = (stackTrace ?? "").Split('\n').FirstOrDefault() ?? "";
                Line($"[CONSOLE-{type}] {condition} | {head}");
            }
            else if (type == LogType.Warning)
            {
                Line($"[console-warn] {Truncate(condition, 160)}");
            }
        }

        // ── Utilities ─────────────────────────────────────────

        private void Line(string s)
        {
            _log.AppendLine($"{Time.realtimeSinceStartup - _shiftStartRealtime,8:F2}s | {s}");
            Debug.Log($"[HARNESS] {s}");
        }

        private void Fail(string why)
        {
            Line($"[FAIL] {why}");
            File.WriteAllText(Path.Combine(LogDir, $"failed_{DateTime.Now:HHmmss}.log"), _log.ToString());
            UnsubscribeAll();
            Application.logMessageReceived -= CaptureConsole;
            Time.timeScale = 1f;
            Assert.Fail(why);
        }

        private float NextFloat(float min, float max)
            => min + (float)_policy.NextDouble() * (max - min);

        private static IEnumerator Wait(float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end) yield return null;
        }

        private IEnumerator Screenshot(int shiftNumber, int seed, string tag)
        {
            yield return new WaitForEndOfFrame();

            string path = Path.Combine(
                LogDir, $"shift{shiftNumber}_seed{seed}_{tag}.png");
            try
            {
                if (File.Exists(path))
                    File.Delete(path);

                ScreenCapture.CaptureScreenshot(path);
                Line($"[shot] {tag}");
            }
            catch (Exception ex)
            {
                Line($"[harness-warn] screenshot failed: {ex.Message}");
                yield break;
            }

            float deadline = Time.realtimeSinceStartup + 10f;
            while ((!File.Exists(path) || new FileInfo(path).Length == 0)
                   && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            if (!File.Exists(path) || new FileInfo(path).Length == 0)
                Line($"[harness-warn] screenshot did not finish writing: {path}");
        }

        private static string Truncate(string s, int n)
            => string.IsNullOrEmpty(s) || s.Length <= n ? s : s.Substring(0, n) + "…";
    }
}
