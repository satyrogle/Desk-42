using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Desk42.Institutional.Player;

namespace Desk42.Product.OfficeSlice
{
    public enum OfficePaperEntry
    {
        CustomerName,
        PaymentDate,
        AccountMark,
        PapersMatch,
    }

    public enum OfficeMoneyResult
    {
        MoneyFound,
        MoneyMoved,
        MoneyMissing,
    }

    public sealed class OfficeCustomerDefinition
    {
        public OfficeCustomerDefinition(
            string customerId,
            string displayName,
            string linkedAutomationClaimId,
            long arrivalTick,
            string problem,
            string authoredOfficeTraitId)
        {
            CustomerId = Require(customerId, nameof(customerId));
            DisplayName = Require(displayName, nameof(displayName));
            LinkedAutomationClaimId = Require(
                linkedAutomationClaimId, nameof(linkedAutomationClaimId));
            if (arrivalTick < 0L) throw new ArgumentOutOfRangeException(nameof(arrivalTick));
            ArrivalTick = arrivalTick;
            Problem = Require(problem, nameof(problem));
            AuthoredOfficeTraitId = Require(
                authoredOfficeTraitId, nameof(authoredOfficeTraitId));
        }

        public string CustomerId { get; }
        public string DisplayName { get; }
        public string LinkedAutomationClaimId { get; }
        public long ArrivalTick { get; }
        public string Problem { get; }
        public string AuthoredOfficeTraitId { get; }

        private static string Require(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("A stable authored value is required.", parameterName);
            return value;
        }
    }

    public sealed class OfficeCaseWorkDefinition
    {
        public OfficeCaseWorkDefinition(
            string automationClaimId,
            string customerNameOnPaper,
            string paymentDateOnPaper,
            string accountMarkOnPaper,
            OfficePaperEntry paperAnswer,
            int moneyPathAnswer,
            OfficeMoneyResult moneyResult,
            string moneyPathSummary,
            bool refundFile,
            bool badgeActive = false,
            bool shiftLogMatches = false,
            IReadOnlyList<OfficeManualTaskKind> requiredSequence = null,
            int weirdChoiceAnswer = 0,
            string weirdResult = "WEIRD STUFF CHECKED",
            string priorObservableRecord = "")
        {
            AutomationClaimId = Require(automationClaimId, nameof(automationClaimId));
            CustomerNameOnPaper = Require(
                customerNameOnPaper, nameof(customerNameOnPaper));
            PaymentDateOnPaper = Require(
                paymentDateOnPaper, nameof(paymentDateOnPaper));
            AccountMarkOnPaper = Require(
                accountMarkOnPaper, nameof(accountMarkOnPaper));
            if (moneyPathAnswer < 0 || moneyPathAnswer > 2)
                throw new ArgumentOutOfRangeException(nameof(moneyPathAnswer));
            PaperAnswer = paperAnswer;
            MoneyPathAnswer = moneyPathAnswer;
            MoneyResult = moneyResult;
            MoneyPathSummary = Require(moneyPathSummary, nameof(moneyPathSummary));
            RefundFile = refundFile;
            PublicBadgeActive = badgeActive;
            PublicShiftLogMatches = shiftLogMatches;
            if (weirdChoiceAnswer < 0 || weirdChoiceAnswer > 3)
                throw new ArgumentOutOfRangeException(nameof(weirdChoiceAnswer));
            WeirdChoiceAnswer = weirdChoiceAnswer;
            WeirdResult = Require(weirdResult, nameof(weirdResult));
            PriorObservableRecord = priorObservableRecord ?? string.Empty;
            var sequence = requiredSequence == null
                ? new List<OfficeManualTaskKind>
                {
                    OfficeManualTaskKind.Compare,
                    OfficeManualTaskKind.Trace,
                }
                : new List<OfficeManualTaskKind>(requiredSequence);
            if (sequence.Count < 2 || sequence.Count > 3 ||
                !sequence.Contains(OfficeManualTaskKind.Compare) ||
                !sequence.Contains(OfficeManualTaskKind.Trace))
                throw new ArgumentException(
                    "Work sequence must contain Paper and Money once.",
                    nameof(requiredSequence));
            var unique = new HashSet<OfficeManualTaskKind>();
            for (int i = 0; i < sequence.Count; i++)
                if (!unique.Add(sequence[i]))
                    throw new ArgumentException(
                        "Work sequence cannot repeat a room task.",
                        nameof(requiredSequence));
            RequiredSequence = new ReadOnlyCollection<OfficeManualTaskKind>(sequence);
        }

