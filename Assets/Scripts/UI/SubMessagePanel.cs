using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class SubMessagePanel:MonoBehaviour
    {
        [SerializeField]private TMP_Text _text;

        public void setText(string text)
        {
            _text.text = text;
        }

        public void UI_OnElementClick()
        {
            MessagePanel.Instance?.SetMessageInfo(_text.text);
        }
    }
}