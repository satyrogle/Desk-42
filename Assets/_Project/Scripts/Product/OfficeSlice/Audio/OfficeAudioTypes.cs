using System;
using UnityEngine;

namespace Desk42.Product.OfficeSlice
{
    public enum OfficeAudioMixState
    {
        Calm,
        Rush,
        Break,
        Recovery,
        Result,
    }

    [Serializable]
    public sealed class OfficeAudioManifest
    {
        public string schema;
        public string workflow_version;
        public string through_gate;
        public OfficeAudioAssetRecord[] assets = Array.Empty<OfficeAudioAssetRecord>();
        public OfficeAudioCueRecord[] cues = Array.Empty<OfficeAudioCueRecord>();
    }

    [Serializable]
    public sealed class OfficeAudioAssetRecord
    {
        public string asset_id;
        public string resource_path;
        public string runtime_filename;
        public string category;
        public int channels;
        public int sample_rate;
        public int bit_depth;
        public float duration_seconds;
        public bool loop;
        public string final_sha256;
    }

    [Serializable]
    public sealed class OfficeAudioCueRecord
    {
        public string stage;
        public string cue_id;
        public string asset_id;
        public string bus;
        public bool loop;
        public float pan;
        public float base_volume;
    }

    /// <summary>Product-owned presentation settings, separate from all saves.</summary>
    public sealed class OfficeAudioSettings
    {
        private const string Prefix = "desk42.office-slice.presentation.";

        public float Master { get; private set; } = 0.80f;
        public float Music { get; private set; } = 0.55f;
        public float Sfx { get; private set; } = 0.82f;
        public float Ambience { get; private set; } = 0.58f;
        public bool Rumble { get; private set; } = true;
        public bool ReducedFlash { get; private set; }
        public bool AudioEnabled { get; private set; } = true;
        public bool FeedbackEnabled { get; private set; } = true;

        public bool Muted => !AudioEnabled || Master <= 0.0001f;

        public void SetVolumes(float master, float music, float sfx, float ambience)
        {
            Master = Mathf.Clamp01(master);
            Music = Mathf.Clamp01(music);
            Sfx = Mathf.Clamp01(sfx);
            Ambience = Mathf.Clamp01(ambience);
        }

        public void SetRumble(bool enabled) => Rumble = enabled;
        public void SetReducedFlash(bool enabled) => ReducedFlash = enabled;
        public void SetAudioEnabled(bool enabled) => AudioEnabled = enabled;
        public void SetFeedbackEnabled(bool enabled) => FeedbackEnabled = enabled;

        public float BusGain(string bus)
        {
            if (Muted) return 0f;
            float category = string.Equals(bus, "Music", StringComparison.Ordinal)
                ? Music
                : string.Equals(bus, "Ambience", StringComparison.Ordinal)
                    ? Ambience
                    : Sfx;
            return Master * category;
        }

        public void ApplyCommandLine(string[] arguments)
        {
            if (HasArgument(arguments, "--desk42-office-slice-audio-muted"))
                SetAudioEnabled(false);
            if (HasArgument(arguments, "--desk42-office-slice-feedback-disabled"))
                SetFeedbackEnabled(false);
            if (HasArgument(arguments, "--desk42-office-slice-rumble-disabled"))
                SetRumble(false);
            if (HasArgument(arguments, "--desk42-office-slice-reduced-flash"))
                SetReducedFlash(true);
        }

        public void Save()
        {
            PlayerPrefs.SetFloat(Prefix + "master", Master);
            PlayerPrefs.SetFloat(Prefix + "music", Music);
            PlayerPrefs.SetFloat(Prefix + "sfx", Sfx);
            PlayerPrefs.SetFloat(Prefix + "ambience", Ambience);
            PlayerPrefs.SetInt(Prefix + "rumble", Rumble ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + "reduced-flash", ReducedFlash ? 1 : 0);
            PlayerPrefs.Save();
        }

        public static OfficeAudioSettings Load()
        {
            var settings = new OfficeAudioSettings();
            settings.SetVolumes(
                PlayerPrefs.GetFloat(Prefix + "master", settings.Master),
                PlayerPrefs.GetFloat(Prefix + "music", settings.Music),
                PlayerPrefs.GetFloat(Prefix + "sfx", settings.Sfx),
                PlayerPrefs.GetFloat(Prefix + "ambience", settings.Ambience));
            settings.SetRumble(PlayerPrefs.GetInt(Prefix + "rumble", 1) != 0);
            settings.SetReducedFlash(
                PlayerPrefs.GetInt(Prefix + "reduced-flash", 0) != 0);
            return settings;
        }

