using System;

namespace Bow.Core
{
    /// <summary>
    /// seed로부터 결정되는 경기장 배치: 두 궁수의 거리, 높이 차, 지형 함수.
    /// 플레이어 0은 왼쪽(x&lt;0, 오른쪽을 향함), 플레이어 1은 오른쪽(x&gt;0, 왼쪽을 향함).
    /// </summary>
    public sealed class MatchSetup
    {
        public int Seed { get; private set; }
        public float Distance { get; private set; }
        public float HeightDiff { get; private set; }

        private readonly Vec2[] feet = new Vec2[2];

        public MatchSetup(int seed, DuelConfig cfg)
        {
            Seed = seed;
            DeterministicRandom rng = new DeterministicRandom(seed ^ 0x2F1A3C);
            Distance = rng.Range(cfg.minDistance, cfg.maxDistance);
            HeightDiff = rng.Range(-cfg.maxHeightDiff, cfg.maxHeightDiff);
            // 지면 기준선(y=0)은 두 궁수 발 높이의 평균 (아트 §2.1)
            feet[0] = new Vec2(-Distance * 0.5f, -HeightDiff * 0.5f);
            feet[1] = new Vec2(Distance * 0.5f, HeightDiff * 0.5f);
        }

        /// <summary>궁수 발 위치</summary>
        public Vec2 Feet(int playerId) { return feet[playerId == 0 ? 0 : 1]; }

        /// <summary>전방 방향 (+1 오른쪽, −1 왼쪽)</summary>
        public static int Facing(int playerId) { return playerId == 0 ? 1 : -1; }

        /// <summary>
        /// 지형 높이. 두 궁수 사이는 smoothstep으로 이어지고 바깥은 평탄.
        /// </summary>
        public float GroundHeight(float x)
        {
            float x0 = feet[0].x, x1 = feet[1].x;
            if (x <= x0) return feet[0].y;
            if (x >= x1) return feet[1].y;
            float u = (x - x0) / (x1 - x0);
            float s = u * u * (3f - 2f * u);
            // 가운데가 살짝 꺼진 들판 느낌 (최대 0.6m)
            float dip = -0.6f * MathF.Sin(u * MathF.PI);
            return feet[0].y + (feet[1].y - feet[0].y) * s + dip;
        }
    }
}
