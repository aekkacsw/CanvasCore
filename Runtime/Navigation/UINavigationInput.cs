using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Aexxa.CanvasCore
{
    /// <summary>
    /// Where CanvasCore asks about raw input. By default it reads fixed keys and buttons for whichever
    /// backend the project has enabled; assign <see cref="Source"/> to answer from your own Input Actions
    /// instead — see <see cref="IUINavigationInput"/>.
    ///
    /// Compile-time backend detection rather than a package dependency or a per-project wiring step — the
    /// same approach UIBootstrap already uses, and for the same reason: a UI framework that only works on one
    /// input backend is a UI framework half the projects that install it cannot use.
    /// </summary>
    public static class UINavigationInput
    {
        private static IUINavigationInput _source = new DefaultUINavigationInput();

        /// <summary>
        /// Who answers the input questions. Replace once at startup to hook CanvasCore up to your own
        /// bindings — including rebindable ones. Setting null restores the built-in reader rather than
        /// leaving the UI unable to hear anything.
        /// </summary>
        public static IUINavigationInput Source
        {
            get => _source;
            set => _source = value ?? new DefaultUINavigationInput();
        }

        public static bool NavigatedThisFrame() => _source.NavigatedThisFrame();

        public static bool CancelPressedThisFrame() => _source.CancelPressedThisFrame();

        public static bool PointerActivityThisFrame() => _source.PointerActivityThisFrame();
    }

    /// <summary>
    /// The built-in reader: arrows, d-pad, left stick, Tab for navigation; Escape and B/Circle for cancel;
    /// mouse movement, clicks, and touches for pointer activity. Fixed bindings on purpose — it exists so a
    /// project works before anyone has set anything up, not to be the final word (see
    /// <see cref="UINavigationInput.Source"/>).
    /// </summary>
    public sealed class DefaultUINavigationInput : IUINavigationInput
    {
        /// <summary>Stick deflection past which an axis counts as a deliberate direction press rather than drift.</summary>
        private const float AxisThreshold = 0.5f;

        /// <summary>Mouse movement below this is noise — a desk knock should not dismiss a gamepad highlight.</summary>
        private const float PointerMoveThreshold = 2f;

        public bool NavigatedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            var gamepad = Gamepad.current;

            if (gamepad != null
                && (gamepad.dpad.up.wasPressedThisFrame
                    || gamepad.dpad.down.wasPressedThisFrame
                    || gamepad.dpad.left.wasPressedThisFrame
                    || gamepad.dpad.right.wasPressedThisFrame
                    || gamepad.leftStick.ReadValue().sqrMagnitude > AxisThreshold * AxisThreshold))
            {
                return true;
            }

            var keyboard = Keyboard.current;

            if (keyboard != null
                && (keyboard.upArrowKey.wasPressedThisFrame
                    || keyboard.downArrowKey.wasPressedThisFrame
                    || keyboard.leftArrowKey.wasPressedThisFrame
                    || keyboard.rightArrowKey.wasPressedThisFrame
                    || keyboard.tabKey.wasPressedThisFrame))
            {
                return true;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.UpArrow)
                || Input.GetKeyDown(KeyCode.DownArrow)
                || Input.GetKeyDown(KeyCode.LeftArrow)
                || Input.GetKeyDown(KeyCode.RightArrow)
                || Input.GetKeyDown(KeyCode.Tab))
            {
                return true;
            }

            // Wrapped: a project that renamed or removed the default axes throws here rather than returning 0,
            // and an input probe is never worth taking the game down for.
            try
            {
                if (Mathf.Abs(Input.GetAxisRaw("Vertical")) > AxisThreshold
                    || Mathf.Abs(Input.GetAxisRaw("Horizontal")) > AxisThreshold)
                {
                    return true;
                }
            }
            catch (System.ArgumentException)
            {
            }
#endif
            return false;
        }

        public bool CancelPressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            if (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame)
            {
                return true;
            }

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                return true;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.JoystickButton1))
            {
                return true;
            }
#endif
            return false;
        }

        public bool PointerActivityThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;

            if (mouse != null
                && (mouse.delta.ReadValue().sqrMagnitude > PointerMoveThreshold * PointerMoveThreshold
                    || mouse.leftButton.wasPressedThisFrame
                    || mouse.rightButton.wasPressedThisFrame))
            {
                return true;
            }

            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
            {
                return true;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetMouseButtonDown(0)
                || Input.GetMouseButtonDown(1)
                || new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")).sqrMagnitude > 0.0001f)
            {
                return true;
            }
#endif
            return false;
        }
    }
}
