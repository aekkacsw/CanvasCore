using UnityEngine;

namespace Aexxa.CanvasCore.Examples
{
    /// <summary>
    /// Shows the app's opening UI as soon as the scene starts — the pattern most games need at boot.
    /// Start() (not Awake()) because UIManager.Instance is only ready once UIBootstrap.Awake() has run,
    /// and Unity always finishes every object's Awake() before any object's Start() runs.
    /// </summary>
    public sealed class ExampleBootstrap : MonoBehaviour
    {
        private void Start()
        {
            UIManager.Instance.Show<AppBackground>();
            UIManager.Instance.Show<MainMenuScreen>();
        }
    }
}
