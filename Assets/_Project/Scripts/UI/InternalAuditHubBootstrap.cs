// ============================================================
// DESK 42 — Internal Audit Hub Bootstrap (MonoBehaviour)
//
// The between-runs meta-hub. Self-bootstraps a Canvas with:
//   - Bank balance display
//   - Employee Handbook benefit shop (3 buyable perks)
//   - Conspiracy Board access (just opens ConspiracyBoardUI)
//   - Next Shift button → starts a new run
//
// Drop on any GameObject in InternalAudit.unity.
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Desk42.Core;

namespace Desk42.UI
{
    [DisallowMultipleComponent]
    public sealed class InternalAuditHubBootstrap : MonoBehaviour
    {
        // ── Benefit Catalog ──────────────────────────────────

        private static readonly (string Id, string Name, string Desc, int Cost)[] _benefits =
        {
            ("health_insurance", "Health Insurance",
                "Reduce sanity drain by 10%.", 150),
            ("signing_bonus",    "Signing Bonus",
                "Start runs with +20 credits.", 50),
            ("ergonomic_chair",  "Ergonomic Chair",
                "Slow timer drain by 5%.", 100),
        };

        // ── State ─────────────────────────────────────────────

        private TMP_Text _bankText;
        private readonly Dictionary<string, Button>   _benefitBtns   = new();
        private readonly Dictionary<string, TMP_Text> _benefitLabels = new();

        // ── Lifecycle ─────────────────────────────────────────

        private void Start()
        {
            BuildUI();
            Refresh();
        }

        // ── UI ────────────────────────────────────────────────

        private void BuildUI()
        {
            var canvasGO = new GameObject("InternalAuditHubCanvas");
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasGO.AddComponent<GraphicRaycaster>();

            if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<UnityEngine.EventSystems.EventSystem>();
                es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            // Background
            var bg = new GameObject("BG");
            bg.transform.SetParent(canvasGO.transform, false);
            var bgRT = bg.AddComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero; bgRT.anchorMax = Vector2.one;
            bgRT.offsetMin = Vector2.zero; bgRT.offsetMax = Vector2.zero;
            bg.AddComponent<Image>().color = new Color(0.08f, 0.08f, 0.10f, 1f);

            // Title
            CreateLabel(canvasGO.transform, "INTERNAL AUDIT — HUMAN RESOURCES",
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0, -40), new Vector2(900, 60),
                32, FontStyles.Bold, TextAlignmentOptions.Center,
                new Color(0.95f, 0.85f, 0.50f));

            // Bank balance display (top-right)
            var bankGO = new GameObject("BankBalance");
            bankGO.transform.SetParent(canvasGO.transform, false);
            var brRT = bankGO.AddComponent<RectTransform>();
            brRT.anchorMin = new Vector2(1, 1); brRT.anchorMax = new Vector2(1, 1);
            brRT.pivot = new Vector2(1, 1);
            brRT.sizeDelta = new Vector2(360, 60);
            brRT.anchoredPosition = new Vector2(-40, -40);
            bankGO.AddComponent<Image>().color = new Color(0.12f, 0.12f, 0.14f, 0.95f);

            var bankLbl = new GameObject("Lbl");
            bankLbl.transform.SetParent(bankGO.transform, false);
            var brt = bankLbl.AddComponent<RectTransform>();
            brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one;
            brt.offsetMin = new Vector2(16, 0); brt.offsetMax = new Vector2(-16, 0);
            _bankText = bankLbl.AddComponent<TextMeshProUGUI>();
            _bankText.fontSize = 22; _bankText.fontStyle = FontStyles.Bold;
            _bankText.color = new Color(0.95f, 0.85f, 0.50f);
            _bankText.alignment = TextAlignmentOptions.MidlineLeft;
            _bankText.raycastTarget = false;

            // Employee Handbook (left column)
            CreateLabel(canvasGO.transform, "EMPLOYEE HANDBOOK",
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(80, -120), new Vector2(500, 40),
                22, FontStyles.Bold, TextAlignmentOptions.MidlineLeft,
                Color.white);

