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
        private OfficeVisualPressureState _lastPressure = (OfficeVisualPressureState)(-1);

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
            if (snapshot == null || _environment == null || snapshot.Pressure == _lastPressure)
                return;
            _lastPressure = snapshot.Pressure;
            _environment.color = snapshot.Pressure switch
            {
                OfficeVisualPressureState.Rush => new Color(1f, 0.89f, 0.72f),
                OfficeVisualPressureState.Break => new Color(0.78f, 0.58f, 0.61f),
                OfficeVisualPressureState.Recovery => new Color(0.82f, 1f, 0.88f),
                OfficeVisualPressureState.Result => new Color(0.94f, 0.9f, 0.78f),
                _ => Color.white,
            };
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
