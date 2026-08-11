namespace Aexxa.CanvasCore
{
    /// <summary>
    /// Implemented by the MonoBehaviour on a RecycledScrollView's cell prefab. A small pool of these is
    /// instantiated once (sized to the viewport, not the item count) and re-bound to whichever index
    /// scrolls into view — look up your own data source by index inside Bind.
    /// </summary>
    public interface IRecycledScrollCell
    {
        void Bind(int index);
    }
}
