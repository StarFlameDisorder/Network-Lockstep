using System;
using Network;
using UnityEngine;

namespace DefaultNamespace
{
    public class Test:MonoBehaviour
    {
        private void Awake()
        {
            FixedPoint p1 = FixedPoint.FromFloat(1.5f);
            FixedPoint p2 = FixedPoint.FromFloat(-2f);
            
            FixedPoint p3 = p1 * p2;
            Debug.Log(p1.ToFloat()+" * "+p2.ToFloat()+" = "+p3.ToFloat());
        }
    }
}