using UnityEngine;

namespace Desk42.Core
{
    [CreateAssetMenu(
        menuName = "Desk42/Systems/Tide Tuning",
        fileName = "TideTuning")]
    public sealed class TideTuningData : ScriptableObject
    {
        [Header("Pressure")]
        [Min(1f)] public float FastResolutionThreshold = 45f;
        [Min(1)] public int FastResolutionStreak = 3;

        [Header("Hazards")]
        [Tooltip("Seconds between hazards at pressure levels 0 through 3.")]
        public float[] HazardIntervals = { 75f, 55f, 40f, 30f };
        [Min(1f)] public float MinimumHazardSpacing = 20f;
        [Min(1f)] public float OvertimeHazardInterval = 30f;
        [Min(0f)] public float ChainWindow = 30f;
        [Range(0f, 0.5f)] public float IntervalJitterFraction = 0.15f;

        [Header("Shift Scaling")]
        [Min(0f)] public float IntervalReductionPerShift = 10f;
        [Min(1f)] public float FirstHazardSpacingMultiplier = 2f;

        public float GetHazardInterval(int pressureLevel)
        {
            if (HazardIntervals == null || HazardIntervals.Length == 0)
                return MinimumHazardSpacing;
            return Mathf.Max(MinimumHazardSpacing,
                HazardIntervals[Mathf.Clamp(pressureLevel, 0, HazardIntervals.Length - 1)]);
        }

        private void OnValidate()
        {
            if (HazardIntervals == null || HazardIntervals.Length != 4)
                HazardIntervals = new[] { 75f, 55f, 40f, 30f };
            for (int i = 0; i < HazardIntervals.Length; i++)
                HazardIntervals[i] = Mathf.Max(1f, HazardIntervals[i]);
        }
    }
}
