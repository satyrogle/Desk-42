using System;
using System.Collections.Generic;

namespace Desk42.Institutional.Player
{
    /// <summary>
    /// Projects authority state into an immutable, institutionally knowable view.
    /// Every projection rule is deliberately explicit: no general-purpose copier may
    /// accidentally carry beliefs, needs, utility traces or lived material truth.
    /// </summary>
    internal static class PlayerInstitutionProjector
    {
        internal static PlayerInstitutionView Project(
            SocietyState society,
            InstitutionalMaterialWorld world,
            EndogenousDocketState docket)
        {
            if (society == null) throw new ArgumentNullException(nameof(society));
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (docket == null) throw new ArgumentNullException(nameof(docket));
            SocietyStateValidator.Validate(society);
            InstitutionalMaterialWorldValidator.Validate(world, society);
            EndogenousDocketValidator.Validate(docket, society);

            var cases = ProjectCases(society, world, docket);
            string currentCaseId = CurrentCaseId(docket);
            return new PlayerInstitutionView(
                docket.Rulings.Count == 0
                    ? PlayerSlicePhase.Rule
                    : PlayerSlicePhase.Review,
                society.CurrentTick,
                currentCaseId,
                ProjectAgents(society, world, docket),
                cases,
                ProjectEvidence(society, world, docket),
                ProjectRulings(docket),
                ProjectTimeline(society, world, docket),
                ProjectScopePreviews(society, docket, currentCaseId),
                ProjectKnownDecisionPressures(society, docket),
                "Private beliefs, unobserved possession, intention and authoritative " +
                "lived truth remain unknown to the institution.");
        }

        private static List<PublicAgentRecord> ProjectAgents(
            SocietyState society,
            InstitutionalMaterialWorld world,
            EndogenousDocketState docket)
        {
            var agents = new List<AgentState>(society.Agents);
            agents.Sort((left, right) => left.SimulationOrdinal.CompareTo(
                right.SimulationOrdinal));
            var result = new List<PublicAgentRecord>(agents.Count);
            for (int i = 0; i < agents.Count; i++)
            {
                AgentState agent = agents[i];
                var resources = new List<string>();
                for (int ownershipIndex = 0;
                     ownershipIndex < world.OfficialOwnerships.Count;
                     ownershipIndex++)
                {
                    OfficialOwnershipState ownership =
                        world.OfficialOwnerships[ownershipIndex];
                    if (string.Equals(
                            ownership.RegisteredOwnerId,
                            agent.StableId,
                            StringComparison.Ordinal))
                    {
                        resources.Add(
                            $"Registered owner: {ownership.ResourceId}" +
                            (ownership.Disputed ? " (disputed)" : string.Empty));
                    }
                }

                var actions = new List<string>();
                var statements = new List<string>();
                for (int observationIndex = 0;
                     observationIndex < docket.Observations.Count;
                     observationIndex++)
                {
                    DocketObservation observation = docket.Observations[observationIndex];
                    if (string.Equals(
                            observation.AllegedSubjectAgentId,
                            agent.StableId,
                            StringComparison.Ordinal))
                    {
                        AddUnique(actions, Humanise(observation.PropositionId));
                    }
                    if (string.Equals(
                            observation.SourceAgentId,
                            agent.StableId,
                            StringComparison.Ordinal))
                    {
                        AddUnique(statements, Humanise(observation.PropositionId));
                    }
                }

                var statuses = new List<string>();
                for (int statusIndex = 0;
                     statusIndex < agent.Standing.OfficialStatuses.Count;
                     statusIndex++)
                {
                    OfficialStatusState status = agent.Standing.OfficialStatuses[statusIndex];
                    if (status.Recognised) statuses.Add(Humanise(status.StatusId));
                }
                statuses.Sort(StringComparer.Ordinal);

                var caseIds = new List<string>();
                for (int caseIndex = 0; caseIndex < docket.OpenCases.Count; caseIndex++)
                {
                    EndogenousInstitutionalCase opened = docket.OpenCases[caseIndex];
                    if (Contains(opened.PartyIds, agent.StableId))
                        caseIds.Add(opened.CaseId);
                }
                caseIds.Sort(StringComparer.Ordinal);

                result.Add(new PublicAgentRecord(
                    agent.StableId,
                    agent.DisplayName,
                    $"ID-{agent.SimulationOrdinal + 1:00} / {Humanise(agent.SpeciesId)}",
                    Humanise(agent.SpeciesId),
                    agent.Standing.IsRecognised("status.employment-recognised")
                        ? "Employment recognised / employer not adjudicated"
                        : "Employment not recognised",
                    "No official household record",
                    resources,
                    actions,
                    statements,
                    statuses,
                    caseIds));
            }
            return result;
        }

