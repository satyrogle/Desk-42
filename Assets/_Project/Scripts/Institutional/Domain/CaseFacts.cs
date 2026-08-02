using System;
using System.Collections.Generic;

namespace Desk42.Institutional
{
    /// <summary>
    /// One inspectable fact attached to a case. Keys and values are opaque stable
    /// identifiers; comparison never performs trimming, case folding, or culture-specific
    /// normalization.
    /// </summary>
    [Serializable]
    public sealed class CaseFact : IEquatable<CaseFact>, IComparable<CaseFact>
    {
        public string Key;
        public string Value;

        public CaseFact()
        {
        }

        public CaseFact(string key, string value)
        {
            Key = key;
            Value = value;
            Validate();
        }

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(Key))
                throw new InvalidOperationException("A case fact requires a non-blank key.");
            if (string.IsNullOrWhiteSpace(Value))
                throw new InvalidOperationException($"Case fact '{Key}' requires a non-blank value.");
        }

        public CaseFact Copy()
        {
            Validate();
            return new CaseFact(Key, Value);
        }

        public bool Equals(CaseFact other)
        {
            return other != null &&
                   string.Equals(Key, other.Key, StringComparison.Ordinal) &&
                   string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as CaseFact);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int keyHash = Key == null ? 0 : StringComparer.Ordinal.GetHashCode(Key);
                int valueHash = Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
                return (keyHash * 397) ^ valueHash;
            }
        }

        public int CompareTo(CaseFact other)
        {
            if (other == null) return 1;

            int keyComparison = StringComparer.Ordinal.Compare(Key, other.Key);
            return keyComparison != 0
                ? keyComparison
                : StringComparer.Ordinal.Compare(Value, other.Value);
        }
    }

    /// <summary>
    /// A validated key/value set. Construction, addition, and copying detach the
    /// supplied facts and store them in deterministic ordinal order.
    /// </summary>
    [Serializable]
    public sealed class CaseFactSet
    {
        public List<CaseFact> Facts = new();

        public int Count => Facts?.Count ?? 0;

        public CaseFactSet()
        {
        }

        public CaseFactSet(IEnumerable<CaseFact> facts)
        {
            Facts = CopyInDeterministicOrder(facts, nameof(facts));
        }

        public void Add(string key, string value)
        {
            Add(new CaseFact(key, value));
        }

        public void Add(CaseFact fact)
        {
            if (fact == null) throw new ArgumentNullException(nameof(fact));
            Validate();

            var updated = new List<CaseFact>(Facts);
            updated.Add(fact);
            Facts = CopyInDeterministicOrder(updated, nameof(fact));
        }

        public bool Contains(string key, string value)
        {
            return Contains(new CaseFact(key, value));
        }

        public bool Contains(CaseFact fact)
        {
            if (fact == null) throw new ArgumentNullException(nameof(fact));
            fact.Validate();
            Validate();

            for (int i = 0; i < Facts.Count; i++)
            {
                if (Facts[i].Equals(fact)) return true;
            }

            return false;
        }

        public bool ContainsAll(CaseFactSet requiredFacts)
        {
            if (requiredFacts == null) throw new ArgumentNullException(nameof(requiredFacts));
            requiredFacts.Validate();
            return ContainsAll(requiredFacts.Facts);
        }

        public bool ContainsAll(IEnumerable<CaseFact> requiredFacts)
        {
            if (requiredFacts == null) throw new ArgumentNullException(nameof(requiredFacts));
            Validate();

            List<CaseFact> required = CopyInDeterministicOrder(requiredFacts, nameof(requiredFacts));
            for (int i = 0; i < required.Count; i++)
            {
                if (!Contains(required[i])) return false;
            }

            return true;
        }

        public CaseFactSet Copy()
        {
            Validate();
            return new CaseFactSet(Facts);
        }

        public void Validate()
        {
            ValidateFacts(Facts);
        }

        private static List<CaseFact> CopyInDeterministicOrder(
            IEnumerable<CaseFact> facts,
            string parameterName)
        {
            if (facts == null) throw new ArgumentNullException(parameterName);

            var copy = new List<CaseFact>();
            foreach (CaseFact fact in facts)
            {
                if (fact == null)
                    throw new InvalidOperationException("A case fact set cannot contain null facts.");
                copy.Add(fact.Copy());
            }

            ValidateFacts(copy);
            copy.Sort((left, right) => left.CompareTo(right));
            return copy;
        }

        private static void ValidateFacts(List<CaseFact> facts)
        {
            if (facts == null)
                throw new InvalidOperationException("A case fact set requires a fact collection.");

            var keys = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < facts.Count; i++)
            {
                CaseFact fact = facts[i];
                if (fact == null)
                    throw new InvalidOperationException("A case fact set cannot contain null facts.");

                fact.Validate();
                if (!keys.Add(fact.Key))
                    throw new InvalidOperationException($"Duplicate case fact key '{fact.Key}'.");
            }
        }
    }
}
