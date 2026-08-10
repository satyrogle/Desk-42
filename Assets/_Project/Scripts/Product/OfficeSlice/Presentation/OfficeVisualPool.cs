using System;
using System.Collections.Generic;
using UnityEngine;

namespace Desk42.Product.OfficeSlice
{
    /// <summary>Fixed-capacity presentation pool. Requests never create gameplay objects.</summary>
    public sealed class OfficeVisualPool
    {
        private readonly GameObject[] _items;
        private readonly bool[] _active;
        private int _growthCount;

        public int Capacity => _items.Length;
        public int GrowthCount => _growthCount;

        public OfficeVisualPool(Transform parent, Sprite sprite, int capacity)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
            _items = new GameObject[capacity];
            _active = new bool[capacity];
            for (int i = 0; i < capacity; i++)
            {
                var item = new GameObject("M4 Pooled Visual " + i);
                item.transform.SetParent(parent, false);
                SpriteRenderer renderer = item.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.sortingOrder = 220;
                item.SetActive(false);
                _items[i] = item;
            }
        }

        public GameObject Request(Vector3 position)
        {
            return Request(position, null);
        }

        public GameObject Request(Vector3 position, Sprite sprite)
        {
            for (int i = 0; i < _items.Length; i++)
            {
                if (_active[i]) continue;
                _active[i] = true;
                _items[i].transform.position = position;
                if (sprite != null) _items[i].GetComponent<SpriteRenderer>().sprite = sprite;
                _items[i].SetActive(true);
                return _items[i];
            }
            Debug.LogError("OFFICE_M4_VISUAL_POOL_EXHAUSTED " + _items.Length);
            return null;
        }

        public void ReleaseAll()
        {
            for (int i = 0; i < _items.Length; i++)
            {
                _active[i] = false;
                _items[i].SetActive(false);
            }
        }

        public int ActiveCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _active.Length; i++)
                    if (_active[i]) count++;
                return count;
            }
        }
    }
}
