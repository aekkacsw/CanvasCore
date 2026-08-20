namespace Aexxa.CanvasCore
{
    /// <summary>
    /// When CanvasCore is allowed to put a selection highlight on screen.
    ///
    /// This is a real decision, not a preference: a forced selection is what makes a pad or keyboard usable
    /// at all, and the same highlight sitting on a button the mouse user never touched looks like a bug. The
    /// default resolves it by waiting — nothing is selected until someone actually presses a direction.
    /// </summary>
    public enum UIFocusMode
    {
        /// <summary>Select something as soon as a view that wants focus opens, whatever the player is holding. Right for a game that is pad-first.</summary>
        Always,

        /// <summary>Stay out of the way until the player presses a d-pad, stick, or arrow key, then take over and keep the selection correct from there on. Right for a game that has to be good with both.</summary>
        OnFirstNavigationInput,

        /// <summary>Never touch the EventSystem's selection. For projects that manage focus themselves.</summary>
        Never,
    }
}