        private static List<PublicCaseRecord> ProjectCases(
            SocietyState society,
            InstitutionalMaterialWorld world,
            EndogenousDocketState docket)
        {
            var openedCases = new List<EndogenousInstitutionalCase>(docket.OpenCases);
            openedCases.Sort((left, right) =>
            {
                int tick = left.OpenedTick.CompareTo(right.OpenedTick);
                return tick != 0 ? tick : string.CompareOrdinal(left.CaseId, right.CaseId);
            });
            var result = new List<PublicCaseRecord>(openedCases.Count);
            for (int i = 0; i < openedCases.Count; i++)
            {
                EndogenousInstitutionalCase opened = openedCases[i];
                List<DocketObservation> observations = ObservationsFor(opened, docket);
                var allegations = new List<string>();
                var contested = new List<string>();
                var basis = new List<string>();
                int reliabilityTotal = 0;
                for (int observationIndex = 0;
                     observationIndex < observations.Count;
                     observationIndex++)
                {
                    DocketObservation observation = observations[observationIndex];
                    allegations.Add(Humanise(observation.PropositionId));
                    contested.Add(
                        $"Whether {Humanise(observation.PropositionId).ToLowerInvariant()} " +
                        "was authorised");
                    basis.Add(
                        $"{Humanise(observation.ObservationKindId)} / " +
                        $"{Humanise(observation.SourceRecordId)}");
                    reliabilityTotal += observation.Reliability;

                    OfficialOwnershipState ownership =
                        world.GetOfficialOwnership(observation.OfficialResourceId);
                    if (ownership != null)
                    {
                        AddUnique(
                            basis,
                            $"Registered ownership: {Humanise(ownership.RegisteredOwnerId)}");
                    }
                }

                var recognisedFacts = new List<string>();
                CommittedPlayerRuling ruling = RulingForCase(docket, opened.CaseId);
                if (ruling != null)
                    for (int factIndex = 0;
                         factIndex < ruling.RecognisedFactIds.Count;
                         factIndex++)
                        recognisedFacts.Add(Humanise(ruling.RecognisedFactIds[factIndex]));

                var parties = new List<string>();
                for (int partyIndex = 0; partyIndex < opened.PartyIds.Count; partyIndex++)
                    parties.Add(DisplayName(society, opened.PartyIds[partyIndex]));

                var missing = MissingEvidence(opened.IssueId, observations);
                int supportMaximum = observations.Count == 0
                    ? 0
                    : reliabilityTotal / observations.Count;
                int supportMinimum = Math.Max(0, supportMaximum - missing.Count * 12);
                result.Add(new PublicCaseRecord(
                    opened.CaseId,
                    Humanise(opened.IssueId),
                    opened.OpenedTick,
                    opened.CaseVersion,
                    opened.EvidenceEnvelopeHash,
                    parties,
                    allegations,
                    recognisedFacts,
                    contested,
                    opened.ObservationIds,
                    missing,
                    basis,
                    RemediesFor(opened.IssueId),
                    supportMinimum,
                    supportMaximum,
                    opened.OpenedTick + 5,
                    ruling != null,
                    opened.ParentCaseId,
                    opened.OriginatingRulingId));
            }
            return result;
        }

