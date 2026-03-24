using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using JewelsHexaPuzzle.Data;
using JewelsHexaPuzzle.Core;
using JewelsHexaPuzzle.Managers;

namespace JewelsHexaPuzzle.UI
{
    /// <summary>
    /// 로비 스테이지 선택 스크롤 UI를 코드로 완전히 구축하는 빌더.
    /// ScrollRect + Viewport(RectMask2D) + Content(VerticalLayoutGroup + ContentSizeFitter)
    /// 구조를 생성하고, 3열 그리드로 스테이지 버튼을 배치한다.
    /// </summary>
    public class StageScrollBuilder : MonoBehaviour
    {
        // === 내부 참조 ===
        private ScrollRect scrollRect;
        private RectTransform contentRt;
        private RectTransform viewportRt;

        // === 설정 ===
        private const int COLUMNS = 3;
        private const float BUTTON_SIZE = 200f;
        private const float BUTTON_GAP = 25f;
        private const float ROW_SPACING = 8f;
        private const int PADDING = 16;

        // === 외부 참조 ===
        private ScoreManager scoreManager;
        private Font font;
        private Action<int> onStageSelected; // 버튼 클릭 콜백

        // === 공개 접근 ===
        public RectTransform ContentTransform => contentRt;
        public ScrollRect ScrollRectRef => scrollRect;

        /// <summary>
        /// 스크롤 시스템 구축 메인 진입점.
        /// GameManager에서 호출하여 모든 스크롤 UI를 생성한다.
        /// </summary>
        public void Build(ScoreManager scoreMgr, Font f, Action<int> onSelect)
        {
            scoreManager = scoreMgr;
            font = f;
            onStageSelected = onSelect;

            BuildScrollStructure();
            PopulateButtons();
        }

        /// <summary>
        /// 스크롤 위치를 가장 높은 언락 레벨로 이동 (1프레임 대기 후)
        /// </summary>
        public void ScrollToHighestUnlocked()
        {
            StartCoroutine(ScrollToHighestUnlockedCoroutine());
        }

        /// <summary>
        /// 최고 점수 텍스트 갱신
        /// </summary>
        public void RefreshHighScores()
        {
            if (contentRt == null || scoreManager == null) return;

            var allLevels = LevelRegistry.GetAllLevels();
            for (int i = 0; i < allLevels.Count; i++)
            {
                int stageNum = allLevels[i].levelId;
                Transform stageBtn = contentRt.Find($"Row{i / COLUMNS}/Stage{stageNum}Button");
                if (stageBtn == null) continue;

                int levelBest = scoreManager.GetLevelHighScore(stageNum);
                int personalBest = scoreManager.GetPersonalLevelBest(stageNum);

                Transform lbTr = stageBtn.Find("LevelBest");
                Transform pbTr = stageBtn.Find("PersonalBest");

                if (lbTr != null)
                {
                    Text lbText = lbTr.GetComponent<Text>();
                    if (lbText != null)
                        lbText.text = levelBest > 0 ? string.Format("BEST: {0:N0}", levelBest) : "";
                }
                else if (levelBest > 0 && !allLevels[i].isLocked)
                {
                    // PlayIcon → 점수 표시로 교체
                    float btnSize = stageBtn.GetComponent<RectTransform>().sizeDelta.x;
                    Transform playIcon = stageBtn.Find("PlayIcon");
                    if (playIcon != null) Destroy(playIcon.gameObject);

                    CreateScoreTexts(stageBtn.gameObject, btnSize, levelBest, personalBest);
                }

                if (pbTr != null)
                {
                    Text pbText = pbTr.GetComponent<Text>();
                    if (pbText != null)
                        pbText.text = personalBest > 0 ? string.Format("MY: {0:N0}", personalBest) : "";
                }
            }
        }

        // ============================================================
        // 스크롤 구조 생성
        // ============================================================

