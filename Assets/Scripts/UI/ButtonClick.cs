using System;
using UnityEngine;

namespace UI
{
    /// <summary>
    /// 弃用
    /// </summary>
    
    public class ButtonClick:MonoBehaviour
    {
        public event Action OnClickEvent;
        
        public void UI_ButtonClick()
        {
            OnClickEvent?.Invoke();
        }
        
    }
}