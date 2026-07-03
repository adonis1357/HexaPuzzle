#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace JewelsHexaPuzzle.EditorTools
{
    /// <summary>
    /// 리워드 툴팁 검증 메뉴 — 재생 중(인게임) 전 카테고리를 순회하며 툴팁을 열고
    /// 아이콘 로드/이름/능력치/설명을 리포트로 남긴다.
    /// 결과: 콘솔 + .claude/captures/reward_tooltip_verify.txt (아이콘/설명 누락 0건이면 PASS).
    /// </summary>
    public static class RewardTooltipVerifyMenu
    {
        [MenuItem("MatchMine/리워드 툴팁 검증 (전 카테고리)")]
        public static void Run()
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.LogWarning("[리워드툴팁검증] 재생 모드(인게임)에서 실행하세요. Play 후 레벨 진입 상태에서 메뉴 실행.");
                return;
            }
            var sys = JewelsHexaPuzzle.Managers.SkillUpgradeOfferSystem.Instance;
            if (sys == null) { Debug.LogWarning("[리워드툴팁검증] SkillUpgradeOfferSystem 인스턴스 없음 — 인게임 진입 필요."); return; }
            sys.StartRewardTooltipVerify();
        }
    }
}
#endif
