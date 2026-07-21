using System.Collections;
using Desk42.Accessibility;
using Desk42.BSM;
using Desk42.Core;
using UnityEngine;

namespace Desk42.UI
{
    [DisallowMultipleComponent]
    public sealed class ClientFidgetDriver : MonoBehaviour
    {
        [Header("Portrait Target")]
        [Tooltip("Portrait RectTransform that receives state-driven micro-movement and reactions.")]
        [SerializeField] private RectTransform _portrait;

        private enum MotionAxis
        {
            Both,
            Horizontal,
            Vertical,
            None
        }

        private readonly struct MovementProfile
        {
            public readonly float Frequency;
            public readonly float Amplitude;
            public readonly MotionAxis Axis;

            public MovementProfile(float frequency, float amplitude, MotionAxis axis)
            {
                Frequency = frequency;
                Amplitude = amplitude;
                Axis = axis;
            }
        }

        private ClientStateID _state = ClientStateID.Pending;
        private Vector2 _basePosition;
        private Vector2 _reactionOffset;
        private Vector3 _baseScale = Vector3.one;
        private float _tellScale = 1f;
        private float _noiseSeed;
        private bool _hasCachedTransform;
        private bool _isFrozen;
        private Coroutine _recoilRoutine;
        private Coroutine _tellRoutine;

        private void Start()
        {
            CacheTransform();
        }

        public void SetPortrait(RectTransform portrait)
        {
            if (_portrait == portrait)
                return;

            if (_portrait != null && _hasCachedTransform)
            {
                _portrait.anchoredPosition = _basePosition;
                _portrait.localScale = _baseScale;
            }

            _portrait = portrait;
            _hasCachedTransform = false;
            _reactionOffset = Vector2.zero;
            _tellScale = 1f;
            CacheTransform();
        }

        private void Update()
        {
            if (_portrait == null || _isFrozen)
                return;

            if (!_hasCachedTransform)
                CacheTransform();

            MovementProfile profile = GetProfile(_state);
            Vector2 baselineOffset = GetPerlinOffset(profile, Time.unscaledTime);
            _portrait.anchoredPosition = _basePosition + baselineOffset + _reactionOffset;
            _portrait.localScale = _baseScale * _tellScale;
        }

        public void SetState(ClientStateID state)
        {
            _state = state;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[ClientFidgetDriver] State profile set to {state}.");
#endif
        }

        public void OnSlamRecoil(PunchCardType card)
        {
            if (_portrait == null || _isFrozen || AccessibilitySettings.ReducedMotion)
                return;

            if (!EntropyManager.CanActivate(EntropyLayer.ShadowBureaucrat))
                return;

            if (!FeedbackBudget.RequestBurst(FeedbackKind.Shake))
                return;

            if (_recoilRoutine != null)
                StopCoroutine(_recoilRoutine);

            _recoilRoutine = StartCoroutine(RecoilRoutine(GetCardDirection(card)));
        }

        public void OnTellReceived(TellDefinition tell)
        {
            if (tell == null || _portrait == null || _isFrozen
                || AccessibilitySettings.ReducedMotion)
            {
                return;
            }

            if (_tellRoutine != null)
                StopCoroutine(_tellRoutine);

            _tellRoutine = StartCoroutine(TellPulseRoutine(tell));
        }

        public void Freeze()
        {
            if (_portrait == null)
                return;

            _isFrozen = true;

            if (_recoilRoutine != null)
            {
                StopCoroutine(_recoilRoutine);
                _recoilRoutine = null;
            }

            if (_tellRoutine != null)
            {
                StopCoroutine(_tellRoutine);
                _tellRoutine = null;
            }

            _reactionOffset = Vector2.zero;
            _tellScale = 1f;
            _portrait.localScale = _baseScale;
        }

        public void Unfreeze()
        {
            _isFrozen = false;
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            _recoilRoutine = null;
            _tellRoutine = null;

            if (_portrait == null || !_hasCachedTransform)
                return;

            _reactionOffset = Vector2.zero;
            _tellScale = 1f;
            _portrait.anchoredPosition = _basePosition;
            _portrait.localScale = _baseScale;
        }

        private void CacheTransform()
        {
            if (_portrait == null)
                return;

            _basePosition = _portrait.anchoredPosition;
            _baseScale = _portrait.localScale;
            _noiseSeed = Mathf.Abs(GetInstanceID() * 0.137f) + 17.3f;
            _hasCachedTransform = true;
        }

