using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace Desk42.Product.OfficeSlice
{
    public sealed class OfficeRoomWorkJobState
    {
        internal OfficeRoomWorkJobState(
            string jobId,
            string caseId,
            OfficeRoomId room,
            int durationTicks)
        {
            JobId = jobId;
            CaseId = caseId;
            Room = room;
            InitialDurationTicks = durationTicks;
            RemainingTicks = durationTicks;
        }

        public string JobId { get; }
        public string CaseId { get; }
        public OfficeRoomId Room { get; }
        public int InitialDurationTicks { get; }
        public int RemainingTicks { get; internal set; }
        public bool Complete => RemainingTicks <= 0;
    }

    public sealed class OfficeRoomWorkState
    {
        public const int DefaultDurationTicks = 90;
        public const int HelpBonusTicksPerTick = 2;

        private readonly List<OfficeRoomWorkJobState> _jobs = new();
        private readonly Dictionary<string, OfficeRoomWorkJobState> _byId =
            new(StringComparer.Ordinal);
        private readonly IReadOnlyList<OfficeRoomWorkJobState> _readOnlyJobs;

        public OfficeRoomWorkState()
        {
            _readOnlyJobs = _jobs.AsReadOnly();
        }

        public IReadOnlyList<OfficeRoomWorkJobState> Jobs => _readOnlyJobs;
        public bool HelpActive { get; private set; }
        public string HelpJobId { get; private set; } = string.Empty;
        public OfficeCell HelpStartCell { get; private set; }

        public bool TryStartJob(
            string jobId,
            string caseId,
            OfficeRoomId room,
            int durationTicks = DefaultDurationTicks)
        {
            if (string.IsNullOrWhiteSpace(jobId) ||
                string.IsNullOrWhiteSpace(caseId) || durationTicks <= 0 ||
                _byId.ContainsKey(jobId)) return false;
            var job = new OfficeRoomWorkJobState(jobId, caseId, room, durationTicks);
            _jobs.Add(job);
            _byId.Add(jobId, job);
            return true;
        }

        public OfficeRoomWorkJobState Job(string jobId)
        {
            if (string.IsNullOrWhiteSpace(jobId)) return null;
            _byId.TryGetValue(jobId, out OfficeRoomWorkJobState value);
            return value;
        }

        public OfficeRoomWorkJobState ActiveJobAt(OfficeRoomId room)
        {
            for (int i = 0; i < _jobs.Count; i++)
                if (_jobs[i].Room == room && !_jobs[i].Complete) return _jobs[i];
            return null;
        }

        public bool TryStartHelp(OfficeRoomId room, OfficeCell wardenCell)
        {
            OfficeRoomWorkJobState job = ActiveJobAt(room);
            if (job == null || HelpActive) return false;
            HelpActive = true;
            HelpJobId = job.JobId;
            HelpStartCell = wardenCell;
            return true;
        }

        public void CancelHelp()
        {
            HelpActive = false;
            HelpJobId = string.Empty;
            HelpStartCell = default;
        }

        public void AdvanceOneTick(OfficeCell wardenCell)
        {
            OfficeRoomWorkJobState helpedJob = null;
            if (HelpActive)
            {
                helpedJob = Job(HelpJobId);
                if (helpedJob == null || helpedJob.Complete ||
                    wardenCell != HelpStartCell)
                {
                    CancelHelp();
                    helpedJob = null;
                }
            }
            for (int i = 0; i < _jobs.Count; i++)
            {
                OfficeRoomWorkJobState job = _jobs[i];
                if (job.Complete) continue;
                int progress = 1;
                if (job == helpedJob) progress += HelpBonusTicksPerTick;
                job.RemainingTicks = Math.Max(0, job.RemainingTicks - progress);
            }
            if (helpedJob != null && helpedJob.Complete) CancelHelp();
        }

        public void AppendSnapshot(StringBuilder builder)
        {
            builder.Append("|help=").Append(HelpActive).Append(':')
                .Append(HelpJobId).Append(':').Append(HelpStartCell);
            for (int i = 0; i < _jobs.Count; i++)
            {
                OfficeRoomWorkJobState job = _jobs[i];
                builder.Append("|room-work=").Append(job.JobId).Append(':')
                    .Append(job.CaseId).Append(':').Append(job.Room).Append(':')
                    .Append(job.InitialDurationTicks).Append(':')
                    .Append(job.RemainingTicks);
            }
        }
    }

    public enum OfficeStaffRole
    {
        Runner,
        Talker,
    }

    public enum OfficeStaffTaskState
    {
        Idle,
        MovingToWork,
        Working,
        MovingFolder,
        AttendingCustomer,
        Blocked,
    }

    public sealed class OfficeStaffState
    {
        private readonly List<OfficeCell> _path = new();

        internal OfficeStaffState(
            string staffId,
            string displayName,
            OfficeStaffRole role,
            OfficeCell spawn)
        {
            StaffId = staffId;
            DisplayName = displayName;
            Role = role;
            XSubunits = spawn.X * OfficeGrid.LogicalSubunitsPerCell;
            ZSubunits = spawn.Z * OfficeGrid.LogicalSubunitsPerCell;
        }

        public string StaffId { get; }
        public string DisplayName { get; }
        public OfficeStaffRole Role { get; }
        public int XSubunits { get; internal set; }
        public int ZSubunits { get; internal set; }
        public OfficeStaffTaskState TaskState { get; internal set; }
        public string AssignedTargetId { get; internal set; } = string.Empty;
        public string JobId { get; internal set; } = string.Empty;
        public OfficeRoomId SourceRoom { get; internal set; }
        public OfficeRoomId DestinationRoom { get; internal set; }
        public string VisibleIntent { get; internal set; } = "READY";
        public bool IsBlocked => TaskState == OfficeStaffTaskState.Blocked;
        internal int PathIndex { get; set; }
        internal List<OfficeCell> MutablePath => _path;

        public OfficeCell Cell(OfficeGrid grid)
        {
            return grid.CellForLogicalPosition(XSubunits, ZSubunits);
        }
    }

    public sealed class OfficeStaffSystem
    {
        public const int MovementSubunitsPerTick = 4;
        public const string RunnerId = "staff.runner";
        public const string TalkerId = "staff.talker";

        private readonly OfficeGrid _grid;
        private readonly OfficeQueueService _queues;
        private readonly OfficeRoomWorkState _roomWork;
        private readonly OfficeCustomerScheduleState _customers;
        private readonly List<OfficeStaffState> _staff;
        private readonly IReadOnlyList<OfficeStaffState> _readOnlyStaff;
        private readonly Dictionary<string, OfficeStaffState> _byId;
        private bool _runnerDiversionActive;

        public OfficeStaffSystem(
            OfficeGrid grid,
            OfficeQueueService queues,
            OfficeRoomWorkState roomWork,
            OfficeCustomerScheduleState customers)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _queues = queues ?? throw new ArgumentNullException(nameof(queues));
            _roomWork = roomWork ?? throw new ArgumentNullException(nameof(roomWork));
            _customers = customers ?? throw new ArgumentNullException(nameof(customers));
            OfficeCell spawn = _grid.GetInteractionPoint("waiting-area.interact").Cell;
            _staff = new List<OfficeStaffState>
            {
                new(RunnerId, "RUNNER", OfficeStaffRole.Runner, spawn),
                new(TalkerId, "TALKER", OfficeStaffRole.Talker, spawn),
            };
            _byId = new Dictionary<string, OfficeStaffState>(StringComparer.Ordinal);
            for (int i = 0; i < _staff.Count; i++) _byId.Add(_staff[i].StaffId, _staff[i]);
            _readOnlyStaff = _staff.AsReadOnly();
        }

        public IReadOnlyList<OfficeStaffState> Staff => _readOnlyStaff;
        public bool RunnerDiversionActive => _runnerDiversionActive;
        public int RunnerDiversionCount { get; private set; }

        public void SetRunnerDiversion(bool active)
        {
            _runnerDiversionActive = active;
        }

        public OfficeStaffState Get(string staffId)
        {
            if (string.IsNullOrWhiteSpace(staffId)) return null;
            _byId.TryGetValue(staffId, out OfficeStaffState value);
            return value;
        }

        public bool TryAssign(
            string staffId,
            string targetId,
            OfficeRoomId destination,
            long currentTick,
            out string failure)
        {
            failure = string.Empty;
            OfficeStaffState staff = Get(staffId);
            if (staff == null || staff.TaskState != OfficeStaffTaskState.Idle)
            {
                failure = "STAFF_NOT_READY";
                return false;
            }
            if (staff.Role == OfficeStaffRole.Talker)
            {
                OfficeCustomerState customer = null;
                for (int i = 0; i < _customers.Customers.Count; i++)
                    if (string.Equals(_customers.Customers[i].CustomerId,
                            targetId, StringComparison.Ordinal))
                        customer = _customers.Customers[i];
                if (customer == null || customer.QueueState == OfficeCustomerQueueState.Complete ||
                    customer.QueueState == OfficeCustomerQueueState.NotArrived)
                {
                    failure = "CUSTOMER_NOT_HERE";
                    return false;
                }
                staff.AssignedTargetId = customer.CustomerId;
                staff.VisibleIntent = "GOING TO CUSTOMER";
                return BeginMove(staff, OfficeRoomId.FrontDesk,
                    OfficeStaffTaskState.MovingToWork, out failure);
            }

            OfficeFolderState folder = _queues.GetFolder(targetId);
            if (folder == null || folder.IsMoving ||
                folder.OwnerKind != OfficeFolderOwnerKind.RoomQueue)
            {
                failure = "FOLDER_NOT_READY";
                return false;
            }
            staff.AssignedTargetId = targetId;
            staff.SourceRoom = folder.CurrentRoom;
            if (_runnerDiversionActive && destination != OfficeRoomId.WeirdRoom)
            {
                staff.DestinationRoom = OfficeRoomId.WeirdRoom;
                _runnerDiversionActive = false;
                RunnerDiversionCount++;
            }
            else
            {
                staff.DestinationRoom = destination;
            }
            staff.JobId = "job.runner." + targetId + "." +
                currentTick.ToString("D8");
            staff.VisibleIntent = "GOING TO " + RoomLabel(staff.SourceRoom);
            return BeginMove(staff, staff.SourceRoom,
                OfficeStaffTaskState.MovingToWork, out failure);
        }

        public void AdvanceOneTick(long currentTick)
        {
            for (int i = 0; i < _staff.Count; i++)
            {
                OfficeStaffState staff = _staff[i];
                if (staff.Role == OfficeStaffRole.Talker)
                    AdvanceTalker(staff);
                else
                    AdvanceRunner(staff, currentTick);
            }
        }

        public bool IsTalkerAttending(string customerId)
        {
            OfficeStaffState talker = Get(TalkerId);
            return talker.TaskState == OfficeStaffTaskState.AttendingCustomer &&
                string.Equals(talker.AssignedTargetId, customerId,
                    StringComparison.Ordinal);
        }

        public void AppendSnapshot(StringBuilder builder)
        {
            for (int i = 0; i < _staff.Count; i++)
            {
                OfficeStaffState staff = _staff[i];
                builder.Append("|staff=").Append(staff.StaffId).Append(':')
                    .Append(staff.Role).Append(':').Append(staff.XSubunits).Append(',')
                    .Append(staff.ZSubunits).Append(':').Append(staff.TaskState)
                    .Append(':').Append(staff.AssignedTargetId).Append(':')
                    .Append(staff.JobId).Append(':').Append(staff.SourceRoom)
                    .Append(':').Append(staff.DestinationRoom).Append(':')
                    .Append(staff.VisibleIntent);
            }
            builder.Append("|runner-diversion=").Append(_runnerDiversionActive)
                .Append(':').Append(RunnerDiversionCount);
        }

        private void AdvanceTalker(OfficeStaffState staff)
        {
            if (staff.TaskState == OfficeStaffTaskState.MovingToWork)
            {
                if (MoveOneTick(staff))
                {
                    staff.TaskState = OfficeStaffTaskState.AttendingCustomer;
                    staff.VisibleIntent = "CALMING CUSTOMER";
                }
                return;
            }
            if (staff.TaskState != OfficeStaffTaskState.AttendingCustomer) return;
            OfficeCustomerState customer = null;
            for (int i = 0; i < _customers.Customers.Count; i++)
                if (string.Equals(_customers.Customers[i].CustomerId,
                        staff.AssignedTargetId, StringComparison.Ordinal))
                    customer = _customers.Customers[i];
            if (customer == null || customer.QueueState == OfficeCustomerQueueState.Complete)
                Reset(staff);
        }

        private void AdvanceRunner(OfficeStaffState staff, long currentTick)
        {
            if (staff.TaskState == OfficeStaffTaskState.MovingToWork)
            {
                if (MoveOneTick(staff))
                {
                    if (_roomWork.TryStartJob(staff.JobId,
                            staff.AssignedTargetId, staff.SourceRoom))
                    {
                        staff.TaskState = OfficeStaffTaskState.Working;
                        staff.VisibleIntent = "CHECKING FOLDER";
                    }
                    else
                    {
                        staff.TaskState = OfficeStaffTaskState.Blocked;
                        staff.VisibleIntent = "WORK POINT BLOCKED";
                    }
                }
                return;
            }
            if (staff.TaskState == OfficeStaffTaskState.Working)
            {
                OfficeRoomWorkJobState job = _roomWork.Job(staff.JobId);
                if (job == null || !job.Complete) return;
                if (!_queues.TryTakeByRunner(
                        staff.StaffId, staff.AssignedTargetId, staff.SourceRoom))
                {
                    staff.TaskState = OfficeStaffTaskState.Blocked;
                    staff.VisibleIntent = "WAITING FOR FOLDER";
                    return;
                }
                if (!BeginMove(staff, staff.DestinationRoom,
                        OfficeStaffTaskState.MovingFolder, out string ignored))
                {
                    staff.TaskState = OfficeStaffTaskState.Blocked;
                    staff.VisibleIntent = "ROUTE BLOCKED";
                }
                else
                {
                    staff.VisibleIntent = "CARRYING TO " +
                        RoomLabel(staff.DestinationRoom);
                }
                return;
            }
            if (staff.TaskState == OfficeStaffTaskState.MovingFolder)
            {
                if (!MoveOneTick(staff)) return;
                if (_queues.TryDropByRunner(staff.StaffId, staff.DestinationRoom))
                    Reset(staff);
                else
                {
                    staff.TaskState = OfficeStaffTaskState.Blocked;
                    staff.VisibleIntent = "DROP POINT BLOCKED";
                }
                return;
            }
            if (staff.TaskState != OfficeStaffTaskState.Blocked) return;
            OfficeFolderState folder = _queues.GetFolder(staff.AssignedTargetId);
            if (folder != null && !folder.IsMoving &&
                folder.OwnerKind == OfficeFolderOwnerKind.RoomQueue &&
                folder.CurrentRoom == staff.SourceRoom)
            {
                staff.TaskState = OfficeStaffTaskState.Working;
                staff.VisibleIntent = "CHECKING FOLDER";
            }
        }

        private bool BeginMove(
            OfficeStaffState staff,
            OfficeRoomId room,
            OfficeStaffTaskState movingState,
            out string failure)
        {
            OfficeInteractionPoint point = InteractionPoint(room);
            if (point == null || !_grid.TryFindPath(
                    staff.Cell(_grid), point.Cell, out List<OfficeCell> path))
            {
                failure = "NO_STAFF_ROUTE";
                staff.TaskState = OfficeStaffTaskState.Blocked;
                staff.VisibleIntent = "ROUTE BLOCKED";
                return false;
            }
            staff.MutablePath.Clear();
            staff.MutablePath.AddRange(path);
            staff.PathIndex = path.Count > 1 ? 1 : path.Count;
            staff.TaskState = movingState;
            failure = string.Empty;
            return true;
        }

        private bool MoveOneTick(OfficeStaffState staff)
        {
            if (staff.PathIndex >= staff.MutablePath.Count) return true;
            OfficeCell waypoint = staff.MutablePath[staff.PathIndex];
            int targetX = waypoint.X * OfficeGrid.LogicalSubunitsPerCell;
            int targetZ = waypoint.Z * OfficeGrid.LogicalSubunitsPerCell;
            staff.XSubunits = MoveTowards(
                staff.XSubunits, targetX, MovementSubunitsPerTick);
            staff.ZSubunits = MoveTowards(
                staff.ZSubunits, targetZ, MovementSubunitsPerTick);
            if (staff.XSubunits == targetX && staff.ZSubunits == targetZ)
                staff.PathIndex++;
            return staff.PathIndex >= staff.MutablePath.Count;
        }

        private OfficeInteractionPoint InteractionPoint(OfficeRoomId room)
        {
            for (int i = 0; i < _grid.InteractionPoints.Count; i++)
                if (_grid.InteractionPoints[i].Room == room)
                    return _grid.InteractionPoints[i];
            return null;
        }

        private static int MoveTowards(int current, int target, int distance)
        {
            if (current < target) return Math.Min(target, current + distance);
            if (current > target) return Math.Max(target, current - distance);
            return current;
        }

        private static string RoomLabel(OfficeRoomId room)
        {
            return room switch
            {
                OfficeRoomId.FrontDesk => "FRONT",
                OfficeRoomId.PaperRoom => "PAPERS",
                OfficeRoomId.MoneyRoom => "MONEY",
                OfficeRoomId.WeirdRoom => "COPIER",
                _ => "WAITING AREA",
            };
        }

        private static void Reset(OfficeStaffState staff)
        {
            staff.TaskState = OfficeStaffTaskState.Idle;
            staff.AssignedTargetId = string.Empty;
            staff.JobId = string.Empty;
            staff.VisibleIntent = "READY";
            staff.MutablePath.Clear();
            staff.PathIndex = 0;
        }
    }

    public sealed class OfficeCustomerPressureRecord
    {
        internal OfficeCustomerPressureRecord(string customerId)
        {
            CustomerId = customerId;
        }

        public string CustomerId { get; }
        public int PressureTicks { get; internal set; }
        public string LastAuthoredCause { get; internal set; } = "WAITING";
        internal int ObservedCompareAttempts { get; set; }
        internal int ObservedTraceAttempts { get; set; }
    }

    public sealed class OfficeCustomerPressureState
    {
        public const int CalmDurationTicks = 60;
        public const int CalmCooldownTicks = 90;
        public const int CalmReductionPerTick = 10;

        private readonly OfficeCustomerScheduleState _customers;
        private readonly OfficeQueueService _queues;
        private readonly OfficeManualTaskState _manualTasks;
        private readonly int _moodThresholdBonusTicks;
        private readonly Dictionary<string, OfficeCustomerPressureRecord> _records =
            new(StringComparer.Ordinal);

        public OfficeCustomerPressureState(
            OfficeCustomerScheduleState customers,
            OfficeQueueService queues,
            OfficeManualTaskState manualTasks,
            int moodThresholdBonusTicks = 0)
        {
            _customers = customers ?? throw new ArgumentNullException(nameof(customers));
            _queues = queues ?? throw new ArgumentNullException(nameof(queues));
            _manualTasks = manualTasks ?? throw new ArgumentNullException(nameof(manualTasks));
            if (moodThresholdBonusTicks < 0)
                throw new ArgumentOutOfRangeException(nameof(moodThresholdBonusTicks));
            _moodThresholdBonusTicks = moodThresholdBonusTicks;
            for (int i = 0; i < customers.Customers.Count; i++)
            {
                string id = customers.Customers[i].CustomerId;
                _records.Add(id, new OfficeCustomerPressureRecord(id));
            }
        }

        public bool CalmActive { get; private set; }
        public string CalmCustomerId { get; private set; } = string.Empty;
        public int CalmRemainingTicks { get; private set; }
        public int CalmCooldownRemainingTicks { get; private set; }
        public OfficeCell CalmStartCell { get; private set; }

        public OfficeCustomerPressureRecord RecordFor(string customerId)
        {
            if (string.IsNullOrWhiteSpace(customerId)) return null;
            _records.TryGetValue(customerId, out OfficeCustomerPressureRecord value);
            return value;
        }

        public bool TryStartCalm(string customerId, OfficeCell wardenCell)
        {
            OfficeCustomerState active = _customers.ActiveDeskCustomer;
            if (CalmActive || CalmCooldownRemainingTicks > 0 || active == null ||
                !string.Equals(active.CustomerId, customerId,
                    StringComparison.Ordinal)) return false;
            CalmActive = true;
            CalmCustomerId = customerId;
            CalmRemainingTicks = CalmDurationTicks;
            CalmStartCell = wardenCell;
            return true;
        }

        public void CancelCalm()
        {
            CalmActive = false;
            CalmCustomerId = string.Empty;
            CalmRemainingTicks = 0;
            CalmStartCell = default;
        }

        public void AdvanceOneTick(OfficeCell wardenCell, OfficeStaffSystem staff)
        {
            if (CalmCooldownRemainingTicks > 0) CalmCooldownRemainingTicks--;
            if (CalmActive)
            {
                OfficeCustomerState active = _customers.ActiveDeskCustomer;
                if (active == null || wardenCell != CalmStartCell ||
                    !string.Equals(active.CustomerId, CalmCustomerId,
                        StringComparison.Ordinal))
                    CancelCalm();
            }

            for (int i = 0; i < _customers.Customers.Count; i++)
            {
                OfficeCustomerState customer = _customers.Customers[i];
                if (customer.QueueState == OfficeCustomerQueueState.NotArrived ||
                    customer.QueueState == OfficeCustomerQueueState.Complete) continue;
                OfficeCustomerPressureRecord record = RecordFor(customer.CustomerId);
                bool calming = CalmActive && string.Equals(
                    customer.CustomerId, CalmCustomerId, StringComparison.Ordinal);
                int delta = 0;
                if (!calming && (staff == null ||
                    !staff.IsTalkerAttending(customer.CustomerId)))
                {
                    delta = customer.QueueState == OfficeCustomerQueueState.Waiting ? 2 : 1;
                    record.LastAuthoredCause = "WAITING";
                    OfficeFolderState folder = _queues.GetFolder(
                        customer.LinkedAutomationClaimId);
                    if (folder != null && folder.CurrentRoom != OfficeRoomId.FrontDesk)
                    {
                        delta++;
                        record.LastAuthoredCause = "FOLDER AWAY FROM FRONT";
                    }
                    if (folder != null && folder.CurrentRoom == OfficeRoomId.WeirdRoom)
                    {
                        delta += 2;
                        record.LastAuthoredCause = "FOLDER NEAR COPIER";
                    }
                }
                OfficeCaseWorkRecord work = _manualTasks.RecordFor(
                    customer.LinkedAutomationClaimId);
                if (work.CompareAttempts > record.ObservedCompareAttempts)
                {
                    record.ObservedCompareAttempts = work.CompareAttempts;
                    if (!work.CompareCorrect)
                    {
                        delta += 60;
                        record.LastAuthoredCause = "CONTRADICTORY PAPER ANSWER";
                    }
                }
                if (work.TraceAttempts > record.ObservedTraceAttempts)
                {
                    record.ObservedTraceAttempts = work.TraceAttempts;
                    if (!work.TraceCorrect)
                    {
                        delta += 60;
                        record.LastAuthoredCause = "CONTRADICTORY MONEY ANSWER";
                    }
                }
                record.PressureTicks = Math.Max(0, record.PressureTicks + delta);
                if (customer.QueueState == OfficeCustomerQueueState.Waiting)
                    record.PressureTicks = Math.Min(
                        899 + _moodThresholdBonusTicks,
                        record.PressureTicks);
                if (calming)
                {
                    record.PressureTicks = Math.Max(
                        0, record.PressureTicks - CalmReductionPerTick);
                    record.LastAuthoredCause = "CALM";
                }
                customer.VisibleMoodState = MoodFor(record.PressureTicks);
            }

            if (!CalmActive) return;
            CalmRemainingTicks--;
            if (CalmRemainingTicks <= 0)
            {
                CalmActive = false;
                CalmCustomerId = string.Empty;
                CalmRemainingTicks = 0;
                CalmCooldownRemainingTicks = CalmCooldownTicks;
            }
        }

        public void AppendSnapshot(StringBuilder builder)
        {
            builder.Append("|calm=").Append(CalmActive).Append(':')
                .Append(CalmCustomerId).Append(':').Append(CalmRemainingTicks)
                .Append(':').Append(CalmCooldownRemainingTicks).Append(':')
                .Append(CalmStartCell);
            builder.Append("|mood-threshold-bonus=").Append(
                _moodThresholdBonusTicks);
            for (int i = 0; i < _customers.Customers.Count; i++)
            {
                OfficeCustomerPressureRecord record = RecordFor(
                    _customers.Customers[i].CustomerId);
                builder.Append("|pressure=").Append(record.CustomerId).Append(':')
                    .Append(record.PressureTicks).Append(':')
                    .Append(record.LastAuthoredCause).Append(':')
                    .Append(record.ObservedCompareAttempts).Append(':')
                    .Append(record.ObservedTraceAttempts);
            }
        }

        private OfficeVisibleMoodState MoodFor(int pressure)
        {
            if (pressure >= 1800 + _moodThresholdBonusTicks)
                return OfficeVisibleMoodState.Break;
            if (pressure >= 1350 + _moodThresholdBonusTicks)
                return OfficeVisibleMoodState.Strange;
            if (pressure >= 900 + _moodThresholdBonusTicks)
                return OfficeVisibleMoodState.Upset;
            if (pressure >= 450 + _moodThresholdBonusTicks)
                return OfficeVisibleMoodState.Worried;
            return OfficeVisibleMoodState.Calm;
        }
    }
}
