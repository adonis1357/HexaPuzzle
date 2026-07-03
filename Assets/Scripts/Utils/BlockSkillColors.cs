using UnityEngine;

// ============================================================================
// BlockSkillColors.cs - 특수 블록 스킬 단계별 색상 상수
// ============================================================================
// 인게임 블록 아이콘 색조와 스킬트리 노드 색상을 동일하게 유지하기 위한
// 중앙화된 색상 정의. 새 색상 추가/변경 시 이 파일만 수정하면
// HexBlock(인게임)과 SkillTreeUI(스킬트리) 양쪽에 자동 반영.
// ============================================================================

namespace JewelsHexaPuzzle.Utils
{
    /// <summary>
    /// 특수 블록 스킬 단계별 색상 상수.
    /// 배열 인덱스: 0=기본(미해금), 1=Lv1, 2=Lv2, 3=Lv3
    /// </summary>
    public static class BlockSkillColors
    {
        // === 공통 스킬 레벨 색상 (드릴, 드론, 타겟 공용) ===
        public static readonly Color CommonBase   = new Color(0.6f, 0.6f, 0.6f, 1f);   // 기본 회색
        public static readonly Color CommonLevel1 = new Color(0.4f, 0.8f, 1f, 1f);     // 하늘색
        public static readonly Color CommonLevel2 = new Color(1f, 0.6f, 0.1f, 1f);     // 주황색
        public static readonly Color CommonLevel3 = new Color(1f, 0.15f, 0.1f, 1f);    // 빨간색

        // === 드릴 (레벨별 전용 색상 — 투사체 + 아이콘 통일) ===
        // 블록 색상과 구분되도록 흰색/검정을 약간 혼합
        public static readonly Color DrillBase   = new Color(0.92f, 0.92f, 0.92f, 1f);  // 흰색 (스킬 미해금)
        public static readonly Color DrillLevel1 = new Color(0.55f, 0.85f, 1.0f, 1f);   // 하늘색 + 약간 흰
        public static readonly Color DrillLevel2 = new Color(1.0f, 0.65f, 0.18f, 1f);   // 주황색 + 약간 검정
        public static readonly Color DrillLevel3 = new Color(0.75f, 0.45f, 1.0f, 1f);   // 밝은 보라색

        public static readonly Color[] Drill = { DrillBase, DrillLevel1, DrillLevel2, DrillLevel3 };

        // === 폭탄 (검정 배경 + 텍스트 오버레이 방식, 색상은 노드 표시용) ===
        public static readonly Color BombBase   = new Color(0.15f, 0.15f, 0.15f, 1f);  // 검정
        public static readonly Color BombLevel1 = new Color(0.15f, 0.15f, 0.15f, 1f);  // 검정 (v1 텍스트)
        public static readonly Color BombLevel2 = new Color(0.15f, 0.15f, 0.15f, 1f);  // 검정 (v2 텍스트)
        public static readonly Color BombLevel3 = new Color(0.15f, 0.15f, 0.15f, 1f);  // 검정 (v3 텍스트)

        public static readonly Color[] Bomb = { BombBase, BombLevel1, BombLevel2, BombLevel3 };

        /// <summary>폭탄 스킬 레벨별 표시 텍스트 (0=숨김, 1~3=v1~v3)</summary>
        public static string GetBombLevelText(int level)
        {
            switch (level)
            {
                case 1: return "v1";
                case 2: return "v2";
                case 3: return "v3";
                default: return "";
            }
        }

        // === 드론 (스킬 레벨은 공통 색상 사용, 기본만 밝게 별도 정의) ===
        // ★ DroneBase는 회색(0.6) 대신 거의 흰색(0.95)으로 — 드론 스프라이트의
        //   자연색(민트 본체/하늘색 날개)을 그대로 살려 어둡게 보이지 않도록.
        //   DrillBase(0.92)와 유사한 밝기로 통일.
        public static readonly Color DroneBase   = new Color(0.95f, 0.95f, 0.95f, 1f);
        public static readonly Color DroneLevel1 = CommonLevel1;
        public static readonly Color DroneLevel2 = CommonLevel2;
        public static readonly Color DroneLevel3 = CommonLevel3;

        public static readonly Color[] Drone = { DroneBase, DroneLevel1, DroneLevel2, DroneLevel3 };

        // === 타겟 (공통 색상 사용) ===
        public static readonly Color TargetBase   = CommonBase;
        public static readonly Color TargetLevel1 = CommonLevel1;
        public static readonly Color TargetLevel2 = CommonLevel2;
        public static readonly Color TargetLevel3 = CommonLevel3;

        public static readonly Color[] Target = { TargetBase, TargetLevel1, TargetLevel2, TargetLevel3 };

        /// <summary>
        /// 배열에서 레벨에 맞는 색상 반환 (범위 초과 시 마지막 색상)
        /// </summary>
        public static Color GetByLevel(Color[] colors, int level)
        {
            if (colors == null || colors.Length == 0) return Color.white;
            int idx = Mathf.Clamp(level, 0, colors.Length - 1);
            return colors[idx];
        }
    }
}
