using System;

namespace Bow.Core
{
    /// <summary>
    /// 「활」 경기 규칙/밸런스 상수. 브리프(00_프로젝트_브리프.md) 2장의 확정값.
    /// 모든 시스템은 이 인스턴스를 참조하며, 프로토타입에서는 기본값을 그대로 사용한다.
    /// </summary>
    [Serializable]
    public class DuelConfig
    {
        // ---------- 경기 규칙 ----------
        public int maxHp = 100;
        public int bodyDamage = 25;
        public int headDamage = 40;
        public float perfectMultiplier = 1.25f;
        public float matchDuration = 120f;     // 초
        public float minDistance = 16f;        // m
        public float maxDistance = 24f;        // m
        public float maxHeightDiff = 1.5f;     // m (±)

        // ---------- 장애물 (두 궁수 사이 먹 기둥) ----------
        public float obstacleMinHeight = 3.0f;   // m (지면 위)
        public float obstacleMaxHeight = 5.5f;
        public float obstacleHalfWidth = 0.5f;   // 폭 1.0 m
        public float obstacleXJitter = 2.0f;     // 중앙에서 ±2 m

        // ---------- 탄도 ----------
        public float gravity = 9.81f;
        public float minArrowSpeed = 9f;       // 힘 0
        public float maxArrowSpeed = 22f;      // 힘 1
        public float windAccelFactor = 0.6f;   // a_x = factor × w(t)
        public float simStep = 1f / 120f;      // 고정 적분 스텝
        public float maxFlightTime = 8f;
        public float selfHitGraceTime = 0.5f;  // 발사 직후 자기 자신 피격 면제 시간

        // ---------- 바람 (구간형: 유지 → 전환) ----------
        public float windMaxSpeed = 7f;             // 풍속 범위 ±7 m/s
        public float windHoldSeconds = 8f;          // 한 바람이 유지되는 시간
        public float windTransitionSeconds = 2.5f;  // 바람이 바뀌는 데 걸리는 시간 (smoothstep)

        // ---------- 장전 딜레이 & 호흡(Breath) ----------
        public float baseDelay = 5.0f;         // B
        public float breathPeak = 10.0f;       // P
        public float maxDelayReduction = 0.7f; // D_next = B × (1 − 0.7·E)
        public float breathMeterSlowdown = 0.3f; // 추 주기 = base × (1 + 0.3·E)
        public float breathAccuracyBonus = 0.3f; // 각도 오차 = p × 6° × (1 − 0.3·E) (GDD CP-1)

        // ---------- 정확도 추 미터 ----------
        public float meterBasePeriod = 1.1f;   // 추 왕복 주기(초)
        public float meterMaxAngleError = 6f;  // p × 6°
        public float perfectThreshold = 0.12f; // |p| < 0.12 (GDD CP-2)
        public float meterTimeout = 3f;        // 자동 발사

        // ---------- 조준 ----------
        public float minAimAngle = -10f;
        public float maxAimAngle = 85f;
        public float maxDragPixels = 420f;     // 드래그 길이 → 힘 1.0 (1080px 기준)
        public float minDragPixels = 30f;      // 이하면 취소

        // ---------- 히트박스 (m) ----------
        public float headRadius = 0.30f;       // 판정(시각 0.21m보다 관대 — 아트 §3.1)
        public float headCenterY = 1.72f;      // 발 기준
        public float bodyHalfWidth = 0.30f;
        public float bodyBottomY = 0.0f;
        public float bodyTopY = 1.48f;
        public float bowAnchorX = 0.40f;       // 발 기준 화살 출발점 (전방)
        public float bowAnchorY = 1.45f;

        public float ArrowSpeed(float power01)
        {
            if (power01 < 0f) power01 = 0f;
            if (power01 > 1f) power01 = 1f;
            return minArrowSpeed + (maxArrowSpeed - minArrowSpeed) * power01;
        }

        public float ClampAngle(float angleDeg)
        {
            if (angleDeg < minAimAngle) return minAimAngle;
            if (angleDeg > maxAimAngle) return maxAimAngle;
            return angleDeg;
        }
    }
}