        public string AutomationClaimId { get; }
        public string CustomerNameOnPaper { get; }
        public string PaymentDateOnPaper { get; }
        public string AccountMarkOnPaper { get; }
        public OfficePaperEntry PaperAnswer { get; }
        public int MoneyPathAnswer { get; }
        public OfficeMoneyResult MoneyResult { get; }
        public string MoneyPathSummary { get; }
        public bool RefundFile { get; }
        public bool PublicBadgeActive { get; }
        public bool PublicShiftLogMatches { get; }
        public IReadOnlyList<OfficeManualTaskKind> RequiredSequence { get; }
        public int WeirdChoiceAnswer { get; }
        public string WeirdResult { get; }
        public string PriorObservableRecord { get; }
        public bool PublicPapersMatch => PaperAnswer == OfficePaperEntry.PapersMatch;
        public bool PublicRefundPathClear => MoneyResult == OfficeMoneyResult.MoneyFound;
        public string PaperResult => PaperAnswer == OfficePaperEntry.PapersMatch
            ? "THE PAPERS MATCH"
            : "THE PAPERS DON'T MATCH";
        public string MoneyResultLabel => MoneyResult switch
        {
            OfficeMoneyResult.MoneyFound => "MONEY FOUND",
            OfficeMoneyResult.MoneyMoved => "MONEY MOVED",
            _ => "MONEY MISSING",
        };

        private static string Require(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("A public work value is required.", parameterName);
            return value;
        }
    }

    /// <summary>
    /// Product-owned M2 authored layer. It binds plain-language customers and work
    /// to public claims without creating or inspecting institutional truth.
    /// </summary>
    public sealed class OfficeM2Scenario
    {
        private readonly Dictionary<string, OfficeCaseWorkDefinition> _workByClaim;

        private OfficeM2Scenario(
            InstitutionalAutomationSession institutionalSession,
            OfficeCaseRepository cases,
            IEnumerable<OfficeCustomerDefinition> customers,
            IEnumerable<OfficeCaseWorkDefinition> work,
            int shiftOrdinal)
        {
            if (shiftOrdinal < 1 || shiftOrdinal > 3)
                throw new ArgumentOutOfRangeException(nameof(shiftOrdinal));
            InstitutionalSession = institutionalSession ??
                throw new ArgumentNullException(nameof(institutionalSession));
            ShiftOrdinal = shiftOrdinal;
            Cases = cases ?? throw new ArgumentNullException(nameof(cases));
            Customers = new ReadOnlyCollection<OfficeCustomerDefinition>(
                new List<OfficeCustomerDefinition>(customers ??
                    throw new ArgumentNullException(nameof(customers))));
            _workByClaim = new Dictionary<string, OfficeCaseWorkDefinition>(
                StringComparer.Ordinal);
            foreach (OfficeCaseWorkDefinition definition in work ??
                throw new ArgumentNullException(nameof(work)))
                _workByClaim.Add(definition.AutomationClaimId, definition);

            if (Customers.Count != 6 || Cases.Cases.Count != 6 || _workByClaim.Count != 6)
                throw new InvalidOperationException("M2 requires exactly six authored customers.");
            for (int i = 0; i < Customers.Count; i++)
            {
                string claimId = Customers[i].LinkedAutomationClaimId;
                if (Cases.Get(claimId) == null || !_workByClaim.ContainsKey(claimId))
                    throw new InvalidOperationException(
                        "Every customer must bind to one public office case.");
            }
        }

        public InstitutionalAutomationSession InstitutionalSession { get; }
        public int ShiftOrdinal { get; }
        public OfficeCaseRepository Cases { get; }
        public IReadOnlyList<OfficeCustomerDefinition> Customers { get; }

        public OfficeCaseWorkDefinition WorkFor(string automationClaimId)
        {
            if (string.IsNullOrWhiteSpace(automationClaimId)) return null;
            _workByClaim.TryGetValue(automationClaimId, out OfficeCaseWorkDefinition value);
            return value;
        }

