namespace Aexxa.CanvasCore
{
    /// <summary>Where Localization looks for locale CSV files that live outside the build's own assets.</summary>
    public enum ExternalLocaleSource
    {
        /// <summary>Both, StreamingAssets first and persistentDataPath second — so a file the player added wins over the one shipped with the game. The usual choice.</summary>
        StreamingAssetsThenPersistent = 0,

        /// <summary>Only the folder shipped inside the build. Translations stay yours to change on a patch; players cannot add languages.</summary>
        StreamingAssetsOnly = 1,

        /// <summary>Only the writable per-user folder. For games where external files exist purely as a mod/community-translation channel.</summary>
        PersistentDataPathOnly = 2,
    }
}
