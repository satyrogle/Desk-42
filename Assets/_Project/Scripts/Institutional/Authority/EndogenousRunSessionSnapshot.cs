using System;
using System.Collections.Generic;

namespace Desk42.Institutional
{
    /// <summary>
    /// One transactionally persisted playable session. Origin and current history are
    /// kept in the same checksum envelope so replay can never load from another save
    /// generation.
    /// </summary>
    [Serializable]
    internal sealed class EndogenousRunSessionSnapshot
    {
        internal const int CurrentSchemaVersion = 1;
        internal const string CurrentRulesetVersion =
            "endogenous-run-session-snapshot-v1";

        internal int SchemaVersion = CurrentSchemaVersion;
        internal string RulesetVersion = CurrentRulesetVersion;
        internal string SessionId;
        internal long Generation;
        internal string GenerationId;
        internal int SocietySeed;
        internal string InitialCaseId;
        internal string OriginSnapshotSha256;
        internal string CurrentOriginSnapshotSha256;
        internal string CurrentSnapshotSha256;
        internal EndogenousRunSnapshot Origin;
        internal EndogenousRunSnapshot Current;
    }

    internal static class EndogenousRunSessionSnapshotService
    {
        internal static EndogenousRunSessionSnapshot Capture(
            string sessionId,
            long generation,
            EndogenousRunSnapshot origin,
            EndogenousRunSnapshot current)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
                throw new ArgumentException("A stable session id is required.", nameof(sessionId));
            if (generation < 1) throw new ArgumentOutOfRangeException(nameof(generation));
            if (origin == null) throw new ArgumentNullException(nameof(origin));
            if (current == null) throw new ArgumentNullException(nameof(current));

            EndogenousRunSnapshot originCopy = Copy(
                origin, $"{sessionId}:origin:{generation}");
            EndogenousRunSnapshot currentCopy = Copy(
                current, $"{sessionId}:current:{generation}");
            string originHash = EndogenousRunSnapshotStore.PayloadSha256(originCopy);
            string currentHash = EndogenousRunSnapshotStore.PayloadSha256(currentCopy);
            var saved = new EndogenousRunSessionSnapshot
            {
                SessionId = sessionId,
                Generation = generation,
                SocietySeed = originCopy.Society.MasterSeed,
                InitialCaseId = originCopy.Docket.OpenCases.Count == 1
                    ? originCopy.Docket.OpenCases[0].CaseId
                    : string.Empty,
                OriginSnapshotSha256 = originHash,
                CurrentOriginSnapshotSha256 = originHash,
                CurrentSnapshotSha256 = currentHash,
                GenerationId = GenerationId(generation, originHash, currentHash),
                Origin = originCopy,
                Current = currentCopy,
            };
            EndogenousRunSessionSnapshotValidator.Validate(saved);
            return saved;
        }

        internal static string GenerationId(
            long generation,
            string originHash,
            string currentHash)
            => $"generation:{generation}:{originHash}:{currentHash}";

        private static EndogenousRunSnapshot Copy(
            EndogenousRunSnapshot source,
            string snapshotId)
            => EndogenousRunSnapshotService.Capture(
                snapshotId,
                source.Phase,
                source.Society,
                source.MaterialWorld,
                source.Docket);
    }

    internal static class EndogenousRunSessionSnapshotValidator
    {
        internal static void Validate(EndogenousRunSessionSnapshot saved)
        {
            if (saved == null) throw new ArgumentNullException(nameof(saved));
            if (saved.SchemaVersion != EndogenousRunSessionSnapshot.CurrentSchemaVersion ||
                !string.Equals(
                    saved.RulesetVersion,
                    EndogenousRunSessionSnapshot.CurrentRulesetVersion,
                    StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(saved.SessionId) || saved.Generation < 1 ||
                string.IsNullOrWhiteSpace(saved.GenerationId) ||
                string.IsNullOrWhiteSpace(saved.InitialCaseId) ||
                string.IsNullOrWhiteSpace(saved.OriginSnapshotSha256) ||
                string.IsNullOrWhiteSpace(saved.CurrentOriginSnapshotSha256) ||
                string.IsNullOrWhiteSpace(saved.CurrentSnapshotSha256) ||
                saved.Origin == null || saved.Current == null)
            {
                throw new InvalidOperationException(
                    "Playable session snapshot is incomplete or unsupported.");
            }

            EndogenousRunSnapshotValidator.Validate(saved.Origin);
            EndogenousRunSnapshotValidator.Validate(saved.Current);
            string originHash = EndogenousRunSnapshotStore.PayloadSha256(saved.Origin);
            string currentHash = EndogenousRunSnapshotStore.PayloadSha256(saved.Current);
            if (!string.Equals(
                    saved.OriginSnapshotSha256, originHash, StringComparison.Ordinal) ||
                !string.Equals(
                    saved.CurrentOriginSnapshotSha256, originHash, StringComparison.Ordinal) ||
                !string.Equals(
                    saved.CurrentSnapshotSha256, currentHash, StringComparison.Ordinal) ||
                !string.Equals(
                    saved.GenerationId,
                    EndogenousRunSessionSnapshotService.GenerationId(
                        saved.Generation, originHash, currentHash),
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Playable session generation hashes do not match their snapshots.");
            }

            if (saved.Origin.Docket.Rulings.Count != 0 ||
                saved.Origin.Docket.OpenCases.Count != 1 ||
                !saved.Origin.SnapshotId.StartsWith(
                    saved.SessionId + ":origin:",
                    StringComparison.Ordinal) ||
                !saved.Current.SnapshotId.StartsWith(
                    saved.SessionId + ":current:",
                    StringComparison.Ordinal) ||
                saved.Origin.Society.MasterSeed != saved.SocietySeed ||
                saved.Current.Society.MasterSeed != saved.SocietySeed ||
                saved.Current.CurrentTick < saved.Origin.CurrentTick)
            {
                throw new InvalidOperationException(
                    "Playable session origin and current history are incompatible.");
            }

            EndogenousInstitutionalCase originCase = saved.Origin.Docket.OpenCases[0];
            EndogenousInstitutionalCase currentCase =
                saved.Current.Docket.GetCase(saved.InitialCaseId);
            if (!string.Equals(
                    originCase.CaseId, saved.InitialCaseId, StringComparison.Ordinal) ||
                currentCase == null || !string.Equals(
                    currentCase.DocketCandidateId,
                    originCase.DocketCandidateId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    currentCase.EvidenceEnvelopeHash,
                    originCase.EvidenceEnvelopeHash,
                    StringComparison.Ordinal) ||
                !ContainsEvery(
                    saved.Current.AppliedTransitionIds,
                    saved.Origin.AppliedTransitionIds))
            {
                throw new InvalidOperationException(
                    "Current history is not a valid descendant of its replay origin.");
            }
        }

        private static bool ContainsEvery(
            IReadOnlyList<string> current,
            IReadOnlyList<string> origin)
        {
            var available = new HashSet<string>(current, StringComparer.Ordinal);
            for (int i = 0; i < origin.Count; i++)
                if (!available.Contains(origin[i])) return false;
            return true;
        }
    }
}