        /// <summary>
        /// 부모(lobbyContainer) 안의 타이틀/하단 버튼 위치를 읽어 루트 RectTransform 영역 계산
        /// </summary>
        private void AdjustRootBounds(RectTransform rootRt)
        {
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;

            Transform parent = transform.parent;

            // --- 상단 경계: 타이틀 오브젝트의 하단 ---
            float topOffset = -200f; // 폴백: 화면 상단에서 200px 아래
            if (parent != null)
            {
                Transform titleTr = parent.Find("LobbyTitle");
                if (titleTr != null)
                {
                    RectTransform titleRt = titleTr.GetComponent<RectTransform>();
                    if (titleRt != null)
                    {
                        // anchor top, pivot top → 하단 Y = anchoredPosition.y - sizeDelta.y
                        float titleBottom = titleRt.anchoredPosition.y - titleRt.sizeDelta.y;
                        topOffset = titleBottom - 10f; // 타이틀 하단에서 10px 아래 여유
                    }
                }
            }

            // --- 하단 경계: 하단 버튼 중 가장 높은 상단 ---
            float bottomOffset = 170f; // 폴백: 화면 하단에서 170px 위
            if (parent != null)
            {
                // 하단 버튼들: SkillTreeButton (80,80 size 80x80), TutorialResetButton (20,20 size 180x45)
                string[] bottomNames = { "SkillTreeButton", "TutorialResetButton", "UnlockAllButton" };
                float highestTop = 0f;
                foreach (string name in bottomNames)
                {
                    Transform btnTr = parent.Find(name);
                    if (btnTr != null)
                    {
                        RectTransform btnRt = btnTr.GetComponent<RectTransform>();
                        if (btnRt != null)
                        {
                            // anchor bottom-left, pivot bottom-left → 상단 Y = anchoredPosition.y + sizeDelta.y
                            float btnTop = btnRt.anchoredPosition.y + btnRt.sizeDelta.y;
                            if (btnTop > highestTop) highestTop = btnTop;
                        }
                    }
                }
                if (highestTop > 0f)
                    bottomOffset = highestTop + 10f; // 버튼 상단에서 10px 위 여유
            }

            rootRt.offsetMin = new Vector2(20f, bottomOffset);  // 좌 20, 하단
            rootRt.offsetMax = new Vector2(-20f, topOffset);     // 우 -20, 상단
        }

        private void BuildScrollStructure()
        {
            // === 루트 (this 오브젝트) — ScrollRect ===
            // anchor stretch, offset은 GameManager에서 설정된 값을 유지
            RectTransform rootRt = GetComponent<RectTransform>();
            if (rootRt == null) rootRt = gameObject.AddComponent<RectTransform>();

            // 부모(lobbyContainer) 안의 타이틀/하단 버튼 위치를 읽어 동적 계산
            AdjustRootBounds(rootRt);

            scrollRect = gameObject.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Elastic;
            scrollRect.elasticity = 0.1f;
            scrollRect.inertia = true;
            scrollRect.decelerationRate = 0.135f;
            scrollRect.scrollSensitivity = 20f;

            // === Viewport — RectMask2D ===
            GameObject viewportObj = new GameObject("Viewport");
            viewportObj.transform.SetParent(transform, false);
            viewportRt = viewportObj.AddComponent<RectTransform>();
            viewportRt.anchorMin = Vector2.zero;
            viewportRt.anchorMax = Vector2.one;
            viewportRt.pivot = new Vector2(0.5f, 0.5f);
            viewportRt.offsetMin = Vector2.zero;
            viewportRt.offsetMax = Vector2.zero;
            // RectMask2D: Mask보다 가볍고, 별도 Image 불필요
            viewportObj.AddComponent<RectMask2D>();

            scrollRect.viewport = viewportRt;

            // === Content — VerticalLayoutGroup + ContentSizeFitter ===
            GameObject contentObj = new GameObject("Content");
            contentObj.transform.SetParent(viewportObj.transform, false);
            contentRt = contentObj.AddComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.anchoredPosition = Vector2.zero;

            VerticalLayoutGroup vlg = contentObj.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.spacing = ROW_SPACING;
            vlg.padding = new RectOffset(PADDING, PADDING, PADDING, PADDING);

            ContentSizeFitter csf = contentObj.AddComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.content = contentRt;
        }

