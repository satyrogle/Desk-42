using System;
using Desk42.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Desk42.UI
{
    /// <summary>
    /// Draws the room's architectural and anomaly layer on a fixed logical pixel grid.
    /// The desk plate remains stable while Sanity corrupts recognisable office anchors.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RawImage))]
    public sealed class PixelRoomStageOverlay : MonoBehaviour
    {
        private const int TextureWidth = 480;
        private const int TextureHeight = 270;

        private static readonly Color32 Transparent = new(0, 0, 0, 0);
        private static readonly Color32 DeepGreen = Hex("#173F32");
        private static readonly Color32 DeepShadow = Hex("#081513");
        private static readonly Color32 Paper = Hex("#F1E8CE");
        private static readonly Color32 YellowedPaper = Hex("#D8C58B");
        private static readonly Color32 WarmGrey = Hex("#77736A");
        private static readonly Color32 DustyBlue = Hex("#8FA9A6");
        private static readonly Color32 Wood = Hex("#6A402B");
        private static readonly Color32 Brass = Hex("#B68849");
        private static readonly Color32 Soot = Hex("#332C30");
        private static readonly Color32 Teal = Hex("#20D6C7");
        private static readonly Color32 ElectricBlue = Hex("#4AA7FF");
        private static readonly Color32 Magenta = Hex("#D447A7");

        [Header("Stepped animation")]
        [SerializeField, Min(1f)] private float _framesPerSecond = 8f;

        [Header("Controlled intrusion")]
        [SerializeField, Range(0f, 100f)] private float _tubeThreshold = 74f;
        [SerializeField, Range(0f, 100f)] private float _oozeThreshold = 49f;
        [SerializeField, Range(0f, 100f)] private float _impossibleThreshold = 24f;

        private RawImage _image;
        private Texture2D _texture;
        private Color32[] _pixels;
        private float _sanity = 100f;
        private float _nextFrameTime;
        private int _frame;

        private void Awake()
        {
            _image = GetComponent<RawImage>();
            _texture = new Texture2D(
                TextureWidth, TextureHeight, TextureFormat.RGBA32, false, true)
            {
                name = "Desk42_PixelRoomStage_Runtime",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            _pixels = new Color32[TextureWidth * TextureHeight];
            _image.texture = _texture;
            _image.raycastTarget = false;
            RenderFrame();
        }

        private void OnEnable()
        {
            RumorMill.OnSanityChanged += HandleSanityChanged;
            RunStateController run = GameManager.Instance?.Run;
            if (run != null)
                _sanity = run.Sanity;
        }

        private void OnDisable()
        {
            RumorMill.OnSanityChanged -= HandleSanityChanged;
        }

        private void OnDestroy()
        {
            if (_texture != null)
                Destroy(_texture);
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextFrameTime)
                return;

            _nextFrameTime = Time.unscaledTime + 1f / _framesPerSecond;
            _frame++;
            RenderFrame();
        }

        private void HandleSanityChanged(SanityChangedEvent change)
        {
            _sanity = change.Current;
            RenderFrame();
        }

        private void RenderFrame()
        {
            if (_texture == null || _pixels == null)
                return;

            Array.Fill(_pixels, Transparent);
            DrawHealthyArchitecture();
            DrawClock();

            if (_sanity <= _tubeThreshold)
                DrawEtherealTubes();

            if (_sanity <= _oozeThreshold)
            {
                DrawNoticeboardOoze();
                DrawCabinetPressure();
            }

            if (_sanity <= _impossibleThreshold)
            {
                DrawDoorTentacle();
                DrawVoidLampPool();
                DrawRupturePixels();
            }

            _texture.SetPixels32(_pixels);
            _texture.Apply(false, false);
        }

        private void DrawHealthyArchitecture()
        {
            // Keep the centre sightline clear: the claimant is physically seated there.
            // Architecture is grouped into believable left/right wall zones instead of
            // drawing a second "room" over the desk-stage plate.

            // Frosted-glass records-room door: a readable office anchor at every tier.
            DrawRect(32, 24, 76, 102, DeepShadow);
            DrawRect(36, 28, 68, 94, DeepGreen);
            DrawRect(44, 36, 48, 55, DustyBlue);
            DrawRect(46, 38, 44, 51, new Color32(92, 119, 109, 220));
            DrawRect(91, 73, 7, 4, Brass);
            DrawRect(95, 77, 3, 12, WarmGrey);

            // Cork board and strict paper grid.
            DrawRect(324, 28, 78, 55, DeepShadow);
            DrawRect(328, 32, 70, 47, Wood);
            DrawMemo(335, 37, 20, 15, 0);
            DrawMemo(365, 36, 24, 18, 1);
            DrawMemo(342, 58, 28, 14, 2);
            DrawRect(381, 60, 8, 8, DeepGreen);

            // Radiator and pipework keep the right wall functional rather than empty.
            DrawRect(310, 92, 66, 33, DeepShadow);
            for (int x = 314; x <= 366; x += 9)
            {
                DrawRect(x, 96, 6, 25, WarmGrey);
                DrawRect(x + 1, 97, 2, 22, DustyBlue);
            }
            DrawRect(307, 119, 72, 4, Soot);

            // Low archive shelf at far left, below the door sight line.
            DrawRect(5, 105, 48, 22, DeepShadow);
            DrawRect(8, 108, 42, 16, Wood);
            DrawRect(11, 110, 10, 12, YellowedPaper);
            DrawRect(24, 110, 10, 12, Paper);
            DrawRect(37, 110, 10, 12, YellowedPaper);

            // Surface-mounted conduit and service labels give the wall an office logic.
            DrawRect(8, 18, 132, 4, DeepShadow);
            DrawRect(12, 19, 124, 1, WarmGrey);
            DrawRect(117, 22, 4, 42, DeepShadow);
            DrawRect(119, 24, 1, 36, Brass);
            DrawRect(116, 63, 7, 9, DeepShadow);
            DrawRect(118, 65, 3, 4, Teal);

            // A narrow records index beside the noticeboard prevents the right wall
            // reading as an arbitrary empty corner.
            DrawRect(409, 29, 31, 52, DeepShadow);
            DrawRect(413, 33, 23, 44, DeepGreen);
            for (int row = 37; row <= 69; row += 8)
            {
                DrawRect(416, row, 17, 4, YellowedPaper);
                DrawRect(418, row + 1, 9, 1, WarmGrey);
            }
        }

        private void DrawClock()
        {
            // Mounted between the claimant sightline and the noticeboard. The previous
            // centre-wall position made the clock read as part of the claimant's head.
            int centerX = 305;
            int centerY = 27;
            int radius = 10;
            DrawCircle(centerX, centerY, radius + 2, DeepShadow);
            DrawCircle(centerX, centerY, radius, _sanity <= _oozeThreshold
                ? YellowedPaper : Paper);

            for (int marker = 0; marker < 12; marker++)
            {
                float angle = marker * Mathf.PI * 2f / 12f - Mathf.PI / 2f;
                Plot(
                    centerX + Mathf.RoundToInt(Mathf.Cos(angle) * 7f),
                    centerY + Mathf.RoundToInt(Mathf.Sin(angle) * 7f),
                    marker % 3 == 0 ? Soot : WarmGrey);
            }

            float direction = _sanity <= _oozeThreshold ? -1f : 1f;
            int steppedSecond = Mathf.FloorToInt(_frame * direction) % 60;
            float secondAngle = steppedSecond * Mathf.PI * 2f / 60f - Mathf.PI / 2f;
            float minuteAngle = ((_frame / 8f) % 60f) * direction
                * Mathf.PI * 2f / 60f - Mathf.PI / 2f;
            DrawHand(centerX, centerY, secondAngle, 8, Brass);
            DrawHand(centerX, centerY, minuteAngle, 6, Soot);

            if (_sanity <= _impossibleThreshold)
            {
                float impossibleAngle = ((_frame * 5) % 60)
                    * Mathf.PI * 2f / 60f - Mathf.PI / 2f;
                DrawHand(centerX, centerY, impossibleAngle, 7, Teal);
            }

            DrawRect(centerX - 1, centerY - 1, 3, 3, DeepShadow);
        }

        private void DrawEtherealTubes()
        {
            int pulse = _frame % 19;
            DrawTube(6, 9, 95, 9, pulse);
            DrawTube(382, 12, 471, 12, 18 - pulse);

            if (_sanity <= _oozeThreshold)
                DrawTube(402, 12, 432, 44, pulse / 2);
        }

        private void DrawTube(int x0, int y0, int x1, int y1, int pulse)
        {
            DrawLine(x0, y0, x1, y1, DeepShadow, 5);
            DrawLine(x0, y0, x1, y1, new Color32(22, 143, 136, 230), 3);
            DrawLine(x0, y0, x1, y1, new Color32(74, 167, 255, 170), 1);

            int length = Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0));
            if (length <= 0)
                return;

            float progress = (pulse % length) / (float)length;
            int px = Mathf.RoundToInt(Mathf.Lerp(x0, x1, progress));
            int py = Mathf.RoundToInt(Mathf.Lerp(y0, y1, progress));
            DrawRect(px - 2, py - 1, 5, 3, Teal);
        }

        private void DrawNoticeboardOoze()
        {
            int creep = Mathf.Abs((_frame % 10) - 5);
            Color32 wetShadow = new(51, 44, 48, 215);

            // A single attached growth escapes from behind the board. Tapered bends
            // and droplets keep it organic instead of reading as a black UI panel.
            DrawCircle(397, 65, 4, Soot);
            DrawLine(398, 67, 402, 80 + creep / 2, wetShadow, 5);
            DrawLine(402, 80 + creep / 2, 398, 94 + creep, wetShadow, 4);
            DrawLine(398, 94 + creep, 401, 105 + creep, Soot, 2);
            DrawCircle(401, 108 + creep, 2, Soot);
            Plot(401, 109 + creep, Teal);

            DrawLine(394, 70, 389, 81, Soot, 2);
            DrawCircle(388, 83, 2, wetShadow);
        }

        private void DrawCabinetPressure()
        {
            // The top drawer is forced open by thin jointed "fingers". It remains a
            // filing cabinet first, anomaly second.
            DrawRect(426, 88, 34, 3, DeepShadow);
            DrawRect(429, 91, 28, 3, Soot);
            for (int finger = 0; finger < 4; finger++)
            {
                int rootX = 432 + finger * 7;
                int twitch = ((_frame + finger) % 3) - 1;
                int endX = rootX - 4 + finger * 2 + twitch;
                int endY = 102 + (finger % 2) * 5;
                DrawLine(rootX, 92, rootX - 1, 98, Soot, 3);
                DrawLine(rootX - 1, 98, endX, endY, Soot, 2);
                DrawCircle(endX, endY, 1, DeepGreen);
                Plot(rootX, 94, Teal);
            }

            DrawRect(418, 104, 10, 5, YellowedPaper);
            DrawRect(420, 105, 6, 1, WarmGrey);
        }

        private void DrawDoorTentacle()
        {
            int twitch = (_frame % 3) - 1;
            DrawRect(92, 48, 6, 47, DeepShadow);
            DrawLine(97, 93, 110, 106 + twitch, Soot, 7);
            DrawLine(110, 106 + twitch, 126, 114, Soot, 6);
            DrawLine(126, 114, 142, 111 - twitch, Soot, 4);
            DrawLine(142, 111 - twitch, 157, 103, Soot, 3);

            for (int i = 0; i < 5; i++)
            {
                int x = 111 + i * 9;
                int y = 110 + (i > 1 ? 4 - i : i * 2);
                DrawRect(x, y, 2, 1, i == 4 ? Teal : YellowedPaper);
            }
        }

        private void DrawVoidLampPool()
        {
            int wobble = (_frame % 4) - 2;
            DrawLine(410, 14, 410 + wobble, 46, DeepShadow, 3);
            DrawRect(407 + wobble, 44, 7, 2, DeepShadow);
            DrawRect(403 + wobble, 46, 15, 3, Soot);
            DrawRect(400 + wobble, 49, 21, 3, DeepShadow);
            DrawRect(407 + wobble, 52, 7, 3, Teal);

            // A translucent, irregular light pool stains the desktop without covering
            // its props or reading as an opaque rectangle.
            DrawEllipse(343 + wobble, 162, 30, 9, new Color32(8, 21, 19, 165));
            DrawEllipse(343 + wobble, 162, 19, 5, new Color32(5, 12, 12, 205));
            DrawRect(321 + wobble, 158, 11, 1, Teal);
            DrawRect(354 + wobble, 166, 14, 1, new Color32(32, 214, 199, 150));
        }

        private void DrawRupturePixels()
        {
            Plot(452, 95, Magenta);
            if (_frame % 3 == 0)
                Plot(454, 94, Magenta);
        }

        private void DrawMemo(int x, int y, int width, int height, int variant)
        {
            Color32 colour = variant == 1 ? YellowedPaper : Paper;
            DrawRect(x, y, width, height, colour);
            for (int row = y + 4; row < y + height - 2; row += 4)
                DrawRect(x + 3, row, Mathf.Max(2, width - 6 - variant * 2), 1, WarmGrey);
            Plot(x + width / 2, y + 1, Brass);
        }

        private void DrawHand(int x, int y, float angle, int length, Color32 colour)
        {
            int endX = x + Mathf.RoundToInt(Mathf.Cos(angle) * length);
            int endY = y + Mathf.RoundToInt(Mathf.Sin(angle) * length);
            DrawLine(x, y, endX, endY, colour, 1);
        }

        private void DrawCircle(int centerX, int centerY, int radius, Color32 colour)
        {
            int radiusSquared = radius * radius;
            for (int y = -radius; y <= radius; y++)
            {
                for (int x = -radius; x <= radius; x++)
                {
                    if (x * x + y * y <= radiusSquared)
                        Plot(centerX + x, centerY + y, colour);
                }
            }
        }

        private void DrawEllipse(
            int centerX, int centerY, int radiusX, int radiusY, Color32 colour)
        {
            int radiusXSquared = radiusX * radiusX;
            int radiusYSquared = radiusY * radiusY;
            int threshold = radiusXSquared * radiusYSquared;
            for (int y = -radiusY; y <= radiusY; y++)
            {
                for (int x = -radiusX; x <= radiusX; x++)
                {
                    int distance = x * x * radiusYSquared + y * y * radiusXSquared;
                    if (distance <= threshold)
                        Plot(centerX + x, centerY + y, colour);
                }
            }
        }

        private void DrawLine(
            int x0, int y0, int x1, int y1, Color32 colour, int thickness)
        {
            int deltaX = Mathf.Abs(x1 - x0);
            int stepX = x0 < x1 ? 1 : -1;
            int deltaY = -Mathf.Abs(y1 - y0);
            int stepY = y0 < y1 ? 1 : -1;
            int error = deltaX + deltaY;

            while (true)
            {
                int half = Mathf.Max(0, thickness / 2);
                DrawRect(x0 - half, y0 - half, Mathf.Max(1, thickness), Mathf.Max(1, thickness), colour);
                if (x0 == x1 && y0 == y1)
                    break;

                int doubled = error * 2;
                if (doubled >= deltaY)
                {
                    error += deltaY;
                    x0 += stepX;
                }

                if (doubled <= deltaX)
                {
                    error += deltaX;
                    y0 += stepY;
                }
            }
        }

        private void DrawRect(int x, int y, int width, int height, Color32 colour)
        {
            int minX = Mathf.Clamp(x, 0, TextureWidth);
            int minY = Mathf.Clamp(y, 0, TextureHeight);
            int maxX = Mathf.Clamp(x + width, 0, TextureWidth);
            int maxY = Mathf.Clamp(y + height, 0, TextureHeight);
            for (int row = minY; row < maxY; row++)
            {
                int index = (TextureHeight - 1 - row) * TextureWidth + minX;
                for (int column = minX; column < maxX; column++)
                    _pixels[index++] = colour;
            }
        }

        private void Plot(int x, int y, Color32 colour)
        {
            if ((uint)x >= (uint)TextureWidth || (uint)y >= (uint)TextureHeight)
                return;
            _pixels[(TextureHeight - 1 - y) * TextureWidth + x] = colour;
        }

        private static Color32 Hex(string value)
        {
            if (!ColorUtility.TryParseHtmlString(value, out Color colour))
                throw new ArgumentException($"Invalid colour {value}", nameof(value));
            return colour;
        }
    }
}
