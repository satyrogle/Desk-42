using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Desk42.Institutional.Player;
using UnityEngine;

namespace Desk42.Product.Automation
{
    internal enum AutomationRunPhase
    {
        DoctrineSelection = 1,
        ActiveProcessing = 2,
        ShiftClose = 3,
        BranchReview = 4,
    }

    internal enum AutomationBranchOutcome
    {
        Certified = 1,
        EfficientButHarmful = 2,
        HumaneButInsolvent = 3,
        Captured = 4,
        PrecedentCollapse = 5,
        AdministrativeBlindness = 6,
    }

    [Serializable]
    internal sealed class AutomationRunCheckpoint
    {
        internal const int CurrentSchemaVersion = 2;

        public int SchemaVersion = CurrentSchemaVersion;
        public InstitutionalAutomationCheckpoint Institution;
        public AutomationFlowCheckpoint Flow;
    }

    [Serializable]
    internal sealed class AutomationFlowCheckpoint
    {
        public AutomationRunPhase Phase;
        public AutomationPolicyKind Policy;
        public bool DoctrineLocked;
        public int Spawned;
        public int Completed;
        public int AppealsReturned;
        public int AppealsResolved;
        public int OverdueCount;
        public int ReworkCount;
        public int JamCount;
        public int RepairCount;
        public int SecondaryChecks;
        public int PossessionCompleted;
        public int AccessCompleted;
        public int CollectiveCompleted;
        public int IdentityCompleted;
        public int DependencyCompleted;
        public int ProvisionalReliefGranted;
        public int ReliefReserve;
        public int RelianceExposure;
        public int Credits;
        public float Elapsed;
        public float SpawnClock;
        public float SpawnInterval;
        public int BatchSpawned;
        public int ShiftOrdinal;
        public int RouteOrdinal;
        public int StationSelectionIndex;
        public bool ParallelRouting;
        public AutomationRoutePriority RoutePriority;
        public AutomationAppealMode AppealMode;
        public int ShiftStartCompleted;
        public int ShiftStartOverdue;
        public int ShiftStartAppealsReturned;
        public int ShiftStartAppealsResolved;
        public int ShiftStartRulings;
        public int ShiftStartHoldings;
        public long ShiftStartSocietyTick;
        public List<AutomationProcedureTierCheckpoint> Procedures = new();
        public List<string> VerificationPatternIssues = new();
        public List<string> AdverseReviewPatternIssues = new();
        public List<AutomationProcedureDraftChoiceCheckpoint> DraftChoices = new();
        public AutomationShiftSummaryCheckpoint ShiftSummary;
        public AutomationBranchReviewCheckpoint BranchReview;
        public List<AutomationStationCheckpoint> Stations = new();
        public List<AutomationFlowItemCheckpoint> Items = new();
    }

    [Serializable]
    internal sealed class AutomationProcedureTierCheckpoint
    {
        public AutomationProcedureKind Kind;
        public int Tier;
    }

    [Serializable]
    internal sealed class AutomationProcedureDraftChoiceCheckpoint
    {
        public AutomationProcedureKind Kind;
        public int ResultingTier;
    }

    [Serializable]
    internal sealed class AutomationShiftSummaryCheckpoint
    {
        public int ShiftOrdinal;
        public int ClaimsCompleted;
        public int DeadlinesMissed;
        public int AppealsCreated;
        public int AppealsResolved;
        public int HoldingsEstablished;
        public int SocietyChanges;
    }

    [Serializable]
    internal sealed class AutomationBranchReviewCheckpoint
    {
        public AutomationBranchOutcome Outcome;
        public int Throughput;
        public int DeadlineCompliance;
        public int AvoidableError;
        public int AppealReversalRate;
        public int UnresolvedLiability;
        public int SocietyStability;
        public int InstitutionalLegitimacy;
        public int PrecedentConsistency;
        public int MachineResilience;
    }

    [Serializable]
    internal sealed class AutomationStationCheckpoint
    {
        public AutomationStationKind Kind;
        public bool IsAuxiliary;
        public int ThroughputLevel;
        public int CapacityLevel;
        public int ReliabilityLevel;
        public bool IsJammed;
        public float Heat;
        public float Remaining;
        public string ActiveItemId;
        public List<string> QueuedItemIds = new();
    }

    [Serializable]
    internal sealed class AutomationFlowItemCheckpoint
    {
        public string FlowItemId;
        public bool IsAppeal;
        public string AutomationClaimId;
        public string AppealId;
        public string DisplayId;
        public int Sequence;
        public float Age;
        public bool DeadlineReported;
        public bool Misclassified;
        public int ReworkAttempts;
        public int VerificationPasses;
        public bool AdverseReviewPending;
        public bool AdverseReviewCompleted;
        public bool PresumptionOfValidity;
        public bool ProtectedEvidenceChannel;
        public int PresumptionTier;
        public int ProtectedChannelTier;
        public bool RulingApplied;
        public bool AppealResolutionApplied;
        public bool EvidenceRevealed;
    }

