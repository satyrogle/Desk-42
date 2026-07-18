using System;
using System.Collections.Generic;
using Desk42.Accessibility;
using Desk42.Core;
using UnityEngine;

namespace Desk42.UI
{
    [Serializable]
    public sealed class DeskItemProfile
    {
        [Header("Target")]
        [Tooltip("Desk item RectTransform animated by the reactor.")]
        [SerializeField] private RectTransform _target;

        [Header("Identity")]
        [Tooltip("Stable item id: coffee, pen_holder, papers, or stapler.")]
        [SerializeField] private string _itemId;

        [Header("Physics")]
        [Tooltip("Relative mass. Heavier items receive less displacement.")]
        [SerializeField] private float _mass = 1f;

        [Header("Return")]
        [Tooltip("How quickly the item settles back. Higher values settle faster.")]
        [SerializeField] private float _damping = 8f;

        public RectTransform Target => _target;
        public string ItemId => _itemId;
        public float Mass => Mathf.Max(0.1f, _mass);
        public float Damping => Mathf.Max(0.1f, _damping);
    }

    [DisallowMultipleComponent]
    public sealed class DeskItemReactor : MonoBehaviour
    {
        [Header("Desk Items")]
        [Tooltip("Coffee, pen holder, papers, and stapler movement profiles.")]
        [SerializeField] private List<DeskItemProfile> _items = new();

        private sealed class ItemRuntime
        {
            public DeskItemProfile Profile;
            public Vector2 BasePosition;
            public Quaternion BaseRotation;
            public Vector3 BaseScale;
            public Vector2 PositionVelocity;
            public float RotationVelocity;
            public Vector3 ScaleVelocity;
            public float PaperPileOffset;
            public float FrozenUntil;
        }

        private readonly List<ItemRuntime> _runtimeItems = new();

        private void Awake()
        {
            CaptureItems();
        }

        private void Update()
        {
            bool reducedMotion = AccessibilitySettings.ReducedMotion;

            foreach (ItemRuntime item in _runtimeItems)
            {
                RectTransform target = item.Profile.Target;
                if (target == null || Time.unscaledTime < item.FrozenUntil)
                    continue;

                Vector2 restingPosition = GetRestingPosition(item);
                if (reducedMotion)
                {
                    target.anchoredPosition = restingPosition;
                    target.localRotation = item.BaseRotation;
                    target.localScale = item.BaseScale;
                    continue;
                }

                float smoothTime = Mathf.Max(0.02f, 1f / item.Profile.Damping);
                target.anchoredPosition = Vector2.SmoothDamp(
                    target.anchoredPosition,
                    restingPosition,
                    ref item.PositionVelocity,
                    smoothTime,
                    Mathf.Infinity,
                    Time.unscaledDeltaTime);

                float currentAngle = target.localEulerAngles.z;
                float targetAngle = item.BaseRotation.eulerAngles.z;
                float angle = Mathf.SmoothDampAngle(
                    currentAngle,
                    targetAngle,
                    ref item.RotationVelocity,
                    smoothTime,
                    Mathf.Infinity,
                    Time.unscaledDeltaTime);
                target.localRotation = Quaternion.Euler(0f, 0f, angle);
                target.localScale = Vector3.SmoothDamp(
                    target.localScale,
                    item.BaseScale,
                    ref item.ScaleVelocity,
                    smoothTime,
                    Mathf.Infinity,
                    Time.unscaledDeltaTime);
            }
        }

