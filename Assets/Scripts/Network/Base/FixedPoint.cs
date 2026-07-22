using System;
using UnityEngine;

namespace Network
{
    /// <summary>
    /// 定点数 Q16.16
    /// </summary>
    /// <remarks>
    /// 存储值 = 实际值 * 65536<br/>
    /// 范围：约 [-32768, 32767.99998]<br/>
    /// 原始值（raw value）保证在 int 范围内，方便网络传输，高32位禁用
    /// </remarks>
    public struct FixedPoint
    {
        private long _value;
        private const int FractionBits = 16;
        
        /// <summary>
        /// 表示 单位1 的原始值 (65536)
        /// </summary>
        public const long One=1L<<FractionBits;
        
        private const long MaxRawValue = int.MaxValue;
        private const long MinRawValue = int.MinValue;

        public static FixedPoint FromFloat(float value)
        {
            return new FixedPoint((long)Mathf.Round(value * One));
        }

        public static FixedPoint FromInt(int value)
        {
            return new FixedPoint((long)value << FractionBits);
        }

        /// <summary>
        /// 从原始数据创建定点数
        /// </summary>
        /// <param name="value">原始整数值（已缩放的值）</param>
        /// <returns>定点数实例</returns>
        /// <warning>
        /// <para>警告：此方法直接操作原始缩放值，绕过正常的浮点/整数转换逻辑。</para>
        /// <para>使用前请确保 value 已经正确缩放（即 value = 实际值 × 65536）。</para>
        /// <para>错误的传入值会破坏定点数的数值语义，导致后续运算错误。</para>
        /// <para>仅在以下场景使用：网络同步、序列化/反序列化、底层数据交换。</para>
        /// </warning>
        public static FixedPoint FromRawValue(int value)
        {
            return new FixedPoint((long)value);
        }
        
        private FixedPoint(long value)
        {
            if (value > MaxRawValue || value < MinRawValue)
                throw new OverflowException("value out of range");
            _value = value;
        }
        
        public float ToFloat()
        {
            if (_value > MaxRawValue || _value < MinRawValue)
                throw new OverflowException("value out of range");
            return (float)_value/One;
        }
        
        /// <summary>
        /// 获取原始整数值（已缩放的值）
        /// </summary>
        /// <returns>原始整数值</returns>
        /// <warning>
        /// 警告：返回值是已缩放的值（实际值 × 65536），不是实际的浮点数或整数。
        /// 主要用于网络传输和底层存储。
        /// </warning>
        public int GetRawValue()
        {
            if (_value > MaxRawValue || _value < MinRawValue)
                throw new OverflowException("value out of range");
            return (int)(_value);
        }

        public static FixedPoint operator +(FixedPoint a, FixedPoint b)
        {
            return new FixedPoint(a._value + b._value);
        }

        public static FixedPoint operator -(FixedPoint a, FixedPoint b)
        {
            return new FixedPoint(a._value - b._value);
        }
        
        public static FixedPoint operator *(FixedPoint a, FixedPoint b)
        {
            long value = a._value * b._value;
            return new FixedPoint((value)>>FractionBits);
        }
        
        public static FixedPoint operator /(FixedPoint a, FixedPoint b)
        {
            if (b._value == 0)
                throw new DivideByZeroException();
            return new FixedPoint((a._value<<FractionBits)/ b._value);
        }
        
    }
    
    /// <summary>
    /// 定点数 Vector3 (Q16.16)
    /// </summary>
    /// <remarks>
    /// 每个分量均为 Q16.16 定点数<br/>
    /// 存储值 = 实际值 × 65536<br/>
    /// 范围：约 [-32768, 32767.99998] 每分量<br/>
    /// 原始值（raw value）保证在 int 范围内，方便网络传输，高32位禁用
    /// </remarks>
    public struct FixedPointVector3
    {
        private FixedPoint _x, _y, _z;
        
        /// <summary>
        /// 零向量
        /// </summary>
        public static readonly FixedPointVector3 zero = FixedPointVector3.FromVector3(Vector3.zero);

        
        
        public FixedPointVector3(FixedPoint x, FixedPoint y, FixedPoint z)
        {
            _x = x;
            _y = y;
            _z = z;
        }
        
