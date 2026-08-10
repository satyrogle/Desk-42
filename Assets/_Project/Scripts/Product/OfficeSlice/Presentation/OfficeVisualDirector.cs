using System;
using System.Collections.Generic;
using UnityEngine;

namespace Desk42.Product.OfficeSlice
{
    public sealed class OfficeVisualDirector
    {
        public const string RootName = "Office Slice M4 Visual Root";
        private readonly Transform _root;
        private readonly OfficeSpriteCatalog _catalog;
        private readonly List<string> _activeAssetIds = new(64);
        private SpriteRenderer _environment;
        private GameObject _rushOverlay;
        private GameObject _breakOverlay;
        private GameObject _recoveryOverlay;
        private GameObject _fastTrays;
        private GameObject _fastTraysTierTwo;
        private GameObject _calmChairs;
        private GameObject _calmChairsTierTwo;
        private GameObject _redLabels;
        private GameObject _redLabelsTierTwo;
        private GameObject _shiftTwoDressing;
        private GameObject _shiftThreeDressing;
        private GameObject _frontDeskCounter;
        private GameObject _paperCheck;
        private GameObject _moneyTrace;
        private GameObject _autoSorter;
        private GameObject _copyEcho;
        private GameObject _ghostClock;
        private GameObject _supervisorStamp;
        private OfficeVisualPressureState _lastPressure = (OfficeVisualPressureState)(-1);
        private int _lastShift = -1;
        private int _lastUpgradeMask = -1;
        private int _lastUpgradeTiers = -1;
        private int _lastMachineMask = -1;

        public IReadOnlyList<string> ActiveAssetIds => _activeAssetIds;
        public int ActiveVisualObjectCount => _root == null ? 0 : CountActive(_root);
        public OfficeVisualPool VfxPool { get; private set; }
        public bool UsedFallback { get; private set; }

        public OfficeVisualDirector(Transform root, OfficeSpriteCatalog catalog)
        {
            _root = root ?? throw new ArgumentNullException(nameof(root));
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _root.name = RootName;
        }

        public void BuildEnvironment()
        {
            GameObject view = CreateSpriteObject(
                "M4 Authored Office Environment",
                "environment.office.base",
                Vector3.zero,
                Vector3.one,
                0);
            _environment = view.GetComponent<SpriteRenderer>();
            CreateDepartmentLandmarks();
            _rushOverlay = CreateOverlay("M4 Rush Overlay", "environment.state.rush", 10);
            _breakOverlay = CreateOverlay("M4 Break Overlay", "environment.state.break", 11);
            _recoveryOverlay = CreateOverlay("M4 Recovery Overlay", "environment.state.recovery", 12);
            _fastTrays = CreateSpriteObject("Fast Trays Upgrade",
                "upgrade.fast-trays.tier-1", SimulationToVisual(-6.5f, 5f),
                new Vector3(0.75f, 0.75f, 1f), 32);
            _fastTraysTierTwo = CreateSpriteObject("Fast Trays Upgrade Tier Two",
                "upgrade.fast-trays.tier-2", SimulationToVisual(-6.5f, 5f),
                new Vector3(0.75f, 0.75f, 1f), 33);
            _calmChairs = CreateSpriteObject("Calm Chairs Upgrade",
                "upgrade.calm-chairs.tier-1", SimulationToVisual(-2f, -3.5f),
                new Vector3(0.75f, 0.75f, 1f), 32);
            _calmChairsTierTwo = CreateSpriteObject("Calm Chairs Upgrade Tier Two",
                "upgrade.calm-chairs.tier-2", SimulationToVisual(-2f, -3.5f),
                new Vector3(0.75f, 0.75f, 1f), 33);
            _redLabels = CreateSpriteObject("Red Labels Upgrade",
                "upgrade.red-labels.tier-1", SimulationToVisual(-8f, 5f),
                new Vector3(0.6f, 0.6f, 1f), 33);
            _redLabelsTierTwo = CreateSpriteObject("Red Labels Upgrade Tier Two",
                "upgrade.red-labels.tier-2", SimulationToVisual(-8f, 5f),
                new Vector3(0.6f, 0.6f, 1f), 34);
            _shiftTwoDressing = CreateSpriteObject("Shift Two Dressing",
                "environment.shift.2-dressing", SimulationToVisual(-12f, -5f),
                new Vector3(0.7f, 0.7f, 1f), 30);
            _shiftThreeDressing = CreateSpriteObject("Shift Three Dressing",
                "environment.shift.3-dressing", SimulationToVisual(11f, 5f),
                new Vector3(0.7f, 0.7f, 1f), 31);
            CreateMachineTargets();
            Sprite poolSprite = _catalog.ResolveOrFallback("vfx.paper-pickup", out bool used);
            UsedFallback |= used;
            VfxPool = new OfficeVisualPool(_root, poolSprite, 32);
            SetEnvironmentState(OfficeVisualPressureState.Calm, 1, 0, 0, 0);
        }

