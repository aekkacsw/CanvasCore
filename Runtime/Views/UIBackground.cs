namespace Aexxa.CanvasCore
{
    /// <summary>Persistent backdrop/decoration. Singleton per type like UIScreen/UIPopup (use UIManager.Show/Hide), but its layer isn't stacked — it never participates in HandleBack() and normally stays shown for the whole session.</summary>
    public abstract class UIBackground : UIView
    {
        /// <summary>A background is shown once at boot and never hidden, so taking focus would mean taking it permanently.</summary>
        public override bool TakesFocus => false;
    }
}
