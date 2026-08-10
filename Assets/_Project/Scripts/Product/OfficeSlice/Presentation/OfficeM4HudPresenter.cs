using UnityEngine;

namespace Desk42.Product.OfficeSlice
{
    /// <summary>Presentation-only HUD configuration; all displayed text comes from product state.</summary>
    public sealed class OfficeM4HudPresenter
    {
        public const float SafeMargin = 16f;
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
            float panelWidth = Mathf.Min(430f, width * 0.34f);
            return new Rect(SafeMargin, SafeMargin, panelWidth, height - SafeMargin * 2f);
        }

        public bool Fits(int width, int height)
        {
            Rect rect = PlayerPanelRect(width, height);
            return rect.xMin >= 0f && rect.yMin >= 0f && rect.xMax <= width && rect.yMax <= height;
        }
    }
}
