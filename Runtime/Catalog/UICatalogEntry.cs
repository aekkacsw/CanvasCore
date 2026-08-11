using System;
using UnityEngine;

namespace Aexxa.CanvasCore
{
    /// <summary>
    /// Deliberately holds no direct UnityEngine.Object reference to the UI prefab at runtime.
    /// Unity eager-loads every object reachable through a direct serialized reference, so if this
    /// entry held a "UIView prefab" field, every prefab listed in a UICatalogSO would be pulled into
    /// memory the moment anything referencing that catalog (e.g. a bootstrapper in the scene) loads —
    /// regardless of whether that screen is ever shown. Storing a Resources-relative path instead
    /// means the prefab is only loaded on first UIPoolManager.Get&lt;T&gt;() for that type.
    /// </summary>
    [Serializable]
    public sealed class UICatalogEntry
    {
        [SerializeField] private string resourcePath;
        [SerializeField] private string typeAssemblyQualifiedName;

        public UILayerId layer = UILayerId.Screen;

        public bool prewarmOnBoot;

        [Min(0)] public int prewarmCount = 1;

        [Min(1)] public int maxPoolSize = 10;

        /// <summary>Path passed to Resources.Load&lt;UIView&gt;(...) — relative to a folder literally named "Resources".</summary>
        public string ResourcePath => resourcePath;

        private Type _cachedType;

        public Type ViewType
        {
            get
            {
                if (_cachedType == null && !string.IsNullOrEmpty(typeAssemblyQualifiedName))
                {
                    _cachedType = Type.GetType(typeAssemblyQualifiedName);
                }

                return _cachedType;
            }
        }
    }
}
