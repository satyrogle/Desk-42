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
            bool refundFile)
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
            IEnumerable<OfficeCaseWorkDefinition> work)
        {
            InstitutionalSession = institutionalSession ??
                throw new ArgumentNullException(nameof(institutionalSession));
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
            OfficeCaseRepository cases = OfficeCaseProjector.FromClaims(session.Claims);
            string[] names =
            {
                "NIA BELL", "OWEN PIKE", "MARA VALE",
                "IRIS COLE", "TOMAS REED", "JUNE HART",
            };
            string[] problems =
            {
                "MY REFUND ARRIVED YESTERDAY, BUT MY ACCOUNT STILL SAYS NO.",
                "THE SHOP SENT MY REFUND. I NEED IT PUT BACK IN MY ACCOUNT.",
                "THE COPIER MADE ANOTHER REFUND FILE AND NOW BOTH LOOK REAL.",
                "MY ACCESS CARD STOPPED WORKING AFTER I CHANGED DESKS.",
                "MY PAY RECORD HAS MY NAME, BUT THE ACCOUNT MARK IS WRONG.",
                "MY REFUND PAPERS LOOK RIGHT. PLEASE SEND THE MONEY TO MY ACCOUNT.",
            };
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
            return new OfficeM2Scenario(session, cases, customers, work);
        }
    }
}
