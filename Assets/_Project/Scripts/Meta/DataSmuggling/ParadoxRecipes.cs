// ============================================================
// DESK 42 — Paradox Recipes
//
// Steganography via Bureaucracy. The system is built to handle
// average forms, average employees, average decisions. When the
// player executes a contradictory sequence of cards on the SAME
// client encounter, CorpOS stutters, the form glitches, and an
// .exe fragment drops onto the desk.
//
// Recipes are intentionally predefined (not procedurally
// generated) so players can discover and share them. Clues are
// delivered through PostItFragment content on the Conspiracy
// Board (authored separately in narrative data).
//
// Each recipe is an ordered sequence of PunchCardTypes. The
// detector matches by suffix: if the last N cards on the active
// client encounter match the recipe, it fires.
// ============================================================

using System.Collections.Generic;
using Desk42.Core;

namespace Desk42.Meta.DataSmuggling
{
    public sealed class ParadoxRecipe
    {
        public string             Id;
        public string             DisplayName;
        public string             ConspiracyClue;
        public PunchCardType[]    Sequence;

        public ParadoxRecipe(string id, string name, string clue, params PunchCardType[] sequence)
        {
            Id = id;
            DisplayName = name;
            ConspiracyClue = clue;
            Sequence = sequence;
        }
    }

    public static class ParadoxRecipes
    {
        public static readonly IReadOnlyList<ParadoxRecipe> All = new[]
        {
            new ParadoxRecipe("urgent_postponed",
                "URGENT — POSTPONED",
                "A form stamped both ways at once. Tube 4 chokes; a disk drops.",
                PunchCardType.Expedite, PunchCardType.Forget),

            new ParadoxRecipe("legal_transfer",
                "LEGAL HOLD — TRANSFERRED",
                "Held in legal indefinitely, also sent to another department. " +
                "The filing system divides by zero.",
                PunchCardType.LegalHold, PunchCardType.Transfer),

            new ParadoxRecipe("redact_analyse",
                "REDACTED — ANALYSED",
                "Black out the truth, then study what isn't there. Recursion error.",
                PunchCardType.Redact, PunchCardType.Analyse),

            new ParadoxRecipe("nda_interrogate",
                "NDA — INTERROGATED",
                "Forbidden from speaking, compelled to answer. A loop opens.",
                PunchCardType.NonDisclosure, PunchCardType.Interrogate),

            new ParadoxRecipe("gaslight_empathy",
                "GASLIT — EMPATHISED",
                "Denied their experience and validated it in the same breath. " +
                "Form re-files itself in a third drawer that does not exist.",
                PunchCardType.Gaslight, PunchCardType.Empathise),
        };
    }
}
