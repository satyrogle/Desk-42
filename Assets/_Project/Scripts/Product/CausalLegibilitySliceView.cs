using System;
using System.Collections.Generic;
using Desk42.Institutional.Player;
using UnityEngine;

namespace Desk42.Product
{
    public enum CausalLegibilityPanel
    {
        Society,
        Docket,
        Evidence,
        Ruling,
        Consequences,
    }

    /// <summary>
    /// Text-first IMGUI surface for the legibility test. It renders immutable public
    /// records and emits validated player commands; it owns no simulation state.
    /// </summary>
    internal sealed class CausalLegibilitySliceView
    {
        private static readonly Color Background = new(0.055f, 0.067f, 0.075f);
        private static readonly Color Panel = new(0.10f, 0.12f, 0.13f);
        private static readonly Color Paper = new(0.86f, 0.84f, 0.73f);
        private static readonly Color Ink = new(0.10f, 0.11f, 0.10f);
        private static readonly Color Acid = new(0.70f, 0.88f, 0.30f);
        private static readonly Color Amber = new(0.95f, 0.63f, 0.20f);
        private static readonly Color Muted = new(0.60f, 0.65f, 0.65f);
        private static readonly Color Red = new(0.92f, 0.35f, 0.31f);

        private readonly Func<PlayerInstitutionView> _view;
        private readonly Func<PlayerScopeChoice, PlayerRulingDisposition,
            PlayerInstitutionView> _commit;
        private readonly Func<PlayerInstitutionView> _replay;
        private readonly Action _save;
        private readonly Action _load;
        private readonly Func<string> _status;

        private CausalLegibilityPanel _panel = CausalLegibilityPanel.Docket;
        private PlayerScopeChoice _scope = PlayerScopeChoice.Narrow;
        private PlayerRulingDisposition _disposition =
            PlayerRulingDisposition.Recognised;
        private bool _showTechnicalAttribution;
        private Vector2 _contentScroll;
        private Vector2 _navScroll;
        private GUIStyle _title;
        private GUIStyle _subtitle;
        private GUIStyle _body;
        private GUIStyle _small;
        private GUIStyle _paperBody;
        private GUIStyle _paperHeading;
        private GUIStyle _paperBox;
        private GUIStyle _badge;
        private GUIStyle _button;
        private GUIStyle _navButton;
        private GUIStyle _activeNavButton;

        internal CausalLegibilitySliceView(
            Func<PlayerInstitutionView> view,
            Func<PlayerScopeChoice, PlayerRulingDisposition, PlayerInstitutionView> commit,
            Func<PlayerInstitutionView> replay,
            Action save,
            Action load,
            Func<string> status)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _commit = commit ?? throw new ArgumentNullException(nameof(commit));
            _replay = replay ?? throw new ArgumentNullException(nameof(replay));
            _save = save ?? throw new ArgumentNullException(nameof(save));
            _load = load ?? throw new ArgumentNullException(nameof(load));
            _status = status ?? throw new ArgumentNullException(nameof(status));
        }

        internal void Draw()
        {
            EnsureStyles();
            Color previousBackground = GUI.backgroundColor;
            Color previousContent = GUI.contentColor;
            GUI.backgroundColor = Background;
            GUI.Box(new Rect(0f, 0f, Screen.width, Screen.height), GUIContent.none);
            GUI.backgroundColor = previousBackground;

            float margin = Mathf.Clamp(Screen.width * 0.018f, 14f, 28f);
            var frame = new Rect(
                margin,
                margin,
                Mathf.Max(640f, Screen.width - margin * 2f),
                Mathf.Max(420f, Screen.height - margin * 2f));
            GUILayout.BeginArea(frame);
            DrawHeader();
            GUILayout.Space(10f);
            GUILayout.BeginHorizontal();
            DrawNavigation();
            GUILayout.Space(12f);
            DrawCurrentPanel();
            GUILayout.EndHorizontal();
            GUILayout.Space(8f);
            DrawFooter();
            GUILayout.EndArea();

            GUI.backgroundColor = previousBackground;
            GUI.contentColor = previousContent;
        }

        internal void SelectPanel(CausalLegibilityPanel panel)
        {
            _panel = panel;
            _contentScroll = Vector2.zero;
        }

