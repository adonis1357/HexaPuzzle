using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using JewelsHexaPuzzle.Core;
using JewelsHexaPuzzle.Data;
using JewelsHexaPuzzle.Utils;

namespace JewelsHexaPuzzle.Managers
{
    /// <summary>
    /// 오렌지 블록 10개 제거당 스킬 1개 학습 기회 시스템 (스테이지 21+).
    ///
    /// 흐름:
    ///   1. 오렌지 블록 파괴 시 OnOrangeDestroyed() 호출 → 카운터 ++
    ///   2. 10개 도달 시 pendingUpgrades++ → 즉시 모달 UI 표시 (게임 일시정지)
    ///   3. 모달: 3개 랜덤 스킬 (카테고리당 다음 미해금 레벨)
    ///   4. 클릭 → 강제 해금 → 다음 pending 처리 → 0이 되면 게임 재개
    ///
    /// 학습된 스킬 아이콘 바: 인게임 HUD 우상단, 골드 표시 왼쪽.
    /// 아이콘 클릭 → 툴팁 표시, 아무 곳 클릭 → 툴팁 닫기.
    /// </summary>
    public class SkillUpgradeOfferSystem : MonoBehaviour
    {
        public static SkillUpgradeOfferSystem Instance { get; private set; }

        /// <summary>리워드 선택 모달이 떠 있는지 — InputSystem이 이 동안 그리드 입력을 차단한다.</summary>
        public bool IsChoiceModalOpen => choiceModal != null;
        /// <summary>리워드 설명 툴팁 열림 여부 — InputSystem이 닫는 클릭의 그리드 회전 누출을 차단하는 데 사용.</summary>
        public bool IsTooltipOpen => tooltipOverlay != null;

        private const int ORANGE_PER_UPGRADE = 10;
        private const int MIN_STAGE_FOR_REWARD = 21;
        private const int CHOICES_COUNT = 3;

        // ★ 리워드 선택(헥사퍼즐게임(5) 패키지) — 희귀도 = 한 번에 오르는 레벨 폭 + 카드 바탕색.
        //   고급(+1) 파랑 61% / 희귀(+2) 주황 30% / 전설(+3) 보라 9%.
        //   ── 9 에이전트 토론(2026-06-18) 확정: 60/30/10 → 61/30/9.
        //   전설 9% = 픽당 보라 노출 1-(1-0.09)^3 ≈ 24.9%(4픽 중 1회)를 유지하면서, 누적 풀강 속도를
        //   10% 대비 ~1.1배 늦춰 21+ 파워-난이도 균형. 단순 고정만으로는 짧은 런 전설 0회(도파민 사멸)가
        //   생기므로 전설 피티 천장(LEGENDARY_PITY_CEILING) + 리롤 하드캡(REROLL_MAX)으로 분산/악용 보완.
        private enum RewardRarity { Advanced = 0, Rare = 1, Legendary = 2 }
        private static int RarityGain(RewardRarity r) => (int)r + 1;
        private static string RarityNameKo(RewardRarity r)
            => r == RewardRarity.Legendary ? "전설" : (r == RewardRarity.Rare ? "희귀" : "고급");

        // ★ 희귀도 임계값 단일 소스 — remain==2 재정규화도 여기서 파생(이중 수정/하드코딩 제거).
        //   v < RARE_CUT → 고급(87%), RARE_CUT ≤ v < LEG_CUT → 희귀(10%), v ≥ LEG_CUT → 전설(3%).
        //   (희귀/전설을 기존 30%/9%에서 1/3로 축소 → 10%/3%, 남는 26%는 고급으로 — 사용자 요청)
        private const float RARE_CUT = 0.87f; // 고급 누적 컷 (= 고급 비율)
        private const float LEG_CUT  = 0.97f; // 희귀 누적 컷 (전설 = 1 - LEG_CUT = 3%)

        private static RewardRarity RollRarity()
        {
            float v = Random.value;
            if (v < RARE_CUT) return RewardRarity.Advanced;
            if (v < LEG_CUT)  return RewardRarity.Rare;
            return RewardRarity.Legendary;
        }
        /// <summary>
        /// ★ 제시 스킬부터 연속으로 "실제 해금 가능한" 레벨 수 (감사 H3).
        /// 체인 최대레벨뿐 아니라 각 후속 레벨의 requiredLevel 게이트(예: DrillMove2=Lv25, Move3=Lv35)도
        /// 동시 검사 — ForceUnlockSkill이 레벨 미달로 거부하면 카드에 표기된 +2/+3이 조용히 유실되기 때문.
        /// </summary>
        // ★ 체인 카테고리 베이스/위치 — 비정렬 체인(드릴쿠션 550~552)도 정확히 판정.
        //   기존 (int)st % 100 산식은 100단위 정렬을 가정 → 드릴쿠션(550)에서 chainPos=50으로 깨져
        //   카테고리가 드릴데미지(500)로 오판되던 버그. 카테고리 판정은 항상 범위 기반 GetCategoryFirstSkill 사용.
        private static int ChainBaseVal(SkillType st)
        {
            var first = SkillUnlockSchedule.GetCategoryFirstSkill(st);
            return first != SkillType.None ? (int)first : (int)st - ((int)st % 100); // 폴백(미등록)
        }
        private static int ChainPos(SkillType st) => (int)st - ChainBaseVal(st); // 0=Lv1

        private static int MaxUnlockableGain(SkillType st)
        {
            int chainPos = ChainPos(st);             // 체인 내 위치 (0=Lv1)
            int baseVal = ChainBaseVal(st);

            // 레벨 게이팅 제거(런별 로그라이크): 체인 max 클램프(최대 3)만 적용. 희귀도 확률은 RollRarityForSkill에서.
            int gain = 0;
            for (int k = 0; chainPos + k < 3; k++)
            {
                var t = (SkillType)(baseVal + chainPos + k);
                var node = SkillTreeDefinition.GetSkill(t);
                if (node == null) break;                                   // 체인 끝
                gain++;
            }
            return gain;
        }

        /// <summary>
        /// ★ 스킬별 가능한 희귀도만 추첨 (가이드 REWARD_ROLL_RULES).
        /// 적용 후 값이 능력치 최대치를 넘거나 requiredLevel 게이트에 막히는 희귀도는 등장 불가
        /// → 실제 해금 가능 상승폭(remain) 이하만 허용.
        /// remain≥3: 고급61/희귀30/전설9, remain=2: 고급/희귀(61:30 자동 재정규화), remain≤1: 고급만.
        /// </summary>
        private static RewardRarity RollRarityForSkill(SkillType st)
        {
            int remain = MaxUnlockableGain(st); // 체인 max + requiredLevel 동시 반영 (감사 H3)
            float v = Random.value;
            if (remain >= 3)
            {
                if (v < RARE_CUT) return RewardRarity.Advanced;
                if (v < LEG_CUT)  return RewardRarity.Rare;
                return RewardRarity.Legendary;
            }
            if (remain == 2)
            {
                // 전설 제외, 고급:희귀 = 61:30 자동 재정규화 → 고급 컷 = RARE_CUT/LEG_CUT (≈0.6703).
                if (v < RARE_CUT / LEG_CUT) return RewardRarity.Advanced;
                return RewardRarity.Rare;
            }
            return RewardRarity.Advanced; // remain ≤ 1
        }
        // 희귀도 톤 (fill / border / label) — SkillSelectStyle.asset 값과 동일
        private static void RarityTone(RewardRarity r, out Color fill, out Color border, out Color label)
        {
            switch (r)
            {
                case RewardRarity.Rare:      Hex("#8a4718", out fill); Hex("#e6912f", out border); Hex("#ffcf8f", out label); break;
                case RewardRarity.Legendary: Hex("#4a2878", out fill); Hex("#a062d6", out border); Hex("#d9b3ff", out label); break;
                default:                     Hex("#28518c", out fill); Hex("#5e9be6", out border); Hex("#acd2ff", out label); break;
            }
        }
        private static void Hex(string h, out Color c) { ColorUtility.TryParseHtmlString(h, out c); }
        private const float CARD_FILL_ALPHA = 1.0f;   // 카드 바탕 불투명 (가이드 REWARD_SCREEN_GUIDE: cardFillAlpha=1)
        private const int REROLL_COST_GOLD = 100;      // 다시 뽑기 기본 골드 비용 (n번째 리롤 = 100×n)

        // ★ 토론 가드(2026-06-18) — 비율 9%의 분산/악용 보완 (둘 다 같은 런 안에서만 의미).
        //   ① 리롤 하드캡: 무제한 골드 리롤로 전설을 사실상 "구매"하는 구멍 차단. 픽당 최대 REROLL_MAX회,
        //      누진 비용(100→200→소진). 리롤은 피티 카운터에 영향 없음(악용 방지).
        //   ② 전설 피티 천장: '전설 가능 카드(remain≥3)가 있었는데 전설 미등장'이 LEGENDARY_PITY_CEILING회
        //      연속되면 다음 오퍼에서 전설 가능 카드 1장을 전설로 강제. 짧은 런의 전설 0회(도파민 사멸) 제거.
        //      런(스테이지) 단위 — ResetForNewStage에서 0 초기화.
        private const int REROLL_MAX = 2;
        private const int LEGENDARY_PITY_CEILING = 5;

        // 패널 9-slice 스프라이트 캐시
        private static Sprite _panelFillSprite, _panelBorderSprite;
        private static bool _panelSpritesTried;
        private static Sprite PanelFill()  { EnsurePanelSprites(); return _panelFillSprite; }
        private static Sprite PanelBorder(){ EnsurePanelSprites(); return _panelBorderSprite; }

        // 리워드 UI 클로드 디자인 스프라이트 (패널 9-slice / 슬롯 / 토글 탭) — 1회 로드 캐시
        private static Sprite _rwPanelSpr, _rwSlotSpr, _rwTabSpr, _rwOpenTabSpr;
        private static bool _rwSpritesTried;
        private static void EnsureRewardSprites()
        {
            if (_rwSpritesTried) return;
            _rwSpritesTried = true;
            _rwPanelSpr   = Resources.Load<Sprite>("UI/reward_panel");
            _rwSlotSpr    = Resources.Load<Sprite>("UI/reward_slot");
            _rwTabSpr     = Resources.Load<Sprite>("UI/reward_tab");
            _rwOpenTabSpr = Resources.Load<Sprite>("UI/reward_open_tab"); // 측면 도킹 열기 탭(">" 꺽쇠)
        }
        private static Sprite RewardPanelSpr() { EnsureRewardSprites(); return _rwPanelSpr; }
        private static Sprite RewardSlotSpr()  { EnsureRewardSprites(); return _rwSlotSpr; }
        private static Sprite RewardTabSpr()   { EnsureRewardSprites(); return _rwTabSpr; }
        private static Sprite RewardOpenTabSpr(){ EnsureRewardSprites(); return _rwOpenTabSpr; }
        private static void EnsurePanelSprites()
        {
            if (_panelSpritesTried) return;
            _panelSpritesTried = true;
            _panelFillSprite   = Resources.Load<Sprite>("SkillIcons/UI_PanelFill");
            _panelBorderSprite = Resources.Load<Sprite>("SkillIcons/UI_PanelBorder");
        }

        private JewelsHexaPuzzle.Utils.ObscuredInt orangeCounter = 0; // ★ 보안: 메모리 치트 내성
        private JewelsHexaPuzzle.Utils.ObscuredInt overflowCounter = 0;   // 만충(10) 게이지 위에 추가 획득한 RW — 픽 후 드레인→리필에 사용
        private int pendingUpgrades = 0;
        private bool isShowingChoice = false;

        // ★ 가드 카운터 (런 단위) — legendaryDrought: 전설 가능 픽 연속 미등장 횟수, rerollCount: 현재 오퍼 리롤 횟수.
        private int legendaryDrought = 0;
        private int rerollCount = 0;

        // UI 참조
        private Canvas canvas;
        private GameObject choiceModal;
        private GameObject tooltipOverlay;
        private GameObject iconBarObj;
        private RectTransform iconBarRt;

        // ── 리워드 패널 접기/펴기 ──
        private bool rewardCollapsed = false;              // 접힘 상태 (리빌드에도 유지)
        private GameObject rewardCollapseLine;             // 접힘 시 좌하단 세로 라인 + "열기" 탭 (persistent)
        private RectTransform rewardCollapseLineRt;
        private float lastPanelH = 140f;                   // 마지막 패널 높이 (라인 높이로 사용)
        private bool rewardToggleAnimating = false;

        /// <summary>리워드 패널(또는 접힘 라인)이 화면에 보이는 중인지 (에디터 토글 버튼 배치용).</summary>
        public bool IsRewardPanelVisible => (iconBarObj != null && iconBarObj.activeSelf)
            || (rewardCollapseLine != null && rewardCollapseLine.activeSelf);
        /// <summary>리워드 패널(또는 접힘 라인) 상단 Y (좌하단 pivot) — 에디터 토글 버튼을 이 위에 둠.</summary>
        public float RewardPanelTopY
        {
            get
            {
                if (rewardCollapseLine != null && rewardCollapseLine.activeSelf && rewardCollapseLineRt != null)
                    return rewardCollapseLineRt.anchoredPosition.y + rewardCollapseLineRt.sizeDelta.y;
                return iconBarRt != null ? iconBarRt.anchoredPosition.y + iconBarRt.sizeDelta.y : 0f;
            }
        }
        private readonly List<GameObject> iconBarItems = new List<GameObject>();
        private Font font;

        // 진행 게이지 (마나 게이지 우측, 동일 사이즈)
        private GameObject progressGaugeObj;
        private Image progressFillImage;
        private JewelsHexaPuzzle.UI.LiquidGaugeSlosh progressSlosh; // RW 입체 액체 채움+출렁임
        private Text progressText;   // 중앙 값 (MP와 동일 구조)
        private Text rwTopText;      // 상단 "단계 N"
        private Text rwBottomText;   // 하단 "RW  max N"

        // 일시정지 복원용
        private float prevTimeScale = 1f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // SkillTreeManager 이벤트 구독 — 해금 시 아이콘 바 갱신
            if (SkillTreeManager.Instance != null)
            {
                SkillTreeManager.Instance.OnSkillUnlocked += OnAnySkillUnlocked;
                SkillTreeManager.Instance.OnSkillTreeReset += RebuildIconBar;
            }
        }

        private void OnDestroy()
        {
            if (SkillTreeManager.Instance != null)
            {
                SkillTreeManager.Instance.OnSkillUnlocked -= OnAnySkillUnlocked;
                SkillTreeManager.Instance.OnSkillTreeReset -= RebuildIconBar;
            }
            if (Instance == this) Instance = null;
        }

        private void OnAnySkillUnlocked(SkillType _)
        {
            RebuildIconBar();
            UpdateProgressGauge();
        }

