namespace Aexxa.CanvasCore
{
    /// <summary>
    /// The three questions CanvasCore asks about raw input, behind an interface so a project can answer them
    /// its own way.
    ///
    /// <para>The built-in answer reads fixed keys and buttons — arrows, d-pad, left stick, Escape, B/Circle —
    /// which is right for getting started and wrong for a game that lets players rebind. Rather than take a
    /// hard dependency on the Input System package (which would lock out every project still on the old
    /// backend), CanvasCore asks through this interface and ships a default implementation. Point
    /// <see cref="UINavigationInput.Source"/> at your own and CanvasCore reads whatever your Input Actions
    /// asset says — including whatever the player rebound it to.</para>
    ///
    /// <code>
    /// // Example: drive CanvasCore from your own Input Actions
    /// public sealed class MyInput : IUINavigationInput
    /// {
    ///     public bool NavigatedThisFrame()      =&gt; _actions.UI.Navigate.WasPressedThisFrame();
    ///     public bool CancelPressedThisFrame()  =&gt; _actions.UI.Cancel.WasPressedThisFrame();
    ///     public bool PointerActivityThisFrame() =&gt; _actions.UI.Point.WasPerformedThisFrame();
    /// }
    ///
    /// UINavigationInput.Source = new MyInput();   // once, at startup
    /// </code>
    /// </summary>
    public interface IUINavigationInput
    {
        /// <summary>True on frames where the player pressed a direction — the signal that a selection highlight is now wanted rather than noise.</summary>
        bool NavigatedThisFrame();

        /// <summary>True on frames where the player pressed cancel/back.</summary>
        bool CancelPressedThisFrame();

        /// <summary>
        /// True on frames where the player used a pointer — moved a mouse, clicked, touched. Used to hand the
        /// screen back to the mouse: a highlight left over from a burst of keyboard use looks like a stuck
        /// button once the player has picked the mouse back up.
        /// </summary>
        bool PointerActivityThisFrame();
    }
}
