using System.Collections;
using UnityEngine;

namespace Aexxa.CanvasCore
{
    /// <summary>
    /// Auto-dismissing widget for transient notifications. Spawned/despawned like any other UIWidget
    /// (multiple instances can be alive at once), but times out and despawns itself instead of waiting
    /// for an explicit Hide() call. Arrange several concurrent toasts visually with a VerticalLayoutGroup
    /// on the Toast layer's container — this class only owns the message + auto-dismiss timer.
    /// </summary>
    public abstract class UIToast : UIWidget
    {
        public readonly struct Context
        {
            public readonly string Message;
            public readonly float Duration;

            public Context(string message, float duration)
            {
                Message = message;
                Duration = duration;
            }
        }

        private Coroutine _dismissRoutine;

        /// <summary>Assign the message to whatever text component this concrete toast uses.</summary>
        protected abstract void SetMessage(string message);

        public override void OnSpawn(object context)
        {
            var ctx = (Context)context;
            SetMessage(ctx.Message);
            _dismissRoutine = StartCoroutine(DismissAfter(ctx.Duration));
        }

        public override void OnDespawn()
        {
            if (_dismissRoutine != null)
            {
                StopCoroutine(_dismissRoutine);
                _dismissRoutine = null;
            }
        }

        private IEnumerator DismissAfter(float duration)
        {
            yield return new WaitForSeconds(duration);
            _dismissRoutine = null;
            UIManager.Instance.Despawn(this);
        }
    }
}
