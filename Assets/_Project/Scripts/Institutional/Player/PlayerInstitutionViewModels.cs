using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Desk42.Institutional.Player
{
    public enum PlayerSlicePhase
    {
        Observe,
        Inspect,
        Rule,
        Advance,
        Review,
    }

    public enum PlayerScopeChoice
    {
        Narrow,
        Broad,
    }

    /// <summary>
    /// The dispositions actually offered by the causal-legibility slice. Keeping this
    /// vocabulary in the Player assembly prevents presentation code from acquiring a
    /// compile-time dependency on the wider institutional domain.
    /// </summary>
    public enum PlayerRulingDisposition
    {
        Recognised,
        Denied,
    }

    public enum PublicTimelineKind
    {
        Observed,
        OfficiallyRecorded,
        Alleged,
        Inferred,
        RulingEffect,
    }

    /// <summary>
    /// Immutable player projection. This is the only simulation state the product UI
    /// may consume; it deliberately contains no authoritative material or assessor data.
    /// </summary>
    public sealed class PlayerInstitutionView
    {
        internal PlayerInstitutionView(
            PlayerSlicePhase phase,
            long currentCycle,
            string currentCaseId,
            IEnumerable<PublicAgentRecord> agents,
            IEnumerable<PublicCaseRecord> cases,
            IEnumerable<PublicEvidenceRecord> evidence,
            IEnumerable<PublicRulingRecord> rulings,
            IEnumerable<PublicTimelineEntry> timeline,
            IEnumerable<ScopeMatchPreview> scopePreviews,
            IEnumerable<KnownDecisionPressure> knownDecisionPressures,
            string unknownsSummary)
        {
            Phase = phase;
            CurrentCycle = currentCycle;
            CurrentCaseId = currentCaseId ?? string.Empty;
            Agents = PlayerViewCollections.Freeze(agents);
            Cases = PlayerViewCollections.Freeze(cases);
            Evidence = PlayerViewCollections.Freeze(evidence);
            Rulings = PlayerViewCollections.Freeze(rulings);
            Timeline = PlayerViewCollections.Freeze(timeline);
            ScopePreviews = PlayerViewCollections.Freeze(scopePreviews);
            KnownDecisionPressures = PlayerViewCollections.Freeze(
                knownDecisionPressures);
            UnknownsSummary = unknownsSummary ?? string.Empty;
        }

        public PlayerSlicePhase Phase { get; }
        public long CurrentCycle { get; }
        public string CurrentCaseId { get; }
        public IReadOnlyList<PublicAgentRecord> Agents { get; }
        public IReadOnlyList<PublicCaseRecord> Cases { get; }
        public IReadOnlyList<PublicEvidenceRecord> Evidence { get; }
        public IReadOnlyList<PublicRulingRecord> Rulings { get; }
        public IReadOnlyList<PublicTimelineEntry> Timeline { get; }
        public IReadOnlyList<ScopeMatchPreview> ScopePreviews { get; }
        public IReadOnlyList<KnownDecisionPressure> KnownDecisionPressures { get; }
        public string UnknownsSummary { get; }
    }

    public sealed class PublicAgentRecord
    {
        internal PublicAgentRecord(
            string agentId,
            string displayName,
            string officialIdentity,
            string speciesLabel,
            string recognisedEmployer,
            string recognisedHousehold,
            IEnumerable<string> knownResourcesOrEntitlements,
            IEnumerable<string> observedActions,
            IEnumerable<string> submittedStatements,
            IEnumerable<string> recognisedStatuses,
            IEnumerable<string> caseIds)
        {
            AgentId = agentId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            OfficialIdentity = officialIdentity ?? string.Empty;
            SpeciesLabel = speciesLabel ?? string.Empty;
            RecognisedEmployer = recognisedEmployer ?? string.Empty;
            RecognisedHousehold = recognisedHousehold ?? string.Empty;
            KnownResourcesOrEntitlements = PlayerViewCollections.Freeze(
                knownResourcesOrEntitlements);
            ObservedActions = PlayerViewCollections.Freeze(observedActions);
            SubmittedStatements = PlayerViewCollections.Freeze(submittedStatements);
            RecognisedStatuses = PlayerViewCollections.Freeze(recognisedStatuses);
            CaseIds = PlayerViewCollections.Freeze(caseIds);
        }

        public string AgentId { get; }
        public string DisplayName { get; }
        public string OfficialIdentity { get; }
        public string SpeciesLabel { get; }
        public string RecognisedEmployer { get; }
        public string RecognisedHousehold { get; }
        public IReadOnlyList<string> KnownResourcesOrEntitlements { get; }
        public IReadOnlyList<string> ObservedActions { get; }
        public IReadOnlyList<string> SubmittedStatements { get; }
        public IReadOnlyList<string> RecognisedStatuses { get; }
        public IReadOnlyList<string> CaseIds { get; }
    }

    public sealed class PublicCaseRecord
    {
        internal PublicCaseRecord(
            string caseId,
            string issue,
            long openedCycle,
            int caseRevision,
            string evidenceEnvelopeHash,
            IEnumerable<string> parties,
            IEnumerable<string> allegations,
            IEnumerable<string> recognisedFacts,
            IEnumerable<string> contestedPropositions,
            IEnumerable<string> evidenceIds,
            IEnumerable<string> missingEvidence,
            IEnumerable<string> docketBasis,
            IEnumerable<string> possibleRemedies,
            int evidenceSupportMinimum,
            int evidenceSupportMaximum,
            long rulingDeadline,
            bool rulingCommitted,
            string parentCaseId,
            string originatingRulingId)
        {
            CaseId = caseId ?? string.Empty;
            Issue = issue ?? string.Empty;
            OpenedCycle = openedCycle;
            CaseRevision = caseRevision;
            EvidenceEnvelopeHash = evidenceEnvelopeHash ?? string.Empty;
            Parties = PlayerViewCollections.Freeze(parties);
            Allegations = PlayerViewCollections.Freeze(allegations);
            RecognisedFacts = PlayerViewCollections.Freeze(recognisedFacts);
            ContestedPropositions = PlayerViewCollections.Freeze(contestedPropositions);
            EvidenceIds = PlayerViewCollections.Freeze(evidenceIds);
            MissingEvidence = PlayerViewCollections.Freeze(missingEvidence);
            DocketBasis = PlayerViewCollections.Freeze(docketBasis);
            PossibleRemedies = PlayerViewCollections.Freeze(possibleRemedies);
            EvidenceSupportMinimum = evidenceSupportMinimum;
            EvidenceSupportMaximum = evidenceSupportMaximum;
            RulingDeadline = rulingDeadline;
            RulingCommitted = rulingCommitted;
            ParentCaseId = parentCaseId ?? string.Empty;
            OriginatingRulingId = originatingRulingId ?? string.Empty;
        }

        public string CaseId { get; }
        public string Issue { get; }
        public long OpenedCycle { get; }
        public int CaseRevision { get; }
        public string EvidenceEnvelopeHash { get; }
        public IReadOnlyList<string> Parties { get; }
        public IReadOnlyList<string> Allegations { get; }
        public IReadOnlyList<string> RecognisedFacts { get; }
        public IReadOnlyList<string> ContestedPropositions { get; }
        public IReadOnlyList<string> EvidenceIds { get; }
        public IReadOnlyList<string> MissingEvidence { get; }
        public IReadOnlyList<string> DocketBasis { get; }
        public IReadOnlyList<string> PossibleRemedies { get; }
        public int EvidenceSupportMinimum { get; }
        public int EvidenceSupportMaximum { get; }
        public long RulingDeadline { get; }
        public bool RulingCommitted { get; }
        public string ParentCaseId { get; }
        public string OriginatingRulingId { get; }
    }

    public sealed class PublicEvidenceRecord
    {
        internal PublicEvidenceRecord(
            string evidenceId,
            string caseId,
            string source,
            string proposition,
            long enteredCycle,
            string chainOfCustody,
            int reliabilityScore,
            string reliabilityLabel,
            string officialStatus,
            IEnumerable<string> knownContradictions,
            IEnumerable<string> limitingConditions,
            bool citable)
        {
            EvidenceId = evidenceId ?? string.Empty;
            CaseId = caseId ?? string.Empty;
            Source = source ?? string.Empty;
            Proposition = proposition ?? string.Empty;
            EnteredCycle = enteredCycle;
            ChainOfCustody = chainOfCustody ?? string.Empty;
            ReliabilityScore = reliabilityScore;
            ReliabilityLabel = reliabilityLabel ?? string.Empty;
            OfficialStatus = officialStatus ?? string.Empty;
            KnownContradictions = PlayerViewCollections.Freeze(knownContradictions);
            LimitingConditions = PlayerViewCollections.Freeze(limitingConditions);
            Citable = citable;
        }

        public string EvidenceId { get; }
        public string CaseId { get; }
        public string Source { get; }
        public string Proposition { get; }
        public long EnteredCycle { get; }
        public string ChainOfCustody { get; }
        public int ReliabilityScore { get; }
        public string ReliabilityLabel { get; }
        public string OfficialStatus { get; }
        public IReadOnlyList<string> KnownContradictions { get; }
        public IReadOnlyList<string> LimitingConditions { get; }
        public bool Citable { get; }
    }

    public sealed class PublicRulingRecord
    {
        internal PublicRulingRecord(
            string rulingId,
            string caseId,
            long committedCycle,
            string disposition,
            string holding,
            string scope,
            string temporalReach,
            IEnumerable<string> recognisedFacts,
            IEnumerable<string> citedEvidence,
            IEnumerable<string> remedies,
            IEnumerable<string> directInstitutionalChanges)
        {
            RulingId = rulingId ?? string.Empty;
            CaseId = caseId ?? string.Empty;
            CommittedCycle = committedCycle;
            Disposition = disposition ?? string.Empty;
            Holding = holding ?? string.Empty;
            Scope = scope ?? string.Empty;
            TemporalReach = temporalReach ?? string.Empty;
            RecognisedFacts = PlayerViewCollections.Freeze(recognisedFacts);
            CitedEvidence = PlayerViewCollections.Freeze(citedEvidence);
            Remedies = PlayerViewCollections.Freeze(remedies);
            DirectInstitutionalChanges = PlayerViewCollections.Freeze(
                directInstitutionalChanges);
        }

        public string RulingId { get; }
        public string CaseId { get; }
        public long CommittedCycle { get; }
        public string Disposition { get; }
        public string Holding { get; }
        public string Scope { get; }
        public string TemporalReach { get; }
        public IReadOnlyList<string> RecognisedFacts { get; }
        public IReadOnlyList<string> CitedEvidence { get; }
        public IReadOnlyList<string> Remedies { get; }
        public IReadOnlyList<string> DirectInstitutionalChanges { get; }
    }

    public sealed class PublicTimelineEntry
    {
        internal PublicTimelineEntry(
            string entryId,
            long cycle,
            PublicTimelineKind kind,
            string headline,
            string detail,
            string immediateCauseId,
            string originatingRulingId,
            string appliedHoldingId,
            string scopeMatchId,
            string sourceObservableEventId)
        {
            EntryId = entryId ?? string.Empty;
            Cycle = cycle;
            Kind = kind;
            Headline = headline ?? string.Empty;
            Detail = detail ?? string.Empty;
            ImmediateCauseId = immediateCauseId ?? string.Empty;
            OriginatingRulingId = originatingRulingId ?? string.Empty;
            AppliedHoldingId = appliedHoldingId ?? string.Empty;
            ScopeMatchId = scopeMatchId ?? string.Empty;
            SourceObservableEventId = sourceObservableEventId ?? string.Empty;
        }

        public string EntryId { get; }
        public long Cycle { get; }
        public PublicTimelineKind Kind { get; }
        public string Headline { get; }
        public string Detail { get; }
        public string ImmediateCauseId { get; }
        public string OriginatingRulingId { get; }
        public string AppliedHoldingId { get; }
        public string ScopeMatchId { get; }
        public string SourceObservableEventId { get; }
    }

    public sealed class ScopeMatchPreview
    {
        internal ScopeMatchPreview(
            PlayerScopeChoice choice,
            string description,
            int matchingCaseCount,
            int potentiallyCoveredAgentCount,
            int matchingOrganisationCount,
            IEnumerable<string> currentMatches,
            string futureMatchNote)
        {
            Choice = choice;
            Description = description ?? string.Empty;
            MatchingCaseCount = matchingCaseCount;
            PotentiallyCoveredAgentCount = potentiallyCoveredAgentCount;
            MatchingOrganisationCount = matchingOrganisationCount;
            CurrentMatches = PlayerViewCollections.Freeze(currentMatches);
            FutureMatchNote = futureMatchNote ?? string.Empty;
        }

        public PlayerScopeChoice Choice { get; }
        public string Description { get; }
        public int MatchingCaseCount { get; }
        public int PotentiallyCoveredAgentCount { get; }
        public int MatchingOrganisationCount { get; }
        public IReadOnlyList<string> CurrentMatches { get; }
        public string FutureMatchNote { get; }
    }

    public sealed class KnownDecisionPressure
    {
        internal KnownDecisionPressure(
            string agentId,
            string statement,
            string basisRecordId)
        {
            AgentId = agentId ?? string.Empty;
            Statement = statement ?? string.Empty;
            BasisRecordId = basisRecordId ?? string.Empty;
        }

        public string AgentId { get; }
        public string Statement { get; }
        public string BasisRecordId { get; }
    }

    public sealed class PlayerRulingDraft
    {
        internal PlayerRulingDraft(
            string commandId,
            string caseId,
            int expectedCaseRevision,
            string evidenceEnvelopeHash,
            IEnumerable<string> recognisedFactIds,
            IEnumerable<string> citedEvidenceIds,
            PlayerRulingDisposition disposition,
            PlayerScopeChoice scopeChoice)
        {
            CommandId = commandId ?? string.Empty;
            CaseId = caseId ?? string.Empty;
            ExpectedCaseRevision = expectedCaseRevision;
            EvidenceEnvelopeHash = evidenceEnvelopeHash ?? string.Empty;
            RecognisedFactIds = PlayerViewCollections.Freeze(recognisedFactIds);
            CitedEvidenceIds = PlayerViewCollections.Freeze(citedEvidenceIds);
            Disposition = disposition;
            ScopeChoice = scopeChoice;
        }

        public string CommandId { get; }
        public string CaseId { get; }
        public int ExpectedCaseRevision { get; }
        public string EvidenceEnvelopeHash { get; }
        public IReadOnlyList<string> RecognisedFactIds { get; }
        public IReadOnlyList<string> CitedEvidenceIds { get; }
        public PlayerRulingDisposition Disposition { get; }
        public PlayerScopeChoice ScopeChoice { get; }
    }

    public sealed class PlayerRulingPreview
    {
        internal PlayerRulingPreview(
            string finding,
            string disposition,
            string holding,
            ScopeMatchPreview scope,
            string temporalReach,
            string remedy,
            IEnumerable<string> directInstitutionalChanges)
        {
            Finding = finding ?? string.Empty;
            Disposition = disposition ?? string.Empty;
            Holding = holding ?? string.Empty;
            Scope = scope ?? throw new ArgumentNullException(nameof(scope));
            TemporalReach = temporalReach ?? string.Empty;
            Remedy = remedy ?? string.Empty;
            DirectInstitutionalChanges = PlayerViewCollections.Freeze(
                directInstitutionalChanges);
        }

        public string Finding { get; }
        public string Disposition { get; }
        public string Holding { get; }
        public ScopeMatchPreview Scope { get; }
        public string TemporalReach { get; }
        public string Remedy { get; }
        public IReadOnlyList<string> DirectInstitutionalChanges { get; }
    }

    internal static class PlayerViewCollections
    {
        internal static IReadOnlyList<T> Freeze<T>(IEnumerable<T> source)
        {
            var copy = source == null ? new List<T>() : new List<T>(source);
            return new ReadOnlyCollection<T>(copy);
        }
    }
}
