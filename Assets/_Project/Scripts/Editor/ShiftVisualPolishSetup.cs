#if UNITY_EDITOR
using System;
using Desk42.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Desk42.Editor
{
    /// <summary>
    /// Repeatable placeholder-art pass for the core Shift screen. It keeps gameplay
    /// wiring intact while establishing the approved client-facing desk composition.
    /// </summary>
    public static class ShiftVisualPolishSetup
    {
        private const string ShiftScenePath = "Assets/_Project/Scenes/Shift.unity";
        private const string BackgroundPath =
            "Assets/_Project/Art/Concepts/VisualIdentity/Mockups/D42_Mockup_DeskStage_ClientFacing_v003.png";
        private const string CardPrefabPath = "Assets/_Project/Prefabs/CardButton.prefab";

        private static readonly Color DeepGreen = Hex("#123A31");
        private static readonly Color DeepGreenDark = Hex("#091F1B");
        private static readonly Color Paper = Hex("#EFE4C6");
        private static readonly Color Ink = Hex("#24312D");
        private static readonly Color DustyBlue = Hex("#8FA9A6");
        private static readonly Color ApprovalRed = Hex("#A43B32");
        private static readonly Color Brass = Hex("#C89A52");

        [MenuItem("Tools/Desk 42/Visual Identity/Polish Shift With Placeholders")]
        public static void Apply()
        {
            ConfigureBackgroundImport();
            Scene scene = EditorSceneManager.OpenScene(ShiftScenePath, OpenSceneMode.Single);
            GameObject shiftUI = GameObject.Find("ShiftUI");
            if (shiftUI == null)
                throw new InvalidOperationException("ShiftUI was not found in Shift.unity.");

            ConfigureCanvas(shiftUI);
            CreateStageBackground(shiftUI.transform);
            RemoveRejectedPixelRoomOverlay(shiftUI.transform);
            CreateTopStatusBacking(shiftUI.transform);
            PolishHud(shiftUI.transform);
            PolishClaimDocument(shiftUI.transform);
            PolishClientStage(shiftUI.transform);
            PolishCardHand(shiftUI.transform);
            PolishDecisionButtons(shiftUI.transform);
            PolishCardPrefab();
            SetCoreRenderOrder(shiftUI.transform);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[ShiftVisualPolish] Placeholder desk-stage polish applied.");
        }

        public static void ApplyFromCommandLine()
        {
            try
            {
                Apply();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void ConfigureCanvas(GameObject shiftUI)
        {
            Canvas canvas = shiftUI.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = false;

            CanvasScaler scaler = shiftUI.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        private static void ConfigureBackgroundImport()
        {
            if (AssetImporter.GetAtPath(BackgroundPath) is not TextureImporter importer)
                return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        private static void CreateStageBackground(Transform root)
        {
            GameObject background = EnsureImageObject(root, "DeskStageBackground");
            background.transform.SetAsFirstSibling();
            Stretch(background.GetComponent<RectTransform>());

            Image image = background.GetComponent<Image>();
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(BackgroundPath);
            image.color = Color.white;
            image.preserveAspect = false;
            image.raycastTarget = false;
        }

        private static void RemoveRejectedPixelRoomOverlay(Transform root)
        {
            Transform overlay = root.Find("PixelRoomStageOverlay");
            if (overlay != null)
                UnityEngine.Object.DestroyImmediate(overlay.gameObject);
        }

        private static void CreateTopStatusBacking(Transform root)
        {
            GameObject backing = EnsureImageObject(root, "TopStatusBacking");
            backing.transform.SetSiblingIndex(Mathf.Min(1, root.childCount - 1));
            SetRect(backing.GetComponent<RectTransform>(),
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 108f));
            Image image = backing.GetComponent<Image>();
            image.color = new Color(DeepGreenDark.r, DeepGreenDark.g, DeepGreenDark.b, 0.96f);
            image.raycastTarget = false;
        }

        private static void PolishHud(Transform root)
        {
            Place(root, "SoulGauge", new Vector2(0f, 1f), new Vector2(24f, -18f), new Vector2(270f, 30f), new Vector2(0f, 1f));
            Place(root, "SanityGauge", new Vector2(0f, 1f), new Vector2(24f, -58f), new Vector2(270f, 30f), new Vector2(0f, 1f));
            StyleGauge(root.Find("SoulGauge"), "SoulLabel", ApprovalRed);
            StyleGauge(root.Find("SanityGauge"), "SanityLabel", Hex("#377A85"));

            Place(root, "PhaseLabel", new Vector2(0.5f, 1f), new Vector2(0f, -17f), new Vector2(420f, 34f), new Vector2(0.5f, 1f));
            Place(root, "ClientProgressLabel", new Vector2(0.5f, 1f), new Vector2(0f, -57f), new Vector2(480f, 28f), new Vector2(0.5f, 1f));
            StyleText(root.Find("PhaseLabel"), 24f, Paper, FontStyles.Bold, TextAlignmentOptions.Center);
            StyleText(root.Find("ClientProgressLabel"), 17f, DustyBlue, FontStyles.Normal, TextAlignmentOptions.Center);

            Place(root, "TimerLabel", new Vector2(1f, 1f), new Vector2(-24f, -17f), new Vector2(180f, 34f), new Vector2(1f, 1f));
            Place(root, "TimerFill", new Vector2(1f, 1f), new Vector2(-24f, -57f), new Vector2(180f, 12f), new Vector2(1f, 1f));
            StyleText(root.Find("TimerLabel"), 24f, Paper, FontStyles.Bold, TextAlignmentOptions.Right);

            Place(root, "CreditsLabel", new Vector2(1f, 1f), new Vector2(-226f, -59f), new Vector2(180f, 28f), new Vector2(1f, 1f));
            StyleText(root.Find("CreditsLabel"), 19f, Brass, FontStyles.Bold, TextAlignmentOptions.Right);
        }

        private static void StyleGauge(Transform gauge, string labelName, Color fillColor)
        {
            if (gauge == null) return;
            Image background = gauge.Find("Background")?.GetComponent<Image>();
            if (background != null) background.color = new Color(0.02f, 0.07f, 0.06f, 0.95f);
            Image fill = gauge.Find("Fill Area/Fill")?.GetComponent<Image>();
            if (fill != null) fill.color = fillColor;
            StyleText(gauge.Find(labelName), 15f, Paper, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        }

        private static void PolishClaimDocument(Transform root)
        {
            Transform panel = root.Find("ClaimPanel");
            if (panel == null) return;

            DestroyComponent<VerticalLayoutGroup>(panel.gameObject);
            DestroyComponent<ContentSizeFitter>(panel.gameObject);
            SetRect(panel.GetComponent<RectTransform>(),
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f), new Vector2(34f, 18f), new Vector2(510f, 590f));

            Transform background = panel.Find("Background");
            if (background != null)
            {
                Stretch(background.GetComponent<RectTransform>());
                Image backgroundImage = background.GetComponent<Image>();
                backgroundImage.color = new Color(Paper.r, Paper.g, Paper.b, 0.98f);
                background.SetAsFirstSibling();
                AddOutline(background.gameObject, DeepGreenDark, new Vector2(5f, -5f));
            }

            Transform content = panel.Find("PanelRoot");
            if (content == null) return;
            DestroyComponent<VerticalLayoutGroup>(content.gameObject);
            DestroyComponent<ContentSizeFitter>(content.gameObject);
            Stretch(content.GetComponent<RectTransform>());
            content.SetAsLastSibling();

            LayoutDocumentLabel(content, "ClaimIdLabel", 26f, 24f, 458f, 25f, 14f, DustyBlue, FontStyles.Bold);
            LayoutDocumentLabel(content, "ClaimantLabel", 26f, 62f, 458f, 40f, 27f, Ink, FontStyles.Bold);
            LayoutDocumentLabel(content, "AmountLabel", 26f, 108f, 458f, 34f, 23f, ApprovalRed, FontStyles.Bold);
            LayoutDocumentLabel(content, "SpeciesLabel", 26f, 148f, 458f, 28f, 16f, DeepGreen, FontStyles.Bold);
            LayoutDocumentLabel(content, "IncidentText", 26f, 192f, 458f, 190f, 19f, Ink, FontStyles.Normal);
            LayoutDocumentLabel(content, "AnomalyTagsLabel", 26f, 400f, 458f, 70f, 15f, DeepGreen, FontStyles.Italic);
            LayoutDocumentLabel(content, "NDALabel", 26f, 504f, 458f, 34f, 16f, ApprovalRed, FontStyles.Bold);
        }

        private static void LayoutDocumentLabel(
            Transform parent, string name, float x, float y, float width, float height,
            float size, Color color, FontStyles style)
        {
            Transform child = parent.Find(name);
            if (child == null) return;
            RectTransform rect = child.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);

            TMP_Text text = child.GetComponent<TMP_Text>();
            if (text == null) return;
            text.fontSize = size;
            text.color = color;
            text.fontStyle = style;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.enableWordWrapping = true;
            text.overflowMode = TextOverflowModes.Ellipsis;
        }

        private static void PolishClientStage(Transform root)
        {
            Transform info = root.Find("ClientInfo");
            if (info == null) return;

            DestroyComponent<VerticalLayoutGroup>(info.gameObject);
            DestroyComponent<ContentSizeFitter>(info.gameObject);
            SetRect(info.GetComponent<RectTransform>(),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(430f, 470f));

            Transform portrait = info.Find("ClientPortrait");
            if (portrait != null)
            {
                Image image = portrait.GetComponent<Image>();
                image.color = new Color(1f, 1f, 1f, 0f);
                image.raycastTarget = false;
                portrait.gameObject.SetActive(false);
            }

            // Claimant identity and mood must read through the seated character, claim
            // document, and fidget state. Floating labels across their body look like
            // debug UI and break the client-facing desk composition.
            SetActive(info, "VariantLabel", false);
            SetActive(info, "SpeciesLabel", false);
            SetActive(info, "MoodLabel", false);
            SetActive(info, "InjectionLabel", false);

            Transform indicator = info.Find("MoodIndicator");
            if (indicator != null)
            {
                indicator.gameObject.SetActive(false);
            }
        }

        private static void SetActive(Transform parent, string name, bool active)
        {
            Transform child = parent.Find(name);
            if (child != null)
                child.gameObject.SetActive(active);
        }

        private static void PlaceInfoLabel(
            Transform parent, string name, float y, float height,
            float size, Color color, FontStyles style)
        {
            Transform child = parent.Find(name);
            if (child == null) return;
            SetRect(child.GetComponent<RectTransform>(),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(360f, height));
            StyleText(child, size, color, style, TextAlignmentOptions.Center);
        }

        private static void PolishCardHand(Transform root)
        {
            Transform hand = root.Find("CardHand");
            if (hand == null) return;

            SetRect(hand.GetComponent<RectTransform>(),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(980f, 214f));

            Image tray = hand.GetComponent<Image>() ?? hand.gameObject.AddComponent<Image>();
            tray.color = new Color(DeepGreenDark.r, DeepGreenDark.g, DeepGreenDark.b, 0.94f);
            tray.raycastTarget = false;
            AddOutline(hand.gameObject, Brass, new Vector2(2f, -2f));

            HorizontalLayoutGroup layout = hand.GetComponent<HorizontalLayoutGroup>()
                ?? hand.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(22, 22, 7, 7);
            layout.spacing = 18f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
        }

        private static void PolishDecisionButtons(Transform root)
        {
            StyleDecisionButton(root.Find("ApproveBtn"), "APPROVE", new Vector2(650f, 28f), DeepGreen);
            StyleDecisionButton(root.Find("DenyBtn"), "DENY", new Vector2(830f, 28f), ApprovalRed);
        }

        private static void StyleDecisionButton(
            Transform buttonTransform, string label, Vector2 position, Color color)
        {
            if (buttonTransform == null) return;
            SetRect(buttonTransform.GetComponent<RectTransform>(),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f), position, new Vector2(160f, 62f));

            Image image = buttonTransform.GetComponent<Image>();
            image.color = color;
            AddOutline(buttonTransform.gameObject, Paper, new Vector2(2f, -2f));

            Button button = buttonTransform.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = color;
            colors.highlightedColor = Color.Lerp(color, Paper, 0.24f);
            colors.pressedColor = Color.Lerp(color, Color.black, 0.28f);
            button.colors = colors;

            Transform labelTransform = buttonTransform.Find("Label");
            if (labelTransform != null)
            {
                Stretch(labelTransform.GetComponent<RectTransform>());
                TMP_Text text = labelTransform.GetComponent<TMP_Text>();
                text.text = label;
                text.fontSize = 20f;
                text.color = Paper;
                text.fontStyle = FontStyles.Bold;
                text.alignment = TextAlignmentOptions.Center;
            }
        }

        private static void PolishCardPrefab()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(CardPrefabPath);
            try
            {
                Image background = root.GetComponent<Image>();
                background.color = Paper;
                AddOutline(root, DeepGreenDark, new Vector2(3f, -3f));

                var data = new SerializedObject(root.GetComponent<CardButtonView>());
                data.FindProperty("_normalColor").colorValue = Paper;
                data.FindProperty("_jammedColor").colorValue = Brass;
                data.FindProperty("_crumpledColor").colorValue = DustyBlue;
                data.ApplyModifiedPropertiesWithoutUndo();

                StyleText(root.transform.Find("NameLabel"), 16f, Ink, FontStyles.Bold, TextAlignmentOptions.Center);
                StyleText(root.transform.Find("TypeLabel"), 12f, DeepGreen, FontStyles.Italic, TextAlignmentOptions.Center);
                StyleText(root.transform.Find("CostLabel"), 18f, Hex("#72571F"), FontStyles.Bold, TextAlignmentOptions.Center);
                StyleText(root.transform.Find("FatigueLabel"), 12f, ApprovalRed, FontStyles.Bold, TextAlignmentOptions.Center);

                PrefabUtility.SaveAsPrefabAsset(root, CardPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void SetCoreRenderOrder(Transform root)
        {
            MoveAfter(root, "DeskStageBackground", 0);
            MoveAfter(root, "TopStatusBacking", 2);
            MoveAfter(root, "ClaimPanel", 10);
            MoveAfter(root, "ClientInfo", 11);
            MoveAfter(root, "CardHand", 12);
            MoveAfter(root, "ApproveBtn", 13);
            MoveAfter(root, "DenyBtn", 14);
        }

        private static void MoveAfter(Transform root, string name, int index)
        {
            Transform child = root.Find(name);
            if (child != null)
                child.SetSiblingIndex(Mathf.Clamp(index, 0, root.childCount - 1));
        }

        private static void Place(
            Transform root, string name, Vector2 anchor, Vector2 position,
            Vector2 size, Vector2 pivot)
        {
            Transform child = root.Find(name);
            if (child == null) return;
            SetRect(child.GetComponent<RectTransform>(), anchor, anchor, pivot, position, size);
        }

        private static void SetRect(
            RectTransform rect, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 pivot, Vector2 position, Vector2 size)
        {
            if (rect == null) return;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }

        private static void Stretch(RectTransform rect)
        {
            if (rect == null) return;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }

        private static GameObject EnsureImageObject(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
                return existing.gameObject;

            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.layer = LayerMask.NameToLayer("UI");
            go.transform.SetParent(parent, false);
            return go;
        }

        private static GameObject EnsureRawImageObject(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
                return existing.gameObject;

            var go = new GameObject(
                name, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            go.layer = LayerMask.NameToLayer("UI");
            go.transform.SetParent(parent, false);
            return go;
        }

        private static void StyleText(
            Transform transform, float size, Color color,
            FontStyles style, TextAlignmentOptions alignment)
        {
            if (transform == null) return;
            TMP_Text text = transform.GetComponent<TMP_Text>();
            if (text == null) return;
            text.fontSize = size;
            text.color = color;
            text.fontStyle = style;
            text.alignment = alignment;
            text.enableWordWrapping = true;
        }

        private static void AddOutline(GameObject target, Color color, Vector2 distance)
        {
            Outline outline = target.GetComponent<Outline>() ?? target.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = distance;
            outline.useGraphicAlpha = true;
        }

        private static void DestroyComponent<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            if (component != null)
                UnityEngine.Object.DestroyImmediate(component);
        }

        private static Color Hex(string value)
        {
            return ColorUtility.TryParseHtmlString(value, out Color color)
                ? color
                : Color.magenta;
        }
    }
}
#endif
