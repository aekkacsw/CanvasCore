namespace Aexxa.CanvasCore.Examples
{
    /// <summary>
    /// Persistent app backdrop, shown once at boot and never hidden — demonstrates the Background layer
    /// (UILayerId.Background, sortingOrder 0, drawn behind every other layer, not part of the back-stack).
    /// </summary>
    public sealed class AppBackground : UIBackground
    {
    }
}
