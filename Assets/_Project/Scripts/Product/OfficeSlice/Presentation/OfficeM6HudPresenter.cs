using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Desk42.Product.OfficeSlice
{
    public enum OfficeM6ControlScheme
    {
        Keyboard,
        Controller,
    }

    public enum OfficeM6DangerState
    {
        Calm,
        Rush,
        Break,
        Recovery,
        Result,
    }

    public sealed class OfficeM6HudModel
    {
        internal OfficeM6HudModel(long tick)
        {
            Tick = tick;
        }

        public long Tick { get; }
        public string ShiftText { get; internal set; } = string.Empty;
        public string TimeText { get; internal set; } = string.Empty;
        public string WaitingText { get; internal set; } = string.Empty;
        public string DangerText { get; internal set; } = string.Empty;
        public OfficeM6DangerState DangerState { get; internal set; }
        public string ActionPrompt { get; internal set; } = string.Empty;
        public bool CustomerCardVisible { get; internal set; }
        public string CustomerName { get; internal set; } = string.Empty;
        public string CustomerProblem { get; internal set; } = string.Empty;
        public string CustomerMood { get; internal set; } = string.Empty;
        public bool CaseCardVisible { get; internal set; }
        public string WhatWeKnow { get; internal set; } = string.Empty;
        public string WhatNeedsChecking { get; internal set; } = string.Empty;
        public string NextUsefulAction { get; internal set; } = string.Empty;
        public bool ManualChoicesVisible { get; internal set; }
        public bool DecisionChoicesVisible { get; internal set; }
        public bool RuleCardVisible { get; internal set; }
        public string RuleOneText { get; internal set; } = string.Empty;
        public string RuleTwoText { get; internal set; } = string.Empty;
        public bool BreakCardVisible { get; internal set; }
        public string BreakTitle { get; internal set; } = string.Empty;
        public string BreakCause { get; internal set; } = string.Empty;
        public IReadOnlyList<string> RecoveryItems { get; internal set; } =
            Array.Empty<string>();
        public bool ResultVisible { get; internal set; }
        public string ResultTitle { get; internal set; } = string.Empty;
        public string ResultSummary { get; internal set; } = string.Empty;
        public string TomorrowText { get; internal set; } = string.Empty;
        public bool DevelopmentHudVisible { get; internal set; }
        public bool TutorialVisible { get; internal set; }
        public string TutorialText { get; internal set; } = string.Empty;
        public string TutorialHighlightId { get; internal set; } = string.Empty;
        public int CurrentCustomerPresentationFocusCount { get; internal set; }
        public bool CarriedFileVisible { get; internal set; }
        public string CarriedFileText { get; internal set; } = string.Empty;
        public string OriginalCopyLegend { get; internal set; } = string.Empty;
        public string ActionableProblemRoom { get; internal set; } = string.Empty;
        public bool WhatHappenedAvailable { get; internal set; }
        public bool WhatHappenedVisible { get; internal set; }
        public string WhatHappenedPrompt { get; internal set; } = string.Empty;
        public string WhatHappenedText { get; internal set; } = string.Empty;

        public string AllNormalPlayerText()
        {
            var text = new StringBuilder(768);
            Append(text, ShiftText);
            Append(text, TimeText);
            Append(text, WaitingText);
            Append(text, DangerText);
            Append(text, ActionPrompt);
            Append(text, CustomerName);
            Append(text, CustomerProblem);
            Append(text, CustomerMood);
            Append(text, WhatWeKnow);
            Append(text, WhatNeedsChecking);
            Append(text, NextUsefulAction);
            Append(text, RuleOneText);
            Append(text, RuleTwoText);
            Append(text, BreakTitle);
            Append(text, BreakCause);
            for (int i = 0; i < RecoveryItems.Count; i++)
                Append(text, RecoveryItems[i]);
            Append(text, ResultTitle);
            Append(text, ResultSummary);
            Append(text, TomorrowText);
            Append(text, TutorialText);
            Append(text, CarriedFileText);
            Append(text, OriginalCopyLegend);
            Append(text, ActionableProblemRoom);
            Append(text, WhatHappenedPrompt);
            Append(text, WhatHappenedText);
            return text.ToString();
        }

        private static void Append(StringBuilder builder, string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            if (builder.Length > 0) builder.Append('\n');
            builder.Append(value);
        }
    }

    /// <summary>
    /// Read-only player HUD projection and layout. It owns no campaign state.
    /// </summary>
    public sealed class OfficeM6HudPresenter
    {
        public const float SafeMargin = 16f;
        public const float TopBarHeight = 52f;
        public const float SideCardWidth = 300f;
        public const float ActionWidth = 430f;
        public const float ActionHeight = 126f;

        public bool DevelopmentHudVisible { get; private set; }
        public bool WhatHappenedOpen { get; private set; }

        public void SetDevelopmentHudVisible(bool visible)
        {
            DevelopmentHudVisible = visible;
        }

        public void ToggleWhatHappened()
        {
            WhatHappenedOpen = !WhatHappenedOpen;
        }

        public OfficeM6HudModel Project(
            OfficeSimulationState state,
            OfficeCampaignState campaign,
            OfficeM6ControlScheme controlScheme)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));

            var model = new OfficeM6HudModel(state.CurrentTick)
            {
                ShiftText = "SHIFT " + campaign.CurrentShiftOrdinal + " / " +
                    campaign.CurrentShift.Title.ToUpperInvariant(),
                TimeText = FormatTime(state.CurrentTick),
                WaitingText = "WAITING " + WaitingCount(state),
                DangerState = DangerState(state, campaign),
                DevelopmentHudVisible = DevelopmentHudVisible,
            };
            model.DangerText = model.DangerState.ToString().ToUpperInvariant();
            model.ActionPrompt = OfficeM6PlayerCopyCatalog.Prompt(
                state.PrimaryActionLabel, controlScheme);

            if (campaign.Phase == OfficeCampaignPhase.CampaignResult)
            {
                model.ResultVisible = true;
                model.ResultTitle = OfficeM6PlayerCopyCatalog.ThreeShiftResult;
                OfficeCampaignResult result = campaign.Result;
                model.ResultSummary = result == null
                    ? "THE LEDGER IS CLOSING."
                    : "HELPED " + result.CustomersHelped +
                      "   RULE MATCHES " + result.RuleMatches +
                      "   MESSES FIXED " + result.OfficeFailuresRecovered;
                model.TomorrowText = OfficeCampaignResult.NextDayTease;
                return model;
            }

            OfficeCustomerState customer = state.Customers.ActiveDeskCustomer;
            if (customer != null)
            {
                model.CustomerCardVisible = true;
                model.CurrentCustomerPresentationFocusCount = 1;
                model.CustomerName = customer.DisplayName;
                model.CustomerProblem = customer.Problem;
                model.CustomerMood = "MOOD: " +
                    customer.VisibleMoodState.ToString().ToUpperInvariant();
                model.CaseCardVisible = true;
                ProjectCase(state, customer, model);
            }

            model.RuleCardVisible = state.AutomationRule.Unlocked ||
                state.PayrollRule.Unlocked;
            if (state.AutomationRule.Unlocked)
                model.RuleOneText = OfficeM6PlayerCopyCatalog.RuleStatus(
                    "AUTO SORTER", state.AutomationRule.Enabled) + "\n" +
                    OfficeM6PlayerCopyCatalog.RuleOne;
            if (state.PayrollRule.Unlocked)
                model.RuleTwoText = OfficeM6PlayerCopyCatalog.RuleStatus(
                    "PAY MACHINE", state.PayrollRule.Enabled) + "\n" +
                    OfficeM6PlayerCopyCatalog.RuleTwo;

            ProjectBreak(state, model);
            ProjectReadability(state, controlScheme, model);
            return model;
        }

        public Rect TopBarRect(int width, int height)
        {
            return new Rect(SafeMargin, SafeMargin,
                Mathf.Max(0f, width - SafeMargin * 2f), TopBarHeight);
        }

        public Rect CustomerCardRect(int width, int height)
        {
            return new Rect(SafeMargin, 80f,
                Mathf.Min(SideCardWidth, width - SafeMargin * 2f), 150f);
        }

        public Rect CaseCardRect(int width, int height)
        {
            float cardWidth = Mathf.Min(SideCardWidth, width - SafeMargin * 2f);
            return new Rect(width - cardWidth - SafeMargin, 80f,
                cardWidth, 132f);
        }

        public Rect RuleCardRect(int width, int height)
        {
            float cardWidth = Mathf.Min(SideCardWidth, width - SafeMargin * 2f);
            return new Rect(width - cardWidth - SafeMargin, 226f,
                cardWidth, 94f);
        }

        public Rect BreakCardRect(int width, int height)
        {
            float cardWidth = Mathf.Min(SideCardWidth, width - SafeMargin * 2f);
            return new Rect(width - cardWidth - SafeMargin,
                height - 194f - SafeMargin, cardWidth, 194f);
        }

        public Rect ActionRect(int width, int height)
        {
            float cardWidth = Mathf.Min(ActionWidth, width - SafeMargin * 2f);
            return new Rect(SafeMargin, height - ActionHeight - SafeMargin,
                cardWidth, ActionHeight);
        }

        public Rect ResultRect(int width, int height)
        {
            float cardWidth = Mathf.Min(680f, width - SafeMargin * 2f);
            float cardHeight = Mathf.Min(520f, height - SafeMargin * 2f);
            return new Rect((width - cardWidth) * 0.5f,
                (height - cardHeight) * 0.5f, cardWidth, cardHeight);
        }

        public Rect TutorialRect(int width, int height)
        {
            float cardWidth = Mathf.Min(560f, width - SafeMargin * 2f);
            return new Rect((width - cardWidth) * 0.5f, 80f,
                cardWidth, 58f);
        }

        public Rect WhatHappenedRect(int width, int height)
        {
            float cardWidth = Mathf.Min(600f, width - SafeMargin * 2f);
            const float cardHeight = 220f;
            return new Rect((width - cardWidth) * 0.5f,
                (height - cardHeight) * 0.5f, cardWidth, cardHeight);
        }

        public Rect CustomerPortraitRect(int width, int height)
        {
            Rect card = CustomerCardRect(width, height);
            return new Rect(card.xMax - 78f, card.y + 38f, 64f, 64f);
        }

        public bool Fits(int width, int height)
        {
            Rect[] rects =
            {
                TopBarRect(width, height),
                CustomerCardRect(width, height),
                CaseCardRect(width, height),
                RuleCardRect(width, height),
                BreakCardRect(width, height),
                ActionRect(width, height),
                ResultRect(width, height),
                TutorialRect(width, height),
                WhatHappenedRect(width, height),
            };
            for (int i = 0; i < rects.Length; i++)
                if (!Inside(rects[i], width, height)) return false;
            return true;
        }

        public bool FitsAtTextScale(int width, int height, float textScale)
        {
            return textScale >= 0.85f && textScale <= 1.3f &&
                Fits(width, height) &&
                ActionRect(width, height).height >= 96f * textScale &&
                BreakCardRect(width, height).height >= 148f * textScale;
        }

        public bool CriticalTargetsRemainVisible(
            OfficeM6HudModel model,
            int width,
            int height)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            if (model.ResultVisible) return true;
            var visible = new List<Rect>(6)
            {
                TopBarRect(width, height),
                ActionRect(width, height),
            };
            if (model.CustomerCardVisible)
                visible.Add(CustomerCardRect(width, height));
            if (model.CaseCardVisible)
                visible.Add(CaseCardRect(width, height));
            if (model.RuleCardVisible)
                visible.Add(RuleCardRect(width, height));
            if (model.BreakCardVisible)
                visible.Add(BreakCardRect(width, height));
            if (model.TutorialVisible)
                visible.Add(TutorialRect(width, height));

            Vector2[] targets =
            {
                new(width * 0.22f, height * 0.34f),
                new(width * 0.77f, height * 0.31f),
                new(width * 0.78f, height * 0.70f),
                new(width * 0.54f, height * 0.52f),
            };
            for (int targetIndex = 0; targetIndex < targets.Length; targetIndex++)
                for (int rectIndex = 0; rectIndex < visible.Count; rectIndex++)
                    if (visible[rectIndex].Contains(targets[targetIndex]))
                        return false;
            return true;
        }

        private static void ProjectCase(
            OfficeSimulationState state,
            OfficeCustomerState customer,
            OfficeM6HudModel model)
        {
            string caseId = customer.LinkedAutomationClaimId;
            OfficeCaseWorkRecord record = state.ManualTasks.RecordFor(caseId);
            if (record == null)
            {
                model.WhatWeKnow = "THE FILE IS READY.";
            }
            else if (record.TraceComplete)
            {
                model.WhatWeKnow = record.TraceCorrect
                    ? "PAPERS CHECKED. MONEY FOUND."
                    : "PAPERS CHECKED. MONEY ROUTE FAILED.";
            }
            else if (record.CompareComplete)
            {
                model.WhatWeKnow = record.CompareCorrect
                    ? "THE PAPERS MATCH."
                    : "THE PAPERS DO NOT MATCH.";
            }
            else
            {
                model.WhatWeKnow = "THE FILE HAS NOT BEEN CHECKED.";
            }

            OfficeManualTaskKind? next = state.ManualTasks.NextRequiredTask(caseId);
            model.WhatNeedsChecking = next switch
            {
                OfficeManualTaskKind.Compare => "NEEDS: CHECK PAPERS",
                OfficeManualTaskKind.Trace => "NEEDS: TRACE MONEY",
                OfficeManualTaskKind.WeirdCheck => "NEEDS: CHECK THE ODD PART",
                _ => "NEEDS: A DECISION",
            };
            model.NextUsefulAction = "NEXT: " +
                OfficeM6PlayerCopyCatalog.Action(state.PrimaryActionLabel);
            model.ManualChoicesVisible = state.ManualTasks.IsActive;

            OfficeFolderState folder = state.Queues.GetFolder(caseId);
            model.DecisionChoicesVisible = next == null &&
                state.Decisions.RecordFor(caseId) == null &&
                folder != null && !folder.IsMoving &&
                folder.OwnerKind == OfficeFolderOwnerKind.RoomQueue &&
                folder.CurrentRoom == OfficeRoomId.FrontDesk;
        }

        private static void ProjectBreak(
            OfficeSimulationState state,
            OfficeM6HudModel model)
        {
            if (state.PromotionCascade.Active &&
                !state.PromotionCascade.Recovered)
            {
                model.BreakCardVisible = true;
                model.BreakTitle = OfficeM6PlayerCopyCatalog.FixTheMess;
                model.BreakCause = OfficeM6PlayerCopyCatalog.PromotionCause;
                model.RecoveryItems = new[]
                {
                    Item(!state.PromotionCascade.CopierActive, "STOP COPIER"),
                    Item(!state.PromotionCascade.SupervisorStampActive, "REMOVE STAMP"),
                    Item(state.PromotionCascade.ActivePromotionFormCount == 0, "CLEAR FORMS"),
                    Item(state.PromotionCascade.MaraCalmed, "CALM MARA"),
                    Item(state.PromotionCascade.OriginalBadgeReturned, "RETURN ORIGINAL"),
                    Item(state.PromotionCascade.RunnerReassigned, "REASSIGN RUNNER"),
                };
                return;
            }
            if (state.GhostClock.Active && !state.GhostClock.Recovered)
            {
                model.BreakCardVisible = true;
                model.BreakTitle = OfficeM6PlayerCopyCatalog.FixTheMess;
                model.BreakCause = OfficeM6PlayerCopyCatalog.GhostClockCause;
                model.RecoveryItems = new[]
                {
                    Item(!state.GhostClock.ClockTerminalActive, "STOP CLOCK"),
                    Item(state.GhostClock.ActiveSlipCount == 0, "CLEAR TIME SLIPS"),
                };
                return;
            }
            if (state.MissingRoomAccess.Active &&
                !state.MissingRoomAccess.Recovered)
            {
                model.BreakCardVisible = true;
                model.BreakTitle = OfficeM6PlayerCopyCatalog.FixTheMess;
                model.BreakCause = OfficeM6PlayerCopyCatalog.MissingRoomCause;
                model.RecoveryItems = new[]
                {
                    Item(!state.MissingRoomAccess.DoorOpen, "CLOSE MISSING ROOM"),
                    Item(state.MissingRoomAccess.Recovered, "HELP IRIS"),
                };
                return;
            }
            if (state.BreakState.Active && !state.BreakState.Recovered)
            {
                model.BreakCardVisible = true;
                model.BreakTitle = OfficeM6PlayerCopyCatalog.FixTheMess;
                model.BreakCause = OfficeM6PlayerCopyCatalog.CopyEchoCause;
                model.RecoveryItems = new[]
                {
                    Item(!state.BreakState.CopierActive, "STOP COPIER"),
                    Item(state.Queues.ActiveCopyCount == 0, "CLEAR COPIES"),
                    Item(state.BreakState.OriginalFound, "FIND ORIGINAL"),
                };
            }
        }

        private void ProjectReadability(
            OfficeSimulationState state,
            OfficeM6ControlScheme controlScheme,
            OfficeM6HudModel model)
        {
            if (state.Carry.IsCarrying)
            {
                model.CarriedFileVisible = true;
                model.CarriedFileText = "CARRIED FILE - TAB MARK";
            }
            if (state.Queues.ActiveCopyCount > 0 ||
                state.PromotionCascade.PromotionFormIds.Count > 0 ||
                state.GhostClock.ActiveSlipCount > 0)
                model.OriginalCopyLegend =
                    "ORIGINAL: TAB MARK   COPY: STRIPED MARK";

            if (state.PromotionCascade.Active &&
                !state.PromotionCascade.Recovered)
                model.ActionableProblemRoom =
                    "GO TO: WEIRD ROOM COPIER AND STAMP";
            else if (state.GhostClock.Active && !state.GhostClock.Recovered)
                model.ActionableProblemRoom = "GO TO: PAPER ROOM CLOCK";
            else if (state.MissingRoomAccess.Active &&
                !state.MissingRoomAccess.Recovered)
                model.ActionableProblemRoom = "GO TO: WEIRD ROOM DOOR";
            else if (state.BreakState.Active && !state.BreakState.Recovered)
                model.ActionableProblemRoom = "GO TO: WEIRD ROOM COPIER";

            model.WhatHappenedAvailable =
                state.BreakState.Active || state.BreakState.Recovered ||
                state.GhostClock.HasTriggered ||
                state.MissingRoomAccess.HasTriggered ||
                state.PromotionCascade.HasTriggered;
            if (!model.WhatHappenedAvailable)
            {
                WhatHappenedOpen = false;
                return;
            }
            string button = controlScheme == OfficeM6ControlScheme.Controller
                ? "Y" : "H";
            model.WhatHappenedPrompt = button + " - " +
                OfficeM6PlayerCopyCatalog.WhatHappenedTitle;
            model.WhatHappenedVisible = WhatHappenedOpen;
            model.WhatHappenedText =
                OfficeM6PlayerCopyCatalog.WhatHappened(state);
        }

        private static OfficeM6DangerState DangerState(
            OfficeSimulationState state,
            OfficeCampaignState campaign)
        {
            if (campaign.IsComplete ||
                state.Shift.Phase == OfficeShiftPhase.Result)
                return OfficeM6DangerState.Result;
            if ((state.BreakState.Active && !state.BreakState.Recovered) ||
                (state.GhostClock.Active && !state.GhostClock.Recovered) ||
                (state.MissingRoomAccess.Active && !state.MissingRoomAccess.Recovered) ||
                (state.PromotionCascade.Active && !state.PromotionCascade.Recovered))
                return OfficeM6DangerState.Break;
            if (state.BreakState.Recovered || state.GhostClock.Recovered ||
                state.MissingRoomAccess.Recovered ||
                state.PromotionCascade.Recovered)
                return OfficeM6DangerState.Recovery;
            if (state.Customers.ActiveDeskCustomer?.VisibleMoodState >=
                OfficeVisibleMoodState.Worried)
                return OfficeM6DangerState.Rush;
            return OfficeM6DangerState.Calm;
        }

        private static int WaitingCount(OfficeSimulationState state)
        {
            int count = 0;
            IReadOnlyList<OfficeCustomerState> customers = state.Customers.Customers;
            for (int i = 0; i < customers.Count; i++)
                if (customers[i].QueueState == OfficeCustomerQueueState.Waiting ||
                    customers[i].QueueState == OfficeCustomerQueueState.AtDesk)
                    count++;
            return count;
        }

        private static string FormatTime(long tick)
        {
            long seconds = Math.Max(0L, tick) / 30L;
            return "TIME " + (seconds / 60L).ToString("D2") + ":" +
                (seconds % 60L).ToString("D2");
        }

        private static string Item(bool complete, string label)
        {
            return (complete ? "DONE - " : "TODO - ") + label;
        }

        private static bool Inside(Rect rect, int width, int height)
        {
            return rect.xMin >= 0f && rect.yMin >= 0f &&
                rect.xMax <= width && rect.yMax <= height;
        }
    }
}