        private void DrawHeader()
        {
            PlayerInstitutionView current = _view();
            GUILayout.BeginHorizontal(GUILayout.Height(64f));
            GUILayout.BeginVertical();
            GUILayout.Label("DARK LATTICE / BRANCH 42", _title);
            GUILayout.Label(
                "CAUSAL LEGIBILITY SLICE 0.1  •  OFFICIAL RECORD ONLY",
                _small);
            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            GUILayout.BeginVertical(GUILayout.Width(250f));
            GUILayout.Label(
                $"CYCLE {current.CurrentCycle:00}   /   {current.Phase.ToString().ToUpperInvariant()}",
                _subtitle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("SAVE", _button, GUILayout.Height(26f)))
                TryAction(_save);
            if (GUILayout.Button("LOAD", _button, GUILayout.Height(26f)))
                TryAction(_load);
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        private void DrawNavigation()
        {
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(190f),
                GUILayout.ExpandHeight(true));
            GUILayout.Label("INSTITUTION", _small);
            _navScroll = GUILayout.BeginScrollView(_navScroll);
            NavButton(CausalLegibilityPanel.Society, "01  SOCIETY", "8 recognised people");
            NavButton(CausalLegibilityPanel.Docket, "02  DOCKET", "Why the case exists");
            NavButton(CausalLegibilityPanel.Evidence, "03  EVIDENCE", "Sources and limits");
            NavButton(CausalLegibilityPanel.Ruling, "04  RULING", "Disposition + scope");
            NavButton(
                CausalLegibilityPanel.Consequences,
                "05  CONSEQUENCES",
                "What followed officially");
            GUILayout.EndScrollView();
            GUILayout.FlexibleSpace();
            GUILayout.Label("NOT AVAILABLE", _small);
            GUILayout.Label("Authoritative truth\nPrivate beliefs\nRaw utility scores", _small);
            GUILayout.EndVertical();
        }

        private void NavButton(
            CausalLegibilityPanel panel,
            string label,
            string detail)
        {
            GUIStyle style = _panel == panel ? _activeNavButton : _navButton;
            if (GUILayout.Button(label + "\n" + detail, style, GUILayout.Height(58f)))
                SelectPanel(panel);
        }

