namespace Aexxa.CanvasCore
{
    /// <summary>Persistent backdrop/decoration. Singleton per type like UIScreen/UIPopup (use UIManager.Show/Hide), but its layer isn't stacked — it never participates in HandleBack() and normally stays shown for the whole session.</summary>
    public abstract class UIBackground : UIView
    {
    }
}
