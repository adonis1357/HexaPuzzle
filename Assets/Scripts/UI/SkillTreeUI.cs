using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using JewelsHexaPuzzle.Core;
using JewelsHexaPuzzle.Data;
using JewelsHexaPuzzle.Managers;

// ============================================================================
// SkillTreeUI.cs - 스킬 트리 페이지 UI
// ============================================================================
// 로비에서 진입하는 별도 화면. 육각형 노드 + 연결 라인으로 스킬 트리를 표시.
// 스킬 클릭 시 상세 팝업 (설명 + 해금 버튼).
// 에디터 전용 디버그 버튼 포함.
// ============================================================================

namespace JewelsHexaPuzzle.UI
{
    /// <summary>
    /// 스킬 트리 전체 페이지 UI
    /// </summary>
    public class SkillTreeUI : MonoBehaviour
    {
        // 루트 컨테이너 (전체 화면)
        private GameObject rootContainer;
        private RectTransform rootRt;

        // 상단 자원 표시
        private Text goldText;
        private Text spText;

        // 스킬 노드 UI 참조
        private Dictionary<SkillType, GameObject> skillNodes = new Dictionary<SkillType, GameObject>();
        private Dictionary<SkillType, Image> skillNodeBgs = new Dictionary<SkillType, Image>();
        private Dictionary<SkillType, Image> skillNodeBorders = new Dictionary<SkillType, Image>();
        private Dictionary<SkillType, Text> skillNodeTexts = new Dictionary<SkillType, Text>();

        // 자물쇠 아이콘 (잠긴/열린 두 버전)
        private Dictionary<SkillType, GameObject> lockedLockIcons = new Dictionary<SkillType, GameObject>();
        private Dictionary<SkillType, GameObject> openLockIcons = new Dictionary<SkillType, GameObject>();

        // 연결 라인
        private List<GameObject> connectionLines = new List<GameObject>();

        // 상세 팝업
        private GameObject detailPopup;
        private SkillType selectedSkill = SkillType.None;

        // 캐시
        private Font font;
        private Canvas parentCanvas;

        // 실제 스킬 초기화 버튼 (좌측 하단) — 쿨다운 적용
        private Button realResetButton;
        private Text realResetButtonText;
        private Text realResetStatusText;
        private Image realResetButtonBg;
        private Coroutine resetStatusRefreshCo;

        // 레이아웃 상수
        private const float NODE_SIZE = 165f;       // 110 × 1.5
        private const float NODE_SPACING_X = 190f;  // 노드 간격 25px (165 + 25)
        private const float LINE_THICKNESS = 6f;    // 4 × 1.5

        // ============================================================
        // 초기화
        // ============================================================

        /// <summary>
        /// 스킬 트리 UI 초기화 (Canvas 하위에 생성)
        /// </summary>
        public void Initialize(Canvas canvas)
        {
            parentCanvas = canvas;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            CreateSkillTreePage();
            Hide(); // 초기 상태: 숨김
        }

        // ============================================================
        // 표시/숨김
        // ============================================================

        public void Show()
        {
            if (rootContainer != null)
            {
                rootContainer.SetActive(true);
                rootContainer.transform.SetAsLastSibling();
                RefreshAllNodes();
                RefreshResourceDisplay();

                // 실제 스킬 초기화 버튼/잔여시간 표시 갱신 (1초 주기)
                RefreshRealResetButton();
                if (resetStatusRefreshCo != null) StopCoroutine(resetStatusRefreshCo);
                resetStatusRefreshCo = StartCoroutine(ResetStatusRefreshLoop());
            }

            // ★ 그리드 입력 완전 차단 — InputSystem.Update 최상단 isEnabled 게이트로
            //   스킬 노드 탭이 뒤의 블록 회전으로 전달되는 것을 원천 차단.
            //   (GameManager.IsSkillTreeVisible 폴링만으로는 막히지 않는 케이스 대응)
            var input = Object.FindObjectOfType<JewelsHexaPuzzle.Core.InputSystem>();
            if (input != null) input.SetEnabled(false);
        }

        public void Hide()
        {
            if (rootContainer != null)
                rootContainer.SetActive(false);
            if (detailPopup != null)
                detailPopup.SetActive(false);

            // ★ 스킬트리 닫힘 → 그리드 입력 복원 (게임 중일 때만)
            var input = Object.FindObjectOfType<JewelsHexaPuzzle.Core.InputSystem>();
            if (input != null
                && JewelsHexaPuzzle.Managers.GameManager.Instance != null
                && JewelsHexaPuzzle.Managers.GameManager.Instance.CurrentState == JewelsHexaPuzzle.Managers.GameState.Playing)
                input.SetEnabled(true);

            if (resetStatusRefreshCo != null)
            {
                StopCoroutine(resetStatusRefreshCo);
                resetStatusRefreshCo = null;
            }

            // ★ 닫은 시각 기록 (3시간 쿨다운용) — 로비 스킬트리 버튼 빨간 점 인디케이터에서 사용
            //   ISO 8601 round-trip 형식으로 UTC 저장 → 어떤 로케일에서도 안전하게 파싱
            PlayerPrefs.SetString("SkillTree_LastClosedUtc",
                System.DateTime.UtcNow.ToString("o", System.Globalization.CultureInfo.InvariantCulture));
            PlayerPrefs.Save();

            // 로비 인디케이터 즉시 갱신 (닫자마자 점 사라지도록)
            if (JewelsHexaPuzzle.Managers.GameManager.Instance != null)
                JewelsHexaPuzzle.Managers.GameManager.Instance.UpdateSkillTreeRedDot();
        }

        public bool IsVisible => rootContainer != null && rootContainer.activeSelf;

        // ============================================================
        // 페이지 생성
        // ============================================================

        private void CreateSkillTreePage()
        {
            // === 루트 컨테이너 (전체 화면) ===
            rootContainer = new GameObject("SkillTreePage");
            rootContainer.transform.SetParent(parentCanvas.transform, false);
            rootRt = rootContainer.AddComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            // 배경 (어두운 보라 — 완전 불투명, 로비 화면 가림)
            Image bg = rootContainer.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.03f, 0.10f, 1.0f);
            bg.raycastTarget = true;

            // === 타이틀 ===
            CreateTitle();

            // === 자원 표시 (골드 + SP) ===
            CreateResourceDisplay();

            // === 스킬 노드 영역 ===
            CreateSkillNodes();

            // === 나가기 버튼 (우측 하단) ===
            CreateExitButton();

            // === 실제 스킬 초기화 버튼 (좌측 하단, 쿨다운 적용) ===
            CreateRealResetButton();

            // === 에디터 전용 디버그 버튼 (하단 중앙 정렬) ===
#if UNITY_EDITOR
            CreateDebugButtons();
#endif

            // === 상세 팝업 (초기 숨김) ===
            CreateDetailPopup();
        }

        // ============================================================
        // 타이틀
        // ============================================================

        private void CreateTitle()
        {
            GameObject titleObj = new GameObject("SkillTreeTitle");
            titleObj.transform.SetParent(rootContainer.transform, false);
            RectTransform rt = titleObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -40f);
            rt.sizeDelta = new Vector2(400f, 60f);

            Text text = titleObj.AddComponent<Text>();
            text.font = font;
            text.fontSize = 36;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(0.9f, 0.85f, 1f);
            text.raycastTarget = false;
            text.text = "스킬 트리";

