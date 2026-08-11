using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace Aexxa.CanvasCore
{
    public sealed class UIElementPool
    {
        private readonly ObjectPool<UIView> _pool;

        public UIElementPool(UIView prefab, Transform parent, int prewarmCount, int maxSize)
        {
            _pool = new ObjectPool<UIView>(
                createFunc: () =>
                {
                    var instance = Object.Instantiate(prefab, parent);
                    instance.OnCreated();
                    return instance;
                },
                actionOnGet: instance => instance.gameObject.SetActive(true),
                actionOnRelease: instance =>
                {
                    instance.OnDespawn();
                    instance.gameObject.SetActive(false);
                },
                actionOnDestroy: instance => Object.Destroy(instance.gameObject),
                collectionCheck: true,
                defaultCapacity: Mathf.Max(1, prewarmCount),
                maxSize: Mathf.Max(1, maxSize));

            // Get() all prewarmed instances first, then Release() them — releasing one at a time inside
            // the loop would just recycle the same single instance back out on the next Get().
            var warm = new List<UIView>(prewarmCount);

            for (var i = 0; i < prewarmCount; i++)
            {
                warm.Add(_pool.Get());
            }

            foreach (var instance in warm)
            {
                _pool.Release(instance);
            }
        }

        public UIView Get() => _pool.Get();

        public void Release(UIView instance) => _pool.Release(instance);

        public void Clear() => _pool.Clear();
    }
}