        private static bool HasArgument(string[] arguments, string expected)
        {
            if (arguments == null) return false;
            for (int i = 0; i < arguments.Length; i++)
                if (string.Equals(arguments[i], expected,
                        StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }

    public sealed class OfficeAudioStateSnapshot
    {
        public long Tick { get; }
        public string SimulationChecksum { get; }
        public OfficeAudioMixState MixState { get; }
        public int ShiftOrdinal { get; }
        public int CommandCount { get; }
        public int AutomationMatchCount { get; }
        public int PayrollMatchCount { get; }
        public int ActiveCopyCount { get; }
        public int ClearedCopyCount { get; }
        public int ActivePromotionFormCount { get; }
        public int ClearedPromotionFormCount { get; }
        public OfficeVisibleMoodState ActiveCustomerMood { get; }
        public bool HasActiveCustomer { get; }
        public bool AutomationEnabled { get; }
        public bool PayrollEnabled { get; }
        public bool CopyEchoActive { get; }
        public bool CopyEchoRecovered { get; }
        public bool CopierActive { get; }
        public bool OriginalFound { get; }
        public bool GhostClockActive { get; }
        public bool GhostClockRecovered { get; }
        public bool MissingRoomActive { get; }
        public bool MissingRoomRecovered { get; }
        public bool PromotionActive { get; }
        public bool PromotionRecovered { get; }
        public bool PromotionCopierActive { get; }
        public bool PromotionOriginalFound { get; }
        public bool PromotionOriginalReturned { get; }
        public bool SupervisorStampActive { get; }
        public bool RunnerReassigned { get; }
        public bool RunnerFollowingCopier { get; }
        public bool CampaignComplete { get; }
        public bool ShiftResult { get; }
        public string ActiveManualCaseId { get; }
        public OfficeManualTaskKind ActiveManualKind { get; }
        public bool ManualTaskActive { get; }

        public OfficeAudioStateSnapshot(
            OfficeSimulationState state,
            OfficeCampaignState campaign,
            OfficeAudioMixState mixState)
        {
            Tick = state.CurrentTick;
            SimulationChecksum = state.Checksum;
            MixState = mixState;
            ShiftOrdinal = campaign?.CurrentShiftOrdinal ?? state.Shift.ShiftOrdinal;
            CommandCount = state.CommandLog.Commands.Count;
            AutomationMatchCount = state.AutomationRule.Matches.Count;
            PayrollMatchCount = state.PayrollRule.Matches.Count;
            ActiveCopyCount = state.Queues.ActiveCopyCount;
            ClearedCopyCount = state.BreakState.ClearedCopyCount;
            ActivePromotionFormCount = state.PromotionCascade.ActivePromotionFormCount;
            ClearedPromotionFormCount = state.PromotionCascade.ClearedPromotionFormCount;
            OfficeCustomerState active = state.Customers.ActiveDeskCustomer;
            HasActiveCustomer = active != null;
            ActiveCustomerMood = active?.VisibleMoodState ?? OfficeVisibleMoodState.Calm;
            AutomationEnabled = state.AutomationRule.Enabled;
            PayrollEnabled = state.PayrollRule.Enabled;
            CopyEchoActive = state.BreakState.Active && !state.BreakState.Recovered;
            CopyEchoRecovered = state.BreakState.Recovered;
            CopierActive = state.BreakState.CopierActive;
            OriginalFound = state.BreakState.OriginalFound;
            GhostClockActive = state.GhostClock.Active;
            GhostClockRecovered = state.GhostClock.Recovered;
            MissingRoomActive = state.MissingRoomAccess.Active;
            MissingRoomRecovered = state.MissingRoomAccess.Recovered;
            PromotionActive = state.PromotionCascade.Active;
            PromotionRecovered = state.PromotionCascade.Recovered;
            PromotionCopierActive = state.PromotionCascade.CopierActive;
            PromotionOriginalFound = state.PromotionCascade.OriginalBadgeFound;
            PromotionOriginalReturned = state.PromotionCascade.OriginalBadgeReturned;
            SupervisorStampActive = state.PromotionCascade.SupervisorStampActive;
            RunnerReassigned = state.PromotionCascade.RunnerReassigned;
            RunnerFollowingCopier = string.Equals(
                state.Staff.RunnerTaskSourceId,
                OfficeStaffSystem.CopierTaskSourceId,
                StringComparison.Ordinal);
            CampaignComplete = campaign?.IsComplete ?? false;
            ShiftResult = state.Shift.Phase == OfficeShiftPhase.Result;
            ActiveManualCaseId = state.ManualTasks.ActiveCaseId;
            ActiveManualKind = state.ManualTasks.ActiveKind;
            ManualTaskActive = state.ManualTasks.IsActive;
        }
    }

    /// <summary>Reads Office Slice state and never owns or mutates gameplay.</summary>
    public sealed class OfficeAudioStateProjector
    {
        public OfficeAudioStateSnapshot Project(
            OfficeSimulationState state,
            OfficeCampaignState campaign)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            return new OfficeAudioStateSnapshot(state, campaign, MixState(state, campaign));
        }

        public static OfficeAudioMixState MixState(
            OfficeSimulationState state,
            OfficeCampaignState campaign)
        {
            if (campaign?.IsComplete == true ||
                state.Shift.Phase == OfficeShiftPhase.Result)
                return OfficeAudioMixState.Result;
            if (state.BreakState.Active || state.GhostClock.Active ||
                state.MissingRoomAccess.Active || state.PromotionCascade.Active)
                return OfficeAudioMixState.Break;
            if (state.BreakState.Recovered || state.GhostClock.Recovered ||
                state.MissingRoomAccess.Recovered || state.PromotionCascade.Recovered)
                return OfficeAudioMixState.Recovery;
            if (state.Customers.ActiveDeskCustomer?.VisibleMoodState >=
                OfficeVisibleMoodState.Worried)
                return OfficeAudioMixState.Rush;
            return OfficeAudioMixState.Calm;
        }
    }
}