        // ============================================================
        // 외부 진입점 — 오렌지 블록 파괴 시 호출
        // ============================================================
        public void OnOrangeDestroyed()
        {
            if (GameManager.Instance == null) return;
            if (GameManager.Instance.SelectedStage < MIN_STAGE_FOR_REWARD) return;
            if (GameManager.Instance.CurrentGameMode != GameMode.Stage) return;

            // ★ 게이지는 만충(10)에서 멈춰 홀드한다(사용자 요청). 추가 획득분은 overflowCounter에 스택해
            //   픽 후 드레인→리필에 사용. 만충에 처음 도달하면 오퍼 1개 예약(몬스터 이동 후 표시).
            if (orangeCounter < ORANGE_PER_UPGRADE)
            {
                orangeCounter++;
                if (orangeCounter >= ORANGE_PER_UPGRADE && pendingUpgrades == 0)
                    pendingUpgrades = 1;
            }
            else
            {
                overflowCounter++;
            }

            UpdateProgressGauge();

            // ★ 즉시 표시하지 않음 — 몬스터 이동이 모두 끝나고 플레이어 턴이 돌아올 때
            //   GameManager가 ShowPendingUpgradeIfAny()를 호출해 표시한다. (게이지만 갱신)
        }

        /// <summary>
        /// 플레이어 턴 복귀 시 호출 — 대기 중인 업그레이드 선택지가 있으면 그때 표시.
        /// (오렌지 달성 즉시가 아니라 몬스터 턴 종료 후 표시되도록 분리.)
        /// </summary>
        public void ShowPendingUpgradeIfAny()
        {
            if (pendingUpgrades > 0 && !isShowingChoice)
                TryShowNextChoice();
        }

        /// <summary>스테이지 시작 시 초기화 (캐스케이드 잔재 차단 + 런별 스킬 리셋).</summary>
        public void ResetForNewStage()
        {
            orangeCounter = 0;
            overflowCounter = 0;
            pendingUpgrades = 0;
            legendaryDrought = 0;   // ★ 전설 피티 천장: 런(스테이지) 단위 — 다음 런으로 새어 첫 픽 전설 폭주 방지
            rerollCount = 0;

            // ★ 런별 로그라이크 모델(#4 확정): 스킬 초기화는 '로비 이탈 시'에만(ExitToLobby/ReturnToLobby/게임오버).
            //   스테이지 시작/재시작(로비 미경유)에는 스킬을 유지하므로 여기서는 게이지/카운터만 리셋한다.
            //   (Dev_SkillReward_RunBased.md — 변경4)

            if (isShowingChoice && choiceModal != null)
            {
                Destroy(choiceModal);
                choiceModal = null;
                isShowingChoice = false;
                if (Time.timeScale == 0f) Time.timeScale = prevTimeScale;
            }
            RebuildIconBar();
            EnsureProgressGauge();
            UpdateProgressGauge(immediate: true);
        }

        // ============================================================
        // 모달 UI — 3개 선택지
        // ============================================================
        private void TryShowNextChoice()
        {
            if (pendingUpgrades <= 0) return;
            if (SkillTreeManager.Instance == null) return;

            if (!RollAndBuildModal())
            {
                // 모든 스킬 해금됐거나 후보 없음 → 남은 pending 폐기
                pendingUpgrades = 0;
                // ★ 연속 모달 체인(이전 모달이 timeScale=0 설정) 도중 후보가 소진되면
                //   여기서 복원하지 않으면 게임이 영구 정지한다 (OnChoiceClicked의 else 복원은 이미 지나침).
                if (isShowingChoice || Time.timeScale == 0f)
                {
                    isShowingChoice = false;
                    Time.timeScale = prevTimeScale > 0f ? prevTimeScale : 1f;
                }
                return;
            }
            isShowingChoice = true;
            // ★ prevTimeScale=0 재캡처 방지 — 연속 모달(2개 이상 pending)에서 첫 선택 직후
            //   timeScale이 아직 0인 상태로 재호출되면 0이 저장되어 마지막 복원 시 영구 정지된다.
            //   0보다 클 때만 캡처해 최초 진입 시점의 값을 체인 내내 유지한다.
            if (Time.timeScale > 0f) prevTimeScale = Time.timeScale;
            else if (prevTimeScale <= 0f) prevTimeScale = 1f;
            Time.timeScale = 0f;
        }

        /// <summary>
        /// 후보 추첨 + 모달 생성 (timeScale은 건드리지 않음 — 다시뽑기에서 재사용). 성공 시 true.
        /// isReroll=false(신규 오퍼)일 때만 리롤 카운터 리셋 + 전설 피티 카운터 갱신/천장 적용.
        /// isReroll=true(다시뽑기)는 피티에 영향 없음 — 골드 리롤로 전설 사재기 차단(토론 가드).
        /// </summary>
        private bool RollAndBuildModal(bool isReroll = false)
        {
            if (SkillTreeManager.Instance == null) return false;
            var candidates = SkillTreeManager.Instance.GetOrangeRewardCandidates();
            if (candidates.Count == 0) return false;

            if (!isReroll) rerollCount = 0; // 신규 오퍼 → 리롤 카운터 초기화

            // 랜덤 3개 (후보 < 3이면 있는만큼) + 카드별 희귀도 굴림(스킬별 max 제한)
            int pick = Mathf.Min(CHOICES_COUNT, candidates.Count);
            var picks = new List<SkillType>();
            var rarities = new List<RewardRarity>();
            var pool = new List<SkillType>(candidates);
            for (int i = 0; i < pick; i++)
            {
                int idx = Random.Range(0, pool.Count);
                var chosen = pool[idx];
                picks.Add(chosen);
                rarities.Add(RollRarityForSkill(chosen)); // ★ 가이드 롤 규칙: 적용 후 값이 max 초과인 희귀도 제외
                pool.RemoveAt(idx);
            }

            // ★ 전설 피티 천장 (신규 오퍼만). 리롤은 분산 흡수 장치일 뿐 피티 충전 대상이 아니다.
            if (!isReroll)
                ApplyLegendaryPity(picks, rarities);

            BuildChoiceModal(picks, rarities);
            return true;
        }

        /// <summary>
        /// 전설 피티 천장 적용. '전설 가능 카드(remain≥3)'가 한 장이라도 있는 오퍼만 카운트한다
        /// (후보가 끝단 스킬뿐이라 전설이 구조적으로 못 뜨는 픽에서 카운터가 헛돌지 않도록 — 후반 dry 방지).
        ///   · 전설 미등장 + 천장 도달 → 전설 가능 카드 1장을 전설로 강제, 카운터 리셋.
        ///   · 전설 등장 → 카운터 0.   · 전설 미등장(천장 미만) → 카운터 +1.
        /// </summary>
        private void ApplyLegendaryPity(List<SkillType> picks, List<RewardRarity> rarities)
        {
            bool anyEligible = false, anyLegendary = false;
            int firstEligibleIdx = -1;
            for (int i = 0; i < picks.Count; i++)
            {
                if (MaxUnlockableGain(picks[i]) >= 3)
                {
                    anyEligible = true;
                    if (firstEligibleIdx < 0) firstEligibleIdx = i;
                }
                if (rarities[i] == RewardRarity.Legendary) anyLegendary = true;
            }

            if (!anyEligible) return; // 전설 불가 오퍼 — 카운터 동결(헛돌기 방지)

            if (!anyLegendary && legendaryDrought >= LEGENDARY_PITY_CEILING && firstEligibleIdx >= 0)
            {
                rarities[firstEligibleIdx] = RewardRarity.Legendary; // 천장 — 강제 전설
                anyLegendary = true;
                Debug.Log($"[SkillUpgradeOfferSystem] 전설 피티 천장 발동(drought={legendaryDrought}) → {picks[firstEligibleIdx]} 전설 강제");
            }

            legendaryDrought = anyLegendary ? 0 : legendaryDrought + 1;
        }

        /// <summary>★ 다시 뽑기 — 골드 차감 후 같은 pending에 대해 후보 재추첨 (timeScale 유지).
        /// 하드캡 REROLL_MAX회 + 누진 비용(100→200→소진). 무제한 골드 리롤로 전설 사재기 차단(토론 가드).</summary>
        private void RerollCurrentChoice()
        {
            if (GameManager.Instance == null) return;
            if (rerollCount >= REROLL_MAX)
            {
                Debug.Log($"[SkillUpgradeOfferSystem] 다시뽑기 소진 (최대 {REROLL_MAX}회)");
                return;
            }
            int cost = REROLL_COST_GOLD * (rerollCount + 1); // 누진: 1회 100, 2회 200
            if (!GameManager.Instance.SpendGold(cost))
            {
                Debug.Log($"[SkillUpgradeOfferSystem] 다시뽑기 골드 부족 (필요 {cost}G)");
                return;
            }
            rerollCount++;
            if (choiceModal != null) { Destroy(choiceModal); choiceModal = null; }
            if (!RollAndBuildModal(true))
            {
                // 재추첨 실패(후보 소진) → 모달 닫고 게임 재개 (0 복원 방어 포함)
                isShowingChoice = false;
                Time.timeScale = prevTimeScale > 0f ? prevTimeScale : 1f;
                pendingUpgrades = 0;
            }
        }

        private Canvas FindCanvas()
        {
            if (canvas != null) return canvas;
            canvas = Object.FindObjectOfType<Canvas>();
            return canvas;
        }

        private void BuildChoiceModal(List<SkillType> picks, List<RewardRarity> rarities)
        {
            var cv = FindCanvas();
            if (cv == null) return;

            choiceModal = new GameObject("SkillUpgradeChoiceModal");
            choiceModal.transform.SetParent(cv.transform, false);
            var rt = choiceModal.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            // 반투명 배경 (통일 다크글래스 딤)
            var bg = choiceModal.AddComponent<Image>();
            bg.color = JewelsHexaPuzzle.Utils.ClaudeTheme.PopupOverlay;
            bg.raycastTarget = true; // 배경 클릭 차단

            // 타이틀
            var titleObj = new GameObject("Title");
            titleObj.transform.SetParent(choiceModal.transform, false);
            var titleRt = titleObj.AddComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0.5f, 0.85f);
            titleRt.anchorMax = new Vector2(0.5f, 0.85f);
            titleRt.pivot = new Vector2(0.5f, 0.5f);
            titleRt.sizeDelta = new Vector2(700f, 80f);
            var titleText = titleObj.AddComponent<Text>();
            titleText.font = font;
            titleText.fontSize = 42;
            titleText.fontStyle = FontStyle.Bold;
            titleText.alignment = TextAnchor.MiddleCenter;
            Hex("#e9c84b", out var titleGold);
            titleText.color = titleGold;
            titleText.text = pendingUpgrades > 1 ? $"리워드 선택 ({pendingUpgrades}개 남음)" : "리워드 선택";
            var titleOutline = titleObj.AddComponent<Outline>();
            titleOutline.effectColor = new Color(0.2f, 0.1f, 0f, 0.95f);
            titleOutline.effectDistance = new Vector2(2f, -2f);

            // 부제목
            var subObj = new GameObject("Subtitle");
            subObj.transform.SetParent(choiceModal.transform, false);
            var subRt = subObj.AddComponent<RectTransform>();
            subRt.anchorMin = new Vector2(0.5f, 0.78f);
            subRt.anchorMax = new Vector2(0.5f, 0.78f);
            subRt.pivot = new Vector2(0.5f, 0.5f);
            subRt.sizeDelta = new Vector2(700f, 40f);
            var subText = subObj.AddComponent<Text>();
            subText.font = font;
            subText.fontSize = 22;
            subText.alignment = TextAnchor.MiddleCenter;
            Hex("#c7cdd6", out var subCol);
            subText.color = subCol;
            subText.text = "주황 블록 10개 제거 보상 — 하나를 선택하세요";

            // 3개 버튼 가로 배치 (카드별 희귀도 적용)
            float btnW = 280f, btnH = 380f, spacing = 40f;
            float totalW = picks.Count * btnW + (picks.Count - 1) * spacing;
            float startX = -totalW * 0.5f + btnW * 0.5f;
            for (int i = 0; i < picks.Count; i++)
            {
                SkillType st = picks[i];
                RewardRarity rar = (rarities != null && i < rarities.Count) ? rarities[i] : RewardRarity.Advanced;
                float x = startX + i * (btnW + spacing);
                BuildChoiceButton(choiceModal.transform, st, rar, x, btnW, btnH);
            }

            // ★ 다시 뽑기 버튼 (골드 소모) — 카드 아래 중앙
            BuildRerollButton(choiceModal.transform, -30f - btnH * 0.5f - 72f);

