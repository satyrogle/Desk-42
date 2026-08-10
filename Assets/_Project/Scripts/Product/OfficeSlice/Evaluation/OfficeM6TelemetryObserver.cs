using System;
using System.Collections.Generic;

namespace Desk42.Product.OfficeSlice
{
    /// <summary>Observes public product state and emits no gameplay commands.</summary>
    public sealed class OfficeM6TelemetryObserver
    {
        private readonly OfficeM6TelemetryRecorder _recorder;
        private int _observedCommandCount;
        private int _observedFailureCount;
        private int _automationMatchCount;
        private int _payrollMatchCount;
        private int _shift = -1;
        private long _lastTick = -1;
        private double _lastMeaningfulSeconds;
        private int _inactivityOrdinal;
        private string _lastFailureCode = string.Empty;
        private int _sameFailureCount;
        private OfficeM6TutorialStep? _lastTutorialStep;
        private bool _breakActive;
        private bool _breakRecovered;
        private bool _campaignComplete;
        private OfficeManualTaskKind? _activeWorkKind;
        private bool _firstMeaningfulInputRecorded;

        public OfficeM6TelemetryObserver(OfficeM6TelemetryRecorder recorder)
        {
            _recorder = recorder ??
                throw new ArgumentNullException(nameof(recorder));
        }

        public void Observe(
            OfficeSimulationState state,
            OfficeCampaignState campaign,
            OfficeM6Onboarding onboarding)
        {
            if (!_recorder.Enabled || state == null || campaign == null) return;
            if (_lastTick == state.CurrentTick &&
                _shift == campaign.CurrentShiftOrdinal) return;
            long previousTick = _lastTick;
            _lastTick = state.CurrentTick;

            if (_shift != campaign.CurrentShiftOrdinal)
            {
                if (_shift > 0)
                    _recorder.Record("shift_end", previousTick, _shift,
                        (previousTick /
                         (double)OfficeSimulationClock.TicksPerSecond)
                        .ToString("F3", System.Globalization.CultureInfo.InvariantCulture));
                _shift = campaign.CurrentShiftOrdinal;
                _observedCommandCount = 0;
                _observedFailureCount = 0;
                _automationMatchCount = 0;
                _payrollMatchCount = 0;
                _breakActive = false;
                _breakRecovered = false;
                _activeWorkKind = null;
                _lastMeaningfulSeconds = 0d;
                _inactivityOrdinal = 0;
                _recorder.Record(_shift == 1 ? "shift_start" :
                    "next_shift_started", state.CurrentTick, _shift);
                _recorder.Flush();
            }

            ObserveCommands(state, campaign);
            ObserveFailures(state);
            ObserveAutomation(state);
            ObserveBreaks(state);
            ObserveTutorial(state, onboarding);

            double elapsed = state.CurrentTick /
                (double)OfficeSimulationClock.TicksPerSecond;
            if (elapsed - _lastMeaningfulSeconds >= 10d &&
                _inactivityOrdinal == 0)
            {
                _inactivityOrdinal = 1;
                _recorder.Record("inactivity_10_seconds",
                    state.CurrentTick, _shift,
                    onboarding?.Step.ToString() ?? string.Empty);
            }

            if (campaign.IsComplete && !_campaignComplete)
            {
                _campaignComplete = true;
                _recorder.Record("shift_end", state.CurrentTick, _shift,
                    (state.CurrentTick /
                     (double)OfficeSimulationClock.TicksPerSecond)
                    .ToString("F3", System.Globalization.CultureInfo.InvariantCulture));
                _recorder.Record("campaign_completed",
                    state.CurrentTick, _shift, campaign.Checksum);
                _recorder.Flush();
            }
        }

        public void RecordPause(long tick, int shift)
        {
            _recorder.Record("pause", tick, shift);
            _recorder.Flush();
        }

        public void RecordWhatHappened(long tick, int shift)
        {
            _recorder.Record("what_happened_opened", tick, shift);
        }

