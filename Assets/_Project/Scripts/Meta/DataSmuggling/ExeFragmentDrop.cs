// ============================================================
// DESK 42 — Exe Fragment Drop (MonoBehaviour)
//
// Visual representation of a successfully extracted .exe
// fragment after a paradox recipe fires. A small floppy-disk
// icon spawns on the desk with a brief text bubble naming the
// recipe; clicking it removes the icon (the fragment is
// already banked in MetaProgressData).
//
// Self-clearing after 8s if the player doesn't click it.
// ============================================================

using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Desk42.Accessibility;

namespace Desk42.Meta.DataSmuggling
{
    [DisallowMultipleComponent]
    public sealed class ExeFragmentDrop : MonoBehaviour
    {
        private const float AUTO_DESPAWN = 8f;

        public static void SpawnOnDesk(ParadoxRecipe recipe)
        {
            // Find a canvas to attach to. Prefer the top-most
            // screen-space-overlay canvas in the scene.
            var canvas = FindAnyOverlayCanvas();
            if (canvas == null)
            {
                Debug.LogWarning("[ExeFragmentDrop] No overlay canvas found.");
                return;
            }

            var go = new GameObject($"ExeFragment_{recipe.Id}");
            go.transform.SetParent(canvas.transform, false);
            var drop = go.AddComponent<ExeFragmentDrop>();
            drop.Build(recipe);
        }

        private static Canvas FindAnyOverlayCanvas()
        {
            var all = FindObjectsOfType<Canvas>();
            foreach (var c in all)
                if (c.renderMode == RenderMode.ScreenSpaceOverlay && c.isActiveAndEnabled)
                    return c;
            return null;
        }

        private void Build(ParadoxRecipe recipe)
        {
            var rt = gameObject.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0);
            rt.anchorMax = new Vector2(0.5f, 0);
            rt.pivot     = new Vector2(0.5f, 0);
            rt.sizeDelta = new Vector2(360, 110);
            // Random horizontal jitter on the desk
            float x = Random.Range(-300f, 300f);
            rt.anchoredPosition = new Vector2(x, 240);

            var bg = gameObject.AddComponent<Image>();
            bg.color = new Color(0.10f, 0.10f, 0.14f, 0.95f);
            bg.raycastTarget = true;

            var btn = gameObject.AddComponent<Button>();
            btn.onClick.AddListener(() => Destroy(gameObject));

            // Floppy disk pseudo-icon (just a colored square)
            var iconGO = new GameObject("Disk");
            iconGO.transform.SetParent(transform, false);
            var irt = iconGO.AddComponent<RectTransform>();
            irt.anchorMin = new Vector2(0, 0.5f);
            irt.anchorMax = new Vector2(0, 0.5f);
            irt.pivot     = new Vector2(0, 0.5f);
            irt.sizeDelta = new Vector2(72, 72);
            irt.anchoredPosition = new Vector2(12, 0);
            iconGO.AddComponent<Image>().color = new Color(0.15f, 0.15f, 0.30f);

            var iconLbl = new GameObject("DiskLbl");
            iconLbl.transform.SetParent(iconGO.transform, false);
            var ilt = iconLbl.AddComponent<RectTransform>();
            ilt.anchorMin = Vector2.zero;
            ilt.anchorMax = Vector2.one;
            ilt.offsetMin = Vector2.zero;
            ilt.offsetMax = Vector2.zero;
            var iconTmp = iconLbl.AddComponent<TextMeshProUGUI>();
            iconTmp.text = ".EXE";
            iconTmp.fontSize = AccessibilitySettings.Scaled(18);
            iconTmp.fontStyle = FontStyles.Bold;
            iconTmp.alignment = TextAlignmentOptions.Center;
            iconTmp.color = new Color(0.65f, 0.80f, 1f);
            iconTmp.raycastTarget = false;

            // Text bubble
            var txtGO = new GameObject("Text");
            txtGO.transform.SetParent(transform, false);
            var trt = txtGO.AddComponent<RectTransform>();
            trt.anchorMin = new Vector2(0, 0);
            trt.anchorMax = new Vector2(1, 1);
            trt.offsetMin = new Vector2(96, 8);
            trt.offsetMax = new Vector2(-8, -8);
            var tmp = txtGO.AddComponent<TextMeshProUGUI>();
            tmp.text = $"<b>{recipe.DisplayName}</b>\n<size=80%>FRAGMENT EXTRACTED</size>";
            tmp.fontSize = AccessibilitySettings.Scaled(14);
            tmp.color = new Color(0.92f, 0.92f, 0.92f);
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.enableWordWrapping = true;
            tmp.raycastTarget = false;

            StartCoroutine(AutoDespawn());
        }

        private IEnumerator AutoDespawn()
        {
            yield return new WaitForSeconds(AUTO_DESPAWN);
            if (this != null && gameObject != null)
                Destroy(gameObject);
        }
    }
}
