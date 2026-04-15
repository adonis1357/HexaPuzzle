using UnityEngine;
using System.Collections.Generic;

namespace JewelsHexaPuzzle.Data
{
    /// <summary>
    /// 적군 메타데이터 - 한 종류의 적군이 가진 모든 정적 정보.
    /// 새 적군 추가 시 EnemyRegistry.RegisterAll()에 항목만 추가하면 됨.
    /// </summary>
    [System.Serializable]
    public class EnemyMetadata
    {
        // === 기본 정보 ===
        public EnemyType type;              // enum 값
        public string name;                 // 정식 명칭 (예: "색상도둑")
        public string alias;               // 별칭/축약 (예: "그레이")
        public string description;          // 능력 설명 (UI 도감용)

        // === 비주얼 ===
        public Color overlayColor;          // 오버레이 틴트 색상

        // === 밸런스 ===
        public int baseScore;               // 처치 시 기본 점수
        public int defaultMaxOnBoard;       // 보드 최대 동시 존재 수 기본값
    }

    /// <summary>
    /// 적군 레지스트리 - 모든 적군 메타데이터를 중앙 관리.
    /// 새 적군 추가 시 RegisterAll()에 항목만 추가하면 됨.
    ///
    /// 사용법:
    ///   EnemyRegistry.GetName(EnemyType.Chromophage)           → "색상도둑"
    ///   EnemyRegistry.GetOverlayColor(EnemyType.Divider)       → Color(청록)
    ///   EnemyRegistry.GetMetadata(EnemyType.ChaosOverlord)     → EnemyMetadata 전체
    /// </summary>
    public static class EnemyRegistry
    {
        private static Dictionary<EnemyType, EnemyMetadata> registry;
        private static bool isInitialized = false;

        // ============================================================
        // 초기화
        // ============================================================

        /// <summary>
        /// 레지스트리 초기화 (지연 초기화 - 첫 조회 시 자동 호출)
        /// </summary>
        public static void Initialize()
        {
            if (isInitialized) return;
            registry = new Dictionary<EnemyType, EnemyMetadata>();
            RegisterAll();
            isInitialized = true;
        }

        // ============================================================
        // 조회 API
        // ============================================================

        /// <summary>
        /// 적군 메타데이터 전체 반환 (null이면 등록되지 않은 타입)
        /// </summary>
        public static EnemyMetadata GetMetadata(EnemyType type)
        {
            Initialize();
            return registry.TryGetValue(type, out var meta) ? meta : null;
        }

        /// <summary>
        /// 정식 명칭 반환 (EnemyTypeHelper.GetName 대체)
        /// </summary>
        public static string GetName(EnemyType type)
        {
            var meta = GetMetadata(type);
            return meta != null ? meta.name : "없음";
        }

        /// <summary>
        /// 별칭 반환 (EnemyTypeHelper.GetAlias 대체)
        /// </summary>
        public static string GetAlias(EnemyType type)
        {
            var meta = GetMetadata(type);
            return meta != null ? meta.alias : "";
        }

        /// <summary>
        /// 오버레이 색상 반환 (HexBlock.GetEnemyOverlayColor 대체)
        /// </summary>
        public static Color GetOverlayColor(EnemyType type)
        {
            var meta = GetMetadata(type);
            return meta != null ? meta.overlayColor : Color.white;
        }

        /// <summary>
        /// 처치 기본 점수 반환 (ScoreCalculator.GetEnemyBaseScore 대체)
        /// </summary>
        public static int GetBaseScore(EnemyType type)
        {
            var meta = GetMetadata(type);
            return meta != null ? meta.baseScore : 0;
        }

        /// <summary>
        /// 보드 최대 동시 존재 수 기본값
        /// </summary>
        public static int GetDefaultMaxOnBoard(EnemyType type)
        {
            var meta = GetMetadata(type);
            return meta != null ? meta.defaultMaxOnBoard : 3;
        }

        /// <summary>
        /// 등록된 모든 적군 메타데이터 목록
        /// </summary>
        public static List<EnemyMetadata> GetAll()
        {
            Initialize();
            return new List<EnemyMetadata>(registry.Values);
        }

        /// <summary>
        /// 총 적군 종류 수
        /// </summary>
        public static int Count
        {
            get { Initialize(); return registry.Count; }
        }