        private void ObserveCommands(
            OfficeSimulationState state,
            OfficeCampaignState campaign)
        {
            IReadOnlyList<OfficeCommand> commands = state.CommandLog.Commands;
            for (int i = _observedCommandCount; i < commands.Count; i++)
            {
                OfficeCommand command = commands[i];
                if (command.Tick > state.CurrentTick) break;
                _observedCommandCount = i + 1;
                if (Failed(state, command.Sequence)) continue;
                _lastMeaningfulSeconds = state.CurrentTick /
                    (double)OfficeSimulationClock.TicksPerSecond;
                _inactivityOrdinal = 0;
                if (!_firstMeaningfulInputRecorded)
                {
                    _firstMeaningfulInputRecorded = true;
                    _recorder.Record("first_meaningful_input",
                        command.Tick, campaign.CurrentShiftOrdinal,
                        command.Kind.ToString());
                }
                switch (command.Kind)
                {
                    case OfficeCommandKind.Carry:
                        RecordFirst("first_folder_take", command, campaign);
                        break;
                    case OfficeCommandKind.StartWork:
                        _activeWorkKind = (OfficeManualTaskKind)command.Arg0;
                        _recorder.Record(command.Arg0 ==
                            (int)OfficeManualTaskKind.Compare
                                ? "paper_check_start"
                                : command.Arg0 == (int)OfficeManualTaskKind.Trace
                                    ? "money_trace_start" : "odd_check_start",
                            command.Tick, campaign.CurrentShiftOrdinal);
                        break;
                    case OfficeCommandKind.SubmitWorkChoice:
                        string complete = _activeWorkKind ==
                            OfficeManualTaskKind.Compare
                                ? "paper_check_complete"
                                : _activeWorkKind ==
                                    OfficeManualTaskKind.Trace
                                    ? "money_trace_complete"
                                    : "odd_check_complete";
                        _recorder.Record(complete, command.Tick,
                            campaign.CurrentShiftOrdinal);
                        _activeWorkKind = null;
                        break;
                    case OfficeCommandKind.Decide:
                        RecordFirst("first_decision", command, campaign);
                        break;
                    case OfficeCommandKind.Calm:
                        RecordFirst("first_calm", command, campaign);
                        break;
                    case OfficeCommandKind.ToggleRule:
                        _recorder.Record(state.AutomationRule.Enabled
                            ? "rule_enabled" : "rule_disabled",
                            command.Tick, campaign.CurrentShiftOrdinal);
                        break;
                    case OfficeCommandKind.ChooseUpgrade:
                        _recorder.Record("upgrade_selected", command.Tick,
                            campaign.CurrentShiftOrdinal, command.TextArg);
                        break;
                    case OfficeCommandKind.Restart:
                        _recorder.Record("restart", command.Tick,
                            campaign.CurrentShiftOrdinal);
                        _recorder.Flush();
                        break;
                }
            }
        }

        private void ObserveFailures(OfficeSimulationState state)
        {
            IReadOnlyList<OfficeCommandFailure> failures = state.Failures;
            for (int i = _observedFailureCount; i < failures.Count; i++)
            {
                OfficeCommandFailure failure = failures[i];
                _recorder.Record("invalid_action", failure.Tick, _shift,
                    failure.Code);
                if (string.Equals(_lastFailureCode, failure.Code,
                        StringComparison.Ordinal))
                    _sameFailureCount++;
                else
                {
                    _lastFailureCode = failure.Code;
                    _sameFailureCount = 1;
                }
                if (_sameFailureCount >= 2)
                    _recorder.Record("repeated_unsuccessful_action",
                        failure.Tick, _shift, failure.Code);
            }
            _observedFailureCount = failures.Count;
        }

        private void ObserveAutomation(OfficeSimulationState state)
        {
            if (state.AutomationRule.Matches.Count > _automationMatchCount)
            {
                _automationMatchCount = state.AutomationRule.Matches.Count;
                _recorder.Record("automation_match", state.CurrentTick, _shift);
            }
            if (state.PayrollRule.Matches.Count > _payrollMatchCount)
            {
                _payrollMatchCount = state.PayrollRule.Matches.Count;
                _recorder.Record("automation_match", state.CurrentTick, _shift,
                    "pay_machine");
            }
        }

        private void ObserveBreaks(OfficeSimulationState state)
        {
            bool active = AnyBreakActive(state);
            bool recovered = AnyBreakRecovered(state);
            if (active && !_breakActive)
                _recorder.Record("break_start", state.CurrentTick, _shift);
            if (recovered && !_breakRecovered)
            {
                _recorder.Record("break_recovery", state.CurrentTick, _shift);
                _recorder.Flush();
            }
            _breakActive = active;
            _breakRecovered = recovered;
        }

        private void ObserveTutorial(
            OfficeSimulationState state,
            OfficeM6Onboarding onboarding)
        {
            if (onboarding == null || !onboarding.Visible) return;
            if (_lastTutorialStep == onboarding.Step) return;
            _lastTutorialStep = onboarding.Step;
            _recorder.Record("tutorial_prompt", state.CurrentTick, _shift,
                onboarding.Step.ToString());
        }

        private void RecordFirst(
            string eventName,
            OfficeCommand command,
            OfficeCampaignState campaign)
        {
            for (int i = 0; i < _recorder.Events.Count; i++)
                if (_recorder.Events[i].EventName == eventName) return;
            _recorder.Record(eventName, command.Tick,
                campaign.CurrentShiftOrdinal);
        }

        private static bool Failed(OfficeSimulationState state, int sequence)
        {
            for (int i = 0; i < state.Failures.Count; i++)
                if (state.Failures[i].Sequence == sequence) return true;
            return false;
        }

        private static bool AnyBreakActive(OfficeSimulationState state) =>
            (state.BreakState.Active && !state.BreakState.Recovered) ||
            (state.GhostClock.Active && !state.GhostClock.Recovered) ||
            (state.MissingRoomAccess.Active &&
                !state.MissingRoomAccess.Recovered) ||
            (state.PromotionCascade.Active &&
                !state.PromotionCascade.Recovered);

        private static bool AnyBreakRecovered(OfficeSimulationState state) =>
            state.BreakState.Recovered || state.GhostClock.Recovered ||
            state.MissingRoomAccess.Recovered ||
            state.PromotionCascade.Recovered;
    }
}
