using System;
using System.Collections.Generic;
using UnityEngine;

namespace Aexxa.CanvasCore
{
    /// <summary>
    /// One language's entry in a <see cref="LocalizedAsset{T}"/>. A separate non-generic type because Unity
    /// serializes a plain class far more predictably than a nested generic one.
    /// </summary>
    [Serializable]
    public sealed class LocalizedAssetVariant
    {
        [SerializeField]
        [Tooltip("Locale code this variant is for — must match a Locale Code in CanvasCoreSettings, e.g. \"th\".")]
        private string localeCode = string.Empty;

        [SerializeField]
        [Tooltip("Path passed to Resources.Load — relative to a folder literally named \"Resources\", with no file extension.")]
        private string resourcePath = string.Empty;

        public string LocaleCode => localeCode;

        public string ResourcePath => resourcePath;
    }

    /// <summary>
    /// A per-language asset: the sprite with baked-in words, the recorded voice line, the logo that differs by
    /// market. <c>Localization</c> itself only ever deals in strings — this is the small piece that covers
    /// everything else.
    ///
    /// <code>
    /// [SerializeField] private LocalizedAsset&lt;Sprite&gt; banner;
    /// [SerializeField] private LocalizedAsset&lt;AudioClip&gt; greeting;
    ///
    /// image.sprite = banner.Load();          // null when this language has no variant — check before assigning
    /// audioSource.PlayOneShot(greeting.Load());
    /// </code>
    ///
    /// <para>Paths rather than direct references, for the same reason <see cref="LocaleDescriptor"/> holds a
    /// path: Unity eager-loads everything reachable from a loaded object, so a list of direct references would
    /// pull <i>every</i> language's asset into memory the moment the component holding it loads — which for
    /// audio or full-screen art is the entire cost this class exists to avoid.</para>
    ///
    /// <para>Deliberately not a component and deliberately not cached. Resources.Load returns the same
    /// instance for repeated calls, so calling <see cref="Load"/> at the point of use is cheap and leaves the
    /// question of when to release ownership — <c>Resources.UnloadUnusedAssets</c> — with the code that knows
    /// the answer.</para>
    /// </summary>
    [Serializable]
    public class LocalizedAsset<T> where T : UnityEngine.Object
    {
        [SerializeField]
        [Tooltip("Used for any language with no variant of its own. Leave empty for \"this asset only exists in some languages\".")]
        private string defaultResourcePath = string.Empty;

        [SerializeField]
        [Tooltip("Per-language overrides. A language not listed here uses the default path.")]
        private List<LocalizedAssetVariant> variants = new();

        /// <summary>The asset for the active language, or null when neither it nor the default path resolves.</summary>
        public T Load() => Load(Localization.CurrentLocaleCode);

        /// <summary>The asset for a specific language — for a preview, or for playing one line in a language other than the one being read.</summary>
        public T Load(string localeCode)
        {
            var path = ResolvePath(localeCode);

            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            var asset = Resources.Load<T>(path);

            if (asset == null)
            {
                Debug.LogError($"LocalizedAsset<{typeof(T).Name}>: nothing at Resources path '{path}' for locale '{localeCode}'. The asset must sit under a folder literally named \"Resources\", and the path carries no file extension.");
            }

            return asset;
        }

        /// <summary>Load without the null check at every call site — false means this language has nothing and neither does the default.</summary>
        public bool TryLoad(out T asset)
        {
            asset = Load();
            return asset != null;
        }

        /// <summary>Which path this language resolves to, before anything is loaded. Exposed for tooling and tests — and for a preflight check that every variant points somewhere real.</summary>
        public string ResolvePath(string localeCode)
        {
            if (variants != null && !string.IsNullOrEmpty(localeCode))
            {
                foreach (var variant in variants)
                {
                    if (variant != null
                        && !string.IsNullOrEmpty(variant.ResourcePath)
                        && string.Equals(variant.LocaleCode, localeCode, StringComparison.OrdinalIgnoreCase))
                    {
                        return variant.ResourcePath;
                    }
                }
            }

            return defaultResourcePath;
        }
    }
}