        private void DrawCurrentPanel()
        {
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.ExpandWidth(true),
                GUILayout.ExpandHeight(true));
            _contentScroll = GUILayout.BeginScrollView(_contentScroll);
            switch (_panel)
            {
                case CausalLegibilityPanel.Society:
                    DrawSociety();
                    break;
                case CausalLegibilityPanel.Docket:
                    DrawDocket();
                    break;
                case CausalLegibilityPanel.Evidence:
                    DrawEvidence();
                    break;
                case CausalLegibilityPanel.Ruling:
                    DrawRuling();
                    break;
                case CausalLegibilityPanel.Consequences:
                    DrawConsequences();
                    break;
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        private void DrawSociety()
        {
            PlayerInstitutionView current = _view();
            SectionTitle("SOCIETY OBSERVATION", "What Branch 42 officially knows");
            for (int i = 0; i < current.Agents.Count; i++)
            {
                PublicAgentRecord agent = current.Agents[i];
                GUILayout.BeginHorizontal(GUI.skin.box, GUILayout.MinHeight(84f));
                GUILayout.Box(
                    Initials(agent.DisplayName),
                    _badge,
                    GUILayout.Width(58f),
                    GUILayout.Height(58f));
                GUILayout.BeginVertical();
                GUILayout.Label(agent.DisplayName.ToUpperInvariant(), _subtitle);
                GUILayout.Label(
                    $"{agent.OfficialIdentity}  •  {agent.RecognisedEmployer}",
                    _small);
                GUILayout.Label(
                    agent.ObservedActions.Count == 0
                        ? "No observed action in the current record."
                        : "Observed: " + string.Join("; ", agent.ObservedActions),
                    _body);
                if (agent.RecognisedStatuses.Count > 0)
                    GUILayout.Label(
                        "Standing: " + string.Join(", ", agent.RecognisedStatuses),
                        _small);
                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
            }
        }

        private void DrawDocket()
        {
            PlayerInstitutionView current = _view();
            SectionTitle("DOCKET", "A case is an institutional reconstruction, not truth");
            for (int i = 0; i < current.Cases.Count; i++)
            {
                PublicCaseRecord opened = current.Cases[i];
                Color previousBackground = GUI.backgroundColor;
                GUI.backgroundColor = Paper;
                GUILayout.BeginVertical(_paperBox, GUILayout.MinHeight(240f));
                GUI.backgroundColor = previousBackground;
                GUILayout.Label(opened.Issue.ToUpperInvariant(), _paperHeading);
                GUILayout.Label(
                    $"{opened.CaseId}  /  REV {opened.CaseRevision}  /  " +
                    $"DEADLINE CYCLE {opened.RulingDeadline}",
                    _paperBody);
                PaperRule();
                PaperList("PARTIES", opened.Parties);
                PaperList("ALLEGATIONS", opened.Allegations);
                PaperList("CONTESTED", opened.ContestedPropositions);
                PaperList("DOCKET BASIS", opened.DocketBasis);
                PaperList("MISSING EVIDENCE", opened.MissingEvidence);
                GUILayout.Label(
                    $"EVIDENCE SUPPORT RANGE  {opened.EvidenceSupportMinimum}–" +
                    $"{opened.EvidenceSupportMaximum} POINTS",
                    _paperHeading);
                GUILayout.Label(
                    "This is an institutional support score, not a probability of truth.",
                    _paperBody);
                if (!string.IsNullOrWhiteSpace(opened.ParentCaseId))
                    GUILayout.Label(
                        $"CONNECTED TO {opened.ParentCaseId} BY " +
                        $"{opened.OriginatingRulingId}",
                        _paperHeading);
                GUILayout.EndVertical();
                GUILayout.Space(10f);
            }
        }

        private void DrawEvidence()
        {
            PlayerInstitutionView current = _view();
            SectionTitle("EVIDENCE INSPECTOR", "Provenance, contradiction and limits");
            for (int i = 0; i < current.Evidence.Count; i++)
            {
                PublicEvidenceRecord evidence = current.Evidence[i];
                GUILayout.BeginVertical(GUI.skin.box, GUILayout.MinHeight(150f));
                GUILayout.BeginHorizontal();
                GUILayout.Label(evidence.Proposition.ToUpperInvariant(), _subtitle);
                GUILayout.FlexibleSpace();
                GUILayout.Label(
                    evidence.Citable ? "CITABLE" : "CONTEXT ONLY",
                    _badge,
                    GUILayout.Width(110f));
                GUILayout.EndHorizontal();
                GUILayout.Label(
                    $"SOURCE  {evidence.Source}    ENTERED  CYCLE {evidence.EnteredCycle}",
                    _small);
                GUILayout.Label("CUSTODY  " + evidence.ChainOfCustody, _body);
                GUILayout.Label(
                    $"SUPPORT  {evidence.ReliabilityScore} POINTS / " +
                    evidence.ReliabilityLabel,
                    _body);
                GUILayout.Label("STATUS  " + evidence.OfficialStatus, _small);
                if (evidence.KnownContradictions.Count > 0)
                    BulletList("KNOWN CONTRADICTION", evidence.KnownContradictions, Amber);
                if (evidence.LimitingConditions.Count > 0)
                    BulletList("LIMITING CONDITIONS", evidence.LimitingConditions, Muted);
                GUILayout.EndVertical();
                GUILayout.Space(8f);
            }
        }

        private void DrawRuling()
        {
            PlayerInstitutionView current = _view();
            SectionTitle(
                "RULING COMPOSER",
                "Two player choices inside a system-derived ruling");
            if (current.Rulings.Count > 0)
            {
                GUILayout.Label("THIS HISTORY ALREADY CONTAINS A RULING", _subtitle);
                GUILayout.Label(
                    "Return to the saved pre-ruling state to compare another doctrine.",
                    _body);
                if (GUILayout.Button(
                        "REPLAY FROM PRE-RULING SNAPSHOT",
                        _button,
                        GUILayout.Height(44f)))
                    TryAction(() => _replay());
                return;
            }

            DrawRulingStage("1 / SYSTEM-DERIVED FINDING",
                "The available issue and recorded possession change are recognised automatically.");

            DrawRulingStage("2 / PLAYER-SELECTED DISPOSITION", "What happens to this case?");
            GUILayout.BeginHorizontal();
            ChoiceButton(
                "RECOGNISED",
                _disposition == PlayerRulingDisposition.Recognised,
                () => SetDisposition(PlayerRulingDisposition.Recognised));
            ChoiceButton(
                "DENIED",
                _disposition == PlayerRulingDisposition.Denied,
                () => SetDisposition(PlayerRulingDisposition.Denied));
            GUILayout.EndHorizontal();

            DrawRulingStage(
                "3 / SYSTEM-DERIVED PROPOSED HOLDING",
                "Possession requires an authorised transfer.");

            DrawRulingStage("4 / PLAYER-SELECTED SCOPE", "Who and what does that rule bind?");
            GUILayout.BeginHorizontal();
            ChoiceButton(
                "NARROW / CLAIMANT",
                _scope == PlayerScopeChoice.Narrow,
                () => SetScope(PlayerScopeChoice.Narrow));
            ChoiceButton(
                "BROAD / BRANCH 42",
                _scope == PlayerScopeChoice.Broad,
                () => SetScope(PlayerScopeChoice.Broad));
            GUILayout.EndHorizontal();
            ScopeMatchPreview scope = FindScope(current, _scope);
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label(scope.Description, _body);
            GUILayout.Label(
                $"MATCHES NOW  {scope.MatchingCaseCount} CASE / " +
                $"{scope.PotentiallyCoveredAgentCount} PEOPLE / " +
                $"{scope.MatchingOrganisationCount} ORGANISATION",
                _subtitle);
            GUILayout.Label(string.Join(", ", scope.CurrentMatches), _small);
            GUILayout.Label(scope.FutureMatchNote, _small);
            GUILayout.EndVertical();

            DrawRulingStage("5 / FIXED TEMPORAL REACH", "Prospective only.");
            DrawRulingStage(
                "6 / DISPOSITION-REQUIRED REMEDY",
                _disposition == PlayerRulingDisposition.Denied
                    ? "No material change. The claim and holding are not established."
                    : "Return the resource to its registered owner and recognise the holding.");

            GUILayout.Space(10f);
            GUILayout.Label("DIRECT CONSEQUENCES ONLY", _small);
            GUILayout.Label(
                "The interface will not predict who acts, what they believe, " +
                "or whether another case appears.",
                _body);
            if (GUILayout.Button(
                    "SIGN AND COMMIT RULING",
                    _button,
                    GUILayout.Height(52f)))
            {
                TryAction(() => _commit(_scope, _disposition));
            }
        }

        private void DrawConsequences()
        {
            PlayerInstitutionView current = _view();
            SectionTitle("CONSEQUENCE LEDGER", "Public-safe chronology and attribution");
            for (int i = 0; i < current.Timeline.Count; i++)
            {
                PublicTimelineEntry entry = current.Timeline[i];
                GUILayout.BeginHorizontal(GUI.skin.box, GUILayout.MinHeight(78f));
                GUILayout.Label(
                    $"{entry.Cycle:00}",
                    _badge,
                    GUILayout.Width(44f),
                    GUILayout.Height(44f));
                GUILayout.BeginVertical();
                GUILayout.Label(
                    entry.Kind.ToString().ToUpperInvariant() + "  /  " + entry.Headline,
                    _subtitle);
                GUILayout.Label(entry.Detail, _body);
                string attribution = AttributionSummary(entry);
                if (!string.IsNullOrWhiteSpace(attribution))
                    GUILayout.Label(attribution, _small);
                if (_showTechnicalAttribution)
                {
                    string technical = TechnicalAttribution(entry);
                    if (!string.IsNullOrWhiteSpace(technical))
                        GUILayout.Label(technical, _small);
                }
                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
                GUILayout.Space(5f);
            }

            _showTechnicalAttribution = GUILayout.Toggle(
                _showTechnicalAttribution,
                "SHOW TECHNICAL TRACE IDS",
                _button,
                GUILayout.Height(32f));

            if (current.KnownDecisionPressures.Count > 0)
            {
                GUILayout.Space(12f);
                GUILayout.Label("KNOWN DECISION PRESSURES", _subtitle);
                GUILayout.Label(
                    "These are officially inferable conditions, not a claim about " +
                    "private motivation.",
                    _small);
                for (int i = 0; i < current.KnownDecisionPressures.Count; i++)
                {
                    KnownDecisionPressure pressure = current.KnownDecisionPressures[i];
                    GUILayout.Label("• " + pressure.Statement, _body);
                }
            }

            GUILayout.Space(12f);
            GUILayout.Label("WHAT REMAINS UNKNOWN", _subtitle);
            GUILayout.Label(current.UnknownsSummary, _body);
            if (current.Rulings.Count > 0 && GUILayout.Button(
                    "REPLAY FROM PRE-RULING SNAPSHOT",
                    _button,
                    GUILayout.Height(44f)))
                TryAction(() => _replay());
        }

        private void DrawFooter()
        {
            GUILayout.BeginHorizontal(GUI.skin.box, GUILayout.Height(32f));
            GUILayout.Label(_status(), _small);
            GUILayout.FlexibleSpace();
            GUILayout.Label("TRUTH ACCESS: DENIED", _small);
            GUILayout.EndHorizontal();
        }

        private void SectionTitle(string heading, string subheading)
        {
            GUILayout.Label(heading, _title);
            GUILayout.Label(subheading, _body);
            GUILayout.Space(10f);
        }

        private void DrawRulingStage(string heading, string detail)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label(heading, _subtitle);
            GUILayout.Label(detail, _body);
            GUILayout.EndVertical();
            GUILayout.Space(5f);
        }