        // ============================================================
        // 버튼 배치
        // ============================================================

        private void PopulateButtons()
        {
            var allLevels = LevelRegistry.GetAllLevels();
            int totalLevels = allLevels.Count;
            int rows = Mathf.CeilToInt((float)totalLevels / COLUMNS);

            for (int row = 0; row < rows; row++)
            {
                // 행 컨테이너 생성
                GameObject rowObj = new GameObject($"Row{row}");
                rowObj.transform.SetParent(contentRt, false);

                // 행 높이를 LayoutElement로 지정
                LayoutElement rowLE = rowObj.AddComponent<LayoutElement>();
                rowLE.preferredHeight = BUTTON_SIZE + BUTTON_GAP;
                rowLE.flexibleWidth = 1f;

                // 행 내부 수평 배치
                HorizontalLayoutGroup hlg = rowObj.AddComponent<HorizontalLayoutGroup>();
                hlg.childAlignment = TextAnchor.MiddleCenter;
                hlg.childControlWidth = false;
                hlg.childControlHeight = false;
                hlg.childForceExpandWidth = false;
                hlg.childForceExpandHeight = false;
                hlg.spacing = BUTTON_GAP;
                hlg.padding = new RectOffset(0, 0, 0, 0);

                // 이 행에 속하는 레벨 버튼 생성
                for (int col = 0; col < COLUMNS; col++)
                {
                    int idx = row * COLUMNS + col;
                    if (idx >= totalLevels) break;

                    var level = allLevels[idx];
                    var display = level.lobbyDisplay ?? new LobbyDisplayConfig
                    {
                        backgroundColor = new Color(0.3f, 0.3f, 0.5f),
                        borderColor = new Color(0.5f, 0.5f, 0.7f),
                        buttonSize = BUTTON_SIZE
                    };

                    float btnSize = display.buttonSize > 0 ? display.buttonSize : BUTTON_SIZE;

                    CreateStageButton(
                        rowObj,
                        level.levelName,
                        level.subtitle ?? "",
                        display.backgroundColor,
                        display.borderColor,
                        level.levelId,
                        btnSize,
                        level.isLocked,
                        level.difficultyType
                    );
                }
            }
        }

        // ============================================================
        // 스테이지 버튼 생성
        // ============================================================