        public GameObject CreateSpriteObject(
            string objectName,
            string assetId,
            Vector3 position,
            Vector3 scale,
            int sortingOrder)
        {
            Sprite sprite = _catalog.ResolveOrFallback(assetId, out bool usedFallback);
            UsedFallback |= usedFallback;
            var view = new GameObject(objectName);
            view.transform.SetParent(_root, false);
            view.transform.position = position;
            view.transform.localScale = scale;
            SpriteRenderer renderer = view.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = sortingOrder;
            if (!_activeAssetIds.Contains(assetId)) _activeAssetIds.Add(assetId);
            return view;
        }

        public bool SetSprite(Transform view, string assetId)
        {
            if (view == null || !view.TryGetComponent(out SpriteRenderer renderer)) return false;
            Sprite sprite = _catalog.ResolveOrFallback(assetId, out bool usedFallback);
            UsedFallback |= usedFallback;
            if (sprite == null) return false;
            if (renderer.sprite != sprite) renderer.sprite = sprite;
            if (!_activeAssetIds.Contains(assetId)) _activeAssetIds.Add(assetId);
            return !usedFallback;
        }

        public void Apply(OfficeVisualSnapshot snapshot)
        {
            if (snapshot == null || _environment == null) return;
            int upgradeMask = (snapshot.FastTraysVisible ? 1 : 0) |
                (snapshot.CalmChairsVisible ? 2 : 0) |
                (snapshot.RedLabelsVisible ? 4 : 0);
            int upgradeTiers = snapshot.FastTraysTier |
                (snapshot.CalmChairsTier << 2) | (snapshot.RedLabelsTier << 4);
            int machineMask = (snapshot.CopyEchoActive ? 1 : 0) |
                (snapshot.GhostClockActive ? 2 : 0) |
                (snapshot.PromotionCascadeActive ? 4 : 0) |
                (snapshot.AutomationRuleActive ? 8 : 0) |
                (snapshot.PayrollRuleActive ? 16 : 0);
            if (snapshot.Pressure == _lastPressure && snapshot.ShiftOrdinal == _lastShift &&
                upgradeMask == _lastUpgradeMask && upgradeTiers == _lastUpgradeTiers &&
                machineMask == _lastMachineMask) return;
            SetEnvironmentState(snapshot.Pressure, snapshot.ShiftOrdinal, upgradeMask,
                upgradeTiers, machineMask);
        }

