using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Desk42.Core;
using Desk42.OfficeSupplies;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Desk42.UI
{
    public enum CascadeValueKind
    {
        Duration,
        CreditCost,
        SoulCost
    }

    [DisallowMultipleComponent]
    public sealed class CascadePresenter : MonoBehaviour
    {
        [Header("Timing")]
        [SerializeField] private CascadeConfig _config;
        [SerializeField] private KeyCode _fastForwardKey = KeyCode.Space;

        [Header("Modifier Chain")]
        [SerializeField] private TMP_Text _modifierLabel;
        [SerializeField] private Slider _durationBar;
        [SerializeField] private Slider _creditCostBar;
        [SerializeField] private Slider _soulCostBar;

        [Header("Stamp")]
        [SerializeField] private TMP_Text _stampLabel;
        [SerializeField] private StampVerdict _stampVerdict = StampVerdict.Approved;

        [Header("Audio")]
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private AudioClip _modifierChime;

        /// <summary>
        /// True asks the integration layer to lock input and pause Impatience;
        /// false asks it to restore both after the stamp has been presented.
        /// </summary>
        public event Action<bool> IntentStateChanged;

        public event Action<CascadeValueKind, ModifierStep> ModifierPresented;
        public event Action<StampPayload> StampPresented;

        private Coroutine _activeSequence;
        private bool _intentActive;

        public void PlaySequence(SynergyResolutionPacket packet)
        {
            if (_activeSequence != null)
            {
                StopCoroutine(_activeSequence);
                _activeSequence = null;
                SetIntentState(false);
            }

            _activeSequence = StartCoroutine(PlaySequenceRoutine(packet));
        }

        private IEnumerator PlaySequenceRoutine(SynergyResolutionPacket packet)
        {
            // Intent: the integration layer owns input and Impatience state.
            SetIntentState(true);

            SetBarValue(_durationBar, packet.BaseDuration);
            SetBarValue(_creditCostBar, packet.BaseCreditCost);
            SetBarValue(_soulCostBar, packet.BaseSoulCost);

            // Anticipation: the processing animation is triggered here.
            yield return new WaitForSeconds(BaseDelay * 1.5f);

            // The resolver's three chains are presented once each, in packet order.
            yield return PresentSteps(packet.DurationSteps, CascadeValueKind.Duration, _durationBar);
            yield return PresentSteps(packet.CreditCostSteps, CascadeValueKind.CreditCost, _creditCostBar);
            yield return PresentSteps(packet.SoulCostSteps, CascadeValueKind.SoulCost, _soulCostBar);

            int sanity = Mathf.RoundToInt(GameManager.Instance?.Run?.Sanity ?? 100f);
            StampPayload stamp = StampCorruptionTable.Lookup(sanity, _stampVerdict);

            if (_stampLabel != null)
                _stampLabel.text = stamp.StampText;

            StampPresented?.Invoke(stamp);

            SetIntentState(false);
            _activeSequence = null;
        }

        private IEnumerator PresentSteps(
            List<ModifierStep> steps,
            CascadeValueKind valueKind,
            Slider targetBar)
        {
            if (steps == null)
                yield break;

            foreach (ModifierStep step in steps)
            {
                if (_modifierLabel != null)
                    _modifierLabel.text = $"{step.DisplayName}  {FormatDelta(step.Delta)}";

                SetBarValue(targetBar, step.NewValue);

                if (_audioSource != null && _modifierChime != null)
                    _audioSource.PlayOneShot(_modifierChime);

                ModifierPresented?.Invoke(valueKind, step);

                string comboKey = $"{step.SupplyId}_{step.Zone}";
                bool fastForward = Input.GetKey(_fastForwardKey)
                    || GetSeenComboCount(comboKey) >= AutoFastForwardCount;

                yield return new WaitForSeconds(fastForward ? SkipDelay : BaseDelay);
            }
        }

        private void OnDisable()
        {
            if (_activeSequence != null)
            {
                StopCoroutine(_activeSequence);
                _activeSequence = null;
            }

            SetIntentState(false);
        }

        private void SetIntentState(bool active)
        {
            if (_intentActive == active)
                return;

            _intentActive = active;
            IntentStateChanged?.Invoke(active);
        }

        private static void SetBarValue(Slider bar, float value)
        {
            if (bar != null)
                bar.value = value;
        }

        private static string FormatDelta(float delta)
            => delta > 0f ? $"+{delta:0.##}" : delta.ToString("0.##");

        private int GetSeenComboCount(string comboKey)
        {
            MetaProgressData meta = GameManager.Instance?.Meta;
            if (meta == null)
                return 0;

            // The handoff assigns ownership of SeenComboCount to MetaProgressData.
            // Read it by name so this new-file-only branch remains compatible until
            // the owning branch lands that member; no meta state is written here.
            const string memberName = "SeenComboCount";
            Type metaType = meta.GetType();
            object value = metaType
                .GetField(memberName, BindingFlags.Instance | BindingFlags.Public)
                ?.GetValue(meta)
                ?? metaType
                    .GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public)
                    ?.GetValue(meta);

            if (value is IReadOnlyDictionary<string, int> readOnlyCounts
                && readOnlyCounts.TryGetValue(comboKey, out int readOnlyCount))
            {
                return readOnlyCount;
            }

            if (value is IDictionary<string, int> counts
                && counts.TryGetValue(comboKey, out int count))
            {
                return count;
            }

            return 0;
        }

        private float BaseDelay => _config != null ? _config.BaseDelay : 0.3f;
        private float SkipDelay => _config != null ? _config.SkipDelay : 0.08f;
        private int AutoFastForwardCount => _config != null ? _config.AutoFastForwardCount : 3;
    }
}
