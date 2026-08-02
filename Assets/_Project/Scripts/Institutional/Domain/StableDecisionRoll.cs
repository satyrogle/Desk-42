using System;

namespace Desk42.Institutional
{
    /// <summary>
    /// Stateless keyed variation for decisions. Results do not depend on call order,
    /// collection order, Unity's RNG, process-specific string hashes, or saved RNG cursors.
    /// </summary>
    public static class StableDecisionRoll
    {
        private const ulong OffsetBasis = 14695981039346656037UL;
        private const ulong Prime = 1099511628211UL;

        public static int Range(
            int masterSeed,
            long tick,
            string agentId,
            string candidateId,
            int minInclusive,
            int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
                throw new ArgumentOutOfRangeException(nameof(maxExclusive), "Maximum must exceed minimum.");

            ulong hash = OffsetBasis;
            Append(ref hash, unchecked((ulong)(uint)masterSeed));
            Append(ref hash, unchecked((ulong)tick));
            Append(ref hash, agentId ?? string.Empty);
            Append(ref hash, candidateId ?? string.Empty);

            long signedWidth = (long)maxExclusive - minInclusive;
            ulong width = unchecked((ulong)signedWidth);
            long result = (long)minInclusive + (long)(hash % width);
            return (int)result;
        }

        private static void Append(ref ulong hash, ulong value)
        {
            for (int i = 0; i < 8; i++)
            {
                hash ^= value & 0xffUL;
                hash *= Prime;
                value >>= 8;
            }
        }

        private static void Append(ref ulong hash, string value)
        {
            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];
                hash ^= (byte)(character & 0xff);
                hash *= Prime;
                hash ^= (byte)(character >> 8);
                hash *= Prime;
            }
        }
    }
}