            // ★ 헤더("리워드 선택")와 부제("주황 블록...")를 카드보다 위(앞)에 렌더 —
            //   버튼 생성 후 최상위 sibling으로 올려 카드에 가려지지 않게 한다.
            titleObj.transform.SetAsLastSibling();
            subObj.transform.SetAsLastSibling();
        }

        private void BuildChoiceButton(Transform parent, SkillType skillType, RewardRarity rarity, float xOffset, float w, float h)
        {
            var node = SkillTreeDefinition.GetSkill(skillType);
            if (node == null) return;

            // ★ 희귀도 톤 (고급 파랑 / 희귀 주황 / 전설 보라)
            RarityTone(rarity, out var rFill, out var rBorder, out var rLabel);

            var btnObj = new GameObject($"ChoiceBtn_{skillType}");
            btnObj.transform.SetParent(parent, false);
            var rt = btnObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(xOffset, -30f);
            rt.sizeDelta = new Vector2(w, h);

            // 카드 배경 — 희귀도 바탕색 (알파 0.5, 보드가 비쳐 보임). 9-slice 패널 스프라이트 사용.
            var bg = btnObj.AddComponent<Image>();
            var fillSpr = PanelFill();
            if (fillSpr != null) { bg.sprite = fillSpr; bg.type = Image.Type.Sliced; }
            bg.color = new Color(rFill.r, rFill.g, rFill.b, CARD_FILL_ALPHA);

            // 테두리 — 희귀도 보더 색 (9-slice border 스프라이트가 있으면 별도 레이어로)
            var borderSpr = PanelBorder();
            if (borderSpr != null)
            {
                var borderObj = new GameObject("Border");
                borderObj.transform.SetParent(btnObj.transform, false);
                var brt = borderObj.AddComponent<RectTransform>();
                brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one;
                brt.offsetMin = Vector2.zero; brt.offsetMax = Vector2.zero;
                var bimg = borderObj.AddComponent<Image>();
                bimg.sprite = borderSpr; bimg.type = Image.Type.Sliced;
                bimg.color = rBorder; bimg.raycastTarget = false;
            }
            else
            {
                var outline = btnObj.AddComponent<Outline>();
                outline.effectColor = rBorder;
                outline.effectDistance = new Vector2(3f, -3f);
            }

            var btn = btnObj.AddComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = new Color(1f, 1f, 1f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            btn.colors = colors;
            SkillType captured = skillType; RewardRarity capturedRar = rarity;
            btn.onClick.AddListener(() => OnChoiceClicked(captured, capturedRar));

            // ★ 상단 희귀도 라벨 (고급/희귀/전설)
            var rarObj = new GameObject("RarityLabel");
            rarObj.transform.SetParent(btnObj.transform, false);
            var rarRt = rarObj.AddComponent<RectTransform>();
            // ★ 카드 상단 기준 순차 배치 (참조 이미지: 라벨→아이콘→이름→레벨→설명)
            rarRt.anchorMin = new Vector2(0f, 1f); rarRt.anchorMax = new Vector2(1f, 1f);
            rarRt.pivot = new Vector2(0.5f, 1f);
            rarRt.anchoredPosition = new Vector2(0f, -18f);
            rarRt.sizeDelta = new Vector2(-24f, 30f);
            var rarTxt = rarObj.AddComponent<Text>();
            rarTxt.font = font; rarTxt.fontSize = 22; rarTxt.fontStyle = FontStyle.Bold;
            rarTxt.alignment = TextAnchor.MiddleCenter;
            rarTxt.color = rLabel; rarTxt.raycastTarget = false;
            rarTxt.text = RarityNameKo(rarity);
            var rarOl = rarObj.AddComponent<Outline>();
            rarOl.effectColor = new Color(0f, 0f, 0f, 0.8f); rarOl.effectDistance = new Vector2(1f, -1f);

            // 아이콘 (큰 유니코드 심볼 + 컬러 원형 배경)
            var iconBgObj = new GameObject("IconBg");
            iconBgObj.transform.SetParent(btnObj.transform, false);
            var iconBgRt = iconBgObj.AddComponent<RectTransform>();
            iconBgRt.anchorMin = new Vector2(0.5f, 1f);
            iconBgRt.anchorMax = new Vector2(0.5f, 1f);
            iconBgRt.pivot = new Vector2(0.5f, 1f);
            iconBgRt.anchoredPosition = new Vector2(0f, -56f);   // 희귀도 라벨 아래
            iconBgRt.sizeDelta = new Vector2(135f, 135f);        // 육각형 아이콘 +30% (104→135)
            var iconBg = iconBgObj.AddComponent<Image>();
            iconBg.color = node.nodeColor;
            iconBg.raycastTarget = false;

            // ★ 패키지 스킬 아이콘(Resources/SkillIcons/*.png) 우선 표시.
            //   매핑되는 스프라이트가 있으면 Image로, 없으면(데이터 없는 경우) 기존 유니코드 심볼로 폴백.
            Sprite skillSprite = GetSkillSprite(skillType);
            if (skillSprite != null)
            {
                var iconImgObj = new GameObject("IconSprite");
                iconImgObj.transform.SetParent(iconBgObj.transform, false);
                var iconImgRt = iconImgObj.AddComponent<RectTransform>();
                iconImgRt.anchorMin = Vector2.zero;
                iconImgRt.anchorMax = Vector2.one;
                iconImgRt.offsetMin = Vector2.zero;   // 뒤 다크 그레이 헥사와 동일 크기로 정확히 겹침
                iconImgRt.offsetMax = Vector2.zero;
                var iconImg = iconImgObj.AddComponent<Image>();
                iconImg.sprite = skillSprite;
                iconImg.preserveAspect = true;
                iconImg.raycastTarget = false;
                iconImg.transform.SetAsLastSibling(); // 다크 그레이 헥사 배경 위에 원본 아이콘
                // ★ 새 아이콘 PNG는 헥사 프레임 안쪽 웰이 "투명"이라 그대로 두면 바탕색이 비어 보인다.
                //   → UI_HexWell(흰 헥사 실루엣)을 아이콘과 동일 크기로 뒤에 깔고 다크 그레이(#313539)로 틴트해
                //     프레임 안쪽을 다크 그레이 웰로 채운다(앞 아이콘의 골드 프레임이 가장자리를 덮어 모서리 노출 없음).
                Sprite wellSprite = GetWellSprite();
                iconBg.sprite = wellSprite != null ? wellSprite : skillSprite;
                iconBg.preserveAspect = true;
                iconBg.color = new Color(0.192f, 0.208f, 0.224f, 1f); // #313539 다크 그레이 웰
            }
            else
            {
                // 폴백: 데이터(스프라이트 매핑) 없는 스킬 → 기존 유니코드 심볼 + 컬러 원형 배경
                var iconTxtObj = new GameObject("IconText");
                iconTxtObj.transform.SetParent(iconBgObj.transform, false);
                var iconTxtRt = iconTxtObj.AddComponent<RectTransform>();
                iconTxtRt.anchorMin = Vector2.zero;
                iconTxtRt.anchorMax = Vector2.one;
                iconTxtRt.offsetMin = Vector2.zero;
                iconTxtRt.offsetMax = Vector2.zero;
                var iconTxt = iconTxtObj.AddComponent<Text>();
                iconTxt.font = font;
                iconTxt.fontSize = 64;
                iconTxt.fontStyle = FontStyle.Bold;
                iconTxt.alignment = TextAnchor.MiddleCenter;
                iconTxt.color = Color.white;
                iconTxt.text = string.IsNullOrEmpty(node.iconSymbol) ? "★" : node.iconSymbol;
                iconTxt.raycastTarget = false;
                var iconOutline = iconTxtObj.AddComponent<Outline>();
                iconOutline.effectColor = new Color(0f, 0f, 0f, 0.85f);
                iconOutline.effectDistance = new Vector2(1.5f, -1.5f);
            }

            // ★ 능력치 전이값 계산 (배지·레벨전이 공용). 현재 보유 레벨 = 후보 스킬 체인레벨 - 1
            //   (후보는 "다음에 해금할" 스킬). 선택 후 레벨 = 현재 + 희귀도 상승폭(체인 max 클램프).
            //   능력치 기본값(base): 직접타격 2 / 데미지 계열 1~2 / 이동·디딤 등 기능 계열 0 (GetStatBase).
            //   toLv = 적용 후 실제 능력치 절대값. 예) 직접타격 2→4(고급)/6(희귀)/8(전설), 치명타확률 30/50/70%.
            int curLevel = SkillChainLevel(skillType) - 1;
            int chainMax = SkillChainMaxLevel(skillType);
            int nextLevel = Mathf.Min(curLevel + RarityGain(rarity), chainMax);
            int statBase = GetStatBase(skillType);
            int perLevel = GetStatPerLevel(skillType);
            int fromLv = statBase + curLevel * perLevel;   // 기존 능력치값
            int toLv = statBase + nextLevel * perLevel;    // 업그레이드 후 능력치값

            // ★ 우하단 원형 배지 — 해당 리워드의 "실제 능력치"를 표시한다 (사용자 요청).
            //   · 데미지 증분 계열(드릴·폭탄·드론·타겟·망치 데미지): 희귀도 상승폭 "+N"
            //   · 그 외 절대값 계열(직접타격·치명타·이동·디딤 등): 적용 후 능력치 절대값 toLv(+ 단위 %)
            //   아이콘 PNG에 구워진 기본 "+1" 배지 위에 정확히 정렬·덮어 숫자가 두 개로 보이지 않게 한다.
            //   베이크드 배지 위치 = icon-UV(0.777,0.691 top-down) = (0.777,0.309).
            {
                string badgeText = IsIncrementRewardType(skillType)
                    ? $"+{RarityGain(rarity)}"
                    : $"{toLv}{GetStatUnit(skillType)}";
                var badgeObj = new GameObject("IncrementBadge");
                badgeObj.transform.SetParent(iconBgObj.transform, false);
                var badgeRt = badgeObj.AddComponent<RectTransform>();
                badgeRt.anchorMin = badgeRt.anchorMax = new Vector2(0.758f, 0.276f); // ★ 측정된 베이크드 "+1" 중심(pngjs)에 정확 정렬 — 2개 원형 겹침 방지
                badgeRt.pivot = new Vector2(0.5f, 0.5f);
                badgeRt.sizeDelta = new Vector2(60f, 60f);                            // 베이크드 "+1"(외곽반경 0.204) 완전히 덮어 1개 원형 — 큰 아이콘(135) 대비 비율 0.44
                badgeRt.anchoredPosition = Vector2.zero;
                var badgeImg = badgeObj.AddComponent<Image>();
                badgeImg.sprite = GetBadgeSprite();
                badgeImg.raycastTarget = false;
                badgeObj.transform.SetAsLastSibling();

                var badgeTxtObj = new GameObject("BadgeText");
                badgeTxtObj.transform.SetParent(badgeObj.transform, false);
                var badgeTxtRt = badgeTxtObj.AddComponent<RectTransform>();
                badgeTxtRt.anchorMin = Vector2.zero; badgeTxtRt.anchorMax = Vector2.one;
                badgeTxtRt.offsetMin = Vector2.zero; badgeTxtRt.offsetMax = Vector2.zero;
                var badgeTxt = badgeTxtObj.AddComponent<Text>();
                badgeTxt.font = font;
                badgeTxt.fontSize = 26;
                badgeTxt.fontStyle = FontStyle.Bold;
                badgeTxt.alignment = TextAnchor.MiddleCenter;
                badgeTxt.resizeTextForBestFit = true;   // "30%"·"70%" 등 긴 문자열 자동 축소(원형 이탈 방지)
                badgeTxt.resizeTextMinSize = 13;
                badgeTxt.resizeTextMaxSize = GetStatUnit(skillType) == "%" ? 24 : 26; // 낙하 치명타률 "30%/50%/70%"는 숫자 폰트 2pt 작게
                Hex("#ffe9a8", out var badgeGold);
                badgeTxt.color = badgeGold;
                badgeTxt.text = badgeText;
                badgeTxt.raycastTarget = false;
            }

            // 스킬 이름
            var nameObj = new GameObject("SkillName");
            nameObj.transform.SetParent(btnObj.transform, false);
            var nameRt = nameObj.AddComponent<RectTransform>();
            // 아이콘(top -56, 높이 104 → 하단 -160) 아래에 순차 배치
            nameRt.anchorMin = new Vector2(0f, 1f);
            nameRt.anchorMax = new Vector2(1f, 1f);
            nameRt.pivot = new Vector2(0.5f, 1f);
            nameRt.anchoredPosition = new Vector2(0f, -205f);  // 아이콘 +30%(135) 반영해 아래로
            nameRt.sizeDelta = new Vector2(-20f, 36f);
            var nameTxt = nameObj.AddComponent<Text>();
            nameTxt.font = font;
            nameTxt.fontSize = 24;
            nameTxt.fontStyle = FontStyle.Bold;
            nameTxt.alignment = TextAnchor.MiddleCenter;
            nameTxt.color = Color.white;
            nameTxt.text = GetDisplayName(skillType, node.skillName);
            nameTxt.raycastTarget = false;
            var nameOutline = nameObj.AddComponent<Outline>();
            nameOutline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            nameOutline.effectDistance = new Vector2(1f, -1f);

            // ★ 능력치 전이 "기존값 → 업그레이드값" — 전이값(curLevel/nextLevel/fromLv/toLv)은
            //   위 배지 계산부에서 이미 산출됨. 여기서는 그 값으로 "fromLv → toLv" 텍스트만 렌더.
            var lvObj = new GameObject("LevelTransition");
            lvObj.transform.SetParent(btnObj.transform, false);
            var lvRt = lvObj.AddComponent<RectTransform>();
            lvRt.anchorMin = new Vector2(0f, 1f); lvRt.anchorMax = new Vector2(1f, 1f);
            lvRt.pivot = new Vector2(0.5f, 1f);
            lvRt.anchoredPosition = new Vector2(0f, -245f);  // 스킬명 아래
            lvRt.sizeDelta = new Vector2(-20f, 32f);
            var lvTxt = lvObj.AddComponent<Text>();
            lvTxt.font = font; lvTxt.fontSize = 22; lvTxt.fontStyle = FontStyle.Bold;
            lvTxt.alignment = TextAnchor.MiddleCenter;
            lvTxt.supportRichText = true;
            lvTxt.raycastTarget = false;
            // from(회색) → arrow(연금색) → to(골드 강조)
            lvTxt.text = $"<color=#cdd6df>{fromLv}</color> <color=#ffe08a>→</color> <color=#ffe9a8>{toLv}</color>";
            var lvOl = lvObj.AddComponent<Outline>();
            lvOl.effectColor = new Color(0f, 0f, 0f, 0.85f); lvOl.effectDistance = new Vector2(1f, -1f);

            // 설명
            var descObj = new GameObject("Description");
            descObj.transform.SetParent(btnObj.transform, false);
            var descRt = descObj.AddComponent<RectTransform>();
            // 레벨전이 아래(top -252)부터 카드 하단(-18)까지 채움
            descRt.anchorMin = new Vector2(0f, 1f);
            descRt.anchorMax = new Vector2(1f, 1f);
            descRt.pivot = new Vector2(0.5f, 1f);
            descRt.anchoredPosition = new Vector2(0f, -283f);
            descRt.sizeDelta = new Vector2(-28f, h - 283f - 18f);  // 하단 18px 패딩
            var descTxt = descObj.AddComponent<Text>();
            descTxt.font = font;
            descTxt.fontSize = 18;
            descTxt.alignment = TextAnchor.UpperCenter;
            descTxt.horizontalOverflow = HorizontalWrapMode.Wrap;
            descTxt.verticalOverflow = VerticalWrapMode.Truncate;
            descTxt.color = new Color(0.95f, 0.95f, 0.95f);
            descTxt.text = GetDisplayDescription(skillType, rarity, toLv, node.description);
            descTxt.raycastTarget = false;
        }

        // ★ SkillType → 패키지 스프라이트 이름(Resources/SkillIcons/*.png) 매핑.
        //   레벨(1/2/3)은 같은 스프라이트 공유. 매핑 없으면 null → 유니코드 심볼 폴백.
        private static readonly Dictionary<SkillType, string> _skillSpriteMap = new Dictionary<SkillType, string>
        {
            { SkillType.DrillMove1, "Drill_move" }, { SkillType.DrillMove2, "Drill_move" }, { SkillType.DrillMove3, "Drill_move" },
            { SkillType.BombMove1, "Bomb_move" }, { SkillType.BombMove2, "Bomb_move" }, { SkillType.BombMove3, "Bomb_move" },
            { SkillType.BombKnockback1, "Bomb_knockback" }, { SkillType.BombKnockback2, "Bomb_knockback" }, { SkillType.BombKnockback3, "Bomb_knockback" },
            { SkillType.BombDamage1, "Bomb_damage" }, { SkillType.BombDamage2, "Bomb_damage" }, { SkillType.BombDamage3, "Bomb_damage" },
            { SkillType.DrillDamage1, "Drill_damage" }, { SkillType.DrillDamage2, "Drill_damage" }, { SkillType.DrillDamage3, "Drill_damage" },
            { SkillType.DrillCushion1, "Drill_cushion" }, { SkillType.DrillCushion2, "Drill_cushion" }, { SkillType.DrillCushion3, "Drill_cushion" },
            { SkillType.DroneTargetDamage1, "Drone_damage" }, { SkillType.DroneTargetDamage2, "Drone_damage" }, { SkillType.DroneTargetDamage3, "Drone_damage" },
            { SkillType.HammerLevel1, "Hammer_damage" }, { SkillType.HammerLevel2, "Hammer_damage" }, { SkillType.HammerLevel3, "Hammer_damage" },
            { SkillType.SwapLevel1, "Swap_step" }, { SkillType.SwapLevel2, "Swap_step" }, { SkillType.SwapLevel3, "Swap_step" },
            { SkillType.LineLevel1, "LineBtn_damage" }, { SkillType.LineLevel2, "LineBtn_damage" }, { SkillType.LineLevel3, "LineBtn_damage" },
            { SkillType.ChainBomb1, "Bomb_chain" }, { SkillType.ChainBomb2, "Bomb_chain" }, { SkillType.ChainBomb3, "Bomb_chain" },
            { SkillType.TargetDamage1, "Cannon_damage" }, { SkillType.TargetDamage2, "Cannon_damage" }, { SkillType.TargetDamage3, "Cannon_damage" },
            { SkillType.DrillPenetrate1, "Drill_pierce" }, { SkillType.DrillPenetrate2, "Drill_pierce" }, { SkillType.DrillPenetrate3, "Drill_pierce" },
            { SkillType.DroneClone1, "Drone_clone" }, { SkillType.DroneClone2, "Drone_clone" }, { SkillType.DroneClone3, "Drone_clone" },
            { SkillType.DirectHit1, "Skill_DirectHit" }, { SkillType.DirectHit2, "Skill_DirectHit" }, { SkillType.DirectHit3, "Skill_DirectHit" },
            { SkillType.AdjacentHit1, "Skill_AdjacentHit" }, { SkillType.AdjacentHit2, "Skill_AdjacentHit" }, { SkillType.AdjacentHit3, "Skill_AdjacentHit" },
            { SkillType.FallCritChance1, "Skill_CritChance" }, { SkillType.FallCritChance2, "Skill_CritChance" }, { SkillType.FallCritChance3, "Skill_CritChance" },
            { SkillType.FallCritDamage1, "Skill_CritDamage" }, { SkillType.FallCritDamage2, "Skill_CritDamage" }, { SkillType.FallCritDamage3, "Skill_CritDamage" },
        };
        private static readonly Dictionary<string, Sprite> _spriteCache = new Dictionary<string, Sprite>();

        /// <summary>SkillType에 대응하는 패키지 스프라이트 로드 (캐시). 매핑 없으면 null.</summary>
        private Sprite GetSkillSprite(SkillType skillType)
        {
            if (!_skillSpriteMap.TryGetValue(skillType, out var name) || string.IsNullOrEmpty(name))
                return null;
            if (_spriteCache.TryGetValue(name, out var cached)) return cached;
            var spr = Resources.Load<Sprite>("SkillIcons/" + name);
            _spriteCache[name] = spr; // null이어도 캐시(반복 로드 방지)
            return spr;
        }

        private static Sprite _badgeSprite;
        /// <summary>증가량 배지 원형 스프라이트 (네이비 채움 + 골드 링). 프로시저럴 1회 생성.</summary>
        private static Sprite GetBadgeSprite()
        {
            if (_badgeSprite != null && _badgeSprite.texture != null) return _badgeSprite; // ★ 텍스처 유효성까지 — 도메인 리로드 비활성 stale 캐시 방지
            const int SZ = 64;
            var tex = new Texture2D(SZ, SZ, TextureFormat.RGBA32, false);
            var px = new Color[SZ * SZ];
            float c = SZ * 0.5f - 0.5f;
            float rOuter = SZ * 0.47f, rInner = SZ * 0.42f; // ★ 네이비 중심 확대(0.34→0.42) — 아이콘 베이크드 "+1"을 불투명 네이비로 확실히 덮어 숫자 겹침 제거
            Color navy = new Color(0.10f, 0.13f, 0.22f, 1f);
            Color gold = new Color(0.86f, 0.72f, 0.40f, 1f);
            for (int y = 0; y < SZ; y++)
                for (int x = 0; x < SZ; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    // ★ 버그수정: Mathf.SmoothStep(from,to,t)는 t를 0~1 보간계수로 클램프 → d(거리)를 넣으면 전 픽셀 알파=0(투명).
                    //   가장자리 rOuter 기준 1.5px 소프트 페이드로 직접 계산(안쪽 불투명).
                    float a = 1f - Mathf.Clamp01((d - (rOuter - 1.5f)) / 1.5f);
                    Color col = d > rInner ? gold : navy;
                    px[y * SZ + x] = new Color(col.r, col.g, col.b, col.a * a);
                }
            tex.SetPixels(px);
            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            _badgeSprite = Sprite.Create(tex, new Rect(0, 0, SZ, SZ), new Vector2(0.5f, 0.5f), 100f);
            return _badgeSprite;
        }

        /// <summary>★ 다시 뽑기 버튼 생성 (골드 소모). 골드 부족 시 비활성.</summary>
        private void BuildRerollButton(Transform parent, float y)
        {
            bool soldOut = rerollCount >= REROLL_MAX;
            int nextCost = REROLL_COST_GOLD * (rerollCount + 1);
            bool canAfford = !soldOut && GameManager.Instance != null && GameManager.Instance.CurrentGold >= nextCost;

            var obj = new GameObject("RerollButton");
            obj.transform.SetParent(parent, false);
            var rt = obj.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, y);
            rt.sizeDelta = new Vector2(320f, 66f);

            var bg = obj.AddComponent<Image>();
            var fill = PanelFill();
            if (fill != null) { bg.sprite = fill; bg.type = Image.Type.Sliced; }
            bg.color = canAfford ? new Color(0.24f, 0.19f, 0.10f, 0.97f) : new Color(0.16f, 0.16f, 0.16f, 0.9f);

            var border = PanelBorder();
            if (border != null)
            {
                var bo = new GameObject("Border");
                bo.transform.SetParent(obj.transform, false);
                var brt = bo.AddComponent<RectTransform>();
                brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one;
                brt.offsetMin = Vector2.zero; brt.offsetMax = Vector2.zero;
                var bi = bo.AddComponent<Image>();
                bi.sprite = border; bi.type = Image.Type.Sliced;
                bi.color = canAfford ? new Color(0.86f, 0.72f, 0.40f, 1f) : new Color(0.4f, 0.4f, 0.4f, 1f);
                bi.raycastTarget = false;
            }

            var btn = obj.AddComponent<Button>();
            btn.interactable = canAfford;
            btn.onClick.AddListener(() => RerollCurrentChoice());

            var txtObj = new GameObject("Label");
            txtObj.transform.SetParent(obj.transform, false);
            var trt = txtObj.AddComponent<RectTransform>();
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
            var txt = txtObj.AddComponent<Text>();
            txt.font = font;
            txt.fontSize = 26;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            Hex(canAfford ? "#ffe9a8" : "#888888", out var tc);
            txt.color = tc;
            txt.text = soldOut ? "다시 뽑기 소진" : $"다시 뽑기   {nextCost} G";
            txt.raycastTarget = false;
            var ol = txtObj.AddComponent<Outline>();
            ol.effectColor = new Color(0f, 0f, 0f, 0.8f);
            ol.effectDistance = new Vector2(1.5f, -1.5f);
        }

        /// <summary>아이콘 바탕용 헥사 웰 스프라이트(UI_HexWell) 로드 (캐시). 흰 헥사 → 다크 그레이 틴트로 사용.</summary>
        private Sprite GetWellSprite()
        {
            const string key = "UI_HexWell";
            if (_spriteCache.TryGetValue(key, out var cached)) return cached;
            var spr = Resources.Load<Sprite>("SkillIcons/" + key);
            _spriteCache[key] = spr; // null이어도 캐시
            return spr;
        }

        /// <summary>
        /// 능력치 기본값(base) — 카드에 "기존값 → 업그레이드값" 표시용.
        /// 공격력 계열(드릴/폭탄/드론/타겟 데미지)은 기본 데미지 1이 있어 base=1,
        /// 그 외 기능 계열(이동/넉백/쿠션/관통/연쇄/디딤/망치/라인)은 미해금 시 0이므로 base=0.
        /// </summary>
        private static int GetStatBase(SkillType st)
        {
            SkillType cat = (SkillType)ChainBaseVal(st); // 체인 시작값으로 카테고리 판정(비정렬 체인 안전)
            switch (cat)
            {
                // 직접 타격 기본 데미지 2 — 드릴·드론 베이스를 폭탄 중심(2)과 통일.
                case SkillType.DrillDamage1:
                case SkillType.DroneTargetDamage1:
                    return 2;
                // 폭탄 데미지(ring 기준)·전역 타겟강화: 베이스 1
                case SkillType.BombDamage1:
                case SkillType.TargetDamage1:
                    return 1;
                // 스왑: 미해금이어도 기본 교환 거리 1칸 (SwapItem 거리 = 1 + level)
                case SkillType.SwapLevel1:
                    return 1;
                // 드론 분신: 기본 드론 1기 → 총 드론 수 표기
                case SkillType.DroneClone1:
                    return 1;
                // 직접 타격: 기본 데미지 2 + 희귀도(+2/+4/+6) → 카드에 4/6/8 표시 (최대 8)
                case SkillType.DirectHit1:
                    return 2;
                // 인접 타격: 기본 범위 데미지 1
                case SkillType.AdjacentHit1:
                    return 1;
                // 낙하 치명타 확률: 기본 10%
                case SkillType.FallCritChance1:
                    return 10;
                // 낙하 치명타 데미지: 기본 +1
                case SkillType.FallCritDamage1:
                    return 1;
                default:
                    return 0;
            }
        }

        /// <summary>
        /// 능력치 레벨당 증가폭 — 카드 "기존값 → 업그레이드값" 표시용.
        /// 대부분 1/레벨이나, 직접 타격(+2/레벨)·낙하 치명타 확률(+20%/레벨)은 비선형이므로 별도 지정.
        /// </summary>
        private static int GetStatPerLevel(SkillType st)
        {
            SkillType cat = (SkillType)ChainBaseVal(st);
            switch (cat)
            {
                case SkillType.DirectHit1:      return 2;   // +2/+4/+6
                case SkillType.FallCritChance1: return 20;  // +20/+40/+60 %
                case SkillType.SwapLevel1:      return 2;   // 기본1 + 레벨당 +2칸 → 3/5/7
                default:                        return 1;
            }
        }

        /// <summary>
        /// 우하단 원형 배지를 "희귀도 상승폭(+N)"으로 표시할 증분형 리워드인지 판정.
        /// 데미지 증분 계열(드릴·폭탄·드론·타겟·망치 데미지)은 +N, 그 외(직접타격·치명타·기능 계열)는 절대값(toLv).
        /// (GetDisplayDescription에서 "+{gain}"을 쓰는 카테고리와 동일.)
        /// </summary>
        private static bool IsIncrementRewardType(SkillType st)
        {
            SkillType cat = (SkillType)ChainBaseVal(st);
            switch (cat)
            {
                case SkillType.DrillDamage1:
                case SkillType.BombDamage1:
                case SkillType.DroneTargetDamage1:
                case SkillType.TargetDamage1:
                case SkillType.HammerLevel1:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>능력치 절대값 표시 단위 — 낙하 치명타 확률은 "%", 그 외는 단위 없음.</summary>
        private static string GetStatUnit(SkillType st)
        {
            SkillType cat = (SkillType)ChainBaseVal(st);
            return cat == SkillType.FallCritChance1 ? "%" : "";
        }

        /// <summary>
        /// 카드 표시용 스킬명 — 데미지 증가 계열(드릴/폭탄/드론/타겟/망치)은 표현을 "○○ 데미지"로 통일.
        /// (기존 명칭이 "드릴 강화/폭탄 강화/드론 강화/타겟 강화/망치 파워"로 제각각이라 데미지로 일원화)
        /// 그 외 스킬은 기존 skillName 유지.
        /// </summary>
        private static string GetDisplayName(SkillType st, string fallback)
        {
            SkillType cat = (SkillType)ChainBaseVal(st);
            switch (cat)
            {
                // 데미지 증가 계열 — 명칭 "○○ 데미지"로 통일
                case SkillType.DrillDamage1:       return "드릴 데미지";
                case SkillType.BombDamage1:        return "폭탄 데미지";
                case SkillType.DroneTargetDamage1: return "드론 데미지";
                case SkillType.TargetDamage1:      return "타겟 데미지";
                case SkillType.HammerLevel1:       return "망치 데미지";
                // 기능 계열 — 레벨 접미사(I/1/+1) 제거한 깔끔한 명칭
                case SkillType.DrillMove1:         return "드릴 이동";
                case SkillType.BombMove1:          return "폭탄 이동";
                case SkillType.BombKnockback1:     return "폭탄 넉백";
                case SkillType.DrillCushion1:      return "드릴 쿠션";
                case SkillType.SwapLevel1:         return "스왑 디딤";
                case SkillType.LineLevel1:         return "라인 연결";
                case SkillType.ChainBomb1:         return "폭탄 연쇄";
                case SkillType.DrillPenetrate1:    return "드릴 관통";
                case SkillType.DroneClone1:        return "드론 분신";
                case SkillType.DirectHit1:         return "직접 타격";
                case SkillType.AdjacentHit1:       return "인접 타격";
                case SkillType.FallCritChance1:    return "치명타 확률";
                case SkillType.FallCritDamage1:    return "치명타 데미지";
                default:                           return fallback;
            }
        }

        /// <summary>
        /// 카드 표시용 설명 — 데미지 증가 계열은 희귀도 상승폭(고급+1/희귀+2/전설+3)을 반영해
        /// "○○의 블록과 몬스터에게 데미지 +N" 형식으로 동적 생성. 그 외는 기존 설명 유지.
        /// </summary>
        private static string GetDisplayDescription(SkillType st, RewardRarity rarity, int toValue, string fallback)
        {
            int gain = RarityGain(rarity);
            SkillType cat = (SkillType)ChainBaseVal(st);
            switch (cat)
            {
                // 데미지 증가 계열 — 희귀도 증가량(+N)
                case SkillType.DrillDamage1:       return $"드릴이 지나간 블록과 몬스터에게 데미지 +{gain}";
                case SkillType.BombDamage1:        return $"폭발 범위의 블록과 몬스터에게 데미지 +{gain}";
                case SkillType.DroneTargetDamage1: return $"드론 타겟의 블록과 몬스터에게 데미지 +{gain}";
                case SkillType.TargetDamage1:      return $"타겟의 모든 블록과 몬스터에게 데미지 +{gain}";
                case SkillType.HammerLevel1:       return $"망치로 부순 블록과 몬스터에게 데미지 +{gain}";
                // 기능 계열 — 적용 후 능력치 절대값(toValue) 반영 (희귀/전설로 여러 단계 올라도 정확)
                case SkillType.DrillMove1:         return $"드릴 블록을 인접 {toValue}칸까지 이동시켜 발동합니다.";
                case SkillType.BombMove1:          return $"폭탄 블록을 인접 {toValue}칸까지 이동시켜 발동합니다.";
                case SkillType.BombKnockback1:     return $"폭탄 폭발 시 넉백 거리가 {toValue}칸 추가됩니다.";
                case SkillType.DrillCushion1:      return $"드릴이 경계에서 {toValue}회 반사합니다.";
                case SkillType.SwapLevel1:         return $"스왑으로 {toValue}칸 거리의 블록과 교환할 수 있습니다.";
                case SkillType.LineLevel1:         return $"라인 드래그 시 다른 색 블록 {toValue}개까지 브릿지로 사용합니다.";
                case SkillType.ChainBomb1:         return $"폭탄 폭발 후 소형 폭탄 {toValue}개가 랜덤 블록에 투척됩니다.";
                case SkillType.DrillPenetrate1:    return $"드릴 투사체가 몬스터 {toValue}마리를 관통합니다.";
                case SkillType.DroneClone1:        return $"총 {toValue}기의 드론이 서로 다른 우선순위 타겟을 동시 공격합니다.";
                case SkillType.DirectHit1:         return $"직접 명중(매칭 직격·드릴·드론·폭탄) 데미지가 {toValue}로 증가합니다 (기본 2).";
                case SkillType.AdjacentHit1:       return $"인접 타격(매칭 인접·폭발 범위) 데미지가 {toValue}로 증가합니다 (기본 1).";
                case SkillType.FallCritChance1:    return $"블록 낙하 치명타 확률이 {toValue}%로 증가합니다 (기본 10%).";
                case SkillType.FallCritDamage1:    return "낙하 치명타 발생 시 데미지가 증가합니다.";
                default:                           return string.IsNullOrEmpty(fallback) ? "" : fallback;
            }
        }

        /// <summary>SkillType 체인 레벨(체인 내 위치+1: Lv1/2/3). 비정렬 체인(드릴쿠션)도 정확.</summary>
        private static int SkillChainLevel(SkillType st)
        {
            return ChainPos(st) + 1;
        }

        /// <summary>해당 체인의 최대 레벨 (보통 3, 정의가 없으면 더 낮음).</summary>
        private static int SkillChainMaxLevel(SkillType st)
        {
            int baseVal = ChainBaseVal(st); // 체인 시작값 (예: 100, 550) — 비정렬 체인 안전
            int max = 1;
            for (int k = 0; k < 3; k++)
                if (SkillTreeDefinition.GetSkill((SkillType)(baseVal + k)) != null) max = k + 1;
            return max;
        }

        private void OnChoiceClicked(SkillType skillType, RewardRarity rarity)
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayRewardPickSound(); // ★ 효과음: 리워드 픽 확정
            if (SkillTreeManager.Instance == null) return;

            // ★ 희귀도 상승폭만큼 같은 체인에서 연속 해금 (고급 +1 / 희귀 +2 / 전설 +3).
            //   현재 후보 skillType부터 enum 값 +1씩, 같은 체인의 유효 스킬만 해금.
            int gain = RarityGain(rarity);
            int baseVal = (int)skillType;
            int unlocked = 0;
            for (int k = 0; k < gain; k++)
            {
                var t = (SkillType)(baseVal + k);
                if (SkillTreeDefinition.GetSkill(t) == null) break; // 체인 끝
                if ((int)t % 100 != ((int)skillType % 100) + k) break; // 다른 체인 침범 방지
                if (SkillTreeManager.Instance.ForceUnlockSkill(t)) unlocked++;
            }
            Debug.Log($"[SkillUpgradeOfferSystem] {skillType} 희귀도 {RarityNameKo(rarity)}(+{gain}) → {unlocked}레벨 해금");

            // 모달 정리
            if (choiceModal != null) Destroy(choiceModal);
            choiceModal = null;
            isShowingChoice = false;

            // ★ 픽 후 게이지 사이클(사용자 요청): 만충 게이지 드레인 → 오버플로 리필 →
            //   또 만충이면 다음 오퍼, 아니면 게임 재개. (일시정지 중 unscaled 대기로 애니/숫자 롤 표시)
            StartCoroutine(DrainRefillAndMaybeNext());
        }

        /// <summary>
        /// 리워드 픽 후 RW 게이지 사이클 — 만충(10) 드레인 → overflowCounter에서 리필 →
        /// 또 만충이면 다음 오퍼(몬스터 이미 이동했으므로 즉시), 아니면 timeScale 복원.
        /// 게임 일시정지(timeScale 0) 중이라 WaitForSecondsRealtime으로 게이지 애니·숫자 롤을 보여준다.
        /// </summary>
        private IEnumerator DrainRefillAndMaybeNext()
        {
            pendingUpgrades = 0;
            // 1. 드레인: 만충 게이지 비움 (10 → 0, 숫자 롤다운)
            orangeCounter = 0;
            UpdateProgressGauge();
            yield return new WaitForSecondsRealtime(0.5f);
            // 2. 리필: 오버플로에서 채움 (0 → fill, 숫자 롤업)
            int fill = Mathf.Min(overflowCounter, ORANGE_PER_UPGRADE);
            overflowCounter -= fill;
            orangeCounter = fill;
            UpdateProgressGauge();
            if (fill > 0) yield return new WaitForSecondsRealtime(0.5f);
            // 3. 또 만충이면 다음 오퍼 반복, 아니면 게임 재개
            if (orangeCounter >= ORANGE_PER_UPGRADE)
            {
                pendingUpgrades = 1;
                TryShowNextChoice();
            }
            else
            {
                // ★ 0 복원 방어 — prevTimeScale이 어떤 경로로든 0이면 1로 보정 (영구 정지 방지)
                Time.timeScale = prevTimeScale > 0f ? prevTimeScale : 1f;
            }
        }

        // ============================================================
        // 리워드 패널 (HUD 좌하단) — 이번 런에 업그레이드한 능력을 아이콘 타일로 표시.
        //   다크 글래스 패널 + 좌측 골드 액센트 + "리워드" 헤더. 카테고리별 1타일(PNG 아이콘 + Lv 배지).
        //   타일 클릭 → 상세 툴팁. 바닥 고정(pivot 0,0)이라 리워드가 늘면 위로 성장.
        //   (구버전: 우상단 색사각+유니코드 심볼 → 좌하단 아이콘 타일로 가독성/디자인 개선 — 클로드 디자인 목업 기반)
        // ============================================================
        private const float RP_TILE = 114f, RP_GAP = 2f, RP_PAD = 8f, RP_HEADER = 30f, RP_ICON_INSET = 3f; // 아이콘 108px(2배) 유지 + 슬롯/간격 최소화
        private const float RP_RIM = 13f; // 골드 림/라운드 코너 회피용 헤더 좌우 추가 인셋(텍스트 겹침 방지)
        private const int RP_COLS = 4;
        private const int RP_MAX_ROWS = 3;   // ★ 리워드 패널 최대 표시 행 수 (초과 시 세로 스크롤)
        private static readonly Color RP_GLASS  = new Color(0.075f, 0.102f, 0.196f, 0.97f);
        private static readonly Color RP_SLOT   = new Color(0.137f, 0.173f, 0.282f, 1f);
        private static readonly Color RP_GOLD   = new Color(0.914f, 0.784f, 0.294f, 1f);
        private static readonly Color RP_TITLE  = new Color(1f, 0.914f, 0.659f, 1f);
        private static readonly Color RP_SUBTLE = new Color(0.624f, 0.69f, 0.847f, 1f);

        private void EnsureIconBar()
        {
            if (iconBarObj != null) return;
            var cv = FindCanvas();
            if (cv == null) return;

            iconBarObj = new GameObject("RewardPanel");
            iconBarObj.transform.SetParent(cv.transform, false);
            iconBarRt = iconBarObj.AddComponent<RectTransform>();
            iconBarRt.anchorMin = iconBarRt.anchorMax = new Vector2(0f, 0f); // 좌하단
            iconBarRt.pivot = new Vector2(0f, 0f);                            // 바닥 고정 → 늘면 위로
            iconBarRt.anchoredPosition = new Vector2(14f, 18f);              // 최하단으로 내림 (에디터 토글 버튼이 이 패널 바로 위에 배치됨)
            var bg = iconBarObj.AddComponent<Image>();
            var rwPanel = RewardPanelSpr(); // 클로드 디자인 다크글래스+골드림 9-slice
            if (rwPanel != null) { bg.sprite = rwPanel; bg.type = Image.Type.Sliced; bg.color = Color.white; }
            else { var panelSpr = PanelFill(); if (panelSpr != null) { bg.sprite = panelSpr; bg.type = Image.Type.Sliced; } bg.color = RP_GLASS; }
            bg.raycastTarget = true;
            // ★ 패널 영역 좌드래그로 닫기 (타일 Button은 IDragHandler 미구현 → 드래그가 패널로 버블링; 닫기 버튼 클릭 유지)
            var closeDrag = iconBarObj.AddComponent<RewardDragToggle>();
            closeDrag.direction = -1; closeDrag.request = RequestRewardCollapse;
        }

        public void RebuildIconBar()
        {
            EnsureIconBar();
            if (iconBarObj == null) return;

            // 자식(액센트/헤더/타일) 전체 정리 후 재구성
            iconBarItems.Clear();
            for (int i = iconBarObj.transform.childCount - 1; i >= 0; i--)
                Destroy(iconBarObj.transform.GetChild(i).gameObject);

            if (SkillTreeManager.Instance == null) { iconBarObj.SetActive(false); if (rewardCollapseLine != null) rewardCollapseLine.SetActive(false); return; }

            // 카테고리별 (아이콘=첫 스킬, 툴팁=최고 스킬, 레벨=해금 수) 수집
            var firsts = new List<SkillType>();
            var highs = new List<SkillType>();
            var levels = new List<int>();
            foreach (var chain in SkillTreeManager.CategoryChains)
            {
                int lvl = 0; SkillType high = SkillType.None, first = SkillType.None;
                foreach (var s in chain)
                {
                    if (first == SkillType.None) first = s;
                    if (SkillTreeManager.Instance.IsSkillUnlocked(s)) { lvl++; high = s; }
                }
                if (lvl > 0) { firsts.Add(first); highs.Add(high); levels.Add(lvl); }
            }

            int n = firsts.Count;
            if (n == 0) { iconBarObj.SetActive(false); if (rewardCollapseLine != null) rewardCollapseLine.SetActive(false); return; } // 리워드 없으면 패널·라인 숨김
            iconBarObj.SetActive(true);

            int cols = Mathf.Min(RP_COLS, n);
            int rows = Mathf.CeilToInt(n / (float)cols);
            int visRows = Mathf.Min(rows, RP_MAX_ROWS);      // ★ 최대 3행만 표시
            bool scroll = rows > RP_MAX_ROWS;                 // 4행째부터 스크롤
            float gridW = cols * RP_TILE + (cols - 1) * RP_GAP;
            float gridHFull = rows * RP_TILE + (rows - 1) * RP_GAP;   // 전체 행 높이
            float gridHVis = visRows * RP_TILE + (visRows - 1) * RP_GAP; // 보이는(최대 3행) 높이
            float panelW = RP_PAD * 2f + gridW;
            float panelH = RP_PAD + RP_HEADER + 6f + gridHVis + RP_PAD;  // 패널은 보이는 행 기준
            iconBarRt.sizeDelta = new Vector2(panelW, panelH);
            lastPanelH = panelH; // 접힘 라인 높이로 재사용

            // ★ 좌측 골드 세로 스트립(BuildRewardAccent) 제거 — 새 골드림 프레임과 충돌해 라운드 코너서 삐져나옴.
            //   (골드림이 이미 프레임 역할 → 별도 스트립 불필요)
            BuildRewardHeader(n);

            float gridTop = RP_PAD + RP_HEADER + 6f; // 패널 top → 그리드 top 거리

            // 3행 이하: 패널 직속 자식. 4행 이상: 스크롤(뷰포트 마스크 + 콘텐츠 전체행) 콘텐츠 자식.
            Transform tileParent; float tileX0, tileY0;
            if (scroll)
            {
                tileParent = BuildRewardScroll(gridTop, gridW, gridHVis, gridHFull);
                tileX0 = 0f; tileY0 = 0f; // 콘텐츠는 그리드 top·left 기준
            }
            else { tileParent = iconBarObj.transform; tileX0 = RP_PAD; tileY0 = gridTop; }

            for (int i = 0; i < n; i++)
            {
                int r = i / cols, c = i % cols;
                float x = tileX0 + c * (RP_TILE + RP_GAP);
                float yTop = tileY0 + r * (RP_TILE + RP_GAP);
                var tile = BuildRewardTile(firsts[i], highs[i], levels[i], x, yTop, tileParent);
                if (tile != null) iconBarItems.Add(tile);
            }

            // 접기 탭(패널 우측) 빌드 + 접힘/펴짐 상태 즉시 적용
            BuildRewardCollapseButton();
            ApplyRewardCollapseState();
        }

        // ============================================================
        // 리워드 패널 접기/펴기 (왼쪽 라인으로 접힘 + "열기" 탭)
        // ============================================================
        private const float REWARD_LINE_W = 56f;          // 측면 도킹 열기 탭 폭 (reward_open_tab 56×150 종횡비 고정)
        private float CollapsedTabH() => 150f;            // 고정 높이(Simple 스프라이트라 종횡비 유지 필수)

        // ★ 접기 버튼: 패널 헤더 우상단 코너에 작은 셰브론 칩으로 통합(돌출 제거 — UI에 자연스럽게 녹아듦).
        private void BuildRewardCollapseButton()
        {
            if (iconBarObj == null) return;
            var btn = new GameObject("CollapseBtn");
            btn.transform.SetParent(iconBarObj.transform, false);
            var bRt = btn.AddComponent<RectTransform>();
            bRt.anchorMin = bRt.anchorMax = new Vector2(1f, 1f); bRt.pivot = new Vector2(1f, 1f);
            bRt.anchoredPosition = new Vector2(-(RP_RIM - 3f), -(RP_PAD - 1f)); // 헤더 우상단 코너(골드 림 안쪽)
            bRt.sizeDelta = new Vector2(30f, 24f);
            var img = btn.AddComponent<Image>();
            var slotSpr = RewardSlotSpr(); // 작은 소켓 칩
            if (slotSpr != null) { img.sprite = slotSpr; img.type = Image.Type.Simple; img.color = Color.white; }
            else { var p = PanelFill(); if (p != null) { img.sprite = p; img.type = Image.Type.Sliced; } img.color = RP_SLOT; }
            img.raycastTarget = true;
            var chv = new GameObject("Chevron");
            chv.transform.SetParent(btn.transform, false);
            var cRt = chv.AddComponent<RectTransform>();
            cRt.anchorMin = Vector2.zero; cRt.anchorMax = Vector2.one; cRt.offsetMin = Vector2.zero; cRt.offsetMax = new Vector2(0f, 2f);
            var ct = chv.AddComponent<Text>();
            ct.font = font; ct.fontSize = 22; ct.fontStyle = FontStyle.Bold; ct.alignment = TextAnchor.MiddleCenter;
            ct.color = RP_TITLE; ct.text = "‹"; ct.raycastTarget = false; // ‹ 왼쪽 셰브론(왼쪽으로 접힘)
            ct.horizontalOverflow = HorizontalWrapMode.Overflow; ct.verticalOverflow = VerticalWrapMode.Overflow;
            btn.AddComponent<Button>().onClick.AddListener(ToggleRewardCollapsed);
        }

        // 접힘 라인 + "열기" 탭 (persistent — 리빌드에 파괴 안 됨)
        private void EnsureRewardCollapseLine()
        {
            if (rewardCollapseLine != null) return;
            var cv = FindCanvas();
            if (cv == null) return;

            // ★ 접힘 = 패널과 동일 골드림 9-slice 슬림 북마크 탭(자연 통합). 탭 전체가 "열기" 버튼.
            rewardCollapseLine = new GameObject("RewardCollapseTab");
            rewardCollapseLine.transform.SetParent(cv.transform, false);
            rewardCollapseLineRt = rewardCollapseLine.AddComponent<RectTransform>();
            rewardCollapseLineRt.anchorMin = rewardCollapseLineRt.anchorMax = new Vector2(0f, 0f);
            rewardCollapseLineRt.pivot = new Vector2(0f, 0f);
            rewardCollapseLineRt.anchoredPosition = new Vector2(0f, 18f); // ★ 측면(좌측 화면 엣지)에 도킹
            rewardCollapseLineRt.sizeDelta = new Vector2(REWARD_LINE_W, CollapsedTabH());
            var lImg = rewardCollapseLine.AddComponent<Image>();
            var rwOpen = RewardOpenTabSpr(); // 클로드 디자인 측면 도킹 탭(좌측 각진+우측 마름모 라운드, ">" 꺽쇠 내장)
            if (rwOpen != null) { lImg.sprite = rwOpen; lImg.type = Image.Type.Simple; lImg.color = Color.white; }
            else { var spr = PanelFill(); if (spr != null) { lImg.sprite = spr; lImg.type = Image.Type.Sliced; } lImg.color = RP_GLASS; }
            lImg.raycastTarget = true;
            rewardCollapseLine.AddComponent<Button>().onClick.AddListener(ToggleRewardCollapsed); // 클릭으로 열기 유지
            // ★ 우드래그로도 열기 (클릭 유지)
            var openDrag = rewardCollapseLine.AddComponent<RewardDragToggle>();
            openDrag.direction = 1; openDrag.request = RequestRewardCollapse;
            // ">" 꺽쇠는 스프라이트에 내장 — 별도 라벨 없음(닫기 "‹"와 통일)

            rewardCollapseLine.SetActive(false);
        }


        // 접힘/펴짐 상태 즉시 적용 (애니메이션 없이 — 리빌드 시 사용)
        private void ApplyRewardCollapseState()
        {
            EnsureRewardCollapseLine();
            if (rewardCollapseLineRt != null)
            {
                rewardCollapseLineRt.anchoredPosition = new Vector2(0f, 18f);
                rewardCollapseLineRt.sizeDelta = new Vector2(REWARD_LINE_W, CollapsedTabH());
            }
            if (rewardCollapsed)
            {
                if (iconBarObj != null) iconBarObj.SetActive(false);
                if (rewardCollapseLine != null) { rewardCollapseLine.SetActive(true); rewardCollapseLine.transform.SetAsLastSibling(); }
            }
            else
            {
                if (rewardCollapseLine != null) rewardCollapseLine.SetActive(false);
                if (iconBarObj != null)
                {
                    iconBarObj.SetActive(true);
                    if (iconBarRt != null) iconBarRt.anchoredPosition = new Vector2(14f, 18f);
                }
            }
        }

        // 토글 (접기/열기 버튼 클릭) — 현재 상태 반전
        public void ToggleRewardCollapsed() => RequestRewardCollapse(!rewardCollapsed);

        // 목표 상태 요청 (클릭·드래그 공용). collapse=true 닫기, false 열기. 상태 동일/애니 중이면 무시.
        public void RequestRewardCollapse(bool collapse)
        {
            if (rewardToggleAnimating) return;
            if (rewardCollapsed == collapse) return;
            rewardCollapsed = collapse;
            StartCoroutine(AnimateRewardToggle(rewardCollapsed));
        }

        // 슬라이드 애니메이션: 접힘=패널 왼쪽으로 슬라이드아웃→라인, 펴짐=라인 숨김→패널 슬라이드인
        private IEnumerator AnimateRewardToggle(bool collapse)
        {
            rewardToggleAnimating = true;
            EnsureRewardCollapseLine();
            if (rewardCollapseLineRt != null)
            {
                rewardCollapseLineRt.anchoredPosition = new Vector2(0f, 18f);
                rewardCollapseLineRt.sizeDelta = new Vector2(REWARD_LINE_W, CollapsedTabH());
            }
            float dur = 0.22f;
            float offX = iconBarRt != null ? -(iconBarRt.sizeDelta.x + 40f) : -520f; // 화면 왼쪽 밖

            if (collapse)
            {
                if (rewardCollapseLine != null) { rewardCollapseLine.SetActive(true); rewardCollapseLine.transform.SetAsLastSibling(); }
                if (iconBarObj != null && iconBarObj.activeSelf && iconBarRt != null)
                {
                    float x0 = iconBarRt.anchoredPosition.x;
                    float el = 0f;
                    while (el < dur)
                    {
                        el += Time.unscaledDeltaTime;
                        float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(el / dur));
                        iconBarRt.anchoredPosition = new Vector2(Mathf.Lerp(x0, offX, e), 18f);
                        yield return null;
                    }
                    iconBarObj.SetActive(false);
                    iconBarRt.anchoredPosition = new Vector2(14f, 18f); // 다음 펴짐 대비 기준 복원
                }
            }
            else
            {
                if (rewardCollapseLine != null) rewardCollapseLine.SetActive(false);
                if (iconBarObj != null && iconBarRt != null)
                {
                    iconBarObj.SetActive(true);
                    iconBarRt.anchoredPosition = new Vector2(offX, 18f);
                    float el = 0f;
                    while (el < dur)
                    {
                        el += Time.unscaledDeltaTime;
                        float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(el / dur));
                        iconBarRt.anchoredPosition = new Vector2(Mathf.Lerp(offX, 14f, e), 18f);
                        yield return null;
                    }
                    iconBarRt.anchoredPosition = new Vector2(14f, 18f);
                }
            }
            rewardToggleAnimating = false;
        }

        // 리워드 스크롤(4행 이상): ScrollRect + 뷰포트(마스크) + 콘텐츠(전체행) + 얇은 스크롤바. 콘텐츠 Transform 반환.
        private Transform BuildRewardScroll(float gridTop, float gridW, float gridHVis, float gridHFull)
        {
            var scrollObj = new GameObject("RewardScroll");
            scrollObj.transform.SetParent(iconBarObj.transform, false);
            var sRt = scrollObj.AddComponent<RectTransform>();
            sRt.anchorMin = sRt.anchorMax = new Vector2(0f, 1f); sRt.pivot = new Vector2(0f, 1f);
            sRt.anchoredPosition = new Vector2(RP_PAD, -gridTop);
            sRt.sizeDelta = new Vector2(gridW, gridHVis);
            var scroll = scrollObj.AddComponent<ScrollRect>();
            scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic; scroll.elasticity = 0.08f;
            scroll.inertia = true; scroll.decelerationRate = 0.12f; scroll.scrollSensitivity = 34f;

            // 뷰포트 (마스크 + 빈 영역 드래그 수신)
            var vp = new GameObject("Viewport");
            vp.transform.SetParent(scrollObj.transform, false);
            var vpRt = vp.AddComponent<RectTransform>();
            vpRt.anchorMin = Vector2.zero; vpRt.anchorMax = Vector2.one; vpRt.pivot = new Vector2(0f, 1f);
            vpRt.offsetMin = Vector2.zero; vpRt.offsetMax = Vector2.zero;
            var vpImg = vp.AddComponent<Image>(); vpImg.color = new Color(0f, 0f, 0f, 0.0015f); vpImg.raycastTarget = true;
            vp.AddComponent<RectMask2D>();

            // 콘텐츠 (전체 행 높이)
            var content = new GameObject("Content");
            content.transform.SetParent(vp.transform, false);
            var cRt = content.AddComponent<RectTransform>();
            cRt.anchorMin = cRt.anchorMax = new Vector2(0f, 1f); cRt.pivot = new Vector2(0f, 1f);
            cRt.anchoredPosition = Vector2.zero;
            cRt.sizeDelta = new Vector2(gridW, gridHFull);

            scroll.viewport = vpRt;
            scroll.content = cRt;

            // 얇은 스크롤바 (우측 가장자리, 골드 핸들) — 스크롤 가능함을 시각적으로 안내
            var sbObj = new GameObject("Scrollbar");
            sbObj.transform.SetParent(scrollObj.transform, false);
            var sbRt = sbObj.AddComponent<RectTransform>();
            sbRt.anchorMin = new Vector2(1f, 0f); sbRt.anchorMax = new Vector2(1f, 1f); sbRt.pivot = new Vector2(1f, 0.5f);
            sbRt.anchoredPosition = Vector2.zero; sbRt.sizeDelta = new Vector2(4f, 0f);
            var sbImg = sbObj.AddComponent<Image>(); sbImg.color = new Color(0.137f, 0.173f, 0.282f, 0.55f); sbImg.raycastTarget = true;
            var scrollbar = sbObj.AddComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            var handle = new GameObject("Handle");
            handle.transform.SetParent(sbObj.transform, false);
            var hRt = handle.AddComponent<RectTransform>();
            hRt.anchorMin = Vector2.zero; hRt.anchorMax = Vector2.one; hRt.offsetMin = Vector2.zero; hRt.offsetMax = Vector2.zero;
            var hImg = handle.AddComponent<Image>(); hImg.color = new Color(RP_GOLD.r, RP_GOLD.g, RP_GOLD.b, 0.85f); hImg.raycastTarget = true;
            scrollbar.handleRect = hRt; scrollbar.targetGraphic = hImg;
            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

            return content.transform;
        }

        // 좌측 골드 액센트 스트립
        private void BuildRewardAccent(float panelH)
        {
            var accent = new GameObject("Accent");
            accent.transform.SetParent(iconBarObj.transform, false);
            var aRt = accent.AddComponent<RectTransform>();
            aRt.anchorMin = aRt.anchorMax = new Vector2(0f, 1f); aRt.pivot = new Vector2(0f, 1f);
            aRt.anchoredPosition = new Vector2(5f, -10f);
            aRt.sizeDelta = new Vector2(5f, panelH - 20f);
            var aImg = accent.AddComponent<Image>();
            aImg.color = RP_GOLD; aImg.raycastTarget = false;
        }

        // 헤더: ★ + "리워드" + 개수 + 구분선
        private void BuildRewardHeader(int count)
        {
            // ★ 골드 림 코너를 피하도록 좌측 인셋을 RP_RIM만큼 더 줌(겹침 방지). 우측은 접기 버튼 자리 확보.
            MakeHdrText("HdrStar", "★", 20, RP_GOLD, TextAnchor.MiddleLeft, FontStyle.Normal,
                        new Vector2(0f, 1f), new Vector2(RP_RIM + 2f, -RP_PAD - 1f), new Vector2(22f, 26f));
            MakeHdrText("HdrTitle", "리워드", 19, RP_TITLE, TextAnchor.MiddleLeft, FontStyle.Bold,
                        new Vector2(0f, 1f), new Vector2(RP_RIM + 26f, -RP_PAD), new Vector2(120f, 26f));
            MakeHdrText("HdrCount", $"{count}개", 14, RP_SUBTLE, TextAnchor.MiddleRight, FontStyle.Bold,
                        new Vector2(1f, 1f), new Vector2(-RP_RIM - 30f, -RP_PAD - 1f), new Vector2(60f, 24f));
            // 구분선
            var div = new GameObject("HdrDivider");
            div.transform.SetParent(iconBarObj.transform, false);
            var dRt = div.AddComponent<RectTransform>();
            dRt.anchorMin = new Vector2(0f, 1f); dRt.anchorMax = new Vector2(1f, 1f); dRt.pivot = new Vector2(0.5f, 1f);
            dRt.anchoredPosition = new Vector2(0f, -(RP_PAD + RP_HEADER - 2f));
            dRt.sizeDelta = new Vector2(-RP_PAD * 2f - 6f, 1.5f);
            var dImg = div.AddComponent<Image>();
            dImg.color = new Color(0.274f, 0.32f, 0.478f, 0.5f); dImg.raycastTarget = false;
        }

        private void MakeHdrText(string name, string text, int size, Color col, TextAnchor align, FontStyle style,
                                 Vector2 anchor, Vector2 pos, Vector2 dim)
        {
            var o = new GameObject(name);
            o.transform.SetParent(iconBarObj.transform, false);
            var rt = o.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = anchor; rt.pivot = anchor;
            rt.anchoredPosition = pos; rt.sizeDelta = dim;
            var t = o.AddComponent<Text>();
            t.font = font; t.fontSize = size; t.fontStyle = style; t.alignment = align;
            t.color = col; t.text = text; t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
        }

        // 리워드 타일: 슬롯 + PNG 아이콘 + Lv 배지. 클릭 → 툴팁.
        private GameObject BuildRewardTile(SkillType iconSkill, SkillType tooltipSkill, int level, float x, float yTop, Transform parent)
        {
            var tile = new GameObject($"RewardTile_{iconSkill}");
            tile.transform.SetParent(parent, false);
            var rt = tile.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -yTop);
            rt.sizeDelta = new Vector2(RP_TILE, RP_TILE);

            var slot = tile.AddComponent<Image>();
            var rwSlot = RewardSlotSpr(); // 클로드 디자인 다크글래스 소켓 (고정 타일이라 Simple)
            if (rwSlot != null) { slot.sprite = rwSlot; slot.type = Image.Type.Simple; slot.color = Color.white; }
            else { var slotSpr = PanelFill(); if (slotSpr != null) { slot.sprite = slotSpr; slot.type = Image.Type.Sliced; } slot.color = RP_SLOT; }

            SkillType capIcon = iconSkill, capTip = tooltipSkill;
            int capLvl = level;
            tile.AddComponent<Button>().onClick.AddListener(() => ShowTooltip(capIcon, capTip, capLvl));

            // 아이콘 (PNG 우선)
            var node = SkillTreeDefinition.GetSkill(iconSkill);
            Sprite spr = GetSkillSprite(iconSkill);
            var iconObj = new GameObject("Icon");
            iconObj.transform.SetParent(tile.transform, false);
            var iRt = iconObj.AddComponent<RectTransform>();
            iRt.anchorMin = Vector2.zero; iRt.anchorMax = Vector2.one;
            iRt.offsetMin = new Vector2(RP_ICON_INSET, RP_ICON_INSET); iRt.offsetMax = new Vector2(-RP_ICON_INSET, -RP_ICON_INSET); // 슬롯 여백 최소화(아이콘=108px, 2배 유지)
            if (spr != null)
            {
                var iImg = iconObj.AddComponent<Image>();
                iImg.sprite = spr; iImg.preserveAspect = true; iImg.raycastTarget = false;
            }
            else
            {
                var iTxt = iconObj.AddComponent<Text>();
                iTxt.font = font; iTxt.fontSize = 34; iTxt.fontStyle = FontStyle.Bold;
                iTxt.alignment = TextAnchor.MiddleCenter; iTxt.color = Color.white;
                iTxt.text = (node != null && !string.IsNullOrEmpty(node.iconSymbol)) ? node.iconSymbol : "★";
                iTxt.raycastTarget = false;
            }

            // 능력치 배지 — 아이콘 PNG에 구워진 기본 "+1" 배지(우하단)를 덮고 **실제 합산 능력치**(기본+보너스) 표시.
            //   ★ 타일의 자식으로 둠(iconObj 자식이면 렌더 안 됨). 네이비+골드링 배지(오퍼 카드와 동일,
            //   골드 프레임 위에서도 대비)로 베이크드 배지를 충분히 덮는 크기 + 크림색 숫자.
            var badge = new GameObject("LvBadge");
            badge.transform.SetParent(tile.transform, false);
            var bRt = badge.AddComponent<RectTransform>();
            // ★ 베이크드 "+1" 중심(검출값: icon-UV 0.759, bottom-up 0.257)에 정렬. 아이콘 inset 반영해 타일-UV 계산.
            //   배지 52px = 베이크드 "+1"(108px 아이콘 기준 지름~41) 최소 커버 크기(이보다 작으면 "+1"이 다시 삐져나옴).
            float iconSz = RP_TILE - 2f * RP_ICON_INSET;
            float bu = (RP_ICON_INSET + 0.759f * iconSz) / RP_TILE;
            float bv = (RP_ICON_INSET + 0.257f * iconSz) / RP_TILE;
            bRt.anchorMin = bRt.anchorMax = new Vector2(bu, bv); bRt.pivot = new Vector2(0.5f, 0.5f);
            bRt.anchoredPosition = Vector2.zero;
            bRt.sizeDelta = new Vector2(46f, 46f); // 베이크드 "+1"(골드링 0.19UV) 덮는 최소치까지 축소 — 숫자는 들어감
            badge.transform.SetAsLastSibling();
            var bImg = badge.AddComponent<Image>();
            bImg.sprite = GetBadgeSprite(); // 네이비+골드링 원형 — 그 자리(베이크드 "+1")만 덮음, 아이콘 본체는 보임
            bImg.raycastTarget = false;
            var lvObj = new GameObject("LvNum");
            lvObj.transform.SetParent(badge.transform, false);
            var lvRt = lvObj.AddComponent<RectTransform>();
            lvRt.anchorMin = Vector2.zero; lvRt.anchorMax = Vector2.one; lvRt.offsetMin = Vector2.zero; lvRt.offsetMax = Vector2.zero;
            var lvTxt = lvObj.AddComponent<Text>();
            lvTxt.font = font; lvTxt.fontSize = 20; lvTxt.fontStyle = FontStyle.Bold;
            lvTxt.alignment = TextAnchor.MiddleCenter;
            lvTxt.color = RP_TITLE; // 크림색(네이비 배지 위 대비)
            lvTxt.resizeTextForBestFit = true; lvTxt.resizeTextMinSize = 10;
            lvTxt.resizeTextMaxSize = GetStatUnit(iconSkill) == "%" ? 18 : 20; // 낙하 치명타률 "30%/50%/70%"는 숫자 폰트 2pt 작게
            // ★ 배지 = 실제 합산 능력치(기본 + 보너스). 레벨 대신 toLv 표시 (사용자 요청).
            //   예) 직접 타격 2+2=4, 인접 1+2=3, 치명타데미지 1+1=2, 치명타확률 10+20=30% (GetStatUnit "%").
            int tileStatBase = GetStatBase(iconSkill);
            int tilePerLevel = GetStatPerLevel(iconSkill);
            int tileStatTotal = tileStatBase + level * tilePerLevel;
            lvTxt.text = $"{tileStatTotal}{GetStatUnit(iconSkill)}"; lvTxt.raycastTarget = false;

            return tile;
        }


        // ============================================================
        // 툴팁 (아이콘 클릭 시 표시, 아무 곳 클릭 시 닫힘)
        // ============================================================
        /// <summary>리워드 타일 클릭 → 상세 설명 팝업(클로드 디자인 다크글래스 + 히어로 메달리온).
        /// iconSkill=타일과 동일한 PNG 아이콘, tooltipSkill=이름/설명 기준, level=이번 런 누적 레벨.</summary>
        private void ShowTooltip(SkillType iconSkill, SkillType tooltipSkill, int level)
        {
            var node = SkillTreeDefinition.GetSkill(tooltipSkill);
            if (node == null) return;

            CloseTooltip();
            var cv = FindCanvas();
            if (cv == null) return;

            // 능력치 총량(타일 배지와 동일) + 표시 문자열
            int statTotal = GetStatBase(iconSkill) + level * GetStatPerLevel(iconSkill);
            string unit = GetStatUnit(iconSkill);

            // ── 전체 화면 딤 + 클릭 캐처(아무 곳 탭 → 닫기) ──
            tooltipOverlay = new GameObject("SkillTooltipOverlay");
            tooltipOverlay.transform.SetParent(cv.transform, false);
            var rt = tooltipOverlay.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var capture = tooltipOverlay.AddComponent<Image>();
            capture.color = ClaudeTheme.PopupOverlay;
            capture.raycastTarget = true;
            var captureBtn = tooltipOverlay.AddComponent<Button>();
            captureBtn.transition = Selectable.Transition.None;
            captureBtn.onClick.AddListener(CloseTooltip);

            // ── 카드 (클로드 다크글래스 팝업 패널) ──
            var card = new GameObject("TooltipCard");
            card.transform.SetParent(tooltipOverlay.transform, false);
            var cardRt = card.AddComponent<RectTransform>();
            cardRt.anchorMin = cardRt.anchorMax = cardRt.pivot = new Vector2(0.5f, 0.5f);
            cardRt.sizeDelta = new Vector2(460f, 372f);
            var cardBg = card.AddComponent<Image>();
            ClaudeTheme.ApplyPopupPanel(cardBg);   // 다크글래스 9-slice + 골드림
            cardBg.raycastTarget = false;          // 카드 내부 탭도 닫기 허용(요구사항)

            // ── 상단 이미지 = 리워드 패널 타일 그대로(슬롯+PNG아이콘+배지, BuildRewardTile 동일 사양의 확대 복제) ──
            //    사용자 요청: "리워드에 표시된 그대로만 이미지로 사용" — 별도 메달리온/합성 없음.
            const float TILE_SCALE = 1.4f;                       // 114px 타일 → 160px (팝업 히어로 크기)
            float tileSz = RP_TILE * TILE_SCALE;
            var tileObj = new GameObject("TileImage");
            tileObj.transform.SetParent(card.transform, false);
            var tileRt = tileObj.AddComponent<RectTransform>();
            tileRt.anchorMin = tileRt.anchorMax = new Vector2(0.5f, 1f); tileRt.pivot = new Vector2(0.5f, 1f);
            tileRt.anchoredPosition = new Vector2(0f, -18f);
            tileRt.sizeDelta = new Vector2(tileSz, tileSz);
            var slotImg = tileObj.AddComponent<Image>();
            var rwSlot = RewardSlotSpr();
            if (rwSlot != null) { slotImg.sprite = rwSlot; slotImg.type = Image.Type.Simple; slotImg.color = Color.white; }
            else { var pf = PanelFill(); if (pf != null) { slotImg.sprite = pf; slotImg.type = Image.Type.Sliced; } slotImg.color = RP_SLOT; }
            slotImg.raycastTarget = false;
            // 아이콘 (타일과 동일 PNG, 동일 인셋 비율)
            float inset = RP_ICON_INSET * TILE_SCALE;
            var iconSpr = GetSkillSprite(iconSkill);
            var icoObj = new GameObject("Icon");
            icoObj.transform.SetParent(tileObj.transform, false);
            var icoRt = icoObj.AddComponent<RectTransform>();
            icoRt.anchorMin = Vector2.zero; icoRt.anchorMax = Vector2.one;
            icoRt.offsetMin = new Vector2(inset, inset); icoRt.offsetMax = new Vector2(-inset, -inset);
            if (iconSpr != null)
            {
                var ico = icoObj.AddComponent<Image>();
                ico.sprite = iconSpr; ico.preserveAspect = true; ico.raycastTarget = false;
            }
            else
            {
                var icoTxt = icoObj.AddComponent<Text>();
                icoTxt.font = font; icoTxt.fontSize = 46; icoTxt.fontStyle = FontStyle.Bold;
                icoTxt.alignment = TextAnchor.MiddleCenter; icoTxt.color = Color.white;
                icoTxt.text = string.IsNullOrEmpty(node.iconSymbol) ? "★" : node.iconSymbol;
                icoTxt.raycastTarget = false;
            }
            // 능력치 배지 (타일과 동일 스프라이트·위치·표기 — 베이크드 "+1" 자리 bu/bv, 값은 절대 총량)
            float iconSz = RP_TILE - 2f * RP_ICON_INSET;
            float bu = (RP_ICON_INSET + 0.759f * iconSz) / RP_TILE;
            float bv = (RP_ICON_INSET + 0.257f * iconSz) / RP_TILE;
            var badge = new GameObject("LvBadge");
            badge.transform.SetParent(tileObj.transform, false);
            var bRt = badge.AddComponent<RectTransform>();
            bRt.anchorMin = bRt.anchorMax = new Vector2(bu, bv); bRt.pivot = new Vector2(0.5f, 0.5f);
            bRt.anchoredPosition = Vector2.zero;
            bRt.sizeDelta = new Vector2(46f * TILE_SCALE, 46f * TILE_SCALE);
            var bImg = badge.AddComponent<Image>();
            bImg.sprite = GetBadgeSprite(); bImg.raycastTarget = false;
            var bTxtObj = new GameObject("LvNum");
            bTxtObj.transform.SetParent(badge.transform, false);
            var bTxtRt = bTxtObj.AddComponent<RectTransform>();
            bTxtRt.anchorMin = Vector2.zero; bTxtRt.anchorMax = Vector2.one;
            bTxtRt.offsetMin = Vector2.zero; bTxtRt.offsetMax = Vector2.zero;
            var bTxt = bTxtObj.AddComponent<Text>();
            bTxt.font = font; bTxt.fontStyle = FontStyle.Bold; bTxt.alignment = TextAnchor.MiddleCenter;
            bTxt.color = RP_TITLE; // 타일과 동일 크림색
            bTxt.resizeTextForBestFit = true; bTxt.resizeTextMinSize = 12;
            bTxt.resizeTextMaxSize = Mathf.RoundToInt((unit == "%" ? 18 : 20) * TILE_SCALE);
            bTxt.text = $"{statTotal}{unit}"; // 타일 배지와 동일 표기
            bTxt.raycastTarget = false;

            // ── 이름 (골드) ──
            var nameObj = new GameObject("Name");
            nameObj.transform.SetParent(card.transform, false);
            var nameRt = nameObj.AddComponent<RectTransform>();
            nameRt.anchorMin = nameRt.anchorMax = new Vector2(0.5f, 1f); nameRt.pivot = new Vector2(0.5f, 1f);
            nameRt.anchoredPosition = new Vector2(0f, -190f);
            nameRt.sizeDelta = new Vector2(420f, 38f);
            var nameTxt = nameObj.AddComponent<Text>();
            nameTxt.font = font; nameTxt.fontSize = 27; nameTxt.fontStyle = FontStyle.Bold;
            nameTxt.alignment = TextAnchor.MiddleCenter; nameTxt.color = ClaudeTheme.PopupTitle;
            nameTxt.horizontalOverflow = HorizontalWrapMode.Overflow; nameTxt.verticalOverflow = VerticalWrapMode.Overflow;
            nameTxt.text = GetDisplayName(tooltipSkill, node.skillName);
            nameTxt.raycastTarget = false;

            // ── 골드 구분선 ──
            var divObj = new GameObject("Divider");
            divObj.transform.SetParent(card.transform, false);
            var divRt = divObj.AddComponent<RectTransform>();
            divRt.anchorMin = divRt.anchorMax = new Vector2(0.5f, 1f); divRt.pivot = new Vector2(0.5f, 1f);
            divRt.anchoredPosition = new Vector2(0f, -228f);
            divRt.sizeDelta = new Vector2(320f, 2f);
            var divImg = divObj.AddComponent<Image>();
            divImg.color = new Color(0.82f, 0.68f, 0.38f, 0.55f); divImg.raycastTarget = false;

            // ── 설명 (간결·정확) ──
            var descObj = new GameObject("Desc");
            descObj.transform.SetParent(card.transform, false);
            var descRt = descObj.AddComponent<RectTransform>();
            descRt.anchorMin = new Vector2(0f, 0f); descRt.anchorMax = new Vector2(1f, 1f);
            descRt.offsetMin = new Vector2(30f, 44f); descRt.offsetMax = new Vector2(-30f, -244f);
            var descTxt = descObj.AddComponent<Text>();
            descTxt.font = font; descTxt.fontSize = 19;
            descTxt.alignment = TextAnchor.UpperCenter;
            descTxt.horizontalOverflow = HorizontalWrapMode.Wrap; descTxt.verticalOverflow = VerticalWrapMode.Truncate;
            descTxt.color = ClaudeTheme.PopupText; descTxt.lineSpacing = 1.15f;
            descTxt.text = GetTooltipDescription(iconSkill, tooltipSkill, statTotal);
            descTxt.raycastTarget = false;

            // ── 닫기 안내 ──
            var hintObj = new GameObject("CloseHint");
            hintObj.transform.SetParent(card.transform, false);
            var hintRt = hintObj.AddComponent<RectTransform>();
            hintRt.anchorMin = hintRt.anchorMax = new Vector2(0.5f, 0f); hintRt.pivot = new Vector2(0.5f, 0f);
            hintRt.anchoredPosition = new Vector2(0f, 14f); hintRt.sizeDelta = new Vector2(400f, 22f);
            var hintTxt = hintObj.AddComponent<Text>();
            hintTxt.font = font; hintTxt.fontSize = 14; hintTxt.alignment = TextAnchor.MiddleCenter;
            hintTxt.color = ClaudeTheme.PopupTextMuted;
            hintTxt.text = "화면을 탭하면 닫힙니다";
            hintTxt.raycastTarget = false;
        }

        /// <summary>툴팁용 간결·정확 설명 — 증분(데미지) 계열은 누적 총량, 기능/절대 계열은 절대값 반영.</summary>
        private string GetTooltipDescription(SkillType iconSkill, SkillType tooltipSkill, int total)
        {
            SkillType cat = (SkillType)ChainBaseVal(tooltipSkill);
            switch (cat)
            {
                case SkillType.DrillDamage1:       return $"드릴이 지나간 블록·몬스터에게 데미지 {total}";
                case SkillType.BombDamage1:        return $"폭발 범위의 블록·몬스터에게 데미지 {total}";
                case SkillType.DroneTargetDamage1: return $"드론 타겟의 블록·몬스터에게 데미지 {total}";
                case SkillType.TargetDamage1:      return $"타겟의 모든 블록·몬스터에게 데미지 {total}";
                case SkillType.HammerLevel1:       return $"망치로 부순 블록·몬스터에게 데미지 {total}";
                default:
                    return GetDisplayDescription(tooltipSkill, RewardRarity.Advanced, total,
                        SkillTreeDefinition.GetSkill(tooltipSkill)?.description);
            }
        }

        private void CloseTooltip()
        {
            if (tooltipOverlay != null)
            {
                if (AudioManager.Instance != null) AudioManager.Instance.PlayPopupClose(); // ★ 효과음: 팝업 닫기
                Destroy(tooltipOverlay);
            }
            tooltipOverlay = null;
        }