        private static List<PublicEvidenceRecord> ProjectEvidence(
            SocietyState society,
            InstitutionalMaterialWorld world,
            EndogenousDocketState docket)
        {
            var result = new List<PublicEvidenceRecord>();
            var ownershipIds = new HashSet<string>(StringComparer.Ordinal);
            for (int caseIndex = 0; caseIndex < docket.OpenCases.Count; caseIndex++)
            {
                EndogenousInstitutionalCase opened = docket.OpenCases[caseIndex];
                List<DocketObservation> observations = ObservationsFor(opened, docket);
                for (int observationIndex = 0;
                     observationIndex < observations.Count;
                     observationIndex++)
                {
                    DocketObservation observation = observations[observationIndex];
                    var contradictions = new List<string>();
                    OfficialOwnershipState ownership =
                        world.GetOfficialOwnership(observation.OfficialResourceId);
                    if (ownership != null && !string.IsNullOrWhiteSpace(
                            observation.AllegedSubjectAgentId) &&
                        !string.Equals(
                            ownership.RegisteredOwnerId,
                            observation.AllegedSubjectAgentId,
                            StringComparison.Ordinal))
                    {
                        contradictions.Add(
                            "Observed possession and registered ownership identify " +
                            "different holders.");
                    }
                    result.Add(new PublicEvidenceRecord(
                        observation.ObservationId,
                        opened.CaseId,
                        string.IsNullOrWhiteSpace(observation.SourceAgentId)
                            ? Humanise(observation.SourceRecordId)
                            : DisplayName(society, observation.SourceAgentId),
                        Humanise(observation.PropositionId),
                        observation.RecordedTick,
                        $"{Humanise(observation.ObservationKindId)} -> " +
                        $"{Humanise(observation.SourceRecordId)} -> docket",
                        observation.Reliability,
                        ReliabilityLabel(observation.Reliability),
                        observation.OfficiallySubmitted
                            ? "ADMITTED TO OFFICIAL RECORD"
                            : "ALLEGATION ONLY",
                        contradictions,
                        MissingEvidence(opened.IssueId, observations),
                        citable: true));

                    if (ownership != null && ownershipIds.Add(ownership.OwnershipRecordId))
                    {
                        result.Add(new PublicEvidenceRecord(
                            ownership.OwnershipRecordId,
                            opened.CaseId,
                            Humanise(ownership.OwnershipSourceId),
                            $"{Humanise(ownership.RegisteredOwnerId)} is the registered " +
                            $"owner of {Humanise(ownership.ResourceId)}",
                            ownership.RecognitionTick,
                            $"{Humanise(ownership.OwnershipSourceId)} -> ownership register",
                            100,
                            "REGISTERED RECORD",
                            ownership.Disputed ? "DISPUTED REGISTRATION" : "OFFICIAL CONTEXT",
                            new[] { "Registration does not establish current physical possession." },
                            new[] { "Transfer consent is not recorded." },
                            citable: false));
                    }
                }
            }
            result.Sort((left, right) =>
            {
                int tick = left.EnteredCycle.CompareTo(right.EnteredCycle);
                return tick != 0
                    ? tick
                    : string.CompareOrdinal(left.EvidenceId, right.EvidenceId);
            });
            return result;
        }

        private static List<PublicRulingRecord> ProjectRulings(
            EndogenousDocketState docket)
        {
            var result = new List<PublicRulingRecord>(docket.Rulings.Count);
            for (int i = 0; i < docket.Rulings.Count; i++)
                result.Add(ProjectRuling(docket.Rulings[i], docket));
            return result;
        }

        internal static PublicRulingRecord ProjectRuling(
            CommittedPlayerRuling ruling,
            EndogenousDocketState docket)
        {
            if (ruling == null) throw new ArgumentNullException(nameof(ruling));
            if (docket == null) throw new ArgumentNullException(nameof(docket));
            return new PublicRulingRecord(
                ruling.RulingId,
                ruling.CaseId,
                ruling.CommittedTick,
                Humanise(ruling.Disposition.ToString()),
                IsDenial(ruling.Disposition)
                    ? "Proposed but not established: " +
                      Humanise(ruling.HoldingRuleId)
                    : Humanise(ruling.HoldingRuleId),
                IsDenial(ruling.Disposition)
                    ? "Not applied; proposed " + ScopeLabel(ruling.Scope)
                    : ScopeLabel(ruling.Scope),
                Humanise(ruling.TemporalReach.ToString()),
                HumaniseAll(ruling.RecognisedFactIds),
                ruling.CitedEvidenceArtifactIds,
                HumaniseAll(ruling.RemedyDefinitionIds),
                DirectChanges(ruling, docket));
        }

        internal static int MissingEvidenceCountForCase(
            EndogenousInstitutionalCase opened,
            EndogenousDocketState docket)
        {
            if (opened == null) throw new ArgumentNullException(nameof(opened));
            if (docket == null) throw new ArgumentNullException(nameof(docket));
            return MissingEvidence(
                opened.IssueId,
                ObservationsFor(opened, docket)).Count;
        }

