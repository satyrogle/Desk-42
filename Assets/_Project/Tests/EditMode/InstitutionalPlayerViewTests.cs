using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Desk42.Institutional;
using Desk42.Institutional.Player;
using NUnit.Framework;

namespace Desk42.Tests.EditMode
{
    [TestFixture]
    public sealed class InstitutionalPlayerViewTests
    {
        [Test]
        public void InitialView_ContainsEightPeopleOneCaseAndAttributableUncertainty()
        {
            CausalLegibilitySliceSession session = CausalLegibilitySliceSession.Create();
            PlayerInstitutionView view = session.View;

            Assert.AreEqual(PlayerSlicePhase.Rule, view.Phase);
            Assert.AreEqual(8, view.Agents.Count);
            Assert.AreEqual(1, view.Cases.Count);
            Assert.GreaterOrEqual(view.Evidence.Count, 2);
            Assert.AreEqual(2, view.ScopePreviews.Count);
            Assert.IsNotEmpty(view.UnknownsSummary);
            PublicCaseRecord opened = view.Cases.Single();
            Assert.IsNotEmpty(opened.DocketBasis);
            Assert.IsNotEmpty(opened.MissingEvidence);
            Assert.Greater(opened.EvidenceSupportMaximum,
                opened.EvidenceSupportMinimum);
            Assert.IsTrue(view.Evidence.Any(value => value.Citable));
            Assert.IsTrue(view.Evidence.Any(value => !value.Citable &&
                value.OfficialStatus == "OFFICIAL CONTEXT"));
        }

        [Test]
        public void PlayerViewTypeGraph_ExcludesAuthorityAndAssessorState()
        {
            Type[] roots =
            {
                typeof(PlayerInstitutionView),
                typeof(PublicAgentRecord),
                typeof(PublicCaseRecord),
                typeof(PublicEvidenceRecord),
                typeof(PublicRulingRecord),
                typeof(PublicTimelineEntry),
                typeof(ScopeMatchPreview),
                typeof(KnownDecisionPressure),
                typeof(PlayerRulingDraft),
                typeof(PlayerRulingPreview),
            };
            var forbidden = new HashSet<Type>
            {
                typeof(IncidentCandidate),
                typeof(InstitutionalMaterialWorld),
                typeof(MaterialWorldEvent),
                typeof(AgentPerception),
                typeof(AgentDecision),
                typeof(CandidateEvaluation),
                typeof(EndogenousRemedyApplicationTrace),
                typeof(EndogenousScopeApplicationTrace),
                typeof(EndogenousRunSnapshot),
            };
            var visited = new HashSet<Type>();
            for (int i = 0; i < roots.Length; i++)
                AssertSafeTypeGraph(roots[i], forbidden, visited, roots[i].Name);
        }

        [Test]
        public void Projection_IgnoresPrivateNeedsBeliefsAndHiddenPossession()
        {
            EndogenousRunSnapshot snapshot =
                CausalLegibilitySliceSeed.CreatePreRulingSnapshot();
            PlayerInstitutionView before = PlayerInstitutionProjector.Project(
                snapshot.Society, snapshot.MaterialWorld, snapshot.Docket);
            AgentState actor = snapshot.Society.GetAgent(
                CausalLegibilitySliceSeed.OriginAgentId);
            PublicAgentRecord publicActorBefore = before.Agents.Single(value =>
                value.AgentId == actor.StableId);
            actor.GetNeed(NeedKind.Health).Pressure = 1;
            actor.Beliefs.Add(new BeliefState
            {
                BeliefId = "belief.hidden",
                PropositionId = "private-proposition",
                SubjectId = actor.StableId,
                Confidence = 100,
                Secrecy = 100,
            });
            snapshot.MaterialWorld.GetResource(
                CausalLegibilitySliceSeed.LaterResourceId).PhysicalHolderId =
                "agent.ollo-seven";
            actor.EmployerId = "employer.hidden-rewrite";
            actor.HouseholdId = "household.hidden-rewrite";

            PlayerInstitutionView after = PlayerInstitutionProjector.Project(
                snapshot.Society, snapshot.MaterialWorld, snapshot.Docket);
            PublicAgentRecord publicActorAfter = after.Agents.Single(value =>
                value.AgentId == actor.StableId);

            Assert.AreEqual(PublicSignature(before), PublicSignature(after));
            Assert.AreEqual(
                publicActorBefore.RecognisedEmployer,
                publicActorAfter.RecognisedEmployer);
            Assert.AreEqual(
                publicActorBefore.RecognisedHousehold,
                publicActorAfter.RecognisedHousehold);
        }

