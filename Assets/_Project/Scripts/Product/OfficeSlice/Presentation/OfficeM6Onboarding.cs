using System;
using System.Collections.Generic;

namespace Desk42.Product.OfficeSlice
{
    public enum OfficeM6TutorialStep
    {
        Move,
        TakeFile,
        SendFile,
        CheckPapers,
        TraceMoney,
        Decide,
        Calm,
        EnableAutoSorter,
        RespondToBreak,
        Recover,
        Complete,
    }

    /// <summary>
    /// Product-only guidance that observes authoritative commands without
    /// generating commands or mutating campaign state.
    /// </summary>
    public sealed class OfficeM6Onboarding
    {
        private int _observedCommandCount;
        private OfficeManualTaskKind? _observedWorkKind;

        public OfficeM6Onboarding(bool completedPreviously = false)
        {
            Step = completedPreviously
                ? OfficeM6TutorialStep.Complete
                : OfficeM6TutorialStep.Move;
        }

        public OfficeM6TutorialStep Step { get; private set; }
        public bool HintsEnabled { get; private set; } = true;
        public bool Complete => Step == OfficeM6TutorialStep.Complete;
        public bool Visible => HintsEnabled && !Complete;
        public string CurrentSentence => Visible ? SentenceFor(Step) : string.Empty;
        public string HighlightId => Visible ? HighlightFor(Step) : string.Empty;

        public void SetHintsEnabled(bool enabled)
        {
            HintsEnabled = enabled;
        }

        public void Observe(
            OfficeSimulationState state,
            OfficeCampaignState campaign)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (Complete || campaign.CurrentShiftOrdinal != 1) return;

            IReadOnlyList<OfficeCommand> commands = state.CommandLog.Commands;
            for (int i = _observedCommandCount; i < commands.Count; i++)
            {
                OfficeCommand command = commands[i];
                if (command.Tick > state.CurrentTick) break;
                _observedCommandCount = i + 1;
                if (!CommandSucceeded(state, command)) continue;
                if (command.Kind == OfficeCommandKind.StartWork)
                    _observedWorkKind = (OfficeManualTaskKind)command.Arg0;
                ObserveCommand(command, state);
            }

            if (Step == OfficeM6TutorialStep.EnableAutoSorter &&
                state.AutomationRule.Enabled)
                Advance();
            if (Step == OfficeM6TutorialStep.RespondToBreak &&
                !AnyBreakActive(state))
                return;
            if (Step == OfficeM6TutorialStep.Recover && AnyBreakRecovered(state))
                Step = OfficeM6TutorialStep.Complete;
        }

        public void ApplyTo(OfficeM6HudModel model)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            model.TutorialVisible = Visible;
            model.TutorialText = CurrentSentence;
            model.TutorialHighlightId = HighlightId;
        }

        private void ObserveCommand(
            OfficeCommand command,
            OfficeSimulationState state)
        {
            switch (Step)
            {
                case OfficeM6TutorialStep.Move:
                    if (command.Kind == OfficeCommandKind.Move)
                        Advance();
                    break;
                case OfficeM6TutorialStep.TakeFile:
                    if (command.Kind == OfficeCommandKind.Carry)
                        Advance();
                    break;
                case OfficeM6TutorialStep.SendFile:
                    if (command.Kind == OfficeCommandKind.Send)
                        Advance();
                    break;
                case OfficeM6TutorialStep.CheckPapers:
                    if (command.Kind == OfficeCommandKind.SubmitWorkChoice &&
                        _observedWorkKind == OfficeManualTaskKind.Compare)
                        Advance();
                    break;
                case OfficeM6TutorialStep.TraceMoney:
                    if (command.Kind == OfficeCommandKind.SubmitWorkChoice &&
                        _observedWorkKind == OfficeManualTaskKind.Trace)
                        Advance();
                    break;
                case OfficeM6TutorialStep.Decide:
                    if (command.Kind == OfficeCommandKind.Decide)
                        Advance();
                    break;
                case OfficeM6TutorialStep.Calm:
                    if (command.Kind == OfficeCommandKind.Calm)
                        Advance();
                    break;
                case OfficeM6TutorialStep.EnableAutoSorter:
                    if (command.Kind == OfficeCommandKind.ToggleRule)
                        Advance();
                    break;
                case OfficeM6TutorialStep.RespondToBreak:
                    if ((AnyBreakActive(state) || AnyBreakRecovered(state)) &&
                        IsRecoveryAction(command.Kind))
                        Advance();
                    break;
                case OfficeM6TutorialStep.Recover:
                    if (AnyBreakRecovered(state))
                        Advance();
                    break;
            }
        }

        private void Advance()
        {
            if (Step < OfficeM6TutorialStep.Complete)
                Step = (OfficeM6TutorialStep)((int)Step + 1);
        }

        private static bool IsRecoveryAction(OfficeCommandKind kind)
        {
            return kind == OfficeCommandKind.Fix ||
                kind == OfficeCommandKind.Calm ||
                kind == OfficeCommandKind.Drop ||
                kind == OfficeCommandKind.Interact ||
                kind == OfficeCommandKind.RemoveSupervisorStamp ||
                kind == OfficeCommandKind.ReassignRunner;
        }

        private static bool CommandSucceeded(
            OfficeSimulationState state,
            OfficeCommand command)
        {
            IReadOnlyList<OfficeCommandFailure> failures = state.Failures;
            for (int i = 0; i < failures.Count; i++)
                if (failures[i].Sequence == command.Sequence)
                    return false;
            return true;
        }

        private static bool AnyBreakActive(OfficeSimulationState state)
        {
            return (state.BreakState.Active && !state.BreakState.Recovered) ||
                (state.GhostClock.Active && !state.GhostClock.Recovered) ||
                (state.MissingRoomAccess.Active &&
                    !state.MissingRoomAccess.Recovered) ||
                (state.PromotionCascade.Active &&
                    !state.PromotionCascade.Recovered);
        }

        private static bool AnyBreakRecovered(OfficeSimulationState state)
        {
            return state.BreakState.Recovered || state.GhostClock.Recovered ||
                state.MissingRoomAccess.Recovered ||
                state.PromotionCascade.Recovered;
        }

        private static string SentenceFor(OfficeM6TutorialStep step)
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

        private static string HighlightFor(OfficeM6TutorialStep step)
        {
            return step switch
            {
                OfficeM6TutorialStep.Move => "warden",
                OfficeM6TutorialStep.TakeFile => "front-desk.interact",
                OfficeM6TutorialStep.SendFile => "paper-room.interact",
                OfficeM6TutorialStep.CheckPapers => "paper-room.interact",
                OfficeM6TutorialStep.TraceMoney => "money-room.interact",
                OfficeM6TutorialStep.Decide => "front-desk.interact",
                OfficeM6TutorialStep.Calm => "active-customer",
                OfficeM6TutorialStep.EnableAutoSorter => "auto-sorter",
                OfficeM6TutorialStep.RespondToBreak => "copy-echo",
                OfficeM6TutorialStep.Recover => "break-card",
                _ => string.Empty,
            };
        }
    }
}
