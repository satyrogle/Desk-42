// ============================================================
// DESK 42 — ExpenseUnmetEvent Unit Tests (Edit Mode)
//
// Real publisher: PersonalExpenseGenerator.ProcessEndOfShiftExpenses
// (Desk42.Core). It draws 2-4 expenses via SeedEngine and, for any
// expense the player can't cover, publishes ExpenseUnmetEvent via
// RumorMill.PublishDeferred and accumulates RunData.PersonalExpenseDebt.
//
// RumorMill.DrainQueue() is internal to Desk42.Core (no
// InternalsVisibleTo to the test assembly), so deferred dispatch is
// driven here via reflection — the same white-box pattern already
// used in BSMTests.cs — rather than changing runtime visibility.
// ============================================================

using System.Reflection;
using NUnit.Framework;
using Desk42.Core;

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
            var method = typeof(RumorMill).GetMethod("DrainQueue", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method, "RumorMill.DrainQueue() not found — has it been renamed?");
            method.Invoke(null, null);
        }

        [Test]
        public void InsufficientCredits_PublishesExpenseUnmetEvent_AndAccumulatesDebt()
        {
            var runData = new RunData { CorporateCredits = 0 };
            var meta = new MetaProgressData { GlobalShiftNumber = 1 };

            int fireCount = 0;
            int lastAmountShort = 0;
            RumorMill.OnExpenseUnmet += e => { fireCount++; lastAmountShort = e.AmountShort; };

            PersonalExpenseGenerator.ProcessEndOfShiftExpenses(runData, meta);
            DrainDeferredQueue();

            Assert.Greater(fireCount, 0,
                "ExpenseUnmetEvent should fire when CorporateCredits cannot cover a drawn expense.");
            Assert.Greater(runData.PersonalExpenseDebt, 0,
                "PersonalExpenseDebt should accumulate for every unmet expense.");
            Assert.Greater(lastAmountShort, 0);
        }

        [Test]
        public void SufficientCredits_NeverPublishesExpenseUnmetEvent()
        {
            var runData = new RunData { CorporateCredits = 100000 };
            var meta = new MetaProgressData { GlobalShiftNumber = 1 };

            int fireCount = 0;
            RumorMill.OnExpenseUnmet += _ => fireCount++;

            PersonalExpenseGenerator.ProcessEndOfShiftExpenses(runData, meta);
            DrainDeferredQueue();

            Assert.AreEqual(0, fireCount);
            Assert.AreEqual(0, runData.PersonalExpenseDebt);
        }

        [Test]
        public void NullRunData_DoesNotThrow()
        {
            Assert.DoesNotThrow(() =>
                PersonalExpenseGenerator.ProcessEndOfShiftExpenses(null, new MetaProgressData()));
        }

        [Test]
        public void NullMeta_DoesNotThrow()
        {
            Assert.DoesNotThrow(() =>
                PersonalExpenseGenerator.ProcessEndOfShiftExpenses(new RunData(), null));
        }
    }
}
