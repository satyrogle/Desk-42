using System;
using System.Collections.Generic;

namespace Desk42.Product.OfficeSlice
{
    public enum OfficeRoomId
    {
        FrontDesk,
        PaperRoom,
        MoneyRoom,
        WeirdRoom,
        WaitingArea,
    }

    public readonly struct OfficeCell : IEquatable<OfficeCell>
    {
        public OfficeCell(int x, int z)
        {
            X = x;
            Z = z;
        }

        public int X { get; }
        public int Z { get; }

        public bool Equals(OfficeCell other) => X == other.X && Z == other.Z;
        public override bool Equals(object obj) => obj is OfficeCell other && Equals(other);
        public override int GetHashCode() => (X * 397) ^ Z;
        public override string ToString() => X + "," + Z;

        public static bool operator ==(OfficeCell left, OfficeCell right) => left.Equals(right);
        public static bool operator !=(OfficeCell left, OfficeCell right) => !left.Equals(right);
    }

    public sealed class OfficeInteractionPoint
    {
        public OfficeInteractionPoint(
            string id,
            OfficeRoomId room,
            OfficeCell cell,
            int stablePriority)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Room = room;
            Cell = cell;
            StablePriority = stablePriority;
        }

        public string Id { get; }
        public OfficeRoomId Room { get; }
        public OfficeCell Cell { get; }
        public int StablePriority { get; }
    }

    /// <summary>
    /// Explicit M1 planning grid. One logical cell represents the 32 px planning
    /// unit requested by the brief; presentation chooses the world-unit scale.
    /// </summary>
    public sealed class OfficeGrid
    {
        public const int LogicalPixelsPerCell = 32;
        public const int LogicalSubunitsPerCell = 32;

        private readonly HashSet<OfficeCell> _blocked;
        private readonly List<OfficeInteractionPoint> _interactionPoints;

        private OfficeGrid(
            int minX,
            int maxX,
            int minZ,
            int maxZ,
            IEnumerable<OfficeCell> blocked,
            IEnumerable<OfficeInteractionPoint> interactionPoints)
        {
            MinX = minX;
            MaxX = maxX;
            MinZ = minZ;
            MaxZ = maxZ;
            _blocked = new HashSet<OfficeCell>(blocked ?? Array.Empty<OfficeCell>());
            _interactionPoints = new List<OfficeInteractionPoint>(interactionPoints ??
                Array.Empty<OfficeInteractionPoint>());
        }

        public int MinX { get; }
        public int MaxX { get; }
        public int MinZ { get; }
        public int MaxZ { get; }
        public IReadOnlyList<OfficeInteractionPoint> InteractionPoints =>
            _interactionPoints.AsReadOnly();
        public OfficeCell SpawnCell => new(-12, 0);

        public static OfficeGrid CreateM1()
        {
            var blocked = new List<OfficeCell>();
            for (int x = -14; x <= 14; x++)
            {
                blocked.Add(new OfficeCell(x, -9));
                blocked.Add(new OfficeCell(x, 9));
            }
            for (int z = -8; z <= 8; z++)
            {
                blocked.Add(new OfficeCell(-14, z));
                blocked.Add(new OfficeCell(14, z));
            }

            // Short greybox divider stubs make collision readable while leaving a
            // continuous route around each office room.
            blocked.Add(new OfficeCell(-4, 3));
            blocked.Add(new OfficeCell(-4, 4));
            blocked.Add(new OfficeCell(3, 3));
            blocked.Add(new OfficeCell(3, 4));
            blocked.Add(new OfficeCell(3, -4));
            blocked.Add(new OfficeCell(3, -3));

            var points = new List<OfficeInteractionPoint>
            {
                new("front-desk.interact", OfficeRoomId.FrontDesk, new OfficeCell(-10, 5), 0),
                new("paper-room.interact", OfficeRoomId.PaperRoom, new OfficeCell(0, 5), 1),
                new("money-room.interact", OfficeRoomId.MoneyRoom, new OfficeCell(7, 5), 2),
                new("weird-room.interact", OfficeRoomId.WeirdRoom, new OfficeCell(7, -3), 3),
                new("waiting-area.interact", OfficeRoomId.WaitingArea, new OfficeCell(-1, -3), 4),
            };
            return new OfficeGrid(-14, 14, -9, 9, blocked, points);
        }

        public bool IsInside(OfficeCell cell)
        {
            return cell.X >= MinX && cell.X <= MaxX &&
                cell.Z >= MinZ && cell.Z <= MaxZ;
        }

        public bool IsWalkable(OfficeCell cell)
        {
            return IsInside(cell) && !_blocked.Contains(cell);
        }

        public OfficeCell CellForLogicalPosition(int xSubunits, int zSubunits)
        {
            return new OfficeCell(
                FloorDiv(xSubunits + LogicalSubunitsPerCell / 2, LogicalSubunitsPerCell),
                FloorDiv(zSubunits + LogicalSubunitsPerCell / 2, LogicalSubunitsPerCell));
        }

        public OfficeCell SocketCell(OfficeRoomId room, int queueIndex)
        {
            OfficeCell baseCell = room switch
            {
                OfficeRoomId.FrontDesk => new OfficeCell(-11, 6),
                OfficeRoomId.PaperRoom => new OfficeCell(0, 6),
                OfficeRoomId.MoneyRoom => new OfficeCell(7, 6),
                OfficeRoomId.WeirdRoom => new OfficeCell(7, -2),
                OfficeRoomId.WaitingArea => new OfficeCell(-1, -2),
                _ => SpawnCell,
            };
            return new OfficeCell(baseCell.X, baseCell.Z - queueIndex);
        }

        public OfficeInteractionPoint GetInteractionPoint(string id)
        {
            for (int i = 0; i < _interactionPoints.Count; i++)
                if (string.Equals(_interactionPoints[i].Id, id, StringComparison.Ordinal))
                    return _interactionPoints[i];
            return null;
        }

        public OfficeInteractionPoint ChooseClosestInteractionPoint(OfficeCell from)
        {
            OfficeInteractionPoint best = null;
            int bestDistance = int.MaxValue;
            for (int i = 0; i < _interactionPoints.Count; i++)
            {
                OfficeInteractionPoint candidate = _interactionPoints[i];
                int distance = Math.Abs(candidate.Cell.X - from.X) +
                    Math.Abs(candidate.Cell.Z - from.Z);
                if (distance > 2) continue;
                if (best == null || distance < bestDistance ||
                    (distance == bestDistance &&
                        candidate.StablePriority < best.StablePriority) ||
                    (distance == bestDistance &&
                        candidate.StablePriority == best.StablePriority &&
                        string.CompareOrdinal(candidate.Id, best.Id) < 0))
                {
                    best = candidate;
                    bestDistance = distance;
                }
            }
            return best;
        }

        public bool TryFindPath(OfficeCell start, OfficeCell goal, out List<OfficeCell> path)
        {
            path = new List<OfficeCell>();
            if (!IsWalkable(start) || !IsWalkable(goal)) return false;

            var frontier = new Queue<OfficeCell>();
            var cameFrom = new Dictionary<OfficeCell, OfficeCell>();
            var visited = new HashSet<OfficeCell> { start };
            frontier.Enqueue(start);
            OfficeCell[] directions =
            {
                new(-1, 0), new(1, 0), new(0, -1), new(0, 1),
            };

            while (frontier.Count > 0)
            {
                OfficeCell current = frontier.Dequeue();
                if (current == goal) break;
                for (int i = 0; i < directions.Length; i++)
                {
                    OfficeCell next = new(
                        current.X + directions[i].X,
                        current.Z + directions[i].Z);
                    if (!IsWalkable(next) || !visited.Add(next)) continue;
                    cameFrom[next] = current;
                    frontier.Enqueue(next);
                }
            }

            if (!visited.Contains(goal)) return false;
            OfficeCell cursor = goal;
            path.Add(cursor);
            while (cursor != start)
            {
                cursor = cameFrom[cursor];
                path.Add(cursor);
            }
            path.Reverse();
            return true;
        }

        private static int FloorDiv(int value, int divisor)
        {
            return (int)Math.Floor(value / (double)divisor);
        }
    }
}
