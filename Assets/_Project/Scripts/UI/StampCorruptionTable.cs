using System;
using UnityEngine;

namespace Desk42.UI
{
    public enum StampVerdict
    {
        Approved,
        Denied
    }

    public enum StampConsequence
    {
        None,
        MildPenalty,
        MoralInjuryOrImpatience,
        CardLoss
    }

    [Serializable]
    public struct StampPayload
    {
        public string StampText;
        public StampConsequence Consequence;
        public float Severity;

        public StampPayload(
            string stampText,
            StampConsequence consequence,
            float severity)
        {
            StampText = stampText;
            Consequence = consequence;
            Severity = severity;
        }
    }

    public static class StampCorruptionTable
    {
        private static readonly string[] Tier4StampPool =
        {
            "TERMINATED",
            "REDACTED",
            "SYNERGIZED"
        };

        public static StampPayload LookupApproved(int sanity)
            => Lookup(sanity, StampVerdict.Approved);

        public static StampPayload LookupDenied(int sanity)
            => Lookup(sanity, StampVerdict.Denied);

        public static StampPayload Lookup(int sanity, StampVerdict verdict)
        {
            if (sanity >= 75)
            {
                return new StampPayload(
                    verdict == StampVerdict.Approved ? "APPROVED" : "DENIED",
                    StampConsequence.None,
                    0f);
            }

            if (sanity >= 50)
            {
                return new StampPayload(
                    verdict == StampVerdict.Approved ? "APROVED" : "DENED",
                    StampConsequence.MildPenalty,
                    0.33f);
            }

            if (sanity >= 25)
            {
                return new StampPayload(
                    verdict == StampVerdict.Approved ? "COMPLICIT" : "CONDEMNED",
                    StampConsequence.MoralInjuryOrImpatience,
                    0.66f);
            }

            return new StampPayload(
                Tier4StampPool[UnityEngine.Random.Range(0, Tier4StampPool.Length)],
                StampConsequence.CardLoss,
                1f);
        }
    }
}
