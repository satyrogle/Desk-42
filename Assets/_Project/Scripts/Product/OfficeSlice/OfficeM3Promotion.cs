using System;
using System.Collections.Generic;
using System.Text;

namespace Desk42.Product.OfficeSlice
{
    public sealed class OfficePromotionCascadeState
    {
        public const int PromotionFormIntervalTicks = 45;
        public const int BaseMaximumActivePromotionForms = 6;
        public const int BaseCopyClearDurationTicks = 30;
        public const int BaseOriginalFindDurationTicks = 30;
        public const int FailureGraceTicks = 1800;

        private enum RecoveryChannelKind
        {
            None,
            ClearPromotionForm,
            FindOriginalBadge,
        }

        private readonly OfficeM2Scenario _scenario;
        private readonly OfficeQueueService _queues;
        private readonly OfficeBreakState _copyEcho;
        private readonly OfficeStaffSystem _staff;
        private readonly OfficeCampaignAutomationState _campaignRules;
        private readonly bool _lockedRuleOneCopyAccepted;
        private readonly bool _lockedRuleTwoCopyAccepted;
        private readonly List<string> _promotionFormIds = new();
        private readonly IReadOnlyList<string> _readOnlyPromotionFormIds;
        private readonly List<string> _divertedFolderIds = new();
        private readonly IReadOnlyList<string> _readOnlyDivertedFolderIds;
        private readonly HashSet<string> _divertedFolderSet =
            new(StringComparer.Ordinal);
        private readonly string _maraCaseId;
        private readonly string _maraCustomerId;
        private int _nextPromotionFormOrdinal = 1;
        private long _lastPromotionFormTick;
        private RecoveryChannelKind _channelKind;
        private string _channelFolderId = string.Empty;
        private int _channelRemainingTicks;

        public OfficePromotionCascadeState(
            OfficeM2Scenario scenario,
            OfficeQueueService queues,
            OfficeBreakState copyEcho,
            OfficeStaffSystem staff,
            OfficeCampaignUpgradeState upgrades = null,
            OfficeCampaignAutomationState campaignRules = null,
            bool ruleOneAcceptedCopiedRefund = false,
            bool ruleTwoAcceptedCopiedPayroll = false)
        {
            _scenario = scenario ?? throw new ArgumentNullException(nameof(scenario));
            _queues = queues ?? throw new ArgumentNullException(nameof(queues));
            _copyEcho = copyEcho ?? throw new ArgumentNullException(nameof(copyEcho));
            _staff = staff ?? throw new ArgumentNullException(nameof(staff));
            _campaignRules = campaignRules;
            _lockedRuleOneCopyAccepted = ruleOneAcceptedCopiedRefund;
            _lockedRuleTwoCopyAccepted = ruleTwoAcceptedCopiedPayroll;
            upgrades ??= new OfficeCampaignUpgradeState();
            CopyClearDurationTicks = Math.Max(
                1,
                BaseCopyClearDurationTicks - upgrades.CopyClearReductionTicks);
            OriginalFindDurationTicks = Math.Max(
                1,
                BaseOriginalFindDurationTicks -
                upgrades.OriginalFindReductionTicks);
            MaximumActivePromotionForms = Math.Max(
                1,
                BaseMaximumActivePromotionForms -
                upgrades.MaximumCopyReduction);
            _readOnlyPromotionFormIds = _promotionFormIds.AsReadOnly();
            _readOnlyDivertedFolderIds = _divertedFolderIds.AsReadOnly();
            for (int i = 0; i < scenario.Customers.Count; i++)
            {
                OfficeCustomerDefinition customer = scenario.Customers[i];
                if (!string.Equals(customer.DisplayName, "MARA VALE",
                        StringComparison.Ordinal)) continue;
                _maraCaseId = customer.LinkedAutomationClaimId;
                _maraCustomerId = customer.CustomerId;
                break;
            }
            if (string.IsNullOrWhiteSpace(_maraCaseId))
                throw new InvalidOperationException(
                    "The Promotion Cascade requires Mara Vale's authored case.");
        }