        // ============================================================
        // 적군 등록 - 새 적군 추가는 여기에만 항목 추가
        // ============================================================

        private static void RegisterAll()
        {
            // --- #0 색상도둑 (Chromophage) ---
            Register(new EnemyMetadata
            {
                type = EnemyType.Chromophage,
                name = "색상도둑",
                alias = "그레이",
                description = "블록을 회색으로 바꿔 매칭 불가로 만듦",
                overlayColor = new Color(0.5f, 0.55f, 0.5f, 0.35f),
                baseScore = 100,
                defaultMaxOnBoard = 15
            });

            // --- #1 속박의 사슬 (ChainAnchor) ---
            Register(new EnemyMetadata
            {
                type = EnemyType.ChainAnchor,
                name = "속박의 사슬",
                alias = "사슬",
                description = "블록을 묶어 이동 불가로 만듦",
                overlayColor = Color.white,
                baseScore = 150,
                defaultMaxOnBoard = 10
            });

            // --- #2 가시 기생충 (ThornParasite) ---
            Register(new EnemyMetadata
            {
                type = EnemyType.ThornParasite,
                name = "가시 기생충",
                alias = "가시",
                description = "매칭 시 주변에 피해를 줌",
                overlayColor = new Color(0.8f, 0.2f, 0.3f, 0.85f),
                baseScore = 200,
                defaultMaxOnBoard = 8
            });

            // --- #3 분열체 (Divider) ---
            Register(new EnemyMetadata
            {
                type = EnemyType.Divider,
                name = "분열체",
                alias = "분열",
                description = "파괴되면 인접 빈 칸에 2개로 분열",
                overlayColor = new Color(0.1f, 0.85f, 0.85f, 0.8f),
                baseScore = 300,
                defaultMaxOnBoard = 6
            });

            // --- #4 중력왜곡자 (GravityWarper) ---
            Register(new EnemyMetadata
            {
                type = EnemyType.GravityWarper,
                name = "중력왜곡자",
                alias = "중력",
                description = "블록 낙하를 고정시킴",
                overlayColor = new Color(0.6f, 0.2f, 0.9f, 0.8f),
                baseScore = 400,
                defaultMaxOnBoard = 4
            });

            // --- #5 반사방패 (ReflectionShield) ---
            Register(new EnemyMetadata
            {
                type = EnemyType.ReflectionShield,
                name = "반사방패",
                alias = "방패",
                description = "보호막이 있어 여러 번 공격해야 파괴됨",
                overlayColor = new Color(0.8f, 0.85f, 0.9f, 0.85f),
                baseScore = 450,
                defaultMaxOnBoard = 4
            });

            // --- #6 시간동결자 (TimeFreezer) ---
            Register(new EnemyMetadata
            {
                type = EnemyType.TimeFreezer,
                name = "시간동결자",
                alias = "동결",
                description = "회전 비용을 2배로 증가시킴",
                overlayColor = new Color(0.3f, 0.6f, 1f, 0.8f),
                baseScore = 500,
                defaultMaxOnBoard = 3
            });

            // --- #7 공명쌍둥이 (ResonanceTwin) ---
            Register(new EnemyMetadata
            {
                type = EnemyType.ResonanceTwin,
                name = "공명쌍둥이",
                alias = "쌍둥이",
                description = "2개가 쌍으로 존재, 동시에 파괴해야 처치",
                overlayColor = new Color(1f, 0.9f, 0.2f, 0.8f),
                baseScore = 600,
                defaultMaxOnBoard = 4
            });

            // --- #8 그림자포자 (ShadowSpore) ---
            Register(new EnemyMetadata
            {
                type = EnemyType.ShadowSpore,
                name = "그림자포자",
                alias = "포자",
                description = "시간이 지나면 주변으로 확산됨",
                overlayColor = new Color(0.35f, 0.1f, 0.45f, 0.85f),
                baseScore = 700,
                defaultMaxOnBoard = 5
            });

            // --- #9 카오스 군주 (ChaosOverlord) ---
            Register(new EnemyMetadata
            {
                type = EnemyType.ChaosOverlord,
                name = "카오스 군주",
                alias = "카오스",
                description = "여러 적군 능력을 동시에 사용하는 최종 보스",
                overlayColor = new Color(1f, 0.5f, 0.8f, 0.9f),
                baseScore = 1500,
                defaultMaxOnBoard = 1
            });

            // --- #10 고블린 (Goblin) ---
            Register(new EnemyMetadata
            {
                type = EnemyType.Goblin,
                name = "고블린",
                alias = "몽둥이",
                description = "나무 몽둥이를 들고 블록을 공격해 금간 블록으로 만드는 적군",
                overlayColor = new Color(0.3f, 0.7f, 0.2f, 0.9f),
                baseScore = 500,
                defaultMaxOnBoard = 8
            });

            // --- #11 갑옷 고블린 (ArmoredGoblin) ---
            Register(new EnemyMetadata
            {
                type = EnemyType.ArmoredGoblin,
                name = "갑옷 고블린",
                alias = "갑옷",
                description = "갑옷으로 무장한 고블린. HP가 3배로 높아 제거하기 어렵다",
                overlayColor = new Color(0.5f, 0.5f, 0.55f, 0.9f),
                baseScore = 1000,
                defaultMaxOnBoard = 4
            });

            // --- #12 활 고블린 (ArcherGoblin) ---
            Register(new EnemyMetadata
            {
                type = EnemyType.ArcherGoblin,
                name = "활 고블린",
                alias = "활",
                description = "상단에 고정 배치되어 매턴 화살을 쏴 블록에 크랙을 가하는 은신 사격수",
                overlayColor = new Color(0.2f, 0.5f, 0.15f, 0.9f),
                baseScore = 800,
                defaultMaxOnBoard = 5
            });

            // --- #13 방패 고블린 (ShieldGoblin) ---
            Register(new EnemyMetadata
            {
                type = EnemyType.ShieldGoblin,
                name = "방패 고블린",
                alias = "방패",
                description = "드릴을 차단하는 방패를 장착한 고블린. 방패 내구도 소진 후 일반 상태로 전환",
                overlayColor = new Color(0.6f, 0.5f, 0.1f, 0.9f),
                baseScore = 1000,
                defaultMaxOnBoard = 4
            });

            // --- #14 폭탄 고블린 (BombGoblin) ---
            Register(new EnemyMetadata
            {
                type = EnemyType.BombGoblin,
                name = "폭탄 고블린",
                alias = "폭탄",
                description = "카운트다운 후 광역 폭발하여 주변 블록을 대량 파괴하는 위험한 고블린",
                overlayColor = new Color(0.8f, 0.2f, 0.1f, 0.9f),
                baseScore = 1200,
                defaultMaxOnBoard = 3
            });
            // --- #15 힐러 고블린 (HealerGoblin) ---
            Register(new EnemyMetadata
            {
                type = EnemyType.HealerGoblin,
                name = "힐러 고블린",
                alias = "힐러",
                description = "피해 입은 아군 고블린을 회복시키는 지원형 고블린",
                overlayColor = new Color(0.3f, 0.8f, 0.4f, 0.9f),
                baseScore = 900,
                defaultMaxOnBoard = 3
            });

            // --- #16 헤비급 고블린 (HeavyGoblin) ---
            Register(new EnemyMetadata
            {
                type = EnemyType.HeavyGoblin,
                name = "헤비급 고블린",
                alias = "헤비",
                description = "3블록 삼각형을 점유하는 거대 고블린. HP 36, 회전 불가",
                overlayColor = new Color(0.45f, 0.3f, 0.2f, 0.9f),
                baseScore = 2000,
                defaultMaxOnBoard = 2
            });

            // --- #17 도둑 고블린 (ThiefGoblin) ---
            Register(new EnemyMetadata
            {
                type = EnemyType.ThiefGoblin,
                name = "도둑 고블린",
                alias = "도둑",
                description = "은신하며 블록을 훔치는 교활한 고블린",
                overlayColor = new Color(0.3f, 0.25f, 0.35f, 0.9f),
                baseScore = 1100,
                defaultMaxOnBoard = 4
            });

            // --- #18 마법사 고블린 (WizardGoblin) ---
            Register(new EnemyMetadata
            {
                type = EnemyType.WizardGoblin,
                name = "마법사 고블린",
                alias = "마법사",
                description = "마법으로 블록을 변환하는 고블린",
                overlayColor = new Color(0.4f, 0.2f, 0.6f, 0.9f),
                baseScore = 1300,
                defaultMaxOnBoard = 3
            });
        }

        private static void Register(EnemyMetadata meta)
        {
            registry[meta.type] = meta;
        }
    }
}
