using System;
using System.Collections.Generic;
using UnityEngine;

namespace Aexxa.CanvasCore
{
    [CreateAssetMenu(menuName = "Aexxa/CanvasCore/UI Catalog", fileName = "UICatalog")]
    public sealed class UICatalogSO : ScriptableObject
    {
        [SerializeField] private List<UICatalogEntry> entries = new();

        private Dictionary<Type, UICatalogEntry> _lookup;

        public IReadOnlyList<UICatalogEntry> Entries => entries;

        public UICatalogEntry Get<T>() where T : UIView => Get(typeof(T));

        public UICatalogEntry Get(Type viewType)
        {
            BuildLookupIfNeeded();

            if (_lookup.TryGetValue(viewType, out var entry))
            {
                return entry;
            }

            throw new KeyNotFoundException($"UICatalogSO '{name}': no entry registered for view type '{viewType.Name}'.");
        }

        public bool TryGet(Type viewType, out UICatalogEntry entry)
        {
            BuildLookupIfNeeded();
            return _lookup.TryGetValue(viewType, out entry);
        }

        private void OnValidate()
        {
            _lookup = null;
        }

        private void BuildLookupIfNeeded()
        {
            if (_lookup != null)
            {
                return;
            }

            _lookup = new Dictionary<Type, UICatalogEntry>();

            foreach (var entry in entries)
            {
                var type = entry.ViewType;

                if (type == null)
                {
                    continue;
                }

                if (!_lookup.TryAdd(type, entry))
                {
                    Debug.LogError($"UICatalogSO '{name}': duplicate entry for view type '{type.Name}'.", this);
                }
            }
        }
    }
}
