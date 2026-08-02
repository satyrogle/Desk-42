using System;

namespace Desk42.Institutional.Scenarios
{
    /// <summary>
    /// Compile-time marker for the data-only scenario assembly. Concrete scenario
    /// folders may construct Domain definitions and policies only. A host or test
    /// assembly owns execution; scenario content cannot reference Authority at all.
    /// </summary>
    public static class ScenarioAssemblyBoundary
    {
        public static Type DefinitionContract => typeof(InstitutionalScenarioDefinition);
    }
}
