using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Desk42.Product.OfficeSlice
{
    public enum OfficeFolderOwnerKind
    {
        RoomQueue,
        Transit,
        Warden,
        Runner,
        Cleared,
    }

    public sealed class OfficeFolderState
    {
        internal OfficeFolderState(string caseId, OfficeRoomId room)
        {
            CaseId = caseId;
            CurrentRoom = room;
            OwnerKind = OfficeFolderOwnerKind.RoomQueue;
            OwnerId = room.ToString();
        }

        public string CaseId { get; }
        public OfficeRoomId CurrentRoom { get; internal set; }
        public OfficeRoomId SourceRoom { get; internal set; }
        public OfficeRoomId DestinationRoom { get; internal set; }
        public long TransferStartTick { get; internal set; }
        public long TransferEndTick { get; internal set; }
        public bool IsMoving { get; internal set; }
        public OfficeFolderOwnerKind OwnerKind { get; internal set; }
        public string OwnerId { get; internal set; }

        public float ProgressAt(long tick)
        {
            if (!IsMoving) return 1f;
            if (TransferEndTick <= TransferStartTick) return 1f;
            float progress = (tick - TransferStartTick) /
                (float)(TransferEndTick - TransferStartTick);
            return Math.Max(0f, Math.Min(1f, progress));
        }
    }

    public sealed class OfficeRoomQueueState
    {
        private readonly List<string> _caseIds = new();

        internal OfficeRoomQueueState(OfficeRoomId room)
        {
            Room = room;
        }

        public OfficeRoomId Room { get; }
        public IReadOnlyList<string> CaseIds =>
            new ReadOnlyCollection<string>(_caseIds);
        public int Count => _caseIds.Count;

        public bool Contains(string caseId)
        {
            return _caseIds.Contains(caseId);
        }

        public string Peek()
        {
            return _caseIds.Count == 0 ? string.Empty : _caseIds[0];
        }

        internal void Add(string caseId)
        {
            if (_caseIds.Contains(caseId))
                throw new InvalidOperationException(
                    "Duplicate queue insertion: " + caseId + " into " + Room);
            _caseIds.Add(caseId);
        }

        internal bool Remove(string caseId)
        {
            return _caseIds.Remove(caseId);
        }
    }

    /// <summary>
    /// Logical folder ownership and queue ordering. Unity transforms are never used
    /// to decide whether a case is queued or in transit.
    /// </summary>
    public sealed class OfficeQueueService
    {
        public const int DefaultTransferDurationTicks = 15;

        private readonly Dictionary<OfficeRoomId, OfficeRoomQueueState> _queues;
        private readonly Dictionary<string, OfficeFolderState> _folders;
        private readonly List<string> _folderOrder;
        private readonly OfficeCaseRepository _cases;

        public OfficeQueueService(OfficeCaseRepository cases)
        {
            _cases = cases ?? throw new ArgumentNullException(nameof(cases));
            _queues = new Dictionary<OfficeRoomId, OfficeRoomQueueState>();
            _folders = new Dictionary<string, OfficeFolderState>(StringComparer.Ordinal);
            _folderOrder = new List<string>();
            foreach (OfficeRoomId room in Enum.GetValues(typeof(OfficeRoomId)))
                _queues.Add(room, new OfficeRoomQueueState(room));

            for (int i = 0; i < _cases.Cases.Count; i++)
            {
                OfficeCase officeCase = _cases.Cases[i];
                var folder = new OfficeFolderState(
                    officeCase.AutomationClaimId,
                    OfficeRoomId.FrontDesk);
                _folders.Add(folder.CaseId, folder);
                _folderOrder.Add(folder.CaseId);
                _queues[OfficeRoomId.FrontDesk].Add(folder.CaseId);
            }
            ValidateSingleOwnership();
        }

        public IReadOnlyList<string> FolderIds => _folderOrder.AsReadOnly();
        public string WardenCarriedFolderId
        {
            get
            {
                for (int i = 0; i < _folderOrder.Count; i++)
                {
                    OfficeFolderState folder = _folders[_folderOrder[i]];
                    if (folder.OwnerKind == OfficeFolderOwnerKind.Warden)
                        return folder.CaseId;
                }
                return string.Empty;
            }
        }

        public OfficeFolderState GetFolder(string caseId)
        {
            if (string.IsNullOrWhiteSpace(caseId)) return null;
            _folders.TryGetValue(caseId, out OfficeFolderState folder);
            return folder;
        }

        public OfficeRoomQueueState GetQueue(OfficeRoomId room)
        {
            return _queues[room];
        }

        public bool TryEnqueue(string caseId, OfficeRoomId room)
        {
            if (!_folders.TryGetValue(caseId, out OfficeFolderState folder)) return false;
            if (folder.IsMoving || IsQueuedAnywhere(caseId) ||
                folder.OwnerKind != OfficeFolderOwnerKind.Cleared) return false;
            folder.CurrentRoom = room;
            folder.OwnerKind = OfficeFolderOwnerKind.RoomQueue;
            folder.OwnerId = room.ToString();
            _queues[room].Add(caseId);
            ValidateSingleOwnership();
            return true;
        }

        public bool TryTransferCase(
            string caseId,
            OfficeRoomId destination,
            long currentTick,
            int durationTicks = DefaultTransferDurationTicks)
        {
            if (!_folders.TryGetValue(caseId, out OfficeFolderState folder)) return false;
            if (folder.IsMoving ||
                folder.OwnerKind != OfficeFolderOwnerKind.RoomQueue ||
                !IsQueuedIn(caseId, folder.CurrentRoom)) return false;
            if (durationTicks <= 0) throw new ArgumentOutOfRangeException(nameof(durationTicks));

            _queues[folder.CurrentRoom].Remove(caseId);
            BeginTransfer(folder, destination, currentTick, durationTicks);
            ValidateSingleOwnership();
            return true;
        }

        public bool TryTakeByWarden(string caseId, OfficeRoomId room)
        {
            if (!string.IsNullOrWhiteSpace(WardenCarriedFolderId)) return false;
            if (!_folders.TryGetValue(caseId, out OfficeFolderState folder)) return false;
            if (folder.IsMoving || folder.CurrentRoom != room ||
                folder.OwnerKind != OfficeFolderOwnerKind.RoomQueue ||
                !IsQueuedIn(caseId, room)) return false;
            _queues[room].Remove(caseId);
            folder.OwnerKind = OfficeFolderOwnerKind.Warden;
            folder.OwnerId = "warden";
            ValidateSingleOwnership();
            return true;
        }

        public bool TryDropByWarden(OfficeRoomId room)
        {
            string caseId = WardenCarriedFolderId;
            if (string.IsNullOrWhiteSpace(caseId)) return false;
            OfficeFolderState folder = _folders[caseId];
            folder.CurrentRoom = room;
            folder.OwnerKind = OfficeFolderOwnerKind.RoomQueue;
            folder.OwnerId = room.ToString();
            _queues[room].Add(caseId);
            ValidateSingleOwnership();
            return true;
        }

        public bool TryTakeByRunner(
            string staffId,
            string caseId,
            OfficeRoomId room)
        {
            if (string.IsNullOrWhiteSpace(staffId) ||
                !_folders.TryGetValue(caseId, out OfficeFolderState folder)) return false;
            for (int i = 0; i < _folderOrder.Count; i++)
            {
                OfficeFolderState existing = _folders[_folderOrder[i]];
                if (existing.OwnerKind == OfficeFolderOwnerKind.Runner &&
                    string.Equals(existing.OwnerId, staffId, StringComparison.Ordinal))
                    return false;
            }
            if (folder.IsMoving || folder.CurrentRoom != room ||
                folder.OwnerKind != OfficeFolderOwnerKind.RoomQueue ||
                !IsQueuedIn(caseId, room)) return false;
            _queues[room].Remove(caseId);
            folder.OwnerKind = OfficeFolderOwnerKind.Runner;
            folder.OwnerId = staffId;
            ValidateSingleOwnership();
            return true;
        }

        public bool TryDropByRunner(string staffId, OfficeRoomId room)
        {
            if (string.IsNullOrWhiteSpace(staffId)) return false;
            OfficeFolderState carried = null;
            for (int i = 0; i < _folderOrder.Count; i++)
            {
                OfficeFolderState folder = _folders[_folderOrder[i]];
                if (folder.OwnerKind == OfficeFolderOwnerKind.Runner &&
                    string.Equals(folder.OwnerId, staffId, StringComparison.Ordinal))
                {
                    carried = folder;
                    break;
                }
            }
            if (carried == null) return false;
            carried.CurrentRoom = room;
            carried.OwnerKind = OfficeFolderOwnerKind.RoomQueue;
            carried.OwnerId = room.ToString();
            _queues[room].Add(carried.CaseId);
            ValidateSingleOwnership();
            return true;
        }

        public bool TrySendByWarden(
            OfficeRoomId destination,
            long currentTick,
            int durationTicks = DefaultTransferDurationTicks)
        {
            string caseId = WardenCarriedFolderId;
            if (string.IsNullOrWhiteSpace(caseId)) return false;
            if (durationTicks <= 0) throw new ArgumentOutOfRangeException(nameof(durationTicks));
            OfficeFolderState folder = _folders[caseId];
            BeginTransfer(folder, destination, currentTick, durationTicks);
            ValidateSingleOwnership();
            return true;
        }

        private static void BeginTransfer(
            OfficeFolderState folder,
            OfficeRoomId destination,
            long currentTick,
            int durationTicks)
        {
            folder.SourceRoom = folder.CurrentRoom;
            folder.DestinationRoom = destination;
            folder.TransferStartTick = currentTick;
            folder.TransferEndTick = currentTick + durationTicks;
            folder.IsMoving = true;
            folder.OwnerKind = OfficeFolderOwnerKind.Transit;
            folder.OwnerId = folder.SourceRoom + ">" + destination;
        }

        public bool TrySendFromRoom(OfficeRoomId source, long currentTick)
        {
            string caseId = _queues[source].Peek();
            if (string.IsNullOrWhiteSpace(caseId)) return false;
            return TryTransferCase(caseId, NextRoom(source), currentTick);
        }

        public bool TrySendCase(string caseId, long currentTick)
        {
            OfficeFolderState folder = GetFolder(caseId);
            if (folder == null || folder.IsMoving) return false;
            return TryTransferCase(caseId, NextRoom(folder.CurrentRoom), currentTick);
        }

        public void AdvanceToTick(long currentTick)
        {
            for (int i = 0; i < _folderOrder.Count; i++)
            {
                OfficeFolderState folder = _folders[_folderOrder[i]];
                if (!folder.IsMoving || currentTick < folder.TransferEndTick) continue;
                folder.CurrentRoom = folder.DestinationRoom;
                folder.IsMoving = false;
                folder.OwnerKind = OfficeFolderOwnerKind.RoomQueue;
                folder.OwnerId = folder.CurrentRoom.ToString();
                _queues[folder.CurrentRoom].Add(folder.CaseId);
            }
            ValidateSingleOwnership();
        }

        public bool AllFoldersAtFrontDesk()
        {
            if (_queues[OfficeRoomId.FrontDesk].Count != _folders.Count) return false;
            for (int i = 0; i < _folderOrder.Count; i++)
            {
                OfficeFolderState folder = _folders[_folderOrder[i]];
                if (folder.IsMoving ||
                    folder.OwnerKind != OfficeFolderOwnerKind.RoomQueue ||
                    folder.CurrentRoom != OfficeRoomId.FrontDesk)
                    return false;
            }
            return true;
        }

        public bool HasSingleLogicalOwnerForEveryFolder()
        {
            try
            {
                ValidateSingleOwnership();
                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        public static OfficeRoomId NextRoom(OfficeRoomId room)
        {
            return room switch
            {
                OfficeRoomId.FrontDesk => OfficeRoomId.PaperRoom,
                OfficeRoomId.PaperRoom => OfficeRoomId.MoneyRoom,
                OfficeRoomId.MoneyRoom => OfficeRoomId.WeirdRoom,
                OfficeRoomId.WeirdRoom => OfficeRoomId.FrontDesk,
                OfficeRoomId.WaitingArea => OfficeRoomId.FrontDesk,
                _ => OfficeRoomId.FrontDesk,
            };
        }

        private bool IsQueuedAnywhere(string caseId)
        {
            foreach (OfficeRoomQueueState queue in _queues.Values)
                if (queue.Contains(caseId)) return true;
            return false;
        }

        private bool IsQueuedIn(string caseId, OfficeRoomId room)
        {
            return _queues[room].Contains(caseId);
        }

        private void ValidateSingleOwnership()
        {
            var owners = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < _folderOrder.Count; i++) owners[_folderOrder[i]] = 0;
            foreach (OfficeRoomQueueState queue in _queues.Values)
            {
                for (int i = 0; i < queue.CaseIds.Count; i++)
                {
                    string caseId = queue.CaseIds[i];
                    if (!owners.ContainsKey(caseId))
                        throw new InvalidOperationException("Unknown queued folder: " + caseId);
                    owners[caseId]++;
                }
            }
            for (int i = 0; i < _folderOrder.Count; i++)
            {
                OfficeFolderState folder = _folders[_folderOrder[i]];
                int expected = folder.OwnerKind == OfficeFolderOwnerKind.RoomQueue ? 1 : 0;
                if (owners[folder.CaseId] != expected)
                    throw new InvalidOperationException(
                        "Folder does not have exactly one logical owner: " + folder.CaseId);
                if ((folder.OwnerKind == OfficeFolderOwnerKind.Transit) != folder.IsMoving)
                    throw new InvalidOperationException(
                        "Folder transit state disagrees with its logical owner: " +
                        folder.CaseId);
                if (string.IsNullOrWhiteSpace(folder.OwnerId))
                    throw new InvalidOperationException(
                        "Folder logical owner ID is missing: " + folder.CaseId);
            }
        }
    }
}
