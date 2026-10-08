using System;

namespace Bow.Core
{
    /// <summary>
    /// UnityEngine 비의존 2D 벡터. 코어 시뮬레이션(탄도/판정)에서 사용한다.
    /// </summary>
    [Serializable]
    public struct Vec2
    {
        public float x;
        public float y;

        public Vec2(float x, float y) { this.x = x; this.y = y; }

        public static readonly Vec2 Zero = new Vec2(0f, 0f);

        public float Length { get { return MathF.Sqrt(x * x + y * y); } }
        public float SqrLength { get { return x * x + y * y; } }

        public Vec2 Normalized
        {
            get
            {
                float len = Length;
                return len > 1e-6f ? new Vec2(x / len, y / len) : Zero;
            }
        }

        public static Vec2 operator +(Vec2 a, Vec2 b) { return new Vec2(a.x + b.x, a.y + b.y); }
        public static Vec2 operator -(Vec2 a, Vec2 b) { return new Vec2(a.x - b.x, a.y - b.y); }
        public static Vec2 operator *(Vec2 a, float s) { return new Vec2(a.x * s, a.y * s); }
        public static Vec2 operator *(float s, Vec2 a) { return new Vec2(a.x * s, a.y * s); }

        public static float Distance(Vec2 a, Vec2 b) { return (a - b).Length; }

        public static Vec2 FromAngleDeg(float angleDeg)
        {
            float rad = angleDeg * (MathF.PI / 180f);
            return new Vec2(MathF.Cos(rad), MathF.Sin(rad));
        }

        public override string ToString() { return "(" + x.ToString("F2") + ", " + y.ToString("F2") + ")"; }
    }
}
