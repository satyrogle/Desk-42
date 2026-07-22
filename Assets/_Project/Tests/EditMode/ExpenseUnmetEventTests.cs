using System.Linq;
using System.Reflection;
using Desk42.Core;
using NUnit.Framework;

namespace Desk42.Tests.EditMode
{
    [TestFixture]
    public sealed class ExpenseUnmetEventTests
    {
        [SetUp]
        public void SetUp()
        {
            SeedEngine.Init(12345);
            RumorMill.ClearAllSubscriptions();
            RumorMill.FlushQueue();
        }

        [TearDown]
        public void TearDown()
        {
            RumorMill.ClearAllSubscriptions();
            RumorMill.FlushQueue();
        }

        private static void DrainDeferredQueue()
        {
            var method = typeof(RumorMill).GetMethod(
                "DrainQueue", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method);
            method.Invoke(null, null);
        }

        private static RunData GenerateRun(int credits)
        {
            var runData = new RunData
            {
                ShiftNumber = 3,
                CorporateCredits = credits,
            };
            PersonalExpenseGenerator.GenerateForShift(
                runData, new MetaProgressData { GlobalShiftNumber = 3 });
            return runData;
        }

        [Test]
        public void GenerateForShift_RepeatedCallKeepsExactStoredLedger()
        {
            var runData = GenerateRun(100);
            string first = string.Join("|", runData.PersonalObligations
                .Select(x => $"{x.Id}:{x.Amount}"));

            // Advance the stream. An accidental redraw would now differ.
            SeedEngine.Next(SeedStream.PersonalExpenses, 0, 1000);
            PersonalExpenseGenerator.GenerateForShift(
                runData, new MetaProgressData { GlobalShiftNumber = 3 });

            string second = string.Join("|", runData.PersonalObligations
                .Select(x => $"{x.Id}:{x.Amount}"));
            Assert.AreEqual(first, second);
            Assert.AreEqual(3, runData.ObligationsShiftNumber);
            Assert.IsFalse(runData.ObligationsApplied);
        }

        [Test]
        public void InsufficientCredits_AppliesLedgerOnceAndPublishesShortfallsOnce()
        {
            var runData = GenerateRun(0);
            int expectedDebt = runData.PersonalObligations.Sum(x => x.Amount);
            int fireCount = 0;
            int reportedShortfall = 0;
            RumorMill.OnExpenseUnmet += e =>
            {
                fireCount++;
                reportedShortfall += e.AmountShort;
            };

            PersonalExpenseGenerator.ProcessEndOfShiftExpenses(runData);
            int debtAfterFirstApply = runData.PersonalExpenseDebt;
            PersonalExpenseGenerator.ProcessEndOfShiftExpenses(runData);
            DrainDeferredQueue();

            Assert.AreEqual(expectedDebt, debtAfterFirstApply);
            Assert.AreEqual(expectedDebt, runData.PersonalExpenseDebt);
            Assert.AreEqual(expectedDebt, reportedShortfall);
            Assert.AreEqual(runData.PersonalObligations.Count, fireCount);
            Assert.IsTrue(runData.ObligationsApplied);
            Assert.IsTrue(runData.PersonalObligations.All(x => x.Applied));
        }

        [Test]
        public void SufficientCredits_RecordsPaidLedgerWithoutDebt()
        {
            var runData = GenerateRun(100000);
            int due = runData.PersonalObligations.Sum(x => x.Amount);
            int creditsBefore = runData.CorporateCredits;
            int fireCount = 0;
            RumorMill.OnExpenseUnmet += _ => fireCount++;

            PersonalExpenseGenerator.ProcessEndOfShiftExpenses(runData);
            DrainDeferredQueue();

            Assert.AreEqual(creditsBefore - due, runData.CorporateCredits);
            Assert.AreEqual(0, runData.PersonalExpenseDebt);
            Assert.AreEqual(0, fireCount);
            Assert.IsTrue(runData.PersonalObligations.All(
                x => x.Applied && x.AmountPaid == x.Amount && x.AmountShort == 0));
        }

        [Test]
        public void MissingLedger_DoesNotGenerateOrChargeAtClockOut()
        {
            var runData = new RunData { CorporateCredits = 42, ShiftNumber = 1 };

            PersonalExpenseGenerator.ProcessEndOfShiftExpenses(runData);

            Assert.AreEqual(42, runData.CorporateCredits);
            Assert.IsEmpty(runData.PersonalObligations);
            Assert.IsFalse(runData.ObligationsApplied);
        }

        [Test]
        public void NullInputs_DoNotThrow()
        {
            Assert.DoesNotThrow(() =>
                PersonalExpenseGenerator.GenerateForShift(null, new MetaProgressData()));
            Assert.DoesNotThrow(() =>
                PersonalExpenseGenerator.ProcessEndOfShiftExpenses(null));
        }
    }
}