        private void CreateStageButton(GameObject parent, string stageLabel, string subtitle,
            Color bgColor, Color borderColor, int stageNum, float btnSize, bool isLocked,
            DifficultyType difficultyType)
        {
            GameObject stageBtn = new GameObject($"Stage{stageNum}Button");
            stageBtn.transform.SetParent(parent.transform, false);

            // LayoutElement로 크기 지정 (LayoutGroup이 관리)
            LayoutElement le = stageBtn.AddComponent<LayoutElement>();
            le.preferredWidth = btnSize;
            le.preferredHeight = btnSize;

            RectTransform stageBtnRt = stageBtn.GetComponent<RectTransform>();
            if (stageBtnRt == null) stageBtnRt = stageBtn.AddComponent<RectTransform>();
            stageBtnRt.sizeDelta = new Vector2(btnSize, btnSize);

            // 잠긴 레벨: 색상 어둡게
            Color displayBg = isLocked ? bgColor * 0.4f : bgColor;
            Color displayBorder = isLocked ? borderColor * 0.4f : borderColor;

            // 육각형 배경
            Image hexBg = stageBtn.AddComponent<Image>();
            hexBg.sprite = HexBlock.GetHexFlashSprite();
            hexBg.color = displayBg;
            hexBg.type = Image.Type.Simple;
            hexBg.preserveAspect = true;

            Button btn = stageBtn.AddComponent<Button>();
            var btnColors = btn.colors;
            btnColors.highlightedColor = displayBg * 1.3f;
            btnColors.pressedColor = displayBg * 0.7f;
            btn.colors = btnColors;
            btn.targetGraphic = hexBg;

            if (isLocked)
                btn.interactable = false;

            // 육각형 테두리
            GameObject borderObj = new GameObject("HexBorder");
            borderObj.transform.SetParent(stageBtn.transform, false);
            RectTransform borderRt = borderObj.AddComponent<RectTransform>();
            borderRt.anchorMin = Vector2.zero;
            borderRt.anchorMax = Vector2.one;
            borderRt.offsetMin = new Vector2(-8f, -8f);
            borderRt.offsetMax = new Vector2(8f, 8f);
            Image borderImg = borderObj.AddComponent<Image>();
            borderImg.sprite = HexBlock.GetHexBorderSprite();
            borderImg.color = displayBorder;
            borderImg.type = Image.Type.Simple;
            borderImg.preserveAspect = true;
            borderImg.raycastTarget = false;

            // 스테이지 텍스트
            GameObject stageTextObj = new GameObject("StageText");
            stageTextObj.transform.SetParent(stageBtn.transform, false);
            RectTransform stageTextRt = stageTextObj.AddComponent<RectTransform>();
            stageTextRt.anchorMin = new Vector2(0.5f, 0.5f);
            stageTextRt.anchorMax = new Vector2(0.5f, 0.5f);
            stageTextRt.pivot = new Vector2(0.5f, 0.5f);
            stageTextRt.anchoredPosition = new Vector2(0f, 20f);
            stageTextRt.sizeDelta = new Vector2(btnSize * 0.9f, 36f);
            Text stageText = stageTextObj.AddComponent<Text>();
            stageText.font = font;
            stageText.fontSize = isLocked ? 18 : 28;
            stageText.alignment = TextAnchor.MiddleCenter;
            stageText.color = isLocked ? new Color(0.5f, 0.5f, 0.6f) : new Color(0.85f, 0.9f, 1f);
            stageText.raycastTarget = false;
            stageText.text = isLocked ? "\uD83D\uDD12" : stageLabel;

            if (!isLocked)
            {
                int levelBest = scoreManager != null ? scoreManager.GetLevelHighScore(stageNum) : 0;
                int personalBest = scoreManager != null ? scoreManager.GetPersonalLevelBest(stageNum) : 0;

                if (levelBest > 0 || personalBest > 0)
                {
                    CreateScoreTexts(stageBtn, btnSize, levelBest, personalBest);
                }
                else
                {
                    // 재생 아이콘 표시
                    GameObject playObj = new GameObject("PlayIcon");
                    playObj.transform.SetParent(stageBtn.transform, false);
                    RectTransform playRt = playObj.AddComponent<RectTransform>();
                    playRt.anchorMin = new Vector2(0.5f, 0.5f);
                    playRt.anchorMax = new Vector2(0.5f, 0.5f);
                    playRt.pivot = new Vector2(0.5f, 0.5f);
                    playRt.anchoredPosition = new Vector2(0f, -15f);
                    playRt.sizeDelta = new Vector2(40f, 40f);
                    Text playText = playObj.AddComponent<Text>();
                    playText.font = font;
                    playText.fontSize = 32;
                    playText.alignment = TextAnchor.MiddleCenter;
                    playText.color = Color.white;
                    playText.raycastTarget = false;
                    playText.text = "\u25B6";
                }
            }

            // 부제 텍스트
            GameObject subtitleObj = new GameObject("Subtitle");
            subtitleObj.transform.SetParent(stageBtn.transform, false);
            RectTransform subtitleRt = subtitleObj.AddComponent<RectTransform>();
            subtitleRt.anchorMin = new Vector2(0.5f, 0.5f);
            subtitleRt.anchorMax = new Vector2(0.5f, 0.5f);
            subtitleRt.pivot = new Vector2(0.5f, 0.5f);
            subtitleRt.anchoredPosition = new Vector2(0f, -48f);
            subtitleRt.sizeDelta = new Vector2(btnSize * 0.95f, 20f);
            Text subtitleText = subtitleObj.AddComponent<Text>();
            subtitleText.font = font;
            subtitleText.fontSize = 10;
            subtitleText.alignment = TextAnchor.MiddleCenter;
            subtitleText.color = isLocked ? new Color(0.4f, 0.4f, 0.5f) : new Color(0.7f, 0.7f, 0.8f);
            subtitleText.raycastTarget = false;
            subtitleText.text = subtitle;

            // 난이도 별 표시
            CreateDifficultyStars(stageBtn, btnSize, isLocked, difficultyType);

            // 버튼 클릭 콜백
            int capturedStageNum = stageNum;
            btn.onClick.AddListener(() =>
            {
                if (AudioManager.Instance != null) AudioManager.Instance.PlayButtonClick();
                onStageSelected?.Invoke(capturedStageNum);
            });
        }