        [Test]
        public void Draft_CarriesRevisionAndEvidenceEnvelope_AndRejectsStaleInput()
        {
            CausalLegibilitySliceSession session = CausalLegibilitySliceSession.Create();
            PlayerRulingDraft current = session.CreateDraft(
                PlayerScopeChoice.Narrow, PlayerRulingDisposition.Recognised);
            var stale = new PlayerRulingDraft(
                current.CommandId,
                current.CaseId,
                current.ExpectedCaseRevision + 1,
                current.EvidenceEnvelopeHash,
                current.RecognisedFactIds,
                current.CitedEvidenceIds,
                current.Disposition,
                current.ScopeChoice);

            Assert.Greater(current.ExpectedCaseRevision, 0);
            Assert.IsNotEmpty(current.EvidenceEnvelopeHash);
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => session.Commit(stale));
            StringAssert.Contains("case changed", error.Message.ToLowerInvariant());
        }

        [Test]
        public void ScopePreview_UsesOnlyCurrentOfficialMatchesAndMakesNoPrediction()
        {
            CausalLegibilitySliceSession session = CausalLegibilitySliceSession.Create();
            PlayerRulingDraft draft = session.CreateDraft(
                PlayerScopeChoice.Broad, PlayerRulingDisposition.Recognised);
            PlayerRulingPreview preview = session.Preview(draft);

            Assert.AreEqual(PlayerScopeChoice.Broad, preview.Scope.Choice);
            Assert.AreEqual(2, Enum.GetValues(typeof(PlayerRulingDisposition)).Length);
            StringAssert.StartsWith("System-derived:", preview.Finding);
            StringAssert.StartsWith("System-derived proposal:", preview.Holding);
            Assert.AreEqual("Fixed: Prospective", preview.TemporalReach);
            StringAssert.StartsWith("Disposition-required:", preview.Remedy);
            Assert.AreEqual(1, preview.Scope.MatchingCaseCount);
            Assert.AreEqual(8, preview.Scope.PotentiallyCoveredAgentCount);
            StringAssert.Contains("not predicted", preview.Scope.FutureMatchNote);
            string direct = string.Join("|", preview.DirectInstitutionalChanges);
            StringAssert.DoesNotContain("Mara", direct);
            StringAssert.DoesNotContain("descendant", direct.ToLowerInvariant());
            StringAssert.DoesNotContain("will", direct.ToLowerInvariant());
        }

        [Test]
        public void BroadAndNarrowScope_ProduceLegibleDifferentHistories()
        {
            CausalLegibilitySliceSession narrow = CausalLegibilitySliceSession.Create();
            CausalLegibilitySliceSession broad = CausalLegibilitySliceSession.Create();

            narrow.Commit(narrow.CreateDraft(
                PlayerScopeChoice.Narrow, PlayerRulingDisposition.Recognised));
            broad.Commit(broad.CreateDraft(
                PlayerScopeChoice.Broad, PlayerRulingDisposition.Recognised));

            Assert.AreEqual(1, narrow.View.Cases.Count);
            Assert.AreEqual(2, broad.View.Cases.Count);
            Assert.AreEqual(PlayerSlicePhase.Review, broad.View.Phase);
            PublicCaseRecord descendant = broad.View.Cases.Single(value =>
                !string.IsNullOrEmpty(value.ParentCaseId));
            Assert.IsNotEmpty(descendant.OriginatingRulingId);
            Assert.IsTrue(broad.View.Timeline.Any(value =>
                value.Kind == PublicTimelineKind.RulingEffect &&
                value.Headline == "Holding applied" &&
                !string.IsNullOrEmpty(value.ScopeMatchId)));
            Assert.IsTrue(broad.View.Timeline.Any(value =>
                value.Kind == PublicTimelineKind.RulingEffect &&
                value.Headline == "Remedy executed" &&
                value.Detail.Contains("registered owner")));
            Assert.IsTrue(broad.View.Rulings.Single().DirectInstitutionalChanges.Any(
                value => value.StartsWith("Execute remedy:",
                    StringComparison.Ordinal)));
            Assert.IsTrue(narrow.View.Timeline.Any(value =>
                value.Headline == "Holding did not apply"));
            Assert.IsTrue(broad.View.KnownDecisionPressures.Any(value =>
                value.Statement.Contains("officially recognised protection")));
            Assert.IsFalse(PublicSignature(broad.View).Contains("utility"));
        }

