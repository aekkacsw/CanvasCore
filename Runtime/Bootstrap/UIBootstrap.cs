using Aexxa.CanvasCore;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public sealed class UIBootstrap : MonoBehaviour
{
    [SerializeField] private UICatalogSO catalog;
    [SerializeField] private UIRootCanvas rootPrefab;
    [Tooltip("Close the top-most stacked Popup/Screen on Escape (Android's hardware back button is mapped " +
        "to Escape by both Unity input backends). Auto-detects whichever backend this project has enabled " +
        "(legacy Input Manager and/or the new Input System) at compile time, so there's nothing to wire per " +
        "project. Turn off to handle back input yourself (gamepad button, custom Input Actions asset) and " +
        "call Manager.HandleBack() directly.")]
    [SerializeField] private bool autoHandleBackInput = true;

    public UIManager Manager { get; private set; }

    private void Awake()
    {
        var rootInstance = Instantiate(rootPrefab);
        DontDestroyOnLoad(rootInstance.gameObject);
        Manager = new UIManager(catalog, rootInstance);
    }

    private void Update()
    {
        if (autoHandleBackInput && EscapePressedThisFrame())
        {
            Manager.HandleBack();
        }
    }

    private static bool EscapePressedThisFrame()
    {
        var pressed = false;

#if ENABLE_INPUT_SYSTEM
        pressed |= Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        pressed |= Input.GetKeyDown(KeyCode.Escape);
#endif

        return pressed;
    }
}