        private static List<PublicTimelineEntry> ProjectTimeline(
            SocietyState society,
            InstitutionalMaterialWorld world,
            EndogenousDocketState docket)
        {
            var rows = new List<OrderedTimelineEntry>();
            for (int i = 0; i < world.OfficialOwnerships.Count; i++)
            {
                OfficialOwnershipState ownership = world.OfficialOwnerships[i];
                if (!IsResourceInPublicCase(ownership.ResourceId, docket)) continue;
                rows.Add(new OrderedTimelineEntry(0, new PublicTimelineEntry(
                    $"timeline:{ownership.OwnershipRecordId}",
                    ownership.RecognitionTick,
                    PublicTimelineKind.OfficiallyRecorded,
                    "Ownership record available",
                    $"{Humanise(ownership.RegisteredOwnerId)} is registered as owner of " +
                    Humanise(ownership.ResourceId),
                    ownership.OwnershipSourceId,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    ownership.OwnershipRecordId)));
            }
            for (int i = 0; i < docket.Observations.Count; i++)
            {
                DocketObservation observation = docket.Observations[i];
                rows.Add(new OrderedTimelineEntry(1, new PublicTimelineEntry(
                    $"timeline:{observation.ObservationId}",
                    observation.RecordedTick,
                    observation.OfficiallySubmitted
                        ? PublicTimelineKind.OfficiallyRecorded
                        : PublicTimelineKind.Alleged,
                    Humanise(observation.ObservationKindId),
                    Humanise(observation.PropositionId),
                    observation.SourceRecordId,
                    observation.OriginatingRulingId,
                    HoldingForRuling(docket, observation.OriginatingRulingId),
                    ScopeTraceForRuling(docket, observation.OriginatingRulingId),
                    observation.ObservationId)));
            }
            for (int i = 0; i < docket.OpenCases.Count; i++)
            {
                EndogenousInstitutionalCase opened = docket.OpenCases[i];
                rows.Add(new OrderedTimelineEntry(2, new PublicTimelineEntry(
                    $"timeline:{opened.CaseId}",
                    opened.OpenedTick,
                    PublicTimelineKind.Alleged,
                    "Case entered docket",
                    $"{Humanise(opened.IssueId)} involving " +
                    JoinNames(society, opened.PartyIds),
                    opened.ObservationIds.Count > 0 ? opened.ObservationIds[0] : string.Empty,
                    opened.OriginatingRulingId,
                    HoldingForRuling(docket, opened.OriginatingRulingId),
                    ScopeTraceForRuling(docket, opened.OriginatingRulingId),
                    opened.ObservationIds.Count > 0 ? opened.ObservationIds[0] : string.Empty)));
            }
            for (int i = 0; i < docket.Rulings.Count; i++)
            {
                CommittedPlayerRuling ruling = docket.Rulings[i];
                rows.Add(new OrderedTimelineEntry(3, new PublicTimelineEntry(
                    $"timeline:{ruling.RulingId}",
                    ruling.CommittedTick,
                    PublicTimelineKind.RulingEffect,
                    "Ruling committed",
                    $"{Humanise(ruling.Disposition.ToString())}; " +
                    $"{ScopeLabel(ruling.Scope)}",
                    ruling.CaseId,
                    ruling.RulingId,
                    ruling.HoldingRuleId,
                    string.Empty,
                    string.Empty)));
            }
            for (int i = 0; i < docket.RemedyApplicationTraces.Count; i++)
            {
                EndogenousRemedyApplicationTrace trace =
                    docket.RemedyApplicationTraces[i];
                rows.Add(new OrderedTimelineEntry(4, new PublicTimelineEntry(
                    $"timeline:{trace.TraceId}",
                    trace.AppliedTick,
                    PublicTimelineKind.RulingEffect,
                    trace.MaterialStateChanged
                        ? "Remedy executed"
                        : "Remedy already satisfied",
                    trace.MaterialStateChanged
                        ? $"{Humanise(trace.ResourceId)} returned from " +
                          $"{Humanise(trace.PreviousPhysicalHolderId)} to its " +
                          $"registered owner, {Humanise(trace.NewPhysicalHolderId)}."
                        : $"{Humanise(trace.ResourceId)} was already held by its " +
                          "registered owner.",
                    trace.RulingId,
                    trace.RulingId,
                    EndogenousPlayerRulingService.PossessionHoldingRule,
                    string.Empty,
                    trace.MaterialEventId)));
            }
            for (int i = 0; i < docket.AccessRemedyApplicationTraces.Count; i++)
            {
                EndogenousAccessRemedyApplicationTrace trace =
                    docket.AccessRemedyApplicationTraces[i];
                rows.Add(new OrderedTimelineEntry(4, new PublicTimelineEntry(
                    $"timeline:{trace.TraceId}",
                    trace.AppliedTick,
                    PublicTimelineKind.RulingEffect,
                    trace.MaterialStateChanged
                        ? "Access remedy executed"
                        : "Access remedy already satisfied",
                    trace.MaterialStateChanged
                        ? $"Access restored for {DisplayName(society, trace.BeneficiaryAgentId)}."
                        : "The official access grant was already active.",
                    trace.RulingId,
                    trace.RulingId,
                    EndogenousPlayerRulingService.AccessHoldingRule,
                    string.Empty,
                    trace.MaterialEventId)));
            }
            for (int i = 0; i < docket.ScopeApplicationTraces.Count; i++)
            {
                EndogenousScopeApplicationTrace trace = docket.ScopeApplicationTraces[i];
                rows.Add(new OrderedTimelineEntry(5, new PublicTimelineEntry(
                    $"timeline:{trace.TraceId}",
                    trace.AppliedTick,
                    PublicTimelineKind.RulingEffect,
                    trace.ScopeMatched ? "Holding applied" : "Holding did not apply",
                    trace.ScopeMatched
                        ? $"The holding recognised protection for " +
                          DisplayName(society, trace.ActorId) + "."
                        : $"The holding did not cover " +
                          DisplayName(society, trace.ActorId) + " in this context.",
                    trace.RulingId,
                    trace.RulingId,
                    trace.HoldingRuleId,
                    trace.TraceId,
                    string.Empty)));
            }
            rows.Sort((left, right) =>
            {
                int tick = left.Entry.Cycle.CompareTo(right.Entry.Cycle);
                if (tick != 0) return tick;
                int order = left.Order.CompareTo(right.Order);
                return order != 0
                    ? order
                    : string.CompareOrdinal(left.Entry.EntryId, right.Entry.EntryId);
            });
            var result = new List<PublicTimelineEntry>(rows.Count);
            for (int i = 0; i < rows.Count; i++) result.Add(rows[i].Entry);
            return result;
        }

