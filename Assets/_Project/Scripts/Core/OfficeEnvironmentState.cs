using UnityEngine;

namespace Desk42.Core
{
    public static class OfficeEnvironmentState
    {
        private const float TEMP_DEFAULT    = 50f;
        private const float TEMP_DRIFT_RATE = 0.5f;
        private const float NOISE_DEFAULT   = 20f;
        private const float NOISE_DRIFT_RATE = 1.0f;

        private static float _temperature = TEMP_DEFAULT;
        private static float _noiseLevel  = NOISE_DEFAULT;

        public static float Temperature => _temperature;
        public static float NoiseLevel  => _noiseLevel;

        public static void Tick(float dt)
        {
            _temperature = Mathf.MoveTowards(_temperature, TEMP_DEFAULT, TEMP_DRIFT_RATE * dt);
            _noiseLevel  = Mathf.MoveTowards(_noiseLevel,  NOISE_DEFAULT, NOISE_DRIFT_RATE * dt);
        }

        public static void ApplyHazard(OfficeHazardType hazard)
        {
            switch (hazard)
            {
                case OfficeHazardType.PrinterJam:
                    ModifyTemperature(8f);
                    break;
                case OfficeHazardType.CoffeeMachineDown:
                    ModifyTemperature(-5f);
                    break;
                case OfficeHazardType.FireDrill:
                    _temperature = TEMP_DEFAULT;
                    _noiseLevel  = Mathf.Min(80f, 100f);
                    break;
                case OfficeHazardType.MandatoryMeeting:
                    ModifyNoise(15f);
                    break;
                case OfficeHazardType.SystemCrash:
                    ModifyNoise(10f);
                    break;
            }
        }

        public static void ApplyCardEffect(PunchCardType card)
        {
            switch (card)
            {
                case PunchCardType.Expedite:
                    ModifyTemperature(3f);
                    break;
                case PunchCardType.CooperationRoute:
                    ModifyNoise(-5f);
                    break;
            }
        }

        public static void ApplyClientStateTick(ClientStateID state, float dt)
        {
            switch (state)
            {
                case ClientStateID.Agitated:
                    ModifyNoise(2f * dt);
                    break;
                case ClientStateID.Cooperative:
                    ModifyNoise(-1f * dt);
                    break;
            }
        }

        public static float GetTellLeadTimeMultiplier()
        {
            return 1.0f - (_noiseLevel / 200f);
        }

        public static float GetInjectionDurationMultiplier()
        {
            if (_temperature > 75f) return 0.8f;
            if (_temperature < 25f) return 1.2f;
            return 1.0f;
        }

        public static string GetTemperatureState()
        {
            if (_temperature > 75f) return "OVERHEATING";
            if (_temperature < 25f) return "FREEZING";
            return "NORMAL";
        }

        public static void ModifyTemperature(float delta)
        {
            _temperature = Mathf.Clamp(_temperature + delta, 0f, 100f);
        }

        public static void ModifyNoise(float delta)
        {
            _noiseLevel = Mathf.Clamp(_noiseLevel + delta, 0f, 100f);
        }

        public static void Reset()
        {
            _temperature = TEMP_DEFAULT;
            _noiseLevel  = NOISE_DEFAULT;
        }
    }
}
