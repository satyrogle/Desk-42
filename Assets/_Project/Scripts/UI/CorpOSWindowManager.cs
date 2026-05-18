// ============================================================
// DESK 42 — CorpOS Window Manager (MonoBehaviour)
//
// Simulates a dying 90s operating system. As the shift progresses
// and Desk Entropy or Sanity drops, the OS starts producing
// passive-aggressive error popups, lagging the cursor, or 
// triggering visual glitches (BSOD).
// ============================================================

using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Desk42.Core;

namespace Desk42.UI
{
    public class CorpOSWindowManager : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("The parent transform for spawning popups.")]
        [SerializeField] private Transform _popupAnchor;
        
        [Tooltip("Prefab for a 90s-style generic error dialog.")]
        [SerializeField] private GameObject _errorPopupPrefab;
        
        [Tooltip("The Blue Screen of Death overlay.")]
        [SerializeField] private CanvasGroup _bsodOverlay;
        [SerializeField] private TMP_Text _bsodText;

        [Header("Config")]
        [SerializeField] private float _basePopupChance = 0.05f;

        private void Awake()
        {
            Desk42Services.Register(this);
        }

        private void OnDestroy()
        {
            Desk42Services.Unregister<CorpOSWindowManager>();
        }

        private void OnEnable()
        {
            RumorMill.OnClaimResolved += HandleClaimResolved;
            RumorMill.OnSanityChanged += HandleSanityChanged;
        }

        private void OnDisable()
        {
            RumorMill.OnClaimResolved -= HandleClaimResolved;
            RumorMill.OnSanityChanged -= HandleSanityChanged;
        }

        private void HandleClaimResolved(ClaimResolvedEvent e)
        {
            // Chance to spawn an error popup increases as sanity drops
            var run = GameManager.Instance?.Run;
            if (run == null) return;

            float sanityFactor = (100f - run.Sanity) / 100f; // 0 to 1
            float chance = _basePopupChance + (sanityFactor * 0.2f);
            
            if (SeedEngine.NextFloat(SeedStream.RumorMillEvents) < chance)
            {
                SpawnPassiveAggressiveError();
            }
        }

        private void HandleSanityChanged(SanityChangedEvent e)
        {
            // Fugue is a mid-run sanity event, not a win condition.
            // The BSOD is the Layer 4 finale — only the boss fight
            // system should call TriggerLayer4Ending() once the
            // player has actually beaten TaskMgr_Override.exe.
        }

        /// <summary>
        /// Layer 4 finale (Crash to Win). Call this ONLY from the
        /// boss fight win state. Plays the BSOD, fires the ending
        /// achievement + telemetry, persists HasEscapedTheLoop,
        /// and quits the application.
        /// </summary>
        public void TriggerLayer4Ending()
        {
            StartCoroutine(TriggerBSOD());
        }

        private void SpawnPassiveAggressiveError()
        {
            if (_errorPopupPrefab == null || _popupAnchor == null) return;

            var popup = Instantiate(_errorPopupPrefab, _popupAnchor);
            
            // Randomize position slightly to create desktop clutter
            var rt = popup.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchoredPosition = new Vector2(
                    SeedEngine.NextFloat(SeedStream.RumorMillEvents, -300f, 300f),
                    SeedEngine.NextFloat(SeedStream.RumorMillEvents, -200f, 200f)
                );
            }

            // Phase 3/4 override: Make it draggable
            if (popup.GetComponent<CanvasGroup>() == null)
                popup.AddComponent<CanvasGroup>();
            popup.AddComponent<DraggableWarningPopup>();

            // Clean up the fake popup automatically so it doesn't leak memory or kill UI performance forever
            Destroy(popup, 15f);

            // Play Windows 95 error sound equivalent if hooked
            Debug.Log("[CorpOS] Spawning passive-aggressive OS popup.");
        }

        private IEnumerator TriggerBSOD()
        {
            if (_bsodOverlay == null) yield break;

            Debug.Log("[CorpOS] EMPLOYEE HAS LEFT THE BUILDING. Triggering End Sequence.");

            // Telemetry: boss fight win signal (paired with
            // BossFightStarted, emitted from the boss fight system).
            Desk42.Meta.Analytics.Analytics.Track(
                Desk42.Meta.Analytics.TelemetryEvents.BossFightWon);

            if (_bsodText != null)
            {
                _bsodText.text = "EMPLOYEE HAS LEFT THE BUILDING.\n" +
                                 "PROMOTION TO MIDDLE MANAGEMENT: APPROVED.\n\n" +
                                 "* THE LOOP HAS BEEN BROKEN.\n" +
                                 "* RESTART TO INITIATE NEW PARADIGM.\n\n" +
                                 "YOUR PERFORMANCE IS FINALLY ADEQUATE.\n\n" +
                                 "PRESS F12 TO ARCHIVE THIS MEMO.";
            }

            _bsodOverlay.alpha = 1f;

            // 1. Fire achievement
            Desk42.Meta.Achievements.Achievements.Unlock(Desk42.Meta.Achievements.AchievementCatalog.EndingTaskFailedSuccessfully);

            // 2. Wait 2 frames so the Steam overlay has time to
            //    paint the unlock popup before we Quit.
            yield return null;
            yield return null;

            // 3. Final telemetry event for the crash-to-win funnel.
            Desk42.Meta.Analytics.Analytics.Track(
                Desk42.Meta.Analytics.TelemetryEvents.CrashToWinCompleted);

            // 4. Flush Analytics
            Desk42.Meta.Analytics.Analytics.Flush();

            // 5. Save Meta (HasEscapedTheLoop persists the win
            //    so MainMenu can show the "You're back?" state and
            //    unlock the Middle Manager archetype on next launch).
            var meta = GameManager.Instance?.Meta;
            if (meta != null)
            {
                meta.HasEscapedTheLoop = true;
                Desk42.Core.SaveSystem.SaveMeta(meta);
            }

            // 6. Hold the BSOD for 5 seconds so the player can
            //    read it (and grab the screenshot).
            yield return new WaitForSeconds(5f);

            // Glitch out
            float t = 0;
            while (t < 1f)
            {
                t += Time.deltaTime * 2f;
                _bsodOverlay.alpha = SeedEngine.NextFloat(SeedStream.RumorMillEvents) > 0.5f ? 1f : 0f;
                yield return null;
            }

            _bsodOverlay.alpha = 0f;

            // 7. Quit
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
