// ============================================================
// DESK 42 — CardButton Prefab Rebuilder
//
// Rebuilds the CardButton prefab with a sane internal layout.
// The original prefab has labels anchored at (473, 296) which
// is way outside the 100x100 card bounds — that's why cards
// render as blank white squares.
//
// Usage:
//   Tools → Desk 42 → Rebuild CardButton Prefab
//
// Replaces the existing prefab in place. Existing scene
// references to the prefab survive.
// ============================================================

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Desk42.UI;

namespace Desk42.EditorTools
{
    public static class CardButtonPrefabRebuilder
    {
        [MenuItem("Tools/Desk 42/Rebuild CardButton Prefab")]
        public static void Rebuild()
        {
            string[] guids = AssetDatabase.FindAssets("CardButton t:Prefab");
            string path = null;
            foreach (var guid in guids)
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                if (p.EndsWith("CardButton.prefab")) { path = p; break; }
            }

            if (path == null)
            {
                EditorUtility.DisplayDialog("Rebuild CardButton",
                    "CardButton.prefab not found in Assets.", "OK");
                return;
            }

            var prefab = PrefabUtility.LoadPrefabContents(path);

            // Wipe all children
            for (int i = prefab.transform.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(prefab.transform.GetChild(i).gameObject);

            // Strip any extra components except essentials we'll re-add
            foreach (var c in prefab.GetComponents<Component>())
            {
                if (c is Transform || c is RectTransform) continue;
                Object.DestroyImmediate(c);
            }

            // Configure root RectTransform
            var rt = prefab.GetComponent<RectTransform>();
            if (rt == null) rt = prefab.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(140, 200);

            // Background image
            var bg = prefab.AddComponent<Image>();
            bg.color = new Color(0.95f, 0.93f, 0.85f, 1f); // warm paper tone

            // Button
            var btn = prefab.AddComponent<Button>();
            var btnColors = btn.colors;
            btnColors.normalColor      = new Color(1f, 1f, 1f);
            btnColors.highlightedColor = new Color(1f, 0.95f, 0.75f);
            btnColors.pressedColor     = new Color(0.85f, 0.80f, 0.60f);
            btnColors.disabledColor    = new Color(0.55f, 0.55f, 0.50f);
            btn.colors = btnColors;
            btn.targetGraphic = bg;

            // CardButtonView component
            var view = prefab.AddComponent<CardButtonView>();

            // Children: NameLabel, TypeLabel, CostLabel, FatigueLabel
            var nameLabel    = CreateChildLabel(prefab, "NameLabel",    new Color(0.1f, 0.1f, 0.1f),
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1),
                new Vector2(0, -10), new Vector2(-12, 50), 16, FontStyles.Bold,
                TextAlignmentOptions.Top);

            var typeLabel    = CreateChildLabel(prefab, "TypeLabel",    new Color(0.35f, 0.35f, 0.4f),
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1),
                new Vector2(0, -65), new Vector2(-12, 22), 12, FontStyles.Italic,
                TextAlignmentOptions.Top);

            var costLabel    = CreateChildLabel(prefab, "CostLabel",    new Color(0.55f, 0.45f, 0.10f),
                new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0),
                new Vector2(0, 12), new Vector2(-12, 28), 18, FontStyles.Bold,
                TextAlignmentOptions.Center);

            var fatigueLabel = CreateChildLabel(prefab, "FatigueLabel", new Color(0.75f, 0.30f, 0.20f),
                new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0),
                new Vector2(0, 44), new Vector2(-12, 22), 12, FontStyles.Bold,
                TextAlignmentOptions.Center);

            // Wire CardButtonView's serialized fields
            var so = new SerializedObject(view);
            SetRef(so, "_nameLabel",    nameLabel);
            SetRef(so, "_typeLabel",    typeLabel);
            SetRef(so, "_costLabel",    costLabel);
            SetRef(so, "_fatigueLabel", fatigueLabel);
            SetRef(so, "_button",       btn);
            SetRef(so, "_background",   bg);
            so.ApplyModifiedPropertiesWithoutUndo();

            // Save
            PrefabUtility.SaveAsPrefabAsset(prefab, path);
            PrefabUtility.UnloadPrefabContents(prefab);
            AssetDatabase.SaveAssets();

            Debug.Log($"[CardButtonRebuilder] Prefab rebuilt at {path}.");
            EditorUtility.DisplayDialog("Rebuild CardButton",
                "Prefab rebuilt. The cards in your hand should now show name/type/cost.\n\n" +
                "If a hand is currently rendered in the Shift scene, " +
                "Stop Play mode and Press Play again from Boot.",
                "OK");
        }

        private static TMP_Text CreateChildLabel(GameObject parent, string name,
            Color color,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 anchoredPos, Vector2 sizeDelta,
            float fontSize, FontStyles fontStyle,
            TextAlignmentOptions alignment)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);

            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot     = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text      = name; // placeholder, replaced at runtime
            tmp.alignment = alignment;
            tmp.fontSize  = fontSize;
            tmp.color     = color;
            tmp.fontStyle = fontStyle;
            tmp.enableWordWrapping = true;
            tmp.overflowMode = TextOverflowModes.Truncate;
            tmp.raycastTarget = false; // don't block button clicks

            return tmp;
        }

        private static void SetRef(SerializedObject so, string fieldName, Object obj)
        {
            var p = so.FindProperty(fieldName);
            if (p != null) p.objectReferenceValue = obj;
        }
    }
}
#endif
