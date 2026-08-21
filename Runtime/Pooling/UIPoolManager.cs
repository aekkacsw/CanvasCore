using System;
using System.Collections.Generic;
using UnityEngine;

namespace Aexxa.CanvasCore
{
    public sealed class UIPoolManager
    {
        private readonly UICatalogSO _catalog;
        private readonly Func<UILayerId, Transform> _resolveParent;
        private readonly Dictionary<Type, UIElementPool> _pools = new();

        public UIPoolManager(UICatalogSO catalog, Func<UILayerId, Transform> resolveParent)
        {
            _catalog = catalog;
            _resolveParent = resolveParent;
        }

        public void PrewarmBootEntries()
        {
            foreach (var entry in _catalog.Entries)
            {
                if (entry.prewarmOnBoot)
                {
                    GetOrCreatePool(entry);
                }
            }
        }

        public UIView Get<T>() where T : UIView => Get(typeof(T));

        public UIView Get(Type viewType)
        {
            var entry = _catalog.Get(viewType);
            return GetOrCreatePool(entry).Get();
        }

        public void Release(UIView instance)
        {
            var type = instance.GetType();

            if (_pools.TryGetValue(type, out var pool))
            {
                pool.Release(instance);
            }
            else
            {
                Debug.LogWarning($"UIPoolManager: releasing '{type.Name}' with no matching pool — destroying instead.", instance);
                UnityEngine.Object.Destroy(instance.gameObject);
            }
        }

        private UIElementPool GetOrCreatePool(UICatalogEntry entry)
        {
            var type = entry.ViewType;

            if (!_pools.TryGetValue(type, out var pool))
            {
                var prefab = CanvasCoreResources.Load<UIView>(entry.ResourcePath);

                if (prefab == null)
                {
                    throw new InvalidOperationException(
                        $"UIPoolManager: Resources.Load<UIView>(\"{entry.ResourcePath}\") returned null for '{type?.Name}'. " +
                        "Check the entry's resource path and that the prefab sits inside a folder literally named 'Resources'.");
                }

                var parent = _resolveParent(entry.layer);
                pool = new UIElementPool(prefab, parent, entry.prewarmCount, entry.maxPoolSize);
                _pools.Add(type, pool);
            }

            return pool;
        }
    }
}
