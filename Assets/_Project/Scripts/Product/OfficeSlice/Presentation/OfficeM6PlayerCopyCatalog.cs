using System;
using System.Collections.Generic;

namespace Desk42.Product.OfficeSlice
{
    public static class OfficeM6PlayerCopyCatalog
    {
        public const int ActionLabelCharacterBudget = 32;
        public const string DoThis = "DO THIS";
        public const string AtTheDesk = "AT THE DESK";
        public const string ThisFile = "THIS FILE";
        public const string YourMachines = "YOUR MACHINES";
        public const string FixTheMess = "FIX THE MESS";
        public const string WhatHappenedTitle = "WHAT HAPPENED?";
        public const string ThreeShiftResult = "THREE-SHIFT RESULT";
        public const string TutorialChoice =
            "1-4 - CHOOSE THE MATCHING RECORD";
        public const string DecisionChoice =
            "1-4 - CHOOSE WHAT HAPPENS TO THE FILE";
        public const string RuleOne = OfficeAutomationRuleState.PlayerRule;
        public const string RuleTwo = OfficePayrollRuleState.PlayerRule;
        public const string CopyEchoCause =
            "THE COPIER IS MAKING CONFIDENT COPIES.";
        public const string GhostClockCause =
            "THE CLOCK KEEPS MAKING EARLY FILES.";
        public const string MissingRoomCause =
            "AN OLD CARD OPENED A ROOM THAT ISN'T THERE.";
        public const string PromotionCause =
            "THE COPIER PROMOTED ITSELF.";

        public static readonly IReadOnlyList<string> BannedInternalTerms =
            Array.AsReadOnly(new[]
            {
                "institutional state",
                "causal propagation",
                "deterministic consequence",
                "authority state",
                "claim profile",
                "adjudication",
                "public boundary",
                "checkpoint schema",
                "simulation checksum",
            });

        public static readonly IReadOnlyList<string> CanonicalActionLabels =
            Array.AsReadOnly(new[]
            {
                "INTERACT", "TAKE FILE", "PUT DOWN", "CHECK PAPERS",
                "TRACE MONEY", "CHECK ODD PART", "SEND TO PAPER",
                "SEND TO MONEY", "SEND TO WEIRD", "SEND TO FRONT", "CALM",
                "HELP", "FIX COPIER", "CLEAR COPY", "STOP CLOCK",
                "CLEAR TIME SLIP", "CLOSE MISSING ROOM", "STOP COPIER",
                "REMOVE STAMP", "CLEAR PROMOTION FORM", "FIND ORIGINAL",
                "RETURN ORIGINAL", "REASSIGN RUNNER", "CHOOSE UPGRADE",
                "NEXT SHIFT", "MOVE CLOSER", "NOTHING HERE", "WAIT",
            });

        public static IReadOnlyList<string> StaticPlayerStrings =>
            Array.AsReadOnly(new[]
            {
                DoThis, AtTheDesk, ThisFile, YourMachines, FixTheMess,
                WhatHappenedTitle, ThreeShiftResult, TutorialChoice,
                DecisionChoice, RuleOne, RuleTwo,
                "THE FILE IS READY.", "PAPERS CHECKED. MONEY FOUND.",
                "PAPERS CHECKED. MONEY ROUTE FAILED.",
                "THE PAPERS MATCH.", "THE PAPERS DO NOT MATCH.",
                "THE FILE HAS NOT BEEN CHECKED.", "NEEDS: CHECK PAPERS",
                "NEEDS: TRACE MONEY", "NEEDS: CHECK THE ODD PART",
                "NEEDS: A DECISION", PromotionCause, GhostClockCause,
                MissingRoomCause, CopyEchoCause,
                TutorialSentence(OfficeM6TutorialStep.Move),
                TutorialSentence(OfficeM6TutorialStep.TakeFile),
                TutorialSentence(OfficeM6TutorialStep.SendFile),
                TutorialSentence(OfficeM6TutorialStep.CheckPapers),
                TutorialSentence(OfficeM6TutorialStep.TraceMoney),
                TutorialSentence(OfficeM6TutorialStep.Decide),
                TutorialSentence(OfficeM6TutorialStep.Calm),
                TutorialSentence(OfficeM6TutorialStep.EnableAutoSorter),
                TutorialSentence(OfficeM6TutorialStep.RespondToBreak),
                TutorialSentence(OfficeM6TutorialStep.Recover),
            });

