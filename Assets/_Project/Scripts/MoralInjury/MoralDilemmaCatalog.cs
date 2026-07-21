using System;
using System.Collections.Generic;
using UnityEngine;

namespace Desk42.MoralInjury
{
    [CreateAssetMenu(
        menuName = "Desk42/Moral Injury/Dilemma Catalog",
        fileName = "MoralDilemmaCatalog")]
    public sealed class MoralDilemmaCatalog : ScriptableObject
    {
        [SerializeField] private List<MoralDilemmaData> _dilemmas = new();

        public IReadOnlyList<MoralDilemmaData> Dilemmas => _dilemmas;

        private void OnValidate()
        {
            _dilemmas.RemoveAll(item => item == null);
            for (int i = _dilemmas.Count - 1; i >= 0; i--)
            {
                if (_dilemmas.IndexOf(_dilemmas[i]) != i)
                    _dilemmas.RemoveAt(i);
            }
        }
    }
}