        private void SetEnvironmentState(
            OfficeVisualPressureState pressure,
            int shiftOrdinal,
            int upgradeMask,
            int upgradeTiers,
            int machineMask)
        {
            bool pressureChanged = pressure != _lastPressure;
            _lastPressure = pressure;
            _lastShift = shiftOrdinal;
            _lastUpgradeMask = upgradeMask;
            _lastUpgradeTiers = upgradeTiers;
            _lastMachineMask = machineMask;
            _environment.color = pressure switch
            {
                OfficeVisualPressureState.Rush => new Color(1f, 0.89f, 0.72f),
                OfficeVisualPressureState.Break => new Color(0.78f, 0.58f, 0.61f),
                OfficeVisualPressureState.Recovery => new Color(0.82f, 1f, 0.88f),
                OfficeVisualPressureState.Result => new Color(0.94f, 0.9f, 0.78f),
                _ => Color.white,
            };
            _rushOverlay.SetActive(pressure == OfficeVisualPressureState.Rush);
            _breakOverlay.SetActive(pressure == OfficeVisualPressureState.Break);
            _recoveryOverlay.SetActive(pressure == OfficeVisualPressureState.Recovery);
            _fastTrays.SetActive((upgradeMask & 1) != 0);
            _fastTraysTierTwo.SetActive((upgradeTiers & 3) >= 2);
            if (_fastTraysTierTwo.activeSelf) _fastTrays.SetActive(false);
            _calmChairs.SetActive((upgradeMask & 2) != 0);
            _calmChairsTierTwo.SetActive(((upgradeTiers >> 2) & 3) >= 2);
            if (_calmChairsTierTwo.activeSelf) _calmChairs.SetActive(false);
            _redLabels.SetActive((upgradeMask & 4) != 0);
            _redLabelsTierTwo.SetActive(((upgradeTiers >> 4) & 3) >= 2);
            if (_redLabelsTierTwo.activeSelf) _redLabels.SetActive(false);
            _shiftTwoDressing.SetActive(shiftOrdinal >= 2);
            _shiftThreeDressing.SetActive(shiftOrdinal >= 3);
            SetSprite(_frontDeskCounter.transform,
                pressure == OfficeVisualPressureState.Break
                    ? "machine.front-desk-counter.warning"
                    : pressure == OfficeVisualPressureState.Rush
                        ? "machine.front-desk-counter.active"
                        : "machine.front-desk-counter.idle");
            SetSprite(_autoSorter.transform, (machineMask & 8) != 0
                ? "machine.auto-sorter.active" : "machine.auto-sorter.idle");
            SetSprite(_copyEcho.transform, (machineMask & 1) != 0
                ? "machine.copy-echo.break"
                : (machineMask & 8) != 0
                    ? "machine.copy-echo.warning" : "machine.copy-echo.idle");
            _ghostClock.SetActive(shiftOrdinal >= 2);
            SetSprite(_ghostClock.transform, (machineMask & 2) != 0
                ? "machine.ghost-clock.break"
                : (machineMask & 16) != 0
                    ? "machine.ghost-clock.active" : "machine.ghost-clock.idle");
            _supervisorStamp.SetActive(shiftOrdinal >= 3);
            SetSprite(_supervisorStamp.transform, (machineMask & 4) != 0
                ? "machine.supervisor-stamp.break" : "machine.supervisor-stamp.idle");
            SetSprite(_paperCheck.transform, pressure == OfficeVisualPressureState.Break
                ? "machine.paper-check.warning" : "machine.paper-check.idle");
            SetSprite(_moneyTrace.transform, (machineMask & 16) != 0
                ? "machine.money-trace.active" : "machine.money-trace.idle");

            if (pressureChanged && VfxPool != null)
            {
                VfxPool.ReleaseAll();
                string effect = pressure switch
                {
                    OfficeVisualPressureState.Break => "vfx.promotion-cascade-ink-fracture",
                    OfficeVisualPressureState.Recovery => "vfx.recovery-complete",
                    OfficeVisualPressureState.Result => "vfx.shift-close",
                    OfficeVisualPressureState.Rush => "vfx.customer-mood-rise",
                    _ => string.Empty,
                };
                if (!string.IsNullOrEmpty(effect)) RequestVfx(effect, Vector3.zero);
            }
        }

        public GameObject RequestVfx(string assetId, Vector3 position)
        {
            Sprite sprite = _catalog.ResolveOrFallback(assetId, out bool usedFallback);
            UsedFallback |= usedFallback;
            if (sprite == null || VfxPool == null) return null;
            if (!_activeAssetIds.Contains(assetId)) _activeAssetIds.Add(assetId);
            return VfxPool.Request(position, sprite);
        }