        public void OnClientStateChanged(ClientStateID from, ClientStateID to)
        {
            if (AccessibilitySettings.ReducedMotion)
            {
                SnapToRestingPositions();
                return;
            }

            if (!EntropyManager.CanActivate(EntropyLayer.ShadowBureaucrat)
                || !FeedbackBudget.RequestBurst(FeedbackKind.Particle))
            {
                return;
            }

            switch (to)
            {
                case ClientStateID.Agitated:
                    Nudge("pen_holder", new Vector2(3f, 0f), 7f, 1f);
                    Nudge("coffee", new Vector2(0f, 1.5f), -2f, 1.035f);
                    break;
                case ClientStateID.Litigious:
                    Nudge("papers", Vector2.up * 5f, 0f, 1f);
                    break;
                case ClientStateID.Cooperative:
                    SettleAll();
                    break;
                case ClientStateID.Paranoid:
                    foreach (ItemRuntime item in _runtimeItems)
                        Nudge(item, Vector2.down * 4f, 0f, 1f);
                    break;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[DeskItemReactor] Client state reaction {from} -> {to}.");
#endif
        }

        public void OnCardSlammed(PunchCardType cardType)
        {
            if (AccessibilitySettings.ReducedMotion)
            {
                SnapToRestingPositions();
                return;
            }

            if (!EntropyManager.CanActivate(EntropyLayer.ShadowBureaucrat)
                || !FeedbackBudget.RequestBurst(FeedbackKind.Particle))
            {
                return;
            }

            foreach (ItemRuntime item in _runtimeItems)
                Nudge(item, Vector2.down * 2f, 0f, 1f);

            if (cardType == PunchCardType.Expedite)
                Nudge("stapler", Vector2.zero, 5f, 1f);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[DeskItemReactor] Desk recoil fired for {cardType}.");
#endif
        }

        public void OnHazardFired(OfficeHazardType hazard)
        {
            if (AccessibilitySettings.ReducedMotion)
            {
                if (hazard == OfficeHazardType.PrinterJam)
                    AddPaperPileOffset();

                SnapToRestingPositions();
                return;
            }

            if (!EntropyManager.CanActivate(EntropyLayer.GlassCracking)
                || !FeedbackBudget.RequestBurst(FeedbackKind.Particle))
            {
                return;
            }

            switch (hazard)
            {
                case OfficeHazardType.FireDrill:
                    foreach (ItemRuntime item in _runtimeItems)
                        Nudge(item, GetScatterDirection(item.Profile.ItemId) * 10f, 5f, 1f);
                    break;
                case OfficeHazardType.SystemCrash:
                    foreach (ItemRuntime item in _runtimeItems)
                        item.FrozenUntil = Time.unscaledTime + 0.5f;
                    break;
                case OfficeHazardType.PrinterJam:
                    AddPaperPileOffset();
                    break;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[DeskItemReactor] Hazard reaction fired for {hazard}.");
#endif
        }

        public void ResetAll()
        {
            if (_runtimeItems.Count == 0)
                CaptureItems();

            foreach (ItemRuntime item in _runtimeItems)
            {
                RectTransform target = item.Profile.Target;
                if (target == null)
                    continue;

                item.PositionVelocity = Vector2.zero;
                item.RotationVelocity = 0f;
                item.ScaleVelocity = Vector3.zero;
                item.PaperPileOffset = 0f;
                item.FrozenUntil = 0f;
                target.anchoredPosition = item.BasePosition;
                target.localRotation = item.BaseRotation;
                target.localScale = item.BaseScale;
            }
        }

        internal void PlayClaimResolutionReaction(bool approved)
        {
            foreach (ItemRuntime item in _runtimeItems)
            {
                if (!IsItem(item, "papers"))
                    continue;

                if (approved)
                {
                    Nudge(item, new Vector2(2f, -1f), -1.5f, 1f);
                }
                else
                {
                    Nudge(item, new Vector2(9f, 6f), 7f, 1f);
                }
            }
        }

        private void CaptureItems()
        {
            _runtimeItems.Clear();

            if (_items == null)
                return;

            foreach (DeskItemProfile profile in _items)
            {
                if (profile?.Target == null)
                    continue;

                _runtimeItems.Add(new ItemRuntime
                {
                    Profile = profile,
                    BasePosition = profile.Target.anchoredPosition,
                    BaseRotation = profile.Target.localRotation,
                    BaseScale = profile.Target.localScale
                });
            }
        }

        private void AddPaperPileOffset()
        {
            foreach (ItemRuntime item in _runtimeItems)
            {
                if (IsItem(item, "papers"))
                    item.PaperPileOffset += 6f / item.Profile.Mass;
            }
        }

        private void Nudge(string itemId, Vector2 offset, float angle, float scale)
        {
            foreach (ItemRuntime item in _runtimeItems)
            {
                if (IsItem(item, itemId))
                    Nudge(item, offset, angle, scale);
            }
        }

        private static void Nudge(
            ItemRuntime item,
            Vector2 offset,
            float angle,
            float scale)
        {
            RectTransform target = item.Profile.Target;
            if (target == null)
                return;

            float inverseMass = 1f / item.Profile.Mass;
            target.anchoredPosition += offset * inverseMass;
            target.localRotation *= Quaternion.Euler(0f, 0f, angle * inverseMass);
            target.localScale = Vector3.LerpUnclamped(
                item.BaseScale,
                item.BaseScale * scale,
                inverseMass);
        }

        private void SettleAll()
        {
            foreach (ItemRuntime item in _runtimeItems)
            {
                item.PositionVelocity = Vector2.zero;
                item.RotationVelocity = 0f;
                item.ScaleVelocity = Vector3.zero;
            }
        }

        private void SnapToRestingPositions()
        {
            foreach (ItemRuntime item in _runtimeItems)
            {
                RectTransform target = item.Profile.Target;
                if (target == null)
                    continue;

                target.anchoredPosition = GetRestingPosition(item);
                target.localRotation = item.BaseRotation;
                target.localScale = item.BaseScale;
            }
        }

        private static Vector2 GetRestingPosition(ItemRuntime item)
        {
            return item.BasePosition + Vector2.up * item.PaperPileOffset;
        }

        private static bool IsItem(ItemRuntime item, string itemId)
        {
            return string.Equals(
                item.Profile.ItemId,
                itemId,
                StringComparison.OrdinalIgnoreCase);
        }

        private static Vector2 GetScatterDirection(string itemId)
        {
            if (string.Equals(itemId, "coffee", StringComparison.OrdinalIgnoreCase))
                return new Vector2(-1f, 0.35f).normalized;
            if (string.Equals(itemId, "pen_holder", StringComparison.OrdinalIgnoreCase))
                return new Vector2(1f, 0.5f).normalized;
            if (string.Equals(itemId, "papers", StringComparison.OrdinalIgnoreCase))
                return new Vector2(0.6f, 1f).normalized;
            return new Vector2(1f, -0.25f).normalized;
        }
    }
}
