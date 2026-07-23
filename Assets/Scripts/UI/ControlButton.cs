using System;
using TMPro;
using UnityEngine;

namespace UI
{
    public class ControlButton:MonoBehaviour
    {
        [SerializeField] private GameObject _targetObject;
        [SerializeField] private string _showName;
        [SerializeField] private TMP_Text _text;
        [SerializeField] private Vector3 _transformation;
        private RectTransform _targetTransform;
        private bool _isActive=true;

        private void Awake()
        {
            _text.text = _showName+":开";
            _targetTransform = _targetObject.GetComponent<RectTransform>();
        }

        public void OnClick()
        {
            SwitchState();
        }

        private void SwitchState()
        {
            if (_isActive)
            {
                _targetTransform.localPosition+=_transformation;
                _text.text = _showName+":关";
                _isActive = false;
            }
            else
            {
                _targetTransform.localPosition-=_transformation;
                _text.text = _showName+":开";
                _isActive = true;
            }
            
        }
        
        
    }
}