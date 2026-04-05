// ============================================================
// DESK 42 — Archetype Factory
//
// Single entry point for instantiating archetypes by ID.
// Add new archetypes here when they are implemented.
// ============================================================

using UnityEngine;

namespace Desk42.Archetypes
{
    public static class ArchetypeFactory
    {
        /// <summary>
        /// Create an archetype instance by its stable string ID.
        /// Returns null (with a warning) for unknown IDs.
        /// </summary>
        public static IArchetype Create(string archetypeId)
        {
            return archetypeId switch
            {
                "auditor"    => new TheAuditor(),
                "gaslighter" => new TheGaslighter(),
                "bureaucrat" => new TheBureaucrat(),
                "it_person"  => new TheITPerson(),
                _ => Fallback(archetypeId),
            };
        }

        private static IArchetype Fallback(string id)
        {
            Debug.LogWarning($"[ArchetypeFactory] Unknown archetype id: '{id}'. " +
                             "Defaulting to Auditor.");
            return new TheAuditor();
        }

        /// <summary>All valid archetype IDs, in selection-screen order.</summary>
        public static readonly string[] AllIds =
        {
            "auditor",
            "gaslighter",
            "bureaucrat",
            "it_person",
        };
    }
}
