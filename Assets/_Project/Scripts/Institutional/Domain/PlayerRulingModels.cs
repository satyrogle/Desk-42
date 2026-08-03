using System;
using System.Collections.Generic;

namespace Desk42.Institutional
{
    public enum ScopeExpressionKind
    {
        Predicate,
        All,
        Any,
        Not,
    }

    public enum ScopePredicateKind
    {
        FactEquals,
        IssueEquals,
        OrganisationEquals,
        JurisdictionEquals,
        RelationshipTypeEquals,
        OfficialStatusEquals,
        AgentEquals,
        ActivityEquals,
    }

    public enum TemporalReach
    {
        Prospective,
        Retrospective,
    }

    [Serializable]
    public sealed class ScopeExpression
    {
        public ScopeExpressionKind Kind;
        public ScopePredicateKind PredicateKind;
        public string Key;
        public string Value;
        public List<ScopeExpression> Children = new();
    }

    [Serializable]
    public sealed class ScopeMatchContext
    {
        public string AgentId;
        public string IssueId;
        public string OrganisationId;
        public string JurisdictionId;
        public string RelationshipTypeId;
        public string ActivityId;
        public CaseFactSet Facts = new();
        public List<string> OfficialStatusIds = new();
    }

    public static class ScopeExpressionEvaluator
    {
        public const int MaximumDepth = 4;
        public const int MaximumNodes = 32;

        public static void Validate(ScopeExpression expression)
        {
            int nodes = 0;
            Validate(expression, depth: 0, ref nodes);
        }

        public static bool Matches(ScopeExpression expression, ScopeMatchContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            context.Facts ??= new CaseFactSet();
            context.OfficialStatusIds ??= new List<string>();
            context.Facts.Validate();
            Validate(expression);
            return Evaluate(expression, context);
        }

        public static ScopeExpression Copy(ScopeExpression source)
        {
            Validate(source);
            var copy = new ScopeExpression
            {
                Kind = source.Kind,
                PredicateKind = source.PredicateKind,
                Key = source.Key,
                Value = source.Value,
                Children = new List<ScopeExpression>(source.Children.Count),
            };
            for (int i = 0; i < source.Children.Count; i++)
                copy.Children.Add(Copy(source.Children[i]));
            return copy;
        }

        private static void Validate(ScopeExpression expression, int depth, ref int nodes)
        {
            if (expression == null)
                throw new InvalidOperationException("A scope expression is required.");
            if (depth > MaximumDepth)
                throw new InvalidOperationException("Scope expression exceeds maximum depth.");
            nodes++;
            if (nodes > MaximumNodes)
                throw new InvalidOperationException("Scope expression exceeds maximum node count.");
            if (!Enum.IsDefined(typeof(ScopeExpressionKind), expression.Kind) ||
                !Enum.IsDefined(typeof(ScopePredicateKind), expression.PredicateKind) ||
                expression.Children == null)
            {
                throw new InvalidOperationException("Scope expression contains undefined state.");
            }

            switch (expression.Kind)
            {
                case ScopeExpressionKind.Predicate:
                    if (expression.Children.Count != 0 || string.IsNullOrWhiteSpace(expression.Value))
                        throw new InvalidOperationException(
                            "A scope predicate requires a value and no children.");
                    if (expression.PredicateKind == ScopePredicateKind.FactEquals &&
                        string.IsNullOrWhiteSpace(expression.Key))
                    {
                        throw new InvalidOperationException(
                            "A fact scope predicate requires a key.");
                    }
                    break;
                case ScopeExpressionKind.All:
                case ScopeExpressionKind.Any:
                    if (expression.Children.Count == 0)
                        throw new InvalidOperationException(
                            "ALL and ANY scope expressions require children.");
                    break;
                case ScopeExpressionKind.Not:
                    if (expression.Children.Count != 1)
                        throw new InvalidOperationException(
                            "A NOT scope expression requires exactly one child.");
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            for (int i = 0; i < expression.Children.Count; i++)
                Validate(expression.Children[i], depth + 1, ref nodes);
        }

        private static bool Evaluate(ScopeExpression expression, ScopeMatchContext context)
        {
            switch (expression.Kind)
            {
                case ScopeExpressionKind.Predicate:
                    return EvaluatePredicate(expression, context);
                case ScopeExpressionKind.All:
                    for (int i = 0; i < expression.Children.Count; i++)
                        if (!Evaluate(expression.Children[i], context)) return false;
                    return true;
                case ScopeExpressionKind.Any:
                    for (int i = 0; i < expression.Children.Count; i++)
                        if (Evaluate(expression.Children[i], context)) return true;
                    return false;
                case ScopeExpressionKind.Not:
                    return !Evaluate(expression.Children[0], context);
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private static bool EvaluatePredicate(
            ScopeExpression expression,
            ScopeMatchContext context)
        {
            switch (expression.PredicateKind)
            {
                case ScopePredicateKind.FactEquals:
                    return context.Facts.Contains(expression.Key, expression.Value);
                case ScopePredicateKind.IssueEquals:
                    return Equal(context.IssueId, expression.Value);
                case ScopePredicateKind.OrganisationEquals:
                    return Equal(context.OrganisationId, expression.Value);
                case ScopePredicateKind.JurisdictionEquals:
                    return Equal(context.JurisdictionId, expression.Value);
                case ScopePredicateKind.RelationshipTypeEquals:
                    return Equal(context.RelationshipTypeId, expression.Value);
                case ScopePredicateKind.AgentEquals:
                    return Equal(context.AgentId, expression.Value);
                case ScopePredicateKind.ActivityEquals:
                    return Equal(context.ActivityId, expression.Value);
                case ScopePredicateKind.OfficialStatusEquals:
                    for (int i = 0; i < context.OfficialStatusIds.Count; i++)
                        if (Equal(context.OfficialStatusIds[i], expression.Value)) return true;
                    return false;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private static bool Equal(string left, string right)
            => string.Equals(left, right, StringComparison.Ordinal);
    }

    [Serializable]
    public sealed class PlayerRulingCommand
    {
        public string CommandId;
        public string CaseId;
        public int ExpectedCaseVersion;
        public string EvidenceEnvelopeHash;
        public List<string> RecognisedFactIds = new();
        public List<string> CitedEvidenceArtifactIds = new();
        public RulingDisposition Disposition;
        public string HoldingRuleId;
        public ScopeExpression Scope;
        public TemporalReach TemporalReach;
        public List<string> RemedyDefinitionIds = new();
        public List<string> AppliedProcedureIds = new();
    }

    [Serializable]
    public sealed class CommittedPlayerRuling
    {
        public string RulingId;
        public string PlayerCommandId;
        public string CaseId;
        public int CaseVersion;
        public long CommittedTick;
        public string EvidenceEnvelopeHash;
        public List<string> RecognisedFactIds = new();
        public List<string> CitedEvidenceArtifactIds = new();
        public RulingDisposition Disposition;
        public string HoldingRuleId;
        public ScopeExpression Scope;
        public TemporalReach TemporalReach;
        public List<string> RemedyDefinitionIds = new();
        public List<string> AppliedProcedureIds = new();
        public string RulesetVersion;
    }
}
