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
    }
}