        private static List<ScopeMatchPreview> ProjectScopePreviews(
            SocietyState society,
            EndogenousDocketState docket,
            string currentCaseId)
        {
            EndogenousInstitutionalCase opened = docket.GetCase(currentCaseId);
            if (opened == null) return new List<ScopeMatchPreview>();
            return new List<ScopeMatchPreview>
            {
                BuildScopePreview(society, docket, opened, PlayerScopeChoice.Narrow),
                BuildScopePreview(society, docket, opened, PlayerScopeChoice.Broad),
            };
        }

        private static ScopeMatchPreview BuildScopePreview(
            SocietyState society,
            EndogenousDocketState docket,
            EndogenousInstitutionalCase opened,
            PlayerScopeChoice choice)
        {
            ScopeExpression scope = CausalLegibilityScopeFactory.Create(
                opened, choice);
            var matches = new List<string>();
            var organisations = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < society.Agents.Count; i++)
            {
                AgentState agent = society.Agents[i];
                if (!ScopeExpressionEvaluator.Matches(scope, OfficialContext(
                        agent, opened.IssueId)))
                    continue;
                matches.Add(agent.DisplayName);
                organisations.Add(agent.EmployerId);
            }
            int caseCount = 0;
            for (int i = 0; i < docket.OpenCases.Count; i++)
            {
                EndogenousInstitutionalCase candidate = docket.OpenCases[i];
                if (CaseMatches(scope, candidate)) caseCount++;
            }
            return new ScopeMatchPreview(
                choice,
                choice == PlayerScopeChoice.Narrow
                    ? "This issue, limited to the present claimant."
                    : "This issue throughout Branch 42, regardless of claimant.",
                caseCount,
                matches.Count,
                organisations.Count,
                matches,
                "Future disputes are not predicted. New contexts will be tested " +
                "against the holding only when they become institutionally visible.");
        }

