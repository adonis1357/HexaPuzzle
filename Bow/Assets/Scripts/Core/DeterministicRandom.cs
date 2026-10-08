namespace Bow.Core
{
    /// <summary>
    /// 플랫폼 독립 결정론 난수 (xorshift32).
    /// 네트워크 대전에서 양쪽이 같은 seed로 같은 값을 얻기 위해 System.Random 대신 사용한다.
    /// </summary>
    public sealed class DeterministicRandom
    {
        private uint state;

        public DeterministicRandom(int seed)
        {
            state = (uint)seed;
            if (state == 0u) state = 0x9E3779B9u;
            // 초기 편향 제거용 워밍업
            for (int i = 0; i < 4; i++) NextUInt();
        }

        public uint NextUInt()
        {
            uint x = state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            state = x;
            return x;
        }

        /// <summary>[0, 1) 구간 float</summary>
        public float NextFloat()
        {
            return (NextUInt() >> 8) * (1f / 16777216f);
        }

        /// <summary>[min, max) 구간 float</summary>
        public float Range(float min, float max)
        {
            return min + (max - min) * NextFloat();
        }

        /// <summary>[min, max) 구간 int</summary>
        public int Range(int min, int max)
        {
            if (max <= min) return min;
            return min + (int)(NextUInt() % (uint)(max - min));
        }

        /// <summary>평균 0, 표준편차 sigma의 가우시안 (Box-Muller)</summary>
        public float Gaussian(float sigma)
        {
            float u1 = NextFloat();
            float u2 = NextFloat();
            if (u1 < 1e-7f) u1 = 1e-7f;
            float mag = System.MathF.Sqrt(-2f * System.MathF.Log(u1));
            return mag * System.MathF.Cos(2f * System.MathF.PI * u2) * sigma;
        }
    }
}
