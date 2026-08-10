using UnityEngine;

namespace Desk42.Product.OfficeSlice
{
    /// <summary>Presentation-only HUD configuration; all displayed text comes from product state.</summary>
    public sealed class OfficeM4HudPresenter
    {
        public const float SafeMargin = 16f;
        public const float PlayerPanelWidth = 400f;
        public const float PlayerPanelHeight = 350f;
        public bool DevelopmentHudVisible { get; private set; }
        public bool ReducedFlash { get; private set; }

        public void SetDevelopmentHudVisible(bool visible)
        {
            DevelopmentHudVisible = visible;
        }

        public void SetReducedFlash(bool reduced)
        {
            ReducedFlash = reduced;
        }

        public Rect PlayerPanelRect(int width, int height)
        {
            float panelWidth = Mathf.Min(PlayerPanelWidth, width - SafeMargin * 2f);
            float panelHeight = Mathf.Min(PlayerPanelHeight, height - SafeMargin * 2f);
            return new Rect(SafeMargin, height - panelHeight - SafeMargin,
                panelWidth, panelHeight);
        }

        public Rect PortraitRect(int width, int height)
        {
            Rect panel = PlayerPanelRect(width, height);
            return new Rect(panel.xMax - 82f, panel.y + 42f, 68f, 68f);
        }

        public Rect ResultPanelRect(int width, int height)
        {
            float panelWidth = Mathf.Min(650f, width - SafeMargin * 2f);
            float panelHeight = Mathf.Min(650f, height - SafeMargin * 2f);
            return new Rect((width - panelWidth) * 0.5f,
                (height - panelHeight) * 0.5f, panelWidth, panelHeight);
        }

        public Rect DevelopmentPanelRect(int width, int height)
        {
            float panelWidth = Mathf.Min(520f, width - SafeMargin * 2f);
            float panelHeight = Mathf.Min(150f, height - SafeMargin * 2f);
            return new Rect(width - panelWidth - SafeMargin,
                height - panelHeight - SafeMargin, panelWidth, panelHeight);
        }

        public bool Fits(int width, int height)
        {
            return Inside(PlayerPanelRect(width, height), width, height) &&
                Inside(PortraitRect(width, height), width, height) &&
                Inside(ResultPanelRect(width, height), width, height) &&
                Inside(DevelopmentPanelRect(width, height), width, height);
        }

        public bool BreakTargetsRemainVisible(int width, int height)
        {
            Rect panel = PlayerPanelRect(width, height);
            Vector2[] targets =
            {
                new(width * 0.22f, height * 0.34f),
                new(width * 0.77f, height * 0.31f),
                new(width * 0.78f, height * 0.70f),
                new(width * 0.54f, height * 0.52f),
            };
            for (int i = 0; i < targets.Length; i++)
                if (panel.Contains(targets[i])) return false;
            return true;
        }

        private static bool Inside(Rect rect, int width, int height)
        {
            return rect.xMin >= 0f && rect.yMin >= 0f &&
                rect.xMax <= width && rect.yMax <= height;
        }
    }
}