        private static List<KnownDecisionPressure> ProjectKnownDecisionPressures(
            SocietyState society,
            EndogenousDocketState docket)
        {
            var result = new List<KnownDecisionPressure>();
            for (int i = 0; i < docket.ScopeApplicationTraces.Count; i++)
            {
                EndogenousScopeApplicationTrace trace = docket.ScopeApplicationTraces[i];
                result.Add(new KnownDecisionPressure(
                    trace.ActorId,
                    trace.ScopeMatched
                        ? $"{DisplayName(society, trace.ActorId)} had officially " +
                          "recognised protection when the later action became available."
                        : $"The holding did not provide recognised protection to " +
                          $"{DisplayName(society, trace.ActorId)} in the later context.",
                    trace.TraceId));
            }
            return result;
        }

        internal static ScopeMatchPreview FindScopePreview(
            PlayerInstitutionView view,
            PlayerScopeChoice choice)
        {
            for (int i = 0; i < view.ScopePreviews.Count; i++)
                if (view.ScopePreviews[i].Choice == choice) return view.ScopePreviews[i];
            throw new InvalidOperationException($"No scope preview exists for {choice}.");
        }

        private static ScopeMatchContext OfficialContext(AgentState agent, string issueId)
        {
            var statusIds = new List<string>();
            for (int i = 0; i < agent.Standing.OfficialStatuses.Count; i++)
                if (agent.Standing.OfficialStatuses[i].Recognised)
                    statusIds.Add(agent.Standing.OfficialStatuses[i].StatusId);
            return new ScopeMatchContext
            {
                AgentId = agent.StableId,
                IssueId = issueId,
                OrganisationId = agent.EmployerId,
                JurisdictionId = "branch-42",
                OfficialStatusIds = statusIds,
            };
        }

        private static bool CaseMatches(
            ScopeExpression scope,
            EndogenousInstitutionalCase opened)
        {
            if (opened.PartyIds.Count == 0)
                return ScopeExpressionEvaluator.Matches(scope, new ScopeMatchContext
                {
                    IssueId = opened.IssueId,
                    JurisdictionId = "branch-42",
                });
            for (int i = 0; i < opened.PartyIds.Count; i++)
                if (ScopeExpressionEvaluator.Matches(scope, new ScopeMatchContext
                    {
                        AgentId = opened.PartyIds[i],
                        IssueId = opened.IssueId,
                        JurisdictionId = "branch-42",
                    }))
                    return true;
            return false;
        }

        private static List<DocketObservation> ObservationsFor(
            EndogenousInstitutionalCase opened,
            EndogenousDocketState docket)
        {
            var result = new List<DocketObservation>();
            for (int i = 0; i < opened.ObservationIds.Count; i++)
            {
                DocketObservation observation = docket.GetObservation(
                    opened.ObservationIds[i]);
                if (observation != null) result.Add(observation);
            }
            result.Sort((left, right) =>
                string.CompareOrdinal(left.ObservationId, right.ObservationId));
            return result;
        }

        private static List<string> MissingEvidence(
            string issueId,
            IReadOnlyList<DocketObservation> observations)
        {
            var missing = new List<string>();
            bool hasDirectStatement = false;
            for (int i = 0; i < observations.Count; i++)
                if (!string.IsNullOrWhiteSpace(observations[i].SourceAgentId))
                    hasDirectStatement = true;
            if (!hasDirectStatement) missing.Add("No direct statement is in the record.");
            if (string.Equals(
                    issueId,
                    EndogenousIssueKindIds.PossessionDispute,
                    StringComparison.Ordinal))
            {
                missing.Add("No consent or authorised-transfer record is available.");
                missing.Add("Intent remains undiscoverable from the official record.");
            }
            else if (string.Equals(issueId,
                         EndogenousIssueKindIds.IdentityContinuity,
                         StringComparison.Ordinal))
            {
                missing.Add("No complete supersession chain is in the record.");
                missing.Add("Biometric continuity remains contested.");
            }
            else if (string.Equals(issueId,
                         EndogenousIssueKindIds.DependencyEmergencySupport,
                         StringComparison.Ordinal))
            {
                missing.Add("Dependency status is not independently verified.");
                missing.Add("Delay harm cannot be measured from the official record.");
            }
            return missing;
        }

