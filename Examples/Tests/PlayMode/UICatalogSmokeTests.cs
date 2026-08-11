using System;
using System.Collections;
using System.Collections.Generic;
using Aexxa.CanvasCore;
using Aexxa.CanvasCore.Examples;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Aexxa.CanvasCore.Examples.Tests.PlayMode
{
    /// <summary>
    /// Regression test for the example UICatalog.asset: spawns and despawns every registered entry once.
    /// Catches a bad resourcePath, a stale typeAssemblyQualifiedName after a rename/move (see the catalog
    /// Inspector's "Sync Path / Type From Prefab" note), or a prefab missing a required child reference —
    /// all of which currently only surface as a runtime exception the first time a player actually opens
    /// that screen. Views whose OnSpawn requires a non-null context (ConfirmPopup, any UIToast) need a
    /// minimal valid one registered below, keyed by resourcePath. Everything else defaults to a null context.
    /// </summary>
    public class UICatalogSmokeTests
    {
        private const string CatalogPath = "Assets/Plugins/aexxa/CanvasCore/Examples/ScriptableObjects/UICatalog.asset";
        private const string RootPrefabPath = "Assets/Plugins/aexxa/CanvasCore/Prefabs/UIRoot.prefab";

        private static readonly Dictionary<string, Func<object>> ContextFactories = new()
        {
            {
                "UI/Popup/Confirm/ConfirmPopup", () =>
                    new ConfirmPopup.Context("Smoke Test", "message", null, null)
            },
            { "UI/Toast/Simple/SimpleToast", () => new UIToast.Context("smoke test toast", 0.1f) },
        };

        [UnityTest]
        public IEnumerator EveryCatalogEntry_CanSpawnAndDespawn_WithoutException()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<UICatalogSO>(CatalogPath);
            var rootPrefab = AssetDatabase.LoadAssetAtPath<UIRootCanvas>(RootPrefabPath);

            Assert.IsNotNull(catalog, $"Catalog asset not found at '{CatalogPath}'.");
            Assert.IsNotNull(rootPrefab, $"Root prefab not found at '{RootPrefabPath}'.");
            Assert.IsTrue(catalog.Entries.Count > 0, "Catalog has no entries to smoke-test.");

            var root = UnityEngine.Object.Instantiate(rootPrefab);
            var manager = new UIManager(catalog, root);

            yield return null;

            foreach (var entry in catalog.Entries)
            {
                var type = entry.ViewType;
                Assert.IsNotNull(type,
                    $"Entry '{entry.ResourcePath}' has an unresolvable Type — click 'Sync Path / Type From Prefab' on it in the catalog Inspector.");

                var context = ContextFactories.TryGetValue(entry.ResourcePath, out var factory) ? factory() : null;

                UIView view = null;
                Assert.DoesNotThrow(() => view = manager.Spawn(type, context), $"Spawning '{type.Name}' threw.");
                Assert.IsNotNull(view, $"Spawning '{type.Name}' returned null.");

                yield return null;

                Assert.DoesNotThrow(() => manager.Despawn(view), $"Despawning '{type.Name}' threw.");

                yield return null;
            }

            UnityEngine.Object.Destroy(root.gameObject);
        }
    }
}
