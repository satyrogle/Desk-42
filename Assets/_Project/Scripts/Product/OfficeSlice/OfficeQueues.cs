using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Desk42.Product.OfficeSlice
{
    public sealed class OfficeFolderState
    {
        internal OfficeFolderState(string caseId, OfficeRoomId room)
        {
            CaseId = caseId;
            CurrentRoom = room;
        }

        public string CaseId { get; }
        public OfficeRoomId CurrentRoom { get; internal set; }
        public OfficeRoomId SourceRoom { get; internal set; }
        public OfficeRoomId DestinationRoom { get; internal set; }
        public long TransferStartTick { get; internal set; }
        public long TransferEndTick { get; internal set; }
        public bool IsMoving { get; internal set; }

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
            if (folder.IsMoving || IsQueuedAnywhere(caseId)) return false;
            folder.CurrentRoom = room;
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
            if (folder.IsMoving || !IsQueuedIn(caseId, folder.CurrentRoom)) return false;
            if (durationTicks <= 0) throw new ArgumentOutOfRangeException(nameof(durationTicks));

            _queues[folder.CurrentRoom].Remove(caseId);
            folder.SourceRoom = folder.CurrentRoom;
            folder.DestinationRoom = destination;
            folder.TransferStartTick = currentTick;
            folder.TransferEndTick = currentTick + durationTicks;
            folder.IsMoving = true;
            ValidateSingleOwnership();
            return true;
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
                if (folder.IsMoving || folder.CurrentRoom != OfficeRoomId.FrontDesk)
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
                int expected = folder.IsMoving ? 0 : 1;
                if (owners[folder.CaseId] != expected)
                    throw new InvalidOperationException(
                        "Folder does not have exactly one logical owner: " + folder.CaseId);
            }
        }
    }
}
