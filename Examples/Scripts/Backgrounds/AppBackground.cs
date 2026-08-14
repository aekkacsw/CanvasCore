namespace Aexxa.CanvasCore.Examples
{
    /// <summary>
    /// Persistent app backdrop, shown once at boot and never hidden — demonstrates the Background layer
    /// (UILayerId.Background, sortingOrder 0, drawn behind every other layer). A UIWidget rather than a
    /// UIScreen: nothing here participates in back-stack navigation, it just sits there.
    /// </summary>
    public sealed class AppBackground : UIWidget
    {
    }
}
