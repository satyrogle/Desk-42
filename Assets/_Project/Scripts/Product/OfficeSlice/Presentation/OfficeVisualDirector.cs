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
        private GameObject _calmChairs;
        private GameObject _redLabels;
        private GameObject _shiftTwoDressing;
        private GameObject _shiftThreeDressing;
        private OfficeVisualPressureState _lastPressure = (OfficeVisualPressureState)(-1);
        private int _lastShift = -1;
        private int _lastUpgradeMask = -1;

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
                "environment.upgrade.fast-trays", SimulationToVisual(-6.5f, 5f),
                new Vector3(0.75f, 0.75f, 1f), 32);
            _calmChairs = CreateSpriteObject("Calm Chairs Upgrade",
                "environment.upgrade.calm-chairs", SimulationToVisual(-2f, -3.5f),
                new Vector3(0.75f, 0.75f, 1f), 32);
            _redLabels = CreateSpriteObject("Red Labels Upgrade",
                "environment.upgrade.red-labels", SimulationToVisual(-8f, 5f),
                new Vector3(0.6f, 0.6f, 1f), 33);
            _shiftTwoDressing = CreateSpriteObject("Shift Two Dressing",
                "environment.shift.2-dressing", SimulationToVisual(-12f, -5f),
                new Vector3(0.7f, 0.7f, 1f), 30);
            _shiftThreeDressing = CreateSpriteObject("Shift Three Dressing",
                "environment.shift.3-dressing", SimulationToVisual(11f, 5f),
                new Vector3(0.7f, 0.7f, 1f), 31);
            SetEnvironmentState(OfficeVisualPressureState.Calm, 1, 0);
            Sprite fallback = _catalog.ResolveOrFallback("fallback.explicit", out bool used);
            UsedFallback |= used;
            VfxPool = new OfficeVisualPool(_root, fallback, 32);
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

        public void Apply(OfficeVisualSnapshot snapshot)
        {
            if (snapshot == null || _environment == null) return;
            int upgradeMask = (snapshot.FastTraysVisible ? 1 : 0) |
                (snapshot.CalmChairsVisible ? 2 : 0) |
                (snapshot.RedLabelsVisible ? 4 : 0);
            if (snapshot.Pressure == _lastPressure && snapshot.ShiftOrdinal == _lastShift &&
                upgradeMask == _lastUpgradeMask) return;
            SetEnvironmentState(snapshot.Pressure, snapshot.ShiftOrdinal, upgradeMask);
        }

        private void SetEnvironmentState(
            OfficeVisualPressureState pressure,
            int shiftOrdinal,
            int upgradeMask)
        {
            _lastPressure = pressure;
            _lastShift = shiftOrdinal;
            _lastUpgradeMask = upgradeMask;
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
            _calmChairs.SetActive((upgradeMask & 2) != 0);
            _redLabels.SetActive((upgradeMask & 4) != 0);
            _shiftTwoDressing.SetActive(shiftOrdinal >= 2);
            _shiftThreeDressing.SetActive(shiftOrdinal >= 3);
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
            GameObject[] roots = GameObject.FindGameObjectsWithTag("Untagged");
            int count = 0;
            for (int i = 0; i < roots.Length; i++)
                if (roots[i].activeInHierarchy && roots[i].name == RootName) count++;
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
