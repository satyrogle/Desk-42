using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Desk42.Institutional;
using Desk42.Institutional.Scenarios;
using NUnit.Framework;

namespace Desk42.Tests.EditMode
{
    public sealed class InstitutionalScenarioAssemblyBoundaryTests
    {
        private const string ScenarioAssemblyName = "Desk42.Institutional.Scenarios";

        [Test]
        public void ScenarioAssembly_ReferencesOnlyApprovedInstitutionalContracts()
        {
            Assembly assembly = typeof(ScenarioAssemblyBoundary).Assembly;
            string[] institutionalReferences = assembly.GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .Where(name => name.StartsWith("Desk42.Institutional", StringComparison.Ordinal))
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

            Assert.That(assembly.GetName().Name, Is.EqualTo(ScenarioAssemblyName));
            Assert.That(institutionalReferences, Is.EqualTo(new[]
            {
                "Desk42.Institutional.Domain",
            }));
            Assert.That(ScenarioAssemblyBoundary.DefinitionContract,
                Is.EqualTo(typeof(InstitutionalScenarioDefinition)));
        }

        [Test]
        public void EngineAssemblies_DoNotGrantScenarioAssemblyInternalAccess()
        {
            AssertNoFriendAccess(typeof(InstitutionalScenarioDefinition).Assembly);
            AssertNoFriendAccess(typeof(InstitutionalScenarioEngine).Assembly);
        }

        private static void AssertNoFriendAccess(Assembly assembly)
        {
            string[] friends = assembly
                .GetCustomAttributes<InternalsVisibleToAttribute>()
                .Select(attribute => new AssemblyName(attribute.AssemblyName).Name)
                .ToArray();
            Assert.That(friends, Does.Not.Contain(ScenarioAssemblyName),
                $"{assembly.GetName().Name} must not expose internals to scenario content.");
        }
    }
}
