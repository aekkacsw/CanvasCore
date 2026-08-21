#if UNITY_EDITOR
using System.Collections.Generic;
#endif
using UnityEngine;

namespace Aexxa.CanvasCore
{
    /// <summary>
    /// The one place CanvasCore reaches into Resources, so the answer to "which asset did we actually get?"
    /// is answered once rather than at each call site.
    ///
    /// <para>Resources.Load takes a path, not an asset, and a project can hold more than one asset at the same
    /// path — Unity does not define which of them the single-result overload returns. CanvasCore used to ship
    /// its editable assets as ordinary package assets, which put a second "Localization/en" in every project
    /// that imported them, and that is exactly how a key chosen in the Inspector could resolve against a table
    /// nobody had edited. The package now ships those files in folders Unity ignores (see CanvasCoreImporter),
    /// so the duplicate should no longer be possible.</para>
    ///
    /// <para>"Should" is why this class exists. A consumer can still create the collision themselves — a
    /// leftover copy from an older CanvasCore, a second plugin that happens to use the same path, an asset
    /// dragged somewhere convenient. In the Editor this notices and says so, naming both files, instead of
    /// leaving a wrong translation to be found in a build. It cannot check in a player: there is no
    /// AssetDatabase there and an asset's path is not something a build keeps. Detecting it while the project
    /// is still open is the only place the check can pay off, which is what makes the shipping layout — not
    /// this fallback — the actual fix.</para>
    /// </summary>
    internal static class CanvasCoreResources
    {
        /// <summary>Resources.Load, plus an Editor-only complaint when the path turns out to be ambiguous.</summary>
        internal static T Load<T>(string path) where T : Object
        {
            var asset = Resources.Load<T>(path);

#if UNITY_EDITOR
            asset = ReportAmbiguity(asset, path);
#endif

            return asset;
        }

#if UNITY_EDITOR
        /// <summary>
        /// What each path turned out to be, so the scan runs once per path per domain reload. A null value
        /// means "looked, only one asset there". LoadAll walks every Resources folder in the project, which is
        /// far too much work to repeat on a call some caller makes per label per language change — and the
        /// answer cannot change without a reimport, which reloads the domain anyway.
        /// </summary>
        private static readonly Dictionary<string, Object> Verdicts = new Dictionary<string, Object>();

        /// <summary>
        /// Returns the project's own copy when a path resolves to several assets, and says so once. Preferring
        /// Assets/ over a package is what every Editor window here already does, so at least the Inspector and
        /// the running game agree on one table while the duplicate is being cleaned up.
        /// </summary>
        private static T ReportAmbiguity<T>(T asset, string path) where T : Object
        {
            if (asset == null)
            {
                return null;
            }

            if (Verdicts.TryGetValue(path, out var verdict))
            {
                return verdict == null ? asset : (T)verdict;
            }

            var candidates = Resources.LoadAll<T>(path);

            if (candidates.Length < 2)
            {
                Verdicts[path] = null;
                return asset;
            }

            var preferred = asset;

            foreach (var candidate in candidates)
            {
                if (UnityEditor.AssetDatabase.GetAssetPath(candidate).StartsWith("Assets/", System.StringComparison.Ordinal))
                {
                    preferred = candidate;
                    break;
                }
            }

            Verdicts[path] = preferred;

            Debug.LogError(
                $"CanvasCore: {candidates.Length} assets of type {typeof(T).Name} share the Resources path '{path}', " +
                "so which one Resources.Load returns is undefined — a key can resolve against a table you never " +
                "edited, and a build will not necessarily pick what the Editor picks. Falling back to " +
                $"'{UnityEditor.AssetDatabase.GetAssetPath(preferred)}'. Delete or move all but one of: " +
                string.Join(", ", System.Array.ConvertAll(candidates, one => UnityEditor.AssetDatabase.GetAssetPath(one))),
                preferred);

            return preferred;
        }
#endif
    }
}