        private static List<string> RemediesFor(string issueId)
        {
            if (string.Equals(
                    issueId,
                    EndogenousIssueKindIds.PossessionDispute,
                    StringComparison.Ordinal))
                return new List<string> { "Restore possession", "No change" };
            if (string.Equals(
                    issueId,
                    EndogenousIssueKindIds.AccessWithdrawal,
                    StringComparison.Ordinal))
                return new List<string> { "Restore access", "No change" };
            if (string.Equals(issueId,
                    EndogenousIssueKindIds.IdentityContinuity,
                    StringComparison.Ordinal))
                return new List<string> { "Restore identity continuity", "No change" };
            if (string.Equals(issueId,
                    EndogenousIssueKindIds.DependencyEmergencySupport,
                    StringComparison.Ordinal))
                return new List<string> { "Grant emergency support", "No change" };
            return new List<string> { "Recognise collective", "No change" };
        }

        private static List<string> DirectChanges(
            CommittedPlayerRuling ruling,
            EndogenousDocketState docket)
        {
            var result = new List<string>
            {
                $"Record disposition: {Humanise(ruling.Disposition.ToString())}",
            };
            for (int i = 0; i < ruling.AppliedProcedureIds.Count; i++)
                result.Add(
                    "Apply procedure: " + Humanise(ruling.AppliedProcedureIds[i]));
            if (IsDenial(ruling.Disposition))
            {
                result.Add($"Do not establish proposed holding: " +
                           Humanise(ruling.HoldingRuleId));
                result.Add("Do not apply proposed scope or material remedy");
                return result;
            }
            result.Add($"Establish holding: {Humanise(ruling.HoldingRuleId)}");
            result.Add($"Apply scope: {ScopeLabel(ruling.Scope)}");
            EndogenousAccessRemedyApplicationTrace accessTrace =
                AccessRemedyTraceForRuling(docket, ruling.RulingId);
            if (accessTrace != null)
            {
                result.Add(
                    "Execute remedy: restore access for " +
                    Humanise(accessTrace.BeneficiaryAgentId));
            }
            else if (CollectiveRemedyTraceForRuling(
                         docket, ruling.RulingId) is { } collectiveTrace)
            {
                result.Add(
                    "Execute remedy: recognise collective standing for " +
                    collectiveTrace.MemberAgentIds.Count + " members");
            }
            else
            {
                EndogenousRemedyApplicationTrace trace = RemedyTraceForRuling(
                    docket, ruling.RulingId);
                result.Add(trace == null
                    ? "Required remedy has no recorded execution"
                    : $"Execute remedy: {Humanise(trace.ResourceId)} to " +
                      Humanise(trace.NewPhysicalHolderId));
            }
            return result;
        }

        private static EndogenousAccessRemedyApplicationTrace
            AccessRemedyTraceForRuling(
                EndogenousDocketState docket,
                string rulingId)
        {
            for (int i = 0; i < docket.AccessRemedyApplicationTraces.Count; i++)
                if (string.Equals(
                        docket.AccessRemedyApplicationTraces[i].RulingId,
                        rulingId,
                        StringComparison.Ordinal))
                    return docket.AccessRemedyApplicationTraces[i];
            return null;
        }

        private static EndogenousCollectiveRemedyApplicationTrace
            CollectiveRemedyTraceForRuling(
                EndogenousDocketState docket,
                string rulingId)
        {
            for (int i = 0;
                 i < docket.CollectiveRemedyApplicationTraces.Count;
                 i++)
                if (string.Equals(
                        docket.CollectiveRemedyApplicationTraces[i].RulingId,
                        rulingId,
                        StringComparison.Ordinal))
                    return docket.CollectiveRemedyApplicationTraces[i];
            return null;
        }

        private static bool IsDenial(RulingDisposition disposition)
        {
            return disposition == RulingDisposition.Denied ||
                   disposition == RulingDisposition.ReversedAndDenied;
        }

        private static EndogenousRemedyApplicationTrace RemedyTraceForRuling(
            EndogenousDocketState docket,
            string rulingId)
        {
            for (int i = 0; i < docket.RemedyApplicationTraces.Count; i++)
                if (string.Equals(
                        docket.RemedyApplicationTraces[i].RulingId,
                        rulingId,
                        StringComparison.Ordinal))
                    return docket.RemedyApplicationTraces[i];
            return null;
        }

        private static string CurrentCaseId(EndogenousDocketState docket)
        {
            if (docket.OpenCases.Count == 0) return string.Empty;
            return docket.OpenCases[0].CaseId;
        }

