using Aexxa.CanvasCore;
using TMPro;
using UnityEngine;

namespace Aexxa.CanvasCore.Examples
{
    public sealed class SimpleToast : UIToast
    {
        [SerializeField] private TMP_Text messageText;

        protected override void SetMessage(string message)
        {
            messageText.text = message;
        }
    }
}
