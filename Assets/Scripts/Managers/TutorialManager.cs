// =====================================================================================
// TutorialManager.cs — 튜토리얼 시스템 매니저
// =====================================================================================
// 싱글톤 패턴. 튜토리얼 시퀀스 진행, 트리거 감지, 완료 상태 저장(PlayerPrefs).
// GameManager, InputSystem, BlockRemovalSystem 등과 연동하여 튜토리얼을 제어.
// =====================================================================================
using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using JewelsHexaPuzzle.Core;
using JewelsHexaPuzzle.Data;
using JewelsHexaPuzzle.UI;

namespace JewelsHexaPuzzle.Managers
{
    /// <summary>
    /// 튜토리얼 시스템 중앙 매니저 (싱글톤)
    /// </summary>
    public class TutorialManager : MonoBehaviour
    {
        // ============================================================
        // 싱글톤
        // ============================================================
        public static TutorialManager Instance { get; private set; }

        // ============================================================
        // 상태
        // ============================================================
        private bool isTutorialActive = false;
        public bool IsTutorialActive => isTutorialActive;

        private TutorialSequence currentSequence;
        private int currentStepIndex;
        private bool waitingForTap = false;
        private bool waitingForEvent = false;
        private TutorialWaitEvent pendingWaitEvent = TutorialWaitEvent.None;
        // preset으로 설정된 wait가 WaitForEvent 스텝 시작 전에 이미 발생됐음을 기록
        // (예: Dialog 탭 후 BRS 재개 → OnBombCreated가 tut_bomb_wait_created 스텝 시작 전에 이벤트 캡처)
        private TutorialWaitEvent lastFiredEvent = TutorialWaitEvent.None;

        /// <summary>
        /// 현재 시퀀스의 현재 스텝 이후에 지정 이벤트를 기다리는 스텝이 있는지 확인.
        /// 이벤트 캡처 시 tutorial이 아직 wait 상태가 아니더라도 대비하여 pause 거는 용도.
        /// </summary>
        private bool WillWaitForEvent(TutorialWaitEvent eventType)
        {
            if (!isTutorialActive || currentSequence == null || currentSequence.steps == null)
                return false;
            for (int i = currentStepIndex; i < currentSequence.steps.Length; i++)
            {
                var s = currentSequence.steps[i];
                if (s == null) continue;
                if (s.type == TutorialStepType.WaitForEvent && s.waitEvent == eventType) return true;
                if (s.type == TutorialStepType.Dialog && s.waitForEventBeforeShow == eventType) return true;
            }
            return false;
        }

        // 특수블록 튜토리얼 상태
        private bool isPausedForTutorial = false;
        private HexCoord? pendingDrillCoord = null;
        private HexCoord? pendingSpecialCoord = null;   // 범용 특수블록 좌표 (bomb/drone/rainbow/xblock)
        private HexCoord? pendingHammerTargetCoord = null; // Stage 6 망치 튜토리얼 타겟 블록

        // Stage 11 스왑 튜토리얼: 허용된 두 좌표만 드래그 가능
        private HexCoord? pendingSwapSourceCoord = null;
        private HexCoord? pendingSwapDestCoord = null;

        // Stage 17 라인 튜토리얼: 드래그 경로 블록 목록 (Glow 타겟 + 드래그 선 시작/끝)
        private List<HexCoord> pendingLineDrawPath = null;

        /// <summary>망치 튜토리얼 타겟 좌표 제한 활성 여부 (HammerItem이 검증).</summary>
        public bool HasHammerTargetRestriction => pendingHammerTargetCoord.HasValue;
        public HexCoord? HammerTargetCoord => pendingHammerTargetCoord;

        /// <summary>잘못된 위치 클릭 시 토스트 표시 (HammerItem/HammerGauge에서 호출).</summary>
        public void ShowHammerWrongClickHint()
        {
            UIManager.Instance?.ShowToast("타겟 블록을 클릭해주세요!");
        }

        /// <summary>스왑 튜토리얼 타겟 좌표 제한 활성 여부 (SwapItem이 검증).</summary>
        public bool HasSwapTargetRestriction => pendingSwapSourceCoord.HasValue && pendingSwapDestCoord.HasValue;
        public HexCoord? SwapSourceCoord => pendingSwapSourceCoord;
        public HexCoord? SwapDestCoord => pendingSwapDestCoord;

        /// <summary>스왑 튜토리얼: 주어진 좌표가 허용된 소스 또는 대상인지 확인.</summary>
        public bool IsSwapCoordAllowed(HexCoord coord)
        {
            if (!HasSwapTargetRestriction) return true;
            return coord.Equals(pendingSwapSourceCoord.Value) || coord.Equals(pendingSwapDestCoord.Value);
        }

        /// <summary>잘못된 위치 클릭 시 토스트 표시 (SwapItem에서 호출).</summary>
        public void ShowSwapWrongClickHint()
        {
            UIManager.Instance?.ShowToast("밝게 표시된 블록을 드래그해 주세요!");
        }

        /// <summary>
        /// BRS 캐스케이드 pause 동기화용 프로퍼티.
        /// true이면 BRS가 yield return null로 대기.
        /// </summary>
        public bool IsPausedForTutorial => isPausedForTutorial;

        // ============================================================
        // 데이터
        // ============================================================
        private List<TutorialSequence> allSequences;
        private HashSet<string> completedTutorials = new HashSet<string>();
        private const string PREFS_KEY = "CompletedTutorials";

        // ============================================================
        // 기능 해금 시스템 (레벨 해금 연동)
        // ============================================================

        // 기능 ID 상수
        public const string FEATURE_DRILL = "drill";
        public const string FEATURE_BOMB = "bomb";
        public const string FEATURE_RAINBOW = "rainbow";
        public const string FEATURE_XBLOCK = "xblock";
        public const string FEATURE_DRONE = "drone";
        public const string FEATURE_ITEM_HAMMER = "item_hammer";
        public const string FEATURE_ITEM_SWAP = "item_swap";
        public const string FEATURE_ITEM_LINEDRAW = "item_linedraw";
        public const string FEATURE_ITEM_REVERSE = "item_reverse";

        // 스테이지별 해금 매핑 — 해당 레벨이 해금되면 기능도 해금
        // 재배치: 역회전 아이템을 마지막에, 튜토리얼 간 3스테이지 간격으로 균등 배치
        // Stage 14는 "몬스터 공략 가이드" 튜토리얼 슬롯 (기능 해금 없음 — Rainbow/XBlock 통합됨)
        // Stage 23 XBlock 해금 시 Rainbow도 함께 해금 (둘은 동일한 타겟 레이저로 취급)
        private static readonly Dictionary<int, string[]> stageAutoUnlock = new Dictionary<int, string[]>
        {
            { 4, new[] { FEATURE_DRILL } },
            { 6, new[] { FEATURE_ITEM_HAMMER } },
            { 8, new[] { FEATURE_BOMB } },
            { 11, new[] { FEATURE_ITEM_SWAP } },
            { 17, new[] { FEATURE_ITEM_LINEDRAW } },
            { 20, new[] { FEATURE_DRONE } },
            { 23, new[] { FEATURE_XBLOCK, FEATURE_RAINBOW } },
            { 26, new[] { FEATURE_ITEM_REVERSE } },
        };

        // 이미 알림 발송한 기능 추적 (중복 이벤트 방지)
        private HashSet<string> notifiedFeatures = new HashSet<string>();

        public event Action<string> OnFeatureUnlocked;

        // ============================================================
        // 참조
        // ============================================================
        private TutorialUI tutorialUI;
        private InputSystem inputSystem;
        private HexGrid hexGrid;
        private MatchingSystem matchingSystem;
        private Coroutine sequenceCoroutine;

        // ============================================================
        // 이벤트
        // ============================================================
        public event Action OnTutorialStarted;
        public event Action OnTutorialEnded;

        // ============================================================
        // 초기화
        // ============================================================

        private bool isInitialized = false;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            LoadCompletedTutorials();
            allSequences = TutorialDatabase.GetAllSequences();
            Debug.Log($"[TutorialManager] Awake: {allSequences.Count}개 시퀀스 로드, {completedTutorials.Count}개 완료됨");
        }

        private void Start()
        {
            EnsureInitialized();
        }