        private Vector2 GetPerlinOffset(MovementProfile profile, float time)
        {
            if (profile.Axis == MotionAxis.None || profile.Frequency <= 0f
                || profile.Amplitude <= 0f)
            {
                return Vector2.zero;
            }

            float sampleTime = time * profile.Frequency;
            float x = (Mathf.PerlinNoise(_noiseSeed, sampleTime) - 0.5f)
                * 2f * profile.Amplitude;
            float y = (Mathf.PerlinNoise(_noiseSeed + 31.7f, sampleTime) - 0.5f)
                * 2f * profile.Amplitude;

            return profile.Axis switch
            {
                MotionAxis.Horizontal => new Vector2(x, 0f),
                MotionAxis.Vertical => new Vector2(0f, y),
                _ => new Vector2(x, y)
            };
        }

        private IEnumerator RecoilRoutine(Vector2 direction)
        {
            const float duration = 0.4f;
            Vector2 startOffset = direction.normalized * 8f;
            _reactionOffset = startOffset;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (_portrait == null || _portrait.gameObject == null)
                    yield break;

                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - Mathf.Pow(1f - progress, 3f);
                _reactionOffset = Vector2.LerpUnclamped(startOffset, Vector2.zero, eased);
                yield return null;
            }

            _reactionOffset = Vector2.zero;
            _recoilRoutine = null;
        }

        private IEnumerator TellPulseRoutine(TellDefinition tell)
        {
            const float duration = 0.28f;
            GetTellPulse(tell, out float peakScale, out Vector2 peakOffset);

            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (_portrait == null || _portrait.gameObject == null)
                    yield break;

                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                float pulse = Mathf.Sin(progress * Mathf.PI);
                _tellScale = Mathf.LerpUnclamped(1f, peakScale, pulse);
                _reactionOffset = Vector2.LerpUnclamped(Vector2.zero, peakOffset, pulse);
                yield return null;
            }

            _tellScale = 1f;
            _reactionOffset = Vector2.zero;
            _tellRoutine = null;
        }

        private static MovementProfile GetProfile(ClientStateID state)
        {
            return state switch
            {
                ClientStateID.Pending => new MovementProfile(0.3f, 1f, MotionAxis.Both),
                ClientStateID.Agitated => new MovementProfile(2f, 4f, MotionAxis.Horizontal),
                ClientStateID.Litigious => new MovementProfile(1f, 2f, MotionAxis.Vertical),
                ClientStateID.Cooperative => new MovementProfile(0.5f, 2f, MotionAxis.Both),
                ClientStateID.Suspicious => new MovementProfile(0.8f, 1.5f, MotionAxis.Horizontal),
                ClientStateID.Resigned => new MovementProfile(0.1f, 0.5f, MotionAxis.Vertical),
                ClientStateID.Paranoid => new MovementProfile(1.5f, 3f, MotionAxis.Both),
                ClientStateID.Dissociating => new MovementProfile(0f, 0f, MotionAxis.None),
                ClientStateID.Smug => new MovementProfile(0.2f, 1f, MotionAxis.Vertical),
                _ => new MovementProfile(0.3f, 1f, MotionAxis.Both)
            };
        }

        private static Vector2 GetCardDirection(PunchCardType card)
        {
            return card switch
            {
                PunchCardType.Expedite or PunchCardType.AutoFile
                    or PunchCardType.CooperationRoute or PunchCardType.Empathise => Vector2.right,
                PunchCardType.Redact or PunchCardType.Forget
                    or PunchCardType.NonDisclosure or PunchCardType.Gaslight => Vector2.left,
                PunchCardType.ThreatAudit or PunchCardType.Interrogate
                    or PunchCardType.Escalate or PunchCardType.LegalHold
                    or PunchCardType.Legal or PunchCardType.AppointCouncil => Vector2.up,
                _ => Vector2.down
            };
        }

        private static void GetTellPulse(
            TellDefinition tell,
            out float peakScale,
            out Vector2 peakOffset)
        {
            string key = $"{tell.TellType} {tell.AnimationTrigger}".ToLowerInvariant();

            if (key.Contains("smalltalk") || key.Contains("cooperative"))
            {
                peakScale = 1.01f;
                peakOffset = Vector2.up * 0.75f;
                return;
            }

            if (key.Contains("resigned") || key.Contains("dissociat")
                || key.Contains("sigh") || key.Contains("stare"))
            {
                peakScale = 0.98f;
                peakOffset = Vector2.down;
                return;
            }

            if (key.Contains("smug") || key.Contains("suspicious")
                || key.Contains("glance") || key.Contains("covermouth")
                || key.Contains("feetondesk") || key.Contains("voluntary"))
            {
                peakScale = 1.01f;
                peakOffset = Vector2.right * 1.5f;
                return;
            }

            peakScale = 1.025f;
            peakOffset = Vector2.up;
        }
    }
}
