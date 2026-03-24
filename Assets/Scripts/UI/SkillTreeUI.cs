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

        // 레이아웃 상수
        private const float NODE_SIZE = 110f;
        private const float NODE_SPACING_X = 160f;
        private const float LINE_THICKNESS = 4f;

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
            }
        }

        public void Hide()
        {
            if (rootContainer != null)
                rootContainer.SetActive(false);
            if (detailPopup != null)
                detailPopup.SetActive(false);
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

            // === 에디터 전용 디버그 버튼 ===
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
            var allSkills = SkillTreeDefinition.GetAllSkills();

            // 노드 컨테이너 (중앙 정렬)
            GameObject nodesContainer = new GameObject("SkillNodesContainer");
            nodesContainer.transform.SetParent(rootContainer.transform, false);
            RectTransform containerRt = nodesContainer.AddComponent<RectTransform>();
            containerRt.anchorMin = new Vector2(0.5f, 0.5f);
            containerRt.anchorMax = new Vector2(0.5f, 0.5f);
            containerRt.pivot = new Vector2(0.5f, 0.5f);
            containerRt.anchoredPosition = new Vector2(0f, 60f);
            containerRt.sizeDelta = new Vector2(800f, 400f);

            // 드릴 스킬 체인: 좌→우 배치
            // 첫 노드는 좌측 시작, 이후 오른쪽으로 진행
            float startX = -(NODE_SPACING_X);  // 왼쪽부터
            float y = 0f;

            // "드릴" 카테고리 라벨
            GameObject catLabel = new GameObject("DrillCategoryLabel");
            catLabel.transform.SetParent(nodesContainer.transform, false);
            RectTransform catRt = catLabel.AddComponent<RectTransform>();
            catRt.anchoredPosition = new Vector2(startX - NODE_SIZE * 0.5f - 20f, y + NODE_SIZE * 0.5f + 30f);
            catRt.sizeDelta = new Vector2(200f, 30f);
            Text catText = catLabel.AddComponent<Text>();
            catText.font = font;
            catText.fontSize = 20;
            catText.alignment = TextAnchor.MiddleLeft;
            catText.color = new Color(0.7f, 0.85f, 1f, 0.9f);
            catText.raycastTarget = false;
            catText.text = "◆ 드릴 스킬";

            for (int i = 0; i < allSkills.Count; i++)
            {
                var skill = allSkills[i];
                float x = startX + i * NODE_SPACING_X;

                // 연결 라인 (이전 노드와)
                if (i > 0)
                {
                    float prevX = startX + (i - 1) * NODE_SPACING_X;
                    CreateConnectionLine(nodesContainer.transform, prevX, y, x, y);
                }

                // 스킬 노드 생성
                CreateSkillNode(nodesContainer.transform, skill, new Vector2(x, y));
            }

            // === 자물쇠 아이콘 생성 (노드와 별개로 nodesContainer 자식) ===
            // connectionLines[i-1]이 skill[i-1]→skill[i]를 연결
            for (int i = 0; i < allSkills.Count; i++)
            {
                var skill = allSkills[i];
                Vector2 lockPos;

                if (i > 0 && connectionLines.Count >= i)
                {
                    // 연결 라인의 오른쪽 끝점 + 8px
                    RectTransform lineRt = connectionLines[i - 1].GetComponent<RectTransform>();
                    float lineRightX = lineRt.anchoredPosition.x + lineRt.sizeDelta.x * 0.5f;
                    float lineY = lineRt.anchoredPosition.y;
                    lockPos = new Vector2(lineRightX + 17f, lineY);
                }
                else
                {
                    // 첫 번째 노드: 노드 왼쪽 끝에서 왼쪽으로 배치
                    float nodeX = startX;
                    lockPos = new Vector2(nodeX - NODE_SIZE * 0.5f - 5f, y);
                }

                CreateLockIconPair(nodesContainer.transform, skill.skillType, lockPos);
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

            // === 아이콘 (드릴 프로시저럴) ===
            CreateDrillIcon(nodeObj.transform, skillData);

            // === 스킬 이름 텍스트 (노드 아래) ===
            GameObject nameObj = new GameObject("SkillName");
            nameObj.transform.SetParent(nodeObj.transform, false);
            RectTransform nameRt = nameObj.AddComponent<RectTransform>();
            nameRt.anchoredPosition = new Vector2(0f, -NODE_SIZE * 0.5f - 18f);
            nameRt.sizeDelta = new Vector2(140f, 28f);
            Text nameText = nameObj.AddComponent<Text>();
            nameText.font = font;
            nameText.fontSize = 16;
            nameText.alignment = TextAnchor.MiddleCenter;
            nameText.color = new Color(0.85f, 0.85f, 0.95f, 0.9f);
            nameText.raycastTarget = false;
            nameText.text = skillData.skillName;

            // === 비용 텍스트 (이름 아래) ===
            GameObject costObj = new GameObject("SkillCost");
            costObj.transform.SetParent(nodeObj.transform, false);
            RectTransform costRt = costObj.AddComponent<RectTransform>();
            costRt.anchoredPosition = new Vector2(0f, -NODE_SIZE * 0.5f - 38f);
            costRt.sizeDelta = new Vector2(160f, 22f);
            Text costText = costObj.AddComponent<Text>();
            costText.font = font;
            costText.fontSize = 13;
            costText.alignment = TextAnchor.MiddleCenter;
            costText.color = new Color(0.7f, 0.7f, 0.7f, 0.8f);
            costText.raycastTarget = false;
            costText.text = $"SP:{skillData.skillPointCost}  골드:{skillData.goldCost}";

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
        /// 자물쇠 아이콘 한 쌍 생성 (잠긴 + 열린)
        /// 부모는 nodesContainer (노드 바깥 독립 배치)
        /// lockPos = 연결 라인 오른쪽 끝점 + 8px 위치
        /// </summary>
        private void CreateLockIconPair(Transform parent, SkillType skillType, Vector2 lockPos)
        {
            // 공통 크기
            Vector2 iconSize = new Vector2(28f, 34f);

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
        private void CreateDrillIcon(Transform parent, SkillNodeData skillData)
        {
            // 드릴 본체 (세로 직사각형)
            GameObject body = new GameObject("DrillBody");
            body.transform.SetParent(parent, false);
            Image bodyImg = body.AddComponent<Image>();
            bodyImg.color = new Color(0.9f, 0.9f, 0.95f, 0.95f);
            bodyImg.raycastTarget = false;
            RectTransform bodyRt = body.GetComponent<RectTransform>();
            bodyRt.anchoredPosition = new Vector2(0f, 4f);
            bodyRt.sizeDelta = new Vector2(14f, 30f);

            // 드릴 끝 (삼각형 → 역삼각 텍스트로 근사)
            GameObject tip = new GameObject("DrillTip");
            tip.transform.SetParent(parent, false);
            Text tipText = tip.AddComponent<Text>();
            tipText.font = font;
            tipText.fontSize = 22;
            tipText.alignment = TextAnchor.MiddleCenter;
            tipText.color = new Color(0.9f, 0.9f, 0.95f, 0.95f);
            tipText.raycastTarget = false;
            tipText.text = "\u25BC"; // ▼
            RectTransform tipRt = tip.GetComponent<RectTransform>();
            tipRt.anchoredPosition = new Vector2(0f, -18f);
            tipRt.sizeDelta = new Vector2(30f, 25f);

            // 이동 범위 표시 (화살표 수)
            GameObject rangeObj = new GameObject("MoveRange");
            rangeObj.transform.SetParent(parent, false);
            Text rangeText = rangeObj.AddComponent<Text>();
            rangeText.font = font;
            rangeText.fontSize = 14;
            rangeText.alignment = TextAnchor.MiddleCenter;
            rangeText.color = new Color(1f, 1f, 0.7f, 0.9f);
            rangeText.raycastTarget = false;
            rangeText.text = $"{skillData.drillMoveRange}칸";
            RectTransform rangeRt = rangeObj.GetComponent<RectTransform>();
            rangeRt.anchoredPosition = new Vector2(0f, 26f);
            rangeRt.sizeDelta = new Vector2(50f, 20f);

            Outline rangeOutline = rangeObj.AddComponent<Outline>();
            rangeOutline.effectColor = new Color(0f, 0f, 0f, 0.6f);
            rangeOutline.effectDistance = new Vector2(1f, 1f);
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
        // 에디터 전용 디버그 버튼
        // ============================================================

#if UNITY_EDITOR
        private void CreateDebugButtons()
        {
            // SP 추가 버튼 (좌측 하단)
            GameObject addSPObj = new GameObject("DebugAddSP");
            addSPObj.transform.SetParent(rootContainer.transform, false);
            RectTransform addSPRt = addSPObj.AddComponent<RectTransform>();
            addSPRt.anchorMin = new Vector2(0f, 0f);
            addSPRt.anchorMax = new Vector2(0f, 0f);
            addSPRt.pivot = new Vector2(0f, 0f);
            addSPRt.anchoredPosition = new Vector2(20f, 80f);
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

            // 스킬 초기화 버튼
            GameObject resetObj = new GameObject("DebugResetSkills");
            resetObj.transform.SetParent(rootContainer.transform, false);
            RectTransform resetRt = resetObj.AddComponent<RectTransform>();
            resetRt.anchorMin = new Vector2(0f, 0f);
            resetRt.anchorMax = new Vector2(0f, 0f);
            resetRt.pivot = new Vector2(0f, 0f);
            resetRt.anchoredPosition = new Vector2(20f, 20f);
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

            if (unlockBtn != null && unlockBg != null && unlockText != null)
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

                switch (state)
                {
                    case SkillState.Unlocked:
                        // 밝고 채도 높은 색상 + 금색 테두리
                        bg.color = nodeData != null ? nodeData.nodeColor : Color.cyan;
                        if (border != null) border.color = new Color(1f, 0.85f, 0.3f, 1f);
                        break;

                    case SkillState.Available:
                        // 보통 밝기 + 흰색 테두리
                        Color availColor = nodeData != null ? nodeData.nodeColor : Color.gray;
                        bg.color = new Color(availColor.r * 0.7f, availColor.g * 0.7f, availColor.b * 0.7f, 0.9f);
                        if (border != null) border.color = new Color(0.8f, 0.8f, 0.9f, 0.9f);
                        break;

                    case SkillState.Locked:
                        // 어두운 회색 + 어두운 테두리
                        bg.color = new Color(0.25f, 0.22f, 0.3f, 0.7f);
                        if (border != null) border.color = new Color(0.4f, 0.35f, 0.45f, 0.5f);
                        break;
                }

                // 자물쇠 아이콘 표시/숨기기
                // Locked → 잠긴 자물쇠, Available → 열린 자물쇠, Unlocked → 없음
                bool showLocked = (state == SkillState.Locked);
                bool showOpen = (state == SkillState.Available);

                if (lockedLockIcons.ContainsKey(type) && lockedLockIcons[type] != null)
                    lockedLockIcons[type].SetActive(showLocked);
                if (openLockIcons.ContainsKey(type) && openLockIcons[type] != null)
                    openLockIcons[type].SetActive(showOpen);
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
            if (spText != null) spText.text = sp.ToString();
        }

        private void OnGoldChanged(int gold)
        {
            if (goldText != null) goldText.text = gold.ToString();
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
