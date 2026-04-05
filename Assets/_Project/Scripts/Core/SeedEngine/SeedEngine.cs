// ============================================================
// DESK 42 — Seed Engine
//
// ALL procedural decisions in the game route through here.
// Never call UnityEngine.Random or System.Random directly.
//
// Design:
//   - One master seed (int) per run.
//   - Seven named sub-streams, each with its own independent
//     System.Random instance seeded by hash(masterSeed, stream).
//   - Streams are independent: drawing a card draft option
//     does NOT affect what shop inventory appears.
//   - The same master seed ALWAYS produces the same run.
//
// Usage:
//   SeedEngine.Init(seed);
//   int claimIndex = SeedEngine.Next(SeedStream.ClaimQueue, 0, clientPool.Count);
//   float roll = SeedEngine.NextFloat(SeedStream.ClientBehaviourTree);
//   bool coinFlip = SeedEngine.NextBool(SeedStream.RumorMillEvents);
//
// Share codes:
//   string code = SeedEngine.CurrentSeedCode;   // e.g. "K3M9XZ"
//   SeedEngine.Init(SeedEngine.ParseSeedCode("K3M9XZ"));
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Desk42.Core
{
    /// <summary>
    /// Named RNG streams — one per procedural domain.
    /// Adding a stream: add an enum value, the dict in Init() auto-handles it.
    /// </summary>
    public enum SeedStream
    {
        ClaimQueue,           // claim types, species, hidden traits per encounter
        ShopInventory,        // which supplies appear and when
        CardDraft,            // which cards offered at each selection
        RumorMillEvents,      // which cascades trigger and when
        OfficeTempCycle,      // PEAK / LUNCH / CRASH phase durations
        FactionDispositions,  // starting friendly/neutral/hostile per dept
        RegulationOrder,      // which regulations appear on which days
        ClientBehaviourTree,  // within-encounter BT mutation rolls + tells
        MutationGeneration,   // counter-trait selection for Repeat Offenders
        FormCorruption,       // which form fields corrupt and how
        AudioVariation,       // procedural jazz pattern variation
        MoralDilemma,         // dilemma type selection + consequence weighting
    }

    public static class SeedEngine
    {
        // ── State ─────────────────────────────────────────────

        private static int _masterSeed;
        private static readonly Dictionary<SeedStream, System.Random> _streams
            = new(12);

        private static bool _isInitialized;

        // ── Constants ─────────────────────────────────────────

        private const string SHARE_CODE_CHARS = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        private const int    SHARE_CODE_LENGTH = 6;

        // ── Initialisation ────────────────────────────────────

        /// <summary>
        /// Initialise with a specific seed. Call once at the start of each run.
        /// Re-initialising with the same seed resets all streams to their
        /// starting state, guaranteeing identical output.
        /// </summary>
        public static void Init(int masterSeed)
        {
            _masterSeed = masterSeed;
            _streams.Clear();

            foreach (SeedStream stream in Enum.GetValues(typeof(SeedStream)))
            {
                // Each stream gets a unique seed derived from the master.
                // FNV-1a hash gives good avalanche effect from small int inputs.
                int streamSeed = FNV1aHash(masterSeed ^ ((int)stream * 2654435761));
                _streams[stream] = new System.Random(streamSeed);
            }

            _isInitialized = true;
            Debug.Log($"[SeedEngine] Initialised with seed {masterSeed} ({CurrentSeedCode}).");
        }

        /// <summary>Initialise with a random seed. Use for non-seeded runs.</summary>
        public static void InitRandom()
        {
            Init(new System.Random().Next());
        }

        // ── Draw API ──────────────────────────────────────────

        /// <summary>
        /// Returns a random int in [minInclusive, maxExclusive).
        /// Equivalent to Random.Range for integers.
        /// </summary>
        public static int Next(SeedStream stream, int minInclusive, int maxExclusive)
        {
            AssertInitialized();
            return _streams[stream].Next(minInclusive, maxExclusive);
        }

        /// <summary>Returns a random int in [0, maxExclusive).</summary>
        public static int Next(SeedStream stream, int maxExclusive)
            => Next(stream, 0, maxExclusive);

        /// <summary>Returns a random float in [0.0, 1.0).</summary>
        public static float NextFloat(SeedStream stream)
        {
            AssertInitialized();
            return (float)_streams[stream].NextDouble();
        }

        /// <summary>Returns a random float in [min, max).</summary>
        public static float NextFloat(SeedStream stream, float min, float max)
            => min + (max - min) * NextFloat(stream);

        /// <summary>Returns true with the given probability (0–1).</summary>
        public static bool NextBool(SeedStream stream, float probability = 0.5f)
            => NextFloat(stream) < probability;

        /// <summary>Shuffle a list in-place using Fisher-Yates.</summary>
        public static void Shuffle<T>(SeedStream stream, IList<T> list)
        {
            AssertInitialized();
            var rng = _streams[stream];
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        /// <summary>
        /// Pick a weighted random index. weights[i] is the relative probability
        /// of index i being selected. Does NOT need to sum to 1.
        /// </summary>
        public static int WeightedRandom(SeedStream stream, float[] weights)
        {
            AssertInitialized();
            float total = 0f;
            foreach (float w in weights) total += w;

            float roll = NextFloat(stream) * total;
            float cumulative = 0f;

            for (int i = 0; i < weights.Length; i++)
            {
                cumulative += weights[i];
                if (roll < cumulative) return i;
            }

            return weights.Length - 1; // fallback for floating point edge
        }

        // ── Share Codes ───────────────────────────────────────

        /// <summary>
        /// The 6-character alphanumeric share code for the current seed.
        /// e.g. "K3M9XZ" — displayed to the player and shareable.
        /// </summary>
        public static string CurrentSeedCode
        {
            get
            {
                AssertInitialized();
                return SeedToCode(_masterSeed);
            }
        }

        public static int CurrentMasterSeed
        {
            get { AssertInitialized(); return _masterSeed; }
        }

        /// <summary>
        /// Parse a share code back into a master seed int.
        /// Returns false if the code is invalid.
        /// </summary>
        public static bool TryParseSeedCode(string code, out int seed)
        {
            seed = 0;
            if (string.IsNullOrWhiteSpace(code) || code.Length != SHARE_CODE_LENGTH)
                return false;

            code = code.ToUpperInvariant();
            int result = 0;
            int baseN  = SHARE_CODE_CHARS.Length;

            foreach (char c in code)
            {
                int idx = SHARE_CODE_CHARS.IndexOf(c);
                if (idx < 0) return false;
                result = result * baseN + idx;
            }

            seed = result;
            return true;
        }

        // ── Private Helpers ───────────────────────────────────

        private static string SeedToCode(int seed)
        {
            int baseN = SHARE_CODE_CHARS.Length;
            char[] chars = new char[SHARE_CODE_LENGTH];
            uint useed = (uint)seed; // work unsigned to handle negative ints

            for (int i = SHARE_CODE_LENGTH - 1; i >= 0; i--)
            {
                chars[i] = SHARE_CODE_CHARS[(int)(useed % (uint)baseN)];
                useed /= (uint)baseN;
            }

            return new string(chars);
        }

        private static int FNV1aHash(int value)
        {
            // FNV-1a 32-bit — good avalanche, fast
            unchecked
            {
                uint hash = 2166136261u;
                byte[] bytes = BitConverter.GetBytes(value);
                foreach (byte b in bytes)
                {
                    hash ^= b;
                    hash *= 16777619u;
                }
                return (int)hash;
            }
        }

        private static void AssertInitialized()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!_isInitialized)
                throw new InvalidOperationException(
                    "[SeedEngine] Not initialised. Call SeedEngine.Init(seed) " +
                    "before drawing from any stream.");
#endif
        }
    }
}