        /// <summary>
        /// 참조/UI 초기화 보장 (지연 초기화 패턴)
        /// Awake에서 못하는 초기화(FindObjectOfType 등)를 여기서 수행
        /// </summary>
        private void EnsureInitialized()
        {
            if (isInitialized) return;

            // 참조 자동 탐색
            inputSystem = FindObjectOfType<InputSystem>();
            hexGrid = FindObjectOfType<HexGrid>();
            matchingSystem = FindObjectOfType<MatchingSystem>();

            // TutorialUI 생성
            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas != null)
            {
                if (tutorialUI == null)
                {
                    GameObject uiObj = new GameObject("TutorialUI");
                    uiObj.transform.SetParent(this.transform);
                    tutorialUI = uiObj.AddComponent<TutorialUI>();
                    tutorialUI.Initialize(canvas);
                }
                isInitialized = true;
                Debug.Log($"[TutorialManager] 초기화 완료: inputSystem={inputSystem != null}, hexGrid={hexGrid != null}, tutorialUI={tutorialUI != null}");
            }
            else
            {
                Debug.LogWarning("[TutorialManager] Canvas를 찾을 수 없습니다! 초기화 보류.");
            }
        }

        // ============================================================
        // PlayerPrefs 저장/로드
        // ============================================================

        private void LoadCompletedTutorials()
        {
            string data = PlayerPrefs.GetString(PREFS_KEY, "");
            if (!string.IsNullOrEmpty(data))
            {
                foreach (string id in data.Split(','))
                {
                    if (!string.IsNullOrEmpty(id))
                        completedTutorials.Add(id);
                }
            }
        }

        private void SaveCompletedTutorials()
        {
            string data = string.Join(",", completedTutorials);
            PlayerPrefs.SetString(PREFS_KEY, data);
            PlayerPrefs.Save();
        }

        // ============================================================
        // 기능 해금 관리 (레벨 해금 직접 연동)
        // ============================================================

        /// <summary>
        /// 기능 해금 여부 확인 — LevelRegistry의 레벨 해금 상태를 직접 참조
        /// </summary>
        public bool IsFeatureUnlocked(string featureId)
        {
            foreach (var kvp in stageAutoUnlock)
            {
                foreach (var feat in kvp.Value)
                {
                    if (feat == featureId)
                    {
                        var level = LevelRegistry.GetLevel(kvp.Key);
                        return level != null && !level.isLocked;
                    }
                }
            }
            return true; // 매핑에 없는 기능은 항상 해금
        }

        /// <summary>
        /// 특수 블록 해금 여부 확인
        /// </summary>
        public bool IsSpecialBlockUnlocked(SpecialBlockType type)
        {
            switch (type)
            {
                case SpecialBlockType.Drill: return IsFeatureUnlocked(FEATURE_DRILL);
                case SpecialBlockType.Bomb: return IsFeatureUnlocked(FEATURE_BOMB);
                case SpecialBlockType.Rainbow: return IsFeatureUnlocked(FEATURE_RAINBOW);
                case SpecialBlockType.XBlock: return IsFeatureUnlocked(FEATURE_XBLOCK);
                case SpecialBlockType.Drone: return IsFeatureUnlocked(FEATURE_DRONE);
                default: return true; // MoveBlock, FixedBlock 등은 항상 허용
            }
        }

        /// <summary>
        /// 레벨 해금 상태 동기화 — 새로 해금된 기능에 대해 OnFeatureUnlocked 이벤트 발생
        /// 스테이지 시작/클리어 시 호출
        /// </summary>
        public void SyncFeatureUnlocks()
        {
            foreach (var kvp in stageAutoUnlock)
            {
                var level = LevelRegistry.GetLevel(kvp.Key);
                if (level != null && !level.isLocked)
                {
                    foreach (var feat in kvp.Value)
                    {
                        if (notifiedFeatures.Add(feat))
                        {
                            OnFeatureUnlocked?.Invoke(feat);
                            Debug.Log($"[TutorialManager] ★ 기능 해금 (레벨 연동): {feat}");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 튜토리얼 완료 여부 확인
        /// </summary>
        public bool IsTutorialCompleted(string sequenceId)
        {
            return completedTutorials.Contains(sequenceId);
        }

        /// <summary>
        /// 튜토리얼 완료 마킹
        /// </summary>
        public void MarkCompleted(string sequenceId)
        {
            if (completedTutorials.Add(sequenceId))
            {
                SaveCompletedTutorials();
                Debug.Log($"[TutorialManager] 튜토리얼 완료: {sequenceId}");
            }
        }

        /// <summary>
        /// 모든 튜토리얼 완료 상태 리셋 (디버그용)
        /// </summary>
        public void ResetAllTutorials()
        {
            completedTutorials.Clear();
            PlayerPrefs.DeleteKey(PREFS_KEY);
            PlayerPrefs.Save();
            Debug.Log("[TutorialManager] 모든 튜토리얼 리셋");
            notifiedFeatures.Clear();
        }

        /// <summary>
        /// 해당 스테이지에 튜토리얼이 있는지 확인 (로비 UI 뱃지 표시용).
        /// </summary>
        public static bool IsTutorialStage(int stageNumber)
        {
            switch (stageNumber)
            {
                case 1: case 3: case 4: case 6: case 8:
                case 11: case 17: case 20: case 23: case 26:
                case 31: case 33:  // 합성 튜토리얼만 (31=드릴+드릴, 33=드릴+폭탄 + 자유 합성 안내)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 스테이지 기반 튜토리얼의 sequenceId 매핑.
        /// 튜토리얼 스테이지 진입 시 재시도/재플레이에서도 반복되도록 완료 기록을 리셋하는 데 사용.
        /// (이벤트 기반 힌트 hint_bomb/rainbow/drone/xblock/chromophage/chain은 제외 — 첫 등장 시에만 표시)
        /// </summary>
        private string GetTutorialSequenceIdForStage(int stageNumber)
        {
            switch (stageNumber)
            {
                case 1:  return "stage1_onboarding";
                case 4:  return "stage4_drill";
                case 6:  return "tut_hammer";
                case 8:  return "tut_bomb";
                case 11: return "tut_swap";
                case 17: return "tut_linedraw";
                case 20: return "tut_drone";
                case 23: return "tut_targetlaser";
                case 26: return "tut_reverse";
            }
            // 합성 튜토리얼은 31, 33만 유지 (나머지 짝수/홀수 합성 스테이지에서는 튜토리얼 제거됨)
            if (stageNumber == 31 || stageNumber == 33)
                return $"combo_tut_stage{stageNumber}";
            return null;
        }

        /// <summary>
        /// 튜토리얼 스테이지 재진입 시 해당 시퀀스의 "완료됨" 기록만 선택적으로 제거.
        /// PlayerPrefs에도 반영해 세션 간에도 유지.
        /// 호출 후 `CheckTrigger`에서 showOnce 체크를 통과하고 시퀀스가 다시 재생됨.
        /// </summary>
        private void ResetTutorialCompletionForStage(int stageNumber)
        {
            string sequenceId = GetTutorialSequenceIdForStage(stageNumber);
            if (string.IsNullOrEmpty(sequenceId)) return;

            if (completedTutorials.Remove(sequenceId))
            {
                SaveCompletedTutorials();
                Debug.Log($"[TutorialManager] 튜토리얼 재시도 대비: '{sequenceId}' 완료 기록 제거 → 다시 재생");
            }
        }

        /// <summary>
        /// notifiedFeatures 초기화 — 에디터 해금/잠금 토글 시 호출하여
        /// 다음 SyncFeatureUnlocks() 호출 때 이벤트가 새로 발생하도록 함
        /// </summary>
        public void ResetNotifiedFeatures()
        {
            notifiedFeatures.Clear();
            Debug.Log("[TutorialManager] notifiedFeatures 초기화");
        }

        // ============================================================
        // 트리거 체크 (외부에서 호출)
        // ============================================================

        /// <summary>
        /// 스테이지 시작 시 호출 — 해당 스테이지의 온보딩 튜토리얼 체크
        /// </summary>
        public void OnStageStart(int stageNumber)
        {
            EnsureInitialized();

            // 레벨 해금 상태 기반 기능 해금 동기화
            SyncFeatureUnlocks();

            // ★ 튜토리얼 스테이지는 재시도/재플레이 시 항상 반복되도록 완료 기록을 리셋
            ResetTutorialCompletionForStage(stageNumber);

            Debug.Log($"[TutorialManager] OnStageStart({stageNumber}): active={isTutorialActive}, tutorialUI={tutorialUI != null}, sequences={allSequences?.Count}");

            // 스테이지 1 온보딩 보드 사전 배치 (중앙 매칭 + 상단 고블린 유도)
            if (stageNumber == 1 && !IsTutorialCompleted("stage1_onboarding"))
            {
                SetupStage1TutorialBoard();
            }

            // ★ Stage 2: 몽둥이 고블린 첫 등장 — 필드 반경 2 (19블록) 제한 + 설명 튜토리얼
            if (stageNumber == 2)
            {
                SetupStage2TutorialBoard();
            }

            // ★ Stage 3 (2026-04-27 재설계): 반경 2 필드 + 소환 영역 2칸 + 몽둥이 2→3 웨이브
            //   기존 드릴 튜토리얼 콘텐츠 완전 제거
            if (stageNumber == 3)
            {
                SetupStage3CompactBoard();
            }

            // ★ Stage 4: 반경 3 필드(37블록) + 소환 영역 2칸 + 드릴 첫 등장
            //   컴팩트 보드는 항상 적용(스테이지 컨셉), 드릴 튜토리얼 preset은 미완료 시에만 덮어쓰기
            if (stageNumber == 4)
            {
                SetupStage4CompactBoard();
                if (!IsTutorialCompleted("stage4_drill"))
                {
                    SetupStage4DrillBoard();
                }
            }

            // ★ Stage 5~9: 공통 컴팩트 보드 — 반경 4 필드(61블록) + 소환 영역 3줄
            //   Stage 8(폭탄 튜토리얼)도 동일 보드 위에 preset만 덮어씀
            if (stageNumber >= 5 && stageNumber <= 9)
            {
                SetupStage5To9CompactBoard();
            }

            // 스테이지 8: 폭탄 인터랙티브 보드 (컴팩트 보드 위에 preset 색상만 덮어씀)
            if (stageNumber == 8 && !IsTutorialCompleted("tut_bomb"))
            {
                SetupBombTutorialBoard();
            }

            // 스테이지 11: 스왑 인터랙티브 보드 — 스왑으로 폭탄(5매칭) 생성 가능한 배치
            if (stageNumber == 11 && !IsTutorialCompleted("tut_swap"))
            {
                SetupSwapTutorialBoard();
            }

            // 스테이지 17: 라인 인터랙티브 보드 — 보라 블록 5개 경로 드래그 시범
            if (stageNumber == 17 && !IsTutorialCompleted("tut_linedraw"))
            {
                SetupLineDrawTutorialBoard();
            }

            // 스테이지 20: 드론 인터랙티브 보드
            if (stageNumber == 20 && !IsTutorialCompleted("tut_drone"))
            {
                SetupDroneTutorialBoard();
            }

            // 스테이지 23: 타겟 레이저 (Rainbow+XBlock 통합) 인터랙티브 보드
            if (stageNumber == 23 && !IsTutorialCompleted("tut_targetlaser"))
            {
                SetupTargetLaserTutorialBoard();
            }

            // 스테이지 26: 역회전 인터랙티브 보드 — CW=3매칭, CCW=드릴 생성
            if (stageNumber == 26 && !IsTutorialCompleted("tut_reverse"))
            {
                SetupReverseTutorialBoard();
            }

            // 스테이지 31, 33: 합성 튜토리얼 인터랙티브 보드만 (나머지 합성 스테이지 제거)
            if ((stageNumber == 31 || stageNumber == 33) && !IsTutorialCompleted($"combo_tut_stage{stageNumber}"))
            {
                SetupComboTutorialBoard(stageNumber);
            }

            if (isTutorialActive) return;
            CheckTrigger(TutorialTrigger.OnStageStart, stageNumber);
        }

        /// <summary>
        /// 스테이지 1 온보딩 보드 사전 배치
        /// 중앙(0,0) 근처에서 매칭이 일어나도록 블록 배치.
        /// 매칭 상단(r이 작은 방향)에 고블린이 위치하여 낙하 데미지 연출 확인 가능.
        ///
        /// ★ 클러스터 (0,0)(1,0)(0,1) CW 회전 시:
        ///   (0,0)←(0,1)=Red, (1,0)←(0,0)=Green, (0,1)←(1,0)=Red
        ///   → (-1,1)=Red, (0,0)=Red, (0,1)=Red  →  삼각형(-1,1)(0,0)(0,1) Red 3매칭!
        ///
        /// ★ 회전 전에는 (0,0)=Green이므로 직접 매칭 없음 (즉시 캐스케이드 방지)
        /// ★ allowedCoords로 입력 제한하므로 FindMatchableCluster 순서 무관
        /// </summary>
        // ============================================================
        // 튜토리얼 매칭 방지 배경 헬퍼
        // ============================================================

        /// <summary>
        /// 튜토리얼 preset + 빈 칸을 매칭 불가능한 색상으로 일괄 배치.
        /// 좌표 공식 (q + 2*r) mod 5 → 5색 순환, **모든 삼각형이 3색 구성**이 되어 매칭 불가.
        /// 특수블록 생성/클릭/발동 중 주변 블록이 우연히 매칭되는 현상을 방지.
        ///
        /// 삼각형 매칭 방지 증명:
        ///   삼각형 유형 A: (q,r), (q+1,r), (q,r+1) → f=X, X+1, X+2 (서로 다름)
        ///   삼각형 유형 B: (q,r), (q+1,r), (q+1,r-1) → f=X, X+1, X-1 (서로 다름)
        /// </summary>
        private void ApplyMatchProofBackground(Dictionary<HexCoord, GemType> overrides)
        {
            if (hexGrid == null) return;

            GemType[] palette = {
                GemType.Red, GemType.Blue, GemType.Green, GemType.Yellow, GemType.Purple
            };

            var full = new Dictionary<HexCoord, GemType>();
            foreach (var block in hexGrid.GetAllBlocks())
            {
                if (block == null) continue;
                HexCoord coord = block.Coord;

                if (overrides != null && overrides.TryGetValue(coord, out GemType overrideColor))
                {
                    full[coord] = overrideColor;
                }
                else
                {
                    // 매칭 방지 공식: 5색 순환으로 모든 삼각형이 3색 구성
                    int idx = ((coord.q + 2 * coord.r) % 5 + 5) % 5;
                    full[coord] = palette[idx];
                }
            }

            hexGrid.SetPresetBlocks(full);
        }

        private void SetupStage1TutorialBoard()
        {
            if (hexGrid == null) return;

            // ★ Stage 1: 중앙 7블록만 활성화하여 "뭉쳐있는 작은 필드" 시각 연출
            //   - 기본 그리드(반경 5, 61블록)는 그대로 생성되지만
            //   - 외곽 54블록은 SetActive(false)로 비활성화하여 중앙 7블록만 보임
            //   - 드롭 시스템은 건드리지 않음 → 기존 TriggerStartDrop 정상 동작
            //   - 다음 스테이지 진입 시 InitializeGrid가 모든 블록을 재생성하므로 자동 복원
            HideOuterBlocksForStage1();

            // ★ Stage 1은 소환 영역(BgCellExt)을 모두 숨김 — 고블린 없으므로 불필요
            hexGrid.SetExtendedVisibleArea(columnRadius: 1, visibleRows: 0);

            ApplyStage1Preset_ScenarioA();
            Debug.Log("[TutorialManager] Stage 1 Scenario A 프리셋 적용 (중앙 7블록 + 소환 영역 숨김, 120° 회전 시 Red 매칭)");
        }

        /// <summary>
        /// Stage 1에서 중앙 반경 1(7블록) 외의 모든 블록을 그리드에서 영구 제거.
        /// </summary>
        private void HideOuterBlocksForStage1()
        {
            LimitGridRadius(1);
        }

        /// <summary>
        /// Stage 3 보드 세팅 — 반경 2 필드 + 소환 영역 2줄 (Stage 2와 동일한 컴팩트 구성, 흙더미 없음).
        /// 몽둥이 고블린 2→3 순차 웨이브 미션.
        /// </summary>
        private void SetupStage3CompactBoard()
        {
            if (hexGrid == null) return;

            LimitGridRadius(2);
            hexGrid.SetExtendedVisibleArea(columnRadius: 2, visibleRows: 2);
            hexGrid.RealignExtendedCellsToActualTops();

            Debug.Log("[TutorialManager] Stage 3: 필드 반경 2 + 소환 영역 컬럼 반경 2, 2줄 부착 (몽둥이 2→3 웨이브)");
        }

        /// <summary>
        /// Stage 4 보드 세팅 — 반경 3 필드(37블록) + 소환 영역 2줄.
        /// 드릴 튜토리얼 보드(SetupStage4DrillBoard)와 함께 사용 — 컴팩트 보드를 먼저 적용한 뒤
        /// 튜토리얼 미완료 시 드릴 preset(ApplyMatchProofBackground)이 색상을 덮어쓴다.
        /// 드릴 preset 좌표 최대 거리는 3(예: (3,-1)) → 반경 3 필드에 정확히 들어감.
        /// </summary>
        private void SetupStage4CompactBoard()
        {
            if (hexGrid == null) return;

            LimitGridRadius(3);
            hexGrid.SetExtendedVisibleArea(columnRadius: 3, visibleRows: 2);
            hexGrid.RealignExtendedCellsToActualTops();

            Debug.Log("[TutorialManager] Stage 4: 필드 반경 3(37블록) + 소환 영역 컬럼 반경 3, 2줄 부착 (드릴 튜토리얼)");
        }

        /// <summary>
        /// Stage 5~9 공통 컴팩트 보드 세팅 — 반경 4 필드(61블록) + 소환 영역 3줄.
        /// 중심에서 4칸까지 블록 필드, 그 상단에 3줄의 소환 영역 배치.
        /// Stage 8(폭탄 튜토리얼)도 동일 보드를 사용하며, 폭탄 preset은 그 위에 덮어씌워짐.
        ///
        /// 동작:
        ///   1) LimitGridRadius(4) → 외곽(반경 5) 블록 영구 제거 + 외곽 BgCell 숨김 (반경 4 = 61블록)
        ///   2) SetExtendedVisibleArea(4, 3) → 컬럼 -4..+4 × row 1~3 활성화
        ///   3) RealignExtendedCellsToActualTops → 각 컬럼 실제 최상단 바로 위 3칸으로 정렬
        /// </summary>
        private void SetupStage5To9CompactBoard()
        {
            if (hexGrid == null) return;

            // ── 1) 그리드 반경 4로 제한 (61블록 = 1+6+12+18+24)
            LimitGridRadius(4);

            // ── 2) 소환 영역: 컬럼 -4..+4 × row 1~3
            hexGrid.SetExtendedVisibleArea(columnRadius: 4, visibleRows: 3);

            // ── 3) 각 컬럼의 실제 최상단 블록 바로 위 3칸으로 BgCellExt 정렬
            hexGrid.RealignExtendedCellsToActualTops();

            Debug.Log("[TutorialManager] Stage 5~9: 필드 반경 4(61블록) + 소환 영역 컬럼 반경 4, 3줄 부착 + 정렬 완료");
        }

        /// <summary>
        /// Stage 2 보드 세팅 — 몽둥이 고블린 첫 등장 튜토리얼.
        /// 필드 반경 2 (19블록) 제한, 소환 영역 2줄을 필드 상단 바로 위에 부착.
        /// 19블록 전체에 흙더미 1/3 적용 — 흙더미 학습 미션.
        /// </summary>
        private void SetupStage2TutorialBoard()
        {
            if (hexGrid == null) return;

            // 외곽 블록 제거 (반경 > 2)
            LimitGridRadius(2);

            // 소환 영역: 컬럼도 반경 2로 제한 + 2줄만 표시
            hexGrid.SetExtendedVisibleArea(columnRadius: 2, visibleRows: 2);

            // ★ BgCellExt를 실제 필드 상단 바로 위로 재배치
            //   (BgCellExt는 원래 gridRadius=5 기준 좌표에 생성되어 있음 → 반경 2 필드와 거리 발생)
            hexGrid.RealignExtendedCellsToActualTops();

            // ★ 외곽 라인(radius=2)만 흙더미 1/3 적용 (12블록)
            //   중앙 7블록(radius≤1)은 자유롭게 회전·매칭 가능 → 흙더미 정화 진입점 확보
            int dirtApplied = 0;
            foreach (var block in hexGrid.GetAllBlocks())
            {
                if (block == null || block.Data == null) continue;
                if (block.Data.gemType == GemType.None) continue;
                if (block.Data.specialType != SpecialBlockType.None &&
                    block.Data.specialType != SpecialBlockType.MoveBlock) continue;

                var c = block.Coord;
                int hexDist = Mathf.Max(Mathf.Abs(c.q), Mathf.Abs(c.r), Mathf.Abs(c.q + c.r));
                // 외곽 라인만 (반경 정확히 2): 중앙 7블록은 제외
                if (hexDist != 2) continue;

                block.Data.dirtMound = 1;
                block.UpdateVisuals();
                dirtApplied++;
            }

            Debug.Log($"[TutorialManager] Stage 2: 필드 반경 2 + 소환 영역 정렬 + 외곽 라인 흙더미 1/3 적용 ({dirtApplied}블록, 중앙 7블록 제외)");
        }

        /// <summary>
        /// 공통: 그리드를 주어진 반경으로 제한 (외곽 블록 영구 제거 + 배경 셀 숨김).
        /// - radius 1: 7블록 (Stage 1)
        /// - radius 2: 19블록 (Stage 2)
        /// 다음 스테이지 진입 시 InitializeGrid가 블록을 재생성하므로 자동 복원.
        /// </summary>
        public void LimitGridRadius(int radius)
        {
            if (hexGrid == null) return;
            if (radius < 0) return;

            // 지정 반경 밖의 좌표를 먼저 수집 (iterate 중 dict 수정 방지)
            var outerCoords = new List<HexCoord>();
            foreach (var block in hexGrid.GetAllBlocks())
            {
                if (block == null) continue;
                var c = block.Coord;
                int distance = Mathf.Max(Mathf.Abs(c.q), Mathf.Abs(c.r), Mathf.Abs(c.q + c.r));
                if (distance > radius)
                    outerCoords.Add(c);
            }

            // 외곽 블록 영구 제거
            foreach (var coord in outerCoords)
                hexGrid.RemoveBlockPermanently(coord);

            Debug.Log($"[TutorialManager] 그리드 반경 {radius} 제한: 외곽 {outerCoords.Count}개 영구 제거");

            // 배경 그리드(BgCell) 셀도 해당 반경만 보이도록 숨김
            HideOuterBackgroundCells(radius);

            // 슬롯 캐시 무효화 — 제거된 블록 반영 (다음 ProcessFalling에서 재구축)
            var brs = UnityEngine.Object.FindObjectOfType<BlockRemovalSystem>();
            if (brs != null) brs.InvalidateSlotCache();
        }

        /// <summary>
        /// 배경 그리드 셀(BgCell)을 주어진 반경만큼만 표시, 나머지는 숨김.
        /// ★ BgCellExt(소환 영역)는 건드리지 않음 — SetExtendedVisibleArea가 별도 관리.
        /// 헥스 그리드 코너 블록의 실제 최대 거리는 sqrt(3) * radius * hexSize.
        /// (1.5 * radius * hexSize는 정육각형 변 거리이고, 코너는 더 멀리 있음 → 임계값 보정)
        /// </summary>
        private void HideOuterBackgroundCells(int radius)
        {
            if (hexGrid == null || hexGrid.GridContainer == null) return;

            float hexSize = hexGrid.HexSize;
            // ★ 임계값 = sqrt(3) * r * hexSize + 작은 여유
            //   라디우스 r 헥스의 모든 셀 (코너 포함) 위치를 전부 포함
            float visibleRadius = hexSize * (Mathf.Sqrt(3f) * radius) + hexSize * 0.3f;

            Transform bgContainer = null;
            for (int i = 0; i < hexGrid.GridContainer.childCount; i++)
            {
                var child = hexGrid.GridContainer.GetChild(i);
                if (child.name == "GridBackground")
                {
                    bgContainer = child;
                    break;
                }
            }
            if (bgContainer == null) return;

            int bgHidden = 0, bgKept = 0;
            for (int i = 0; i < bgContainer.childCount; i++)
            {
                var cell = bgContainer.GetChild(i);
                if (cell == null) continue;
                // ★ 소환 영역(BgCellExt_*)은 SetExtendedVisibleArea가 관리하므로 건드리지 않음
                if (cell.name.StartsWith("BgCellExt_")) continue;

                var crt = cell as RectTransform ?? cell.GetComponent<RectTransform>();
                if (crt == null) continue;

                float dist = crt.anchoredPosition.magnitude;
                bool inside = dist <= visibleRadius;
                cell.gameObject.SetActive(inside);
                if (inside) bgKept++;
                else bgHidden++;
            }
            Debug.Log($"[TutorialManager] 배경 셀 반경 {radius} 제한 (threshold={visibleRadius:F1}): 유지={bgKept}, 숨김={bgHidden}");
        }

        /// <summary>
        /// Stage 1 Scenario A — 120° 회전 시 Red 매칭 유도
        /// </summary>
        private void ApplyStage1Preset_ScenarioA()
        {
            if (hexGrid == null) return;

            var presets = new Dictionary<HexCoord, GemType>
            {
                { new HexCoord( 0, 0), GemType.Red   },
                { new HexCoord( 1, 0), GemType.Green },
                { new HexCoord( 0, 1), GemType.Red   },
                { new HexCoord(-1, 1), GemType.Green },
                { new HexCoord(-1, 0), GemType.Green },
                { new HexCoord( 0,-1), GemType.Green },
                { new HexCoord( 1,-1), GemType.Red   },
            };

            int applied = 0;
            foreach (var kvp in presets)
            {
                var block = hexGrid.GetBlock(kvp.Key);
                if (block == null)
                {
                    Debug.LogWarning($"[TutorialManager] Stage1 Scenario A: 블록 ({kvp.Key}) 없음 — 스킵");
                    continue;
                }

                var data = new BlockData();
                data.gemType = kvp.Value;
                data.isCracked = false;
                data.isShell = false;
                data.specialType = SpecialBlockType.None;
                data.tier = BlockTier.Normal;
                block.SetBlockData(data);

                // ★ 강제로 비주얼 활성화 — HideVisuals 상태에서 벗어나도록
                //   SetBlockData는 UpdateVisuals를 호출하지만 추가 보증 차원
                block.UpdateVisuals();

                // 트랜스폼 초기화 — 드롭 애니메이션 잔여 영향 제거
                var rt = block.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.localScale = Vector3.one;
                }

                applied++;
            }
            Debug.Log($"[TutorialManager] Stage1 Scenario A 프리셋 적용: {applied}/7 블록");
        }


        /// <summary>
        /// Stage 1 Scenario B — 240° 회전 시 Red 매칭 유도 (120°에서는 매칭 없음)
        /// 강제 탭 #2 직전에 보드를 재설정하기 위해 사용
        ///
        ///  초기 상태: (0,0)=G, (1,0)=R, (0,1)=R, (-1,1)=G, (-1,0)=R, (0,-1)=G, (1,-1)=R
        ///  1st CW 회전: (0,0)=R, (1,0)=G, (0,1)=R → 매칭 없음
        ///  2nd CW 회전 (240°): (0,0)=R, (1,0)=R, (0,1)=G → 삼각형 (0,0)(1,-1)(1,0) = R,R,R 매칭!
        /// </summary>
        public void ApplyStage1Preset_ScenarioB()
        {
            if (hexGrid == null) return;

            var presets = new Dictionary<HexCoord, GemType>
            {
                { new HexCoord( 0, 0), GemType.Green },
                { new HexCoord( 1, 0), GemType.Red   },
                { new HexCoord( 0, 1), GemType.Red   },
                { new HexCoord(-1, 1), GemType.Green },
                { new HexCoord(-1, 0), GemType.Red   },
                { new HexCoord( 0,-1), GemType.Green },
                { new HexCoord( 1,-1), GemType.Red   },
            };

            foreach (var block in hexGrid.GetAllBlocks())
            {
                if (block == null) continue;
                if (!presets.TryGetValue(block.Coord, out GemType target)) continue;

                var data = block.Data ?? new BlockData();
                data.gemType = target;
                data.isCracked = false;
                data.isShell = false;
                data.specialType = SpecialBlockType.None;
                data.tier = BlockTier.Normal;
                block.SetBlockData(data);
            }
            Debug.Log("[TutorialManager] Stage 1 Scenario B 프리셋 적용 (240° 회전 시 Red 매칭)");
        }

        /// <summary>
        /// Stage 1 Scenario C — 자유 회전 연습용 (R/G 랜덤 배치, 이후 자유 플레이)
        /// </summary>
        public void ApplyStage1Preset_ScenarioC()
        {
            if (hexGrid == null) return;

            // 무작위 R/G 배치 (정확한 매칭 보장 없음)
            var coords = new HexCoord[] {
                new HexCoord( 0, 0), new HexCoord( 1, 0), new HexCoord( 0, 1),
                new HexCoord(-1, 1), new HexCoord(-1, 0), new HexCoord( 0,-1),
                new HexCoord( 1,-1)
            };
            // 고정 패턴: 중앙만 G, 나머지 R → 어떤 회전도 중앙 주변에 매칭 없음 보장 불가
            // 대신 초기 매칭 없는 상태를 제공하여 플레이어가 자유 연습
            GemType[] layout = {
                GemType.Green, GemType.Red, GemType.Green,
                GemType.Red, GemType.Green, GemType.Red,
                GemType.Green
            };

            for (int i = 0; i < coords.Length; i++)
            {
                var block = hexGrid.GetBlock(coords[i]);
                if (block == null) continue;
                var data = block.Data ?? new BlockData();
                data.gemType = layout[i];
                data.isCracked = false;
                data.isShell = false;
                data.specialType = SpecialBlockType.None;
                data.tier = BlockTier.Normal;
                block.SetBlockData(data);
            }
            Debug.Log("[TutorialManager] Stage 1 Scenario C 프리셋 적용 (자유 연습)");
        }

        /// <summary>
        /// Stage 1 튜토리얼 스텝 진입 시 필요한 프리셋 전환.
        /// ★ 2026-04-24: 자동 프리셋 전환 제거
        ///   240°/360° 시연을 위해 보드를 강제로 리셋하면 플레이 중 색상이 갑자기 바뀌어 부자연스러움.
        ///   → 240°/360° 규칙은 Dialog 설명만으로 학습하고, 보드 상태는 플레이어 액션의 자연스러운 결과로 유지.
        /// </summary>
        private void HandleStage1PresetSwitch(string stepId)
        {
            // 자동 프리셋 전환 비활성화 — 플레이 중 색상 갑작스런 변경 방지
            // (추후 필요 시 스텝 id 기반 특수 처리 재추가 가능)
        }

        // ============================================================
        // 특수블록 튜토리얼 보드 사전 배치
        // ============================================================

        /// <summary>
        /// 스테이지 3 드릴 인터랙티브 보드 — CW 회전으로 다이아몬드 패턴 생성
        /// ★ 드릴은 "2개의 삼각형이 공유 변으로 합쳐진 마름모(다이아몬드)" 4블록으로 생성됨
        /// (일자 4개로는 삼각형 매칭이 성립하지 않아 드릴 생성 불가)
        ///
        /// 타겟 Post-CW Blue 위치: (0,0)(1,0)(1,-1)(2,-1)
        ///   - 삼각형1: (0,0)(1,0)(1,-1)
        ///   - 삼각형2: (1,0)(1,-1)(2,-1)
        ///   - 공유 변: (1,0)-(1,-1)  →  Vertical 방향 드릴
        ///
        /// 클러스터 (0,0)(1,0)(0,1) CW 회전 매핑:
        ///   pos(0,0) ← data(0,1) = Blue  (다이아 완성)
        ///   pos(1,0) ← data(0,0) = Blue  (다이아 완성)
        ///   pos(0,1) ← data(1,0) = Red   (더미)
        /// </summary>
        private void SetupStage4DrillBoard()
        {
            if (hexGrid == null) return;

            var presets = new Dictionary<HexCoord, GemType>
            {
                // ── 클러스터 (회전 대상) ──
                { new HexCoord( 0, 0), GemType.Blue },    // CW → pos(1,0) = Blue
                { new HexCoord( 0, 1), GemType.Blue },    // CW → pos(0,0) = Blue
                { new HexCoord( 1, 0), GemType.Red },     // CW → pos(0,1) = Red (더미)

                // ── 다이아몬드 완성용 고정 Blue 블록 ──
                { new HexCoord( 1,-1), GemType.Blue },
                { new HexCoord( 2,-1), GemType.Blue },

                // ── 안전 블록 (다이아 주변 5매칭/다른 삼각형 방지) ──
                { new HexCoord(-1, 0), GemType.Green },
                { new HexCoord(-1, 1), GemType.Purple },
                { new HexCoord( 0,-1), GemType.Yellow },
                { new HexCoord( 1, 1), GemType.Yellow },
                { new HexCoord( 2, 0), GemType.Purple },  // Blue 차단 (bomb 방지)
                { new HexCoord( 3,-1), GemType.Green },
                { new HexCoord( 1,-2), GemType.Purple },
                { new HexCoord( 2,-2), GemType.Yellow },
            };

            ApplyMatchProofBackground(presets);
            Debug.Log("[TutorialManager] 스테이지 4 드릴 다이아몬드 보드 사전 배치 완료");
        }

        /// <summary>
        /// 스테이지 8 폭탄 인터랙티브 보드 — CW 회전으로 5블록 폭탄 패턴 생성
        /// 클러스터 (0,0)(1,0)(0,1) CW 회전:
        ///   pos(0,0)=Blue(from (0,1)), pos(1,0)=Green(from (0,0)), pos(0,1)=Blue(from (1,0))
        ///   → 센터(0,0) + 이웃 (-1,0)(-1,1)(0,1)(0,-1) = 5 Blue → 폭탄!
        /// ★ 4이웃이 연속(인덱스 2,3,4,5)이므로 드론 아닌 폭탄으로 판정
        /// </summary>
        private void SetupBombTutorialBoard()
        {
            if (hexGrid == null) return;

            var presets = new Dictionary<HexCoord, GemType>
            {
                // 클러스터 블록
                { new HexCoord( 0, 0), GemType.Green },   // CW → pos(1,0) = Green
                { new HexCoord( 1, 0), GemType.Blue },    // CW → pos(0,1) = Blue
                { new HexCoord( 0, 1), GemType.Blue },    // CW → pos(0,0) = Blue

                // 폭탄 패턴 블록 (비클러스터, 이미 Blue)
                { new HexCoord(-1, 0), GemType.Blue },
                { new HexCoord(-1, 1), GemType.Blue },
                { new HexCoord( 0,-1), GemType.Blue },

                // 안전 블록 (사전 삼각형 방지)
                { new HexCoord( 1, 1), GemType.Red },
                { new HexCoord(-2, 1), GemType.Red },
                { new HexCoord(-1,-1), GemType.Green },
                { new HexCoord(-1, 2), GemType.Yellow },
                { new HexCoord( 1,-1), GemType.Purple },
                { new HexCoord(-2, 0), GemType.Purple },
                { new HexCoord( 0, 2), GemType.Red },
                { new HexCoord( 2, 0), GemType.Green },
            };

            ApplyMatchProofBackground(presets);
            Debug.Log("[TutorialManager] 스테이지 8 폭탄 인터랙티브 보드 사전 배치 완료");
        }

        /// <summary>
        /// 스테이지 11 스왑 튜토리얼 보드 — 스왑+회전 2단계로 폭탄 생성.
        /// 스왑은 "인접" 1칸 거리, 스왑 직후에는 폭탄 불가 / 이후 CW 회전 시 폭탄 완성.
        ///
        /// 배치 (초기 상태):
        ///   Blue 정적: (-1,1), (-1,0), (0,-1), (1,-1)  — (0,0) 중심 이웃 4개
        ///   Blue 이동원: (1,1)                         — 스왑해서 (0,1)로 옮길 블록
        ///   Cluster (회전 대상): (0,0)=Yellow, (1,0)=Purple, (0,1)=Purple
        ///   Safety (초기 3매칭 방지): (-1,2)(-2,1)(-1,-1)(1,-2)(2,-1)(2,0)(2,1)(0,2)(1,2)
        ///
        /// 스왑 (0,1)↔(1,1) 거리=1 인접:
        ///   → (0,1)=Blue, (1,1)=Purple. 아직 폭탄 없음 (cluster 내 Blue 1개 + Yellow + Purple)
        ///
        /// CW 회전 cluster (0,0)(1,0)(0,1):
        ///   (0,0)←(0,1)=Blue, (1,0)←(0,0)=Yellow, (0,1)←(1,0)=Purple
        ///   → (0,0)=Blue + 이웃 4 Blue = 5-cluster = 폭탄!
        ///
        /// 게이지 충전 설명용 Green: (2,1), (0,2) 에 배치.
        /// </summary>
        private void SetupSwapTutorialBoard()
        {
            if (hexGrid == null) return;

            var presets = new Dictionary<HexCoord, GemType>
            {
                // === 폭탄 완성 시 쓸 Blue 이웃 4개 (정적) ===
                { new HexCoord(-1, 1), GemType.Blue },
                { new HexCoord(-1, 0), GemType.Blue },
                { new HexCoord( 0,-1), GemType.Blue },
                { new HexCoord( 1,-1), GemType.Blue },

                // === 스왑 대상 Blue (이동해서 (0,1)로) ===
                { new HexCoord( 1, 1), GemType.Blue },

                // === 스왑 소스 + 회전 클러스터 ===
                { new HexCoord( 0, 0), GemType.Yellow },  // CW 후 (1,0) 자리로 감 (non-Blue)
                { new HexCoord( 1, 0), GemType.Purple },  // CW 후 (0,1) 자리로 감 (non-Blue)
                { new HexCoord( 0, 1), GemType.Purple },  // 스왑 소스 → (1,1)과 교환되어 Blue가 됨

                // === 초기 3매칭 방지 safety ===
                // Blue-Blue 인접 쌍의 공유 이웃이 Blue가 되면 안 됨
                { new HexCoord(-1, 2), GemType.Purple }, // (0,1)(-1,1) 공유
                { new HexCoord(-2, 1), GemType.Purple }, // (-1,1)(-1,0) 공유
                { new HexCoord(-1,-1), GemType.Yellow }, // (-1,0)(0,-1) 공유
                { new HexCoord( 1,-2), GemType.Yellow }, // (0,-1)(1,-1) 공유
                { new HexCoord( 2,-1), GemType.Purple }, // (1,-1)-주변
                { new HexCoord( 2, 0), GemType.Yellow }, // (1,0)(1,1)(2,0) 삼각형에서 (1,1) Blue → Yellow 유지
                { new HexCoord( 2, 1), GemType.Green },  // (1,1) 주변 safety + 게이지 설명용 녹색
                { new HexCoord( 0, 2), GemType.Green },  // (0,1) 주변 safety + 녹색
                { new HexCoord( 1, 2), GemType.Yellow }, // (1,1)(0,2)(1,2) 삼각형

                // 추가 녹색 (게이지 설명용, 고립 배치)
                { new HexCoord(-2, 2), GemType.Green },
                { new HexCoord(-3, 2), GemType.Yellow },
                { new HexCoord(-2, 3), GemType.Purple },
            };

            ApplyMatchProofBackground(presets);

            // 스왑 타겟 좌표: (0,1) Purple ↔ (1,1) Blue — 인접 1칸 거리
            pendingSwapSourceCoord = new HexCoord(0, 1);
            pendingSwapDestCoord = new HexCoord(1, 1);

            Debug.Log("[TutorialManager] 스테이지 11 스왑 보드 완료 — 스왑(0,1)↔(1,1) → CW 회전으로 폭탄");
        }

        /// <summary>
        /// 스테이지 17 라인 튜토리얼 보드 — Purple 5개 경로 + 초기 3-매칭 없음.
        ///
        /// 드래그 경로: (0,0) → (1,-1) → (2,-1) → (2,0) → (3,0)
        ///   - 인접 쌍은 연결되지만 3개가 상호 인접한 삼각형은 없음 → 초기 3매칭 안 생김
        ///   - 보라색(Purple) 이므로 gauge_explain 에서 하이라이트되는 색상과 동일
        ///
        /// 경로 외 Purple 없음 (사전 매칭 방지). 안전 블록으로 각 인접 쌍 공유 이웃 방어.
        /// </summary>
        private void SetupLineDrawTutorialBoard()
        {
            if (hexGrid == null) return;

            var presets = new Dictionary<HexCoord, GemType>
            {
                // === 드래그 경로 (Purple 5개 연결) ===
                { new HexCoord( 0, 0), GemType.Purple },
                { new HexCoord( 1,-1), GemType.Purple },
                { new HexCoord( 2,-1), GemType.Purple },
                { new HexCoord( 2, 0), GemType.Purple },
                { new HexCoord( 3, 0), GemType.Purple },

                // === 안전 블록: 인접 Purple 쌍의 공유 이웃이 Purple이 되면 안 됨 ===
                // (0,0)-(1,-1) 공유: (1,0), (0,-1)
                { new HexCoord( 1, 0), GemType.Yellow },
                { new HexCoord( 0,-1), GemType.Green },
                // (1,-1)-(2,-1) 공유: (1,0) above, (2,-2)
                { new HexCoord( 2,-2), GemType.Green },
                // (2,-1)-(2,0) 공유: (1,0) above, (3,-1)
                { new HexCoord( 3,-1), GemType.Yellow },
                // (2,0)-(3,0) 공유: (3,-1) above, (2,1)
                { new HexCoord( 2, 1), GemType.Green },

                // 추가 주변 안전 블록 (매치프루프 기본값 덮어씀)
                { new HexCoord(-1, 0), GemType.Yellow },
                { new HexCoord(-1, 1), GemType.Blue },
                { new HexCoord( 0, 1), GemType.Red },
                { new HexCoord( 1, 1), GemType.Blue },
                { new HexCoord( 3, 1), GemType.Red },
                { new HexCoord( 1,-2), GemType.Blue },
                { new HexCoord( 3,-2), GemType.Blue },
            };

            ApplyMatchProofBackground(presets);

            // 드래그 경로 저장 (target_drag 단계에서 Glow/연결선/손가락 애니메이션 참조)
            pendingLineDrawPath = new List<HexCoord>
            {
                new HexCoord( 0, 0),
                new HexCoord( 1,-1),
                new HexCoord( 2,-1),
                new HexCoord( 2, 0),
                new HexCoord( 3, 0),
            };

            Debug.Log("[TutorialManager] 스테이지 17 라인 보드 완료 — Purple 5개 경로 배치");
        }

        /// <summary>
        /// 스테이지 23 타겟 레이저 인터랙티브 보드 (Rainbow/XBlock 통합) — CW 회전으로 링 패턴 완성
        /// 링 센터 (1,1)=Yellow, 링 블록 6개 모두 Blue
        /// 클러스터 (0,0)(1,0)(0,1) CW 회전:
        ///   pos(1,0)=Blue(from (0,0)), pos(0,1)=Blue(from (1,0))
        ///   → 링 완성: (2,1)(2,0)(1,0)(0,1)(0,2)(1,2) 모두 Blue → XBlock 생성 (해금 상태)
        /// </summary>
        private void SetupTargetLaserTutorialBoard()
        {
            if (hexGrid == null) return;

            // 회전 후 (0,0) 이 Yellow가 되도록 (0,1)=Yellow 사전 배치
            //   → 회전 후 (0,0)(0,-1)(1,-1) Red 3-match 유발을 방지
            //   → 이와 함께 (0,-1)(1,-1) 도 non-Red로 변경해 잔여 3-match 완전 차단
            // 링 센터 (1,1) = Yellow (match-proof 배경 기본값): 생성된 타겟 레이저 gemType 이
            //   주변 cascade 리필 색상과 3-match 가능성이 최소화됨
            var presets = new Dictionary<HexCoord, GemType>
            {
                // 링 센터 (Yellow — 타겟 레이저 gemType이 됨)
                { new HexCoord( 1, 1), GemType.Yellow },

                // 링 블록 (Blue) — 4개 사전 배치
                { new HexCoord( 2, 1), GemType.Blue },
                { new HexCoord( 2, 0), GemType.Blue },
                { new HexCoord( 0, 2), GemType.Blue },
                { new HexCoord( 1, 2), GemType.Blue },

                // 클러스터 블록 (CW 회전: (0,0)←(0,1), (1,0)←(0,0), (0,1)←(1,0))
                { new HexCoord( 0, 0), GemType.Blue },    // CW → pos(1,0) = Blue ✓ (링 완성)
                { new HexCoord( 1, 0), GemType.Blue },    // CW → pos(0,1) = Blue ✓ (링 완성)
                { new HexCoord( 0, 1), GemType.Yellow },  // CW → pos(0,0) = Yellow (Red 3매칭 방지)

                // 안전 블록 (회전 후·링 매칭 후 잔여 3매칭 방지)
                //   (0,-1)/(1,-1) 를 Purple/Green 으로 교체 → (0,0)(0,-1)(1,-1) Red 3매칭 불가
                { new HexCoord( 1,-1), GemType.Purple },
                { new HexCoord( 2,-1), GemType.Green },
                { new HexCoord( 3, 0), GemType.Red },
                { new HexCoord( 2, 2), GemType.Purple },
                { new HexCoord(-1, 0), GemType.Purple },
                { new HexCoord( 0,-1), GemType.Green },
                { new HexCoord( 0, 3), GemType.Green },
                { new HexCoord( 3, 1), GemType.Red },
                { new HexCoord(-1, 1), GemType.Red },
            };

            ApplyMatchProofBackground(presets);
            Debug.Log("[TutorialManager] 스테이지 14 타겟 레이저 인터랙티브 보드 완료 — 링 매칭 외 자동 3-match 없음");
        }

        /// <summary>
        /// 스테이지 22 드론 인터랙티브 보드 — CW 회전으로 나비 패턴 생성
        /// 클러스터 (0,0)(1,0)(0,1) CW 회전:
        ///   pos(0,0)=Blue(from (0,1)), pos(1,0)=Green(from (0,0)), pos(0,1)=Blue(from (1,0))
        ///   → 센터(0,0) + 날개A(1,-1)(0,-1) + 날개B(-1,1)(0,1) = 5 Blue 나비 → 드론!
        /// ★ 이웃 인덱스 [1],[2],[4],[5] → 쌍A=[1,2], 쌍B=[4,5], 교차 비인접 → 나비 패턴
        /// </summary>
        private void SetupDroneTutorialBoard()
        {
            if (hexGrid == null) return;

            var presets = new Dictionary<HexCoord, GemType>
            {
                // 클러스터 블록
                { new HexCoord( 0, 0), GemType.Green },   // CW → pos(1,0) = Green
                { new HexCoord( 1, 0), GemType.Blue },    // CW → pos(0,1) = Blue
                { new HexCoord( 0, 1), GemType.Blue },    // CW → pos(0,0) = Blue

                // 나비 날개 블록 (비클러스터, 이미 Blue)
                { new HexCoord( 1,-1), GemType.Blue },    // 날개 A1
                { new HexCoord( 0,-1), GemType.Blue },    // 날개 A2
                { new HexCoord(-1, 1), GemType.Blue },    // 날개 B1

                // 안전 블록 (사전 삼각형 방지)
                { new HexCoord( 1, 1), GemType.Red },
                { new HexCoord( 2,-1), GemType.Red },
                { new HexCoord(-1, 2), GemType.Yellow },
                { new HexCoord( 1,-2), GemType.Green },
                { new HexCoord(-1, 0), GemType.Purple },
                { new HexCoord(-1,-1), GemType.Purple },
                { new HexCoord( 2, 0), GemType.Yellow },
                { new HexCoord( 0,-2), GemType.Red },
                { new HexCoord(-2, 1), GemType.Green },
                { new HexCoord( 0, 2), GemType.Purple },
            };

            ApplyMatchProofBackground(presets);
            Debug.Log("[TutorialManager] 스테이지 22 드론 인터랙티브 보드 사전 배치 완료");
        }

        // (Rainbow/XBlock 통합: 기존 SetupXBlockTutorialBoard 삭제 — Stage 23는 SetupTargetLaserTutorialBoard 사용)

        /// <summary>
        /// 스테이지 26 역회전 인터랙티브 보드 — 정회전과 역회전의 결과 차이를 직접 체험.
        ///
        /// 클러스터 (0,0)(1,0)(0,1):
        ///   초기: (0,0)=Red, (1,0)=Blue, (0,1)=Blue
        ///
        /// 정회전 CW: pos(0,0)←(0,1)=Blue, pos(1,0)←(0,0)=Red, pos(0,1)←(1,0)=Blue
        ///   → (0,0)=Blue, (1,0)=Red, (0,1)=Blue + (-1,1)=Blue → 3-매칭 (-1,1)(0,1)(0,0) Type B
        ///   → 단순 매칭만 발생 (특수 블록 X)
        ///
        /// 역회전 CCW: pos(0,0)←(1,0)=Blue, pos(1,0)←(0,1)=Blue, pos(0,1)←(0,0)=Red
        ///   → (0,0)=Blue, (1,0)=Blue, (0,1)=Red + (1,-1)=Blue, (2,-1)=Blue
        ///   → (0,0)(1,0)(1,-1)(2,-1) 다이아몬드 4-매칭 → 드릴 생성!
        ///
        /// 즉, 정회전이면 단순 3-매칭 / 역회전이면 드릴 특수 블록 생성.
        /// </summary>
        private void SetupReverseTutorialBoard()
        {
            if (hexGrid == null) return;

            var presets = new Dictionary<HexCoord, GemType>
            {
                // ── 클러스터 (회전 대상) ──
                { new HexCoord( 0, 0), GemType.Red },     // CW→(1,0)=R; CCW→(0,1)=R
                { new HexCoord( 1, 0), GemType.Blue },    // CW→(0,1)=B; CCW→(0,0)=B
                { new HexCoord( 0, 1), GemType.Blue },    // CW→(0,0)=B; CCW→(1,0)=B

                // ── 다이아 완성용 고정 (CCW 시 드릴 4-매칭) ──
                { new HexCoord( 1,-1), GemType.Blue },
                { new HexCoord( 2,-1), GemType.Blue },

                // ── CW 3-매칭 완성용 (정회전 시 단순 매칭) ──
                { new HexCoord(-1, 1), GemType.Blue },

                // ── 안전 블록 (사전/주변 매칭 방지) ──
                { new HexCoord(-1, 0), GemType.Green },
                { new HexCoord( 0,-1), GemType.Yellow },
                { new HexCoord( 1, 1), GemType.Yellow },
                { new HexCoord( 2, 0), GemType.Purple },  // (1,0)=B 주변 Blue 차단
                { new HexCoord( 3,-1), GemType.Green },   // (2,-1)=B 주변 Blue 차단
                { new HexCoord( 1,-2), GemType.Purple },
                { new HexCoord( 2,-2), GemType.Yellow },
                { new HexCoord(-1, 2), GemType.Green },   // (-1,1)=B 주변 Blue 차단
                { new HexCoord(-2, 1), GemType.Purple },
                { new HexCoord(-2, 2), GemType.Yellow },
            };

            ApplyMatchProofBackground(presets);
            Debug.Log("[TutorialManager] 스테이지 26 역회전 인터랙티브 보드 사전 배치 완료 (CW=3매칭 / CCW=드릴)");
        }

        // ============================================================
        // 합성 튜토리얼 (스테이지 31~50)
        // ============================================================

        /// <summary>
        /// 합성 튜토리얼 보드 사전 배치 — (0,0)과 (1,0)에 두 특수 블록을 인접 배치.
        /// 플레이어가 두 블록을 스왑하면 합성 효과 발동.
        /// 주변은 안전색으로 채워 사전 삼각형 매칭 방지.
        /// </summary>
        private void SetupComboTutorialBoard(int stageNumber)
        {
            if (hexGrid == null) return;

            // 조합 결정 (기본/심화 모두 동일 preset — 심화는 메시지만 다름)
            SpecialBlockType type1, type2;
            GemType color1, color2;
            DrillDirection drillDir1 = DrillDirection.Vertical;
            DrillDirection drillDir2 = DrillDirection.Slash;

            switch (stageNumber)
            {
                case 31: case 32: // 드릴+드릴
                    type1 = SpecialBlockType.Drill; color1 = GemType.Blue; drillDir1 = DrillDirection.Vertical;
                    type2 = SpecialBlockType.Drill; color2 = GemType.Red;  drillDir2 = DrillDirection.Slash;
                    break;
                case 33: case 34: // 드릴+폭탄
                    type1 = SpecialBlockType.Drill; color1 = GemType.Blue; drillDir1 = DrillDirection.Vertical;
                    type2 = SpecialBlockType.Bomb;  color2 = GemType.Red;
                    break;
                case 35: case 36: // 드릴+X블록
                    type1 = SpecialBlockType.Drill;  color1 = GemType.Blue; drillDir1 = DrillDirection.Vertical;
                    type2 = SpecialBlockType.XBlock; color2 = GemType.Green;
                    break;
                case 37: case 38: // 드릴+드론
                    type1 = SpecialBlockType.Drill; color1 = GemType.Blue; drillDir1 = DrillDirection.Vertical;
                    type2 = SpecialBlockType.Drone; color2 = GemType.Purple;
                    break;
                case 39: case 40: // 폭탄+폭탄
                    type1 = SpecialBlockType.Bomb; color1 = GemType.Blue;
                    type2 = SpecialBlockType.Bomb; color2 = GemType.Red;
                    break;
                case 41: case 42: // 폭탄+X블록
                    type1 = SpecialBlockType.Bomb;   color1 = GemType.Blue;
                    type2 = SpecialBlockType.XBlock; color2 = GemType.Green;
                    break;
                case 43: case 44: // 폭탄+드론
                    type1 = SpecialBlockType.Bomb;  color1 = GemType.Blue;
                    type2 = SpecialBlockType.Drone; color2 = GemType.Purple;
                    break;
                case 45: case 46: // X블록+X블록
                    type1 = SpecialBlockType.XBlock; color1 = GemType.Green;
                    type2 = SpecialBlockType.XBlock; color2 = GemType.Yellow;
                    break;
                case 47: case 48: // X블록+드론
                    type1 = SpecialBlockType.XBlock; color1 = GemType.Green;
                    type2 = SpecialBlockType.Drone;  color2 = GemType.Purple;
                    break;
                case 49: case 50: // 드론+드론
                    type1 = SpecialBlockType.Drone; color1 = GemType.Purple;
                    type2 = SpecialBlockType.Drone; color2 = GemType.Yellow;
                    break;
                default:
                    return;
            }

            // 1) 주변 안전 블록 배치 (사전 삼각형 매칭 방지)
            // 중앙 (0,0)과 (1,0)은 특수 블록이므로 일반 매칭에서 제외
            // 주변은 서로 다른 색 위주로 배치해 인접 매칭 위험 최소화
            var safePresets = new Dictionary<HexCoord, GemType>
            {
                { new HexCoord( 0, 1), GemType.Yellow },
                { new HexCoord( 1, 1), GemType.Green },
                { new HexCoord( 1,-1), GemType.Green },
                { new HexCoord( 2,-1), GemType.Yellow },
                { new HexCoord( 2, 0), GemType.Red },
                { new HexCoord( 0,-1), GemType.Red },
                { new HexCoord(-1, 0), GemType.Purple },
                { new HexCoord(-1, 1), GemType.Red },
                { new HexCoord( 0, 2), GemType.Blue },
                { new HexCoord( 2, 1), GemType.Purple },
                { new HexCoord(-1,-1), GemType.Yellow },
                { new HexCoord(-2, 0), GemType.Green },
                { new HexCoord( 1,-2), GemType.Purple },
                { new HexCoord( 3, 0), GemType.Yellow },
                { new HexCoord( 3,-1), GemType.Purple },
            };
            ApplyMatchProofBackground(safePresets);

            // 2) 특수 블록 2개 배치 — (0,0)과 (1,0) 인접
            PlaceSpecialBlock(new HexCoord(0, 0), color1, type1, drillDir1);
            PlaceSpecialBlock(new HexCoord(1, 0), color2, type2, drillDir2);

            Debug.Log($"[TutorialManager] 스테이지 {stageNumber} 합성 튜토리얼 보드 사전 배치 완료 ({type1}+{type2})");
        }

        /// <summary>
        /// 특정 좌표에 특수 블록 직접 배치 (SetPresetBlocks가 일반 색상만 지원하므로 보조).
        /// </summary>
        private void PlaceSpecialBlock(HexCoord coord, GemType color, SpecialBlockType specialType, DrillDirection drillDir)
        {
            if (hexGrid == null) return;
            var block = hexGrid.GetBlock(coord);
            if (block == null) return;

            var data = new BlockData(color);
            data.specialType = specialType;
            if (specialType == SpecialBlockType.Drill)
                data.drillDirection = drillDir;
            block.SetBlockData(data);
        }

        /// <summary>
        /// 매칭 발생 시 호출
        /// </summary>
        public void OnMatchOccurred(int matchCount)
        {
            // 이벤트 대기 중이면 해제
            if (waitingForEvent && pendingWaitEvent == TutorialWaitEvent.MatchOccurred)
            {
                lastFiredEvent = pendingWaitEvent;
                waitingForEvent = false;
                pendingWaitEvent = TutorialWaitEvent.None;
                return;
            }
        }

        /// <summary>
        /// 회전 완료 시 호출
        /// </summary>
        public void OnRotationComplete(bool success)
        {
            if (waitingForEvent && pendingWaitEvent == TutorialWaitEvent.RotationComplete)
            {
                lastFiredEvent = pendingWaitEvent;
                waitingForEvent = false;
                pendingWaitEvent = TutorialWaitEvent.None;
            }
        }

        /// <summary>
        /// 특수 블록 생성 시 호출
        /// </summary>
        public void OnSpecialBlockCreated(SpecialBlockType type)
        {
            EnsureInitialized();
            if (waitingForEvent && pendingWaitEvent == TutorialWaitEvent.SpecialBlockCreated)
            {
                lastFiredEvent = pendingWaitEvent;
                waitingForEvent = false;
                pendingWaitEvent = TutorialWaitEvent.None;
            }

            // 첫 생성 힌트 체크 (진행 중인 튜토리얼이 없을 때만)
            if (!isTutorialActive)
            {
                var hint = allSequences.FirstOrDefault(s =>
                    s.trigger == TutorialTrigger.OnFirstSpecialCreate &&
                    s.triggerSpecialType == type &&
                    s.showOnce && !IsTutorialCompleted(s.sequenceId));

                if (hint != null)
                    StartSequence(hint);
            }
        }

        /// <summary>
        /// 적군 등장 시 호출
        /// </summary>
        public void OnEnemyEncountered(EnemyType type)
        {
            EnsureInitialized();
            if (isTutorialActive) return;

            var hint = allSequences.FirstOrDefault(s =>
                s.trigger == TutorialTrigger.OnFirstEnemyEncounter &&
                s.triggerEnemyType == type &&
                s.showOnce && !IsTutorialCompleted(s.sequenceId));

            if (hint != null)
                StartSequence(hint);
        }

        /// <summary>
        /// 캐스케이드 완료 시 호출
        /// </summary>
        public void OnCascadeComplete()
        {
            if (waitingForEvent && pendingWaitEvent == TutorialWaitEvent.CascadeComplete)
            {
                lastFiredEvent = pendingWaitEvent;
                waitingForEvent = false;
                pendingWaitEvent = TutorialWaitEvent.None;
            }
        }

        // ============================================================
        // 트리거 매칭
        // ============================================================

        private void CheckTrigger(TutorialTrigger trigger, int stageNumber = -1)
        {
            Debug.Log($"[TutorialManager] CheckTrigger: trigger={trigger}, stage={stageNumber}, 총 시퀀스={allSequences?.Count}");
            bool foundAny = false;
            foreach (var seq in allSequences)
            {
                if (seq.trigger != trigger) continue;
                foundAny = true;
                if (seq.triggerStage >= 0 && seq.triggerStage != stageNumber)
                {
                    Debug.Log($"[TutorialManager]   → '{seq.sequenceId}' 스테이지 불일치 (요구={seq.triggerStage}, 현재={stageNumber})");
                    continue;
                }
                if (seq.showOnce && IsTutorialCompleted(seq.sequenceId))
                {
                    Debug.Log($"[TutorialManager]   → '{seq.sequenceId}' 이미 완료됨");
                    continue;
                }

                Debug.Log($"[TutorialManager]   → '{seq.sequenceId}' 매칭! 시퀀스 시작");
                StartSequence(seq);
                break; // 한 번에 하나의 시퀀스만
            }
            if (!foundAny)
                Debug.Log($"[TutorialManager]   → trigger={trigger}에 해당하는 시퀀스 없음");
        }

        // ============================================================
        // 시퀀스 재생
        // ============================================================

        private void StartSequence(TutorialSequence sequence)
        {
            if (isTutorialActive) return;
            if (sequence == null || sequence.steps == null || sequence.steps.Length == 0)
            {
                Debug.LogWarning($"[TutorialManager] StartSequence 실패: sequence={sequence != null}, steps={sequence?.steps?.Length}");
                return;
            }

            // UI 준비 확인
            EnsureInitialized();
            if (tutorialUI == null)
            {
                Debug.LogError("[TutorialManager] StartSequence 실패: tutorialUI가 null! Canvas가 없거나 초기화 실패.");
                return;
            }

            currentSequence = sequence;
            currentStepIndex = 0;
            isTutorialActive = true;

            // 상태 초기화 — 이전 시퀀스의 잔류 상태가 현재 대기에 영향 주는 것 방지
            waitingForEvent = false;
            pendingWaitEvent = TutorialWaitEvent.None;
            lastFiredEvent = TutorialWaitEvent.None;
            isPausedForTutorial = false;

            // 스킵 버튼 콜백 사전 설정 (모든 스텝에서 스킵 가능)
            if (tutorialUI != null)
                tutorialUI.ShowSkipButton(() => SkipTutorial());

            OnTutorialStarted?.Invoke();
            Debug.Log($"[TutorialManager] ★ 시퀀스 시작: {sequence.sequenceId} ({sequence.steps.Length}스텝)");

            if (sequenceCoroutine != null) StopCoroutine(sequenceCoroutine);
            sequenceCoroutine = StartCoroutine(PlaySequenceCoroutine());
        }

        private IEnumerator PlaySequenceCoroutine()
        {
            while (currentStepIndex < currentSequence.steps.Length)
            {
                TutorialStep step = currentSequence.steps[currentStepIndex];
                yield return StartCoroutine(PlayStepCoroutine(step));
                currentStepIndex++;
            }

            // 시퀀스 완료
            CompleteSequence();
        }

        private IEnumerator PlayStepCoroutine(TutorialStep step)
        {
            Debug.Log($"[TutorialManager] 스텝 실행: {step.id} ({step.type})");

            switch (step.type)
            {
                case TutorialStepType.Dialog:
                    yield return StartCoroutine(HandleDialogStep(step));
                    break;

                case TutorialStepType.Highlight:
                    yield return StartCoroutine(HandleHighlightStep(step));
                    break;

                case TutorialStepType.ForcedAction:
                    yield return StartCoroutine(HandleForcedActionStep(step));
                    break;

                case TutorialStepType.FreePlayHint:
                    yield return StartCoroutine(HandleFreePlayHintStep(step));
                    break;

                case TutorialStepType.WaitForEvent:
                    yield return StartCoroutine(HandleWaitForEventStep(step));
                    break;
            }
        }

        // ── Dialog 스텝 ──
        private IEnumerator HandleDialogStep(TutorialStep step)
        {
            // ★ Stage 1 전용: 특정 스텝 진입 시 보드 프리셋 재적용 (240°/360° 시나리오 전환)
            HandleStage1PresetSwitch(step.id);

            // ★ Dialog 내용을 표시하기 전 이벤트 대기 (race-free 버전: wait + dialog 원자적 처리)
            //   이미 이전 Dialog 탭에서 preset으로 waitingForEvent=true가 설정되어 있다면,
            //   이벤트가 캡처되어 lastFiredEvent가 세팅됐거나 waitingForEvent가 여전히 true 상태.
            if (step.waitForEventBeforeShow != TutorialWaitEvent.None)
            {
                // preset 캐시 체크: 이미 fire된 경우 즉시 통과
                bool alreadyFired = (lastFiredEvent == step.waitForEventBeforeShow);
                if (alreadyFired)
                {
                    lastFiredEvent = TutorialWaitEvent.None;
                    Debug.Log($"[TutorialManager] Dialog pre-wait {step.waitForEventBeforeShow}: preset 캐시 적중, 즉시 통과");
                }
                else
                {
                    // 필요 시 setup — preset이 안 되어있으면 여기서 설정
                    if (pendingWaitEvent != step.waitForEventBeforeShow)
                    {
                        waitingForEvent = true;
                        pendingWaitEvent = step.waitForEventBeforeShow;
                    }
                    Debug.Log($"[TutorialManager] Dialog pre-wait: {step.waitForEventBeforeShow}");
                    // ★ waitTimeoutOverride 지정 시 그 값 사용, 아니면 60초 기본
                    float dialogWaitTimeout = step.waitTimeoutOverride > 0f ? step.waitTimeoutOverride : 60f;
                    float elapsed = 0f;
                    while (waitingForEvent && elapsed < dialogWaitTimeout)
                    {
                        elapsed += Time.unscaledDeltaTime;
                        yield return null;
                    }
                    if (waitingForEvent)
                    {
                        Debug.LogWarning($"[TutorialManager] Dialog pre-wait 타임아웃 ({dialogWaitTimeout}s): {step.waitForEventBeforeShow}");
                        waitingForEvent = false;
                        pendingWaitEvent = TutorialWaitEvent.None;
                        // ★ waitTimeoutContinueOnSkip = true면 Dialog 본체를 건너뛰고 다음 스텝으로
                        if (step.waitTimeoutContinueOnSkip)
                        {
                            Debug.Log($"[TutorialManager] Dialog {step.id}: 타임아웃 스킵 — 본체 표시 생략");
                            yield break;
                        }
                    }
                }
            }

            if (step.pauseGame)
                LockInput();

            // 블록 좌표 하이라이트 (드릴 튜토리얼용)
            if (step.highlightBlockCoords != null && step.highlightBlockCoords.Length > 0 && hexGrid != null)
            {
                HashSet<HexCoord> highlightSet = new HashSet<HexCoord>();
                foreach (var c in step.highlightBlockCoords) highlightSet.Add(c);

                foreach (var block in hexGrid.GetAllBlocks())
                {
                    if (block == null) continue;
                    if (highlightSet.Contains(block.Coord))
                        block.SetTutorialGlow(true);
                    else
                        block.SetTutorialDimmed(true);
                }
            }

            // 튜토리얼용 게이지 자동 충전 (Dialog 표시 중 백그라운드로 게이지 상승)
            if (step.id == "tut_hammer_auto_charge")
                StartCoroutine(AutoChargeHammerGaugeCoroutine());
            else if (step.id == "tut_swap_auto_charge")
                StartCoroutine(AutoChargeSwapGaugeCoroutine());
            else if (step.id == "tut_linedraw_auto_charge")
                StartCoroutine(AutoChargeLineGaugeCoroutine());

            // Stage 6 망치 튜토리얼: "빨간 블록을 매칭할 때마다..." 설명 시점에 필드의 모든 Red 블록 하이라이트
            if (step.id == "tut_hammer_gauge_explain" && hexGrid != null)
            {
                foreach (var block in hexGrid.GetAllBlocks())
                {
                    if (block == null || block.Data == null) continue;
                    if (block.Data.gemType == GemType.Red)
                        block.SetTutorialGlow(true);
                    else
                        block.SetTutorialDimmed(true);
                }
            }

            // Stage 11 스왑 튜토리얼: "녹색 블록이 제거될 때마다..." 설명 시점에 필드의 모든 Green 블록 하이라이트
            if (step.id == "tut_swap_gauge_explain" && hexGrid != null)
            {
                foreach (var block in hexGrid.GetAllBlocks())
                {
                    if (block == null || block.Data == null) continue;
                    if (block.Data.gemType == GemType.Green)
                        block.SetTutorialGlow(true);
                    else
                        block.SetTutorialDimmed(true);
                }
            }

            // Stage 17 라인 튜토리얼: "보라 블록이 제거될 때마다..." 설명 시점에 필드의 모든 Purple 블록 하이라이트
            if (step.id == "tut_linedraw_gauge_explain" && hexGrid != null)
            {
                foreach (var block in hexGrid.GetAllBlocks())
                {
                    if (block == null || block.Data == null) continue;
                    if (block.Data.gemType == GemType.Purple)
                        block.SetTutorialGlow(true);
                    else
                        block.SetTutorialDimmed(true);
                }
            }

            // Stage 11 스왑 튜토리얼: 스왑 완료 후 "폭탄이 될 5개 Blue" 밝게 표시
            // 스왑 후 Blue: (-1,1), (-1,0), (0,-1), (1,-1), (0,1) — 회전으로 (0,0)에 Blue가 오면 폭탄 5매칭
            if (step.id == "tut_swap_effect" && hexGrid != null)
            {
                var bombBlueCoords = new HashSet<HexCoord>
                {
                    new HexCoord(-1, 1),
                    new HexCoord(-1, 0),
                    new HexCoord( 0,-1),
                    new HexCoord( 1,-1),
                    new HexCoord( 0, 1),
                };
                foreach (var block in hexGrid.GetAllBlocks())
                {
                    if (block == null || block.Data == null) continue;
                    if (bombBlueCoords.Contains(block.Coord))
                        block.SetTutorialGlow(true);
                    else
                        block.SetTutorialDimmed(true);
                }
            }

            // Stage 6 망치 튜토리얼: "블록 터치 안내" Dialog에서 망치 사용 타겟 블록 1개 하이라이트
            // (빨간색/특수블록 제외 — 게이지 충전용 빨간은 보존, 일반 블록 중 하나 선정)
            if (step.id == "tut_hammer_target_block" && hexGrid != null)
            {
                var candidates = new List<HexBlock>();
                foreach (var block in hexGrid.GetAllBlocks())
                {
                    if (block == null || block.Data == null) continue;
                    if (block.Data.gemType == GemType.None) continue;
                    if (block.Data.gemType == GemType.Red) continue;
                    if (block.Data.specialType != SpecialBlockType.None) continue;
                    candidates.Add(block);
                }
                if (candidates.Count > 0)
                {
                    HexBlock target = candidates[UnityEngine.Random.Range(0, candidates.Count)];
                    foreach (var block in hexGrid.GetAllBlocks())
                    {
                        if (block == null) continue;
                        if (block == target)
                            block.SetTutorialGlow(true);
                        else
                            block.SetTutorialDimmed(true);
                    }
                }
            }

            // 드릴 하이라이트 (동적 좌표 — 드릴 생성 위치 강조)
            // 스테이지 4/11 공통: pendingDrillCoord가 저장된 시점(DrillCreatedPause) 이후 Dialog에서 사용
            if ((step.id == "s11_drill_explain" || step.id == "s4_drill_explain")
                && pendingDrillCoord.HasValue && hexGrid != null)
            {
                foreach (var block in hexGrid.GetAllBlocks())
                {
                    if (block == null) continue;
                    if (block.Coord.Equals(pendingDrillCoord.Value))
                        block.SetTutorialGlow(true);
                    else
                        block.SetTutorialDimmed(true);
                }
            }

            // 대화/말풍선 표시
            waitingForTap = true;
            bool showTap = step.autoAdvanceDelay <= 0f;
            Action onTapHandler = WrapTapWithCooldown(() => { waitingForTap = false; });
            bool useSpeechBubble = step.useHighlight && tutorialUI != null;

            if (useSpeechBubble)
            {
                // UI 타겟 옆 말풍선 (직관적 UX)
                ApplyHighlightDialogFromStep(step, showTap, onTapHandler);
            }
            else if (tutorialUI != null)
            {
                // 하단 대화 패널 (일반 설명) — iconSpecialType 설정 시 왼쪽 아이콘 + 오른쪽 정렬 텍스트
                Sprite iconSprite = ResolveSpecialIconSprite(step.iconSpecialType);
                tutorialUI.ShowDialog(step.characterName, step.title, step.message, showTap, onTapHandler, iconSprite);
            }

            // 자동 진행 또는 탭 대기
            if (step.autoAdvanceDelay > 0f)
            {
                yield return new WaitForSecondsRealtime(step.autoAdvanceDelay);
            }
            else
            {
                while (waitingForTap)
                    yield return null;
            }

            // 정리 — 다음 스텝도 하이라이트면 유지 (부드러운 몰딩 전환)
            if (tutorialUI != null)
            {
                if (useSpeechBubble)
                {
                    tutorialUI.HideSpeechBubble();
                    if (!IsNextStepHighlighting())
                        tutorialUI.HideHighlight();
                }
                else
                {
                    tutorialUI.HideDialog();
                    if (!IsNextStepHighlighting())
                        tutorialUI.HideHighlight();
                }
            }

            // 블록 하이라이트 정리
            // ※ "tut_hammer_charged"는 정리하지 않고 유지 → 이후 WaitForEvent(HammerUsed)까지 타겟 블록 글로우 지속,
            //   WaitForEvent 종료 시 ClearBlockHighlights가 호출되어 자연스럽게 정리됨
            if ((step.highlightBlockCoords != null && step.highlightBlockCoords.Length > 0) ||
                step.id == "s11_drill_explain" || step.id == "s4_drill_explain" ||
                step.id == "tut_hammer_gauge_explain" || step.id == "tut_swap_gauge_explain" ||
                step.id == "tut_swap_effect" || step.id == "tut_linedraw_gauge_explain")
            {
                ClearBlockHighlights();
            }

            // ★ 레이스 방지: 다음 스텝이 WaitForEvent 또는 waitForEventBeforeShow가 설정된 Dialog라면
            //   BRS 재개 전에 미리 wait 상태 설정. (BRS pause 해제 직후 이벤트가 wait 시작 전에 발생해도 포착)
            if (currentSequence != null && currentStepIndex + 1 < currentSequence.steps.Length)
            {
                var nextStep = currentSequence.steps[currentStepIndex + 1];
                TutorialWaitEvent presetEvent = TutorialWaitEvent.None;

                if (nextStep.type == TutorialStepType.WaitForEvent &&
                    nextStep.waitEvent != TutorialWaitEvent.None)
                    presetEvent = nextStep.waitEvent;
                else if (nextStep.type == TutorialStepType.Dialog &&
                         nextStep.waitForEventBeforeShow != TutorialWaitEvent.None)
                    presetEvent = nextStep.waitForEventBeforeShow;

                if (presetEvent != TutorialWaitEvent.None)
                {
                    waitingForEvent = true;
                    pendingWaitEvent = presetEvent;
                    lastFiredEvent = TutorialWaitEvent.None; // 새 preset 시작, 이전 캐시 초기화
                    Debug.Log($"[TutorialManager] 다음 이벤트 프리셋: {presetEvent}");
                }
            }

            // Dialog에서 resume 처리 (매칭/드릴 pause 후 탭 시)
            if (isPausedForTutorial)
            {
                isPausedForTutorial = false;
                Debug.Log("[TutorialManager] BRS pause 해제 (Dialog 탭)");
            }

            if (step.pauseGame)
                UnlockInput();
        }

        // ── Highlight 스텝 ── (항상 말풍선 모드)
        private IEnumerator HandleHighlightStep(TutorialStep step)
        {
            if (step.pauseGame)
                LockInput();

            if (tutorialUI != null)
            {
                waitingForTap = true;
                Action onTapHandler = WrapTapWithCooldown(() => { waitingForTap = false; });
                ApplyHighlightDialogFromStep(step, true, onTapHandler);
            }

            while (waitingForTap)
                yield return null;

            if (tutorialUI != null)
            {
                tutorialUI.HideSpeechBubble();
                if (!IsNextStepHighlighting())
                    tutorialUI.HideHighlight();
            }

            if (step.pauseGame)
                UnlockInput();
        }

        /// <summary>
        /// 스텝의 하이라이트 정보 + 말풍선 설명을 TutorialUI에 요청.
        /// UI 타겟이 있으면 말풍선이 해당 UI 옆에 꼬리를 달고 등장.
        /// </summary>
        private void ApplyHighlightDialogFromStep(TutorialStep step, bool showTap, Action onTap)
        {
            if (tutorialUI == null) return;

            // 1) UI 이름 기반 자동 타겟팅 (우선순위 최상)
            if (!string.IsNullOrEmpty(step.highlightTargetName))
            {
                RectTransform target = FindUITarget(step.highlightTargetName);
                if (target != null)
                {
                    tutorialUI.ShowHighlightDialog(target, step.highlightPadding,
                        step.characterName, step.title, step.message, showTap, onTap);
                    return;
                }
                Debug.LogWarning($"[TutorialManager] highlightTargetName='{step.highlightTargetName}'을 찾지 못함 — 좌표 폴백");
            }

            // 2) 직사각형 크기 지정
            if (step.highlightSize.x > 0f && step.highlightSize.y > 0f)
            {
                tutorialUI.ShowHighlightDialog(step.highlightScreenPos, step.highlightSize,
                    step.characterName, step.title, step.message, showTap, onTap);
                return;
            }

            // 3) 원형 (하위 호환)
            tutorialUI.ShowHighlightDialogCircle(step.highlightScreenPos, step.highlightRadius,
                step.characterName, step.title, step.message, showTap, onTap);
        }

        /// <summary>
        /// 스텝의 하이라이트 정보로 TutorialUI에 요청 전달 (말풍선 없이 하이라이트만).
        /// highlightTargetName이 있으면 UI 자동 탐색, 없으면 좌표 기반.
        /// </summary>
        private void ApplyHighlightFromStep(TutorialStep step)
        {
            if (tutorialUI == null) return;

            // 1) UI 이름 기반 자동 타겟팅 (우선순위 최상)
            if (!string.IsNullOrEmpty(step.highlightTargetName))
            {
                RectTransform target = FindUITarget(step.highlightTargetName);
                if (target != null)
                {
                    tutorialUI.ShowHighlightForTarget(target, step.highlightPadding);
                    return;
                }
                Debug.LogWarning($"[TutorialManager] highlightTargetName='{step.highlightTargetName}'을 찾지 못함 — 좌표 폴백");
            }

            // 2) 직사각형 크기 지정
            if (step.highlightSize.x > 0f && step.highlightSize.y > 0f)
            {
                tutorialUI.ShowHighlightRect(step.highlightScreenPos, step.highlightSize);
                return;
            }

            // 3) 원형 (하위 호환)
            tutorialUI.ShowHighlight(step.highlightScreenPos, step.highlightRadius);
        }

        /// <summary>
        /// 다음 Dialog/Highlight 스텝이 하이라이트를 사용하는지 확인.
        /// 연속 하이라이트 시 숨김 스킵 → 부드러운 몰딩 전환.
        /// </summary>
        private bool IsNextStepHighlighting()
        {
            if (currentSequence == null) return false;
            int nextIdx = currentStepIndex + 1;
            if (nextIdx >= currentSequence.steps.Length) return false;

            var next = currentSequence.steps[nextIdx];
            if (next.type != TutorialStepType.Dialog && next.type != TutorialStepType.Highlight)
                return false;

            return next.useHighlight || !string.IsNullOrEmpty(next.highlightTargetName);
        }

        /// <summary>
        /// 튜토리얼 대화/말풍선 탭 쿨다운: 표시 직후 1초는 탭 무시 — 실수로 스킵 방지.
        /// </summary>
        private const float TAP_COOLDOWN_SECONDS = 1.0f;
        private Action WrapTapWithCooldown(Action inner)
        {
            float openTime = Time.unscaledTime;
            return () =>
            {
                if (Time.unscaledTime - openTime < TAP_COOLDOWN_SECONDS) return;
                inner?.Invoke();
            };
        }

        /// <summary>
        /// 특수블록 타입 → 대화창 아이콘 스프라이트 (해금 인트로용)
        /// </summary>
        private Sprite ResolveSpecialIconSprite(SpecialBlockType type)
        {
            switch (type)
            {
                case SpecialBlockType.Drill:   return HexBlock.GetDrillAnyIconSprite();
                case SpecialBlockType.Bomb:    return BombBlockSystem.GetBombIconSprite();
                case SpecialBlockType.Drone:   return DroneBlockSystem.GetDroneIconSprite();
                case SpecialBlockType.Rainbow: return DonutBlockSystem.GetDonutIconSprite();
                case SpecialBlockType.XBlock:  return XBlockSystem.GetXBlockIconSprite();
                default: return null;
            }
        }

        /// <summary>
        /// UI 타겟(버튼 등) 위치에 손가락 가이드 표시 — 월드 → Canvas 로컬 좌표 변환.
        /// </summary>
        private void ShowFingerGuideAtUI(string targetName)
        {
            if (tutorialUI == null) return;
            RectTransform target = FindUITarget(targetName);
            if (target == null) return;

            Canvas canvas = tutorialUI.GetComponentInParent<Canvas>();
            if (canvas == null) return;
            RectTransform canvasRt = canvas.transform as RectTransform;
            if (canvasRt == null) return;

            Vector3[] worldCorners = new Vector3[4];
            target.GetWorldCorners(worldCorners);
            Vector3 worldCenter = (worldCorners[0] + worldCorners[2]) * 0.5f;
            Vector2 localCenter = canvasRt.InverseTransformPoint(worldCenter);

            // ★ 새 손끝 구조: fingerGuideObj 원점 = 손끝(탭 지점)
            //   → 오프셋 없이 버튼 중심을 직접 넘기면 최저점에서 손끝이 버튼 중앙에 일치
            tutorialUI.ShowFingerGuide(localCenter);
        }

        /// <summary>
        /// 블록들의 월드 중심을 Canvas 로컬 좌표로 변환해 손가락 가이드를 표시한다.
        /// 블록의 RectTransform.anchoredPosition은 HexGrid 기준이므로 그대로 쓰면
        /// Canvas 중앙에 맞춰진 FingerGuide 위치와 어긋난다 — 반드시 월드→캔버스 변환 필요.
        ///
        /// ★ 새 손끝 구조: fingerGuideObj 원점 = 손끝(탭 지점).
        ///   기본 yOffset = 0 → 최저점(프레스)에서 손끝이 정확히 블록 중심에 일치.
        ///   몸통은 손끝 위쪽(-10, +40)에 배치되어 자연스럽게 블록 위에서 누르는 구도.
        /// </summary>
        private void ShowFingerGuideAtBlocks(System.Collections.Generic.IEnumerable<HexBlock> blocks, float yOffset = 0f)
        {
            if (tutorialUI == null || blocks == null) return;
            Canvas canvas = tutorialUI.GetComponentInParent<Canvas>();
            if (canvas == null) return;
            RectTransform canvasRt = canvas.transform as RectTransform;
            if (canvasRt == null) return;

            Vector3 worldSum = Vector3.zero;
            int count = 0;
            foreach (var b in blocks)
            {
                if (b == null) continue;
                RectTransform brt = b.GetComponent<RectTransform>();
                if (brt == null) continue;
                Vector3[] corners = new Vector3[4];
                brt.GetWorldCorners(corners);
                worldSum += (corners[0] + corners[2]) * 0.5f;
                count++;
            }
            if (count == 0) return;

            Vector3 worldCenter = worldSum / count;
            Vector2 localCenter = canvasRt.InverseTransformPoint(worldCenter);
            // 손끝이 블록 중심을 직접 탭하도록 (yOffset=0 기본)
            tutorialUI.ShowFingerGuide(localCenter + new Vector2(0f, yOffset));
        }

        /// <summary>
        /// 라인 드래그 경로 블록 Glow + 나머지 Dim 재적용.
        /// pendingLineDrawPath 없으면 Stage 17 기본 경로로 복원.
        /// </summary>
        private void ApplyLineDrawPathGlow()
        {
            if (hexGrid == null) return;

            // 안전장치: pending 경로가 비어있으면 Stage 17 기본 경로로 재설정
            if (pendingLineDrawPath == null || pendingLineDrawPath.Count < 2)
            {
                pendingLineDrawPath = new List<HexCoord>
                {
                    new HexCoord(0, 0), new HexCoord(1, -1),
                    new HexCoord(2, -1), new HexCoord(2, 0),
                    new HexCoord(3, 0),
                };
            }

            var pathSet = new HashSet<HexCoord>(pendingLineDrawPath);
            foreach (var block in hexGrid.GetAllBlocks())
            {
                if (block == null) continue;
                if (pathSet.Contains(block.Coord))
                    block.SetTutorialGlow(true);
                else
                    block.SetTutorialDimmed(true);
            }
            Debug.Log($"[TutorialManager] 라인 경로 Glow 적용: {pendingLineDrawPath.Count}개 블록");
        }

        /// <summary>
        /// 스왑 타겟 2블록 Glow + 나머지 Dim 재적용 (idempotent — 재진입 시 재적용).
        /// pending 좌표가 없으면 Stage 11 기본값 (0,0)↔(2,0)으로 복원.
        /// </summary>
        private void ApplySwapTargetGlow()
        {
            if (hexGrid == null) return;

            // 안전장치: pending 좌표가 비어있으면 Stage 11 기본값으로 재설정
            if (!pendingSwapSourceCoord.HasValue) pendingSwapSourceCoord = new HexCoord(0, 0);
            if (!pendingSwapDestCoord.HasValue) pendingSwapDestCoord = new HexCoord(2, 0);

            HexCoord src = pendingSwapSourceCoord.Value;
            HexCoord dst = pendingSwapDestCoord.Value;

            foreach (var block in hexGrid.GetAllBlocks())
            {
                if (block == null) continue;
                if (block.Coord.Equals(src) || block.Coord.Equals(dst))
                    block.SetTutorialGlow(true);
                else
                    block.SetTutorialDimmed(true);
            }
            Debug.Log($"[TutorialManager] 스왑 타겟 Glow 적용: src={src}, dst={dst}");
        }

        /// <summary>
        /// 두 블록 사이에 드래그 연결선 표시 (스왑 튜토리얼).
        /// 블록 월드 좌표를 Canvas 로컬 좌표로 변환해 TutorialUI에 전달.
        /// </summary>
        private void ShowDragLineAtBlocks(HexBlock srcBlock, HexBlock dstBlock)
        {
            if (tutorialUI == null || srcBlock == null || dstBlock == null) return;
            Canvas canvas = tutorialUI.GetComponentInParent<Canvas>();
            if (canvas == null) return;
            RectTransform canvasRt = canvas.transform as RectTransform;
            if (canvasRt == null) return;

            RectTransform srcRt = srcBlock.GetComponent<RectTransform>();
            RectTransform dstRt = dstBlock.GetComponent<RectTransform>();
            if (srcRt == null || dstRt == null) return;

            Vector3[] srcCorners = new Vector3[4];
            Vector3[] dstCorners = new Vector3[4];
            srcRt.GetWorldCorners(srcCorners);
            dstRt.GetWorldCorners(dstCorners);

            Vector3 srcWorld = (srcCorners[0] + srcCorners[2]) * 0.5f;
            Vector3 dstWorld = (dstCorners[0] + dstCorners[2]) * 0.5f;
            Vector2 srcLocal = canvasRt.InverseTransformPoint(srcWorld);
            Vector2 dstLocal = canvasRt.InverseTransformPoint(dstWorld);

            tutorialUI.ShowDragLine(srcLocal, dstLocal);
        }

        /// <summary>
        /// 두 블록 사이 드래그 손가락 애니메이션 표시 (스왑 튜토리얼용).
        /// 월드 좌표 → Canvas 로컬 좌표로 변환하여 TutorialUI에 전달.
        ///
        /// ★ 새 손끝 구조: 손끝이 원점 → yOffset = 0으로 손끝이 정확히 블록 중심을 드래그.
        /// </summary>
        private void ShowFingerDragAtBlocks(HexBlock srcBlock, HexBlock dstBlock, float yOffset = 0f)
        {
            if (tutorialUI == null || srcBlock == null || dstBlock == null) return;
            Canvas canvas = tutorialUI.GetComponentInParent<Canvas>();
            if (canvas == null) return;
            RectTransform canvasRt = canvas.transform as RectTransform;
            if (canvasRt == null) return;

            RectTransform srcRt = srcBlock.GetComponent<RectTransform>();
            RectTransform dstRt = dstBlock.GetComponent<RectTransform>();
            if (srcRt == null || dstRt == null) return;

            Vector3[] srcCorners = new Vector3[4];
            Vector3[] dstCorners = new Vector3[4];
            srcRt.GetWorldCorners(srcCorners);
            dstRt.GetWorldCorners(dstCorners);

            Vector3 srcWorld = (srcCorners[0] + srcCorners[2]) * 0.5f;
            Vector3 dstWorld = (dstCorners[0] + dstCorners[2]) * 0.5f;

            Vector2 srcLocal = (Vector2)canvasRt.InverseTransformPoint(srcWorld) + new Vector2(0f, yOffset);
            Vector2 dstLocal = (Vector2)canvasRt.InverseTransformPoint(dstWorld) + new Vector2(0f, yOffset);

            tutorialUI.ShowFingerDrag(srcLocal, dstLocal);
        }

        /// <summary>
        /// 이름으로 실제 UI의 RectTransform 탐색 (비활성 포함).
        /// </summary>
        private RectTransform FindUITarget(string name)
        {
            // 1차: 활성 오브젝트만 탐색
            GameObject obj = GameObject.Find(name);
            if (obj != null)
                return obj.GetComponent<RectTransform>();

            // 2차: Resources.FindObjectsOfTypeAll로 비활성 포함 탐색
            var all = Resources.FindObjectsOfTypeAll<RectTransform>();
            foreach (var rt in all)
            {
                if (rt == null) continue;
                if (rt.gameObject.name != name) continue;
                // 씬에 속한 것만 (프리팹/에셋 제외)
                if (rt.gameObject.scene.IsValid() && rt.gameObject.activeInHierarchy)
                    return rt;
            }
            // 3차: 이름만 맞으면 반환 (비활성도 허용)
            foreach (var rt in all)
            {
                if (rt == null) continue;
                if (rt.gameObject.name == name && rt.gameObject.scene.IsValid())
                    return rt;
            }
            return null;
        }

        // ── ForcedAction 스텝 ──
        private HexBlock[] highlightedCluster = null;

        private IEnumerator HandleForcedActionStep(TutorialStep step)
        {
            // ★ 통일 정책: characterName 설정된 ForcedAction 스텝은 모두 하단 Dialog Panel UI 사용
            //   - 다른 튜토리얼 단계(Dialog/스왑 드래그/라인 드래그 등)와 동일한 포맷 보장
            //   - 메시지 비어있으면 분기에서 ShowDialog/ShowHighlightDialog 호출 (예: 망치 force_click 등)
            //   - characterName 미설정 (구버전) 폴백: useBottomHint→BottomHint, 아니면 HintBanner
            bool useDialogPanel = !string.IsNullOrEmpty(step.characterName);

            // 오버레이 끄기 (게임 보이게) + 힌트 표시
            if (tutorialUI != null)
            {
                tutorialUI.SetDimOverlayActive(false);

                if (useDialogPanel && !string.IsNullOrEmpty(step.message))
                {
                    // ★ 통일 Dialog Panel UI (스왑/라인 드래그 패턴과 동일)
                    string dlgTitle = string.IsNullOrEmpty(step.title) ? "" : step.title;
                    tutorialUI.ShowDialog(step.characterName, dlgTitle, step.message, false, null);
                    tutorialUI.SetDimOverlayActive(false);
                    tutorialUI.SetTapAreaActive(false); // 탭 통과 — 블록/버튼 입력 허용
                }
                else if (!string.IsNullOrEmpty(step.message))
                {
                    // 폴백: characterName 없는 구버전 스텝
                    if (step.useBottomHint)
                        tutorialUI.ShowBottomHint(step.message);
                    else
                        tutorialUI.ShowHintBanner(step.message, 999f);
                }
                tutorialUI.ShowSkipButton(() => SkipTutorial());
            }

            // 분기 우선순위:
            //   1) Stage 6 망치 버튼 강제 클릭 (id 기반)
            //   2) Stage 6 망치 타겟 블록 클릭 (id 기반)
            //   3) forceDrillClick/forceSpecialClick — 특수블록 클릭 강제 (드릴/폭탄/드론/레인보우/X블록)
            //   4) autoHighlight — 자동 클러스터 회전 유도
            //   5) allowedCoords 지정 모드
            bool autoHighlight = (step.allowedCoords == null || step.allowedCoords.Length == 0);
            highlightedCluster = null;

            if (step.id == "tut_hammer_force_click")
            {
                // 망치 버튼 강제 클릭 스텝
                // - 블록/회전 전체 차단 + 차단 시 토스트 "망치 버튼을 클릭해주세요!"
                // - 화면 전체 어둡게 + 망치 버튼만 스포트라이트
                // - 말풍선: "밝게 표시된 망치 버튼을 클릭해주세요!"
                // - 망치 버튼 위에 손가락 가이드 (bounce 애니메이션)
                if (inputSystem != null)
                {
                    inputSystem.SetRestrictedMode(true, new HashSet<HexCoord>(),
                        onBlocked: () => UIManager.Instance?.ShowToast("망치 버튼을 클릭해주세요!"));
                    inputSystem.SetEnabled(true);
                }

                // 스포트라이트 + 말풍선 (ShowHighlightDialog가 둘 다 처리, showTap=false로 탭 비활성)
                RectTransform hammerBtn = FindUITarget("HammerButton");
                if (hammerBtn != null && tutorialUI != null)
                {
                    tutorialUI.ShowHighlightDialog(hammerBtn, 18f,
                        "엘라시온", "충전 완료!",
                        "밝게 표시된 망치 버튼을 클릭해주세요!",
                        false, null);

                    // 스포트라이트 어두운 영역(= 망치 버튼 외 전 영역) 클릭 시 토스트
                    tutorialUI.SetSpotlightClickCallback(() =>
                        UIManager.Instance?.ShowToast("망치 버튼을 클릭해주세요!"));
                }

                // 손가락 가이드 (AnimateFingerBounce 위아래 Sin 움직임)
                ShowFingerGuideAtUI("HammerButton");
            }
            else if (step.id == "tut_swap_force_click")
            {
                // 스왑 버튼 강제 클릭 (망치와 동일 패턴)
                if (inputSystem != null)
                {
                    inputSystem.SetRestrictedMode(true, new HashSet<HexCoord>(),
                        onBlocked: () => UIManager.Instance?.ShowToast("스왑 버튼을 클릭해주세요!"));
                    inputSystem.SetEnabled(true);
                }

                RectTransform swapBtn = FindUITarget("SwapButton");
                if (swapBtn != null && tutorialUI != null)
                {
                    tutorialUI.ShowHighlightDialog(swapBtn, 18f,
                        "엘라시온", "준비 완료!",
                        "밝게 표시된 스왑 버튼을 클릭해주세요!",
                        false, null);

                    tutorialUI.SetSpotlightClickCallback(() =>
                        UIManager.Instance?.ShowToast("스왑 버튼을 클릭해주세요!"));
                }

                ShowFingerGuideAtUI("SwapButton");
            }
            else if (step.id == "tut_linedraw_force_click")
            {
                // 라인(SSD) 버튼 강제 클릭 (망치와 동일 패턴)
                if (inputSystem != null)
                {
                    inputSystem.SetRestrictedMode(true, new HashSet<HexCoord>(),
                        onBlocked: () => UIManager.Instance?.ShowToast("라인 버튼을 클릭해주세요!"));
                    inputSystem.SetEnabled(true);
                }

                RectTransform lineBtn = FindUITarget("LineDrawButton");
                if (lineBtn != null && tutorialUI != null)
                {
                    tutorialUI.ShowHighlightDialog(lineBtn, 18f,
                        "엘라시온", "준비 완료!",
                        "밝게 표시된 라인 버튼을 클릭해주세요!",
                        false, null);

                    tutorialUI.SetSpotlightClickCallback(() =>
                        UIManager.Instance?.ShowToast("라인 버튼을 클릭해주세요!"));
                }

                ShowFingerGuideAtUI("LineDrawButton");
            }
            else if (step.id == "tut_reverse_force_click")
            {
                // 역회전 버튼 강제 클릭 (망치/스왑/라인과 동일 패턴)
                //   - 블록/회전 전체 차단 + 잘못된 곳 탭 시 토스트
                //   - 화면 전체 어둡게 + 역회전 버튼만 스포트라이트 + 손가락 가이드
                if (inputSystem != null)
                {
                    inputSystem.SetRestrictedMode(true, new HashSet<HexCoord>(),
                        onBlocked: () => UIManager.Instance?.ShowToast("역회전 버튼을 클릭해주세요!"));
                    inputSystem.SetEnabled(true);
                }

                RectTransform reverseBtn = FindUITarget("ReverseRotationButton");
                if (reverseBtn != null && tutorialUI != null)
                {
                    tutorialUI.ShowHighlightDialog(reverseBtn, 18f,
                        "엘라시온", "역회전 활성화",
                        "밝게 표시된 역회전 버튼을 클릭해주세요!",
                        false, null);

                    tutorialUI.SetSpotlightClickCallback(() =>
                        UIManager.Instance?.ShowToast("역회전 버튼을 클릭해주세요!"));
                }

                ShowFingerGuideAtUI("ReverseRotationButton");
            }
            else if (step.id == "tut_hammer_target_block" && hexGrid != null)
            {
                // 타겟 설정 (idempotent — OnHammerActivated에서 이미 설정된 경우 건너뜀)
                SelectAndGlowHammerTargetBlock();

                // 타겟 블록 위치에 말풍선 표시 (스포트라이트 포함 — 블록에 초점)
                if (pendingHammerTargetCoord.HasValue && tutorialUI != null)
                {
                    var targetBlock = hexGrid.GetBlock(pendingHammerTargetCoord.Value);
                    if (targetBlock != null)
                    {
                        RectTransform blockRt = targetBlock.GetComponent<RectTransform>();
                        if (blockRt != null)
                        {
                            tutorialUI.ShowHighlightDialog(blockRt, 8f,
                                "엘라시온", "블록 파괴",
                                "망치 아이템으로 블록을 클릭하면 파괴됩니다.",
                                false, null);
                        }

                        // 손가락 가이드: 타겟 블록 중심에 "위→아래 누르기" 애니메이션
                        ShowFingerGuideAtBlocks(new[] { targetBlock });
                    }
                }

                // 입력은 열어둠 (HammerItem이 자체 검증) — InputSystem 제한은 미사용
                if (inputSystem != null) inputSystem.SetEnabled(true);
            }
            else if (step.id == "tut_swap_target_drag" && hexGrid != null)
            {
                // 스왑 타겟 2블록 Glow + 드래그 손가락 애니메이션 + 연결선
                // ★ SwapItem이 isActive=true로 자체 마우스 입력 처리 →
                //    dim 오버레이가 클릭을 가로막지 않도록 Dialog Panel만 띄우고 dim 비활성
                ApplySwapTargetGlow();

                // 기존 Dialog Panel(하단)을 재사용해 안내 메시지 표시 + dim/tapArea 비활성
                if (tutorialUI != null)
                {
                    tutorialUI.ShowDialog("엘라시온", "블록 드래그",
                        "블록 2개를 드래그해서 블록의 블록 자리를 바꿀 수 있습니다!",
                        false, null);
                    // dim 오버레이 + 탭 영역 비활성 → 블록 클릭/드래그 pass-through
                    tutorialUI.SetDimOverlayActive(false);
                    tutorialUI.SetTapAreaActive(false);
                }

                if (pendingSwapSourceCoord.HasValue && pendingSwapDestCoord.HasValue)
                {
                    HexCoord src = pendingSwapSourceCoord.Value;
                    HexCoord dst = pendingSwapDestCoord.Value;

                    HexBlock srcBlock = hexGrid.GetBlock(src);
                    HexBlock dstBlock = hexGrid.GetBlock(dst);

                    // 드래그 연결선 + 손가락 드래그 애니메이션 (src → dst 반복)
                    if (srcBlock != null && dstBlock != null && tutorialUI != null)
                    {
                        ShowDragLineAtBlocks(srcBlock, dstBlock);
                        ShowFingerDragAtBlocks(srcBlock, dstBlock);
                    }
                }

                // 입력은 열어둠 (SwapItem이 자체 Update에서 마우스 처리 — InputSystem 제한 해제만)
                if (inputSystem != null)
                    inputSystem.SetRestrictedMode(false, null);
            }
            else if (step.id == "tut_linedraw_target_drag" && hexGrid != null)
            {
                // 라인 타겟 경로 Glow + 드래그 손가락 애니메이션 + 연결선
                // ★ LineDrawItem이 isActive=true로 자체 마우스 입력 처리 →
                //    dim 오버레이 없이 Dialog Panel만 띄워 입력 통과
                ApplyLineDrawPathGlow();

                // 기존 Dialog Panel(하단) 재사용 — 안내 메시지 + dim/tapArea 비활성
                if (tutorialUI != null)
                {
                    tutorialUI.ShowDialog("엘라시온", "경로 드래그",
                        "밝게 표시된 블록들을 순서대로 드래그해\n한 번에 제거해 보세요!",
                        false, null);
                    tutorialUI.SetDimOverlayActive(false);
                    tutorialUI.SetTapAreaActive(false);
                }

                // 경로 첫 블록 → 마지막 블록 드래그 선 + 손가락 애니메이션
                if (pendingLineDrawPath != null && pendingLineDrawPath.Count >= 2)
                {
                    HexBlock first = hexGrid.GetBlock(pendingLineDrawPath[0]);
                    HexBlock last  = hexGrid.GetBlock(pendingLineDrawPath[pendingLineDrawPath.Count - 1]);
                    if (first != null && last != null && tutorialUI != null)
                    {
                        ShowDragLineAtBlocks(first, last);
                        ShowFingerDragAtBlocks(first, last);
                    }
                }

                // 입력은 열어둠 (LineDrawItem 자체 Update 처리)
                if (inputSystem != null)
                    inputSystem.SetRestrictedMode(false, null);
            }
            else if ((step.forceDrillClick || step.forceSpecialClick) && pendingDrillCoord.HasValue)
            {
                // 특수블록 클릭 강제: 특수블록 좌표만 허용 (드릴/폭탄/드론/레인보우/X블록 공용)
                // ★ 캐스케이드 낙하 완료 대기 (BRS.IsProcessing가 false가 될 때까지)
                //    드릴/드론 등이 생성된 후 BRS pause가 풀리면 드론은 1칸 더 떨어질 수 있음.
                //    pause 직후 즉시 글로우를 잡으면 낙하 전 좌표에 락되어 시각적 어긋남 발생 → 대기 후 재계산.
                var brs = UnityEngine.Object.FindObjectOfType<BlockRemovalSystem>();
                if (brs != null)
                {
                    float waitStart = Time.realtimeSinceStartup;
                    while (brs.IsProcessing && Time.realtimeSinceStartup - waitStart < 2.0f)
                    {
                        yield return null; // 캐스케이드 끝날 때까지 1프레임씩 대기 (최대 2초 안전망)
                    }
                }

                // ★ 캐스케이드 낙하로 특수블록 위치가 달라졌을 수 있으므로, 저장된 좌표의 블록이
                //    여전히 특수블록인지 확인하고, 아니면 그리드를 스캔해 실제 위치로 보정
                HexCoord targetCoord = pendingDrillCoord.Value;
                if (hexGrid != null)
                {
                    var storedBlock = hexGrid.GetBlock(targetCoord);
                    bool stillSpecial = storedBlock != null && storedBlock.Data != null
                        && storedBlock.Data.specialType != SpecialBlockType.None
                        && storedBlock.Data.specialType != SpecialBlockType.MoveBlock
                        && storedBlock.Data.specialType != SpecialBlockType.FixedBlock;
                    if (!stillSpecial)
                    {
                        // 그리드에서 사용 가능한 특수블록(드릴/폭탄/드론/레인보우/X블록) 위치 탐색
                        HexBlock found = null;
                        foreach (var block in hexGrid.GetAllBlocks())
                        {
                            if (block == null || block.Data == null) continue;
                            var st = block.Data.specialType;
                            if (st == SpecialBlockType.Drill || st == SpecialBlockType.Bomb ||
                                st == SpecialBlockType.Drone || st == SpecialBlockType.Rainbow ||
                                st == SpecialBlockType.XBlock)
                            {
                                // 저장된 좌표에서 가까운 블록 우선 (낙하는 같은 열 이내 1~수 칸)
                                if (found == null ||
                                    block.Coord.DistanceTo(pendingDrillCoord.Value) <
                                    found.Coord.DistanceTo(pendingDrillCoord.Value))
                                {
                                    found = block;
                                }
                            }
                        }
                        if (found != null)
                        {
                            targetCoord = found.Coord;
                            pendingDrillCoord = targetCoord; // 보정된 좌표 저장 (SwapItem/이후 호출 일관성)
                            Debug.Log($"[TutorialManager] forceSpecialClick 좌표 보정: 저장={pendingDrillCoord.Value} → 실제={targetCoord} ({found.Data.specialType})");
                        }
                    }
                }

                var coords = new HashSet<HexCoord> { targetCoord };
                if (inputSystem != null)
                {
                    // ★ 잘못된 좌표 탭 시 토스트 표시 (step.blockedToastMessage 있으면 사용, 아니면 기본 메시지)
                    string blockedMsg = string.IsNullOrEmpty(step.blockedToastMessage)
                        ? "밝은 특수 블록을 터치해 주세요!"
                        : step.blockedToastMessage;
                    inputSystem.SetRestrictedMode(true, coords,
                        onBlocked: () => UIManager.Instance?.ShowToast(blockedMsg));
                    inputSystem.SetEnabled(true);
                }

                // 특수블록 글로우 + 나머지 딤
                if (hexGrid != null)
                {
                    foreach (var block in hexGrid.GetAllBlocks())
                    {
                        if (block == null) continue;
                        if (block.Coord.Equals(targetCoord))
                            block.SetTutorialGlow(true);
                        else
                            block.SetTutorialDimmed(true);
                    }
                }

                // 손가락 가이드: 특수블록 위치 (월드 → Canvas 로컬 변환)
                if (step.showFingerGuide && tutorialUI != null && hexGrid != null)
                {
                    var specialBlock = hexGrid.GetBlock(targetCoord);
                    if (specialBlock != null)
                        ShowFingerGuideAtBlocks(new[] { specialBlock });
                }

                // ★ Dialog Panel UI는 메서드 상단에서 통일 처리 (useDialogPanel 분기)
                //   여기서는 step.message가 비어있는 경우에만 fallback 안내 표시
                if (tutorialUI != null && !string.IsNullOrEmpty(step.characterName) && string.IsNullOrEmpty(step.message))
                {
                    string clickTitle = string.IsNullOrEmpty(step.title) ? "특수 블록 발동" : step.title;
                    tutorialUI.ShowDialog(step.characterName, clickTitle,
                        "밝은 특수 블록을 터치해 발동해 보세요!", false, null);
                    tutorialUI.SetDimOverlayActive(false);
                    tutorialUI.SetTapAreaActive(false);
                }
            }
            else if (autoHighlight && matchingSystem != null && hexGrid != null)
            {
                HexBlock[] cluster = matchingSystem.FindMatchableCluster();
                if (cluster != null && cluster.Length == 3)
                {
                    highlightedCluster = cluster;
                    Debug.Log($"[TutorialManager] 매칭 가능 클러스터 발견: {cluster[0].Coord}, {cluster[1].Coord}, {cluster[2].Coord}");

                    // 1) 모든 블록 디밍
                    HashSet<HexCoord> clusterCoords = new HashSet<HexCoord>();
                    foreach (var b in cluster) clusterCoords.Add(b.Coord);

                    foreach (var block in hexGrid.GetAllBlocks())
                    {
                        if (block == null) continue;
                        if (clusterCoords.Contains(block.Coord))
                            block.SetTutorialGlow(true);   // 밝게 + 펄스
                        else
                            block.SetTutorialDimmed(true);  // 어둡게
                    }

                    // 2) 입력 제한: 클러스터 좌표만 허용
                    if (inputSystem != null)
                    {
                        inputSystem.SetRestrictedMode(true, clusterCoords);
                        inputSystem.SetEnabled(true);
                    }

                    // 3) 손가락 가이드: 손끝이 클러스터 중심을 가리키도록 배치 (월드 → Canvas 로컬 변환)
                    ShowFingerGuideAtBlocks(cluster);
                }
                else
                {
                    Debug.LogWarning("[TutorialManager] 매칭 가능 클러스터를 찾지 못함 — 자유 입력");
                    if (inputSystem != null) inputSystem.SetEnabled(true);
                    if (step.showFingerGuide && tutorialUI != null)
                    {
                        // ★ 폴백: 보드 중심 (0,0) 블록에 손가락 가이드 (step.fingerGuidePos가 기본값(0,0)=캔버스 중심이라 엉뚱함)
                        HexBlock centerBlock = hexGrid != null ? hexGrid.GetBlock(new HexCoord(0, 0)) : null;
                        if (centerBlock != null)
                            ShowFingerGuideAtBlocks(new[] { centerBlock });
                        else
                            tutorialUI.ShowFingerGuide(step.fingerGuidePos);
                    }
                }
            }
            else if (!autoHighlight)
            {
                // 지정 좌표로 제한
                var coords = new HashSet<HexCoord>();
                foreach (var c in step.allowedCoords) coords.Add(c);

                // 지정 좌표 글로우 + 나머지 딤
                if (hexGrid != null)
                {
                    foreach (var block in hexGrid.GetAllBlocks())
                    {
                        if (block == null) continue;
                        if (coords.Contains(block.Coord))
                            block.SetTutorialGlow(true);
                        else
                            block.SetTutorialDimmed(true);
                    }
                }

                if (inputSystem != null)
                {
                    // 다른 블록 탭 시 토스트 안내 (회전 강제 스텝에서 사용)
                    // ★ step.blockedToastMessage 지정 시 해당 메시지, 아니면 기본 메시지
                    string blockedMsg = string.IsNullOrEmpty(step.blockedToastMessage)
                        ? "지정된 블록을 탭하여 블록을 회전 시켜주세요!"
                        : step.blockedToastMessage;
                    inputSystem.SetRestrictedMode(true, coords,
                        onBlocked: () => UIManager.Instance?.ShowToast(blockedMsg));
                    inputSystem.SetEnabled(true);
                }

                // 손가락 가이드: 지정 좌표 중심 (월드 → Canvas 로컬 변환)
                if (step.showFingerGuide && tutorialUI != null && hexGrid != null)
                {
                    var targetBlocks = new System.Collections.Generic.List<HexBlock>();
                    foreach (var c in step.allowedCoords)
                    {
                        var block = hexGrid.GetBlock(c);
                        if (block != null) targetBlocks.Add(block);
                    }
                    ShowFingerGuideAtBlocks(targetBlocks);
                }
            }
            else
            {
                // 폴백: 자유 입력 (매칭 시스템/그리드 모두 없을 때)
                if (inputSystem != null) inputSystem.SetEnabled(true);
                if (step.showFingerGuide && tutorialUI != null)
                {
                    // ★ 보드 중심 (0,0) 블록에 손가락 가이드 (step.fingerGuidePos 기본값이 캔버스 중심이라 엉뚱함)
                    HexBlock centerBlock = hexGrid != null ? hexGrid.GetBlock(new HexCoord(0, 0)) : null;
                    if (centerBlock != null)
                        ShowFingerGuideAtBlocks(new[] { centerBlock });
                    else
                        tutorialUI.ShowFingerGuide(step.fingerGuidePos);
                }
            }

            // ForcedAction은 비주얼 설정만 하고 즉시 종료
            // → 정리는 다음 WaitForEvent 스텝 완료 시 또는 시퀀스 완료 시 수행
            yield return null;
        }

        /// <summary>
        /// 모든 블록의 튜토리얼 비주얼 초기화
        /// </summary>
        private void ClearBlockHighlights()
        {
            if (hexGrid == null) return;
            foreach (var block in hexGrid.GetAllBlocks())
            {
                if (block != null)
                    block.ClearTutorialVisuals();
            }
            highlightedCluster = null;
        }

        // ── FreePlayHint 스텝 ──
        private IEnumerator HandleFreePlayHintStep(TutorialStep step)
        {
            // 입력 해제, 힌트만 표시 후 자동 소멸
            UnlockInput();

            if (tutorialUI != null)
            {
                tutorialUI.HideAll();
                tutorialUI.ShowHintBanner(step.message, step.hintDuration);
            }

            yield return new WaitForSecondsRealtime(step.hintDuration + 0.5f);
        }

        // ── WaitForEvent 스텝 ──
        private IEnumerator HandleWaitForEventStep(TutorialStep step)
        {
            // ★ 이전 Dialog 스텝의 preset으로 이미 이벤트가 발생/캡처된 경우 대기 스킵
            //   (BRS가 Dialog tap 직후 즉시 특수블록을 생성해 wait 스텝 시작 전에 이벤트가 끝난 상황)
            bool skipWait = (lastFiredEvent == step.waitEvent && step.waitEvent != TutorialWaitEvent.None);
            if (skipWait)
            {
                Debug.Log($"[TutorialManager] 이벤트 {step.waitEvent} preset에서 이미 캡처됨 — 대기 스킵");
                lastFiredEvent = TutorialWaitEvent.None;
            }
            else
            {
                waitingForEvent = true;
                pendingWaitEvent = step.waitEvent;
            }

            Debug.Log($"[TutorialManager] 이벤트 대기: {step.waitEvent}");

            // 특수블록 튜토리얼 pause 이벤트는 타임아웃 60초 (캐스케이드 대기)
            //   *CreatedPause만 시스템 이벤트(보드 상태 변화 대기) — 60초로 충분
            bool isSpecialPauseEvent = step.waitEvent == TutorialWaitEvent.DrillCreatedPause ||
                                       step.waitEvent == TutorialWaitEvent.BombCreatedPause ||
                                       step.waitEvent == TutorialWaitEvent.DroneCreatedPause ||
                                       step.waitEvent == TutorialWaitEvent.RainbowCreatedPause ||
                                       step.waitEvent == TutorialWaitEvent.XBlockCreatedPause;

            // 플레이어의 명시적 액션(버튼 클릭/블록 파괴/회전/특수블록 탭)에만 발생하는 이벤트 →
            // 타임아웃 스킵 방지를 위해 매우 긴 대기 (실질 무한)
            // ★ MatchHighlightPause는 ForcedAction 회전의 후속 대기 — 사용자가 올바른 좌표를
            //    탭할 때까지 진행 금지. 60초 타임아웃으로 자동 스킵되면 "탭 없이 세션 넘어감"이라
            //    엄격 강제가 무너지므로, 회전 계열 사용자 액션 이벤트와 동일하게 3600초 대기.
            // ★ *Activated 계열(DrillActivated/BombActivated/DroneActivated/RainbowActivated/XBlockActivated)도
            //    사용자가 특수블록을 탭해야 발생하는 이벤트 → 탭할 때까지 종료 금지가 사용자 요구사항.
            bool isUserActionEvent = step.waitEvent == TutorialWaitEvent.HammerUsed ||
                                     step.waitEvent == TutorialWaitEvent.HammerActivated ||
                                     step.waitEvent == TutorialWaitEvent.SwapActivated ||
                                     step.waitEvent == TutorialWaitEvent.SwapUsed ||
                                     step.waitEvent == TutorialWaitEvent.LineDrawActivated ||
                                     step.waitEvent == TutorialWaitEvent.LineDrawUsed ||
                                     step.waitEvent == TutorialWaitEvent.RotationComplete ||
                                     step.waitEvent == TutorialWaitEvent.MatchHighlightPause ||
                                     step.waitEvent == TutorialWaitEvent.DrillActivated ||
                                     step.waitEvent == TutorialWaitEvent.BombActivated ||
                                     step.waitEvent == TutorialWaitEvent.DroneActivated ||
                                     step.waitEvent == TutorialWaitEvent.RainbowActivated ||
                                     step.waitEvent == TutorialWaitEvent.XBlockActivated ||
                                     step.waitEvent == TutorialWaitEvent.ComboActivated;
            float timeout = isUserActionEvent ? 3600f : (isSpecialPauseEvent ? 60f : 15f);
            // ★ step.waitTimeoutOverride > 0이면 카테고리 기본값 무시하고 지정값 사용 (옵셔널 대기 용)
            if (step.waitTimeoutOverride > 0f)
                timeout = step.waitTimeoutOverride;

            if (!skipWait)
            {
                float elapsed = 0f;
                while (waitingForEvent && elapsed < timeout)
                {
                    elapsed += Time.unscaledDeltaTime;
                    yield return null;
                }

                if (waitingForEvent)
                {
                    Debug.LogWarning($"[TutorialManager] 이벤트 대기 타임아웃: {step.waitEvent}");
                    waitingForEvent = false;
                    pendingWaitEvent = TutorialWaitEvent.None;
                }
            }

            // ★ ForcedAction에서 설정한 비주얼/입력 제한 정리
            ClearBlockHighlights();

            // 망치 타겟 제한 해제 — HammerUsed 이벤트 해제/타임아웃 양쪽 모두 커버
            if (step.waitEvent == TutorialWaitEvent.HammerUsed)
                pendingHammerTargetCoord = null;

            if (tutorialUI != null)
            {
                tutorialUI.HideFingerGuide();
                tutorialUI.HideHintBanner();
                tutorialUI.HideBottomHint();    // 하단 힌트(useBottomHint 스텝) 해제
                tutorialUI.HideDialog();        // ForcedAction 중 띄워둔 Dialog Panel 해제
                tutorialUI.HideSpeechBubble();  // ForcedAction에서 설정한 말풍선 해제
                tutorialUI.HideHighlight();     // 스포트라이트도 해제
                tutorialUI.HideDragLine();      // 드래그 연결선 해제
                tutorialUI.SetSpotlightClickCallback(null); // 스포트라이트 클릭 콜백 해제
            }

            if (inputSystem != null)
                inputSystem.SetRestrictedMode(false, null);

            // 캐스케이드 완료 대기 (매칭 후 블록 정리 시간) — pause 이벤트에서는 스킵
            if (!isSpecialPauseEvent)
                yield return new WaitForSecondsRealtime(0.5f);
        }

        // ============================================================
        // 드릴 튜토리얼 콜백 (BRS/DrillBlockSystem에서 호출)
        // ============================================================

        /// <summary>
        /// 매칭 하이라이트 완료 시 호출 (BRS에서 특수 블록 생성 전).
        /// MatchHighlightPause 대기 중이면 pause 걸고 이벤트 해제.
        /// </summary>
        public void OnMatchHighlightComplete()
        {
            if (waitingForEvent && pendingWaitEvent == TutorialWaitEvent.MatchHighlightPause)
            {
                isPausedForTutorial = true;
                lastFiredEvent = pendingWaitEvent;
                waitingForEvent = false;
                pendingWaitEvent = TutorialWaitEvent.None;
                Debug.Log("[TutorialManager] MatchHighlightPause 트리거 — BRS pause 시작");
            }
            else if (WillWaitForEvent(TutorialWaitEvent.MatchHighlightPause))
            {
                // fallback: wait 스텝 시작 전 이벤트 발생 → BRS pause + 캐시 세팅
                isPausedForTutorial = true;
                lastFiredEvent = TutorialWaitEvent.MatchHighlightPause;
                Debug.Log("[TutorialManager] MatchHighlightPause fallback 캐시 — wait 스텝 대기 전 이벤트");
            }
        }

        /// <summary>
        /// 드릴 생성 완료 시 호출 (BRS에서 드릴 블록 생성 후).
        /// DrillCreatedPause 대기 중이면 pause 걸고 이벤트 해제.
        /// </summary>
        public void OnDrillCreated(HexCoord coord)
        {
            pendingDrillCoord = coord;

            if (waitingForEvent && pendingWaitEvent == TutorialWaitEvent.DrillCreatedPause)
            {
                isPausedForTutorial = true;
                lastFiredEvent = pendingWaitEvent;
                waitingForEvent = false;
                pendingWaitEvent = TutorialWaitEvent.None;
                Debug.Log($"[TutorialManager] DrillCreatedPause 트리거 — 드릴 좌표={coord}, BRS pause 시작");
            }
            else if (WillWaitForEvent(TutorialWaitEvent.DrillCreatedPause))
            {
                isPausedForTutorial = true;
                lastFiredEvent = TutorialWaitEvent.DrillCreatedPause;
                Debug.Log($"[TutorialManager] DrillCreatedPause fallback 캐시 — 드릴 좌표={coord}");
            }
        }

        /// <summary>
        /// 드릴 발동 완료 시 호출 (DrillBlockSystem에서 드릴 코루틴 끝).
        /// DrillActivated 대기 중이면 이벤트 해제.
        /// </summary>
        public void OnDrillActivated()
        {
            if (waitingForEvent && pendingWaitEvent == TutorialWaitEvent.DrillActivated)
            {
                lastFiredEvent = pendingWaitEvent;
                waitingForEvent = false;
                pendingWaitEvent = TutorialWaitEvent.None;
                Debug.Log("[TutorialManager] DrillActivated 트리거");
            }
        }

        /// <summary>
        /// 합성(콤보) 발동 시 호출 (SpecialBlockComboSystem.ComboCoroutine 시작점).
        /// ComboActivated 대기 중이면 이벤트 해제.
        /// </summary>
        public void OnComboActivated()
        {
            if (waitingForEvent && pendingWaitEvent == TutorialWaitEvent.ComboActivated)
            {
                lastFiredEvent = pendingWaitEvent;
                waitingForEvent = false;
                pendingWaitEvent = TutorialWaitEvent.None;
                Debug.Log("[TutorialManager] ComboActivated 트리거");
            }
        }

        // ============================================================
        // 폭탄/드론/레인보우/X블록 튜토리얼 콜백
        // ============================================================

        /// <summary>
        /// 폭탄 생성 완료 시 호출 (BRS에서 폭탄 블록 생성 후).
        /// </summary>
        public void OnBombCreated(HexCoord coord)
        {
            pendingSpecialCoord = coord;
            pendingDrillCoord = coord; // forceDrillClick 호환

            if (waitingForEvent && pendingWaitEvent == TutorialWaitEvent.BombCreatedPause)
            {
                isPausedForTutorial = true;
                lastFiredEvent = pendingWaitEvent;
                waitingForEvent = false;
                pendingWaitEvent = TutorialWaitEvent.None;
                Debug.Log($"[TutorialManager] BombCreatedPause 트리거 — 폭탄 좌표={coord}, BRS pause 시작");
            }
            else if (WillWaitForEvent(TutorialWaitEvent.BombCreatedPause))
            {
                isPausedForTutorial = true;
                lastFiredEvent = TutorialWaitEvent.BombCreatedPause;
                Debug.Log($"[TutorialManager] BombCreatedPause fallback 캐시 — 폭탄 좌표={coord}");
            }
        }

        /// <summary>
        /// 폭탄 발동 완료 시 호출 (BombBlockSystem에서 폭탄 코루틴 끝).
        /// </summary>
        public void OnBombActivated()
        {
            if (waitingForEvent && pendingWaitEvent == TutorialWaitEvent.BombActivated)
            {
                lastFiredEvent = pendingWaitEvent;
                waitingForEvent = false;
                pendingWaitEvent = TutorialWaitEvent.None;
                Debug.Log("[TutorialManager] BombActivated 트리거");
            }
        }

        /// <summary>
        /// 드론 생성 완료 시 호출 (BRS에서 드론 블록 생성 후).
        /// </summary>
        public void OnDroneCreated(HexCoord coord)
        {
            pendingSpecialCoord = coord;
            pendingDrillCoord = coord;

            if (waitingForEvent && pendingWaitEvent == TutorialWaitEvent.DroneCreatedPause)
            {
                isPausedForTutorial = true;
                lastFiredEvent = pendingWaitEvent;
                waitingForEvent = false;
                pendingWaitEvent = TutorialWaitEvent.None;
                Debug.Log($"[TutorialManager] DroneCreatedPause 트리거 — 드론 좌표={coord}, BRS pause 시작");
            }
            else if (WillWaitForEvent(TutorialWaitEvent.DroneCreatedPause))
            {
                isPausedForTutorial = true;
                lastFiredEvent = TutorialWaitEvent.DroneCreatedPause;
                Debug.Log($"[TutorialManager] DroneCreatedPause fallback 캐시 — 드론 좌표={coord}");
            }
        }

        /// <summary>
        /// 드론 발동 완료 시 호출 (DroneBlockSystem에서 드론 코루틴 끝).
        /// </summary>
        public void OnDroneActivated()
        {
            if (waitingForEvent && pendingWaitEvent == TutorialWaitEvent.DroneActivated)
            {
                lastFiredEvent = pendingWaitEvent;
                waitingForEvent = false;
                pendingWaitEvent = TutorialWaitEvent.None;
                Debug.Log("[TutorialManager] DroneActivated 트리거");
            }
        }

        /// <summary>
        /// 레인보우타겟 레이저 생성 완료 시 호출 (BRS에서 레인보우 블록 생성 후).
        /// </summary>
        public void OnRainbowCreated(HexCoord coord)
        {
            pendingSpecialCoord = coord;
            pendingDrillCoord = coord;

            if (waitingForEvent && pendingWaitEvent == TutorialWaitEvent.RainbowCreatedPause)
            {
                isPausedForTutorial = true;
                lastFiredEvent = pendingWaitEvent;
                waitingForEvent = false;
                pendingWaitEvent = TutorialWaitEvent.None;
                Debug.Log($"[TutorialManager] RainbowCreatedPause 트리거 — 레인보우 좌표={coord}, BRS pause 시작");
            }
            else if (WillWaitForEvent(TutorialWaitEvent.RainbowCreatedPause))
            {
                isPausedForTutorial = true;
                lastFiredEvent = TutorialWaitEvent.RainbowCreatedPause;
                Debug.Log($"[TutorialManager] RainbowCreatedPause fallback 캐시 — 레인보우 좌표={coord}");
            }
        }

        /// <summary>
        /// 레인보우 발동 완료 시 호출 (DonutBlockSystem에서 코루틴 끝).
        /// </summary>
        public void OnRainbowActivated()
        {
            if (waitingForEvent && pendingWaitEvent == TutorialWaitEvent.RainbowActivated)
            {
                lastFiredEvent = pendingWaitEvent;
                waitingForEvent = false;
                pendingWaitEvent = TutorialWaitEvent.None;
                Debug.Log("[TutorialManager] RainbowActivated 트리거");
            }
        }

        /// <summary>
        /// X블록 생성 완료 시 호출 (BRS에서 X블록 생성 후).
        /// </summary>
        public void OnXBlockCreated(HexCoord coord)
        {
            pendingSpecialCoord = coord;
            pendingDrillCoord = coord;

            if (waitingForEvent && pendingWaitEvent == TutorialWaitEvent.XBlockCreatedPause)
            {
                isPausedForTutorial = true;
                lastFiredEvent = pendingWaitEvent;
                waitingForEvent = false;
                pendingWaitEvent = TutorialWaitEvent.None;
                Debug.Log($"[TutorialManager] XBlockCreatedPause 트리거 — X블록 좌표={coord}, BRS pause 시작");
            }
            else if (WillWaitForEvent(TutorialWaitEvent.XBlockCreatedPause))
            {
                isPausedForTutorial = true;
                lastFiredEvent = TutorialWaitEvent.XBlockCreatedPause;
                Debug.Log($"[TutorialManager] XBlockCreatedPause fallback 캐시 — X블록 좌표={coord}");
            }
        }

        /// <summary>
        /// X블록 발동 완료 시 호출 (XBlockSystem에서 코루틴 끝).
        /// </summary>
        public void OnXBlockActivated()
        {
            if (waitingForEvent && pendingWaitEvent == TutorialWaitEvent.XBlockActivated)
            {
                lastFiredEvent = pendingWaitEvent;
                waitingForEvent = false;
                pendingWaitEvent = TutorialWaitEvent.None;
                Debug.Log("[TutorialManager] XBlockActivated 트리거");
            }
        }

        /// <summary>
        /// 망치 아이템 사용 완료 시 호출 (HammerItem.ProcessHammerUsage 끝).
        /// HammerUsed 대기 중이면 이벤트 해제.
        /// </summary>
        public void OnHammerUsed()
        {
            // 타겟 제한 해제 (망치 사용 완료 → 더 이상 블록 제한 필요 없음)
            pendingHammerTargetCoord = null;

            if (waitingForEvent && pendingWaitEvent == TutorialWaitEvent.HammerUsed)
            {
                lastFiredEvent = pendingWaitEvent;
                waitingForEvent = false;
                pendingWaitEvent = TutorialWaitEvent.None;
                Debug.Log("[TutorialManager] HammerUsed 트리거");
            }
        }

        /// <summary>
        /// 망치 버튼 클릭 → UseReady 상태 전환 시 호출 (HammerGauge.OnHammerButtonClicked).
        /// HammerActivated 대기 중이면 이벤트 해제 + 즉시 타겟 블록 선정 (race 방지).
        /// </summary>
        public void OnHammerActivated()
        {
            if (waitingForEvent && pendingWaitEvent == TutorialWaitEvent.HammerActivated)
            {
                lastFiredEvent = pendingWaitEvent;
                waitingForEvent = false;
                pendingWaitEvent = TutorialWaitEvent.None;
                Debug.Log("[TutorialManager] HammerActivated 트리거");

                // ★ 이벤트 해제 즉시 타겟 설정 — 다음 스텝 진입 전 플레이어가 블록을 클릭해도
                //   HammerItem의 튜토리얼 가드가 작동하도록 pendingHammerTargetCoord를 바로 세팅
                SelectAndGlowHammerTargetBlock();
            }
        }

        /// <summary>
        /// 역회전 버튼 클릭 → 토글 활성화 시 호출 (ReverseRotationItem.Activate).
        /// </summary>
        public void OnReverseActivated()
        {
            if (waitingForEvent && pendingWaitEvent == TutorialWaitEvent.ReverseActivated)
            {
                lastFiredEvent = pendingWaitEvent;
                waitingForEvent = false;
                pendingWaitEvent = TutorialWaitEvent.None;
                Debug.Log("[TutorialManager] ReverseActivated 트리거");
            }
            else if (WillWaitForEvent(TutorialWaitEvent.ReverseActivated))
            {
                lastFiredEvent = TutorialWaitEvent.ReverseActivated;
                Debug.Log("[TutorialManager] ReverseActivated fallback 캐시");
            }
        }

        /// <summary>
        /// 스왑 버튼 클릭 → 타겟 선택 대기 상태 전환 시 호출 (SwapItem).
        /// </summary>
        public void OnSwapActivated()
        {
            if (waitingForEvent && pendingWaitEvent == TutorialWaitEvent.SwapActivated)
            {
                lastFiredEvent = pendingWaitEvent;
                waitingForEvent = false;
                pendingWaitEvent = TutorialWaitEvent.None;
                Debug.Log("[TutorialManager] SwapActivated 트리거");
            }
            else if (WillWaitForEvent(TutorialWaitEvent.SwapActivated))
            {
                lastFiredEvent = TutorialWaitEvent.SwapActivated;
                Debug.Log("[TutorialManager] SwapActivated fallback 캐시");
            }
        }

        /// <summary>
        /// 스왑 아이템 사용 완료 시 호출 (두 블록 위치 교환 성공 후).
        /// </summary>
        public void OnSwapUsed()
        {
            // 스왑 타겟 제한 해제 — 스왑 완료 후 자유 선택 허용
            pendingSwapSourceCoord = null;
            pendingSwapDestCoord = null;

            if (waitingForEvent && pendingWaitEvent == TutorialWaitEvent.SwapUsed)
            {
                lastFiredEvent = pendingWaitEvent;
                waitingForEvent = false;
                pendingWaitEvent = TutorialWaitEvent.None;
                Debug.Log("[TutorialManager] SwapUsed 트리거");
            }
            else if (WillWaitForEvent(TutorialWaitEvent.SwapUsed))
            {
                lastFiredEvent = TutorialWaitEvent.SwapUsed;
                Debug.Log("[TutorialManager] SwapUsed fallback 캐시");
            }
        }

        /// <summary>
        /// 라인(SSD) 버튼 클릭 → 드래그 대기 상태 전환 시 호출 (LineDrawItem).
        /// </summary>
        public void OnLineDrawActivated()
        {
            if (waitingForEvent && pendingWaitEvent == TutorialWaitEvent.LineDrawActivated)
            {
                lastFiredEvent = pendingWaitEvent;
                waitingForEvent = false;
                pendingWaitEvent = TutorialWaitEvent.None;
                Debug.Log("[TutorialManager] LineDrawActivated 트리거");
            }
            else if (WillWaitForEvent(TutorialWaitEvent.LineDrawActivated))
            {
                lastFiredEvent = TutorialWaitEvent.LineDrawActivated;
                Debug.Log("[TutorialManager] LineDrawActivated fallback 캐시");
            }
        }

        /// <summary>
        /// 라인(SSD) 드래그 완료 시 호출 (경로 블록 파괴 후).
        /// </summary>
        public void OnLineDrawUsed()
        {
            if (waitingForEvent && pendingWaitEvent == TutorialWaitEvent.LineDrawUsed)
            {
                lastFiredEvent = pendingWaitEvent;
                waitingForEvent = false;
                pendingWaitEvent = TutorialWaitEvent.None;
                Debug.Log("[TutorialManager] LineDrawUsed 트리거");
            }
            else if (WillWaitForEvent(TutorialWaitEvent.LineDrawUsed))
            {
                lastFiredEvent = TutorialWaitEvent.LineDrawUsed;
                Debug.Log("[TutorialManager] LineDrawUsed fallback 캐시");
            }
        }

        /// <summary>
        /// Stage 6 망치 튜토리얼: 타겟 블록 1개 선정 + Glow + pendingHammerTargetCoord 저장.
        /// 타겟 좌표는 **한 번만** 선정하되 Glow는 **매 호출마다 재적용** — 이전 스텝의
        /// ClearBlockHighlights로 Glow가 지워진 후 다음 스텝 진입 시 다시 표시되도록.
        /// </summary>
        private void SelectAndGlowHammerTargetBlock()
        {
            if (hexGrid == null) return;

            // 타겟 좌표 결정 (없으면 새로 선정, 있으면 기존 재사용)
            if (!pendingHammerTargetCoord.HasValue)
            {
                var candidates = new List<HexBlock>();
                foreach (var block in hexGrid.GetAllBlocks())
                {
                    if (block == null || block.Data == null) continue;
                    if (block.Data.gemType == GemType.None) continue;
                    if (block.Data.gemType == GemType.Red) continue;
                    if (block.Data.specialType != SpecialBlockType.None) continue;
                    candidates.Add(block);
                }
                if (candidates.Count == 0) return;
                HexBlock selected = candidates[UnityEngine.Random.Range(0, candidates.Count)];
                pendingHammerTargetCoord = selected.Coord;
            }

            // Glow + Dim 매번 재적용 (이전 ClearBlockHighlights로 지워진 경우 복원)
            HexCoord targetCoord = pendingHammerTargetCoord.Value;
            foreach (var block in hexGrid.GetAllBlocks())
            {
                if (block == null) continue;
                if (block.Coord.Equals(targetCoord)) block.SetTutorialGlow(true);
                else block.SetTutorialDimmed(true);
            }
        }

        /// <summary>
        /// 튜토리얼용 망치 게이지 자동 충전 (Stage 6 전용).
        /// 5씩 10회 = 1 레이어(50) 충전 — 충전 과정을 플레이어가 시각적으로 볼 수 있게 0.15s 간격.
        /// </summary>
        private IEnumerator AutoChargeHammerGaugeCoroutine()
        {
            var gauge = JewelsHexaPuzzle.Items.HammerGauge.Instance;
            if (gauge == null) yield break;
            for (int i = 0; i < 10; i++)
            {
                gauge.AddGauge(5);
                yield return new WaitForSecondsRealtime(0.15f);
            }
        }

        /// <summary>
        /// 스왑 게이지 자동 충전 (튜토리얼용). 5씩 10회 = 1 레이어.
        /// </summary>
        private IEnumerator AutoChargeSwapGaugeCoroutine()
        {
            var gauge = JewelsHexaPuzzle.Items.SwapGauge.Instance;
            if (gauge == null) yield break;
            for (int i = 0; i < 10; i++)
            {
                gauge.AddGauge(5);
                yield return new WaitForSecondsRealtime(0.15f);
            }
        }

        /// <summary>
        /// 라인(SSD) 게이지 자동 충전 (튜토리얼용). 5씩 10회 = 1 레이어.
        /// </summary>
        private IEnumerator AutoChargeLineGaugeCoroutine()
        {
            var gauge = JewelsHexaPuzzle.Items.LineGauge.Instance;
            if (gauge == null) yield break;
            for (int i = 0; i < 10; i++)
            {
                gauge.AddGauge(5);
                yield return new WaitForSecondsRealtime(0.15f);
            }
        }

        // ============================================================
        // 시퀀스 완료 / 스킵
        // ============================================================

        private void CompleteSequence()
        {
            if (currentSequence != null && currentSequence.showOnce)
                MarkCompleted(currentSequence.sequenceId);

            // 레벨 해금 상태 동기화 (튜토리얼 완료 시점에 새 해금 확인)
            SyncFeatureUnlocks();

            isTutorialActive = false;
            currentSequence = null;
            currentStepIndex = 0;
            waitingForTap = false;
            waitingForEvent = false;
            pendingWaitEvent = TutorialWaitEvent.None;

            // 블록 하이라이트 정리
            ClearBlockHighlights();

            if (tutorialUI != null) tutorialUI.HideAll();
            UnlockInput();

            OnTutorialEnded?.Invoke();
            Debug.Log("[TutorialManager] 시퀀스 완료");
        }

        /// <summary>
        /// 현재 튜토리얼 스킵 (전체 시퀀스 완료 처리)
        /// </summary>
        public void SkipTutorial()
        {
            if (!isTutorialActive) return;

            Debug.Log($"[TutorialManager] 튜토리얼 스킵: {currentSequence?.sequenceId}");

            if (sequenceCoroutine != null)
            {
                StopCoroutine(sequenceCoroutine);
                sequenceCoroutine = null;
            }

            // BRS pause 해제
            isPausedForTutorial = false;
            pendingDrillCoord = null;
            pendingHammerTargetCoord = null;

            // 입력 제한 해제
            if (inputSystem != null)
                inputSystem.SetRestrictedMode(false, null);

            CompleteSequence();
        }

        /// <summary>
        /// 튜토리얼 완전 중단 + 모든 UI/블록 상태 초기화 (완료 마킹 없음).
        /// 로비 이동/ForceReset 등 튜토리얼 잔류 이미지 제거가 필요한 경우에 호출.
        /// SkipTutorial과 달리 sequenceId를 completed로 마킹하지 않으므로
        /// 재진입 시 튜토리얼이 다시 재생됨.
        /// </summary>
        public void AbortTutorial()
        {
            // 진행 중 코루틴 중지
            if (sequenceCoroutine != null)
            {
                StopCoroutine(sequenceCoroutine);
                sequenceCoroutine = null;
            }

            bool wasActive = isTutorialActive;

            // 상태 플래그 초기화
            isTutorialActive = false;
            currentSequence = null;
            currentStepIndex = 0;
            waitingForTap = false;
            waitingForEvent = false;
            pendingWaitEvent = TutorialWaitEvent.None;
            isPausedForTutorial = false;
            pendingDrillCoord = null;

            // 블록 튜토리얼 비주얼 (글로우/딤) 해제
            ClearBlockHighlights();

            // 튜토리얼 UI 전체 숨김 (대화 패널/스포트라이트/말풍선/손가락/배너/스킵 등)
            if (tutorialUI != null)
                tutorialUI.HideAll();

            // 입력 제한 해제
            if (inputSystem != null)
                inputSystem.SetRestrictedMode(false, null);

            if (wasActive)
                OnTutorialEnded?.Invoke();

            Debug.Log("[TutorialManager] AbortTutorial: 튜토리얼 상태 완전 초기화 (완료 마킹 없음)");
        }

        /// <summary>
        /// 강제 복구 시 BRS pause 상태만 해제 (튜토리얼 자체는 유지)
        /// ForceRecoverFromStuck에서 호출
        /// </summary>
        public void ForceUnpause()
        {
            isPausedForTutorial = false;
            Debug.Log("[TutorialManager] ForceUnpause 호출 — isPausedForTutorial=false");
        }

        // ============================================================
        // 입력 제어
        // ============================================================

        private void LockInput()
        {
            if (inputSystem != null)
                inputSystem.SetEnabled(false);
        }

        private void UnlockInput()
        {
            if (inputSystem != null)
                inputSystem.SetEnabled(true);
        }

        // ============================================================
        // 디버그
        // ============================================================

        /// <summary>
        /// 특정 시퀀스를 강제로 재생 (디버그용)
        /// </summary>
        public void DebugPlaySequence(string sequenceId)
        {
            var seq = allSequences.FirstOrDefault(s => s.sequenceId == sequenceId);
            if (seq != null)
            {
                // 완료 상태 무시하고 재생
                if (isTutorialActive) SkipTutorial();
                StartSequence(seq);
            }
            else
            {
                Debug.LogWarning($"[TutorialManager] 시퀀스 '{sequenceId}' 를 찾을 수 없음");
            }
        }
    }
}
