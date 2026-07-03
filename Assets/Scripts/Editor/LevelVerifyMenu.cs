#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace JewelsHexaPuzzle.EditorTools
{
    /// <summary>
    /// 전 레벨 미션 자동 검증 메뉴 — Edit 모드에서 게임 실제 로직으로 150레벨 × 3회 검증.
    /// 메뉴: MatchMine/전 레벨 미션 검증 (3회). 결과는 콘솔 + .claude/captures/levelverify.txt.
    /// 검증 항목: (1)소환=미션 일치 (2)처치→미션 감소 (3)이동보상.
    /// </summary>
    public static class LevelVerifyMenu
    {
        [MenuItem("MatchMine/전 레벨 미션 검증 (3회)")]
        public static void RunLevelVerify()
        {
            string report = JewelsHexaPuzzle.Managers.LevelMissionVerifier.RunVerification(3);
            Debug.Log("[전 레벨 미션 검증]\n" + report);
        }

        [MenuItem("MatchMine/전 레벨 디자인 덤프 (이동·활성/대기 미션)")]
        public static void DumpLevels()
        {
            string report = JewelsHexaPuzzle.Managers.LevelMissionVerifier.DumpAllLevels();
            Debug.Log("[전 레벨 디자인 덤프]\n" + report);
        }

        [MenuItem("MatchMine/미션 활성한도 밴드 검증 (150레벨)")]
        public static void VerifyLimitBands()
        {
            string report = JewelsHexaPuzzle.Managers.LevelMissionVerifier.VerifyMissionLimitBands();
            Debug.Log("[미션 활성한도 밴드 검증]\n" + report);
        }
    }
}
#endif