        /// <summary>
        /// 从原始数据创建定点数向量
        /// </summary>
        /// <param name="x">X 分量原始值</param>
        /// <param name="y">Y 分量原始值</param>
        /// <param name="z">Z 分量原始值</param>
        /// <returns>定点数向量实例</returns>
        /// <warning>
        /// <para>警告：此方法直接操作原始缩放值，绕过正常的浮点数转换逻辑。</para>
        /// <para>使用前请确保 x, y, z 已经正确缩放（即 value = 实际值 × 65536）。</para>
        /// <para>错误的传入值会破坏定点数的数值语义，导致后续运算错误。</para>
        /// <para>仅在以下场景使用：网络同步、序列化/反序列化、底层数据交换。</para>
        /// </warning>
        public static FixedPointVector3 FromRawValue(int x,int y,int z)
        {
            return new FixedPointVector3(
                FixedPoint.FromRawValue(x),
                FixedPoint.FromRawValue(y),
                FixedPoint.FromRawValue(z)
                );
        }
        
        public static FixedPointVector3 FromVector3(Vector3 vec)
        {
            return new FixedPointVector3(
                FixedPoint.FromFloat(vec.x),
                FixedPoint.FromFloat(vec.y),
                FixedPoint.FromFloat(vec.z)
            );
        }
        
        public static FixedPointVector3 FromFloat(float x,float y,float z)
        {
            return new FixedPointVector3(
                FixedPoint.FromFloat(x),
                FixedPoint.FromFloat(y),
                FixedPoint.FromFloat(z)
            );
        }
        
        public Vector3 ToVector3()
        {
            return new Vector3(_x.ToFloat(), _y.ToFloat(), _z.ToFloat());
        }
        
        // 获取原始整数值（int 形式，定点数缩放后的值）
        public int GetRawX() => _x.GetRawValue();
        public int GetRawY() => _y.GetRawValue();
        public int GetRawZ() => _z.GetRawValue();
        
        // 向量加法
        public static FixedPointVector3 operator +(FixedPointVector3 a, FixedPointVector3 b)
        {
            return new FixedPointVector3(a._x + b._x, a._y + b._y, a._z + b._z);
        }

        // 向量减法
        public static FixedPointVector3 operator -(FixedPointVector3 a, FixedPointVector3 b)
        {
            return new FixedPointVector3(a._x - b._x, a._y - b._y, a._z - b._z);
        }

        // 向量与浮点数乘法（标量乘法）
        public static FixedPointVector3 operator *(FixedPointVector3 vec, float scalar)
        {
            FixedPoint fpScalar = FixedPoint.FromFloat(scalar);
            return new FixedPointVector3(vec._x * fpScalar, vec._y * fpScalar, vec._z * fpScalar);
        }

        // 浮点数与向量乘法（交换律）
        public static FixedPointVector3 operator *(float scalar, FixedPointVector3 vec)
        {
            return vec * scalar;
        }
        
        // 向量与整数乘法
        public static FixedPointVector3 operator *(FixedPointVector3 vec, int scalar)
        {
            FixedPoint fpScalar = FixedPoint.FromInt(scalar);
            return vec*fpScalar;
        }

        // 整数与向量乘法（交换律）
        public static FixedPointVector3 operator *(int scalar, FixedPointVector3 vec)
        {
            return vec * scalar;
        }
        
        // 向量与 FixedPoint 标量乘法
        public static FixedPointVector3 operator *(FixedPointVector3 vec, FixedPoint scalar)
        {
            return new FixedPointVector3(vec._x * scalar, vec._y * scalar, vec._z * scalar);
        }

        // FixedPoint 标量与向量乘法（交换律）
        public static FixedPointVector3 operator *(FixedPoint scalar, FixedPointVector3 vec)
        {
            return vec * scalar;
        }
        
        public static FixedPointVector3 operator /(FixedPointVector3 vec, float scalar)
        {
            if (Mathf.Approximately(scalar, 0f)) throw new DivideByZeroException();
            return vec / FixedPoint.FromFloat(scalar);
        }

        public static FixedPointVector3 operator /(FixedPointVector3 vec, int scalar)
        {
            if (scalar == 0) throw new DivideByZeroException();
            return vec / FixedPoint.FromInt(scalar);
        }

        public static FixedPointVector3 operator /(FixedPointVector3 vec, FixedPoint scalar)
        {
            if (scalar.GetRawValue() == 0) throw new DivideByZeroException();
            FixedPoint one = FixedPoint.FromInt(1);
            FixedPoint inv = one / scalar;
            return new FixedPointVector3(vec._x * inv, vec._y * inv, vec._z * inv);
        }

        // 重写 ToString 便于调试
        public override string ToString()
        {
            return $"({_x.ToFloat():F3}, {_y.ToFloat():F3}, {_z.ToFloat():F3})";
        }
    }
}