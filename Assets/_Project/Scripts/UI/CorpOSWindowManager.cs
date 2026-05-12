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
            if (e.TriggeredFugue)
            {
                StartCoroutine(TriggerBSOD());
            }
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

            Debug.Log("[CorpOS] KERNEL PANIC. Triggering BSOD.");
            
            if (_bsodText != null)
            {
                _bsodText.text = "A FATAL EXCEPTION 0E HAS OCCURRED AT MEMORY ADDRESS 0028:C0011E36.\n" +
                                 "THE CURRENT APPLICATION WILL BE TERMINATED.\n\n" +
                                 "* PRESS ANY KEY TO CONTINUE IGNORING THIS.\n" +
                                 "* PRESS CTRL+ALT+DEL TO LOSE MORE TIME.\n\n" +
                                 "YOUR PERFORMANCE REMAINS INADEQUATE.";
            }

            _bsodOverlay.alpha = 1f;
            
            // Freeze fake OS
            yield return new WaitForSeconds(3f);
            
            // Glitch out
            float t = 0;
            while (t < 1f)
            {
                t += Time.deltaTime * 2f;
                // Client-side aesthetic effect; using UnityEngine.Random here instead of SeedEngine is fine 
                // for visual stuttering, but using SeedEngine aligns with strict rules.
                _bsodOverlay.alpha = SeedEngine.NextFloat(SeedStream.RumorMillEvents) > 0.5f ? 1f : 0f;
                yield return null;
            }

            _bsodOverlay.alpha = 0f;
        }
    }
}