        public OfficeCustomerDefinition CustomerForClaim(string automationClaimId)
        {
            for (int i = 0; i < Customers.Count; i++)
                if (string.Equals(Customers[i].LinkedAutomationClaimId,
                        automationClaimId, StringComparison.Ordinal))
                    return Customers[i];
            return null;
        }

        public static OfficeM2Scenario Create()
        {
            InstitutionalAutomationSession session =
                InstitutionalAutomationSession.Create(6);
            return CreateForCampaign(session, 1);
        }

        public static OfficeM2Scenario CreateForCampaign(
            InstitutionalAutomationSession session,
            int shiftOrdinal,
            IReadOnlyList<OfficeCampaignDecisionCallback> priorDecisions = null)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (shiftOrdinal < 1 || shiftOrdinal > 3)
                throw new ArgumentOutOfRangeException(nameof(shiftOrdinal));
            if (session.Claims == null || session.Claims.Count != 6)
                throw new InvalidOperationException(
                    "A campaign shift requires exactly six released public claims.");
            OfficeCaseRepository cases = OfficeCaseProjector.FromClaims(session.Claims);
            string[] names =
            {
                "NIA BELL", "OWEN PIKE", "MARA VALE",
                "IRIS COLE", "TOMAS REED", "JUNE HART",
            };
            string[] problems = ProblemsForShift(shiftOrdinal);
            string[] traitIds =
            {
                "trait.keeps-receipts", "trait.speaks-plainly",
                "trait.watches-copier", "trait.arrives-early",
                "trait.checks-every-line", "trait.brings-bank-note",
            };
            long[] arrivals = { 0L, 270L, 540L, 810L, 1080L, 1350L };
            var customers = new List<OfficeCustomerDefinition>(6);
            var work = new List<OfficeCaseWorkDefinition>(6);
            for (int i = 0; i < cases.Cases.Count; i++)
            {
                OfficeCase officeCase = cases.Cases[i];
                customers.Add(new OfficeCustomerDefinition(
                    "customer.m2." + (i + 1).ToString("D2"),
                    names[i],
                    officeCase.AutomationClaimId,
                    arrivals[i],
                    problems[i],
                    traitIds[i]));
            }

            AddWorkForShift(work, cases, shiftOrdinal, priorDecisions);
            return new OfficeM2Scenario(
                session, cases, customers, work, shiftOrdinal);
        }

        private static string[] ProblemsForShift(int shiftOrdinal)
        {
            if (shiftOrdinal == 1)
                return new[]
                {
                    "MY REFUND ARRIVED YESTERDAY, BUT MY ACCOUNT STILL SAYS NO.",
                    "THE SHOP SENT MY REFUND. I NEED IT PUT BACK IN MY ACCOUNT.",
                    "THE COPIER MADE ANOTHER REFUND FILE AND NOW BOTH LOOK REAL.",
                    "MY ACCESS CARD STOPPED WORKING AFTER I CHANGED DESKS.",
                    "MY PAY RECORD HAS MY NAME, BUT THE ACCOUNT MARK IS WRONG.",
                    "MY REFUND PAPERS LOOK RIGHT. PLEASE SEND THE MONEY TO MY ACCOUNT.",
                };
            if (shiftOrdinal == 2)
                return new[]
                {
                    "YESTERDAY IS NOW CHARGING INTEREST.",
                    "MY SHIFT WAS PAID TO SOMEONE WHO CLOCKED IN BEFORE I ARRIVED.",
                    "THE COPIER MADE A STAFF BADGE WITH MY FACE AND SOMEONE ELSE'S NUMBER.",
                    "MY OLD ACCESS CARD OPENS A ROOM THAT NO LONGER EXISTS.",
                    "I DIED ON FRIDAY. PAYROLL SAYS I WORKED SATURDAY.",
                    "MY BADGE AND SHIFT LOG MATCH. MY PAY STILL DIDN'T ARRIVE.",
                };
            return new[]
            {
                "THE REFUND SYSTEM SENT ME A RECEIPT FOR TOMORROW.",
                "THE NEW SUPERVISOR MOVED MY CASE WITHOUT ASKING.",
                "THE COPIER HAS MY BADGE, TOMAS'S HOURS, AND A MANAGER'S STAMP.",
                "THE MISSING ROOM IS NOW LISTED AS A DEPARTMENT.",
                "PAYROLL PROMOTED THE MACHINE ABOVE ME.",
                "MY PAY IS CORRECT, BUT THE APPROVAL CAME FROM THE COPIER.",
            };
        }