    internal static class AutomationRunStore
    {
        private const int EnvelopeVersion = 1;
        private static readonly Encoding Utf8WithoutBom = new UTF8Encoding(false);

        [Serializable]
        private sealed class Envelope
        {
            public int EnvelopeVersion;
            public string PayloadSha256;
            public string Payload;
        }

        internal static void Save(string path, AutomationRunCheckpoint checkpoint)
        {
            Validate(checkpoint);
            string payload = JsonUtility.ToJson(checkpoint);
            var envelope = new Envelope
            {
                EnvelopeVersion = EnvelopeVersion,
                PayloadSha256 = Sha256(payload),
                Payload = payload,
            };
            WriteAtomically(path, JsonUtility.ToJson(envelope));
        }

        internal static AutomationRunCheckpoint Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("A run save path is required.", nameof(path));
            string fullPath = Path.GetFullPath(path);
            try
            {
                return LoadExact(fullPath);
            }
            catch (Exception primaryError)
            {
                string backupPath = fullPath + ".bak";
                if (!File.Exists(backupPath)) throw;
                try
                {
                    return LoadExact(backupPath);
                }
                catch (Exception backupError)
                {
                    throw new AggregateException(
                        "Primary and backup automation runs are invalid.",
                        primaryError,
                        backupError);
                }
            }
        }

        private static AutomationRunCheckpoint LoadExact(string path)
        {
            string json = File.ReadAllText(path, Utf8WithoutBom);
            Envelope envelope = JsonUtility.FromJson<Envelope>(json);
            if (envelope == null || envelope.EnvelopeVersion != EnvelopeVersion ||
                string.IsNullOrWhiteSpace(envelope.PayloadSha256) ||
                string.IsNullOrWhiteSpace(envelope.Payload) ||
                !string.Equals(envelope.PayloadSha256, Sha256(envelope.Payload),
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "Automation run envelope is incomplete or corrupt.");
            }
            AutomationRunCheckpoint checkpoint =
                JsonUtility.FromJson<AutomationRunCheckpoint>(envelope.Payload);
            Validate(checkpoint);
            return checkpoint;
        }

        private static void Validate(AutomationRunCheckpoint checkpoint)
        {
            if (checkpoint == null || checkpoint.SchemaVersion !=
                    AutomationRunCheckpoint.CurrentSchemaVersion ||
                checkpoint.Institution == null || checkpoint.Flow == null ||
                !Enum.IsDefined(typeof(AutomationRunPhase),
                    checkpoint.Flow.Phase) ||
                checkpoint.Flow.Procedures == null ||
                checkpoint.Flow.VerificationPatternIssues == null ||
                checkpoint.Flow.AdverseReviewPatternIssues == null ||
                checkpoint.Flow.DraftChoices == null ||
                checkpoint.Flow.Stations == null ||
                checkpoint.Flow.Items == null)
            {
                throw new InvalidOperationException(
                    "Automation run checkpoint is incomplete or unsupported.");
            }
        }

        private static void WriteAtomically(string path, string json)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("A run save path is required.", nameof(path));
            string fullPath = Path.GetFullPath(path);
            string directory = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrWhiteSpace(directory))
                throw new InvalidOperationException("Run save path has no directory.");
            Directory.CreateDirectory(directory);
            string temporaryPath = fullPath + ".tmp";
            string backupPath = fullPath + ".bak";
            using (var stream = new FileStream(
                       temporaryPath,
                       FileMode.Create,
                       FileAccess.Write,
                       FileShare.None,
                       4096,
                       FileOptions.WriteThrough))
            using (var writer = new StreamWriter(stream, Utf8WithoutBom))
            {
                writer.Write(json);
                writer.Flush();
                stream.Flush(true);
            }
            if (!File.Exists(fullPath))
            {
                File.Move(temporaryPath, fullPath);
                return;
            }
            try
            {
                File.Replace(temporaryPath, fullPath, backupPath, true);
            }
            catch (PlatformNotSupportedException)
            {
                File.Copy(fullPath, backupPath, true);
                File.Delete(fullPath);
                File.Move(temporaryPath, fullPath);
            }
        }

        private static string Sha256(string value)
        {
            using SHA256 algorithm = SHA256.Create();
            byte[] bytes = algorithm.ComputeHash(Utf8WithoutBom.GetBytes(value));
            var result = new StringBuilder(bytes.Length * 2);
            for (int i = 0; i < bytes.Length; i++)
                result.Append(bytes[i].ToString("x2"));
            return result.ToString();
        }
    }
}