        // ============================================================
        // 헬퍼 메서드
        // ============================================================

        private void CreateScoreTexts(GameObject stageBtn, float btnSize, int levelBest, int personalBest)
        {
            // BEST (전체 유저 최고)
            GameObject lbObj = new GameObject("LevelBest");
            lbObj.transform.SetParent(stageBtn.transform, false);
            RectTransform lbRt = lbObj.AddComponent<RectTransform>();
            lbRt.anchorMin = new Vector2(0.5f, 0.5f);
            lbRt.anchorMax = new Vector2(0.5f, 0.5f);
            lbRt.pivot = new Vector2(0.5f, 0.5f);
            lbRt.anchoredPosition = new Vector2(0f, -12f);
            lbRt.sizeDelta = new Vector2(btnSize * 0.9f, 18f);
            Text lbText = lbObj.AddComponent<Text>();
            lbText.font = font;
            lbText.fontSize = 12;
            lbText.alignment = TextAnchor.MiddleCenter;
            lbText.color = new Color(1f, 0.85f, 0.3f, 0.95f);
            lbText.raycastTarget = false;
            lbText.text = levelBest > 0 ? string.Format("BEST: {0:N0}", levelBest) : "";
            Outline lbOutline = lbObj.AddComponent<Outline>();
            lbOutline.effectColor = new Color(0f, 0f, 0f, 0.8f);
            lbOutline.effectDistance = new Vector2(1, 1);

            // MY BEST (개인 최고)
            GameObject pbObj = new GameObject("PersonalBest");
            pbObj.transform.SetParent(stageBtn.transform, false);
            RectTransform pbRt = pbObj.AddComponent<RectTransform>();
            pbRt.anchorMin = new Vector2(0.5f, 0.5f);
            pbRt.anchorMax = new Vector2(0.5f, 0.5f);
            pbRt.pivot = new Vector2(0.5f, 0.5f);
            pbRt.anchoredPosition = new Vector2(0f, -28f);
            pbRt.sizeDelta = new Vector2(btnSize * 0.9f, 16f);
            Text pbText = pbObj.AddComponent<Text>();
            pbText.font = font;
            pbText.fontSize = 10;
            pbText.alignment = TextAnchor.MiddleCenter;
            pbText.color = new Color(0.7f, 0.9f, 1f, 0.85f);
            pbText.raycastTarget = false;
            pbText.text = personalBest > 0 ? string.Format("MY: {0:N0}", personalBest) : "";
            Outline pbOutline = pbObj.AddComponent<Outline>();
            pbOutline.effectColor = new Color(0f, 0f, 0f, 0.7f);
            pbOutline.effectDistance = new Vector2(1, 1);
        }

