using System;
using System.Collections.Generic;
using Desk42.Institutional;
using NUnit.Framework;

namespace Desk42.Tests.EditMode
{
    public sealed class InstitutionalCaseFactTests
    {
        [Test]
        public void CaseFact_EqualityAndOrderingUseOrdinalKeyValueSemantics()
        {
            var exact = new CaseFact("Attribute", "Exact-Value");
            var equal = new CaseFact("Attribute", "Exact-Value");
            var differentKeyCase = new CaseFact("attribute", "Exact-Value");
            var differentValueCase = new CaseFact("Attribute", "exact-value");

            Assert.AreEqual(exact, equal);
            Assert.AreEqual(exact.GetHashCode(), equal.GetHashCode());
            Assert.AreNotEqual(exact, differentKeyCase);
            Assert.AreNotEqual(exact, differentValueCase);
            Assert.AreNotEqual(0, exact.CompareTo(differentKeyCase));
            Assert.AreNotEqual(0, exact.CompareTo(differentValueCase));
        }

        [TestCase(null, "value")]
        [TestCase("", "value")]
        [TestCase("   ", "value")]
        [TestCase("key", null)]
        [TestCase("key", "")]
        [TestCase("key", "\t")]
        public void CaseFact_RejectsBlankKeysAndValues(string key, string value)
        {
            Assert.Throws<InvalidOperationException>(() => new CaseFact(key, value));
        }

        [Test]
        public void CaseFactSet_ConstructionAndCopyAreDetachedAndDeterministicallyOrdered()
        {
            var later = new CaseFact("zeta", "2");
            var earlier = new CaseFact("alpha", "1");
            var source = new List<CaseFact> { later, earlier };

            var facts = new CaseFactSet(source);

            CollectionAssert.AreEqual(new[] { "alpha", "zeta" },
                facts.Facts.ConvertAll(fact => fact.Key));
            Assert.AreNotSame(earlier, facts.Facts[0]);
            Assert.AreNotSame(later, facts.Facts[1]);

            earlier.Value = "mutated-source";
            Assert.AreEqual("1", facts.Facts[0].Value);

            CaseFactSet copy = facts.Copy();
            Assert.AreNotSame(facts.Facts, copy.Facts);
            Assert.AreNotSame(facts.Facts[0], copy.Facts[0]);

            facts.Facts[0].Value = "mutated-original";
            Assert.AreEqual("1", copy.Facts[0].Value);
        }

        [Test]
        public void CaseFactSet_RejectsDuplicateKeysButTreatsKeyCaseAsDistinct()
        {
            Assert.Throws<InvalidOperationException>(() => new CaseFactSet(new[]
            {
                new CaseFact("region", "north"),
                new CaseFact("region", "south"),
            }));

            var ordinallyDistinct = new CaseFactSet(new[]
            {
                new CaseFact("region", "north"),
                new CaseFact("Region", "south"),
            });
            Assert.AreEqual(2, ordinallyDistinct.Count);
        }

        [Test]
        public void PrecedentScope_RequiredFactsUseAllOfMatching()
        {
            var scope = new PrecedentScope
            {
                RequiredFacts = new CaseFactSet(new[]
                {
                    new CaseFact("category", "type-a"),
                    new CaseFact("region", "north"),
                }),
            };

            Assert.IsTrue(scope.AppliesTo(new CaseFactSet(new[]
            {
                new CaseFact("region", "north"),
                new CaseFact("category", "type-a"),
                new CaseFact("extra", "allowed"),
            })));
            Assert.IsFalse(scope.AppliesTo(new CaseFactSet(new[]
            {
                new CaseFact("region", "north"),
            })));
            Assert.IsFalse(scope.AppliesTo(new CaseFactSet(new[]
            {
                new CaseFact("region", "North"),
                new CaseFact("category", "type-a"),
            })));
        }

        [Test]
        public void PrecedentScope_LegacyOverloadPreservesExistingReachBehavior()
        {
            var individual = new PrecedentScope
            {
                Reach = PrecedentReach.Individual,
                BoundAgentId = "agent-a",
                BoundEmployerId = "employer-a",
                IdentityConditionId = "condition-a",
                RequiredFacts = new CaseFactSet(new[] { new CaseFact("generic", "required") }),
            };
            Assert.IsTrue(individual.AppliesTo("agent-a", "other-employer", "condition-a"));
            Assert.IsFalse(individual.AppliesTo("agent-b", "employer-a", "condition-a"));

            var employer = new PrecedentScope
            {
                Reach = PrecedentReach.Employer,
                BoundEmployerId = "employer-a",
                IdentityConditionId = "condition-a",
            };
            Assert.IsTrue(employer.AppliesTo("agent-b", "employer-a", "condition-a"));
            Assert.IsFalse(employer.AppliesTo("agent-b", "employer-b", "condition-a"));
            Assert.IsFalse(employer.AppliesTo("agent-b", "employer-a", "condition-b"));

            var jurisdiction = new PrecedentScope
            {
                Reach = PrecedentReach.Jurisdiction,
                IdentityConditionId = "condition-a",
            };
            Assert.IsTrue(jurisdiction.AppliesTo("any-agent", "any-employer", "condition-a"));
            Assert.IsFalse(jurisdiction.AppliesTo("any-agent", "any-employer", "condition-b"));
        }

        [Test]
        public void PrecedentScope_CombinedOverloadRequiresLegacyAndGenericDimensions()
        {
            var scope = new PrecedentScope
            {
                Reach = PrecedentReach.Employer,
                BoundEmployerId = "employer-a",
                IdentityConditionId = "condition-a",
                RequiredFacts = new CaseFactSet(new[]
                {
                    new CaseFact("region", "north"),
                }),
            };

            var matchingFacts = new CaseFactSet(new[] { new CaseFact("region", "north") });
            var wrongFacts = new CaseFactSet(new[] { new CaseFact("region", "south") });

            Assert.IsTrue(scope.AppliesTo("agent-a", "employer-a", "condition-a", matchingFacts));
            Assert.IsFalse(scope.AppliesTo("agent-a", "employer-b", "condition-a", matchingFacts));
            Assert.IsFalse(scope.AppliesTo("agent-a", "employer-a", "condition-a", wrongFacts));
        }

        [Test]
        public void FactTypesAreSerializable()
        {
            Assert.IsTrue(typeof(CaseFact).IsSerializable);
            Assert.IsTrue(typeof(CaseFactSet).IsSerializable);
        }
    }
}
