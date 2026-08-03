using System;
using System.Collections.Generic;

namespace Desk42.Institutional
{
    internal static class EndogenousDocketStateDeepCopy
    {
        internal static EndogenousDocketState Copy(EndogenousDocketState source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var copy = new EndogenousDocketState
            {
                SchemaVersion = source.SchemaVersion,
                RulesetVersion = source.RulesetVersion,
                DirectorEnabled = source.DirectorEnabled,
            };
            for (int i = 0; i < source.IncidentCandidates.Count; i++)
            {
                IncidentCandidate value = source.IncidentCandidates[i];
                copy.IncidentCandidates.Add(new IncidentCandidate
                {
                    CandidateId = value.CandidateId,
                    CauseEventIds = Strings(value.CauseEventIds),
                    AffectedAgentIds = Strings(value.AffectedAgentIds),
                    ConflictKindId = value.ConflictKindId,
                    DetectedTick = value.DetectedTick,
                    UnresolvedMaterialHarm = value.UnresolvedMaterialHarm,
                    SubjectResourceId = value.SubjectResourceId,
                    DedupeKey = value.DedupeKey,
                    ParentCaseId = value.ParentCaseId,
                    OriginatingRulingId = value.OriginatingRulingId,
                    CausalAgentActionId = value.CausalAgentActionId,
                });
            }
            for (int i = 0; i < source.Observations.Count; i++)
            {
                DocketObservation value = source.Observations[i];
                copy.Observations.Add(new DocketObservation
                {
                    ObservationId = value.ObservationId,
                    RecordedTick = value.RecordedTick,
                    ObservationKindId = value.ObservationKindId,
                    IssueId = value.IssueId,
                    PropositionId = value.PropositionId,
                    SourceAgentId = value.SourceAgentId,
                    AllegedSubjectAgentId = value.AllegedSubjectAgentId,
                    OfficialResourceId = value.OfficialResourceId,
                    SourceRecordId = value.SourceRecordId,
                    Reliability = value.Reliability,
                    ObservedMaterialHarm = value.ObservedMaterialHarm,
                    OfficiallySubmitted = value.OfficiallySubmitted,
                    AuthorityIncidentCandidateId = value.AuthorityIncidentCandidateId,
                    ParentCaseId = value.ParentCaseId,
                    OriginatingRulingId = value.OriginatingRulingId,
                    CausalAgentActionId = value.CausalAgentActionId,
                });
            }
            for (int i = 0; i < source.DocketCandidates.Count; i++)
            {
                DocketCandidate value = source.DocketCandidates[i];
                copy.DocketCandidates.Add(new DocketCandidate
                {
                    DocketCandidateId = value.DocketCandidateId,
                    EligibilityRuleId = value.EligibilityRuleId,
                    IssueId = value.IssueId,
                    EligibleTick = value.EligibleTick,
                    UnresolvedMaterialHarm = value.UnresolvedMaterialHarm,
                    ObservableEvidenceIds = Strings(value.ObservableEvidenceIds),
                    AllegingAgentIds = Strings(value.AllegingAgentIds),
                    PotentialPartyIds = Strings(value.PotentialPartyIds),
                    Admitted = value.Admitted,
                    AdmittedCaseId = value.AdmittedCaseId,
                    AuthorityIncidentCandidateId = value.AuthorityIncidentCandidateId,
                    ParentCaseId = value.ParentCaseId,
                    OriginatingRulingId = value.OriginatingRulingId,
                    CausalAgentActionId = value.CausalAgentActionId,
                });
            }
            for (int i = 0; i < source.OpenCases.Count; i++)
            {
                EndogenousInstitutionalCase value = source.OpenCases[i];
                copy.OpenCases.Add(new EndogenousInstitutionalCase
                {
                    CaseId = value.CaseId,
                    DocketCandidateId = value.DocketCandidateId,
                    IssueId = value.IssueId,
                    OpenedTick = value.OpenedTick,
                    CaseVersion = value.CaseVersion,
                    EvidenceEnvelopeHash = value.EvidenceEnvelopeHash,
                    PartyIds = Strings(value.PartyIds),
                    ObservationIds = Strings(value.ObservationIds),
                    AvailableFactIds = Strings(value.AvailableFactIds),
                    ParentCaseId = value.ParentCaseId,
                    OriginatingRulingId = value.OriginatingRulingId,
                    CausalAgentActionId = value.CausalAgentActionId,
                });
            }
            for (int i = 0; i < source.Rulings.Count; i++)
            {
                CommittedPlayerRuling value = source.Rulings[i];
                copy.Rulings.Add(new CommittedPlayerRuling
                {
                    RulingId = value.RulingId,
                    PlayerCommandId = value.PlayerCommandId,
                    CaseId = value.CaseId,
                    CaseVersion = value.CaseVersion,
                    CommittedTick = value.CommittedTick,
                    EvidenceEnvelopeHash = value.EvidenceEnvelopeHash,
                    RecognisedFactIds = Strings(value.RecognisedFactIds),
                    CitedEvidenceArtifactIds = Strings(
                        value.CitedEvidenceArtifactIds),
                    Disposition = value.Disposition,
                    HoldingRuleId = value.HoldingRuleId,
                    Scope = ScopeExpressionEvaluator.Copy(value.Scope),
                    TemporalReach = value.TemporalReach,
                    RemedyDefinitionIds = Strings(value.RemedyDefinitionIds),
                    AppliedProcedureIds = Strings(value.AppliedProcedureIds),
                    RulesetVersion = value.RulesetVersion,
                });
            }
            for (int i = 0; i < source.RemedyApplicationTraces.Count; i++)
            {
                EndogenousRemedyApplicationTrace value =
                    source.RemedyApplicationTraces[i];
                copy.RemedyApplicationTraces.Add(new EndogenousRemedyApplicationTrace
                {
                    TraceId = value.TraceId,
                    RulingId = value.RulingId,
                    CaseId = value.CaseId,
                    RemedyDefinitionId = value.RemedyDefinitionId,
                    AppliedTick = value.AppliedTick,
                    ResourceId = value.ResourceId,
                    DestinationRuleId = value.DestinationRuleId,
                    PreviousPhysicalHolderId = value.PreviousPhysicalHolderId,
                    NewPhysicalHolderId = value.NewPhysicalHolderId,
                    PreviousLocationContextId = value.PreviousLocationContextId,
                    NewLocationContextId = value.NewLocationContextId,
                    MaterialEventId = value.MaterialEventId,
                    MaterialStateChanged = value.MaterialStateChanged,
                });
            }
            for (int i = 0; i < source.ScopeApplicationTraces.Count; i++)
            {
                EndogenousScopeApplicationTrace value = source.ScopeApplicationTraces[i];
                copy.ScopeApplicationTraces.Add(new EndogenousScopeApplicationTrace
                {
                    TraceId = value.TraceId,
                    RulingId = value.RulingId,
                    HoldingRuleId = value.HoldingRuleId,
                    ActorId = value.ActorId,
                    OpportunityId = value.OpportunityId,
                    IssueId = value.IssueId,
                    JurisdictionId = value.JurisdictionId,
                    AppliedTick = value.AppliedTick,
                    ScopeMatched = value.ScopeMatched,
                    AffectedOfficialStatusId = value.AffectedOfficialStatusId,
                    StatusBefore = value.StatusBefore,
                    StatusAfter = value.StatusAfter,
                });
            }
            for (int i = 0; i < source.AccessRemedyApplicationTraces.Count; i++)
            {
                EndogenousAccessRemedyApplicationTrace value =
                    source.AccessRemedyApplicationTraces[i];
                copy.AccessRemedyApplicationTraces.Add(
                    new EndogenousAccessRemedyApplicationTrace
                    {
                        TraceId = value.TraceId,
                        RulingId = value.RulingId,
                        CaseId = value.CaseId,
                        AppliedTick = value.AppliedTick,
                        AccessGrantId = value.AccessGrantId,
                        BeneficiaryAgentId = value.BeneficiaryAgentId,
                        StateBefore = value.StateBefore,
                        StateAfter = value.StateAfter,
                        MaterialEventId = value.MaterialEventId,
                        MaterialStateChanged = value.MaterialStateChanged,
                    });
            }
            for (int i = 0;
                 i < source.CollectiveRemedyApplicationTraces.Count;
                 i++)
            {
                EndogenousCollectiveRemedyApplicationTrace value =
                    source.CollectiveRemedyApplicationTraces[i];
                copy.CollectiveRemedyApplicationTraces.Add(
                    new EndogenousCollectiveRemedyApplicationTrace
                    {
                        TraceId = value.TraceId,
                        RulingId = value.RulingId,
                        CaseId = value.CaseId,
                        AppliedTick = value.AppliedTick,
                        CollectiveCommitmentId = value.CollectiveCommitmentId,
                        RecognisedStatusId = value.RecognisedStatusId,
                        MemberAgentIds = Strings(value.MemberAgentIds),
                        ChangedAgentIds = Strings(value.ChangedAgentIds),
                    });
            }
            for (int i = 0; i < source.Appeals.Count; i++)
            {
                EndogenousAppealRecord value = source.Appeals[i];
                copy.Appeals.Add(new EndogenousAppealRecord
                {
                    AppealId = value.AppealId,
                    CaseId = value.CaseId,
                    ChallengedRulingId = value.ChallengedRulingId,
                    FiledTick = value.FiledTick,
                    ProcedureId = value.ProcedureId,
                    GroundsEvidenceIds = Strings(value.GroundsEvidenceIds),
                    Resolved = value.Resolved,
                    ResolvedTick = value.ResolvedTick,
                    ResultingRulingId = value.ResultingRulingId,
                    ResultingHoldingId = value.ResultingHoldingId,
                });
            }
            for (int i = 0; i < source.Holdings.Count; i++)
            {
                EndogenousHoldingRecord value = source.Holdings[i];
                copy.Holdings.Add(new EndogenousHoldingRecord
                {
                    HoldingId = value.HoldingId,
                    SourceAppealId = value.SourceAppealId,
                    SourceRulingId = value.SourceRulingId,
                    RuleId = value.RuleId,
                    IssueId = value.IssueId,
                    EstablishedTick = value.EstablishedTick,
                    Scope = ScopeExpressionEvaluator.Copy(value.Scope),
                    SupportingEvidenceIds = Strings(value.SupportingEvidenceIds),
                    AppliedCaseIds = Strings(value.AppliedCaseIds),
                });
            }
            return copy;
        }

        private static List<string> Strings(IReadOnlyList<string> source)
        {
            var result = new List<string>(source.Count);
            for (int i = 0; i < source.Count; i++) result.Add(source[i]);
            return result;
        }
    }
}