            float y = -180;
            foreach (var b in _benefits)
            {
                BuildBenefitRow(canvasGO.transform, b.Id, b.Name, b.Desc, b.Cost,
                    new Vector2(80, y));
                y -= 100;
            }

            // Conspiracy Board access (right column)
            CreateLabel(canvasGO.transform, "CONSPIRACY BOARD",
                new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-80, -120), new Vector2(500, 40),
                22, FontStyles.Bold, TextAlignmentOptions.MidlineRight,
                Color.white);

            BuildBigButton(canvasGO.transform, "BoardBtn", "OPEN CONSPIRACY BOARD",
                new Color(0.30f, 0.20f, 0.45f),
                new Vector2(1, 1), new Vector2(-330, -210),
                new Vector2(420, 100), OnConspiracyBoardClicked);

            // Next Shift (bottom-center)
            BuildBigButton(canvasGO.transform, "NextShiftBtn", "BEGIN NEXT SHIFT",
                new Color(0.20f, 0.50f, 0.30f),
                new Vector2(0.5f, 0), new Vector2(0, 60),
                new Vector2(380, 80), OnNextShiftClicked);
        }

        private void BuildBenefitRow(Transform parent, string id, string name, string desc,
            int cost, Vector2 anchoredPos)
        {
            var row = new GameObject($"Benefit_{id}");
            row.transform.SetParent(parent, false);
            var rt = row.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(0, 1);
            rt.pivot     = new Vector2(0, 1);
            rt.sizeDelta = new Vector2(680, 90);
            rt.anchoredPosition = anchoredPos;

            row.AddComponent<Image>().color = new Color(0.13f, 0.13f, 0.16f, 1f);

            // Name
            var nameTmp = CreateLabel(row.transform, name,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1),
                new Vector2(16, -8), new Vector2(-16, 32),
                18, FontStyles.Bold, TextAlignmentOptions.MidlineLeft,
                new Color(0.95f, 0.85f, 0.50f));
            nameTmp.rectTransform.anchoredPosition = new Vector2(16, -8);

            // Desc
            CreateLabel(row.transform, desc,
                new Vector2(0, 0), new Vector2(1, 0.5f), new Vector2(0, 0),
                new Vector2(16, 8), new Vector2(-180, 28),
                12, FontStyles.Normal, TextAlignmentOptions.TopLeft,
                new Color(0.85f, 0.85f, 0.85f));

            // Buy button
            var btnGO = new GameObject("Buy");
            btnGO.transform.SetParent(row.transform, false);
            var brt = btnGO.AddComponent<RectTransform>();
            brt.anchorMin = new Vector2(1, 0.5f); brt.anchorMax = new Vector2(1, 0.5f);
            brt.pivot = new Vector2(1, 0.5f);
            brt.sizeDelta = new Vector2(150, 60);
            brt.anchoredPosition = new Vector2(-12, 0);
            btnGO.AddComponent<Image>().color = new Color(0.20f, 0.40f, 0.25f);
            var btn = btnGO.AddComponent<Button>();
            string capId = id;
            int capCost = cost;
            btn.onClick.AddListener(() => OnBuyBenefit(capId, capCost));
            _benefitBtns[id] = btn;

            var lbl = new GameObject("Lbl");
            lbl.transform.SetParent(btnGO.transform, false);
            var lrt = lbl.AddComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
            var ltmp = lbl.AddComponent<TextMeshProUGUI>();
            ltmp.text = $"¢{cost}";
            ltmp.fontSize = 20; ltmp.fontStyle = FontStyles.Bold;
            ltmp.color = Color.white; ltmp.alignment = TextAlignmentOptions.Center;
            ltmp.raycastTarget = false;
            _benefitLabels[id] = ltmp;
        }

        private void BuildBigButton(Transform parent, string name, string label, Color color,
            Vector2 anchorMinMax, Vector2 anchoredPos, Vector2 size, Action onClick)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMinMax; rt.anchorMax = anchorMinMax;
            rt.pivot = new Vector2(0.5f, 0);
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPos;

            go.AddComponent<Image>().color = color;
            var btn = go.AddComponent<Button>();
            btn.onClick.AddListener(() => onClick?.Invoke());

            var lbl = new GameObject("Lbl");
            lbl.transform.SetParent(go.transform, false);
            var lrt = lbl.AddComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
            var tmp = lbl.AddComponent<TextMeshProUGUI>();
            tmp.text = label; tmp.fontSize = 22; tmp.fontStyle = FontStyles.Bold;
            tmp.color = Color.white; tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
        }

        // ── Actions ───────────────────────────────────────────

        private void OnBuyBenefit(string id, int cost)
        {
            var meta = GameManager.Instance?.Meta;
            if (meta == null) return;
            if (meta.EmployeeHandbook.HasBenefit(id))
            {
                Debug.Log($"[Hub] Already own {id}.");
                return;
            }
            if (meta.BankBalance < cost)
            {
                Debug.Log($"[Hub] Cannot afford {id}: ¢{cost}, have ¢{meta.BankBalance}.");
                return;
            }
            meta.BankBalance -= cost;
            meta.EmployeeHandbook.UnlockBenefit(id, cost);
            SaveSystem.SaveMeta(meta);
            Refresh();
        }

        private void OnNextShiftClicked()
        {
            // Re-uses MainMenu picker chain by spawning the pickers on this GO
            var archPicker = GetOrAdd<ArchetypePickerPanel>();
            archPicker.Show(archetypeId =>
            {
                var vowPicker = GetOrAdd<VowPickerPanel>();
                vowPicker.Show(vows =>
                {
                    GameManager.Instance.StartNewRun(archetypeId);
                    var run = GameManager.Instance.Run?.RawData;
                    if (run != null && vows != null && vows.Count > 0)
                    {
                        run.ActiveVows.Clear();
                        run.ActiveVows.AddRange(vows);
                    }
                });
            });
        }

        private void OnConspiracyBoardClicked()
        {
            var board = FindObjectOfType<ConspiracyBoardUI>();
            if (board != null)
                board.gameObject.SetActive(true);
            else
                Debug.LogWarning("[Hub] No ConspiracyBoardUI in scene.");
        }

        // ── Refresh ───────────────────────────────────────────

        private void Refresh()
        {
            var meta = GameManager.Instance?.Meta;
            if (meta == null) { _bankText.text = "¢???"; return; }

            _bankText.text = $"BANKED: ¢{meta.BankBalance:N0}";

            foreach (var b in _benefits)
            {
                bool owned = meta.EmployeeHandbook.HasBenefit(b.Id);
                bool affordable = meta.BankBalance >= b.Cost;

                if (_benefitBtns.TryGetValue(b.Id, out var btn))
                    btn.interactable = !owned && affordable;

                if (_benefitLabels.TryGetValue(b.Id, out var lbl))
                    lbl.text = owned ? "OWNED" : $"¢{b.Cost}";
            }
        }

        // ── Helpers ───────────────────────────────────────────

        private T GetOrAdd<T>() where T : MonoBehaviour
        {
            var existing = GetComponent<T>();
            return existing != null ? existing : gameObject.AddComponent<T>();
        }

        private static TMP_Text CreateLabel(Transform parent, string text,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 anchoredPos, Vector2 sizeDelta,
            float fontSize, FontStyles style, TextAlignmentOptions align, Color color)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.pivot = pivot; rt.anchoredPosition = anchoredPos; rt.sizeDelta = sizeDelta;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text; tmp.fontSize = fontSize; tmp.fontStyle = style;
            tmp.alignment = align; tmp.color = color;
            tmp.enableWordWrapping = true; tmp.raycastTarget = false;
            return tmp;
        }
    }
}