#if UNITY_EDITOR
        // ============================================================
        // 리워드 툴팁 검증 시스템 (Editor QA) — 전 카테고리 순회하며 툴팁을 열고
        //   아이콘 로드/이름/능력치/설명 데이터를 리포트로 남긴다.
        //   메뉴: MatchMine/리워드 툴팁 검증 (전 카테고리). 재생 중(인게임)에 실행.
        //   출력: 콘솔 + .claude/captures/reward_tooltip_verify.txt
        // ============================================================
        public void StartRewardTooltipVerify() => StartCoroutine(RewardTooltipVerifyRoutine());

        private IEnumerator RewardTooltipVerifyRoutine()
        {
            var chains = SkillTreeManager.CategoryChains;
            if (chains == null) yield break;
            var sb = new System.Text.StringBuilder();
            int missIcon = 0, missDesc = 0;
            sb.AppendLine($"=== 리워드 툴팁 검증 ({chains.Length}개 카테고리) ===");
            for (int i = 0; i < chains.Length; i++)
            {
                var chain = chains[i];
                if (chain == null || chain.Length == 0) continue;
                // 체인 앞 2레벨 해금 시도 후, 리워드 패널과 동일하게 "실제 해금 수"로 레벨 산출
                //   (이미 해금돼 ForceUnlockSkill이 false여도 패널 타일과 배지값이 일치하도록)
                for (int k = 0; k < Mathf.Min(2, chain.Length); k++)
                    SkillTreeManager.Instance?.ForceUnlockSkill(chain[k]);
                int lvl = 0;
                SkillType iconSkill = chain[0], tip = chain[0];
                foreach (var s in chain)
                    if (SkillTreeManager.Instance != null && SkillTreeManager.Instance.IsSkillUnlocked(s)) { lvl++; tip = s; }
                if (lvl == 0) { lvl = 1; tip = chain[0]; }
                int stat = GetStatBase(iconSkill) + lvl * GetStatPerLevel(iconSkill);
                var iconSpr = GetSkillSprite(iconSkill);
                var node = SkillTreeDefinition.GetSkill(tip);
                string nm = node != null ? GetDisplayName(tip, node.skillName) : "?";
                string desc = GetTooltipDescription(iconSkill, tip, stat);
                bool noIcon = iconSpr == null, noDesc = string.IsNullOrEmpty(desc);
                if (noIcon) missIcon++;
                if (noDesc) missDesc++;
                string flag = (noIcon ? " [아이콘없음!]" : "") + (noDesc ? " [설명없음!]" : "");
                sb.AppendLine($"[{i}] {nm} | 아이콘={(iconSpr != null ? iconSpr.name : "NULL")} | 스탯={stat}{GetStatUnit(iconSkill)} | 설명='{desc}'{flag}");
                ShowTooltip(iconSkill, tip, lvl); // 시각 확인용(순차 표시)
                yield return new WaitForSecondsRealtime(1.1f);
            }
            CloseTooltip();
            sb.AppendLine($"--- 결과: 아이콘 누락 {missIcon}건, 설명 누락 {missDesc}건 → {((missIcon == 0 && missDesc == 0) ? "PASS ✅" : "FAIL")}");
            string report = sb.ToString();
            Debug.Log("[리워드툴팁검증]\n" + report);
            try
            {
                string dir = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath), ".claude", "captures");
                System.IO.Directory.CreateDirectory(dir);
                System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "reward_tooltip_verify.txt"), report);
            }
            catch { }
        }
