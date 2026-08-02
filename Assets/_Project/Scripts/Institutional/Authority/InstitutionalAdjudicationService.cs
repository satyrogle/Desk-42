using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Desk42.Institutional
{
    /// <summary>
    /// Scenario-owned inputs for one adjudication. The request identifies the
    /// evidence envelope and applicable rules; the service owns report projection.
    /// </summary>
    internal sealed class InstitutionalAdjudicationRequest
    {
        internal string CaseId;
        internal string IssueId;
        internal string PhaseId;
        internal long Cycle;
        internal long MaximumEvidenceCycle;
        internal InstitutionalPolicyConfiguration PolicyConfiguration;
        internal int RequiredEvidenceScore;
        internal bool PermitProvisionalRecognition;
        internal int? ProvisionalEvidenceScore;
        internal int CitedHoldingWeight;
        internal List<string> CitedHoldingIds = new();
        internal CaseFactSet CaseFacts = new();
        // When true, citation IDs are validated and scored here but projected by
        // InstitutionalAppealPrecedentService as one exact-once graph mutation.
        internal bool DeferCitationProjection;
        internal List<string> AppliedPolicyIds = new();
        internal List<string> SkippedProcedureIds = new();
    }

    /// <summary>
    /// Immutable detached evidence used by an adjudication result. These are the
    /// score inputs captured at decision time, not live report artifacts.
    /// </summary>
    internal sealed class AdjudicationEvidenceSnapshot
    {
        internal string ArtifactId { get; }
        internal string CaseId { get; }
        internal long EnteredCycle { get; }
        internal EvidenceArtifactKind Kind { get; }
        internal string EvidenceClassId { get; }
        internal string IssueId { get; }
        internal string PropositionId { get; }
        internal EvidenceEffect Effect { get; }
        internal int BaseWeight { get; }
        internal int Reliability { get; }
        internal bool OfficiallySubmitted { get; }
        internal string ProvenanceId { get; }
        internal string SourceDecisionId { get; }
        internal string SourceSocietyEventId { get; }

        internal AdjudicationEvidenceSnapshot(EvidenceArtifact artifact)
        {
            if (artifact == null) throw new ArgumentNullException(nameof(artifact));
            ArtifactId = artifact.ArtifactId;
            CaseId = artifact.CaseId;
            EnteredCycle = artifact.EnteredCycle;
            Kind = artifact.Kind;
            EvidenceClassId = artifact.EvidenceClassId;
            IssueId = artifact.IssueId;
            PropositionId = artifact.PropositionId;
            Effect = artifact.Effect;
            BaseWeight = artifact.BaseWeight;
            Reliability = artifact.Reliability;
            OfficiallySubmitted = artifact.OfficiallySubmitted;
            ProvenanceId = artifact.Provenance?.ProvenanceId;
            SourceDecisionId = artifact.Provenance?.SourceDecisionId;
            SourceSocietyEventId = artifact.Provenance?.SourceSocietyEventId;
        }
    }

    /// <summary>
    /// Complete output from one evidence-to-ruling pass. EvidenceScoreMinimum and
    /// EvidenceScoreMaximum are weighted score bounds, never probabilities.
    /// </summary>
    internal sealed class InstitutionalAdjudicationResult
    {
        internal IReadOnlyList<AdjudicationEvidenceSnapshot> FrozenEvidence { get; }
        internal OfficialFinding Finding { get; }
        internal Ruling Ruling { get; }
        internal int EvidenceScore { get; }
        internal int EvidenceScoreMinimum { get; }
        internal int EvidenceScoreMaximum { get; }
        internal bool SubstantivelyRecognised { get; }

        internal InstitutionalAdjudicationResult(
            List<AdjudicationEvidenceSnapshot> frozenEvidence,
            OfficialFinding finding,
            Ruling ruling,
            int evidenceScore,
            int evidenceScoreMinimum,
            int evidenceScoreMaximum,
            bool substantivelyRecognised)
        {
            FrozenEvidence = new ReadOnlyCollection<AdjudicationEvidenceSnapshot>(
                frozenEvidence ?? throw new ArgumentNullException(nameof(frozenEvidence)));
            Finding = finding ?? throw new ArgumentNullException(nameof(finding));
            Ruling = ruling ?? throw new ArgumentNullException(nameof(ruling));
            EvidenceScore = evidenceScore;
            EvidenceScoreMinimum = evidenceScoreMinimum;
            EvidenceScoreMaximum = evidenceScoreMaximum;
            SubstantivelyRecognised = substantivelyRecognised;
        }
    }

    /// <summary>
    /// Converts a bounded evidence envelope into findings and rulings without
    /// knowing scenario roles, status effects, material remedies, or holdings.
    /// </summary>
    internal static class InstitutionalAdjudicationService
    {
        internal static InstitutionalAdjudicationResult IssueInitial(
            InstitutionalConsequenceReport report,
            InstitutionalAdjudicationRequest request)
        {
            ValidateCore(report, request);
            PreparedAdjudication prepared = Prepare(report, request);

            FindingDisposition findingDisposition;
            RulingDisposition rulingDisposition;
            bool recognised;
            if (prepared.Score >= request.RequiredEvidenceScore)
            {
                findingDisposition = FindingDisposition.Established;
                rulingDisposition = RulingDisposition.Recognised;
                recognised = true;
            }
            else if (request.PermitProvisionalRecognition &&
                     prepared.Score >= request.ProvisionalEvidenceScore.Value)
            {
                findingDisposition = FindingDisposition.ProvisionallyEstablished;
                rulingDisposition = RulingDisposition.ProvisionallyRecognised;
                recognised = true;
            }
            else
            {
                findingDisposition = FindingDisposition.NotEstablished;
                rulingDisposition = RulingDisposition.Denied;
                recognised = false;
            }

            OfficialFinding finding = CreateFinding(
                request,
                prepared,
                findingDisposition);
            Ruling ruling = CreateRuling(request, prepared, finding, rulingDisposition);
            EnsureRowsAreNew(report, finding, ruling);

            report.OfficialFindings.Add(finding);
            report.Rulings.Add(ruling);
            InstitutionalTimeline.Add(
                report,
                request.Cycle,
                InstitutionalTimelineKind.RulingIssued,
                ruling.RulingId,
                request.CaseId,
                ruling.Disposition.ToString());

            return CreateResult(prepared, finding, ruling, recognised);
        }

        internal static InstitutionalAdjudicationResult ResolveAppeal(
            InstitutionalConsequenceReport report,
            InstitutionalAdjudicationRequest request,
            Ruling challengedRuling,
            Appeal filedAppeal)
        {
            ValidateCore(report, request);
            OfficialFinding challengedFinding = ValidateAppeal(
                report,
                request,
                challengedRuling,
                filedAppeal);
            PreparedAdjudication prepared = Prepare(report, request);
            bool finalRecognised = prepared.Score >= request.RequiredEvidenceScore;
            bool challengedRecognised = IsSubstantivelyRecognised(challengedFinding);

            RulingDisposition rulingDisposition;
            if (finalRecognised == challengedRecognised)
            {
                rulingDisposition = RulingDisposition.Affirmed;
            }
            else
            {
                rulingDisposition = finalRecognised
                    ? RulingDisposition.ReversedAndRecognised
                    : RulingDisposition.ReversedAndDenied;
            }

            OfficialFinding finding = CreateFinding(
                request,
                prepared,
                finalRecognised
                    ? FindingDisposition.Established
                    : FindingDisposition.NotEstablished);
            Ruling ruling = CreateRuling(request, prepared, finding, rulingDisposition);
            EnsureRowsAreNew(report, finding, ruling);
            EnsureNoTimelineEntry(
                report,
                InstitutionalTimelineKind.AppealHeard,
                filedAppeal.AppealId);

            report.OfficialFindings.Add(finding);
            report.Rulings.Add(ruling);
            InstitutionalTimeline.Add(
                report,
                request.Cycle,
                InstitutionalTimelineKind.AppealHeard,
                filedAppeal.AppealId,
                request.CaseId,
                ruling.RulingId);
            InstitutionalTimeline.Add(
                report,
                request.Cycle,
                InstitutionalTimelineKind.RulingIssued,
                ruling.RulingId,
                request.CaseId,
                ruling.Disposition.ToString());

            filedAppeal.Disposition = finalRecognised == challengedRecognised
                ? AppealDisposition.Affirmed
                : AppealDisposition.Reversed;
            filedAppeal.ResultingRulingId = ruling.RulingId;

            return CreateResult(prepared, finding, ruling, finalRecognised);
        }

        private static PreparedAdjudication Prepare(
            InstitutionalConsequenceReport report,
            InstitutionalAdjudicationRequest request)
        {
            EvidenceEvaluation evaluation = InstitutionalEvidencePipeline.Evaluate(
                report,
                request.CaseId,
                request.MaximumEvidenceCycle,
                request.PolicyConfiguration);
            ValidateEvidenceEnvelope(request, evaluation.Evidence);
            ValidateCitations(report, request);

            int score = checked(evaluation.Score + request.CitedHoldingWeight);
            int minimum = checked(evaluation.MinimumScore + request.CitedHoldingWeight);
            int maximum = checked(evaluation.MaximumScore + request.CitedHoldingWeight);
            var frozen = new List<AdjudicationEvidenceSnapshot>(evaluation.Evidence.Count);
            for (int i = 0; i < evaluation.Evidence.Count; i++)
                frozen.Add(new AdjudicationEvidenceSnapshot(evaluation.Evidence[i]));

            return new PreparedAdjudication(
                score,
                minimum,
                maximum,
                evaluation.Evidence,
                frozen,
                ResolveCitedScopeIds(report, request.CitedHoldingIds));
        }

        private static OfficialFinding CreateFinding(
            InstitutionalAdjudicationRequest request,
            PreparedAdjudication prepared,
            FindingDisposition disposition)
        {
            OfficialFinding finding = InstitutionalEvidencePipeline.CreateFinding(
                request.CaseId,
                request.IssueId,
                request.Cycle,
                request.PhaseId,
                disposition,
                prepared.Score,
                request.RequiredEvidenceScore,
                prepared.Evidence);
            finding.PrecedentWeightApplied = request.CitedHoldingWeight;
            return finding;
        }

        private static Ruling CreateRuling(
            InstitutionalAdjudicationRequest request,
            PreparedAdjudication prepared,
            OfficialFinding finding,
            RulingDisposition disposition)
        {
            return new Ruling
            {
                RulingId = $"ruling:{request.CaseId}:{request.PhaseId}:{request.Cycle}",
                CaseId = request.CaseId,
                Cycle = request.Cycle,
                PolicyConfigurationId = request.PolicyConfiguration.PolicyConfigurationId,
                PolicyVersion = request.PolicyConfiguration.PolicyVersion,
                Disposition = disposition,
                FindingId = finding.FindingId,
                // Legacy field names retained for report compatibility. These values
                // are weighted evidence-score bounds and may exceed [0, 100].
                ConfidenceMinimum = prepared.MinimumScore,
                ConfidenceMaximum = prepared.MaximumScore,
                EvidenceArtifactIds = InstitutionalEvidencePipeline.CopyIds(prepared.Evidence),
                AppliedPolicyIds = CopyIdentifiers(request.AppliedPolicyIds),
                SkippedProcedureIds = CopyIdentifiers(request.SkippedProcedureIds),
                CitedHoldingIds = request.DeferCitationProjection
                    ? new List<string>()
                    : CopyIdentifiers(request.CitedHoldingIds),
                CitedScopeIds = request.DeferCitationProjection
                    ? new List<string>()
                    : prepared.CitedScopeIds,
            };
        }

        private static InstitutionalAdjudicationResult CreateResult(
            PreparedAdjudication prepared,
            OfficialFinding finding,
            Ruling ruling,
            bool recognised)
        {
            return new InstitutionalAdjudicationResult(
                prepared.FrozenEvidence,
                finding,
                ruling,
                prepared.Score,
                prepared.MinimumScore,
                prepared.MaximumScore,
                recognised);
        }

        private static void ValidateCore(
            InstitutionalConsequenceReport report,
            InstitutionalAdjudicationRequest request)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            if (request == null) throw new ArgumentNullException(nameof(request));
            RequireId(request.CaseId, nameof(request.CaseId));
            RequireId(request.IssueId, nameof(request.IssueId));
            RequireId(request.PhaseId, nameof(request.PhaseId));
            if (request.Cycle < 0)
                throw new InvalidOperationException("Adjudication cycle cannot be negative.");
            if (request.MaximumEvidenceCycle < 0 ||
                request.MaximumEvidenceCycle > request.Cycle)
            {
                throw new InvalidOperationException(
                    "The evidence envelope must close on or before the adjudication cycle.");
            }
            if (request.PolicyConfiguration == null)
                throw new InvalidOperationException("Policy configuration is required.");
            RequireId(
                request.PolicyConfiguration.PolicyConfigurationId,
                nameof(request.PolicyConfiguration.PolicyConfigurationId));
            RequireId(
                request.PolicyConfiguration.PolicyVersion,
                nameof(request.PolicyConfiguration.PolicyVersion));
            if (request.PolicyConfiguration.EvidenceClassWeights == null)
                throw new InvalidOperationException("Evidence class weights cannot be null.");
            if (request.RequiredEvidenceScore < 1)
                throw new InvalidOperationException("Required evidence score must be positive.");
            if (request.CitedHoldingWeight < 0)
                throw new InvalidOperationException("Cited holding weight cannot be negative.");
            if (request.CaseFacts == null)
                throw new InvalidOperationException("Adjudication case facts are required.");
            request.CaseFacts.Validate();

            if (request.PermitProvisionalRecognition)
            {
                if (!request.ProvisionalEvidenceScore.HasValue ||
                    request.ProvisionalEvidenceScore.Value < 1)
                {
                    throw new InvalidOperationException(
                        "Permitted provisional recognition requires a positive threshold.");
                }
                if (request.ProvisionalEvidenceScore.Value > request.RequiredEvidenceScore)
                {
                    throw new InvalidOperationException(
                        "Provisional threshold cannot exceed the recognition threshold.");
                }
            }

            ValidateIdentifiers(request.AppliedPolicyIds, nameof(request.AppliedPolicyIds));
            ValidateIdentifiers(request.SkippedProcedureIds, nameof(request.SkippedProcedureIds));
            ValidateIdentifiers(request.CitedHoldingIds, nameof(request.CitedHoldingIds));
            EnsureDisjoint(
                request.AppliedPolicyIds,
                request.SkippedProcedureIds,
                "A procedure cannot be both applied and skipped.");
            ValidateReportCollections(report);
        }

        private static OfficialFinding ValidateAppeal(
            InstitutionalConsequenceReport report,
            InstitutionalAdjudicationRequest request,
            Ruling challengedRuling,
            Appeal filedAppeal)
        {
            if (challengedRuling == null)
                throw new ArgumentNullException(nameof(challengedRuling));
            if (filedAppeal == null)
                throw new ArgumentNullException(nameof(filedAppeal));
            if (!ContainsReference(report.Rulings, challengedRuling))
                throw new InvalidOperationException(
                    "The challenged ruling must be the ruling registered in the report.");
            if (!ContainsReference(report.Appeals, filedAppeal))
                throw new InvalidOperationException(
                    "The filed appeal must be the appeal registered in the report.");
            if (!string.Equals(challengedRuling.CaseId, request.CaseId,
                    StringComparison.Ordinal) ||
                !string.Equals(filedAppeal.CaseId, request.CaseId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Appeal, challenged ruling, and adjudication must concern the same case.");
            }
            if (!string.Equals(filedAppeal.ChallengedRulingId,
                    challengedRuling.RulingId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "The appeal does not reference the supplied challenged ruling.");
            }
            if (challengedRuling.Cycle > filedAppeal.FiledCycle ||
                filedAppeal.FiledCycle > filedAppeal.HearingCycle ||
                filedAppeal.HearingCycle > request.Cycle)
            {
                throw new InvalidOperationException(
                    "Challenged ruling, filing, hearing, and resolution chronology is invalid.");
            }
            if (filedAppeal.Disposition != AppealDisposition.Pending ||
                !string.IsNullOrEmpty(filedAppeal.ResultingRulingId))
            {
                throw new InvalidOperationException("Only a pending appeal can be resolved.");
            }

            OfficialFinding challengedFinding = FindUniqueFinding(
                report,
                challengedRuling.FindingId);
            if (!string.Equals(challengedFinding.CaseId, request.CaseId,
                    StringComparison.Ordinal) ||
                challengedFinding.Cycle != challengedRuling.Cycle)
            {
                throw new InvalidOperationException(
                    "The challenged ruling's finding must belong to the same case and cycle.");
            }
            ValidateAppealGrounds(report, request, filedAppeal);
            return challengedFinding;
        }

        private static void ValidateAppealGrounds(
            InstitutionalConsequenceReport report,
            InstitutionalAdjudicationRequest request,
            Appeal appeal)
        {
            ValidateIdentifiers(
                appeal.GroundsEvidenceArtifactIds,
                nameof(appeal.GroundsEvidenceArtifactIds));
            for (int i = 0; i < appeal.GroundsEvidenceArtifactIds.Count; i++)
            {
                EvidenceArtifact artifact = FindUniqueEvidence(
                    report,
                    appeal.GroundsEvidenceArtifactIds[i]);
                if (!string.Equals(artifact.CaseId, request.CaseId,
                        StringComparison.Ordinal) ||
                    artifact.EnteredCycle > request.MaximumEvidenceCycle)
                {
                    throw new InvalidOperationException(
                        "Appeal grounds must reference evidence in the resolved case envelope.");
                }
            }
        }

        private static void ValidateEvidenceEnvelope(
            InstitutionalAdjudicationRequest request,
            List<EvidenceArtifact> evidence)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < evidence.Count; i++)
            {
                EvidenceArtifact artifact = evidence[i];
                RequireId(artifact.ArtifactId, "Evidence artifact ID");
                if (!ids.Add(artifact.ArtifactId))
                    throw new InvalidOperationException(
                        $"Duplicate evidence artifact '{artifact.ArtifactId}'.");
                if (!string.Equals(artifact.CaseId, request.CaseId,
                        StringComparison.Ordinal) ||
                    artifact.EnteredCycle > request.MaximumEvidenceCycle)
                {
                    throw new InvalidOperationException(
                        "Evidence evaluation escaped the requested case envelope.");
                }
            }
        }

        private static void ValidateCitations(
            InstitutionalConsequenceReport report,
            InstitutionalAdjudicationRequest request)
        {
            if (request.CitedHoldingWeight > 0 && request.CitedHoldingIds.Count == 0)
            {
                throw new InvalidOperationException(
                    "A positive precedent weight requires a cited holding.");
            }

            for (int i = 0; i < request.CitedHoldingIds.Count; i++)
            {
                Holding holding = FindUniqueHolding(report, request.CitedHoldingIds[i]);
                if (holding.EstablishedCycle > request.Cycle)
                    throw new InvalidOperationException("A ruling cannot cite a future holding.");
                if (!string.Equals(holding.IssueId, request.IssueId,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "A cited holding must govern the adjudicated issue.");
                }
                if (holding.Scope == null || string.IsNullOrWhiteSpace(holding.Scope.ScopeId))
                    throw new InvalidOperationException("A cited holding requires a valid scope.");
                if (!holding.Scope.AppliesTo(request.CaseFacts))
                {
                    throw new InvalidOperationException(
                        "A cited holding must match the adjudicated case facts before " +
                        "it can contribute precedent weight.");
                }
            }
        }

        private static List<string> ResolveCitedScopeIds(
            InstitutionalConsequenceReport report,
            List<string> citedHoldingIds)
        {
            var scopeIds = new List<string>(citedHoldingIds.Count);
            for (int i = 0; i < citedHoldingIds.Count; i++)
            {
                string scopeId = FindUniqueHolding(report, citedHoldingIds[i]).Scope.ScopeId;
                if (!scopeIds.Contains(scopeId)) scopeIds.Add(scopeId);
            }
            return scopeIds;
        }

        private static bool IsSubstantivelyRecognised(OfficialFinding finding)
        {
            return finding.Disposition == FindingDisposition.Established ||
                   finding.Disposition == FindingDisposition.ProvisionallyEstablished;
        }

        private static void EnsureRowsAreNew(
            InstitutionalConsequenceReport report,
            OfficialFinding finding,
            Ruling ruling)
        {
            if (ContainsId(report.OfficialFindings, finding.FindingId,
                    value => value.FindingId))
            {
                throw new InvalidOperationException(
                    $"Finding '{finding.FindingId}' has already been recorded.");
            }
            if (ContainsId(report.Rulings, ruling.RulingId, value => value.RulingId))
            {
                throw new InvalidOperationException(
                    $"Ruling '{ruling.RulingId}' has already been recorded.");
            }
            EnsureNoTimelineEntry(
                report,
                InstitutionalTimelineKind.RulingIssued,
                ruling.RulingId);
        }

        private static void EnsureNoTimelineEntry(
            InstitutionalConsequenceReport report,
            InstitutionalTimelineKind kind,
            string causeId)
        {
            for (int i = 0; i < report.Timeline.Count; i++)
            {
                InstitutionalTimelineEntry entry = report.Timeline[i];
                if (entry.Kind == kind &&
                    string.Equals(entry.CauseId, causeId, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Timeline row for '{causeId}' and '{kind}' already exists.");
                }
            }
        }

        private static OfficialFinding FindUniqueFinding(
            InstitutionalConsequenceReport report,
            string findingId)
        {
            OfficialFinding result = null;
            for (int i = 0; i < report.OfficialFindings.Count; i++)
            {
                OfficialFinding candidate = report.OfficialFindings[i];
                if (!string.Equals(candidate.FindingId, findingId,
                        StringComparison.Ordinal)) continue;
                if (result != null)
                    throw new InvalidOperationException(
                        $"Finding reference '{findingId}' is ambiguous.");
                result = candidate;
            }
            return result ?? throw new InvalidOperationException(
                $"Finding reference '{findingId}' does not exist.");
        }

        private static EvidenceArtifact FindUniqueEvidence(
            InstitutionalConsequenceReport report,
            string artifactId)
        {
            EvidenceArtifact result = null;
            for (int i = 0; i < report.EvidenceArtifacts.Count; i++)
            {
                EvidenceArtifact candidate = report.EvidenceArtifacts[i];
                if (!string.Equals(candidate.ArtifactId, artifactId,
                        StringComparison.Ordinal)) continue;
                if (result != null)
                    throw new InvalidOperationException(
                        $"Evidence reference '{artifactId}' is ambiguous.");
                result = candidate;
            }
            return result ?? throw new InvalidOperationException(
                $"Evidence reference '{artifactId}' does not exist.");
        }

        private static Holding FindUniqueHolding(
            InstitutionalConsequenceReport report,
            string holdingId)
        {
            Holding result = null;
            for (int i = 0; i < report.Holdings.Count; i++)
            {
                Holding candidate = report.Holdings[i];
                if (!string.Equals(candidate.HoldingId, holdingId,
                        StringComparison.Ordinal)) continue;
                if (result != null)
                    throw new InvalidOperationException(
                        $"Holding reference '{holdingId}' is ambiguous.");
                result = candidate;
            }
            return result ?? throw new InvalidOperationException(
                $"Holding reference '{holdingId}' does not exist.");
        }

        private static bool ContainsReference<T>(List<T> rows, T expected)
            where T : class
        {
            int matches = 0;
            for (int i = 0; i < rows.Count; i++)
            {
                if (!ReferenceEquals(rows[i], expected)) continue;
                matches++;
            }
            if (matches > 1)
                throw new InvalidOperationException("A report row is registered more than once.");
            return matches == 1;
        }

        private static bool ContainsId<T>(
            List<T> rows,
            string id,
            Func<T, string> selectId)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                if (string.Equals(selectId(rows[i]), id, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private static void ValidateReportCollections(InstitutionalConsequenceReport report)
        {
            if (report.EvidenceArtifacts == null || report.OfficialFindings == null ||
                report.Rulings == null || report.Appeals == null ||
                report.Holdings == null || report.Timeline == null)
            {
                throw new InvalidOperationException(
                    "Adjudication report collections must be initialised.");
            }
        }

        private static void RequireId(string value, string field)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new InvalidOperationException($"{field} is required.");
        }

        private static void ValidateIdentifiers(List<string> values, string field)
        {
            if (values == null)
                throw new InvalidOperationException($"{field} cannot be null.");
            var unique = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < values.Count; i++)
            {
                RequireId(values[i], field);
                if (!unique.Add(values[i]))
                    throw new InvalidOperationException(
                        $"{field} contains duplicate ID '{values[i]}'.");
            }
        }

        private static void EnsureDisjoint(
            List<string> left,
            List<string> right,
            string message)
        {
            var leftIds = new HashSet<string>(left, StringComparer.Ordinal);
            for (int i = 0; i < right.Count; i++)
            {
                if (leftIds.Contains(right[i]))
                    throw new InvalidOperationException(message);
            }
        }

        private static List<string> CopyIdentifiers(List<string> source)
        {
            return new List<string>(source);
        }

        private sealed class PreparedAdjudication
        {
            internal readonly int Score;
            internal readonly int MinimumScore;
            internal readonly int MaximumScore;
            internal readonly List<EvidenceArtifact> Evidence;
            internal readonly List<AdjudicationEvidenceSnapshot> FrozenEvidence;
            internal readonly List<string> CitedScopeIds;

            internal PreparedAdjudication(
                int score,
                int minimumScore,
                int maximumScore,
                List<EvidenceArtifact> evidence,
                List<AdjudicationEvidenceSnapshot> frozenEvidence,
                List<string> citedScopeIds)
            {
                Score = score;
                MinimumScore = minimumScore;
                MaximumScore = maximumScore;
                Evidence = evidence;
                FrozenEvidence = frozenEvidence;
                CitedScopeIds = citedScopeIds;
            }
        }
    }
}
