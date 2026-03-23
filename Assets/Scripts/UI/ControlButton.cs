using System;
using TMPro;
using UnityEngine;

namespace UI
{
    public class ControlButton:MonoBehaviour
    {
        [SerializeField]private GameObject targetObject;
        [SerializeField]private string showName;
        [SerializeField]private TMP_Text text;
        [SerializeField] private Vector3 tranformation;
        private RectTransform _targetTransform;
        private bool _isActive=true;

        private void Awake()
        {
            text.text = showName+":开";
            _targetTransform = targetObject.GetComponent<RectTransform>();
        }

        public void OnClick()
        {
            SwitchState();
        }

        private void SwitchState()
        {
            if (_isActive)
            {
                _targetTransform.localPosition+=tranformation;
                text.text = showName+":关";
                _isActive = false;
            }
            else
            {
                _targetTransform.localPosition-=tranformation;
                text.text = showName+":开";
                _isActive = true;
            }
            
        }
        
        
    }
}