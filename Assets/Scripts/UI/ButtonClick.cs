using System;
using UnityEngine;

namespace UI
{
    public class ButtonClick:MonoBehaviour
    {
        public event Action OnClickEvent;
        
        public void UI_ButtonClick()
        {
            OnClickEvent?.Invoke();
        }
        
    }
}