using Desk42.Core;
using NUnit.Framework;
using UnityEngine;

namespace Desk42.Tests.EditMode
{
    public sealed class TideSystemTests
    {
        private TideTuningData _tuning;

        [SetUp]
        public void SetUp()
        {
            SeedEngine.Init(421001);
            _tuning = ScriptableObject.CreateInstance<TideTuningData>();
            _tuning.FastResolutionThreshold = 45f;
            _tuning.FastResolutionStreak = 3;
        }

        [TearDown]
        public void TearDown()
        {
            OfficeEnvironmentState.Reset();
            Object.DestroyImmediate(_tuning);
            RumorMill.FlushQueue();
        }

        [Test]
        public void ThreeFastClaims_EscalatePressure()
        {
            var tide = new TideSystem(_tuning);
            tide.Initialize(1);

            tide.NotifyClaimResolved(15f, false);
            tide.NotifyClaimResolved(15f, false);
            tide.NotifyClaimResolved(15f, false);

            Assert.AreEqual(1, tide.PressureLevel);
        }

        [Test]
        public void CalmPressure_FiresAReachableHazardWithinFirstShiftEnvelope()
        {
            OfficeEnvironmentState.Reset();
            var tide = new TideSystem(_tuning);
            tide.Initialize(1);

            tide.Tick(75f);

            Assert.AreNotEqual(50f, OfficeEnvironmentState.Temperature,
                "The first-shift Tide should be felt during a seven-claim shift.");
        }
    }
}
