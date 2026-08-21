using System;
using Aexxa.CanvasCore;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Aexxa.CanvasCore.Examples
{
    public sealed class ConfirmPopup : UIPopup
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text messageText;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;

        private Action _onConfirm;
        private Action _onCancel;

        public readonly struct Context
        {
            public readonly string Title;
            public readonly string Message;
            public readonly Action OnConfirm;
            public readonly Action OnCancel;

            public Context(string title, string message, Action onConfirm, Action onCancel)
            {
                Title = title;
                Message = message;
                OnConfirm = onConfirm;
                OnCancel = onCancel;
            }
        }

        // Close On Backdrop Click is unchecked on this prefab — a confirm dialog needs an explicit
        // Confirm/Cancel choice, an accidental outside click shouldn't silently dismiss it.
        public override void OnCreated()
        {
            base.OnCreated();
            confirmButton.onClick.AddListener(HandleConfirm);
            cancelButton.onClick.AddListener(HandleCancel);
        }

        public override void OnSpawn(object context)
        {
            var ctx = (Context)context;
            titleText.text = ctx.Title;
            messageText.text = ctx.Message;
            _onConfirm = ctx.OnConfirm;
            _onCancel = ctx.OnCancel;
        }

        public override void OnDespawn()
        {
            _onConfirm = null;
            _onCancel = null;
        }

        private void HandleConfirm()
        {
            _onConfirm?.Invoke();
            UIManager.Instance.Hide<ConfirmPopup>();
        }

        private void HandleCancel()
        {
            _onCancel?.Invoke();
            UIManager.Instance.Hide<ConfirmPopup>();
        }
    }
}