        private static void AddWorkForShift(
            List<OfficeCaseWorkDefinition> work,
            OfficeCaseRepository cases,
            int shiftOrdinal,
            IReadOnlyList<OfficeCampaignDecisionCallback> priorDecisions)
        {
            if (shiftOrdinal == 1)
            {
                work.Add(new OfficeCaseWorkDefinition(cases.Cases[0].AutomationClaimId,
                    "NIA BELL", "TODAY / RECEIPT SAYS YESTERDAY", "REFUND READY",
                    OfficePaperEntry.PaymentDate, 1, OfficeMoneyResult.MoneyMoved,
                    "COMPANY > PAYMENT RECORD > HOLDING ACCOUNT", true));
                work.Add(new OfficeCaseWorkDefinition(cases.Cases[1].AutomationClaimId,
                    "OWEN PIKE", "YESTERDAY", "REFUND READY",
                    OfficePaperEntry.PapersMatch, 0, OfficeMoneyResult.MoneyFound,
                    "COMPANY > PAYMENT RECORD > CUSTOMER ACCOUNT", true));
                work.Add(new OfficeCaseWorkDefinition(cases.Cases[2].AutomationClaimId,
                    "MARA VALE", "YESTERDAY", "REFUND READY",
                    OfficePaperEntry.PapersMatch, 0, OfficeMoneyResult.MoneyFound,
                    "COMPANY > PAYMENT RECORD > CUSTOMER ACCOUNT", true));
                work.Add(new OfficeCaseWorkDefinition(cases.Cases[3].AutomationClaimId,
                    "IRIS COLE", "MONDAY", "OLD DESK MARK",
                    OfficePaperEntry.AccountMark, 1, OfficeMoneyResult.MoneyMoved,
                    "COMPANY > ACCESS RECORD > OLD DESK", false));
                work.Add(new OfficeCaseWorkDefinition(cases.Cases[4].AutomationClaimId,
                    "TOMAS REED", "FRIDAY", "ACCOUNT 14 / FILE SAYS 41",
                    OfficePaperEntry.AccountMark, 0, OfficeMoneyResult.MoneyFound,
                    "COMPANY > PAY RECORD > ACCOUNT 14", false));
                work.Add(new OfficeCaseWorkDefinition(cases.Cases[5].AutomationClaimId,
                    "JUNE HART", "THURSDAY", "REFUND READY",
                    OfficePaperEntry.PapersMatch, 0, OfficeMoneyResult.MoneyFound,
                    "COMPANY > PAYMENT RECORD > CUSTOMER ACCOUNT", true));
                return;
            }

            OfficePaperEntry[] paperAnswers = shiftOrdinal == 2
                ? new[]
                {
                    OfficePaperEntry.PaymentDate, OfficePaperEntry.PapersMatch,
                    OfficePaperEntry.AccountMark, OfficePaperEntry.AccountMark,
                    OfficePaperEntry.PaymentDate, OfficePaperEntry.PapersMatch,
                }
                : new[]
                {
                    OfficePaperEntry.PaymentDate, OfficePaperEntry.AccountMark,
                    OfficePaperEntry.AccountMark, OfficePaperEntry.PapersMatch,
                    OfficePaperEntry.AccountMark, OfficePaperEntry.PapersMatch,
                };
            OfficeMoneyResult[] moneyResults = shiftOrdinal == 2
                ? new[]
                {
                    OfficeMoneyResult.MoneyMoved, OfficeMoneyResult.MoneyFound,
                    OfficeMoneyResult.MoneyMoved, OfficeMoneyResult.MoneyMissing,
                    OfficeMoneyResult.MoneyMoved, OfficeMoneyResult.MoneyFound,
                }
                : new[]
                {
                    OfficeMoneyResult.MoneyMissing, OfficeMoneyResult.MoneyMoved,
                    OfficeMoneyResult.MoneyMoved, OfficeMoneyResult.MoneyFound,
                    OfficeMoneyResult.MoneyMoved, OfficeMoneyResult.MoneyFound,
                };
            int[] moneyAnswers = { 1, 0, 1, 2, 1, 0 };
            string[] customerNames =
            {
                "NIA BELL", "OWEN PIKE", "MARA VALE",
                "IRIS COLE", "TOMAS REED", "JUNE HART",
            };
            for (int i = 0; i < cases.Cases.Count; i++)
            {
                bool payroll = i == 1 || i == 4 || i == 5;
                IReadOnlyList<OfficeManualTaskKind> sequence =
                    SequenceFor(shiftOrdinal, i);
                string path = moneyResults[i] == OfficeMoneyResult.MoneyFound
                    ? "COMPANY > PAYMENT RECORD > CUSTOMER ACCOUNT"
                    : moneyResults[i] == OfficeMoneyResult.MoneyMoved
                        ? "COMPANY > PAYMENT RECORD > HOLDING ACCOUNT"
                        : "COPIED FILE > NO PAYMENT RECORD > NO ACCOUNT";
                work.Add(new OfficeCaseWorkDefinition(
                    cases.Cases[i].AutomationClaimId,
                    customerNameOnPaper: customerNames[i],
                    paymentDateOnPaper: shiftOrdinal == 2 ? "SATURDAY" : "TOMORROW",
                    accountMarkOnPaper: payroll ? "BADGE ACTIVE" : "OFFICE RECORD",
                    paperAnswer: paperAnswers[i],
                    moneyPathAnswer: moneyAnswers[i],
                    moneyResult: moneyResults[i],
                    moneyPathSummary: path,
                    refundFile: !payroll && i == 0,
                    badgeActive: payroll,
                    shiftLogMatches: payroll,
                    requiredSequence: sequence,
                    weirdChoiceAnswer: i % 3,
                    weirdResult: i == 3
                        ? "MISSING ROOM RECORD FOUND"
                        : i == 4
                            ? "CLOCK RECORD DOES NOT MATCH THE PERSON"
                            : "COPIED OFFICE MARK FOUND",
                    priorObservableRecord: shiftOrdinal == 3 && i == 0
                        ? PriorDecisionRecord(
                            priorDecisions, 1, "customer.m2.01")
                        : shiftOrdinal == 3 && i == 1
                            ? PriorDecisionRecord(
                                priorDecisions, 2, "customer.m2.02")
                            : string.Empty));
            }
        }

