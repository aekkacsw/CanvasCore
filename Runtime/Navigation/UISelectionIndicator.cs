using UnityEngine;
using UnityEngine.EventSystems;

namespace Aexxa.CanvasCore
{
    /// <summary>
    /// Shows something — a frame, a glow, an arrow — while this control is the selected one, and hides it
    /// again when it is not.
    ///
    /// <para>This exists because Unity's default answer is not visible. A Button's stock ColorBlock uses
    /// <c>selected = (0.96, 0.96, 0.96)</c>: a four percent darkening, multiplied onto whatever colour the
    /// button already is. On a saturated or a dark button that is nothing at all — the selection is genuinely
    /// there, every navigation event works, and the player cannot see where they are. It is the single most
    /// common reason gamepad support "does not work" in a project where it does.</para>
    ///
    /// <para>A separate object rather than a colour is also the honest fix: a highlight that has to survive
    /// any button colour, any theme, and a dark mode cannot be a tint of the thing it is highlighting.</para>
    ///
    /// <para>Put it on the Selectable, point it at a child to switch on. Pointer hover deliberately does not
    /// trigger it — hover already has its own visual, and a frame following the mouse would be noise.</para>
    /// </summary>
    [AddComponentMenu("Canvas Core/UI Selection Indicator")]
    [DisallowMultipleComponent]
    public sealed class UISelectionIndicator : MonoBehaviour, ISelectHandler, IDeselectHandler
    {
        [SerializeField]
        [Tooltip("Shown while this control is selected. Usually a frame or glow sitting behind the button graphic.")]
        private GameObject indicator;

        private void OnEnable()
        {
            // A pooled view can come back while it is still the EventSystem's selected object, in which case
            // no select event will be sent — the state has to be read rather than waited for.
            Apply(EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject);
        }

        private void OnDisable() => Apply(false);

        public void OnSelect(BaseEventData eventData) => Apply(true);

        public void OnDeselect(BaseEventData eventData) => Apply(false);

        private void Apply(bool selected)
        {
            if (indicator != null)
            {
                indicator.SetActive(selected);
            }
        }
    }
}