        private void ChoiceButton(string label, bool selected, Action action)
        {
            Color previous = GUI.backgroundColor;
            GUI.backgroundColor = selected ? Acid : Panel;
            if (GUILayout.Button(label, _button, GUILayout.Height(38f))) action();
            GUI.backgroundColor = previous;
        }

        private void SetScope(PlayerScopeChoice scope)
        {
            _scope = scope;
        }

        private void SetDisposition(PlayerRulingDisposition disposition)
        {
            _disposition = disposition;
        }

        private void PaperList(string heading, IReadOnlyList<string> values)
        {
            GUILayout.Label(heading, _paperHeading);
            if (values.Count == 0)
            {
                GUILayout.Label("None recorded.", _paperBody);
                return;
            }
            for (int i = 0; i < values.Count; i++)
                GUILayout.Label("• " + values[i], _paperBody);
        }

        private void BulletList(
            string heading,
            IReadOnlyList<string> values,
            Color colour)
        {
            Color previous = GUI.contentColor;
            GUI.contentColor = colour;
            GUILayout.Label(heading, _small);
            for (int i = 0; i < values.Count; i++)
                GUILayout.Label("• " + values[i], _body);
            GUI.contentColor = previous;
        }

        private static void PaperRule()
        {
            GUILayout.Box(GUIContent.none, GUILayout.ExpandWidth(true), GUILayout.Height(1f));
        }

