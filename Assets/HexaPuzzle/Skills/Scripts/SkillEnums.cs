using System;

namespace HexaPuzzle.Skills
{
    /// <summary>스킬 분류. 카탈로그/UI 필터·정렬에 사용.</summary>
    public enum SkillCategory
    {
        PowerUpUpgrade = 0, // 보드 위 파워업(드릴/폭탄/드론/레이저포) 강화
        ItemUpgrade    = 1, // 도구(망치/스왑/라인) 강화
        Combat         = 2, // 전투·치명타 패시브
        Deploy         = 3, // 즉시 랜덤 배치(소환)
        Meta           = 4, // 메타 진행(달성 게이지 감소 등)
    }

    /// <summary>
    /// 모든 스킬의 안정적 식별자. 문자열 id(skills.json)와 1:1 대응.
    /// enum 값 이름 == json "id" (소문자/언더스코어는 그대로) 로 매핑됩니다.
    /// 새 스킬을 추가하면 여기와 skills.json 양쪽에 추가하세요.
    /// </summary>
    public enum SkillId
    {
        None = -1,

        // ── PowerUpUpgrade ──
        DrillDamage, DrillMove, DrillCushion, DrillPierce,
        BombDamage, BombMove, BombChain, BombKnockback,
        DroneDamage, DroneClone,
        CannonDamage,

        // ── ItemUpgrade ──
        HammerDamage, HammerRange,
        SwapStep,
        LineDamage, LineConnect,

        // ── Combat ──
        DirectHit, AdjacentHit, CritChance, CritDamage,

        // ── Deploy ──
        DeployDrill, DeployBomb, DeployDrone, DeployCannon,
        DeployMix2, DeployMix3, DeployMix4,

        // ── Meta ──
        GaugeReduce,
    }

    /// <summary>SkillId ↔ json id(string) 변환.</summary>
    public static class SkillIds
    {
        /// <summary>"drill_damage" → SkillId.DrillDamage. 알 수 없으면 None.</summary>
        public static SkillId Parse(string jsonId)
        {
            if (string.IsNullOrEmpty(jsonId)) return SkillId.None;
            var pascal = ToPascal(jsonId);
            return Enum.TryParse<SkillId>(pascal, true, out var v) ? v : SkillId.None;
        }

        /// <summary>SkillId.DrillDamage → "drill_damage".</summary>
        public static string ToJsonId(SkillId id)
        {
            var s = id.ToString();
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (char.IsUpper(c) && i > 0) sb.Append('_');
                sb.Append(char.ToLowerInvariant(c));
            }
            return sb.ToString();
        }

        private static string ToPascal(string snake)
        {
            var parts = snake.Split('_');
            var sb = new System.Text.StringBuilder();
            foreach (var p in parts)
            {
                if (p.Length == 0) continue;
                sb.Append(char.ToUpperInvariant(p[0]));
                if (p.Length > 1) sb.Append(p.Substring(1));
            }
            return sb.ToString();
        }
    }
}