        [Test]
        public void Replay_ReturnsToIdenticalPreRulingViewAndAllowsAnotherScope()
        {
            CausalLegibilitySliceSession session = CausalLegibilitySliceSession.Create();
            string original = PublicSignature(session.View);
            session.Commit(session.CreateDraft(
                PlayerScopeChoice.Broad, PlayerRulingDisposition.Recognised));
            Assert.AreEqual(2, session.View.Cases.Count);

            session.ReplayFromPreRuling();

            Assert.AreEqual(original, PublicSignature(session.View));
            session.Commit(session.CreateDraft(
                PlayerScopeChoice.Narrow, PlayerRulingDisposition.Recognised));
            Assert.AreEqual(1, session.View.Cases.Count);
        }

        [Test]
        public void SaveLoad_PreservesPlayableHistoryAndReplayOrigin()
        {
            string directory = Path.Combine(
                Path.GetTempPath(), "desk42-player-view-" + Guid.NewGuid().ToString("N"));
            string path = Path.Combine(directory, "session.json");
            try
            {
                CausalLegibilitySliceSession session =
                    CausalLegibilitySliceSession.Create();
                session.Commit(session.CreateDraft(
                    PlayerScopeChoice.Broad, PlayerRulingDisposition.Recognised));
                session.Save(path);
                Assert.IsFalse(File.Exists(path + ".pre-ruling"));

                CausalLegibilitySliceSession loaded =
                    CausalLegibilitySliceSession.Load(path);

                Assert.AreEqual(PublicSignature(session.View),
                    PublicSignature(loaded.View));
                Assert.AreEqual(2, loaded.View.Cases.Count);
                loaded.ReplayFromPreRuling();
                Assert.AreEqual(1, loaded.View.Cases.Count);
                Assert.IsEmpty(loaded.View.Rulings);
            }
            finally
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, recursive: true);
            }
        }

        [Test]
        public void SessionSave_AtomicallyRecoversOneCompatibleGeneration()
        {
            string directory = Path.Combine(
                Path.GetTempPath(), "desk42-session-envelope-" +
                Guid.NewGuid().ToString("N"));
            string path = Path.Combine(directory, "session.json");
            try
            {
                CausalLegibilitySliceSession session =
                    CausalLegibilitySliceSession.Create();
                session.Save(path);
                session.Commit(session.CreateDraft(
                    PlayerScopeChoice.Broad,
                    PlayerRulingDisposition.Recognised));
                session.Save(path);
                Assert.IsTrue(File.Exists(path + ".bak"));

                File.WriteAllText(path, "interrupted generation");
                CausalLegibilitySliceSession recovered =
                    CausalLegibilitySliceSession.Load(path);

                Assert.IsEmpty(recovered.View.Rulings);
                Assert.AreEqual(1, recovered.View.Cases.Count);
            }
            finally
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, recursive: true);
            }
        }

        [Test]
        public void SessionEnvelope_RejectsCurrentHistoryFromAnotherSession()
        {
            EndogenousRunSnapshot origin =
                CausalLegibilitySliceSeed.CreatePreRulingSnapshot();
            EndogenousRunSessionSnapshot first =
                EndogenousRunSessionSnapshotService.Capture(
                    "session.first", 1, origin, origin);
            EndogenousRunSessionSnapshot second =
                EndogenousRunSessionSnapshotService.Capture(
                    "session.second", 1, origin, origin);
            first.Current = second.Current;
            first.CurrentSnapshotSha256 =
                EndogenousRunSnapshotStore.PayloadSha256(first.Current);
            first.GenerationId = EndogenousRunSessionSnapshotService.GenerationId(
                first.Generation,
                first.OriginSnapshotSha256,
                first.CurrentSnapshotSha256);

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => EndogenousRunSessionSnapshotValidator.Validate(first));
            StringAssert.Contains("incompatible", error.Message.ToLowerInvariant());
        }

        [Test]
        public void PlayerAssembly_HasNoScenarioOrUnityDependency()
        {
            string[] references = typeof(CausalLegibilitySliceSession).Assembly
                .GetReferencedAssemblies()
                .Select(value => value.Name)
                .ToArray();
            CollectionAssert.DoesNotContain(references, "Desk42.Institutional.Scenarios");
            CollectionAssert.DoesNotContain(references, "UnityEngine");
        }

        [Test]
        public void PlayerAssembly_AllExportedApiSignaturesExcludeDomainAndAuthority()
        {
            Assembly playerAssembly = typeof(CausalLegibilitySliceSession).Assembly;
            Type[] exported = playerAssembly.GetExportedTypes();
            Assert.IsNotEmpty(exported);
            for (int i = 0; i < exported.Length; i++)
            {
                Type type = exported[i];
                AssertPublicApiType(type.BaseType, playerAssembly, type.FullName + ".base");
                foreach (Type contract in type.GetInterfaces())
                    AssertPublicApiType(contract, playerAssembly, type.FullName + ".interface");
                foreach (FieldInfo field in type.GetFields(
                             BindingFlags.Instance | BindingFlags.Static |
                             BindingFlags.Public | BindingFlags.DeclaredOnly))
                    AssertPublicApiType(
                        field.FieldType, playerAssembly, type.FullName + "." + field.Name);
                foreach (PropertyInfo property in type.GetProperties(
                             BindingFlags.Instance | BindingFlags.Static |
                             BindingFlags.Public | BindingFlags.DeclaredOnly))
                    AssertPublicApiType(
                        property.PropertyType,
                        playerAssembly,
                        type.FullName + "." + property.Name);
                foreach (MethodInfo method in type.GetMethods(
                             BindingFlags.Instance | BindingFlags.Static |
                             BindingFlags.Public | BindingFlags.DeclaredOnly))
                {
                    AssertPublicApiType(
                        method.ReturnType,
                        playerAssembly,
                        type.FullName + "." + method.Name + ".return");
                    foreach (ParameterInfo parameter in method.GetParameters())
                        AssertPublicApiType(
                            parameter.ParameterType,
                            playerAssembly,
                            type.FullName + "." + method.Name + "." + parameter.Name);
                }
                foreach (ConstructorInfo constructor in type.GetConstructors())
                    foreach (ParameterInfo parameter in constructor.GetParameters())
                        AssertPublicApiType(
                            parameter.ParameterType,
                            playerAssembly,
                            type.FullName + ".ctor." + parameter.Name);
            }
        }

        private static void AssertSafeTypeGraph(
            Type type,
            ISet<Type> forbidden,
            ISet<Type> visited,
            string path)
        {
            if (type == null || !visited.Add(type)) return;
            Assert.IsFalse(forbidden.Contains(type), $"{path} exposes {type.FullName}.");
            if (type.IsPrimitive || type.IsEnum || type == typeof(string) ||
                type == typeof(decimal) || type == typeof(DateTime))
                return;
            if (type.IsArray)
            {
                AssertSafeTypeGraph(type.GetElementType(), forbidden, visited, path + "[]");
                return;
            }
            if (type.IsGenericType)
                foreach (Type argument in type.GetGenericArguments())
                    AssertSafeTypeGraph(argument, forbidden, visited, path + "<>");
            if (type.Namespace == null ||
                !type.Namespace.StartsWith(
                    "Desk42.Institutional.Player",
                    StringComparison.Ordinal))
                return;
            foreach (PropertyInfo property in type.GetProperties(
                         BindingFlags.Instance | BindingFlags.Public))
            {
                AssertSafeTypeGraph(
                    property.PropertyType,
                    forbidden,
                    visited,
                    path + "." + property.Name);
            }
            foreach (FieldInfo field in type.GetFields(
                         BindingFlags.Instance | BindingFlags.Public))
            {
                AssertSafeTypeGraph(
                    field.FieldType,
                    forbidden,
                    visited,
                    path + "." + field.Name);
            }
        }

        private static void AssertPublicApiType(
            Type type,
            Assembly playerAssembly,
            string path)
        {
            if (type == null) return;
            if (type.IsByRef || type.IsPointer || type.IsArray)
            {
                AssertPublicApiType(type.GetElementType(), playerAssembly, path + "[]");
                return;
            }
            if (type.IsGenericType)
                foreach (Type argument in type.GetGenericArguments())
                    AssertPublicApiType(argument, playerAssembly, path + "<>");
            string assemblyName = type.Assembly.GetName().Name ?? string.Empty;
            if (type.Assembly != playerAssembly && assemblyName.StartsWith(
                    "Desk42.Institutional",
                    StringComparison.Ordinal))
            {
                Assert.Fail($"{path} exposes {type.FullName} from {assemblyName}.");
            }
        }

        private static string PublicSignature(PlayerInstitutionView view)
        {
            return string.Join("|", new[]
            {
                view.Phase.ToString(),
                view.CurrentCycle.ToString(),
                string.Join(",", view.Agents.Select(value =>
                    value.AgentId + ":" + string.Join("+", value.RecognisedStatuses))),
                string.Join(",", view.Cases.Select(value =>
                    value.CaseId + ":" + value.CaseRevision + ":" +
                    value.OriginatingRulingId)),
                string.Join(",", view.Evidence.Select(value =>
                    value.EvidenceId + ":" + value.OfficialStatus)),
                string.Join(",", view.Rulings.Select(value =>
                    value.RulingId + ":" + value.Scope)),
                string.Join(",", view.Timeline.Select(value =>
                    value.EntryId + ":" + value.Headline + ":" + value.ScopeMatchId)),
                string.Join(",", view.KnownDecisionPressures.Select(value =>
                    value.AgentId + ":" + value.Statement)),
            });
        }
    }
}