        private void CreateDifficultyStars(GameObject stageBtn, float btnSize, bool isLocked, DifficultyType difficultyType)
        {
            int starCount;
            Color starColor;
            switch (difficultyType)
            {
                case DifficultyType.Easy:
                    starCount = 1;
                    starColor = isLocked ? new Color(0.3f, 0.4f, 0.3f) : new Color(0.3f, 0.9f, 0.3f);
                    break;
                case DifficultyType.Normal:
                    starCount = 2;
                    starColor = isLocked ? new Color(0.4f, 0.4f, 0.3f) : new Color(1f, 0.85f, 0.2f);
                    break;
                case DifficultyType.Hard:
                default:
                    starCount = 3;
                    starColor = isLocked ? new Color(0.4f, 0.3f, 0.3f) : new Color(1f, 0.3f, 0.3f);
                    break;
            }

            GameObject starObj = new GameObject("DifficultyStars");
            starObj.transform.SetParent(stageBtn.transform, false);
            RectTransform starRt = starObj.AddComponent<RectTransform>();
            starRt.anchorMin = new Vector2(0.5f, 0.5f);
            starRt.anchorMax = new Vector2(0.5f, 0.5f);
            starRt.pivot = new Vector2(0.5f, 0.5f);
            starRt.anchoredPosition = new Vector2(0f, -64f);
            starRt.sizeDelta = new Vector2(btnSize * 0.6f, 16f);
            Text starText = starObj.AddComponent<Text>();
            starText.font = font;
            starText.fontSize = 14;
            starText.alignment = TextAnchor.MiddleCenter;
            starText.color = starColor;
            starText.raycastTarget = false;

            string stars = "";
            for (int s = 0; s < starCount; s++) stars += "\u2605";
            starText.text = stars;

            Outline starOutline = starObj.AddComponent<Outline>();
            starOutline.effectColor = new Color(0f, 0f, 0f, 0.6f);
            starOutline.effectDistance = new Vector2(1, 1);
        }

        // ============================================================
        // 스크롤 위치 계산
        // ============================================================

        private IEnumerator ScrollToHighestUnlockedCoroutine()
        {
            // 1프레임 대기 (레이아웃 확정)
            yield return null;
            Canvas.ForceUpdateCanvases();
            yield return null;

            if (scrollRect == null || contentRt == null) yield break;

            // 가장 높은 언락 레벨 찾기
            var allLevels = LevelRegistry.GetAllLevels();
            int highestUnlockedIndex = 0;
            for (int i = 0; i < allLevels.Count; i++)
            {
                if (!allLevels[i].isLocked)
                    highestUnlockedIndex = i;
            }

            int targetRow = highestUnlockedIndex / COLUMNS;

            // Content와 Viewport 높이
            float contentHeight = contentRt.rect.height;
            float viewportHeight = viewportRt != null ? viewportRt.rect.height : 0f;
            if (viewportHeight <= 0f) viewportHeight = 1340f;

            float scrollable = contentHeight - viewportHeight;
            if (scrollable <= 0f)
            {
                scrollRect.verticalNormalizedPosition = 1f;
                yield break;
            }

            // 타겟 행의 Content 내 Y 위치 추정
            // VLG padding.top + row * (rowHeight + spacing)
            float rowHeight = BUTTON_SIZE + BUTTON_GAP;
            float targetY = PADDING + targetRow * (rowHeight + ROW_SPACING) + rowHeight * 0.5f;

            // 타겟이 뷰포트 중앙에 오도록
            float scrollOffset = targetY - viewportHeight * 0.5f;
            float normalized = 1f - scrollOffset / scrollable;
            normalized = Mathf.Clamp01(normalized);
            scrollRect.verticalNormalizedPosition = normalized;
            scrollRect.StopMovement();

            Debug.Log($"[StageScrollBuilder] 스크롤: 레벨{highestUnlockedIndex + 1} 중앙 정렬 (row={targetRow}, contentH={contentHeight:F0}, vpH={viewportHeight:F0})");
        }
    }
}
