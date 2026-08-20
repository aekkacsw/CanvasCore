namespace Aexxa.CanvasCore
{
    /// <summary>What Localization.Get returns when a key is in neither the current nor the fallback table.</summary>
    public enum MissingKeyDisplay
    {
        /// <summary>The key itself, verbatim. Least noisy in a shipped build.</summary>
        Key = 0,

        /// <summary>The key wrapped in #hashes# so an untranslated string is impossible to miss on screen. Default.</summary>
        MarkedKey = 1,

        /// <summary>An empty string — for UI where a missing translation must not disturb layout.</summary>
        Empty = 2,
    }
}