        public static string Prompt(
            string authoritativeAction,
            OfficeM6ControlScheme controlScheme)
        {
            string button = controlScheme == OfficeM6ControlScheme.Controller
                ? "A" : "E";
            return button + " - " + Action(authoritativeAction);
        }

        public static string Action(string authoritativeAction)
        {
            if (string.IsNullOrWhiteSpace(authoritativeAction)) return "WAIT";
            if (authoritativeAction.StartsWith("SEND TO ",
                    StringComparison.Ordinal))
                return authoritativeAction;
            return authoritativeAction switch
            {
                "TAKE FOLDER" => "TAKE FILE",
                "FIX MACHINE" => "FIX COPIER",
                "FIX COPY" => "CLEAR COPY",
                "CHECK WEIRD STUFF" => "CHECK ODD PART",
                "REMOVE SUPERVISOR STAMP" => "REMOVE STAMP",
                "FIND ORIGINAL BADGE" => "FIND ORIGINAL",
                "CHOOSE AN OFFICE UPGRADE" => "CHOOSE UPGRADE",
                "MOVE TO A WORK POINT" => "MOVE CLOSER",
                "NOTHING TO DO HERE" => "NOTHING HERE",
                _ => authoritativeAction,
            };
        }

        public static string RuleStatus(string machine, bool enabled)
        {
            return machine + ": " + (enabled ? "ON" : "OFF");
        }

        public static string WhatHappened(OfficeSimulationState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (state.PromotionCascade.HasTriggered)
                return "A COPIED BADGE PASSED BOTH MACHINES. " +
                    "THE COPIER PROMOTED ITSELF.";
            if (state.GhostClock.HasTriggered)
                return "THE CLOCK MADE EARLY FILES FASTER THAN THE OFFICE " +
                    "COULD CHECK THEM.";
            if (state.MissingRoomAccess.HasTriggered)
                return "AN OLD ACCESS CARD OPENED A ROOM THAT ISN'T THERE.";
            if (state.BreakState.Active || state.BreakState.Recovered)
                return "THE AUTO SORTER SENT A COPY. THE COPIER KEPT COPYING IT.";
            return "THE OFFICE IS STILL WORKING.";
        }

        public static string TutorialSentence(OfficeM6TutorialStep step)
        {
            return step switch
            {
                OfficeM6TutorialStep.Move =>
                    "MOVE WITH WASD OR THE LEFT STICK.",
                OfficeM6TutorialStep.TakeFile =>
                    "GO TO THE FRONT DESK AND TAKE THE FILE.",
                OfficeM6TutorialStep.SendFile =>
                    "CARRY THE FILE TO THE ROOM THAT CAN CHECK IT.",
                OfficeM6TutorialStep.CheckPapers =>
                    "CHECK THE PAPERS AND PICK THE MATCHING RECORD.",
                OfficeM6TutorialStep.TraceMoney =>
                    "TRACE THE MONEY AND PICK WHERE IT WENT.",
                OfficeM6TutorialStep.Decide =>
                    "RETURN TO THE DESK AND DECIDE THE CASE.",
                OfficeM6TutorialStep.Calm =>
                    "CALM THE CUSTOMER WHEN THE WAIT GETS TO THEM.",
                OfficeM6TutorialStep.EnableAutoSorter =>
                    "TURN ON THE AUTO SORTER TO SEND EASY FILES.",
                OfficeM6TutorialStep.RespondToBreak =>
                    "THE COPIER GOT CONFIDENT; START FIXING THE MESS.",
                OfficeM6TutorialStep.Recover =>
                    "FINISH EVERY ITEM IN THE FIX THE MESS LIST.",
                _ => string.Empty,
            };
        }
    }
}