        private void CreateMachineTargets()
        {
            _frontDeskCounter = CreateSpriteObject("Front Desk Counter Machine",
                "machine.front-desk-counter.idle", SimulationToVisual(-9f, 4.5f),
                new Vector3(0.8f, 0.8f, 1f), 35);
            _paperCheck = CreateSpriteObject("Paper Check Table", "machine.paper-check.idle",
                SimulationToVisual(0f, 4.5f), new Vector3(0.8f, 0.8f, 1f), 35);
            _moneyTrace = CreateSpriteObject("Money Trace Machine", "machine.money-trace.idle",
                SimulationToVisual(8.5f, 4.5f), new Vector3(0.8f, 0.8f, 1f), 35);
            _autoSorter = CreateSpriteObject("Auto Sorter", "machine.auto-sorter.idle",
                SimulationToVisual(10f, -3.5f), new Vector3(0.8f, 0.8f, 1f), 36);
            _copyEcho = CreateSpriteObject("Copy Echo", "machine.copy-echo.idle",
                SimulationToVisual(5.5f, -3.5f), new Vector3(0.8f, 0.8f, 1f), 36);
            _ghostClock = CreateSpriteObject("Ghost Clock Terminal", "machine.ghost-clock.idle",
                SimulationToVisual(2.2f, 5f), new Vector3(0.7f, 0.7f, 1f), 36);
            _supervisorStamp = CreateSpriteObject("Supervisor Stamp",
                "machine.supervisor-stamp.idle", SimulationToVisual(5.5f, -3.5f),
                new Vector3(0.58f, 0.58f, 1f), 37);
        }

        private void CreateDepartmentLandmarks()
        {
            CreateSpriteObject("Front Desk Counter", "environment.kit.counter",
                SimulationToVisual(-9f, 5f), new Vector3(1.6f, 1.2f, 1f), 20);
            CreateSpriteObject("Paper Room Shelves", "environment.kit.shelf",
                SimulationToVisual(0f, 5f), new Vector3(1.15f, 1.15f, 1f), 20);
            CreateSpriteObject("Money Room Vault", "environment.kit.vault",
                SimulationToVisual(8.5f, 5f), new Vector3(1.15f, 1.15f, 1f), 20);
            CreateSpriteObject("Waiting Area Chair", "environment.kit.chair",
                SimulationToVisual(-1f, -3.5f), Vector3.one, 20);
            CreateSpriteObject("Weird Room Impossible Door", "environment.kit.impossible-door",
                SimulationToVisual(11.5f, -3.5f), Vector3.one, 20);
            CreateSpriteObject("Waiting Area Plant", "environment.kit.plant",
                SimulationToVisual(-4.8f, -3.7f), new Vector3(0.65f, 0.65f, 1f), 21);
            CreateSpriteObject("Authored Route Overlay", "environment.route.overlay",
                SimulationToVisual(0f, 0f), new Vector3(0.7f, 0.7f, 1f), 18);
            string socketId = "environment.interaction.socket";
            Vector3 socketScale = new(0.22f, 0.22f, 1f);
            CreateSpriteObject("Front Desk Authored Socket", socketId,
                SimulationToVisual(-9f, 2f), socketScale, 22);
            CreateSpriteObject("Paper Room Authored Socket", socketId,
                SimulationToVisual(0f, 2f), socketScale, 22);
            CreateSpriteObject("Money Room Authored Socket", socketId,
                SimulationToVisual(8f, 2f), socketScale, 22);
            CreateSpriteObject("Weird Room Authored Socket", socketId,
                SimulationToVisual(8f, -6f), socketScale, 22);
        }

        private GameObject CreateOverlay(string name, string id, int sortingOrder)
        {
            GameObject overlay = CreateSpriteObject(name, id, Vector3.zero, Vector3.one, sortingOrder);
            overlay.SetActive(false);
            return overlay;
        }

        public static Vector3 SimulationToVisual(float x, float z, float depth = 0f)
        {
            return new Vector3(x * 0.5f, z * 0.45f, depth);
        }

        public static int SortingOrder(float simulationZ)
        {
            return 100 - Mathf.RoundToInt(simulationZ * 4f);
        }

        public static int ActiveRootCount()
        {
            Transform[] roots = UnityEngine.Object.FindObjectsOfType<Transform>();
            int count = 0;
            for (int i = 0; i < roots.Length; i++)
                if (roots[i].gameObject.activeInHierarchy && roots[i].name == RootName) count++;
            return count;
        }

        private static int CountActive(Transform parent)
        {
            int count = parent.gameObject.activeInHierarchy ? 1 : 0;
            for (int i = 0; i < parent.childCount; i++) count += CountActive(parent.GetChild(i));
            return count;
        }
    }
}