#endif

        // ============================================================
        // 진행 게이지 (마나 게이지 우측, 동일 사이즈)
        //   - 상단: "X/10" 현재 게이지 진행도
        //   - 하단: "단계 N" 누적 해금 스킬 수
        //   - 주황 채움 (Image.fillAmount 수직)
        // ============================================================
        private const float GAUGE_SIZE = 70f;      // MPGaugeUI와 동일
        private const float GAUGE_X_MP = 260f;    // MP 게이지 anchoredPosition.x
        private const float GAUGE_Y = -132f;      // MP 게이지와 동일 Y
        private static readonly Color ORANGE_FILL = new Color(1.0f, 0.65f, 0.18f, 0.95f);
        private static readonly Color ORANGE_BG = new Color(0.30f, 0.18f, 0.08f, 0.85f);

        private void EnsureProgressGauge()
        {
            if (progressGaugeObj != null) return;
            var cv = FindCanvas();
            if (cv == null) return;

            // 스테이지 21+ Stage 모드에서만 표시 (그 외 비활성)
            bool shouldShow = GameManager.Instance != null
                              && GameManager.Instance.CurrentGameMode == GameMode.Stage
                              && GameManager.Instance.SelectedStage >= MIN_STAGE_FOR_REWARD;
            if (!shouldShow)
            {
                if (progressGaugeObj != null) progressGaugeObj.SetActive(false);
                return;
            }

            progressGaugeObj = new GameObject("SkillProgressGauge");
            progressGaugeObj.transform.SetParent(cv.transform, false);
            var rt = progressGaugeObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            // ★ 게임 그리드의 헥사곤 인접 배치와 동일하게 — 단, 두 게이지 사이 3px 간격 유지
            //   flat-top 헥사곤(R=70) axial (q+1, r-1) 이웃의 상대 좌표(접합):
            //     Δx =  1.5 × R = 105
            //     Δy = -√3/2 × R ≈ -60.6
            //   거리 √3 × R 위에 normal 방향(0.866, -0.5)으로 3px 추가하여 시각적 간격 확보
            const float HEX_R = GAUGE_SIZE; // 70 — 게이지 헥사곤 반지름
            const float GAP_PX = 5f;        // 두 헥사곤 사이 간격 (사용자 요청: 3px → 5px)
            float deltaX = 1.5f * HEX_R + GAP_PX * 0.8660254f;
            float deltaY = -0.8660254f * HEX_R - GAP_PX * 0.5f;
            rt.anchoredPosition = new Vector2(GAUGE_X_MP + deltaX, GAUGE_Y + deltaY);
            rt.sizeDelta = new Vector2(GAUGE_SIZE * 2f, GAUGE_SIZE * 2f);

            // ★ 입체 액체 게이지 — MP와 동일 빌더(유리BG→액체→표면→유리Front)+출렁임, RW 골드 액체.
            var rwLiquid  = new Color(0.96f, 0.78f, 0.32f, 1f);   // RW 골드 액체
            var rwSurface = new Color(1f, 0.94f, 0.78f, 0.95f);   // 표면 메니스커스(밝은 골드)
            var rwRefs = JewelsHexaPuzzle.UI.LiquidHexGaugeBuilder.Build(rt, rwLiquid, rwSurface);
            progressFillImage = rwRefs.fill;
            progressSlosh = rwRefs.slosh;
            if (progressSlosh != null) progressSlosh.SetTarget(0f, true);

            // 4. 공용 3-존 텍스트 (MP 게이지와 동일 폰트) — 중앙(값)/하단 2줄(단계 N + RW max N)
            var txt = JewelsHexaPuzzle.UI.LiquidHexGaugeBuilder.BuildText(rt, font, Color.white, new Color(1f, 0.88f, 0.62f, 0.92f));
            rwTopText = txt.top;        // 미사용(비움)
            progressText = txt.center;  // 중앙 값
            rwBottomText = txt.bottom;  // "단계 N\nRW  max N" (사용자 요청: 단계를 RW max 위에)
            // ★ 단계를 "RW max 10" 위에 표시 — 중앙 값을 위로, 하단을 2줄로 확장
            var cRt = progressText.rectTransform;
            cRt.anchorMin = new Vector2(0f, 0.46f); cRt.anchorMax = new Vector2(1f, 0.68f);
            cRt.offsetMin = Vector2.zero; cRt.offsetMax = Vector2.zero;
            var bRt = rwBottomText.rectTransform;
            bRt.anchorMin = new Vector2(0f, 0.06f); bRt.anchorMax = new Vector2(1f, 0.44f);
            bRt.offsetMin = Vector2.zero; bRt.offsetMax = Vector2.zero;
            rwBottomText.fontSize = 10;
            rwBottomText.lineSpacing = 0.78f;
            rwBottomText.text = $"단계 0\nRW  max {ORANGE_PER_UPGRADE}";
        }

        private void UpdateProgressGauge(bool immediate = false)
        {
            EnsureProgressGauge();
            if (progressGaugeObj == null) return;

            // 활성/비활성 토글 (스테이지 변경 등으로 표시 조건이 바뀐 경우)
            bool shouldShow = GameManager.Instance != null
                              && GameManager.Instance.CurrentGameMode == GameMode.Stage
                              && GameManager.Instance.SelectedStage >= MIN_STAGE_FOR_REWARD;
            progressGaugeObj.SetActive(shouldShow);
            if (!shouldShow) return;

            // 채움 비율: orangeCounter / ORANGE_PER_UPGRADE — LiquidGaugeSlosh가 채움·출렁임 구동
            _progressFillTarget = Mathf.Clamp01((float)orangeCounter / ORANGE_PER_UPGRADE);
            if (progressSlosh != null) progressSlosh.SetTarget(_progressFillTarget, immediate);

            // 하단 2줄: "단계 N"(위) + "RW max N"(아래), 중앙: 현재 값 — 숫자 순차 카운트(NumberRoller)
            int learnedCount = CountLearnedSkills();
            if (rwBottomText != null) rwBottomText.text = $"단계 {learnedCount}\nRW  max {ORANGE_PER_UPGRADE}";
            if (progressText != null)
            {
                System.Func<int, string> fmt = v => v.ToString();
                if (immediate) JewelsHexaPuzzle.Utils.NumberRoller.SetImmediate(progressText, orangeCounter, fmt);
                // ★ 한 칸씩 또렷이 보이도록 단위당 0.1초로 느리게 롤(게이지 전용). duration 1.6s 캡.
                else JewelsHexaPuzzle.Utils.NumberRoller.Roll(progressText, orangeCounter, fmt, 1.6f, 0.1f, 0.1f);
            }
        }

        // ★ 리워드 게이지 채움/출렁임은 LiquidGaugeSlosh가 구동 (기존 Update SmoothDamp 제거)
        private float _progressFillTarget = 0f;

        /// <summary>오렌지 리워드로 학습 가능한 카테고리 13개 × 3레벨 중 해금된 총 개수.</summary>
        private int CountLearnedSkills()
        {
            if (SkillTreeManager.Instance == null) return 0;
            int n = 0;
            foreach (var chain in SkillTreeManager.CategoryChains)
            {
                foreach (var s in chain)
                {
                    if (SkillTreeManager.Instance.IsSkillUnlocked(s)) n++;
                }
            }
            return n;
        }

        // ============================================================
        // SDF 헥사곤 스프라이트 (MPGaugeUI와 동일한 flat-top 형상으로 시각 일치)
        // ============================================================

        /// <summary>flat-top 헥사곤 SDF (MPGaugeUI.HexSDF와 동일)</summary>
        private static float HexSDF(Vector2 point, Vector2 center, float radius)
        {
            Vector2 p = point - center;
            float maxDist = float.MinValue;
            for (int i = 0; i < 6; i++)
            {
                float angle = (30f + i * 60f) * Mathf.Deg2Rad;
                Vector2 normal = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                float edgeDist = radius * 0.8660254f;
                float dist = Vector2.Dot(p, normal) - edgeDist;
                if (dist > maxDist) maxDist = dist;
            }
            return maxDist;
        }

        /// <summary>채워진 헥사곤 스프라이트 (MPGaugeUI 배경/채움과 동일 패턴, 색상만 입힘)</summary>
        private static Sprite CreateGaugeHexSprite(int size, float padding, Color color)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Color[] pixels = new Color[size * size];

            Vector2 center = new Vector2(size / 2f, size / 2f);
            float radius = size / 2f - padding;
            float aa = 2.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 point = new Vector2(x + 0.5f, y + 0.5f);
                    float dist = HexSDF(point, center, radius);
                    float alpha = Mathf.Clamp01(1f - dist / aa);
                    if (alpha > 0f)
                    {
                        // 중앙→가장자리 약간 밝아지는 그라데이션 (MPGaugeUI 동일 패턴)
                        float normalizedDist = Mathf.Clamp01(-dist / (radius * 0.8660254f));
                        float brightness = Mathf.Lerp(0.85f, 1f, 1f - normalizedDist);
                        Color col = color * brightness;
                        col.a = color.a * alpha;
                        pixels[y * size + x] = col;
                    }
                    else
                    {
                        pixels[y * size + x] = Color.clear;
                    }
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>헥사곤 테두리 링 스프라이트 (outerRadius - innerRadius 영역만 채움)</summary>
        private static Sprite CreateGaugeHexBorderSprite(int size, float outerPad, float innerPad, Color color)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Color[] pixels = new Color[size * size];

            Vector2 center = new Vector2(size / 2f, size / 2f);
            float outerR = size / 2f - outerPad;
            float innerR = size / 2f - innerPad;
            float aa = 2.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 point = new Vector2(x + 0.5f, y + 0.5f);
                    float outerDist = HexSDF(point, center, outerR);
                    float innerDist = HexSDF(point, center, innerR);
                    // 외곽 안쪽 + 내곽 바깥쪽 = 링 영역
                    float ringAlpha = Mathf.Clamp01(1f - outerDist / aa) * Mathf.Clamp01(innerDist / aa);
                    if (ringAlpha > 0f)
                    {
                        Color col = color;
                        col.a = color.a * ringAlpha;
                        pixels[y * size + x] = col;
                    }
                    else
                    {
                        pixels[y * size + x] = Color.clear;
                    }
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }

    /// <summary>
    /// 리워드 패널 토글 드래그 제스처. 열기 탭=우드래그(열기), 패널=좌드래그(닫기).
    /// 수평 우세 + 임계 이상일 때만 발동(스크롤/탭/세로 제스처와 충돌 방지). 클릭(Button)은 별도 유지.
    /// </summary>
    public class RewardDragToggle : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public int direction;                  // +1 = 우드래그로 열기, -1 = 좌드래그로 닫기
        public System.Action<bool> request;    // request(collapse): true=닫기, false=열기
        public float threshold = 50f;          // 발동 최소 수평 이동(px)
        private Vector2 _press;

        public void OnBeginDrag(PointerEventData e) { _press = e.position; }
        public void OnDrag(PointerEventData e) { } // 라우팅 위해 구현 필요(빈 바디)
        public void OnEndDrag(PointerEventData e)
        {
            float dx = e.position.x - _press.x;
            float dy = e.position.y - _press.y;
            if (Mathf.Abs(dx) < threshold) return;            // 임계 미만 무시
            if (Mathf.Abs(dx) <= Mathf.Abs(dy)) return;       // 수평 우세 아니면 무시(세로 제스처)
            if (direction > 0 && dx > 0) request?.Invoke(false);      // 우드래그 → 열기
            else if (direction < 0 && dx < 0) request?.Invoke(true);  // 좌드래그 → 닫기
        }
    }
}