        private static CommittedPlayerRuling RulingForCase(
            EndogenousDocketState docket,
            string caseId)
        {
            for (int i = 0; i < docket.Rulings.Count; i++)
                if (string.Equals(
                        docket.Rulings[i].CaseId,
                        caseId,
                        StringComparison.Ordinal))
                    return docket.Rulings[i];
            return null;
        }

        private static string HoldingForRuling(
            EndogenousDocketState docket,
            string rulingId)
        {
            if (string.IsNullOrWhiteSpace(rulingId)) return string.Empty;
            for (int i = 0; i < docket.Rulings.Count; i++)
                if (string.Equals(
                        docket.Rulings[i].RulingId,
                        rulingId,
                        StringComparison.Ordinal))
                    return docket.Rulings[i].HoldingRuleId;
            return string.Empty;
        }

        private static string ScopeTraceForRuling(
            EndogenousDocketState docket,
            string rulingId)
        {
            if (string.IsNullOrWhiteSpace(rulingId)) return string.Empty;
            for (int i = 0; i < docket.ScopeApplicationTraces.Count; i++)
                if (string.Equals(
                        docket.ScopeApplicationTraces[i].RulingId,
                        rulingId,
                        StringComparison.Ordinal))
                    return docket.ScopeApplicationTraces[i].TraceId;
            return string.Empty;
        }

        private static bool IsResourceInPublicCase(
            string resourceId,
            EndogenousDocketState docket)
        {
            for (int i = 0; i < docket.Observations.Count; i++)
                if (string.Equals(
                        docket.Observations[i].OfficialResourceId,
                        resourceId,
                        StringComparison.Ordinal))
                    return true;
            return false;
        }

        private static string ScopeLabel(ScopeExpression scope)
        {
            if (scope == null) return "No scope";
            if (ContainsPredicate(scope, ScopePredicateKind.AgentEquals))
                return "Present claimant only";
            return "All possession disputes in Branch 42";
        }

        private static bool ContainsPredicate(
            ScopeExpression expression,
            ScopePredicateKind kind)
        {
            if (expression.Kind == ScopeExpressionKind.Predicate &&
                expression.PredicateKind == kind)
                return true;
            for (int i = 0; i < expression.Children.Count; i++)
                if (ContainsPredicate(expression.Children[i], kind)) return true;
            return false;
        }

        private static string DisplayName(SocietyState society, string agentId)
        {
            AgentState agent = society.GetAgent(agentId);
            return agent == null ? Humanise(agentId) : agent.DisplayName;
        }

        private static string JoinNames(SocietyState society, IReadOnlyList<string> ids)
        {
            var names = new List<string>(ids.Count);
            for (int i = 0; i < ids.Count; i++)
                names.Add(DisplayName(society, ids[i]));
            return names.Count == 0 ? "unidentified parties" : string.Join(", ", names);
        }

        private static List<string> HumaniseAll(IReadOnlyList<string> values)
        {
            var result = new List<string>(values.Count);
            for (int i = 0; i < values.Count; i++) result.Add(Humanise(values[i]));
            return result;
        }

        internal static string Humanise(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "Not recorded";
            string result = value;
            int lastDot = result.LastIndexOf('.');
            if (lastDot >= 0 && lastDot < result.Length - 1)
                result = result.Substring(lastDot + 1);
            result = result.Replace('-', ' ').Replace('_', ' ');
            return char.ToUpperInvariant(result[0]) + result.Substring(1);
        }

        private static string ReliabilityLabel(int reliability)
        {
            if (reliability >= 90) return "HIGH DOCUMENTARY SUPPORT";
            if (reliability >= 70) return "MATERIAL SUPPORT";
            if (reliability >= 50) return "CONTESTED SUPPORT";
            return "WEAK SUPPORT";
        }

        private static bool Contains(IReadOnlyList<string> values, string expected)
        {
            for (int i = 0; i < values.Count; i++)
                if (string.Equals(values[i], expected, StringComparison.Ordinal)) return true;
            return false;
        }

        private static void AddUnique(List<string> values, string value)
        {
            for (int i = 0; i < values.Count; i++)
                if (string.Equals(values[i], value, StringComparison.Ordinal)) return;
            values.Add(value);
        }

        private readonly struct OrderedTimelineEntry
        {
            internal OrderedTimelineEntry(int order, PublicTimelineEntry entry)
            {
                Order = order;
                Entry = entry;
            }

            internal int Order { get; }
            internal PublicTimelineEntry Entry { get; }
        }
    }
}
