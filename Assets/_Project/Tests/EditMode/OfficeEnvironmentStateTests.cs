using Desk42.Core;
using NUnit.Framework;

namespace Desk42.Tests.EditMode
{
    /// <summary>
    /// OfficeEnvironmentState is static and is not persisted in RunData, so its
    /// reset contract is what keeps one run's office from leaking into the next.
    /// </summary>
    public sealed class OfficeEnvironmentStateTests
    {
        private const float TempDefault  = 50f;
        private const float NoiseDefault = 20f;

        [SetUp]
        public void SetUp() => OfficeEnvironmentState.Reset();

        [TearDown]
        public void TearDown() => OfficeEnvironmentState.Reset();

        [Test]
        public void Reset_RestoresNeutralOffice()
        {
            OfficeEnvironmentState.ModifyTemperature(+40f);
            OfficeEnvironmentState.ModifyNoise(+70f);

            OfficeEnvironmentState.Reset();

            Assert.AreEqual(TempDefault,  OfficeEnvironmentState.Temperature, 0.001f);
            Assert.AreEqual(NoiseDefault, OfficeEnvironmentState.NoiseLevel,  0.001f);
        }

        [Test]
        public void StateIsStatic_AndPersistsUntilResetIsCalled()
        {
            // Documents the leak the BeginNewRun/ResumeRun hooks exist to prevent:
            // nothing about ending a run clears these values on its own.
            OfficeEnvironmentState.ApplyHazard(OfficeHazardType.PrinterJam);
            float afterHazard = OfficeEnvironmentState.Temperature;

            Assert.Greater(afterHazard, TempDefault);
            Assert.AreEqual(afterHazard, OfficeEnvironmentState.Temperature, 0.001f);
        }

        [Test]
        public void FireDrill_NormalisesTemperatureAndRaisesAlarmNoise()
        {
            OfficeEnvironmentState.ModifyTemperature(+30f);

            OfficeEnvironmentState.ApplyHazard(OfficeHazardType.FireDrill);

            Assert.AreEqual(TempDefault, OfficeEnvironmentState.Temperature, 0.001f);
            Assert.AreEqual(80f, OfficeEnvironmentState.NoiseLevel, 0.001f);
        }

        [Test]
        public void FireDrill_IsAnAbsoluteSet_NotAnIncrease()
        {
            // Current authored behaviour: a fire drill during an already-louder
            // office pulls noise *down* to the alarm level. Locked so the
            // set-vs-raise question is a deliberate change, not an accident.
            OfficeEnvironmentState.ModifyNoise(+75f); // 20 -> 95

            OfficeEnvironmentState.ApplyHazard(OfficeHazardType.FireDrill);

            Assert.AreEqual(80f, OfficeEnvironmentState.NoiseLevel, 0.001f);
        }

        [Test]
        public void Tick_DriftsBackTowardDefaults()
        {
            OfficeEnvironmentState.ModifyTemperature(+20f);
            float before = OfficeEnvironmentState.Temperature;

            OfficeEnvironmentState.Tick(5f);

            Assert.Less(OfficeEnvironmentState.Temperature, before);
            Assert.GreaterOrEqual(OfficeEnvironmentState.Temperature, TempDefault);
        }

        [Test]
        public void InjectionDurationMultiplier_TracksTemperatureBands()
        {
            OfficeEnvironmentState.Reset();
            Assert.AreEqual(1.0f, OfficeEnvironmentState.GetInjectionDurationMultiplier(), 0.001f);

            OfficeEnvironmentState.ModifyTemperature(+40f); // 90 -> overheating
            Assert.AreEqual(0.8f, OfficeEnvironmentState.GetInjectionDurationMultiplier(), 0.001f);

            OfficeEnvironmentState.Reset();
            OfficeEnvironmentState.ModifyTemperature(-40f); // 10 -> freezing
            Assert.AreEqual(1.2f, OfficeEnvironmentState.GetInjectionDurationMultiplier(), 0.001f);
        }
    }
}
