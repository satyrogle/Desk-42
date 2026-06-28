using UnityEngine;

namespace Desk42.UI
{
    [CreateAssetMenu(
        fileName = "CascadeConfig",
        menuName = "Desk 42/UI/Cascade Config")]
    public sealed class CascadeConfig : ScriptableObject
    {
        public float BaseDelay = 0.3f;
        public float SkipDelay = 0.08f;
        public int AutoFastForwardCount = 3;
        public bool EnableTier4CardLoss = false;
    }
}
