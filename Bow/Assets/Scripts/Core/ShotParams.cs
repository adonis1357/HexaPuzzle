using System;

namespace Bow.Core
{
    /// <summary>
    /// 한 발의 사격 입력. 네트워크로 그대로 전송되며, 양쪽 클라이언트가 동일하게 시뮬레이션한다.
    /// angleDeg는 전방(상대 방향) 기준 각도(수평 0°, 수직 90°)이고 facing은 MatchSetup이 결정한다.
    /// </summary>
    [Serializable]
    public struct ShotParams
    {
        public int shooterId;
        public float angleDeg;     // 조준 각도 (오차 적용 전)
        public float power;        // 0~1
        public float deviation;    // 추 미터 오프셋 p ∈ [-1, 1]
        public float launchTime;   // 경기 시간 (초)
        public float breathEff;    // 발사 시 호흡 효율 E (오차 감소용, 0~1)

        public ShotParams(int shooterId, float angleDeg, float power, float deviation, float launchTime, float breathEff)
        {
            this.shooterId = shooterId;
            this.angleDeg = angleDeg;
            this.power = power;
            this.deviation = deviation;
            this.launchTime = launchTime;
            this.breathEff = breathEff;
        }

        public override string ToString()
        {
            return "Shot[p" + shooterId + " ang=" + angleDeg.ToString("F1") + " pow=" + power.ToString("F2")
                 + " dev=" + deviation.ToString("F2") + " t=" + launchTime.ToString("F2") + "]";
        }
    }

    public enum HitZone { None = 0, Head = 1, Body = 2, Ground = 3, Out = 4, Obstacle = 5 }

    /// <summary>탄도 시뮬레이션 결과</summary>
    public sealed class ArrowFlight
    {
        /// <summary>고정 스텝 간격의 위치 (simStep 간격)</summary>
        public Vec2[] points;
        public float stepSeconds;
        public HitZone zone;
        public int targetId;       // 맞춘 궁수 id, 없으면 -1
        public Vec2 hitPoint;
        public float flightTime;
        public bool perfect;
        public ShotParams shot;

        public bool HitArcher { get { return zone == HitZone.Head || zone == HitZone.Body; } }

        public Vec2 PositionAt(float elapsed)
        {
            if (points == null || points.Length == 0) return Vec2.Zero;
            float f = elapsed / stepSeconds;
            int i = (int)f;
            if (i < 0) return points[0];
            if (i >= points.Length - 1) return points[points.Length - 1];
            float u = f - i;
            return points[i] * (1f - u) + points[i + 1] * u;
        }

        public Vec2 VelocityDirAt(float elapsed)
        {
            if (points == null || points.Length < 2) return new Vec2(1f, 0f);
            float f = elapsed / stepSeconds;
            int i = (int)f;
            if (i < 0) i = 0;
            if (i >= points.Length - 1) i = points.Length - 2;
            return (points[i + 1] - points[i]).Normalized;
        }
    }
}