            Outline outline = titleObj.AddComponent<Outline>();
            outline.effectColor = new Color(0.3f, 0.1f, 0.5f, 0.8f);
            outline.effectDistance = new Vector2(2f, 2f);
        }

        // ============================================================
        // 자원 표시 (골드 + 스킬 포인트)
        // ============================================================

        private void CreateResourceDisplay()
        {
            // === 골드 (우측 상단) ===
            GameObject goldObj = new GameObject("SkillTreeGold");
            goldObj.transform.SetParent(rootContainer.transform, false);
            RectTransform goldRt = goldObj.AddComponent<RectTransform>();
            goldRt.anchorMin = new Vector2(1f, 1f);
            goldRt.anchorMax = new Vector2(1f, 1f);
            goldRt.pivot = new Vector2(1f, 1f);
            goldRt.anchoredPosition = new Vector2(-30f, -20f);
            goldRt.sizeDelta = new Vector2(120f, 40f);

            goldText = goldObj.AddComponent<Text>();
            goldText.font = font;
            goldText.fontSize = 26;
            goldText.alignment = TextAnchor.MiddleRight;
            goldText.color = new Color(1f, 0.84f, 0f);
            goldText.raycastTarget = false;
            goldText.resizeTextForBestFit = true;
            goldText.resizeTextMinSize = 14;
            goldText.resizeTextMaxSize = 26;

            Outline goldOutline = goldObj.AddComponent<Outline>();
            goldOutline.effectColor = new Color(0f, 0f, 0f, 0.7f);
            goldOutline.effectDistance = new Vector2(1, 1);

            // 골드 라벨
            GameObject goldLabelObj = new GameObject("GoldLabel");
            goldLabelObj.transform.SetParent(rootContainer.transform, false);
            RectTransform goldLabelRt = goldLabelObj.AddComponent<RectTransform>();
            goldLabelRt.anchorMin = new Vector2(1f, 1f);
            goldLabelRt.anchorMax = new Vector2(1f, 1f);
            goldLabelRt.pivot = new Vector2(1f, 1f);
            goldLabelRt.anchoredPosition = new Vector2(-30f, -58f);
            goldLabelRt.sizeDelta = new Vector2(120f, 20f);
            Text goldLabel = goldLabelObj.AddComponent<Text>();
            goldLabel.font = font;
            goldLabel.fontSize = 14;
            goldLabel.alignment = TextAnchor.MiddleRight;
            goldLabel.color = new Color(0.8f, 0.8f, 0.8f, 0.8f);
            goldLabel.raycastTarget = false;
            goldLabel.text = "GOLD";

            // === 스킬 포인트 (골드 왼쪽) ===
            GameObject spObj = new GameObject("SkillTreeSP");
            spObj.transform.SetParent(rootContainer.transform, false);
            RectTransform spRt = spObj.AddComponent<RectTransform>();
            spRt.anchorMin = new Vector2(1f, 1f);
            spRt.anchorMax = new Vector2(1f, 1f);
            spRt.pivot = new Vector2(1f, 1f);
            spRt.anchoredPosition = new Vector2(-170f, -20f);
            spRt.sizeDelta = new Vector2(100f, 40f);

            spText = spObj.AddComponent<Text>();
            spText.font = font;
            spText.fontSize = 26;
            spText.alignment = TextAnchor.MiddleRight;
            spText.color = new Color(0.5f, 0.9f, 1f);
            spText.raycastTarget = false;
            spText.resizeTextForBestFit = true;
            spText.resizeTextMinSize = 14;
            spText.resizeTextMaxSize = 26;

            Outline spOutline = spObj.AddComponent<Outline>();
            spOutline.effectColor = new Color(0f, 0f, 0f, 0.7f);
            spOutline.effectDistance = new Vector2(1, 1);

            // SP 라벨
            GameObject spLabelObj = new GameObject("SPLabel");
            spLabelObj.transform.SetParent(rootContainer.transform, false);
            RectTransform spLabelRt = spLabelObj.AddComponent<RectTransform>();
            spLabelRt.anchorMin = new Vector2(1f, 1f);
            spLabelRt.anchorMax = new Vector2(1f, 1f);
            spLabelRt.pivot = new Vector2(1f, 1f);
            spLabelRt.anchoredPosition = new Vector2(-170f, -58f);
            spLabelRt.sizeDelta = new Vector2(100f, 20f);
            Text spLabel = spLabelObj.AddComponent<Text>();
            spLabel.font = font;
            spLabel.fontSize = 14;
            spLabel.alignment = TextAnchor.MiddleRight;
            spLabel.color = new Color(0.7f, 0.9f, 1f, 0.8f);
            spLabel.raycastTarget = false;
            spLabel.text = "SKILL PT";
        }

        private void RefreshResourceDisplay()
        {
            if (goldText != null && GameManager.Instance != null)
                goldText.text = GameManager.Instance.CurrentGold.ToString();

            if (spText != null && SkillTreeManager.Instance != null)
                spText.text = SkillTreeManager.Instance.SkillPoints.ToString();
        }

        // ============================================================
        // 스킬 노드 생성
        // ============================================================

        private void CreateSkillNodes()
        {
            // ★ ScrollRect 기반 스크롤 뷰 — 스킬이 늘어나도 스크롤 가능
            // 구조: rootContainer > ScrollView(ScrollRect+Mask) > Content(nodesContainer)

            // 1. ScrollView 오브젝트 (뷰포트 역할)
            GameObject scrollViewObj = new GameObject("SkillScrollView");
            scrollViewObj.transform.SetParent(rootContainer.transform, false);
            RectTransform scrollViewRt = scrollViewObj.AddComponent<RectTransform>();
            scrollViewRt.anchorMin = new Vector2(0f, 0f);
            scrollViewRt.anchorMax = new Vector2(1f, 1f);
            // 상단 80px(타이틀/SP/골드) + 하단 150px 가려서 스크롤 영역 제한
            scrollViewRt.offsetMin = new Vector2(0f, 150f);  // ★ 최하단 150px 가림
            scrollViewRt.offsetMax = new Vector2(0f, -100f);

            // Mask + Image (스크롤 영역 클리핑)
            Image scrollBg = scrollViewObj.AddComponent<Image>();
            scrollBg.color = new Color(0f, 0f, 0f, 0.01f); // 거의 투명 (Mask에 Image 필요)
            scrollBg.raycastTarget = true;
            UnityEngine.UI.Mask mask = scrollViewObj.AddComponent<UnityEngine.UI.Mask>();
            mask.showMaskGraphic = false;

            // ScrollRect 컴포넌트
            UnityEngine.UI.ScrollRect scrollRect = scrollViewObj.AddComponent<UnityEngine.UI.ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = UnityEngine.UI.ScrollRect.MovementType.Elastic;
            scrollRect.elasticity = 0.1f;
            scrollRect.scrollSensitivity = 30f;

            // 2. Content (노드 컨테이너)
            GameObject nodesContainer = new GameObject("SkillNodesContainer");
            nodesContainer.transform.SetParent(scrollViewObj.transform, false);
            RectTransform containerRt = nodesContainer.AddComponent<RectTransform>();
            // Content는 상단 앵커 + 위에서 아래로 확장
            containerRt.anchorMin = new Vector2(0.5f, 1f);
            containerRt.anchorMax = new Vector2(0.5f, 1f);
            containerRt.pivot = new Vector2(0.5f, 1f);

            // ScrollRect에 Content 연결
            scrollRect.content = containerRt;
            scrollRect.viewport = scrollViewRt;

            // ★ Y좌표: 같은 특수 블록끼리 그룹핑
            // 그룹 내 간격 120px, 그룹 간 간격 180px
            float startX = -(NODE_SPACING_X) + 110f; // ★ 왼쪽 50px 이동 (160-50)
            float groupGap = 270f;   // 180 × 1.5
            float chainGap = 180f;   // 120 × 1.5

            // ── 드릴 그룹 ──
            float drillY = 1680f;  // ★ 관통 체인 추가로 상단 확장
            float drillDmgY = drillY - chainGap;
            float drillCushionY = drillDmgY - chainGap;
            float drillPenetrateY = drillCushionY - chainGap; // 관통 체인

            // ── 폭탄 그룹 ── (드릴 그룹 아래 180px 간격)
            //   ★ 폭탄 강화(Damage)와 넉백(Knockback) 행 순서 교체 — Damage가 Knockback 위로
            float bombMoveY = drillPenetrateY - groupGap;    // 180
            float bombDmgY = bombMoveY - chainGap;           // 60  (구 bombKnockY 위치)
            float bombKnockY = bombDmgY - chainGap;          // -60 (구 bombDmgY 위치)
            float chainBombY = bombKnockY - chainGap;        // -180

            // ── 드론 그룹 ──
            float droneDmgY = chainBombY - groupGap;       // -360

            // ── 타겟 그룹 ──
            float targetDmgY = droneDmgY - groupGap;       // -540

            // ── 아이템 그룹 ──
            float hammerY = targetDmgY - groupGap;           // -720
            float swapY = hammerY - chainGap;                // -660
            float lineY = swapY - chainGap;                  // -780

            // Content 높이 — 최하단 스킬이 화면 1/3 지점까지 올라오도록 여백 추가
            // ★ 스크롤 범위: 상단/하단 각 뷰포트 10%까지만 스크롤
            float viewportHeight = Screen.height * 0.85f;
            float marginPercent = 0.10f;
            float topPadding = 300f; // ★ 상단 300px 여백 — 최상단 스킬이 여기서 더 안 내려옴
            float bottomPadding = viewportHeight * (1f - marginPercent); // 하단 90% 여백 → 최하단이 하단 10% 위치까지만
            float topY = drillY + topPadding;
            float bottomY = lineY - bottomPadding;
            float contentHeight = topY - bottomY;
            containerRt.sizeDelta = new Vector2(1200f, contentHeight);
            containerRt.anchoredPosition = new Vector2(0f, 0f);

            // 그룹 라벨 X 위치 (첫 노드 왼쪽)
            float groupLabelX = startX - NODE_SIZE * 0.5f - 120f;

            // ── 드릴 그룹 ──
            float[] drillYs = { drillY, drillDmgY, drillCushionY, drillPenetrateY };
            CreateSkillChain(nodesContainer.transform, SkillTreeDefinition.GetDrillSkills(),
                startX, drillY, "", Color.clear);
            CreateSkillChain(nodesContainer.transform, SkillTreeDefinition.GetDrillDamageSkills(),
                startX, drillDmgY, "", Color.clear);
            CreateSkillChain(nodesContainer.transform, SkillTreeDefinition.GetDrillCushionSkills(),
                startX, drillCushionY, "", Color.clear);
            CreateSkillChain(nodesContainer.transform, SkillTreeDefinition.GetDrillPenetrateSkills(),
                startX, drillPenetrateY, "", Color.clear);
            CreateGroupLabel(nodesContainer.transform, "드릴", new Color(0.5f, 0.8f, 1f),
                groupLabelX, drillYs, startX);

            // ── 폭탄 그룹 ──
            float[] bombYs = { bombMoveY, bombKnockY, bombDmgY, chainBombY };
            CreateSkillChain(nodesContainer.transform, SkillTreeDefinition.GetBombSkills(),
                startX, bombMoveY, "", Color.clear);
            CreateSkillChain(nodesContainer.transform, SkillTreeDefinition.GetBombKnockbackSkills(),
                startX, bombKnockY, "", Color.clear);
            CreateSkillChain(nodesContainer.transform, SkillTreeDefinition.GetBombDamageSkills(),
                startX, bombDmgY, "", Color.clear);
            CreateSkillChain(nodesContainer.transform, SkillTreeDefinition.GetChainBombSkills(),
                startX, chainBombY, "", Color.clear);
            CreateGroupLabel(nodesContainer.transform, "폭탄", new Color(1f, 0.55f, 0.2f),
                groupLabelX, bombYs, startX);

            // ── 드론 그룹 ──
            float[] droneYs = { droneDmgY };
            CreateSkillChain(nodesContainer.transform, SkillTreeDefinition.GetDroneTargetDamageSkills(),
                startX, droneDmgY, "", Color.clear);
            CreateGroupLabel(nodesContainer.transform, "드론", new Color(0.3f, 0.7f, 0.95f),
                groupLabelX, droneYs, startX);

            // ── 타겟 그룹 ──
            float[] targetYs = { targetDmgY };
            CreateSkillChain(nodesContainer.transform, SkillTreeDefinition.GetTargetDamageSkills(),
                startX, targetDmgY, "", Color.clear);
            CreateGroupLabel(nodesContainer.transform, "타겟", new Color(0.9f, 0.75f, 0.2f),
                groupLabelX, targetYs, startX);

            // ── 아이템 그룹: 개별 그룹 라벨 ──
            CreateSkillChain(nodesContainer.transform, SkillTreeDefinition.GetHammerSkills(),
                startX, hammerY, "", Color.clear);
            CreateGroupLabel(nodesContainer.transform, "망치", new Color(0.9f, 0.25f, 0.25f),
                groupLabelX, new[] { hammerY }, startX);

            CreateSkillChain(nodesContainer.transform, SkillTreeDefinition.GetSwapSkills(),
                startX, swapY, "", Color.clear);
            CreateGroupLabel(nodesContainer.transform, "스왑", new Color(0.25f, 0.85f, 0.35f),
                groupLabelX, new[] { swapY }, startX);

            CreateSkillChain(nodesContainer.transform, SkillTreeDefinition.GetLineSkills(),
                startX, lineY, "", Color.clear);
            CreateGroupLabel(nodesContainer.transform, "라인", new Color(0.6f, 0.25f, 0.9f),
                groupLabelX, new[] { lineY }, startX);
        }

        /// <summary>
        /// 그룹 라벨: 육각형 도형 + 중앙 텍스트 + 각 체인 첫 노드와 연결선.
        /// chainYs: 이 그룹에 속한 체인들의 Y좌표 배열.
        /// </summary>
        private void CreateGroupLabel(Transform parent, string title, Color color,
            float labelX, float[] chainYs, float chainStartX)
        {
            // ★ 그룹 타이틀 20px 왼쪽 이동
            float actualLabelX = labelX - 70f; // ★ 추가 50px 왼쪽 이동 (기존 -20 + -50)

            // 그룹 Y 중앙 계산
            float minY = chainYs[0], maxY = chainYs[0];
            foreach (float cy in chainYs) { if (cy < minY) minY = cy; if (cy > maxY) maxY = cy; }
            float centerY = (minY + maxY) / 2f;

            // 육각형 배경
            GameObject labelObj = new GameObject($"GroupLabel_{title}");
            labelObj.transform.SetParent(parent, false);
            RectTransform labelRt = labelObj.AddComponent<RectTransform>();
            labelRt.anchoredPosition = new Vector2(actualLabelX, centerY);
            labelRt.sizeDelta = new Vector2(NODE_SIZE, NODE_SIZE);

            Image hexBg = labelObj.AddComponent<Image>();
            hexBg.sprite = HexBlock.GetHexFlashSprite();
            hexBg.type = Image.Type.Simple;
            hexBg.preserveAspect = true;
            hexBg.color = new Color(color.r * 0.4f, color.g * 0.4f, color.b * 0.4f, 0.85f);
            hexBg.raycastTarget = false;

            // 육각형 테두리
            GameObject borderObj = new GameObject("HexBorder");
            borderObj.transform.SetParent(labelObj.transform, false);
            RectTransform borderRt = borderObj.AddComponent<RectTransform>();
            borderRt.anchorMin = Vector2.zero;
            borderRt.anchorMax = Vector2.one;
            borderRt.offsetMin = Vector2.zero;
            borderRt.offsetMax = Vector2.zero;
            Image borderImg = borderObj.AddComponent<Image>();
            borderImg.sprite = HexBlock.GetHexBorderSprite();
            borderImg.type = Image.Type.Simple;
            borderImg.preserveAspect = true;
            borderImg.color = color;
            borderImg.raycastTarget = false;

            // 중앙 텍스트
            GameObject txtObj = new GameObject("GroupTitle");
            txtObj.transform.SetParent(labelObj.transform, false);
            RectTransform txtRt = txtObj.AddComponent<RectTransform>();
            txtRt.anchoredPosition = Vector2.zero;
            txtRt.sizeDelta = new Vector2(NODE_SIZE, NODE_SIZE);
            Text txt = txtObj.AddComponent<Text>();
            txt.font = font;
            txt.fontSize = 24;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
            txt.raycastTarget = false;
            txt.text = title;

            // ★ 연결선: 타이틀 오른쪽 꼭지점 → 각 스킬 첫 노드 왼쪽 꼭지점 (직선, 축소 없음)
            float labelRightX = actualLabelX + NODE_SIZE * 0.5f;
            float nodeLeftX = chainStartX - NODE_SIZE * 0.5f;

            foreach (float cy in chainYs)
            {
                CreateExactLine(parent, labelRightX, centerY, nodeLeftX, cy);
            }
        }

        /// <summary>
        /// 스킬 체인 한 줄 생성 (노드 + 연결 라인 + 자물쇠 + 카테고리 라벨)
        /// </summary>
        private void CreateSkillChain(Transform parent, List<SkillNodeData> skills,
            float startX, float y, string labelText, Color labelColor)
        {
            if (skills == null || skills.Count == 0) return;

            // ★ 개별 카테고리 라벨 제거됨 — 그룹 라벨(육각형)이 CreateGroupLabel에서 생성

            // 이 체인의 연결 라인 시작 인덱스 기억
            int lineStartIdx = connectionLines.Count;

            for (int i = 0; i < skills.Count; i++)
            {
                var skill = skills[i];
                float x = startX + i * NODE_SPACING_X;

                if (i > 0)
                {
                    float prevX = startX + (i - 1) * NODE_SPACING_X;
                    CreateConnectionLine(parent, prevX, y, x, y);
                }

                CreateSkillNode(parent, skill, new Vector2(x, y));
            }

            // 자물쇠 아이콘
            for (int i = 0; i < skills.Count; i++)
            {
                var skill = skills[i];
                Vector2 lockPos;

                int lineIdx = lineStartIdx + (i - 1);
                if (i > 0 && lineIdx >= 0 && lineIdx < connectionLines.Count)
                {
                    RectTransform lineRt = connectionLines[lineIdx].GetComponent<RectTransform>();
                    float lineRightX = lineRt.anchoredPosition.x + lineRt.sizeDelta.x * 0.5f;
                    float lineY = lineRt.anchoredPosition.y;
                    lockPos = new Vector2(lineRightX + 25f, lineY); // 17×1.5
                }
                else
                {
                    // ★ 1레벨 자물쇠: 육각형 안쪽 중앙에서 약간 왼쪽 (2,3레벨과 동일 정도)
                    float nodeX = startX;
                    lockPos = new Vector2(nodeX - 42f, y); // 28×1.5
                }

                CreateLockIconPair(parent, skill.skillType, lockPos);
            }
        }

        private void CreateSkillNode(Transform parent, SkillNodeData skillData, Vector2 position)
        {
            // === 노드 루트 ===
            GameObject nodeObj = new GameObject($"SkillNode_{skillData.skillType}");
            nodeObj.transform.SetParent(parent, false);
            RectTransform nodeRt = nodeObj.AddComponent<RectTransform>();
            nodeRt.anchoredPosition = position;
            nodeRt.sizeDelta = new Vector2(NODE_SIZE, NODE_SIZE);

            // === 육각형 배경 ===
            Image hexBg = nodeObj.AddComponent<Image>();
            hexBg.sprite = HexBlock.GetHexFlashSprite();
            hexBg.type = Image.Type.Simple;
            hexBg.preserveAspect = true;
            hexBg.color = skillData.nodeColor;

            // === 육각형 테두리 ===
            GameObject borderObj = new GameObject("HexBorder");
            borderObj.transform.SetParent(nodeObj.transform, false);
            RectTransform borderRt = borderObj.AddComponent<RectTransform>();
            borderRt.anchorMin = Vector2.zero;
            borderRt.anchorMax = Vector2.one;
            borderRt.offsetMin = Vector2.zero;
            borderRt.offsetMax = Vector2.zero;
            Image borderImg = borderObj.AddComponent<Image>();
            borderImg.sprite = HexBlock.GetHexBorderSprite();
            borderImg.type = Image.Type.Simple;
            borderImg.preserveAspect = true;
            borderImg.color = Color.white;
            borderImg.raycastTarget = false;

            // === 아이콘 (스킬 타입별 프로시저럴) ===
            int typeVal = (int)skillData.skillType;
            bool isMove = (typeVal >= 100 && typeVal <= 199) || (typeVal >= 200 && typeVal <= 299);
            bool isPowerUp = (typeVal >= 400 && typeVal <= 499)  // 폭탄 강화
                          || (typeVal >= 500 && typeVal <= 549)  // 드릴 강화
                          || (typeVal >= 600 && typeVal <= 699)  // 드론 강화
                          || (typeVal >= 1100 && typeVal <= 1199); // 타겟 강화

            // ★ 아이콘 컨테이너 (전체 5px 아래로 이동)
            GameObject iconContainer = new GameObject("IconContainer");
            iconContainer.transform.SetParent(nodeObj.transform, false);
            RectTransform iconContRt = iconContainer.AddComponent<RectTransform>();
            iconContRt.anchoredPosition = new Vector2(0f, -5f);
            iconContRt.sizeDelta = new Vector2(NODE_SIZE, NODE_SIZE);

            if (isMove)
                CreateMoveIcon(iconContainer.transform);
            else if (typeVal >= 1200 && typeVal <= 1299)
                CreatePenetrateIcon(iconContainer.transform, skillData); // 드릴 관통
            else if (typeVal >= 1000 && typeVal <= 1099)
                CreateChainBombIcon(iconContainer.transform, skillData); // 연쇄폭탄
            else if (typeVal >= 550 && typeVal <= 599)
                CreateCushionIcon(iconContainer.transform, skillData);
            else if (typeVal >= 700 && typeVal <= 799)
                CreateHammerPowerIcon(iconContainer.transform, skillData); // 망치 파워
            else if (isPowerUp)
                CreateFistIcon(iconContainer.transform);
            else if (typeVal >= 300 && typeVal <= 399)
                CreateKnockbackIcon(iconContainer.transform);
            else if (typeVal >= 800 && typeVal <= 899)
                CreateSwapStepIcon(iconContainer.transform, skillData); // 스왑 디딤
            else if (typeVal >= 900 && typeVal <= 999)
                CreateLineConnectIcon(iconContainer.transform, skillData); // 라인 연결
            else
                CreateDrillIcon(iconContainer.transform, skillData);

            // === 스킬 레벨 타이틀 (육각형 안쪽 중앙 상단) ===
            // 스킬 이름에서 로마숫자 레벨만 추출 표시
            GameObject nameObj = new GameObject("SkillName");
            nameObj.transform.SetParent(nodeObj.transform, false);
            RectTransform nameRt = nameObj.AddComponent<RectTransform>();
            nameRt.anchoredPosition = new Vector2(0f, NODE_SIZE * 0.25f); // 프레임 안쪽 상단
            nameRt.sizeDelta = new Vector2(150f, 36f); // 100×1.5, 24×1.5
            Text nameText = nameObj.AddComponent<Text>();
            nameText.font = font;
            nameText.fontSize = 21; // 14 × 1.5
            nameText.fontStyle = FontStyle.Bold;
            nameText.alignment = TextAnchor.MiddleCenter;
            nameText.color = new Color(1f, 1f, 1f, 0.95f);
            nameText.raycastTarget = false;
            nameText.text = GetShortSkillLabel(skillData);

            // === 비용 텍스트 (육각형 프레임 바로 아래) ===
            GameObject costObj = new GameObject("SkillCost");
            costObj.transform.SetParent(nodeObj.transform, false);
            RectTransform costRt = costObj.AddComponent<RectTransform>();
            costRt.anchoredPosition = new Vector2(0f, -NODE_SIZE * 0.5f - 9f); // 6×1.5
            costRt.sizeDelta = new Vector2(240f, 33f); // 160×1.5, 22×1.5
            Text costText = costObj.AddComponent<Text>();
            costText.font = font;
            costText.fontSize = 18; // 12 × 1.5
            costText.alignment = TextAnchor.MiddleCenter;
            costText.color = new Color(0.7f, 0.7f, 0.7f, 0.85f);
            costText.raycastTarget = false;
            costText.text = $"SP:{skillData.skillPointCost}  Gold:{skillData.goldCost}";

            // === 버튼 ===
            Button btn = nodeObj.AddComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = new Color(1f, 0.95f, 0.85f);
            colors.pressedColor = new Color(0.7f, 0.7f, 0.8f);
            btn.colors = colors;

            SkillType capturedType = skillData.skillType;
            btn.onClick.AddListener(() =>
            {
                if (AudioManager.Instance != null) AudioManager.Instance.PlayButtonClick();
                ShowDetailPopup(capturedType);
            });

            // 참조 저장
            skillNodes[skillData.skillType] = nodeObj;
            skillNodeBgs[skillData.skillType] = hexBg;
            skillNodeBorders[skillData.skillType] = borderImg;
            skillNodeTexts[skillData.skillType] = nameText;
        }

        /// <summary>
        /// 스킬 이름에서 특수블록명을 제거하고 로마숫자 레벨만 표시.
        /// "드릴 이동 I" → "이동 Ⅰ", "폭탄 Lv.2" → "Lv.Ⅱ" 등 통일.
        /// </summary>
        private string GetShortSkillLabel(SkillNodeData data)
        {
            // skillName에서 레벨 번호 추출 (마지막 문자 기준)
            string name = data.skillName;
            int level = 0;

            // "III" / "3" / "Lv.3" 등에서 레벨 추출
            if (name.EndsWith("III") || name.EndsWith("3") || name.EndsWith("Ⅲ")) level = 3;
            else if (name.EndsWith("II") || name.EndsWith("2") || name.EndsWith("Ⅱ")) level = 2;
            else if (name.EndsWith("I") || name.EndsWith("1") || name.EndsWith("Ⅰ")) level = 1;

            // 레벨 텍스트를 제거하고 핵심 이름만 추출
            string coreName = name;
            // 특수블록 접두사 제거
            string[] prefixes = { "드릴 ", "연쇄폭탄 ", "폭탄 ", "드론 ", "타겟 ", "망치 ", "스왑 ", "라인 " };
            foreach (var p in prefixes)
            {
                if (coreName.StartsWith(p))
                {
                    coreName = coreName.Substring(p.Length);
                    break;
                }
            }
            // 레벨 접미사 + 보너스 수치 제거
            string[] suffixes = {
                " III", " II", " I", " Lv.3", " Lv.2", " Lv.1", " 3", " 2", " 1",
                " +3", " +2", " +1", "+3", "+2", "+1",
                "3", "2", "1"  // 공백 없는 아라비아 숫자 (강화1, 강화2 등)
            };
            foreach (var s in suffixes)
            {
                if (coreName.EndsWith(s))
                {
                    coreName = coreName.Substring(0, coreName.Length - s.Length);
                    break;
                }
            }

            string romanLevel = level == 3 ? "Ⅲ" : level == 2 ? "Ⅱ" : "Ⅰ";
            return coreName.Trim().Length > 0 ? $"{coreName.Trim()} {romanLevel}" : romanLevel;
        }

        /// <summary>
        /// 자물쇠 아이콘 한 쌍 생성 (잠긴 + 열린)
        /// 부모는 nodesContainer (노드 바깥 독립 배치)
        /// lockPos = 연결 라인 오른쪽 끝점 + 8px 위치
        /// </summary>
        private void CreateLockIconPair(Transform parent, SkillType skillType, Vector2 lockPos)
        {
            // 공통 크기
            Vector2 iconSize = new Vector2(42f, 51f); // 28×1.5, 34×1.5

            // ── 잠긴 자물쇠 (Locked) ── 흰색 α0.7, 닫힌 고리
            Color lockedColor = new Color(1f, 1f, 1f, 0.7f);
            GameObject lockedRoot = CreateSingleLockIcon(parent, "LockedLock", lockPos, iconSize, lockedColor, false);
            lockedLockIcons[skillType] = lockedRoot;

            // ── 열린 자물쇠 (Available/Unlocked) ── 초록 α0.7, 열린 고리
            Color openColor = new Color(0.3f, 0.9f, 0.4f, 0.7f);
            GameObject openRoot = CreateSingleLockIcon(parent, "OpenLock", lockPos, iconSize, openColor, true);
            openLockIcons[skillType] = openRoot;
        }

        /// <summary>
        /// 단일 자물쇠 아이콘 생성 (몸통 + 열쇠구멍 + 고리)
        /// isOpen=true면 오른쪽 고리가 위로 들림
        /// </summary>
        private GameObject CreateSingleLockIcon(Transform parent, string name, Vector2 pos, Vector2 size, Color color, bool isOpen)
        {
            // 루트
            GameObject root = new GameObject(name);
            root.transform.SetParent(parent, false);
            RectTransform rt = root.AddComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            // 몸통 (사각형)
            GameObject body = new GameObject("Body");
            body.transform.SetParent(root.transform, false);
            RectTransform bodyRt = body.AddComponent<RectTransform>();
            bodyRt.anchoredPosition = new Vector2(0f, -4f);
            bodyRt.sizeDelta = new Vector2(20f, 16f);
            Image bodyImg = body.AddComponent<Image>();
            bodyImg.color = color;
            bodyImg.raycastTarget = false;

            // 열쇠구멍 (원)
            Color holeColor = new Color(0.15f, 0.13f, 0.2f, 0.9f);
            GameObject keyhole = new GameObject("Keyhole");
            keyhole.transform.SetParent(body.transform, false);
            RectTransform khRt = keyhole.AddComponent<RectTransform>();
            khRt.anchoredPosition = new Vector2(0f, 1.5f);
            khRt.sizeDelta = new Vector2(5f, 5f);
            Image khImg = keyhole.AddComponent<Image>();
            khImg.color = holeColor;
            khImg.raycastTarget = false;

            // 열쇠구멍 슬롯
            GameObject keySlot = new GameObject("KeySlot");
            keySlot.transform.SetParent(body.transform, false);
            RectTransform ksRt = keySlot.AddComponent<RectTransform>();
            ksRt.anchoredPosition = new Vector2(0f, -2.5f);
            ksRt.sizeDelta = new Vector2(2.5f, 5f);
            Image ksImg = keySlot.AddComponent<Image>();
            ksImg.color = holeColor;
            ksImg.raycastTarget = false;

            // 고리 (U자형)
            float shW = 14f, shH = 10f, bar = 2.5f, shY = 4f;
            float openLift = isOpen ? 6f : 0f;

            // 왼쪽 바
            GameObject leftBar = new GameObject("ShackleL");
            leftBar.transform.SetParent(root.transform, false);
            RectTransform lbRt = leftBar.AddComponent<RectTransform>();
            lbRt.anchoredPosition = new Vector2(-shW * 0.5f + bar * 0.5f, shY);
            lbRt.sizeDelta = new Vector2(bar, shH);
            Image lbImg = leftBar.AddComponent<Image>();
            lbImg.color = color;
            lbImg.raycastTarget = false;

            // 오른쪽 바 (열린 상태면 위로 들림)
            GameObject rightBar = new GameObject("ShackleR");
            rightBar.transform.SetParent(root.transform, false);
            RectTransform rbRt = rightBar.AddComponent<RectTransform>();
            rbRt.anchoredPosition = new Vector2(shW * 0.5f - bar * 0.5f, shY + openLift);
            rbRt.sizeDelta = new Vector2(bar, shH);
            Image rbImg = rightBar.AddComponent<Image>();
            rbImg.color = color;
            rbImg.raycastTarget = false;

            // 상단 바
            GameObject topBar = new GameObject("ShackleT");
            topBar.transform.SetParent(root.transform, false);
            RectTransform tbRt = topBar.AddComponent<RectTransform>();
            float topY = shY + (isOpen ? openLift : 0f) + shH * 0.5f - bar * 0.5f;
            tbRt.anchoredPosition = new Vector2(0f, topY);
            tbRt.sizeDelta = new Vector2(shW, bar);
            Image tbImg = topBar.AddComponent<Image>();
            tbImg.color = color;
            tbImg.raycastTarget = false;

            root.SetActive(false);
            return root;
        }

        /// <summary>
        /// 드릴 아이콘 프로시저럴 생성 (화살표 + 드릴 모양)
        /// </summary>
        /// <summary>이동 스킬 공용 아이콘 — 4방향 화살표 (↑↓←→) 십자 배치</summary>
        /// <summary>강화 스킬 공용 아이콘 — 주먹 모양 (프로시저럴)</summary>
        /// <summary>넉백 스킬 아이콘 — 중앙 충격에서 3방향으로 밀려나는 화살표</summary>
        /// <summary>망치 파워 아이콘 — 망치 + 레벨별 충격 범위 링</summary>
        private void CreateHammerPowerIcon(Transform parent, SkillNodeData skillData)
        {
            int level = Mathf.Clamp(skillData.drillMoveRange, 1, 3);
            Color hammerColor = new Color(1f, 1f, 1f, 0.9f);
            Color handleColor = new Color(0.7f, 0.5f, 0.3f, 0.9f);

            // 망치 머리 (가로 직사각형)
            GameObject head = new GameObject("HammerHead");
            head.transform.SetParent(parent, false);
            Image headImg = head.AddComponent<Image>();
            headImg.color = hammerColor;
            headImg.raycastTarget = false;
            RectTransform headRt = head.GetComponent<RectTransform>();
            headRt.anchoredPosition = new Vector2(0f, 8f);
            headRt.sizeDelta = new Vector2(28f, 14f);

            // 망치 손잡이 (세로 직사각형)
            GameObject handle = new GameObject("HammerHandle");
            handle.transform.SetParent(parent, false);
            Image handleImg = handle.AddComponent<Image>();
            handleImg.color = handleColor;
            handleImg.raycastTarget = false;
            RectTransform handleRt = handle.GetComponent<RectTransform>();
            handleRt.anchoredPosition = new Vector2(0f, -10f);
            handleRt.sizeDelta = new Vector2(6f, 22f);

            // 충격 범위 링 (레벨별 크기)
            float[] ringSizes = { 36f, 48f, 60f };
            float[] ringAlphas = { 0.4f, 0.3f, 0.25f };
            for (int i = 0; i < level; i++)
            {
                GameObject ring = new GameObject($"PowerRing{i}");
                ring.transform.SetParent(parent, false);
                Image ringImg = ring.AddComponent<Image>();
                ringImg.color = new Color(1f, 0.4f, 0.2f, ringAlphas[i]);
                ringImg.raycastTarget = false;
                RectTransform ringRt = ring.GetComponent<RectTransform>();
                ringRt.anchoredPosition = new Vector2(0f, 2f);
                ringRt.sizeDelta = new Vector2(ringSizes[i], ringSizes[i]);
            }

            // 충격 이펙트 (짧은 방사선 4개)
            for (int i = 0; i < 4; i++)
            {
                float angle = 45f + i * 90f;
                float rad = angle * Mathf.Deg2Rad;
                float dist = 14f + level * 4f;
                CreateIconLine(parent,
                    Mathf.Cos(rad) * 10f, Mathf.Sin(rad) * 10f + 2f,
                    Mathf.Cos(rad) * dist, Mathf.Sin(rad) * dist + 2f,
                    new Color(1f, 0.7f, 0.3f, 0.7f));
            }
        }

        /// <summary>스왑 디딤 아이콘 — 디딤돌이 레벨별로 멀어지는 표현</summary>
        private void CreateSwapStepIcon(Transform parent, SkillNodeData skillData)
        {
            int level = Mathf.Clamp(skillData.drillMoveRange, 1, 3);
            Color stoneColor = new Color(1f, 1f, 1f, 0.9f);
            Color lastColor = new Color(1f, 0.35f, 0.25f, 0.95f); // 마지막 = 붉은색
            Color lineColor = new Color(0.6f, 0.6f, 0.6f, 0.5f);
            float stoneSize = 12f;

            // 시작점 정사각형 (출발)
            float sx = -16f, sy = -14f;
            GameObject start = new GameObject("StepStart");
            start.transform.SetParent(parent, false);
            Image startImg = start.AddComponent<Image>();
            startImg.color = new Color(0.3f, 0.55f, 1f, 0.95f); // 파란색
            startImg.raycastTarget = false;
            RectTransform startRt = start.GetComponent<RectTransform>();
            startRt.anchoredPosition = new Vector2(sx, sy);
            startRt.sizeDelta = new Vector2(stoneSize, stoneSize);

            // 디딤돌 배치 (대각선으로 점점 멀어짐)
            // 레벨1: 시작→끝(1칸), 레벨2: 시작→중간→끝(2칸), 레벨3: 시작→①→②→끝(3칸)
            float prevX = sx, prevY = sy;
            for (int i = 0; i < level; i++)
            {
                float gap = 14f + i * 4f; // 간격 점점 넓어짐
                float cx = prevX + gap;
                float cy = prevY + gap * 0.7f;
                bool isLast = (i == level - 1);

                // 연결선
                CreateIconLine(parent, prevX, prevY, cx, cy, lineColor);

                // 디딤돌 정사각형
                GameObject stone = new GameObject($"Step{i}");
                stone.transform.SetParent(parent, false);
                Image stoneImg = stone.AddComponent<Image>();
                stoneImg.color = isLast ? lastColor : stoneColor;
                stoneImg.raycastTarget = false;
                RectTransform stoneRt = stone.GetComponent<RectTransform>();
                stoneRt.anchoredPosition = new Vector2(cx, cy);
                stoneRt.sizeDelta = new Vector2(stoneSize, stoneSize);

                prevX = cx;
                prevY = cy;
            }
        }

        /// <summary>라인 연결 아이콘 — 레벨별 연결 블록 수 증가</summary>
        private void CreateLineConnectIcon(Transform parent, SkillNodeData skillData)
        {
            int level = Mathf.Clamp(skillData.drillMoveRange, 1, 3);
            float s = 10f; // 블록 사이즈
            float gap = 14f; // 지그재그 간격
            float zig = 6f;  // 지그재그 높이
            Color lineColor = new Color(0.8f, 0.5f, 1f, 0.7f);

            Color W = new Color(1f, 1f, 1f, 0.9f);      // 흰색 (홀수)
            Color Y = new Color(1f, 0.85f, 0.2f, 0.95f); // 노란 (2번째)
            Color G = new Color(0.3f, 0.9f, 0.4f, 0.95f); // 초록 (4번째)
            Color P = new Color(0.7f, 0.4f, 1f, 0.95f);  // 보라 (6번째)

            // 레벨1: 3블록 [흰,노란,흰]
            // 레벨2: 5블록 [흰,노란,흰,초록,흰]
            // 레벨3: 7블록 [흰,노란,흰,초록,흰,보라,흰] ㄱ자 꺽임
            Color[][] colorSets = {
                new[] { W, Y, W },
                new[] { W, Y, W, G, W },
                new[] { W, Y, W, G, W, P, W }
            };

            Color[] colors = colorSets[level - 1];
            int count = colors.Length;

            // 위치 계산: 지그재그 + 레벨3은 ㄱ자 꺽임
            Vector2[] positions = new Vector2[count];

            if (level <= 2)
            {
                // 수평 지그재그 중앙 정렬
                float totalW = (count - 1) * gap;
                float startX = -totalW * 0.5f;
                for (int i = 0; i < count; i++)
                {
                    float x = startX + i * gap;
                    float y = (i % 2 == 0) ? -zig : zig;
                    positions[i] = new Vector2(x, y);
                }
            }
            else // 레벨3: ㄱ자 — 4개 수평 후 꺽어서 3개 아래로
            {
                // 상단 4개 (수평 지그재그)
                float rowStartX = -20f;
                for (int i = 0; i < 4; i++)
                {
                    positions[i] = new Vector2(rowStartX + i * gap, (i % 2 == 0) ? zig : -zig);
                }
                // 꺽는 지점에서 아래로 3개 (수직 지그재그)
                float bendX = positions[3].x;
                float bendY = positions[3].y;
                for (int i = 0; i < 3; i++)
                {
                    float x = (i % 2 == 0) ? bendX + zig : bendX - zig;
                    float y = bendY - (i + 1) * gap;
                    positions[4 + i] = new Vector2(x, y);
                }
            }

            // 연결선
            for (int i = 0; i < count - 1; i++)
                CreateIconLine(parent, positions[i].x, positions[i].y,
                    positions[i + 1].x, positions[i + 1].y, lineColor);

            // 블록 정사각형
            for (int i = 0; i < count; i++)
            {
                GameObject block = new GameObject($"ConnBlock{i}");
                block.transform.SetParent(parent, false);
                Image blockImg = block.AddComponent<Image>();
                blockImg.color = colors[i];
                blockImg.raycastTarget = false;
                RectTransform blockRt = block.GetComponent<RectTransform>();
                blockRt.anchoredPosition = positions[i];
                blockRt.sizeDelta = new Vector2(s, s);
            }
        }

        private void CreateKnockbackIcon(Transform parent)
        {
            Color iconColor = new Color(1f, 1f, 1f, 0.9f);
            Color burstColor = new Color(1f, 0.7f, 0.3f, 0.85f);

            // 중앙 폭발 원 (작은 원)
            GameObject center = new GameObject("KBCenter");
            center.transform.SetParent(parent, false);
            Image centerImg = center.AddComponent<Image>();
            centerImg.color = burstColor;
            centerImg.raycastTarget = false;
            RectTransform centerRt = center.GetComponent<RectTransform>();
            centerRt.anchoredPosition = Vector2.zero;
            centerRt.sizeDelta = new Vector2(14f, 14f);

            // 충격 링 (얇은 원)
            GameObject ring = new GameObject("KBRing");
            ring.transform.SetParent(parent, false);
            Image ringImg = ring.AddComponent<Image>();
            ringImg.color = new Color(1f, 0.8f, 0.4f, 0.4f);
            ringImg.raycastTarget = false;
            RectTransform ringRt = ring.GetComponent<RectTransform>();
            ringRt.anchoredPosition = Vector2.zero;
            ringRt.sizeDelta = new Vector2(28f, 28f);

            // 3방향 밀려나는 화살표 (↗ → ↘) — 120° 간격
            float[] angles = { 30f, 150f, 270f };
            foreach (float deg in angles)
            {
                float rad = deg * Mathf.Deg2Rad;
                float dirX = Mathf.Cos(rad);
                float dirY = Mathf.Sin(rad);

                // 밀려나는 선
                float startDist = 10f;
                float endDist = 26f;
                CreateIconLine(parent,
                    dirX * startDist, dirY * startDist,
                    dirX * endDist, dirY * endDist, iconColor);

                // 화살촉
                CreateIconArrow(parent,
                    dirX * endDist, dirY * endDist, deg, iconColor);
            }
        }

        private void CreateFistIcon(Transform parent)
        {
            Color skinColor = new Color(1f, 0.92f, 0.82f, 0.95f);
            Color darkSkin = new Color(0.85f, 0.75f, 0.65f, 0.9f);
            Color knuckleColor = new Color(0.95f, 0.85f, 0.75f, 0.95f);

            // 손바닥 (쥔 주먹 몸통)
            GameObject palm = new GameObject("FistPalm");
            palm.transform.SetParent(parent, false);
            Image palmImg = palm.AddComponent<Image>();
            palmImg.color = skinColor;
            palmImg.raycastTarget = false;
            RectTransform palmRt = palm.GetComponent<RectTransform>();
            palmRt.anchoredPosition = new Vector2(2f, -2f);
            palmRt.sizeDelta = new Vector2(32f, 24f);

            // 쥔 손가락 4개 (상단, 말린 형태 — 두꺼운 가로 블록)
            GameObject fingers = new GameObject("Fingers");
            fingers.transform.SetParent(parent, false);
            Image fingersImg = fingers.AddComponent<Image>();
            fingersImg.color = knuckleColor;
            fingersImg.raycastTarget = false;
            RectTransform fingersRt = fingers.GetComponent<RectTransform>();
            fingersRt.anchoredPosition = new Vector2(2f, 14f);
            fingersRt.sizeDelta = new Vector2(30f, 12f);

            // 손가락 마디선 3줄 (어두운 선으로 구분)
            for (int i = 0; i < 3; i++)
            {
                GameObject line = new GameObject($"FingerLine{i}");
                line.transform.SetParent(parent, false);
                Image lineImg = line.AddComponent<Image>();
                lineImg.color = new Color(0.6f, 0.5f, 0.4f, 0.5f);
                lineImg.raycastTarget = false;
                RectTransform lineRt = line.GetComponent<RectTransform>();
                lineRt.anchoredPosition = new Vector2(-5f + i * 10f, 14f);
                lineRt.sizeDelta = new Vector2(1.5f, 12f);
            }

            // 손가락 끝 (말린 부분 — 아래로 감싸는 작은 블록)
            GameObject fingerTips = new GameObject("FingerTips");
            fingerTips.transform.SetParent(parent, false);
            Image tipsImg = fingerTips.AddComponent<Image>();
            tipsImg.color = darkSkin;
            tipsImg.raycastTarget = false;
            RectTransform tipsRt = fingerTips.GetComponent<RectTransform>();
            tipsRt.anchoredPosition = new Vector2(2f, 6f);
            tipsRt.sizeDelta = new Vector2(28f, 6f);

            // 엄지 (앞으로 감싸서 손가락 위를 덮음)
            GameObject thumb = new GameObject("Thumb");
            thumb.transform.SetParent(parent, false);
            Image thumbImg = thumb.AddComponent<Image>();
            thumbImg.color = skinColor;
            thumbImg.raycastTarget = false;
            RectTransform thumbRt = thumb.GetComponent<RectTransform>();
            thumbRt.anchoredPosition = new Vector2(-14f, 2f);
            thumbRt.sizeDelta = new Vector2(12f, 18f);
            thumbRt.localRotation = Quaternion.Euler(0f, 0f, 10f);

            // 손목 (아래쪽)
            GameObject wrist = new GameObject("Wrist");
            wrist.transform.SetParent(parent, false);
            Image wristImg = wrist.AddComponent<Image>();
            wristImg.color = darkSkin;
            wristImg.raycastTarget = false;
            RectTransform wristRt = wrist.GetComponent<RectTransform>();
            wristRt.anchoredPosition = new Vector2(2f, -18f);
            wristRt.sizeDelta = new Vector2(24f, 10f);

        }

        /// <summary>쿠션 스킬 아이콘 — 드릴이 벽에 반사되는 모습 (레벨별 반사 횟수)</summary>
        /// <summary>연쇄폭탄 아이콘 — 중앙 폭발에서 소형 폭탄이 포물선으로 날아가는 모습</summary>
        private void CreateChainBombIcon(Transform parent, SkillNodeData skillData)
        {
            int count = Mathf.Clamp(skillData.drillMoveRange, 1, 3);
            Color burstColor = new Color(1f, 0.6f, 0.15f, 0.9f);
            Color bombColor = new Color(1f, 1f, 1f, 0.9f);
            Color trailColor = new Color(1f, 0.8f, 0.3f, 0.5f);

            // 중앙 폭발 원
            GameObject center = new GameObject("ChainCenter");
            center.transform.SetParent(parent, false);
            Image centerImg = center.AddComponent<Image>();
            centerImg.color = burstColor;
            centerImg.raycastTarget = false;
            RectTransform centerRt = center.GetComponent<RectTransform>();
            centerRt.anchoredPosition = new Vector2(0f, -8f);
            centerRt.sizeDelta = new Vector2(16f, 16f);

            // 소형 폭탄 + 포물선 궤적 (레벨별 개수)
            // 균형 잡힌 배치: 1개=위, 2개=좌상+우상, 3개=좌상+위+우상
            float[][] bombPositions = {
                new[] { 0f, 22f },          // 1개: 위
                new[] { -16f, 18f, 16f, 18f }, // 2개: 좌상, 우상
                new[] { -18f, 14f, 0f, 24f, 18f, 14f } // 3개: 좌상, 위, 우상
            };

            float[] positions = bombPositions[count - 1];
            for (int i = 0; i < positions.Length; i += 2)
            {
                float bx = positions[i];
                float by = positions[i + 1];

                // 포물선 궤적 (점선 표현 — 3개 작은 점)
                for (int d = 1; d <= 3; d++)
                {
                    float t = d / 4f;
                    float dx = bx * t;
                    float dy = (-8f) + (by + 8f) * t + 8f * t * (1f - t); // 포물선
                    GameObject dot = new GameObject($"Trail{i / 2}_{d}");
                    dot.transform.SetParent(parent, false);
                    Image dotImg = dot.AddComponent<Image>();
                    dotImg.color = trailColor;
                    dotImg.raycastTarget = false;
                    RectTransform dotRt = dot.GetComponent<RectTransform>();
                    dotRt.anchoredPosition = new Vector2(dx, dy);
                    dotRt.sizeDelta = new Vector2(3f, 3f);
                }

                // 소형 폭탄 (끝점)
                GameObject bomb = new GameObject($"MiniBomb{i / 2}");
                bomb.transform.SetParent(parent, false);
                Image bombImg = bomb.AddComponent<Image>();
                bombImg.color = bombColor;
                bombImg.raycastTarget = false;
                RectTransform bombRt = bomb.GetComponent<RectTransform>();
                bombRt.anchoredPosition = new Vector2(bx, by);
                bombRt.sizeDelta = new Vector2(10f, 10f);

                // 폭탄 도화선 (작은 선)
                GameObject fuse = new GameObject($"MiniFuse{i / 2}");
                fuse.transform.SetParent(parent, false);
                Image fuseImg = fuse.AddComponent<Image>();
                fuseImg.color = new Color(0.9f, 0.7f, 0.3f, 0.8f);
                fuseImg.raycastTarget = false;
                RectTransform fuseRt = fuse.GetComponent<RectTransform>();
                fuseRt.anchoredPosition = new Vector2(bx + 3f, by + 6f);
                fuseRt.sizeDelta = new Vector2(2f, 5f);
                fuseRt.localRotation = Quaternion.Euler(0f, 0f, -20f);
            }
        }

        private void CreateCushionIcon(Transform parent, SkillNodeData skillData)
        {
            Color lineColor = new Color(1f, 1f, 1f, 0.9f);
            Color wallColor = new Color(0.7f, 0.7f, 0.8f, 0.7f);

            // 쿠션 레벨: drillMoveRange 값 사용 (1, 2, 3)
            int bounces = Mathf.Clamp(skillData.drillMoveRange, 1, 3);

            // 벽 (오른쪽 세로선)
            GameObject wall = new GameObject("Wall");
            wall.transform.SetParent(parent, false);
            Image wallImg = wall.AddComponent<Image>();
            wallImg.color = wallColor;
            wallImg.raycastTarget = false;
            RectTransform wallRt = wall.GetComponent<RectTransform>();
            wallRt.anchoredPosition = new Vector2(18f, 0f);
            wallRt.sizeDelta = new Vector2(4f, 45f);

            if (bounces == 1)
            {
                // 1회 반사: 입사 → 벽 → 반사
                CreateIconLine(parent, -16f, 12f, 16f, 0f, lineColor);
                CreateIconLine(parent, 16f, 0f, -10f, -14f, lineColor);
                // 화살촉: 마지막 라인 방향 자동 계산
                float a1 = Mathf.Atan2(-14f - 0f, -10f - 16f) * Mathf.Rad2Deg;
                CreateIconArrow(parent, -10f, -14f, a1, lineColor);
            }
            else if (bounces == 2)
            {
                // 왼쪽 벽 추가
                GameObject wall2 = new GameObject("Wall2");
                wall2.transform.SetParent(parent, false);
                Image wall2Img = wall2.AddComponent<Image>();
                wall2Img.color = wallColor;
                wall2Img.raycastTarget = false;
                RectTransform wall2Rt = wall2.GetComponent<RectTransform>();
                wall2Rt.anchoredPosition = new Vector2(-18f, 0f);
                wall2Rt.sizeDelta = new Vector2(4f, 45f);

                CreateIconLine(parent, -2f, 18f, 16f, 6f, lineColor);
                CreateIconLine(parent, 16f, 6f, -16f, -6f, lineColor);
                CreateIconLine(parent, -16f, -6f, 4f, -18f, lineColor);
                float a2 = Mathf.Atan2(-18f - (-6f), 4f - (-16f)) * Mathf.Rad2Deg;
                CreateIconArrow(parent, 4f, -18f, a2, lineColor);
            }
            else // 3회
            {
                GameObject wall2 = new GameObject("Wall2");
                wall2.transform.SetParent(parent, false);
                Image wall2Img = wall2.AddComponent<Image>();
                wall2Img.color = wallColor;
                wall2Img.raycastTarget = false;
                RectTransform wall2Rt = wall2.GetComponent<RectTransform>();
                wall2Rt.anchoredPosition = new Vector2(-18f, 0f);
                wall2Rt.sizeDelta = new Vector2(4f, 45f);

                CreateIconLine(parent, -4f, 20f, 16f, 12f, lineColor);
                CreateIconLine(parent, 16f, 12f, -16f, 2f, lineColor);
                CreateIconLine(parent, -16f, 2f, 16f, -8f, lineColor);
                CreateIconLine(parent, 16f, -8f, -6f, -20f, lineColor);
                float a3 = Mathf.Atan2(-20f - (-8f), -6f - 16f) * Mathf.Rad2Deg;
                CreateIconArrow(parent, -6f, -20f, a3, lineColor);
            }
        }

        /// <summary>
        /// 드릴 관통 아이콘: 투사체(화살표)가 몬스터(원)를 관통하는 모습.
        /// 레벨에 따라 관통하는 몬스터 수가 증가.
        /// </summary>
        private void CreatePenetrateIcon(Transform parent, SkillNodeData skillData)
        {
            int level = Mathf.Clamp((int)skillData.skillType - 1199, 1, 3);
            Color arrowColor = new Color(1f, 0.85f, 0.6f, 0.95f);   // 따뜻한 금색 화살표
            Color monsterColor = new Color(0.55f, 0.2f, 0.2f, 0.75f); // 어두운 적색 몬스터
            Color crackColor = new Color(1f, 0.7f, 0.3f, 0.85f);      // 관통 균열 색

            // ── 몬스터(원형) 배치: 레벨에 따라 1~3개 ──
            // 등간격으로 위에서 아래로 배치
            float topY = (level == 1) ? 0f : (level == 2) ? 10f : 16f;
            float gap = (level == 1) ? 0f : (level == 2) ? 20f : 16f;
            float monsterSize = (level <= 2) ? 18f : 15f;

            for (int m = 0; m < level; m++)
            {
                float my = topY - gap * m;

                // 몬스터 본체 (원)
                GameObject monster = new GameObject($"Monster{m}");
                monster.transform.SetParent(parent, false);
                Image monImg = monster.AddComponent<Image>();
                monImg.color = monsterColor;
                monImg.raycastTarget = false;
                RectTransform monRt = monster.GetComponent<RectTransform>();
                monRt.anchoredPosition = new Vector2(0f, my);
                monRt.sizeDelta = new Vector2(monsterSize, monsterSize);

                // 관통 균열 (X자 크랙 — 투사체가 뚫고 간 자국)
                CreateIconLine(parent, -monsterSize * 0.35f, my + monsterSize * 0.35f,
                    monsterSize * 0.35f, my - monsterSize * 0.35f, crackColor);
                CreateIconLine(parent, monsterSize * 0.35f, my + monsterSize * 0.35f,
                    -monsterSize * 0.35f, my - monsterSize * 0.35f, crackColor);
            }

            // ── 투사체 경로 (수직 관통선) ──
            float lineTop = topY + monsterSize * 0.5f + 10f;
            float lineBot = topY - gap * Mathf.Max(0, level - 1) - monsterSize * 0.5f - 10f;
            CreateIconLine(parent, 0f, lineTop, 0f, lineBot, arrowColor);

            // ── 화살촉 (아래쪽, ▼) ──
            GameObject arrowHead = new GameObject("ArrowHead");
            arrowHead.transform.SetParent(parent, false);
            Text arrowText = arrowHead.AddComponent<Text>();
            arrowText.font = font;
            arrowText.fontSize = 18;
            arrowText.alignment = TextAnchor.MiddleCenter;
            arrowText.color = arrowColor;
            arrowText.raycastTarget = false;
            arrowText.text = "\u25BC"; // ▼
            RectTransform arrowRt = arrowHead.GetComponent<RectTransform>();
            arrowRt.anchoredPosition = new Vector2(0f, lineBot - 6f);
            arrowRt.sizeDelta = new Vector2(20f, 20f);

            // ── 화살표 꼬리 (위쪽, 작은 수평선) ──
            CreateIconLine(parent, -5f, lineTop, 5f, lineTop, arrowColor);
        }

        /// <summary>아이콘 내부용 작은 선분</summary>
        private void CreateIconLine(Transform parent, float x1, float y1, float x2, float y2, Color color)
        {
            GameObject line = new GameObject("IconLine");
            line.transform.SetParent(parent, false);
            Image img = line.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            RectTransform rt = line.GetComponent<RectTransform>();
            rt.anchoredPosition = new Vector2((x1 + x2) * 0.5f, (y1 + y2) * 0.5f);
            float dist = Vector2.Distance(new Vector2(x1, y1), new Vector2(x2, y2));
            rt.sizeDelta = new Vector2(dist, 3f);
            float angle = Mathf.Atan2(y2 - y1, x2 - x1) * Mathf.Rad2Deg;
            rt.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        /// <summary>아이콘 내부용 화살촉 (▶ 텍스트)</summary>
        private void CreateIconArrow(Transform parent, float x, float y, float angleDeg, Color color)
        {
            GameObject arrow = new GameObject("IconArrow");
            arrow.transform.SetParent(parent, false);
            Text txt = arrow.AddComponent<Text>();
            txt.font = font;
            txt.fontSize = 14;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = color;
            txt.raycastTarget = false;
            txt.text = "\u25B6"; // ▶
            RectTransform rt = arrow.GetComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(18f, 18f);
            rt.localRotation = Quaternion.Euler(0f, 0f, angleDeg);
        }

        private void CreateMoveIcon(Transform parent)
        {
            Color arrowColor = new Color(1f, 1f, 1f, 0.9f);

            // 중앙 원 (기준점)
            GameObject center = new GameObject("MoveCenter");
            center.transform.SetParent(parent, false);
            Image centerImg = center.AddComponent<Image>();
            centerImg.color = arrowColor;
            centerImg.raycastTarget = false;
            RectTransform centerRt = center.GetComponent<RectTransform>();
            centerRt.anchoredPosition = Vector2.zero;
            centerRt.sizeDelta = new Vector2(12f, 12f);

            // 4방향 화살표 (▲▼◀▶)
            string[] arrows = { "\u25B2", "\u25BC", "\u25C0", "\u25B6" }; // ▲▼◀▶
            Vector2[] offsets = {
                new Vector2(0f, 24f),    // ↑
                new Vector2(0f, -24f),   // ↓
                new Vector2(-22f, 0f),   // ←
                new Vector2(22f, 0f)     // →
            };
            int[] fontSizes = { 24, 24, 20, 20 };

            for (int i = 0; i < 4; i++)
            {
                GameObject arrow = new GameObject($"Arrow{i}");
                arrow.transform.SetParent(parent, false);
                Text arrowText = arrow.AddComponent<Text>();
                arrowText.font = font;
                arrowText.fontSize = fontSizes[i];
                arrowText.alignment = TextAnchor.MiddleCenter;
                arrowText.color = arrowColor;
                arrowText.raycastTarget = false;
                arrowText.text = arrows[i];
                RectTransform arrowRt = arrow.GetComponent<RectTransform>();
                arrowRt.anchoredPosition = offsets[i];
                arrowRt.sizeDelta = new Vector2(30f, 30f);
            }
        }

        private void CreateDrillIcon(Transform parent, SkillNodeData skillData)
        {
            // 드릴 본체 (세로 직사각형)
            GameObject body = new GameObject("DrillBody");
            body.transform.SetParent(parent, false);
            Image bodyImg = body.AddComponent<Image>();
            bodyImg.color = new Color(0.9f, 0.9f, 0.95f, 0.95f);
            bodyImg.raycastTarget = false;
            RectTransform bodyRt = body.GetComponent<RectTransform>();
            bodyRt.anchoredPosition = new Vector2(0f, 6f);   // 4×1.5
            bodyRt.sizeDelta = new Vector2(21f, 45f);       // 14×1.5, 30×1.5

            // 드릴 끝
            GameObject tip = new GameObject("DrillTip");
            tip.transform.SetParent(parent, false);
            Text tipText = tip.AddComponent<Text>();
            tipText.font = font;
            tipText.fontSize = 33; // 22×1.5
            tipText.alignment = TextAnchor.MiddleCenter;
            tipText.color = new Color(0.9f, 0.9f, 0.95f, 0.95f);
            tipText.raycastTarget = false;
            tipText.text = "\u25BC";
            RectTransform tipRt = tip.GetComponent<RectTransform>();
            tipRt.anchoredPosition = new Vector2(0f, -27f); // -18×1.5
            tipRt.sizeDelta = new Vector2(45f, 38f);        // 30×1.5, 25×1.5

            // ★ 아이콘 내 텍스트 제거됨 — 타이틀과 SP/Gold만 표시
        }

        /// <summary>
        /// 폭탄 아이콘 프로시저럴 생성 (원형 + 도화선)
        /// </summary>
        private void CreateBombIcon(Transform parent, SkillNodeData skillData)
        {
            // 폭탄 본체 (원형)
            GameObject body = new GameObject("BombBody");
            body.transform.SetParent(parent, false);
            Image bodyImg = body.AddComponent<Image>();
            bodyImg.color = new Color(0.95f, 0.55f, 0.15f, 0.95f);
            bodyImg.raycastTarget = false;
            RectTransform bodyRt = body.GetComponent<RectTransform>();
            bodyRt.anchoredPosition = new Vector2(0f, -3f);   // -2×1.5
            bodyRt.sizeDelta = new Vector2(42f, 42f);       // 28×1.5

            // 도화선
            GameObject fuse = new GameObject("BombFuse");
            fuse.transform.SetParent(parent, false);
            Image fuseImg = fuse.AddComponent<Image>();
            fuseImg.color = new Color(0.8f, 0.7f, 0.3f, 0.9f);
            fuseImg.raycastTarget = false;
            RectTransform fuseRt = fuse.GetComponent<RectTransform>();
            fuseRt.anchoredPosition = new Vector2(9f, 21f);   // 6×1.5, 14×1.5
            fuseRt.sizeDelta = new Vector2(4.5f, 18f);         // 3×1.5, 12×1.5
            fuseRt.localRotation = Quaternion.Euler(0f, 0f, -20f);

            // 불꽃
            GameObject spark = new GameObject("BombSpark");
            spark.transform.SetParent(parent, false);
            Image sparkImg = spark.AddComponent<Image>();
            sparkImg.color = new Color(1f, 0.9f, 0.3f, 0.9f);
            sparkImg.raycastTarget = false;
            RectTransform sparkRt = spark.GetComponent<RectTransform>();
            sparkRt.anchoredPosition = new Vector2(12f, 33f); // 8×1.5, 22×1.5
            sparkRt.sizeDelta = new Vector2(9f, 9f);           // 6×1.5

            // ★ 아이콘 내 텍스트 제거됨 — 타이틀과 SP/Gold만 표시
        }

        // ============================================================
        // 연결 라인
        // ============================================================

        private void CreateConnectionLine(Transform parent, float x1, float y1, float x2, float y2)
        {
            GameObject lineObj = new GameObject("ConnectionLine");
            lineObj.transform.SetParent(parent, false);

            Image lineImg = lineObj.AddComponent<Image>();
            lineImg.color = new Color(0.5f, 0.6f, 0.8f, 0.6f);
            lineImg.raycastTarget = false;

            RectTransform lineRt = lineObj.GetComponent<RectTransform>();

            float midX = (x1 + x2) * 0.5f;
            float midY = (y1 + y2) * 0.5f;
            float dist = Vector2.Distance(new Vector2(x1, y1), new Vector2(x2, y2));

            // 노드 크기 반영 → 노드 테두리에서 시작
            float lineLen = dist - NODE_SIZE * 0.85f;
            if (lineLen < 0f) lineLen = 0f;

            lineRt.anchoredPosition = new Vector2(midX, midY);
            lineRt.sizeDelta = new Vector2(lineLen, LINE_THICKNESS);

            // 회전 (수평이 아닌 경우)
            float angle = Mathf.Atan2(y2 - y1, x2 - x1) * Mathf.Rad2Deg;
            lineRt.localRotation = Quaternion.Euler(0f, 0f, angle);

            connectionLines.Add(lineObj);
        }

        /// <summary>
        /// 정확한 두 점 연결선 (노드 크기 축소 없음 — 그룹 타이틀↔스킬 연결용)
        /// </summary>
        private void CreateExactLine(Transform parent, float x1, float y1, float x2, float y2)
        {
            GameObject lineObj = new GameObject("GroupLine");
            lineObj.transform.SetParent(parent, false);

            Image lineImg = lineObj.AddComponent<Image>();
            lineImg.color = new Color(0.5f, 0.6f, 0.8f, 0.5f);
            lineImg.raycastTarget = false;

            RectTransform lineRt = lineObj.GetComponent<RectTransform>();
            float midX = (x1 + x2) * 0.5f;
            float midY = (y1 + y2) * 0.5f;
            float dist = Vector2.Distance(new Vector2(x1, y1), new Vector2(x2, y2));

            lineRt.anchoredPosition = new Vector2(midX, midY);
            lineRt.sizeDelta = new Vector2(dist, LINE_THICKNESS);

            float angle = Mathf.Atan2(y2 - y1, x2 - x1) * Mathf.Rad2Deg;
            lineRt.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        // ============================================================
        // 나가기 버튼
        // ============================================================

        private void CreateExitButton()
        {
            float btnSize = 84f;

            GameObject btnObj = new GameObject("SkillTreeExitButton");
            btnObj.transform.SetParent(rootContainer.transform, false);
            RectTransform btnRt = btnObj.AddComponent<RectTransform>();
            btnRt.anchorMin = new Vector2(1f, 0f);
            btnRt.anchorMax = new Vector2(1f, 0f);
            btnRt.pivot = new Vector2(0.5f, 0.5f);
            // 인게임 나가기 버튼과 동일 위치 (우측 하단)
            btnRt.anchoredPosition = new Vector2(-72f, 82f);
            btnRt.sizeDelta = new Vector2(btnSize, btnSize);

            Image btnBg = btnObj.AddComponent<Image>();
            btnBg.sprite = HexBlock.GetHexFlashSprite();
            btnBg.type = Image.Type.Simple;
            btnBg.preserveAspect = true;
            btnBg.color = new Color(0.85f, 0.65f, 0.55f, 0.90f);

            Button btn = btnObj.AddComponent<Button>();
            var btnColors = btn.colors;
            btnColors.highlightedColor = new Color(0.95f, 0.75f, 0.65f, 0.95f);
            btnColors.pressedColor = new Color(0.65f, 0.50f, 0.40f, 0.95f);
            btn.colors = btnColors;

            // 문 모양 아이콘 (CreateLobbyExitButton과 동일 패턴)
            GameObject frame = new GameObject("DoorFrame");
            frame.transform.SetParent(btnObj.transform, false);
            Image frameImg = frame.AddComponent<Image>();
            frameImg.color = new Color(0.85f, 0.7f, 0.45f);
            frameImg.raycastTarget = false;
            RectTransform frameRt = frame.GetComponent<RectTransform>();
            frameRt.anchoredPosition = new Vector2(0f, 2f);
            frameRt.sizeDelta = new Vector2(30f, 40f);

            GameObject panel = new GameObject("DoorPanel");
            panel.transform.SetParent(frame.transform, false);
            Image panelImg = panel.AddComponent<Image>();
            panelImg.color = new Color(0.55f, 0.35f, 0.15f);
            panelImg.raycastTarget = false;
            RectTransform panelRt = panel.GetComponent<RectTransform>();
            panelRt.anchoredPosition = Vector2.zero;
            panelRt.sizeDelta = new Vector2(24f, 34f);

            GameObject knob = new GameObject("DoorKnob");
            knob.transform.SetParent(panel.transform, false);
            Image knobImg = knob.AddComponent<Image>();
            knobImg.color = new Color(1f, 0.85f, 0.3f);
            knobImg.raycastTarget = false;
            RectTransform knobRt = knob.GetComponent<RectTransform>();
            knobRt.anchoredPosition = new Vector2(6f, -2f);
            knobRt.sizeDelta = new Vector2(5f, 5f);

            // 화살표
            GameObject arrow = new GameObject("ExitArrow");
            arrow.transform.SetParent(btnObj.transform, false);
            Text arrowText = arrow.AddComponent<Text>();
            arrowText.font = font;
            arrowText.fontSize = 20;
            arrowText.alignment = TextAnchor.MiddleCenter;
            arrowText.color = new Color(1f, 1f, 1f, 0.8f);
            arrowText.raycastTarget = false;
            arrowText.text = "\u2192";
            RectTransform arrowRt = arrow.GetComponent<RectTransform>();
            arrowRt.anchoredPosition = new Vector2(22f, 2f);
            arrowRt.sizeDelta = new Vector2(20f, 20f);

            btn.onClick.AddListener(() =>
            {
                if (AudioManager.Instance != null) AudioManager.Instance.PlayButtonClick();
                Hide();
            });
        }

        // ============================================================
        // 실제 스킬 초기화 버튼 (좌측 하단) — 쿨다운 적용
        //   - 미구독 사용 시 7일 쿨다운, 구독 사용 시 24시간 쿨다운
        //   - 구독 중에는 쿨다운 무시 (즉시 사용 가능)
        //   - 잔여 시간을 "활성화까지 6d 23h" 형식으로 표시 (구독과 동일 톤)
        // ============================================================

        private void CreateRealResetButton()
        {
            // 버튼
            GameObject btnObj = new GameObject("RealResetButton");
            btnObj.transform.SetParent(rootContainer.transform, false);
            RectTransform btnRt = btnObj.AddComponent<RectTransform>();
            btnRt.anchorMin = new Vector2(0f, 0f);
            btnRt.anchorMax = new Vector2(0f, 0f);
            btnRt.pivot = new Vector2(0f, 0f);
            btnRt.anchoredPosition = new Vector2(20f, 70f); // 좌하단
            btnRt.sizeDelta = new Vector2(180f, 44f);

            realResetButtonBg = btnObj.AddComponent<Image>();
            realResetButtonBg.color = new Color(0.55f, 0.18f, 0.18f, 0.92f);

            realResetButton = btnObj.AddComponent<Button>();
            var btnColors = realResetButton.colors;
            btnColors.highlightedColor = new Color(0.7f, 0.25f, 0.25f);
            btnColors.pressedColor = new Color(0.4f, 0.12f, 0.12f);
            btnColors.disabledColor = new Color(0.35f, 0.30f, 0.32f, 0.85f);
            realResetButton.colors = btnColors;

            // 라벨
            GameObject labelObj = new GameObject("Label");
            labelObj.transform.SetParent(btnObj.transform, false);
            RectTransform labelRt = labelObj.AddComponent<RectTransform>();
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;
            realResetButtonText = labelObj.AddComponent<Text>();
            realResetButtonText.font = font;
            realResetButtonText.fontSize = 17;
            realResetButtonText.fontStyle = FontStyle.Bold;
            realResetButtonText.alignment = TextAnchor.MiddleCenter;
            realResetButtonText.color = Color.white;
            realResetButtonText.raycastTarget = false;
            realResetButtonText.text = "스킬 초기화";

            Outline labelOutline = labelObj.AddComponent<Outline>();
            labelOutline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            labelOutline.effectDistance = new Vector2(1f, -1f);

            // 잔여 시간 표시 — 버튼 아래
            GameObject statusObj = new GameObject("RealResetStatus");
            statusObj.transform.SetParent(rootContainer.transform, false);
            RectTransform statusRt = statusObj.AddComponent<RectTransform>();
            statusRt.anchorMin = new Vector2(0f, 0f);
            statusRt.anchorMax = new Vector2(0f, 0f);
            statusRt.pivot = new Vector2(0f, 0f);
            statusRt.anchoredPosition = new Vector2(20f, 46f); // 버튼 바로 아래
            statusRt.sizeDelta = new Vector2(220f, 22f);
            realResetStatusText = statusObj.AddComponent<Text>();
            realResetStatusText.font = font;
            realResetStatusText.fontSize = 14;
            realResetStatusText.fontStyle = FontStyle.Bold;
            realResetStatusText.alignment = TextAnchor.UpperLeft;
            realResetStatusText.color = new Color(1f, 0.85f, 0.55f, 1f);
            realResetStatusText.raycastTarget = false;
            realResetStatusText.text = "";

            Outline statusOutline = statusObj.AddComponent<Outline>();
            statusOutline.effectColor = new Color(0.2f, 0.1f, 0f, 0.85f);
            statusOutline.effectDistance = new Vector2(1f, -1f);

            realResetButton.onClick.AddListener(OnRealResetClicked);
        }

        private void OnRealResetClicked()
        {
            if (SkillTreeManager.Instance == null) return;
            if (!SkillTreeManager.Instance.IsResetAvailable) return; // 쿨다운 중

            if (AudioManager.Instance != null) AudioManager.Instance.PlayButtonClick();

            bool ok = SkillTreeManager.Instance.TryResetWithCooldown();
            if (ok)
            {
                RefreshResourceDisplay();
                RefreshAllNodes();
                RefreshRealResetButton();
            }
        }

        /// <summary>
        /// 실제 스킬 초기화 버튼 상태 / 잔여 시간 라벨 갱신.
        ///   - 해금 스킬 0개: 비활성 (이미 초기화 상태이므로 누를 필요 없음)
        ///   - 쿨다운 중: 비활성, "활성화까지 Xd Yh" 표시
        ///   - 구독 중: 쿨다운 무시 (해금 스킬이 있다면 활성)
        ///   - 사용 가능: 활성, 잔여시간 텍스트 비움
        /// </summary>
        private void RefreshRealResetButton()
        {
            if (SkillTreeManager.Instance == null || realResetButton == null) return;

            bool cooldownClear = SkillTreeManager.Instance.IsResetAvailable;
            bool hasAnyUnlocked = SkillTreeManager.Instance.HasAnyUnlockedSkill;
            bool clickable = cooldownClear && hasAnyUnlocked;

            realResetButton.interactable = clickable;

            if (realResetButtonBg != null)
            {
                realResetButtonBg.color = clickable
                    ? new Color(0.55f, 0.18f, 0.18f, 0.92f)   // 활성: 빨강
                    : new Color(0.32f, 0.28f, 0.30f, 0.85f);  // 비활성: 회색
            }

            if (realResetStatusText != null)
            {
                if (!cooldownClear)
                {
                    // 쿨다운 중 — 잔여시간 표시
                    var remaining = SkillTreeManager.Instance.ResetRemainingCooldown;
                    realResetStatusText.text = $"활성화까지 {SkillTreeManager.FormatResetCooldown(remaining)}";
                }
                else if (!hasAnyUnlocked)
                {
                    // 쿨다운은 풀렸지만 초기화할 대상 없음
                    realResetStatusText.text = "* 초기화할 스킬 없음";
                }
                else
                {
                    realResetStatusText.text = "";
                }
            }
        }

        /// <summary>1초 주기 잔여시간 갱신 루프 (Show 동안만 실행).</summary>
        private System.Collections.IEnumerator ResetStatusRefreshLoop()
        {
            while (rootContainer != null && rootContainer.activeInHierarchy)
            {
                RefreshRealResetButton();
                yield return new WaitForSecondsRealtime(1f);
            }
        }

        // ============================================================
        // 에디터 전용 디버그 버튼
        // ============================================================

#if UNITY_EDITOR
        private void CreateDebugButtons()
        {
            // 두 디버그 버튼을 화면 하단 중앙에 가로 정렬 (좌: SP +10, 우: 스킬 초기화)
            // 각 160px, 가운데 20px gap → ±90 offset
            // SP 추가 버튼 (하단 중앙 — 왼쪽)
            GameObject addSPObj = new GameObject("DebugAddSP");
            addSPObj.transform.SetParent(rootContainer.transform, false);
            RectTransform addSPRt = addSPObj.AddComponent<RectTransform>();
            addSPRt.anchorMin = new Vector2(0.5f, 0f);
            addSPRt.anchorMax = new Vector2(0.5f, 0f);
            addSPRt.pivot = new Vector2(0.5f, 0f);
            addSPRt.anchoredPosition = new Vector2(-90f, 30f);
            addSPRt.sizeDelta = new Vector2(160f, 40f);

            Image addSPBg = addSPObj.AddComponent<Image>();
            addSPBg.color = new Color(0.2f, 0.5f, 0.3f, 0.85f);

            Button addSPBtn = addSPObj.AddComponent<Button>();
            var addColors = addSPBtn.colors;
            addColors.highlightedColor = new Color(0.3f, 0.6f, 0.4f);
            addColors.pressedColor = new Color(0.15f, 0.35f, 0.2f);
            addSPBtn.colors = addColors;

            GameObject addSPTextObj = new GameObject("Text");
            addSPTextObj.transform.SetParent(addSPObj.transform, false);
            RectTransform addSPTextRt = addSPTextObj.AddComponent<RectTransform>();
            addSPTextRt.anchorMin = Vector2.zero;
            addSPTextRt.anchorMax = Vector2.one;
            addSPTextRt.offsetMin = Vector2.zero;
            addSPTextRt.offsetMax = Vector2.zero;
            Text addSPText = addSPTextObj.AddComponent<Text>();
            addSPText.font = font;
            addSPText.fontSize = 16;
            addSPText.alignment = TextAnchor.MiddleCenter;
            addSPText.color = Color.white;
            addSPText.raycastTarget = false;
            addSPText.text = "[DEBUG] SP +10";

            addSPBtn.onClick.AddListener(() =>
            {
                if (SkillTreeManager.Instance != null)
                {
                    SkillTreeManager.Instance.AddSkillPoints(10);
                    RefreshResourceDisplay();
                    RefreshAllNodes();
                }
            });

            // 스킬 초기화 버튼 (하단 중앙 — 오른쪽)
            GameObject resetObj = new GameObject("DebugResetSkills");
            resetObj.transform.SetParent(rootContainer.transform, false);
            RectTransform resetRt = resetObj.AddComponent<RectTransform>();
            resetRt.anchorMin = new Vector2(0.5f, 0f);
            resetRt.anchorMax = new Vector2(0.5f, 0f);
            resetRt.pivot = new Vector2(0.5f, 0f);
            resetRt.anchoredPosition = new Vector2(90f, 30f);
            resetRt.sizeDelta = new Vector2(160f, 40f);

            Image resetBg = resetObj.AddComponent<Image>();
            resetBg.color = new Color(0.5f, 0.2f, 0.2f, 0.85f);

            Button resetBtn = resetObj.AddComponent<Button>();
            var resetColors = resetBtn.colors;
            resetColors.highlightedColor = new Color(0.6f, 0.3f, 0.3f);
            resetColors.pressedColor = new Color(0.35f, 0.15f, 0.15f);
            resetBtn.colors = resetColors;

            GameObject resetTextObj = new GameObject("Text");
            resetTextObj.transform.SetParent(resetObj.transform, false);
            RectTransform resetTextRt = resetTextObj.AddComponent<RectTransform>();
            resetTextRt.anchorMin = Vector2.zero;
            resetTextRt.anchorMax = Vector2.one;
            resetTextRt.offsetMin = Vector2.zero;
            resetTextRt.offsetMax = Vector2.zero;
            Text resetText = resetTextObj.AddComponent<Text>();
            resetText.font = font;
            resetText.fontSize = 16;
            resetText.alignment = TextAnchor.MiddleCenter;
            resetText.color = Color.white;
            resetText.raycastTarget = false;
            resetText.text = "[DEBUG] 스킬 초기화";

            resetBtn.onClick.AddListener(() =>
            {
                if (SkillTreeManager.Instance != null)
                {
                    SkillTreeManager.Instance.ResetAllSkills();
                    RefreshResourceDisplay();
                    RefreshAllNodes();
                    RefreshRealResetButton(); // 해금 스킬 0이 됐으니 실제 초기화 버튼 비활성화
                }
            });
        }
#endif

        // ============================================================
        // 상세 팝업
        // ============================================================

        private void CreateDetailPopup()
        {
            detailPopup = new GameObject("SkillDetailPopup");
            detailPopup.transform.SetParent(rootContainer.transform, false);
            RectTransform popupRt = detailPopup.AddComponent<RectTransform>();
            popupRt.anchorMin = Vector2.zero;
            popupRt.anchorMax = Vector2.one;
            popupRt.offsetMin = Vector2.zero;
            popupRt.offsetMax = Vector2.zero;

            // 반투명 배경 (터치 가드)
            Image overlay = detailPopup.AddComponent<Image>();
            overlay.color = new Color(0f, 0f, 0f, 0.6f);
            overlay.raycastTarget = true;

            // 배경 클릭 시 닫기
            Button overlayBtn = detailPopup.AddComponent<Button>();
            overlayBtn.onClick.AddListener(() =>
            {
                detailPopup.SetActive(false);
            });

            // === 패널 ===
            GameObject panel = new GameObject("DetailPanel");
            panel.transform.SetParent(detailPopup.transform, false);
            RectTransform panelRt = panel.AddComponent<RectTransform>();
            panelRt.anchoredPosition = new Vector2(0f, 20f);
            panelRt.sizeDelta = new Vector2(440f, 400f);

            Image panelBg = panel.AddComponent<Image>();
            panelBg.color = new Color(0.12f, 0.08f, 0.20f, 0.98f);

            // 테두리
            Outline panelOutline = panel.AddComponent<Outline>();
            panelOutline.effectColor = new Color(0.5f, 0.4f, 0.8f, 0.7f);
            panelOutline.effectDistance = new Vector2(2f, 2f);

            // === 스킬 이름 ===
            GameObject titleObj = new GameObject("DetailTitle");
            titleObj.transform.SetParent(panel.transform, false);
            RectTransform titleRt = titleObj.AddComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0f, 1f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.anchoredPosition = new Vector2(0f, -15f);
            titleRt.sizeDelta = new Vector2(0f, 40f);
            titleRt.offsetMin = new Vector2(20f, titleRt.offsetMin.y);
            titleRt.offsetMax = new Vector2(-20f, titleRt.offsetMax.y);
            Text titleText = titleObj.AddComponent<Text>();
            titleText.font = font;
            titleText.fontSize = 26;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.color = new Color(0.95f, 0.9f, 1f);
            titleText.raycastTarget = false;
            titleText.text = "";

            // === 설명 ===
            GameObject descObj = new GameObject("DetailDesc");
            descObj.transform.SetParent(panel.transform, false);
            RectTransform descRt = descObj.AddComponent<RectTransform>();
            descRt.anchorMin = new Vector2(0f, 1f);
            descRt.anchorMax = new Vector2(1f, 1f);
            descRt.pivot = new Vector2(0.5f, 1f);
            descRt.anchoredPosition = new Vector2(0f, -60f);
            descRt.sizeDelta = new Vector2(0f, 60f);
            descRt.offsetMin = new Vector2(25f, descRt.offsetMin.y);
            descRt.offsetMax = new Vector2(-25f, descRt.offsetMax.y);
            Text descText = descObj.AddComponent<Text>();
            descText.font = font;
            descText.fontSize = 18;
            descText.alignment = TextAnchor.UpperLeft;
            descText.color = new Color(0.8f, 0.8f, 0.85f, 0.9f);
            descText.raycastTarget = false;
            descText.text = "";

            // === 사용 방법 ===
            GameObject usageObj = new GameObject("DetailUsage");
            usageObj.transform.SetParent(panel.transform, false);
            RectTransform usageRt = usageObj.AddComponent<RectTransform>();
            usageRt.anchorMin = new Vector2(0f, 1f);
            usageRt.anchorMax = new Vector2(1f, 1f);
            usageRt.pivot = new Vector2(0.5f, 1f);
            usageRt.anchoredPosition = new Vector2(0f, -135f);
            usageRt.sizeDelta = new Vector2(0f, 70f);
            usageRt.offsetMin = new Vector2(25f, usageRt.offsetMin.y);
            usageRt.offsetMax = new Vector2(-25f, usageRt.offsetMax.y);
            Text usageText = usageObj.AddComponent<Text>();
            usageText.font = font;
            usageText.fontSize = 15;
            usageText.alignment = TextAnchor.UpperLeft;
            usageText.color = new Color(0.65f, 0.75f, 0.85f, 0.85f);
            usageText.raycastTarget = false;
            usageText.text = "";

            // === 비용 표시 ===
            GameObject costObj = new GameObject("DetailCost");
            costObj.transform.SetParent(panel.transform, false);
            RectTransform costRt = costObj.AddComponent<RectTransform>();
            costRt.anchorMin = new Vector2(0f, 1f);
            costRt.anchorMax = new Vector2(1f, 1f);
            costRt.pivot = new Vector2(0.5f, 1f);
            costRt.anchoredPosition = new Vector2(0f, -220f);
            costRt.sizeDelta = new Vector2(0f, 35f);
            costRt.offsetMin = new Vector2(25f, costRt.offsetMin.y);
            costRt.offsetMax = new Vector2(-25f, costRt.offsetMax.y);
            Text costText = costObj.AddComponent<Text>();
            costText.font = font;
            costText.fontSize = 20;
            costText.alignment = TextAnchor.MiddleCenter;
            costText.color = new Color(1f, 0.84f, 0f);
            costText.raycastTarget = false;
            costText.text = "";

            // === 해금 버튼 ===
            GameObject unlockObj = new GameObject("UnlockButton");
            unlockObj.transform.SetParent(panel.transform, false);
            RectTransform unlockRt = unlockObj.AddComponent<RectTransform>();
            unlockRt.anchorMin = new Vector2(0.15f, 0f);
            unlockRt.anchorMax = new Vector2(0.85f, 0f);
            unlockRt.pivot = new Vector2(0.5f, 0f);
            unlockRt.anchoredPosition = new Vector2(0f, 50f);
            unlockRt.sizeDelta = new Vector2(0f, 55f);

            Image unlockBg = unlockObj.AddComponent<Image>();
            unlockBg.color = new Color(0.2f, 0.6f, 0.3f, 1f);

            Button unlockBtn = unlockObj.AddComponent<Button>();
            var unlockColors = unlockBtn.colors;
            unlockColors.highlightedColor = new Color(0.3f, 0.7f, 0.4f);
            unlockColors.pressedColor = new Color(0.15f, 0.4f, 0.2f);
            unlockColors.disabledColor = new Color(0.3f, 0.3f, 0.35f, 0.8f);
            unlockBtn.colors = unlockColors;

            GameObject unlockTextObj = new GameObject("Text");
            unlockTextObj.transform.SetParent(unlockObj.transform, false);
            RectTransform unlockTextRt = unlockTextObj.AddComponent<RectTransform>();
            unlockTextRt.anchorMin = Vector2.zero;
            unlockTextRt.anchorMax = Vector2.one;
            unlockTextRt.offsetMin = Vector2.zero;
            unlockTextRt.offsetMax = Vector2.zero;
            Text unlockText = unlockTextObj.AddComponent<Text>();
            unlockText.font = font;
            unlockText.fontSize = 22;
            unlockText.alignment = TextAnchor.MiddleCenter;
            unlockText.color = Color.white;
            unlockText.raycastTarget = false;
            unlockText.text = "해금";

            unlockBtn.onClick.AddListener(() =>
            {
                TryUnlockSelectedSkill(unlockBg, unlockText, unlockBtn);
            });

            // === 닫기 버튼 (X) ===
            GameObject closeObj = new GameObject("CloseButton");
            closeObj.transform.SetParent(panel.transform, false);
            RectTransform closeRt = closeObj.AddComponent<RectTransform>();
            closeRt.anchorMin = new Vector2(1f, 1f);
            closeRt.anchorMax = new Vector2(1f, 1f);
            closeRt.pivot = new Vector2(1f, 1f);
            closeRt.anchoredPosition = new Vector2(-8f, -8f);
            closeRt.sizeDelta = new Vector2(40f, 40f);

            Text closeText = closeObj.AddComponent<Text>();
            closeText.font = font;
            closeText.fontSize = 24;
            closeText.alignment = TextAnchor.MiddleCenter;
            closeText.color = new Color(0.8f, 0.6f, 0.6f, 0.9f);
            closeText.text = "\u2715"; // ✕

            Button closeBtn = closeObj.AddComponent<Button>();
            closeBtn.onClick.AddListener(() =>
            {
                detailPopup.SetActive(false);
            });

            detailPopup.SetActive(false);
        }

        private void ShowDetailPopup(SkillType skillType)
        {
            // ★ 스킬 해금 취소 모드: 해금된 스킬 클릭 시 즉시 잠금 처리
            var editorTest = FindObjectOfType<EditorTestSystem>();
            if (editorTest != null && editorTest.IsSkillUnlockCancelMode)
            {
                if (SkillTreeManager.Instance != null && SkillTreeManager.Instance.IsSkillUnlocked(skillType))
                {
                    SkillTreeManager.Instance.LockSkill(skillType);
                    RefreshAllNodes();
                    Debug.Log($"[SkillTreeUI] 스킬 취소: {skillType}");
                }
                return;
            }

            selectedSkill = skillType;
            var nodeData = SkillTreeDefinition.GetSkill(skillType);
            if (nodeData == null || detailPopup == null) return;

            // 텍스트 갱신
            Transform panel = detailPopup.transform.Find("DetailPanel");
            if (panel == null) return;

            var titleText = panel.Find("DetailTitle")?.GetComponent<Text>();
            var descText = panel.Find("DetailDesc")?.GetComponent<Text>();
            var usageText = panel.Find("DetailUsage")?.GetComponent<Text>();
            var costText = panel.Find("DetailCost")?.GetComponent<Text>();
            var unlockObj = panel.Find("UnlockButton");
            var unlockBg = unlockObj?.GetComponent<Image>();
            var unlockBtn = unlockObj?.GetComponent<Button>();
            var unlockText = unlockObj?.Find("Text")?.GetComponent<Text>();

            if (titleText != null) titleText.text = nodeData.skillName;
            if (descText != null) descText.text = nodeData.description;
            if (usageText != null) usageText.text = "◈ 사용법: " + nodeData.usageDescription;
            if (costText != null)
                costText.text = $"비용: SP {nodeData.skillPointCost}  |  골드 {nodeData.goldCost}";

            // 해금 버튼 상태 갱신
            var state = SkillTreeManager.Instance != null
                ? SkillTreeManager.Instance.GetSkillState(skillType)
                : SkillState.Locked;

            // ★ 레벨 게이팅 확인
            bool isLevelGated = SkillTreeManager.Instance != null
                && !SkillTreeManager.Instance.IsCategoryAvailable(skillType);
            int reqLevel = SkillTreeManager.Instance != null
                ? SkillTreeManager.Instance.GetCategoryRequiredLevel(skillType) : 0;

            if (unlockBtn != null && unlockBg != null && unlockText != null)
            {
                if (isLevelGated && reqLevel > 0)
                {
                    // ★ 레벨 게이트 잠김: 요구 레벨 표시
                    unlockText.text = $"Lv.{reqLevel} 필요";
                    unlockBg.color = new Color(0.4f, 0.25f, 0.15f, 0.9f);
                    unlockBtn.interactable = false;
                }
                else
                {
                    switch (state)
                    {
                        case SkillState.Unlocked:
                            unlockText.text = "해금 완료 ✓";
                            unlockBg.color = new Color(0.3f, 0.3f, 0.35f, 0.8f);
                            unlockBtn.interactable = false;
                            break;
                        case SkillState.Available:
                            bool canAfford = CanAffordSkill(nodeData);
                            unlockText.text = canAfford ? "해금" : "자원 부족";
                            unlockBg.color = canAfford
                                ? new Color(0.2f, 0.6f, 0.3f, 1f)
                                : new Color(0.5f, 0.3f, 0.2f, 0.9f);
                            unlockBtn.interactable = canAfford;
                            break;
                        case SkillState.Locked:
                            var prereqData = SkillTreeDefinition.GetSkill(nodeData.prerequisite);
                            string prereqName = prereqData != null ? prereqData.skillName : "???";
                            unlockText.text = $"선행: {prereqName}";
                            unlockBg.color = new Color(0.3f, 0.3f, 0.35f, 0.8f);
                            unlockBtn.interactable = false;
                            break;
                    }
                }
            }

            // 팝업 등장 애니메이션
            detailPopup.SetActive(true);
            detailPopup.transform.SetAsLastSibling();
            if (panel != null)
                StartCoroutine(PopupAppearAnimation(panel.gameObject));
        }

        private bool CanAffordSkill(SkillNodeData nodeData)
        {
            if (SkillTreeManager.Instance == null) return false;
            if (SkillTreeManager.Instance.SkillPoints < nodeData.skillPointCost) return false;
            if (GameManager.Instance != null && GameManager.Instance.CurrentGold < nodeData.goldCost) return false;
            return true;
        }

        private void TryUnlockSelectedSkill(Image unlockBg, Text unlockText, Button unlockBtn)
        {
            if (selectedSkill == SkillType.None || SkillTreeManager.Instance == null) return;

            bool success = SkillTreeManager.Instance.TryUnlockSkill(selectedSkill);

            if (success)
            {
                // 성공 피드백
                unlockText.text = "해금 완료 ✓";
                unlockBg.color = new Color(0.3f, 0.3f, 0.35f, 0.8f);
                unlockBtn.interactable = false;

                // 노드 갱신
                RefreshAllNodes();
                RefreshResourceDisplay();
                RefreshRealResetButton(); // 0 → 1개 해금 시 초기화 버튼 활성화

                // 성공 애니메이션
                if (skillNodes.ContainsKey(selectedSkill))
                    StartCoroutine(NodeUnlockAnimation(skillNodes[selectedSkill]));
            }
            else
            {
                // 실패 피드백 (흔들림)
                StartCoroutine(ShakeAnimation(unlockBg.gameObject));
            }
        }

        // ============================================================
        // 노드 상태 갱신
        // ============================================================

        private void RefreshAllNodes()
        {
            if (SkillTreeManager.Instance == null) return;

            foreach (var kvp in skillNodeBgs)
            {
                SkillType type = kvp.Key;
                Image bg = kvp.Value;
                Image border = skillNodeBorders.ContainsKey(type) ? skillNodeBorders[type] : null;

                var state = SkillTreeManager.Instance.GetSkillState(type);
                var nodeData = SkillTreeDefinition.GetSkill(type);

                // ★ 레벨 게이팅 체크: 카테고리 미해금이면 특별 표시
                bool isLevelGated = !SkillTreeManager.Instance.IsCategoryAvailable(type);
                int requiredLevel = SkillTreeManager.Instance.GetCategoryRequiredLevel(type);

                // 데미지 스킬은 필드 블록 아이콘과 동일한 색상 적용
                Color baseNodeColor = GetDamageSkillColor(type, nodeData);

                if (isLevelGated)
                {
                    // ★ 레벨 게이트 잠김: 매우 어둡고 회색 톤
                    bg.color = new Color(0.2f, 0.18f, 0.22f, 0.4f);
                    if (border != null) border.color = new Color(0.3f, 0.25f, 0.35f, 0.4f);
                }
                else
                {
                    switch (state)
                    {
                        case SkillState.Unlocked:
                            bg.color = baseNodeColor;
                            if (border != null) border.color = new Color(1f, 0.85f, 0.3f, 1f);
                            break;

                        case SkillState.Available:
                            bg.color = new Color(baseNodeColor.r * 0.7f, baseNodeColor.g * 0.7f, baseNodeColor.b * 0.7f, 0.9f);
                            if (border != null) border.color = new Color(0.8f, 0.8f, 0.9f, 0.9f);
                            break;

                        case SkillState.Locked:
                            bg.color = new Color(baseNodeColor.r * 0.4f, baseNodeColor.g * 0.4f, baseNodeColor.b * 0.4f, 0.5f);
                            if (border != null) border.color = new Color(0.4f, 0.35f, 0.45f, 0.5f);
                            break;
                    }
                }

                // 자물쇠 아이콘 표시/숨기기
                bool showLocked = (state == SkillState.Locked);
                bool showOpen = (state == SkillState.Available);

                if (lockedLockIcons.ContainsKey(type) && lockedLockIcons[type] != null)
                    lockedLockIcons[type].SetActive(showLocked);
                if (openLockIcons.ContainsKey(type) && openLockIcons[type] != null)
                    openLockIcons[type].SetActive(showOpen);

                // ★ 레벨 게이트 오버레이 텍스트 (Lv.XX 필요)
                UpdateLevelGateOverlay(type, isLevelGated, requiredLevel);

                // 폭탄 데미지 노드: 검정 배경 위에 v1/v2/v3 텍스트 표시
                if (type == SkillType.BombDamage1 || type == SkillType.BombDamage2 || type == SkillType.BombDamage3)
                {
                    int bombLevel = 0;
                    if (type == SkillType.BombDamage1) bombLevel = 1;
                    else if (type == SkillType.BombDamage2) bombLevel = 2;
                    else if (type == SkillType.BombDamage3) bombLevel = 3;

                    string vText = JewelsHexaPuzzle.Utils.BlockSkillColors.GetBombLevelText(bombLevel);
                    UpdateBombNodeText(skillNodes[type], vText, state);
                }
            }
        }

        /// <summary>
        /// 레벨 게이트 오버레이: 잠긴 스킬 노드(Lv1/Lv2/Lv3 모두)에 "Lv.XX" 텍스트 표시.
        /// 카테고리 게이트(Lv1) + 개별 스킬 게이트(Lv2/Lv3 자체 requiredLevel) 모두 적용.
        /// </summary>
        private void UpdateLevelGateOverlay(SkillType type, bool isGated, int requiredLevel)
        {
            if (!skillNodes.ContainsKey(type)) return;
            GameObject nodeObj = skillNodes[type];

            Transform existing = nodeObj.transform.Find("LevelGateText");

            // ★ 표시 조건 결정:
            //   1) Lv1: 카테고리 게이트(IsCategoryAvailable=false) → 카테고리 requiredLevel 표시
            //   2) Lv2/Lv3: 개별 스킬 requiredLevel > 도달 레벨 → 해당 requiredLevel 표시
            //   3) 그 외: 숨김
            int displayLevel = 0;
            var nodeData = SkillTreeDefinition.GetSkill(type);
            SkillType firstSkill = SkillUnlockSchedule.GetCategoryFirstSkill(type);

            if (type == firstSkill)
            {
                // Lv1: 카테고리 게이트 적용
                if (isGated && requiredLevel > 0) displayLevel = requiredLevel;
            }
            else if (nodeData != null && nodeData.requiredLevel > 0)
            {
                // Lv2/Lv3: 자체 requiredLevel과 도달 레벨 비교
                int reached = SkillTreeManager.Instance != null
                    ? SkillTreeManager.Instance.GetHighestReachedLevel() : 0;
                if (reached < nodeData.requiredLevel) displayLevel = nodeData.requiredLevel;
            }

            if (displayLevel <= 0)
            {
                if (existing != null) existing.gameObject.SetActive(false);
                return;
            }

            Text gateText;
            RectTransform gateRt;
            if (existing != null)
            {
                existing.gameObject.SetActive(true);
                gateText = existing.GetComponent<Text>();
                gateRt = existing.GetComponent<RectTransform>();
            }
            else
            {
                GameObject textObj = new GameObject("LevelGateText");
                textObj.transform.SetParent(nodeObj.transform, false);

                gateText = textObj.AddComponent<Text>();
                gateText.font = font;
                gateText.fontSize = 20;
                gateText.fontStyle = FontStyle.Bold;
                gateText.raycastTarget = false;

                Outline outline = textObj.AddComponent<Outline>();
                outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
                outline.effectDistance = new Vector2(1.5f, -1.5f);

                gateRt = textObj.GetComponent<RectTransform>();
            }

            // ★ 헥스 안쪽 하단에 배치 (하단 평행 라인에서 3px 위)
            //   flat-top 헥스: 중심~하단 변 거리 = (NODE_SIZE/2) × √3/2 ≈ 71.45 (NODE_SIZE=165)
            //   sprite 외곽 안전여유 2px 반영 → 실제 가시 하단 라인 ≈ -71
            //   text bottom = hex bottom + 3 = -68
            const float HEX_BOTTOM_Y = -71f;   // 헥스 하단 평행 변 위치 (음수)
            const float GAP_FROM_BOTTOM = 3f;  // 하단 라인에서 간격
            gateRt.anchorMin = new Vector2(0.5f, 0.5f);
            gateRt.anchorMax = new Vector2(0.5f, 0.5f);
            gateRt.pivot = new Vector2(0.5f, 0f); // 하단 기준
            gateRt.anchoredPosition = new Vector2(0f, HEX_BOTTOM_Y + GAP_FROM_BOTTOM);
            gateRt.sizeDelta = new Vector2(NODE_SIZE, 24f);
            gateText.alignment = TextAnchor.LowerCenter; // 글리프를 박스 하단에 정렬

            gateText.text = $"Lv.{displayLevel}";
            gateText.color = new Color(1f, 0.6f, 0.2f, 0.9f); // 주황색 (잠긴 느낌)
        }

        /// <summary>
        /// 폭탄 데미지 스킬 노드에 v1/v2/v3 텍스트 표시/갱신
        /// </summary>
        private void UpdateBombNodeText(GameObject nodeObj, string vText, SkillState state)
        {
            if (nodeObj == null) return;

            Transform existing = nodeObj.transform.Find("BombVersionText");
            Text bombText;

            if (existing != null)
            {
                bombText = existing.GetComponent<Text>();
            }
            else
            {
                GameObject textObj = new GameObject("BombVersionText");
                textObj.transform.SetParent(nodeObj.transform, false);

                bombText = textObj.AddComponent<Text>();
                bombText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                bombText.fontSize = 18;
                bombText.fontStyle = FontStyle.Bold;
                bombText.alignment = TextAnchor.MiddleCenter;
                bombText.raycastTarget = false;

                Outline outline = textObj.AddComponent<Outline>();
                outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
                outline.effectDistance = new Vector2(1f, -1f);

                RectTransform rt = textObj.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = new Vector2(60f, 30f);
            }

            bombText.text = vText;
            // 해금 → 흰색, 비해금 → 반투명 회색
            bombText.color = (state == SkillState.Unlocked)
                ? Color.white
                : new Color(0.6f, 0.6f, 0.6f, 0.5f);
        }

        /// <summary>
        /// 데미지 스킬 노드의 고유 색상 반환.
        /// BlockSkillColors 중앙 색상 상수 참조 — 인게임 블록 아이콘과 동일 색상 보장.
        /// 데미지 스킬이 아니면 기존 nodeColor 반환.
        /// </summary>
        private Color GetDamageSkillColor(SkillType type, SkillNodeData nodeData)
        {
            switch (type)
            {
                // 드릴 데미지 (공통 색상)
                case SkillType.DrillDamage1: return JewelsHexaPuzzle.Utils.BlockSkillColors.CommonLevel1;
                case SkillType.DrillDamage2: return JewelsHexaPuzzle.Utils.BlockSkillColors.CommonLevel2;
                case SkillType.DrillDamage3: return JewelsHexaPuzzle.Utils.BlockSkillColors.CommonLevel3;

                // 폭탄 데미지 (검정 배경)
                case SkillType.BombDamage1: return JewelsHexaPuzzle.Utils.BlockSkillColors.BombLevel1;
                case SkillType.BombDamage2: return JewelsHexaPuzzle.Utils.BlockSkillColors.BombLevel2;
                case SkillType.BombDamage3: return JewelsHexaPuzzle.Utils.BlockSkillColors.BombLevel3;

                // 드론 타겟 데미지 (공통 색상)
                case SkillType.DroneTargetDamage1: return JewelsHexaPuzzle.Utils.BlockSkillColors.CommonLevel1;
                case SkillType.DroneTargetDamage2: return JewelsHexaPuzzle.Utils.BlockSkillColors.CommonLevel2;
                case SkillType.DroneTargetDamage3: return JewelsHexaPuzzle.Utils.BlockSkillColors.CommonLevel3;

                // 기타 스킬: 기존 nodeColor 유지
                default:
                    return nodeData != null ? nodeData.nodeColor : Color.gray;
            }
        }

        // ============================================================
        // 애니메이션
        // ============================================================

        private IEnumerator PopupAppearAnimation(GameObject panel)
        {
            RectTransform rt = panel.GetComponent<RectTransform>();
            if (rt == null) yield break;

            float duration = 0.25f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                // 오버슈트 이징
                float scale = 1f + (1.08f - 1f) * (1f - t) + (t < 0.7f ? 0.08f * Mathf.Sin(t / 0.7f * Mathf.PI) : 0f);
                if (t >= 0.7f) scale = 1f;
                rt.localScale = Vector3.one * Mathf.Lerp(0f, scale, t);
                yield return null;
            }
            rt.localScale = Vector3.one;
        }

        private IEnumerator NodeUnlockAnimation(GameObject node)
        {
            if (node == null) yield break;
            RectTransform rt = node.GetComponent<RectTransform>();
            if (rt == null) yield break;

            // 스케일 펀치: 1→1.25→1
            float duration = 0.3f;
            float elapsed = 0f;
            Vector3 origScale = rt.localScale;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float punch = Mathf.Sin(t * Mathf.PI) * 0.25f;
                rt.localScale = origScale * (1f + punch);
                yield return null;
            }
            rt.localScale = origScale;
        }

        private IEnumerator ShakeAnimation(GameObject obj)
        {
            if (obj == null) yield break;
            RectTransform rt = obj.GetComponent<RectTransform>();
            if (rt == null) yield break;

            Vector2 origPos = rt.anchoredPosition;
            float duration = 0.3f;
            float elapsed = 0f;
            float intensity = 6f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / duration;
                float offset = Mathf.Sin(t * Mathf.PI * 5f) * intensity * (1f - t);
                rt.anchoredPosition = origPos + new Vector2(offset, 0f);
                yield return null;
            }
            rt.anchoredPosition = origPos;
        }

        // ============================================================
        // 이벤트 구독
        // ============================================================

        private void OnEnable()
        {
            if (SkillTreeManager.Instance != null)
            {
                SkillTreeManager.Instance.OnSkillPointsChanged += OnSPChanged;
                SkillTreeManager.Instance.OnSkillUnlocked += OnSkillUnlockedHandler;
                SkillTreeManager.Instance.OnSkillTreeReset += OnSkillTreeResetHandler;
            }
            if (GameManager.Instance != null)
                GameManager.Instance.OnGoldChanged += OnGoldChanged;
        }

        private void OnDisable()
        {
            if (SkillTreeManager.Instance != null)
            {
                SkillTreeManager.Instance.OnSkillPointsChanged -= OnSPChanged;
                SkillTreeManager.Instance.OnSkillUnlocked -= OnSkillUnlockedHandler;
                SkillTreeManager.Instance.OnSkillTreeReset -= OnSkillTreeResetHandler;
            }
            if (GameManager.Instance != null)
                GameManager.Instance.OnGoldChanged -= OnGoldChanged;
        }

        private void OnSPChanged(int sp)
        {
            if (spText != null) JewelsHexaPuzzle.Utils.NumberRoller.Roll(spText, sp, v => v.ToString());
        }

        private void OnGoldChanged(int gold)
        {
            if (goldText != null) JewelsHexaPuzzle.Utils.NumberRoller.Roll(goldText, gold, v => v.ToString(), 0.7f, 0.35f);
        }

        private void OnSkillUnlockedHandler(SkillType type)
        {
            RefreshAllNodes();
        }

        private void OnSkillTreeResetHandler()
        {
            RefreshAllNodes();
            RefreshResourceDisplay();
        }
    }
}