        private static string PriorDecisionRecord(
            IReadOnlyList<OfficeCampaignDecisionCallback> priorDecisions,
            int shiftOrdinal,
            string customerId)
        {
            if (priorDecisions == null) return string.Empty;
            for (int i = 0; i < priorDecisions.Count; i++)
            {
                OfficeCampaignDecisionCallback callback = priorDecisions[i];
                if (callback.ShiftOrdinal == shiftOrdinal &&
                    string.Equals(callback.CustomerId, customerId,
                        StringComparison.Ordinal))
                    return "SHIFT " + shiftOrdinal + " RECORD / " +
                        callback.Stamp;
            }
            return string.Empty;
        }

        private static IReadOnlyList<OfficeManualTaskKind> SequenceFor(
            int shiftOrdinal,
            int customerIndex)
        {
            if (shiftOrdinal == 1 ||
                (shiftOrdinal == 2 && (customerIndex == 1 || customerIndex == 5)))
                return new[]
                {
                    OfficeManualTaskKind.Compare,
                    OfficeManualTaskKind.Trace,
                };
            if (shiftOrdinal == 2 && (customerIndex == 0 || customerIndex == 3))
                return new[]
                {
                    OfficeManualTaskKind.WeirdCheck,
                    OfficeManualTaskKind.Compare,
                    OfficeManualTaskKind.Trace,
                };
            if (shiftOrdinal == 2 && customerIndex == 2)
                return new[]
                {
                    OfficeManualTaskKind.Compare,
                    OfficeManualTaskKind.WeirdCheck,
                    OfficeManualTaskKind.Trace,
                };
            return new[]
            {
                OfficeManualTaskKind.Compare,
                OfficeManualTaskKind.Trace,
                OfficeManualTaskKind.WeirdCheck,
            };
        }
    }
}
