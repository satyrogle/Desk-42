using System;

namespace Desk42.Product.OfficeSlice
{
    /// <summary>Converts observable state changes into presentation-only cue IDs.</summary>
    public sealed class OfficeAudioEventRouter
    {
        private OfficeAudioStateSnapshot _previous;

        public void Reset(OfficeAudioStateSnapshot baseline)
        {
            _previous = baseline;
        }

        public void Route(
            OfficeAudioStateSnapshot current,
            OfficeSimulationState state,
            Action<string, float> emit)
        {
            if (current == null || state == null || emit == null) return;
            if (_previous == null)
            {
                _previous = current;
                return;
            }

            RouteCommands(current, state, emit);
            RouteAutomation(current, state, emit);
            RouteMood(current, emit);
            RouteMajorEvents(current, emit);
            _previous = current;
        }

        public static string CueForCommand(OfficeCommand command)
        {
            if (command == null) return string.Empty;
            return command.Kind switch
            {
                OfficeCommandKind.Move => command.Tick % 12 < 6
                    ? "warden.step.a" : "warden.step.b",
                OfficeCommandKind.Carry => "folder.take",
                OfficeCommandKind.Drop => "folder.drop",
                OfficeCommandKind.Send => "folder.send",
                OfficeCommandKind.Interact => "action.interact",
                OfficeCommandKind.StartWork =>
                    command.Arg0 == (int)OfficeManualTaskKind.Compare
                        ? "paper.open" : "money.open",
                OfficeCommandKind.SubmitWorkChoice => "paper.selection",
                OfficeCommandKind.Help => "help.start",
                OfficeCommandKind.Calm => "calm.start",
                OfficeCommandKind.Fix => "fix.start",
                OfficeCommandKind.Decide => "decision.stamp",
                OfficeCommandKind.ChooseUpgrade => "event.upgrade-chosen",
                OfficeCommandKind.ContinueToNextShift => "choice.confirm",
                OfficeCommandKind.RemoveSupervisorStamp =>
                    "event.supervisor-removed",
                OfficeCommandKind.ReassignRunner => "event.runner-allegiance",
                _ => string.Empty,
            };
        }

        public static string CueForManualResult(
            OfficeManualTaskKind kind,
            bool correct)
        {
            return kind == OfficeManualTaskKind.Trace
                ? correct ? "money.correct" : "money.incorrect"
                : correct ? "paper.correct" : "paper.incorrect";
        }

        public static string CueForMachine(string machineId, string state)
        {
            if (string.IsNullOrWhiteSpace(machineId) ||
                string.IsNullOrWhiteSpace(state)) return string.Empty;
            return "machine." + machineId + "." + state.ToLowerInvariant();
        }

        private void RouteCommands(
            OfficeAudioStateSnapshot current,
            OfficeSimulationState state,
            Action<string, float> emit)
        {
            for (int i = _previous.CommandCount;
                i < current.CommandCount && i < state.CommandLog.Commands.Count; i++)
            {
                OfficeCommand command = state.CommandLog.Commands[i];
                string cue = CueForCommand(command);
                if (command.Kind == OfficeCommandKind.Move && command.Tick % 6 != 0)
                    cue = string.Empty;
                if (command.Kind == OfficeCommandKind.ToggleRule)
                    cue = current.AutomationEnabled
                        ? "automation.enabled" : "automation.disabled";
                else if (command.Kind == OfficeCommandKind.ToggleRule2)
                    cue = current.PayrollEnabled
                        ? "automation.enabled" : "automation.disabled";
                else if (command.Kind == OfficeCommandKind.SubmitWorkChoice &&
                    !string.IsNullOrWhiteSpace(_previous.ActiveManualCaseId))
                {
                    OfficeCaseWorkRecord record = state.ManualTasks.RecordFor(
                        _previous.ActiveManualCaseId);
                    bool correct = _previous.ActiveManualKind ==
                        OfficeManualTaskKind.Trace
                            ? record.TraceCorrect : record.CompareCorrect;
                    cue = CueForManualResult(_previous.ActiveManualKind, correct);
                }
                if (!string.IsNullOrEmpty(cue)) emit(cue, 1f);
            }
        }

        private void RouteAutomation(
            OfficeAudioStateSnapshot current,
            OfficeSimulationState state,
            Action<string, float> emit)
        {
            for (int i = _previous.AutomationMatchCount;
                i < current.AutomationMatchCount; i++)
            {
                OfficeAutomationRuleMatch match = state.AutomationRule.Matches[i];
                emit(match.Matched
                    ? string.IsNullOrEmpty(state.AutomationRule.LastAcceptedCopyId)
                        ? "automation.match" : "automation.copied-accepted"
                    : "automation.reject", 1f);
            }
            for (int i = _previous.PayrollMatchCount;
                i < current.PayrollMatchCount; i++)
            {
                OfficePayrollRuleMatch match = state.PayrollRule.Matches[i];
                emit(match.Matched ? "automation.second-rule-match" :
                    "automation.reject", 1f);
            }
        }

        private void RouteMood(
            OfficeAudioStateSnapshot current,
            Action<string, float> emit)
        {
            if (!current.HasActiveCustomer ||
                (!_previous.HasActiveCustomer && current.HasActiveCustomer)) return;
            if (current.ActiveCustomerMood == _previous.ActiveCustomerMood) return;
            if (current.ActiveCustomerMood > _previous.ActiveCustomerMood)
            {
                string cue = current.ActiveCustomerMood switch
                {
                    OfficeVisibleMoodState.Strange => "customer.strange",
                    OfficeVisibleMoodState.Upset => "customer.upset",
                    _ => "customer.worried",
                };
                emit(cue, 1f);
            }
            else
                emit("customer.calm-response", 1f);
        }

        private void RouteMajorEvents(
            OfficeAudioStateSnapshot current,
            Action<string, float> emit)
        {
            if (!_previous.CopyEchoActive && current.CopyEchoActive)
                emit("event.copy-echo-trigger", 1f);
            if (current.ActiveCopyCount > _previous.ActiveCopyCount)
                emit("event.copy-spawn", 1f);
            if (current.ClearedCopyCount > _previous.ClearedCopyCount)
                emit("event.copy-clear", 1f);
            if (!current.CopyEchoActive && _previous.CopyEchoActive &&
                current.CopyEchoRecovered)
                emit("event.recovery-complete", 1f);
            if (!_previous.GhostClockActive && current.GhostClockActive)
                emit("event.ghost-clock", 1f);
            if (!_previous.MissingRoomActive && current.MissingRoomActive)
                emit("event.missing-room", 1f);
            if (!_previous.PromotionActive && current.PromotionActive)
            {
                emit("event.promotion-trigger", 1f);
                emit("event.copier-promoted", 0.92f);
            }
            if (current.ActivePromotionFormCount >
                _previous.ActivePromotionFormCount)
                emit("automation.copied-accepted", 0.9f);
            if (_previous.SupervisorStampActive &&
                !current.SupervisorStampActive)
                emit("event.supervisor-removed", 1f);
            if (!_previous.RunnerReassigned && current.RunnerReassigned)
                emit("event.runner-allegiance", 1f);
            if (!_previous.PromotionRecovered && current.PromotionRecovered)
                emit("event.recovery-complete", 1f);
            if (!_previous.ShiftResult && current.ShiftResult)
                emit("event.shift-close", 1f);
            if (!_previous.CampaignComplete && current.CampaignComplete)
            {
                emit("event.final-result", 1f);
                emit("event.next-day-tease", 0.72f);
            }
        }
    }
}
