// ============================================================
// DESK 42 — Seed Serializer
//
// Wraps SeedEngine share codes with clipboard integration and
// a compound share-code format that encodes run configuration:
//
//   Format: SEED-ARCHETYPE[-VOW1.R[-VOW2.R[...]]]
//   Example: K3M9XZ-auditor-zero_tolerance.2-commission_only.1
//
// This lets players share exact run configurations for
// competitive play or replay comparisons.
// ============================================================

using System.Text;
using System.Collections.Generic;
using UnityEngine;

namespace Desk42.Core
{
    public static class SeedSerializer
    {
        private const char SEGMENT_SEP = '-';
        private const char VOW_RANK_SEP = '.';

        // ── Encode ────────────────────────────────────────────

        /// <summary>
        /// Build a shareable code from the current run state.
        /// Returns null if no active run exists.
        /// </summary>
        public static string EncodeCurrentRun()
        {
            var run = GameManager.Instance?.Run;
            if (run == null) return null;

            var sb = new StringBuilder();
            sb.Append(SeedEngine.CurrentSeedCode);
            sb.Append(SEGMENT_SEP);
            sb.Append(run.ArchetypeId ?? "auditor");

            var vows = run.RawData?.ActiveVows;
            if (vows != null)
            {
                foreach (var vow in vows)
                {
                    sb.Append(SEGMENT_SEP);
                    sb.Append(vow.VowId);
                    sb.Append(VOW_RANK_SEP);
                    sb.Append(vow.Rank);
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Encode a specific seed + archetype + vow list.
        /// Used for Daily Brief share codes.
        /// </summary>
        public static string Encode(int masterSeed, string archetypeId,
            IReadOnlyList<ActiveVow> vows = null)
        {
            string seedCode = SeedEngine.SeedToCode(masterSeed);

            var sb = new StringBuilder();
            sb.Append(seedCode);
            sb.Append(SEGMENT_SEP);
            sb.Append(archetypeId);

            if (vows != null)
            {
                foreach (var vow in vows)
                {
                    sb.Append(SEGMENT_SEP);
                    sb.Append(vow.VowId);
                    sb.Append(VOW_RANK_SEP);
                    sb.Append(vow.Rank);
                }
            }

            return sb.ToString();
        }

        // ── Decode ────────────────────────────────────────────

        /// <summary>
        /// Parse a share code into its components.
        /// Returns false if the format is invalid.
        /// </summary>
        public static bool TryDecode(string shareCode, out int masterSeed,
            out string archetypeId, out List<ActiveVow> vows)
        {
            masterSeed  = 0;
            archetypeId = null;
            vows        = new List<ActiveVow>();

            if (string.IsNullOrWhiteSpace(shareCode)) return false;

            var segments = shareCode.Trim().Split(SEGMENT_SEP);
            if (segments.Length < 2) return false;

            // Segment 0: seed code
            if (!SeedEngine.TryParseSeedCode(segments[0], out masterSeed))
                return false;

            // Segment 1: archetype
            archetypeId = segments[1];

            // Segments 2+: vows
            for (int i = 2; i < segments.Length; i++)
            {
                var parts = segments[i].Split(VOW_RANK_SEP);
                if (parts.Length != 2) continue;
                if (!int.TryParse(parts[1], out int rank)) continue;

                vows.Add(new ActiveVow
                {
                    VowId = parts[0],
                    Rank  = Mathf.Clamp(rank, 1, 3),
                });
            }

            return true;
        }

        // ── Clipboard ─────────────────────────────────────────

        /// <summary>Copy the current run's share code to the system clipboard.</summary>
        public static void CopyToClipboard()
        {
            string code = EncodeCurrentRun();
            if (code == null)
            {
                Debug.LogWarning("[SeedSerializer] No active run to share.");
                return;
            }

            GUIUtility.systemCopyBuffer = code;
            Debug.Log($"[SeedSerializer] Share code copied: {code}");
        }

        /// <summary>Read a share code from the system clipboard and parse it.</summary>
        public static bool TryPasteFromClipboard(out int masterSeed,
            out string archetypeId, out List<ActiveVow> vows)
        {
            string clipboard = GUIUtility.systemCopyBuffer;
            return TryDecode(clipboard, out masterSeed, out archetypeId, out vows);
        }
    }
}

