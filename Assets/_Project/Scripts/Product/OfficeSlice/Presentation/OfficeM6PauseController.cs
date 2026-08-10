using System;

namespace Desk42.Product.OfficeSlice
{
    public enum OfficeM6MenuPage
    {
        Pause,
        Settings,
        About,
    }

    public enum OfficeM6MenuAction
    {
        None,
        Resume,
        OpenSettings,
        RestartShift,
        OpenAbout,
        Back,
    }

    public sealed class OfficeM6PauseController
    {
        public bool Paused { get; private set; }
        public OfficeM6MenuPage Page { get; private set; } = OfficeM6MenuPage.Pause;
        public int Selection { get; private set; }

        public int ItemCount => Page switch
        {
            OfficeM6MenuPage.Settings => 11,
            OfficeM6MenuPage.About => 1,
            _ => 4,
        };

        public void Toggle()
        {
            Paused = !Paused;
            Page = OfficeM6MenuPage.Pause;
            Selection = 0;
        }

        public void Resume()
        {
            Paused = false;
            Page = OfficeM6MenuPage.Pause;
            Selection = 0;
        }

        public void MoveSelection(int delta)
        {
            if (!Paused || delta == 0) return;
            int count = ItemCount;
            Selection = (Selection + Math.Sign(delta) + count) % count;
        }

        public OfficeM6MenuAction Confirm()
        {
            if (!Paused) return OfficeM6MenuAction.None;
            if (Page == OfficeM6MenuPage.About)
                return OfficeM6MenuAction.Back;
            if (Page == OfficeM6MenuPage.Settings)
                return Selection == ItemCount - 1
                    ? OfficeM6MenuAction.Back
                    : OfficeM6MenuAction.None;
            return Selection switch
            {
                0 => OfficeM6MenuAction.Resume,
                1 => OfficeM6MenuAction.OpenSettings,
                2 => OfficeM6MenuAction.RestartShift,
                3 => OfficeM6MenuAction.OpenAbout,
                _ => OfficeM6MenuAction.None,
            };
        }

        public void Apply(OfficeM6MenuAction action)
        {
            switch (action)
            {
                case OfficeM6MenuAction.Resume:
                    Resume();
                    break;
                case OfficeM6MenuAction.OpenSettings:
                    Page = OfficeM6MenuPage.Settings;
                    Selection = 0;
                    break;
                case OfficeM6MenuAction.OpenAbout:
                    Page = OfficeM6MenuPage.About;
                    Selection = 0;
                    break;
                case OfficeM6MenuAction.Back:
                    Page = OfficeM6MenuPage.Pause;
                    Selection = 0;
                    break;
            }
        }
    }
}
