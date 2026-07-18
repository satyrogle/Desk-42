#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Desk42.EditorTools
{
    /// <summary>
    /// Dockable reference board for the curated Desk42 visual identity pack.
    /// Keeps concept art visible in-editor without adding reference images to gameplay scenes.
    /// </summary>
    public sealed class VisualIdentityBoardWindow : EditorWindow
    {
        private const string Root = "Assets/_Project/Art/Concepts/VisualIdentity";
        private const string Mockups = Root + "/Mockups";
        private const string ContactSheetPath = Mockups + "/D42_VisualIdentity_ContactSheet_v001.png";
        private const string GuidePath = Root + "/DESK42_PIXEL_ART_IDENTITY.md";
        private const string GenerationNotesPath = Root + "/COMFY_GENERATION_NOTES.md";

        private static readonly AssetEntry[] EnvironmentAssets =
        {
            new AssetEntry("Core Desk", Mockups + "/D42_Mockup_Desk_Core_v001.png", "Primary desk-stage composition and interaction density."),
            new AssetEntry("Healthy Office", Mockups + "/D42_Mockup_Office_Healthy_v001.png", "Stable office palette, lighting, and geometry."),
            new AssetEntry("Degraded Office", Mockups + "/D42_Mockup_Office_Degraded_v001.png", "Crooked reality, contaminated light, and warmer paper."),
            new AssetEntry("Processing Station", Mockups + "/D42_Mockup_ProcessingStation_v001.png", "Close machinery and paper-processing mood reference.")
        };

        private static readonly AssetEntry[] SystemAssets =
        {
            new AssetEntry("CorpOS", Mockups + "/D42_Mockup_CorpOS_v001.png", "Paper bureaucracy translated into a CRT interface."),
            new AssetEntry("Prop Language", Mockups + "/D42_Mockup_PropLanguage_v001.png", "Cream enamel, Bakelite, paper, brass, and approval-red shapes.")
        };

        private static readonly AssetEntry[] ClaimantAssets =
        {
            new AssetEntry("Claimant Board", Mockups + "/D42_Mockup_Claimants_v001.png", "Fixed corporate ID crop with one impossible identity hook."),
            new AssetEntry("Moth Accountant", Mockups + "/D42_Claimant_MothAccountant_v001.png", "Feathery antennae and tired symmetry."),
            new AssetEntry("Gel Anomaly", Mockups + "/D42_Claimant_GelAnomaly_v001.png", "Human read disrupted by translucent teal matter."),
            new AssetEntry("Unregistered Alien", Mockups + "/D42_Claimant_UnregisteredAlien_v001.png", "Oversized eyes and sober office tailoring."),
            new AssetEntry("Void Proxy", Mockups + "/D42_Claimant_VoidProxy_v001.png", "Hard dark mask with a teal machine rim.")
        };

        private static readonly AssetEntry[] ProductionSpriteAssets =
        {
            new AssetEntry("Coffee Sprite", "Assets/_Project/Art/Sprites/VisualIdentity/Props/coffee.png", "Transparent single Sprite extracted from the prop sheet."),
            new AssetEntry("Pen Holder Sprite", "Assets/_Project/Art/Sprites/VisualIdentity/Props/pen_holder.png", "Transparent single Sprite extracted from the prop sheet."),
            new AssetEntry("Papers Sprite", "Assets/_Project/Art/Sprites/VisualIdentity/Props/papers.png", "Transparent single Sprite extracted from the prop sheet."),
            new AssetEntry("Crumpled Paper Sprite", "Assets/_Project/Art/Sprites/VisualIdentity/Props/crumpled_paper.png", "Transparent single Sprite extracted without the neighboring red prop."),
            new AssetEntry("Moth Fidget Sprite", "Assets/_Project/Art/Sprites/VisualIdentity/Claimants/claimant_moth_fidget.png", "Transparent claimant cutout for fidget and portrait-motion testing.")
        };

        private static readonly Color DeepGreen = Hex("#173F32");
        private static readonly Color PaperCream = Hex("#F1E8CE");
        private static readonly Color DustyBlue = Hex("#8FA9A6");
        private static readonly Color WarmGrey = Hex("#77736A");
        private static readonly Color MutedOrange = Hex("#CE713A");
        private static readonly Color ApprovalRed = Hex("#B73B32");
        private static readonly Color AnomalyTeal = Hex("#20D6C7");
        private static readonly Color ElectricBlue = Hex("#4AA7FF");
        private static readonly Color GlitchMagenta = Hex("#D447A7");

        private readonly Dictionary<string, Texture2D> _textures = new Dictionary<string, Texture2D>();
        private readonly string[] _tabs = { "Overview", "Environment", "CorpOS + Props", "Claimants", "Production" };

        private Vector2 _scroll;
        private int _selectedTab;
        private GUIStyle _headerStyle;
        private GUIStyle _subheaderStyle;
        private GUIStyle _captionStyle;
        private GUIStyle _bodyStyle;
        private string _validationMessage;

        [MenuItem("Tools/Desk 42/Visual Identity Board", priority = 5)]
        public static void Open()
        {
            var window = GetWindow<VisualIdentityBoardWindow>("Desk42 Identity");
            window.minSize = new Vector2(760f, 600f);
            window.Show();
        }

        [MenuItem("Tools/Desk 42/Visual Identity/Apply Pixel Import Settings", priority = 6)]
        public static void ApplyPixelImportSettingsMenu()
        {
            int changed = ApplyPixelImportSettings();
            Debug.Log($"[Desk42 Visual Identity] Pixel import settings applied. {changed} texture(s) changed.");
        }

        [MenuItem("Tools/Desk 42/Visual Identity/Select Contact Sheet", priority = 7)]
        public static void SelectContactSheet()
        {
            SelectAndPing(ContactSheetPath);
        }

        private void OnEnable()
        {
            LoadTextures();
        }

        private void OnFocus()
        {
            LoadTextures();
            Repaint();
        }

        private void OnGUI()
        {
            EnsureStyles();
            DrawHeader();
            _selectedTab = GUILayout.Toolbar(_selectedTab, _tabs, GUILayout.Height(27f));

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            GUILayout.Space(12f);
            switch (_selectedTab)
            {
                case 0:
                    DrawOverview();
                    break;
                case 1:
                    DrawGrid(EnvironmentAssets);
                    break;
                case 2:
                    DrawGrid(SystemAssets);
                    break;
                case 3:
                    DrawClaimants();
                    break;
                default:
                    DrawProduction();
                    break;
            }
            GUILayout.Space(18f);
            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            Rect header = GUILayoutUtility.GetRect(1f, 74f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(header, DeepGreen);

            Rect title = new Rect(header.x + 18f, header.y + 10f, header.width - 36f, 32f);
            GUI.Label(title, "DESK42 VISUAL IDENTITY", _headerStyle);

            Rect subtitle = new Rect(header.x + 20f, header.y + 43f, header.width - 40f, 20f);
            GUI.Label(subtitle, "ANALOG AUTHORITY / PAPER AS WORLD / CONTROLLED INTRUSION / HOSTILE GEOMETRY", _captionStyle);
        }

        private void DrawOverview()
        {
            EditorGUILayout.HelpBox(
                "These images are visual-development references. They are wired into this editor board for comparison and handoff, but they are not production sprites.",
                MessageType.Info);

            DrawTexture(ContactSheetPath, 460f);
            GUILayout.Space(10f);
            DrawPalette();
            GUILayout.Space(10f);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Select Artist Guide", GUILayout.Height(28f)))
                SelectAndPing(GuidePath);
            if (GUILayout.Button("Select Comfy Notes", GUILayout.Height(28f)))
                SelectAndPing(GenerationNotesPath);
            if (GUILayout.Button("Select Contact Sheet", GUILayout.Height(28f)))
                SelectAndPing(ContactSheetPath);
            EditorGUILayout.EndHorizontal();

            DrawImportControls();
        }

        private void DrawGrid(AssetEntry[] entries)
        {
            for (int i = 0; i < entries.Length; i += 2)
            {
                EditorGUILayout.BeginHorizontal();
                DrawAssetCard(entries[i]);
                if (i + 1 < entries.Length)
                    DrawAssetCard(entries[i + 1]);
                else
                    GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
                GUILayout.Space(8f);
            }
        }

        private void DrawClaimants()
        {
            DrawAssetCard(ClaimantAssets[0], 390f);
            GUILayout.Space(8f);
            DrawGrid(new[] { ClaimantAssets[1], ClaimantAssets[2], ClaimantAssets[3], ClaimantAssets[4] });
        }

        private void DrawProduction()
        {
            EditorGUILayout.LabelField("Production lock", _subheaderStyle);
            EditorGUILayout.HelpBox(
                "Redraw selected directions on a 320x180 or 384x216 native grid. Scale with whole integers, use Point filtering, disable mip maps, and keep final asset pivots and Pixels Per Unit consistent.",
                MessageType.None);

            DrawRule("Pixel construction", "Hard clusters, selective one-pixel outlines, 12-24 shared colors, upper-left light, two shadow steps.");
            DrawRule("UI", "Paper-form hierarchy, deep green structure, cream reading field, muted orange selection, teal anomaly, rare magenta rupture.");
            DrawRule("Claimants", "Fixed corporate ID framing. One silhouette hook, one material hook, and one behavioral hook per client.");
            DrawRule("Delivery", "Layered source, transparent PNG, palette, pivots, timings, state names, font licenses, and native-size review sheet.");

            GUILayout.Space(10f);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Select Full Production Guide", GUILayout.Height(30f)))
                SelectAndPing(GuidePath);
            if (GUILayout.Button("Validate Imports", GUILayout.Height(30f)))
                _validationMessage = ValidateImportSettings();
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(_validationMessage))
                EditorGUILayout.HelpBox(_validationMessage, _validationMessage.StartsWith("Ready", StringComparison.Ordinal) ? MessageType.Info : MessageType.Warning);

            GUILayout.Space(14f);
            EditorGUILayout.LabelField("Sliced test sprites - 128 PPU", _subheaderStyle);
            DrawGrid(ProductionSpriteAssets);
        }

        private void DrawAssetCard(AssetEntry entry, float previewHeight = 260f)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.MinWidth(260f), GUILayout.ExpandWidth(true));
            EditorGUILayout.LabelField(entry.Label, _subheaderStyle);

            Texture2D texture = GetTexture(entry.Path);
            Rect preview = GUILayoutUtility.GetRect(120f, previewHeight, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(preview, new Color(0.08f, 0.1f, 0.09f, 1f));
            if (texture != null)
                EditorGUI.DrawPreviewTexture(preview, texture, null, ScaleMode.ScaleToFit);
            else
                EditorGUI.HelpBox(preview, "Missing texture:\n" + entry.Path, MessageType.Error);

            EditorGUILayout.LabelField(entry.Note, _bodyStyle);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Select Asset"))
                SelectAndPing(entry.Path);
            if (GUILayout.Button("Copy Path"))
                EditorGUIUtility.systemCopyBuffer = entry.Path;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private void DrawTexture(string path, float height)
        {
            Texture2D texture = GetTexture(path);
            Rect preview = GUILayoutUtility.GetRect(120f, height, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(preview, new Color(0.08f, 0.1f, 0.09f, 1f));
            if (texture != null)
                EditorGUI.DrawPreviewTexture(preview, texture, null, ScaleMode.ScaleToFit);
            else
                EditorGUI.HelpBox(preview, "Missing visual identity contact sheet.", MessageType.Error);
        }

        private void DrawPalette()
        {
            EditorGUILayout.LabelField("Locked palette", _subheaderStyle);
            EditorGUILayout.BeginHorizontal();
            DrawSwatch("OFFICE", DeepGreen, PaperCream);
            DrawSwatch("PAPER", PaperCream, DeepGreen);
            DrawSwatch("NEUTRAL", DustyBlue, DeepGreen);
            DrawSwatch("TASK", MutedOrange, Color.black);
            DrawSwatch("STAMP", ApprovalRed, Color.white);
            DrawSwatch("ANOMALY", AnomalyTeal, Color.black);
            DrawSwatch("CHARGE", ElectricBlue, Color.black);
            DrawSwatch("RUPTURE", GlitchMagenta, Color.white);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField("70 / 20 / 8 / 2 - base structure / neutral support / task accent / anomaly color", _captionStyle);
        }

        private static void DrawSwatch(string label, Color color, Color textColor)
        {
            Rect rect = GUILayoutUtility.GetRect(58f, 48f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, color);
            var style = new GUIStyle(EditorStyles.miniBoldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = textColor }
            };
            GUI.Label(rect, label, style);
        }

        private void DrawImportControls()
        {
            GUILayout.Space(12f);
            EditorGUILayout.LabelField("Unity import wiring", _subheaderStyle);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Validate Pixel Import Settings", GUILayout.Height(28f)))
                _validationMessage = ValidateImportSettings();
            if (GUILayout.Button("Apply Point / No Mips / Uncompressed", GUILayout.Height(28f)))
            {
                int changed = ApplyPixelImportSettings();
                _validationMessage = $"Ready - pixel import settings applied; {changed} texture(s) changed.";
                LoadTextures();
            }
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(_validationMessage))
                EditorGUILayout.HelpBox(_validationMessage, _validationMessage.StartsWith("Ready", StringComparison.Ordinal) ? MessageType.Info : MessageType.Warning);
        }

        private static void DrawRule(string title, string body)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(body, EditorStyles.wordWrappedLabel);
            EditorGUILayout.EndVertical();
        }

        private static int ApplyPixelImportSettings()
        {
            int changed = 0;
            foreach (string path in AllTexturePaths())
            {
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                    continue;

                bool dirty = importer.filterMode != FilterMode.Point
                             || importer.mipmapEnabled
                             || importer.textureCompression != TextureImporterCompression.Uncompressed
                             || importer.npotScale != TextureImporterNPOTScale.None;

                if (IsProductionSpritePath(path))
                {
                    dirty = dirty
                            || importer.textureType != TextureImporterType.Sprite
                            || importer.spriteImportMode != SpriteImportMode.Single
                            || !Mathf.Approximately(importer.spritePixelsPerUnit, 128f)
                            || !importer.alphaIsTransparency
                            || importer.wrapMode != TextureWrapMode.Clamp
                            || (path.EndsWith("/claimant_moth_fidget.png", StringComparison.Ordinal)
                                && importer.spritePivot != new Vector2(0.5f, 0f));
                }

                if (!dirty)
                    continue;

                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.npotScale = TextureImporterNPOTScale.None;
                if (IsProductionSpritePath(path))
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.spritePixelsPerUnit = 128f;
                    importer.alphaIsTransparency = true;
                    importer.wrapMode = TextureWrapMode.Clamp;

                    if (path.EndsWith("/claimant_moth_fidget.png", StringComparison.Ordinal))
                    {
                        var settings = new TextureImporterSettings();
                        importer.ReadTextureSettings(settings);
                        settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
                        settings.spritePivot = new Vector2(0.5f, 0f);
                        importer.SetTextureSettings(settings);
                    }
                }
                importer.SaveAndReimport();
                changed++;
            }

            AssetDatabase.Refresh();
            return changed;
        }

        private static string ValidateImportSettings()
        {
            int missing = 0;
            int nonPixel = 0;
            int total = 0;

            foreach (string path in AllTexturePaths())
            {
                total++;
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                {
                    missing++;
                    continue;
                }

                bool productionInvalid = false;
                if (IsProductionSpritePath(path))
                {
                    productionInvalid = importer.textureType != TextureImporterType.Sprite
                                        || importer.spriteImportMode != SpriteImportMode.Single
                                        || !Mathf.Approximately(importer.spritePixelsPerUnit, 128f)
                                        || !importer.alphaIsTransparency
                                        || importer.wrapMode != TextureWrapMode.Clamp
                                        || (path.EndsWith("/claimant_moth_fidget.png", StringComparison.Ordinal)
                                            && importer.spritePivot != new Vector2(0.5f, 0f));
                }

                if (importer.filterMode != FilterMode.Point
                    || importer.mipmapEnabled
                    || importer.textureCompression != TextureImporterCompression.Uncompressed
                    || importer.npotScale != TextureImporterNPOTScale.None
                    || productionInvalid)
                {
                    nonPixel++;
                }
            }

            if (missing == 0 && nonPixel == 0)
                return $"Ready - all {total} visual identity textures use Point filtering, no mip maps, no NPOT scaling, and no lossy compression.";

            return $"Needs attention - {missing} missing importer(s), {nonPixel} texture(s) not using the locked pixel settings.";
        }

        private void LoadTextures()
        {
            _textures.Clear();
            foreach (string path in AllTexturePaths())
                _textures[path] = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private Texture2D GetTexture(string path)
        {
            if (_textures.TryGetValue(path, out Texture2D texture) && texture != null)
                return texture;

            texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            _textures[path] = texture;
            return texture;
        }

        private static IEnumerable<string> AllTexturePaths()
        {
            yield return ContactSheetPath;
            foreach (AssetEntry entry in EnvironmentAssets)
                yield return entry.Path;
            foreach (AssetEntry entry in SystemAssets)
                yield return entry.Path;
            foreach (AssetEntry entry in ClaimantAssets)
                yield return entry.Path;
            foreach (AssetEntry entry in ProductionSpriteAssets)
                yield return entry.Path;
        }

        private static bool IsProductionSpritePath(string path)
        {
            foreach (AssetEntry entry in ProductionSpriteAssets)
            {
                if (entry.Path == path)
                    return true;
            }
            return false;
        }

        private static void SelectAndPing(string path)
        {
            UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(path);
            if (asset == null)
            {
                Debug.LogWarning($"[Desk42 Visual Identity] Asset not found: {path}");
                return;
            }

            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        private void EnsureStyles()
        {
            if (_headerStyle == null)
                BuildStyles();
        }

        private void BuildStyles()
        {
            _headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 24,
                normal = { textColor = PaperCream }
            };
            _subheaderStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                normal = { textColor = DeepGreen }
            };
            _captionStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                fontStyle = FontStyle.Bold,
                normal = { textColor = AnomalyTeal },
                wordWrap = true
            };
            _bodyStyle = new GUIStyle(EditorStyles.wordWrappedLabel)
            {
                normal = { textColor = WarmGrey }
            };
        }

        private static Color Hex(string value)
        {
            return ColorUtility.TryParseHtmlString(value, out Color color) ? color : Color.magenta;
        }

        private sealed class AssetEntry
        {
            public AssetEntry(string label, string path, string note)
            {
                Label = label;
                Path = path;
                Note = note;
            }

            public string Label { get; }
            public string Path { get; }
            public string Note { get; }
        }
    }
}
#endif
