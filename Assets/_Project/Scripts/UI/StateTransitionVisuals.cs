using Desk42.Core;
using UnityEngine;

namespace Desk42.UI
{
    public static class StateTransitionVisuals
    {
        public static Color GetPanelTint(ClientStateID state)
        {
            Color color = GetStateColor(state);
            color.a = 0.12f;
            return color;
        }

        public static float GetVignetteIntensity(ClientStateID state)
        {
            return state switch
            {
                ClientStateID.Dissociating => 0.6f,
                ClientStateID.Paranoid => 0.3f,
                ClientStateID.Litigious => 0.2f,
                _ => 0f
            };
        }

        public static Color GetStateColor(ClientStateID state)
        {
            return state switch
            {
                ClientStateID.Pending => new Color(0.85f, 0.85f, 0.80f, 1f),
                ClientStateID.Agitated => new Color(0.95f, 0.55f, 0.30f, 1f),
                ClientStateID.Litigious => new Color(0.80f, 0.20f, 0.20f, 1f),
                ClientStateID.Cooperative => new Color(0.40f, 0.75f, 0.50f, 1f),
                ClientStateID.Suspicious => new Color(0.70f, 0.60f, 0.30f, 1f),
                ClientStateID.Resigned => new Color(0.50f, 0.50f, 0.55f, 1f),
                ClientStateID.Paranoid => new Color(0.55f, 0.35f, 0.70f, 1f),
                ClientStateID.Dissociating => new Color(0.30f, 0.30f, 0.35f, 1f),
                ClientStateID.Smug => new Color(0.90f, 0.80f, 0.40f, 1f),
                _ => new Color(0.85f, 0.85f, 0.80f, 1f)
            };
        }
    }
}
