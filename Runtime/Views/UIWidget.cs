namespace Aexxa.CanvasCore
{
    /// <summary>Repeatable, high-frequency element (floating text, toast, HUD marker). Use UIManager.Spawn/Despawn — multiple instances of the same type can be alive at once.</summary>
    public abstract class UIWidget : UIView
    {
        /// <summary>Widgets are things the game puts on screen, not places the player is sent — floating damage numbers taking the highlight off the button under the cursor is the failure this prevents. A widget that IS interactive can override this back to true.</summary>
        public override bool TakesFocus => false;
    }
}
