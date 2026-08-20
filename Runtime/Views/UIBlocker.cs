namespace Aexxa.CanvasCore
{
    /// <summary>
    /// Full-screen input blocker (loading spinner, etc). UIManager.Show&lt;T&gt;()/Hide&lt;T&gt;() are
    /// reference-counted for UIBlocker types specifically — two independent callers (e.g. a network
    /// request and an asset load) can each Show it and the blocker only actually hides once both have
    /// called Hide. Its layer isn't stacked — it never participates in HandleBack().
    /// </summary>
    public abstract class UIBlocker : UIView
    {
        /// <summary>
        /// A blocker exists to stop input reaching what is behind it, so it holds focus while it is up and
        /// blocks the view below — otherwise a pad could still navigate and press the buttons of the screen a
        /// loading spinner is covering, which is exactly the thing the blocker was shown to prevent.
        /// </summary>
        public override bool BlocksInteractionBelow => true;
    }
}