        private ScopeMatchPreview FindScope(
            PlayerInstitutionView current,
            PlayerScopeChoice choice)
        {
            for (int i = 0; i < current.ScopePreviews.Count; i++)
                if (current.ScopePreviews[i].Choice == choice)
                    return current.ScopePreviews[i];
            throw new InvalidOperationException("The selected scope has no public preview.");
        }

        private void TryAction(Action action)
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private static string AttributionSummary(PublicTimelineEntry entry)
        {
            if (!string.IsNullOrWhiteSpace(entry.ScopeMatchId))
                return "ATTRIBUTION  The committed holding was tested against this context.";
            if (!string.IsNullOrWhiteSpace(entry.OriginatingRulingId))
                return "ATTRIBUTION  This record descends from the committed ruling.";
            if (!string.IsNullOrWhiteSpace(entry.ImmediateCauseId))
                return "ATTRIBUTION  An earlier public record supports this entry.";
            return string.Empty;
        }

        private static string TechnicalAttribution(PublicTimelineEntry entry)
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(entry.ImmediateCauseId))
                parts.Add("CAUSE " + entry.ImmediateCauseId);
            if (!string.IsNullOrWhiteSpace(entry.OriginatingRulingId))
                parts.Add("RULING " + entry.OriginatingRulingId);
            if (!string.IsNullOrWhiteSpace(entry.ScopeMatchId))
                parts.Add("SCOPE " + entry.ScopeMatchId);
            return string.Join("  /  ", parts);
        }

        private static string Initials(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName)) return "?";
            string[] words = displayName.Split(' ');
            string result = string.Empty;
            for (int i = 0; i < words.Length && result.Length < 2; i++)
                if (words[i].Length > 0)
                    result += char.ToUpperInvariant(words[i][0]);
            return result;
        }

        private void EnsureStyles()
        {
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Acid },
                wordWrap = true,
            };
            _subtitle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white },
                wordWrap = true,
            };
            _body = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                normal = { textColor = new Color(0.86f, 0.88f, 0.86f) },
                wordWrap = true,
            };
            _small = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                normal = { textColor = Muted },
                wordWrap = true,
            };
            _paperBody = new GUIStyle(_body)
            {
                normal = { textColor = Ink },
            };
            _paperHeading = new GUIStyle(_subtitle)
            {
                normal = { textColor = Ink },
            };
            _paperBox = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(14, 14, 12, 12),
            };
            _paperBox.normal.background = Texture2D.whiteTexture;
            _badge = new GUIStyle(GUI.skin.box)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Acid },
            };
            _button = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white },
            };
            _navButton = new GUIStyle(_button)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 11,
                wordWrap = true,
            };
            _activeNavButton = new GUIStyle(_navButton)
            {
                normal = { textColor = Acid },
            };
        }
    }
}
