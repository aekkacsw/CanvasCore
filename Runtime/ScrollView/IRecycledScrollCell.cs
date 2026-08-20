namespace Aexxa.CanvasCore
{
    /// <summary>Implemented by a recycled list's cell prefab: "you are now showing item <paramref name="index"/>". Called every time the cell is reused, so it must set everything it displays rather than only what changed.</summary>
    public interface IRecycledScrollCell
    {
        void Bind(int index);
    }

    /// <summary>
    /// Optional companion to <see cref="IRecycledScrollCell"/> for lists a player navigates with a gamepad or
    /// keyboard: the cell draws its own highlighted state instead of relying on a Selectable inside it.
    ///
    /// That indirection is the point. A Selectable in a recycled cell is a highlight attached to a GameObject
    /// that will shortly be showing a different item — scroll a few rows and the highlight is either on the
    /// wrong item or gone entirely, because Unity's selection follows objects and a virtualized list's
    /// objects do not follow items. <see cref="RecycledScrollNavigator"/> keeps the selection on an
    /// <em>index</em> and tells whichever cell currently holds it to look selected.
    /// </summary>
    public interface IRecycledScrollCellSelection
    {
        void SetSelected(bool selected);
    }
}
