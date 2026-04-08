using System;
using System.Collections.Generic;
using UnityEngine;
using Desk42.Core;
using Desk42.BSM;

namespace Desk42.Encounter
{
    /// <summary>
    /// Generates procedurally cohesive clients and claims with dark humor flavor text.
    /// Uses the SeedEngine to ensure deterministic runs.
    /// </summary>
    public class ProceduralClientGenerator : MonoBehaviour
    {
        [Serializable]
        public struct SpeciesFlavor
        {
            public string SpeciesId;
            public string[] FirstNames;
            public string[] LastNames;
            public string[] ComplaintTopics;
        }

        [SerializeField] private SpeciesFlavor[] _speciesFlavors;

        public ActiveClaimData GenerateClaim(int shiftNumber, int anteNumber)
        {
            var species = _speciesFlavors.Length > 0 
                ? _speciesFlavors[SeedEngine.Next(SeedStream.ClaimQueue, 0, _speciesFlavors.Length)] 
                : GetFallbackSpecies();

            string fName = species.FirstNames.Length > 0 ? species.FirstNames[SeedEngine.Next(SeedStream.ClaimQueue, 0, species.FirstNames.Length)] : "Unknown";
            string lName = species.LastNames.Length > 0 ? species.LastNames[SeedEngine.Next(SeedStream.ClaimQueue, 0, species.LastNames.Length)] : "Entity";
            string topic = species.ComplaintTopics.Length > 0 ? species.ComplaintTopics[SeedEngine.Next(SeedStream.ClaimQueue, 0, species.ComplaintTopics.Length)] : "bureaucratic friction";

            string clientId = $"{fName.Substring(0, 1)}{lName}-{SeedEngine.Next(SeedStream.ClaimQueue, 1000, 9999)}";
            string claimId = $"CLM-{shiftNumber}-{anteNumber}-{SeedEngine.Next(SeedStream.ClaimQueue, 10, 99)}";

            string narrativeText = $"{fName} {lName} is filing a formal grievance regarding {topic}. They appear highly agitated by the pneumatic tube processing times.";

            return new ActiveClaimData
            {
                ClaimId = claimId,
                ClientVariantId = clientId,
                ClientSpeciesId = species.SpeciesId,
                IncidentText = narrativeText,
                ClaimantName = $"{fName} {lName}",
                ClaimAmount = SeedEngine.Next(SeedStream.ClaimQueue, 10, 50)
            };
        }

        private SpeciesFlavor GetFallbackSpecies()
        {
            return new SpeciesFlavor
            {
                SpeciesId = "humanoid",
                FirstNames = new[] { "Bob", "Alice", "Subject", "Unit" },
                LastNames = new[] { "Smith", "Johnson", "A01", "B02" },
                ComplaintTopics = new[] { "mandatory overtime", "lukewarm coffee", "soul erasure" }
            };
        }
    }
}
