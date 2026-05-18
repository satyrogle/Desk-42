// ============================================================
// DESK 42 — Service Registry
//
// Lightweight type-keyed singleton registry. Replaces
// FindObjectOfType<T> calls in runtime code.
//
// Usage:
//   // Provider (MonoBehaviour Awake / OnDestroy):
//   void Awake()    => Desk42Services.Register(this);
//   void OnDestroy() => Desk42Services.Unregister<MyManager>();
//
//   // Consumer:
//   var mgr = Desk42Services.Get<MyManager>();        // returns null if not found
//   if (Desk42Services.TryGet(out MyManager m)) { … } // explicit null check
//
// Rules:
//   - One instance per type. Registering a second instance of
//     the same type overwrites the first (logs a warning).
//   - Not thread-safe. Unity main thread only.
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Desk42.Core
{
    public static class Desk42Services
    {
        static readonly Dictionary<Type, object> _registry = new();

        public static void Register<T>(T service) where T : class
        {
            var key = typeof(T);
            if (_registry.ContainsKey(key))
                Debug.LogWarning($"[Desk42Services] Overwriting existing registration for {key.Name}.");
            _registry[key] = service;
        }

        public static void Unregister<T>() where T : class
            => _registry.Remove(typeof(T));

        public static T Get<T>() where T : class
        {
            _registry.TryGetValue(typeof(T), out var obj);
            return obj as T;
        }

        public static bool TryGet<T>(out T service) where T : class
        {
            if (_registry.TryGetValue(typeof(T), out var obj) && obj is T typed)
            {
                service = typed;
                return true;
            }
            service = null;
            return false;
        }

        // Editor-only reset so entering Play Mode in the Editor starts clean.
#if UNITY_EDITOR
        [UnityEditor.InitializeOnEnterPlayMode]
        static void OnEnterPlayMode() => _registry.Clear();
#endif
    }
}
