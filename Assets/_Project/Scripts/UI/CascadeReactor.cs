using System.Collections;
using Desk42.Accessibility;
using Desk42.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Desk42.UI
{
    [DisallowMultipleComponent]
    public sealed class CascadeReactor : MonoBehaviour
    {
        [Header("Desk Reaction")]
        [Tooltip("Desk item reactor that settles or scatters papers after a resolution.")]
        [SerializeField] private DeskItemReactor _deskItemReactor;

        [Header("Combo 2x")]
        [Tooltip("Border image pulsed gold when the combo first reaches 2x.")]
        [SerializeField] private Image _comboBorder;

        [Header("Combo 3x")]
        [Tooltip("Golden stapler icon displayed for three seconds at the 3x milestone.")]
        [SerializeField] private Image _goldenStaplerIcon;

        [Header("Combo 4x")]
        [Tooltip("Full-desk tint image warmed for one second at the 4x milestone.")]
        [SerializeField] private Image _deskTint;

        [Header("Queue Awareness")]
        [Tooltip("Toast label used for queue-awareness messages.")]
        [SerializeField] private TMP_Text _queueToast;

        private static readonly Color Golden = new(0.95f, 0.78f, 0.25f, 1f);
        private static readonly Color WarmTint = new(0.95f, 0.62f, 0.25f, 0.28f);

        private Color _borderBaseColor;
        private Color _staplerBaseColor;
        private Color _deskTintBaseColor;
        private Color _toastBaseColor;
        private int _lastComboMilestone;
        private Coroutine _comboRoutine;
        private Coroutine _toastRoutine;

        private void Awake()
        {
            if (_comboBorder != null)
                _borderBaseColor = _comboBorder.color;

            if (_goldenStaplerIcon != null)
            {
                _staplerBaseColor = _goldenStaplerIcon.color;
                SetGraphicAlpha(_goldenStaplerIcon, 0f);
            }

            if (_deskTint != null)
            {
                _deskTintBaseColor = _deskTint.color;
                SetGraphicAlpha(_deskTint, 0f);
            }

            if (_queueToast != null)
            {
                _toastBaseColor = _queueToast.color;
                SetGraphicAlpha(_queueToast, 0f);
            }
        }

        private void OnEnable()
        {
            RumorMill.OnClaimResolved += HandleClaimResolved;
        }

        private void OnDisable()
        {
            RumorMill.OnClaimResolved -= HandleClaimResolved;
            StopAllCoroutines();
            _comboRoutine = null;
            _toastRoutine = null;
            RestoreVisuals();
        }

        private void HandleClaimResolved(ClaimResolvedEvent resolution)
        {
            bool reducedMotion = AccessibilitySettings.ReducedMotion;

            if (!reducedMotion && _deskItemReactor != null
                && EntropyManager.CanActivate(EntropyLayer.ShadowBureaucrat)
                && FeedbackBudget.RequestBurst(FeedbackKind.Particle))
            {
                _deskItemReactor.PlayClaimResolutionReaction(
                    resolution.Kind == ClaimResolutionKind.Approve);
            }

            float combo = GameManager.Instance?.Run?.ComboMultiplier ?? 1f;
            int milestone = GetNewComboMilestone(combo);
            if (!reducedMotion && milestone > 0)
            {
                if (_comboRoutine != null)
                    StopCoroutine(_comboRoutine);

                _comboRoutine = StartCoroutine(PlayComboMilestoneAfterBudgetWindow(milestone));
            }

            int queueRemaining = GameManager.Instance?.Run?.RawData?.PendingClaims?.Count ?? 0;
            if (queueRemaining == 1)
            {
                ShowQueueToast("Last client.");
            }
            else if (resolution.Kind == ClaimResolutionKind.Deny && queueRemaining >= 3)
            {
                ShowQueueToast("The queue heard that.");
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[CascadeReactor] Factions affected: Legal +2, Accounting -1");
#endif
        }

        private int GetNewComboMilestone(float combo)
        {
            int currentMilestone = combo >= 4f ? 4 : combo >= 3f ? 3 : combo >= 2f ? 2 : 0;

            if (currentMilestone == 0)
            {
                _lastComboMilestone = 0;
                return 0;
            }

            if (currentMilestone <= _lastComboMilestone)
                return 0;

            _lastComboMilestone = currentMilestone;
            return currentMilestone;
        }

        private IEnumerator PlayComboMilestoneAfterBudgetWindow(int milestone)
        {
            // The desk reaction is first in the cascade. Waiting past the global
            // loud cooldown lets the independently-budgeted combo visual request.
            yield return new WaitForSecondsRealtime(0.16f);

            if (!gameObject.activeInHierarchy || AccessibilitySettings.ReducedMotion)
                yield break;

            if (!EntropyManager.CanActivate(EntropyLayer.ShadowBureaucrat)
                || !FeedbackBudget.RequestBurst(FeedbackKind.Flash))
            {
                yield break;
            }

            switch (milestone)
            {
                case 2:
                    yield return PulseBorder();
                    break;
                case 3:
                    yield return ShowStaplerIcon();
                    break;
                case 4:
                    yield return WarmDesk();
                    break;
            }

            _comboRoutine = null;
        }

        private IEnumerator PulseBorder()
        {
            if (!IsAlive(_comboBorder))
                yield break;

            const float duration = 0.65f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (!IsAlive(_comboBorder))
                    yield break;

                elapsed += Time.unscaledDeltaTime;
                float pulse = Mathf.Sin(Mathf.Clamp01(elapsed / duration) * Mathf.PI);
                _comboBorder.color = Color.Lerp(_borderBaseColor, Golden, pulse);
                yield return null;
            }

            if (IsAlive(_comboBorder))
                _comboBorder.color = _borderBaseColor;
        }

        private IEnumerator ShowStaplerIcon()
        {
            if (!IsAlive(_goldenStaplerIcon))
                yield break;

            _goldenStaplerIcon.color = Golden;
            yield return new WaitForSecondsRealtime(3f);

            if (IsAlive(_goldenStaplerIcon))
            {
                _goldenStaplerIcon.color = _staplerBaseColor;
                SetGraphicAlpha(_goldenStaplerIcon, 0f);
            }
        }

        private IEnumerator WarmDesk()
        {
            if (!IsAlive(_deskTint))
                yield break;

            _deskTint.color = WarmTint;
            yield return new WaitForSecondsRealtime(1f);

            if (IsAlive(_deskTint))
            {
                _deskTint.color = _deskTintBaseColor;
                SetGraphicAlpha(_deskTint, 0f);
            }
        }

        private void ShowQueueToast(string message)
        {
            if (_queueToast == null || _queueToast.gameObject == null)
                return;

            if (!FeedbackBudget.RequestBurst(FeedbackKind.Toast))
                return;

            if (_toastRoutine != null)
                StopCoroutine(_toastRoutine);

            _toastRoutine = StartCoroutine(QueueToastRoutine(message));
        }

        private IEnumerator QueueToastRoutine(string message)
        {
            if (!IsAlive(_queueToast))
                yield break;

            _queueToast.text = message;
            _queueToast.color = _toastBaseColor;

            if (AccessibilitySettings.ReducedMotion)
            {
                SetGraphicAlpha(_queueToast, 1f);
                yield return new WaitForSecondsRealtime(2f);

                if (IsAlive(_queueToast))
                    SetGraphicAlpha(_queueToast, 0f);

                _toastRoutine = null;
                yield break;
            }

            yield return FadeGraphic(_queueToast, 0f, 1f, 0.15f);
            yield return new WaitForSecondsRealtime(1.35f);
            yield return FadeGraphic(_queueToast, 1f, 0f, 0.25f);
            _toastRoutine = null;
        }

        private static IEnumerator FadeGraphic(Graphic graphic, float from, float to, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (!IsAlive(graphic))
                    yield break;

                elapsed += Time.unscaledDeltaTime;
                SetGraphicAlpha(graphic, Mathf.Lerp(from, to, elapsed / duration));
                yield return null;
            }

            if (IsAlive(graphic))
                SetGraphicAlpha(graphic, to);
        }

        private void RestoreVisuals()
        {
            if (_comboBorder != null)
                _comboBorder.color = _borderBaseColor;

            if (_goldenStaplerIcon != null)
            {
                _goldenStaplerIcon.color = _staplerBaseColor;
                SetGraphicAlpha(_goldenStaplerIcon, 0f);
            }

            if (_deskTint != null)
            {
                _deskTint.color = _deskTintBaseColor;
                SetGraphicAlpha(_deskTint, 0f);
            }

            if (_queueToast != null)
                SetGraphicAlpha(_queueToast, 0f);
        }

        private static bool IsAlive(Graphic graphic)
        {
            return graphic != null && graphic.gameObject != null;
        }

        private static void SetGraphicAlpha(Graphic graphic, float alpha)
        {
            if (!IsAlive(graphic))
                return;

            Color color = graphic.color;
            color.a = alpha;
            graphic.color = color;
        }
    }
}
