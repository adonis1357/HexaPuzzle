using System;
using System.Collections.Generic;

namespace Bow.Core
{
    /// <summary>
    /// 결정론 탄도 시뮬레이터. 고정 스텝(simStep)으로 적분하며 바람 w(t)를 매 스텝 반영한다.
    /// 양쪽 클라이언트가 같은 ShotParams/seed로 같은 결과를 얻는다.
    /// </summary>
    public static class ArrowSimulator
    {
        public static ArrowFlight Simulate(ShotParams shot, MatchSetup setup, WindModel wind, DuelConfig cfg)
        {
            int shooter = shot.shooterId == 0 ? 0 : 1;
            int target = 1 - shooter;
            int facing = MatchSetup.Facing(shooter);

            Vec2 shooterFeet = setup.Feet(shooter);
            Vec2 origin = new Vec2(shooterFeet.x + facing * cfg.bowAnchorX, shooterFeet.y + cfg.bowAnchorY);

            // 추 미터 오차 적용: p × 6° × (1 − 0.3·E)  (호흡 효율이 높을수록 오차 감소)
            float eff = shot.breathEff < 0f ? 0f : (shot.breathEff > 1f ? 1f : shot.breathEff);
            float errMul = shot.errorMul <= 0f ? 1f : shot.errorMul;
            float angleError = shot.deviation * cfg.meterMaxAngleError * (1f - cfg.breathAccuracyBonus * eff) * errMul;
            float angle = cfg.ClampAngle(shot.angleDeg + angleError);
            float speed = cfg.ArrowSpeed(shot.power) * (shot.speedMul <= 0f ? 1f : shot.speedMul);
            float windMul = shot.windMul < 0f ? 1f : shot.windMul;
            Vec2 vel = new Vec2(facing * MathF.Cos(angle * MathF.PI / 180f) * speed,
                                MathF.Sin(angle * MathF.PI / 180f) * speed);
            bool perfect = MathF.Abs(shot.deviation) < cfg.perfectThreshold;

            float dt = cfg.simStep;
            int maxSteps = (int)(cfg.maxFlightTime / dt) + 1;
            List<Vec2> pts = new List<Vec2>(maxSteps);
            Vec2 pos = origin;
            pts.Add(pos);

            ArrowFlight result = new ArrowFlight();
            result.shot = shot;
            result.stepSeconds = dt;
            result.zone = HitZone.Out;
            result.targetId = -1;
            result.perfect = perfect;

            float t = 0f;
            for (int i = 1; i < maxSteps; i++)
            {
                float matchTime = shot.launchTime + t;
                float ax = wind.GetWindAccel(matchTime) * windMul;
                vel.x += ax * dt;
                vel.y -= cfg.gravity * dt;
                Vec2 prev = pos;
                pos = pos + vel * dt;
                t += dt;
                pts.Add(pos);

                // 궁수 피격 판정 (상대 → 우선, 자기 자신은 유예 시간 이후)
                HitZone z = TestArcher(prev, pos, setup.Feet(target), cfg);
                if (z != HitZone.None)
                {
                    result.zone = z; result.targetId = target; result.hitPoint = pos; result.flightTime = t;
                    break;
                }
                if (t > cfg.selfHitGraceTime)
                {
                    z = TestArcher(prev, pos, shooterFeet, cfg);
                    if (z != HitZone.None)
                    {
                        result.zone = z; result.targetId = shooter; result.hitPoint = pos; result.flightTime = t;
                        break;
                    }
                }

                // 장애물 (먹 기둥)
                if (setup.HitsObstacle(pos) || setup.HitsObstacle((prev + pos) * 0.5f))
                {
                    result.zone = HitZone.Obstacle; result.hitPoint = pos; result.flightTime = t;
                    break;
                }

                // 지면
                float ground = setup.GroundHeight(pos.x);
                if (pos.y <= ground && vel.y < 0f)
                {
                    result.zone = HitZone.Ground; result.hitPoint = new Vec2(pos.x, ground); result.flightTime = t;
                    pts[pts.Count - 1] = result.hitPoint;
                    break;
                }

                // 화면 밖
                if (MathF.Abs(pos.x) > 80f || pos.y < -30f)
                {
                    result.zone = HitZone.Out; result.hitPoint = pos; result.flightTime = t;
                    break;
                }
            }

            if (result.flightTime <= 0f) result.flightTime = t;
            result.points = pts.ToArray();
            return result;
        }

        /// <summary>prev→cur 선분(중점 포함 3점 샘플)으로 머리 원/몸통 사각형 판정</summary>
        private static HitZone TestArcher(Vec2 prev, Vec2 cur, Vec2 feet, DuelConfig cfg)
        {
            Vec2 mid = (prev + cur) * 0.5f;
            HitZone z = TestPoint(cur, feet, cfg);
            if (z != HitZone.None) return z;
            z = TestPoint(mid, feet, cfg);
            if (z != HitZone.None) return z;
            return HitZone.None;
        }

        private static HitZone TestPoint(Vec2 p, Vec2 feet, DuelConfig cfg)
        {
            // 머리 (원)
            float hx = feet.x, hy = feet.y + cfg.headCenterY;
            float dx = p.x - hx, dy = p.y - hy;
            if (dx * dx + dy * dy <= cfg.headRadius * cfg.headRadius) return HitZone.Head;
            // 몸통 (AABB)
            if (MathF.Abs(p.x - feet.x) <= cfg.bodyHalfWidth
                && p.y >= feet.y + cfg.bodyBottomY && p.y <= feet.y + cfg.bodyTopY) return HitZone.Body;
            return HitZone.None;
        }

        /// <summary>
        /// 조준 가이드용: 바람 무시, 초기 duration초 동안의 궤적 점들 (HUD 점 3개 표시용).
        /// </summary>
        public static Vec2[] PreviewNoWind(int shooterId, float angleDeg, float power, MatchSetup setup, DuelConfig cfg, float duration, int count, float speedMul = 1f)
        {
            int shooter = shooterId == 0 ? 0 : 1;
            int facing = MatchSetup.Facing(shooter);
            Vec2 feet = setup.Feet(shooter);
            Vec2 origin = new Vec2(feet.x + facing * cfg.bowAnchorX, feet.y + cfg.bowAnchorY);
            float speed = cfg.ArrowSpeed(power) * speedMul;
            float a = cfg.ClampAngle(angleDeg) * MathF.PI / 180f;
            Vec2 v = new Vec2(facing * MathF.Cos(a) * speed, MathF.Sin(a) * speed);
            Vec2[] pts = new Vec2[count];
            for (int i = 0; i < count; i++)
            {
                float t = duration * (i + 1) / count;
                pts[i] = new Vec2(origin.x + v.x * t, origin.y + v.y * t - 0.5f * cfg.gravity * t * t);
            }
            return pts;
        }
    }
}