        public bool HasTriggered { get; private set; }
        public bool Active { get; private set; }
        public bool Recovered { get; private set; }
        public bool Failed { get; private set; }
        public string FailureReason { get; private set; } = string.Empty;
        public long TriggeredTick { get; private set; }
        public bool SupervisorStampActive { get; private set; }
        public bool MaraCalmed { get; private set; }
        public bool OriginalBadgeFound { get; private set; }
        public bool OriginalBadgeReturned { get; private set; }
        public bool RunnerReassigned { get; private set; }
        public int ClearedPromotionFormCount { get; private set; }
        public int CopyClearDurationTicks { get; }
        public int OriginalFindDurationTicks { get; }
        public int MaximumActivePromotionForms { get; }
        public string MaraCaseId => _maraCaseId;
        public string MaraCustomerId => _maraCustomerId;
        public bool CopierActive => _copyEcho.CopierActive;
        public bool RuleOneAcceptedCopiedRefund =>
            _campaignRules?.Rule1AcceptedCopiedRefund ??
            _lockedRuleOneCopyAccepted;
        public bool RuleTwoAcceptedCopiedPayroll =>
            _campaignRules?.Rule2AcceptedCopiedPayroll ??
            _lockedRuleTwoCopyAccepted;
        public IReadOnlyList<string> PromotionFormIds =>
            _readOnlyPromotionFormIds;
        public IReadOnlyList<string> DivertedFolderIds =>
            _readOnlyDivertedFolderIds;
        public bool RecoveryChannelActive =>
            _channelKind != RecoveryChannelKind.None;
        public int RecoveryChannelRemainingTicks => _channelRemainingTicks;
        public int ActivePromotionFormCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _promotionFormIds.Count; i++)
                {
                    OfficeFolderState folder = _queues.GetFolder(
                        _promotionFormIds[i]);
                    if (folder != null &&
                        folder.OwnerKind != OfficeFolderOwnerKind.Cleared) count++;
                }
                return count;
            }
        }

        public static bool ExactAuthoredConjunction(
            bool ruleOneAcceptedCopiedRefund,
            bool ruleTwoAcceptedCopiedPayroll,
            bool copyEchoMachineActive,
            bool copiedPromotionFormReachedMoney,
            OfficeVisibleMoodState maraMood)
        {
            return ruleOneAcceptedCopiedRefund &&
                ruleTwoAcceptedCopiedPayroll &&
                copyEchoMachineActive &&
                copiedPromotionFormReachedMoney &&
                maraMood >= OfficeVisibleMoodState.Upset;
        }

        public void AdvanceOneTick(
            long currentTick,
            OfficeCustomerScheduleState customers)
        {
            if (_scenario.ShiftOrdinal != 3 || Failed || Recovered) return;
            AdvanceRecoveryChannel();
            OfficeCustomerState mara = customers.CustomerForClaim(_maraCaseId);
            if (HasTriggered && mara != null &&
                mara.VisibleMoodState <= OfficeVisibleMoodState.Worried)
                MaraCalmed = true;
            ObserveOriginalReturned();

            if (_promotionFormIds.Count == 0 &&
                RuleOneAcceptedCopiedRefund &&
                RuleTwoAcceptedCopiedPayroll &&
                CopierActive && mara != null &&
                mara.QueueState == OfficeCustomerQueueState.AtDesk)
            {
                CreatePromotionForm(currentTick);
            }

            if (!HasTriggered && _promotionFormIds.Count > 0)
            {
                OfficeFolderState seed = _queues.GetFolder(_promotionFormIds[0]);
                if (seed != null && !seed.IsMoving &&
                    seed.OwnerKind == OfficeFolderOwnerKind.RoomQueue &&
                    seed.CurrentRoom == OfficeRoomId.WeirdRoom)
                    _queues.TryTransferCase(
                        seed.CaseId,
                        OfficeRoomId.MoneyRoom,
                        currentTick);
                bool reachedMoney = seed != null && !seed.IsMoving &&
                    seed.OwnerKind == OfficeFolderOwnerKind.RoomQueue &&
                    seed.CurrentRoom == OfficeRoomId.MoneyRoom;
                if (mara != null && ExactAuthoredConjunction(
                        RuleOneAcceptedCopiedRefund,
                        RuleTwoAcceptedCopiedPayroll,
                        CopierActive,
                        reachedMoney,
                        mara.VisibleMoodState))
                    Trigger(currentTick);
            }

            if (!Active) return;
            AssignNextCopierDiversion(currentTick);
            if (CopierActive &&
                currentTick - _lastPromotionFormTick >=
                    PromotionFormIntervalTicks &&
                ActivePromotionFormCount < MaximumActivePromotionForms)
                CreatePromotionForm(currentTick);
            ObserveRecovery();
            if (!Recovered && currentTick - TriggeredTick > FailureGraceTicks &&
                CopierActive && SupervisorStampActive)
            {
                Failed = true;
                FailureReason =
                    "CLOSING TIME ARRIVED WHILE THE COPIER STILL SUPERVISED THE OFFICE";
            }
        }

        public string ActionAt(OfficeRoomId room)
        {
            if (!Active || Recovered) return string.Empty;
            if (_channelKind != RecoveryChannelKind.None)
                return _channelKind == RecoveryChannelKind.ClearPromotionForm
                    ? "CLEARING PROMOTION FORM"
                    : "FINDING ORIGINAL BADGE";
            if (room == OfficeRoomId.WeirdRoom && CopierActive)
                return "STOP COPIER";
            if (room == OfficeRoomId.WeirdRoom && SupervisorStampActive)
                return "REMOVE SUPERVISOR STAMP";
            if (!string.IsNullOrWhiteSpace(FirstPromotionFormAt(room)))
                return "CLEAR PROMOTION FORM";
            if (room != OfficeRoomId.FrontDesk && !OriginalBadgeFound &&
                IsOriginalAvailableAt(room))
                return "FIND ORIGINAL BADGE";
            if (room == OfficeRoomId.WaitingArea && !RunnerReassigned &&
                _divertedFolderIds.Count >= 2)
                return "REASSIGN RUNNER";
            return string.Empty;
        }

        public bool TryFixAt(OfficeRoomId room, out string result)
        {
            result = string.Empty;
            if (!Active || Recovered || RecoveryChannelActive) return false;
            if (room == OfficeRoomId.WeirdRoom && CopierActive &&
                _copyEcho.TryStopCopier())
            {
                result = "COPIER STOPPED";
                return true;
            }
            string formId = FirstPromotionFormAt(room);
            if (!string.IsNullOrWhiteSpace(formId))
            {
                _channelKind = RecoveryChannelKind.ClearPromotionForm;
                _channelFolderId = formId;
                _channelRemainingTicks = CopyClearDurationTicks;
                result = "CLEARING PROMOTION FORM";
                return true;
            }
            if (room != OfficeRoomId.FrontDesk && !OriginalBadgeFound &&
                IsOriginalAvailableAt(room))
            {
                _channelKind = RecoveryChannelKind.FindOriginalBadge;
                _channelFolderId = _maraCaseId;
                _channelRemainingTicks = OriginalFindDurationTicks;
                result = "FINDING ORIGINAL BADGE";
                return true;
            }
            return false;
        }

        public bool TryRemoveSupervisorStamp(OfficeRoomId room)
        {
            if (!Active || Recovered || !SupervisorStampActive ||
                room != OfficeRoomId.WeirdRoom) return false;
            SupervisorStampActive = false;
            return true;
        }

        public bool TryReassignRunner(OfficeRoomId room)
        {
            if (!Active || Recovered || RunnerReassigned ||
                room != OfficeRoomId.WaitingArea ||
                _divertedFolderIds.Count < 2 ||
                !_staff.TryReassignRunnerToWarden()) return false;
            RunnerReassigned = true;
            return true;
        }

        public bool IsPromotionForm(string folderId)
        {
            return !string.IsNullOrWhiteSpace(folderId) &&
                _promotionFormIds.Contains(folderId);
        }

        public void AppendSnapshot(StringBuilder builder)
        {
            builder.Append("|promotion=").Append(HasTriggered).Append(':')
                .Append(Active).Append(':').Append(Recovered).Append(':')
                .Append(Failed).Append(':').Append(TriggeredTick).Append(':')
                .Append(CopierActive).Append(':')
                .Append(SupervisorStampActive).Append(':')
                .Append(MaraCalmed).Append(':').Append(OriginalBadgeFound)
                .Append(':').Append(OriginalBadgeReturned).Append(':')
                .Append(RunnerReassigned).Append(':')
                .Append(ClearedPromotionFormCount).Append(':')
                .Append(_nextPromotionFormOrdinal).Append(':')
                .Append(_lastPromotionFormTick).Append(':')
                .Append(CopyClearDurationTicks).Append(':')
                .Append(OriginalFindDurationTicks).Append(':')
                .Append(MaximumActivePromotionForms).Append(':')
                .Append(_channelKind).Append(':').Append(_channelFolderId)
                .Append(':').Append(_channelRemainingTicks).Append(':')
                .Append(FailureReason);
            for (int i = 0; i < _promotionFormIds.Count; i++)
                builder.Append("|promotion-form=").Append(_promotionFormIds[i])
                    .Append('>').Append(_maraCaseId);
            for (int i = 0; i < _divertedFolderIds.Count; i++)
                builder.Append("|promotion-diversion=")
                    .Append(_divertedFolderIds[i]);
        }

        private void Trigger(long currentTick)
        {
            HasTriggered = true;
            Active = true;
            TriggeredTick = currentTick;
            SupervisorStampActive = true;
            _lastPromotionFormTick = currentTick;
            _staff.AcceptCopierAsTaskSource();
        }

        private void CreatePromotionForm(long currentTick)
        {
            if (ActivePromotionFormCount >= MaximumActivePromotionForms) return;
            string formId = "promotion-form." +
                (_nextPromotionFormOrdinal++).ToString("D3");
            if (!_queues.TryCreateCopy(
                    formId, _maraCaseId, OfficeRoomId.WeirdRoom)) return;
            _promotionFormIds.Add(formId);
            _lastPromotionFormTick = currentTick;
        }

        private void AssignNextCopierDiversion(long currentTick)
        {
            if (_divertedFolderIds.Count >= 2 ||
                !string.Equals(_staff.RunnerTaskSourceId,
                    OfficeStaffSystem.CopierTaskSourceId,
                    StringComparison.Ordinal) ||
                _staff.Get(OfficeStaffSystem.RunnerId).TaskState !=
                    OfficeStaffTaskState.Idle) return;
            IReadOnlyList<string> folderIds = _queues.FolderIds;
            for (int i = 0; i < folderIds.Count; i++)
            {
                OfficeFolderState folder = _queues.GetFolder(folderIds[i]);
                if (folder == null || folder.IsCopy || folder.IsMoving ||
                    folder.OwnerKind != OfficeFolderOwnerKind.RoomQueue ||
                    folder.CurrentRoom == OfficeRoomId.WeirdRoom ||
                    string.Equals(folder.CaseId, _maraCaseId,
                        StringComparison.Ordinal) ||
                    _divertedFolderSet.Contains(folder.CaseId)) continue;
                if (!_staff.TryAssignFromCopier(
                        folder.CaseId, currentTick, out string ignored)) return;
                _divertedFolderSet.Add(folder.CaseId);
                _divertedFolderIds.Add(folder.CaseId);
                return;
            }
        }

        private void AdvanceRecoveryChannel()
        {
            if (_channelKind == RecoveryChannelKind.None) return;
            _channelRemainingTicks--;
            if (_channelRemainingTicks > 0) return;
            if (_channelKind == RecoveryChannelKind.ClearPromotionForm &&
                _queues.TryClearCopy(_channelFolderId))
                ClearedPromotionFormCount++;
            else if (_channelKind == RecoveryChannelKind.FindOriginalBadge)
                OriginalBadgeFound = true;
            _channelKind = RecoveryChannelKind.None;
            _channelFolderId = string.Empty;
            _channelRemainingTicks = 0;
        }

        private string FirstPromotionFormAt(OfficeRoomId room)
        {
            for (int i = 0; i < _promotionFormIds.Count; i++)
            {
                OfficeFolderState folder = _queues.GetFolder(
                    _promotionFormIds[i]);
                if (folder != null && !folder.IsMoving &&
                    folder.OwnerKind == OfficeFolderOwnerKind.RoomQueue &&
                    folder.CurrentRoom == room) return folder.CaseId;
            }
            return string.Empty;
        }

        private bool IsOriginalAvailableAt(OfficeRoomId room)
        {
            OfficeFolderState original = _queues.GetFolder(_maraCaseId);
            if (original == null || original.IsMoving) return false;
            if (original.OwnerKind == OfficeFolderOwnerKind.Warden)
                return room != OfficeRoomId.FrontDesk;
            return original.OwnerKind == OfficeFolderOwnerKind.RoomQueue &&
                original.CurrentRoom == room;
        }

        private void ObserveOriginalReturned()
        {
            if (!OriginalBadgeFound || OriginalBadgeReturned) return;
            OfficeFolderState original = _queues.GetFolder(_maraCaseId);
            if (original != null && !original.IsMoving &&
                original.OwnerKind == OfficeFolderOwnerKind.RoomQueue &&
                original.CurrentRoom == OfficeRoomId.FrontDesk)
                OriginalBadgeReturned = true;
        }

        private void ObserveRecovery()
        {
            if (!Active || Recovered) return;
            if (!CopierActive && !SupervisorStampActive &&
                ActivePromotionFormCount == 0 && MaraCalmed &&
                OriginalBadgeFound && OriginalBadgeReturned &&
                RunnerReassigned)
            {
                Active = false;
                Recovered = true;
            }
        }
    }
}
