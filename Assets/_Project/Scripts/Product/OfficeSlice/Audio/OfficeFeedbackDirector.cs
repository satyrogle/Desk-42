using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Desk42.Product.OfficeSlice
{
    /// <summary>
    /// Bounded, presentation-only tactile feedback derived from routed audio cues.
    /// It never reads or writes deterministic simulation state.
    /// </summary>
    public sealed class OfficeFeedbackDirector
    {
        public const string RootName = "Office Slice M5 Feedback Root";
        public const float MaximumCameraImpulse = 0.08f;
        public const float MaximumRumbleSeconds = 0.16f;

        private readonly Transform _root;
        private readonly Transform _camera;
        private readonly Vector3 _cameraRestPosition;
        private readonly OfficeVisualDirector _visuals;
        private readonly OfficeAudioSettings _settings;
        private float _cameraImpulse;
        private float _uiPulse;
        private float _folderSnap;
        private float _machineRecoil;
        private float _rumbleRemaining;

        public OfficeFeedbackDirector(
            Transform parent,
            Transform camera,
            OfficeVisualDirector visuals,
            OfficeAudioSettings settings)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            DisableExistingRoots(parent);
            _root = new GameObject(RootName).transform;
            _root.SetParent(parent, false);
            _camera = camera;
            _cameraRestPosition = camera == null ? Vector3.zero : camera.localPosition;
            _visuals = visuals;
        }

        public Transform Root => _root;
        public float CameraImpulse => _cameraImpulse;
        public float UiPulse => _uiPulse;
        public float FolderSnap => _folderSnap;
        public float MachineRecoil => _machineRecoil;
        public bool RumbleActive => _rumbleRemaining > 0f && _settings.Rumble;
        public int RumbleRequestCount { get; private set; }
        public int FeedbackRequestCount { get; private set; }
        public int ObjectCount => _root == null ? 0 : 1;
        public int GrowthCount => 0;
        public bool ObscuresInteractionTargets => false;

        public void RouteCue(string cueId, float intensity)
        {
            if (!_settings.FeedbackEnabled || string.IsNullOrWhiteSpace(cueId)) return;
            float strength = Mathf.Clamp01(intensity);
            FeedbackRequestCount++;
            switch (cueId)
            {
                case "folder.take":
                    _folderSnap = Mathf.Max(_folderSnap, 0.7f * strength);
                    RequestVisual("vfx.paper-pickup");
                    RequestRumble(0.08f, 0.05f, 0.06f);
                    break;
                case "folder.send":
                    AddCameraImpulse(0.018f * strength);
                    _uiPulse = Mathf.Max(_uiPulse, 0.28f * strength);
                    RequestVisual("vfx.folder-send-trail");
                    break;
                case "paper.correct":
                case "money.correct":
                    _machineRecoil = Mathf.Max(_machineRecoil, 0.42f * strength);
                    _uiPulse = Mathf.Max(_uiPulse, 0.5f * strength);
                    RequestVisual("vfx.paper-compare-snap");
                    RequestRumble(0.09f, 0.05f, 0.07f);
                    break;
                case "paper.incorrect":
                case "money.incorrect":
                case "action.invalid":
                    _uiPulse = Mathf.Max(_uiPulse, 0.2f * strength);
                    RequestRumble(0.05f, 0.08f, 0.05f);
                    break;
                case "customer.calm-response":
                case "calm.complete":
                    _uiPulse = Mathf.Max(_uiPulse, 0.35f * strength);
                    RequestVisual("vfx.calm-effect");
                    break;
                case "fix.complete":
                case "event.copier-stop":
                    _machineRecoil = Mathf.Max(_machineRecoil, 0.55f * strength);
                    RequestVisual("vfx.machine-stop");
                    RequestRumble(0.12f, 0.04f, 0.08f);
                    break;
                case "automation.match":
                    _uiPulse = Mathf.Max(_uiPulse, 0.32f * strength);
                    RequestVisual("vfx.rule-accepted-tick");
                    break;
                case "automation.copied-accepted":
                    _uiPulse = Mathf.Max(_uiPulse, 0.38f * strength);
                    _machineRecoil = Mathf.Max(_machineRecoil, 0.24f * strength);
                    RequestVisual("vfx.copy-spawn");
                    break;
                case "event.copy-echo-trigger":
                case "event.promotion-trigger":
                    AddCameraImpulse(MaximumCameraImpulse * strength);
                    _machineRecoil = Mathf.Max(_machineRecoil, 0.8f * strength);
                    RequestVisual("vfx.promotion-cascade-ink-fracture");
                    RequestRumble(0.25f, 0.35f, MaximumRumbleSeconds);
                    break;
                case "event.recovery-complete":
                    AddCameraImpulse(0.045f * strength);
                    _uiPulse = Mathf.Max(_uiPulse, 0.8f * strength);
                    RequestVisual("vfx.recovery-complete");
                    RequestRumble(0.16f, 0.1f, 0.12f);
                    break;
                case "event.upgrade-chosen":
                    AddCameraImpulse(0.025f * strength);
                    RequestVisual("vfx.rule-learned-stamp");
                    break;
                case "event.shift-close":
                case "event.final-result":
                    AddCameraImpulse(0.035f * strength);
                    _uiPulse = Mathf.Max(_uiPulse, 0.72f * strength);
                    RequestVisual("vfx.shift-close");
                    break;
            }
        }

        public void Update(float unscaledDeltaTime)
        {
            float delta = Mathf.Max(0f, unscaledDeltaTime);
            _cameraImpulse = Mathf.MoveTowards(_cameraImpulse, 0f, delta * 0.45f);
            _uiPulse = Mathf.MoveTowards(_uiPulse, 0f, delta * 2.6f);
            _folderSnap = Mathf.MoveTowards(_folderSnap, 0f, delta * 3.8f);
            _machineRecoil = Mathf.MoveTowards(_machineRecoil, 0f, delta * 3.2f);
            _rumbleRemaining = Mathf.Max(0f, _rumbleRemaining - delta);
            if (_camera != null)
            {
                float phase = Time.unscaledTime * 41f;
                _camera.localPosition = _cameraRestPosition + new Vector3(
                    Mathf.Sin(phase) * _cameraImpulse,
                    Mathf.Cos(phase * 0.73f) * _cameraImpulse * 0.45f,
                    0f);
            }
            if (_rumbleRemaining <= 0f || !_settings.Rumble)
                StopRumble();
        }

        public void Dispose()
        {
            StopRumble();
            if (_camera != null) _camera.localPosition = _cameraRestPosition;
            if (_root != null) _root.gameObject.SetActive(false);
        }

        public static int ActiveRootCount()
        {
            Transform[] transforms = UnityEngine.Object.FindObjectsOfType<Transform>();
            int count = 0;
            for (int i = 0; i < transforms.Length; i++)
                if (transforms[i].name == RootName &&
                    transforms[i].gameObject.activeInHierarchy) count++;
            return count;
        }

        private void AddCameraImpulse(float amount)
        {
            _cameraImpulse = Mathf.Clamp(
                Mathf.Max(_cameraImpulse, amount), 0f, MaximumCameraImpulse);
        }

        private void RequestRumble(float low, float high, float duration)
        {
            if (!_settings.Rumble) return;
            _rumbleRemaining = Mathf.Max(_rumbleRemaining,
                Mathf.Min(duration, MaximumRumbleSeconds));
            RumbleRequestCount++;
            Gamepad.current?.SetMotorSpeeds(
                Mathf.Clamp01(low), Mathf.Clamp01(high));
        }

        private void StopRumble()
        {
            _rumbleRemaining = 0f;
            Gamepad.current?.SetMotorSpeeds(0f, 0f);
        }

        private void RequestVisual(string assetId)
        {
            _visuals?.RequestVfx(assetId, Vector3.zero);
        }

        private static void DisableExistingRoots(Transform parent)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name == RootName) child.gameObject.SetActive(false);
            }
        }
    }
}
