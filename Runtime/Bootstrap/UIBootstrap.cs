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
        // UIRoot is DontDestroyOnLoad so it (and anything showing on it — Background, Blocker, a
        // persistent HUD) survives scene loads. This object drives auto back-input handling below, so it
        // has to survive right alongside it — otherwise Back/ESC silently stops working after the first
        // scene unloads. If a scene we load into has its own UIBootstrap, it's a duplicate of the one
        // that's already running; destroy it rather than standing up a second UIManager/UIRoot pair, which
        // would silently replace UIManager.Instance out from under everything already showing.
        //
        // Instance.IsAlive (not just Instance != null) is what decides which one is the duplicate:
        // UIManager is a plain C# object, so a merely non-null Instance doesn't guarantee its UIRootCanvas
        // is still around — IsAlive checks that through Unity's own destroyed-object semantics. If a
        // previous Instance's root was destroyed by anything other than this normal duplicate-handling path
        // (an edge case, not something this framework does on its own), that old Instance is the stale one
        // — this UIBootstrap should take over, not defer to it and get destroyed for nothing.
        if (UIManager.Instance != null && UIManager.Instance.IsAlive)
        {
            Destroy(gameObject);
            return;
        }

        DontDestroyOnLoad(gameObject);

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
