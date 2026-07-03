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
        /// 해금 상태 갱신: LevelRegistry의 현재 isLocked 값을 버튼 interactable에 반영
        /// </summary>
        public void RefreshUnlockStates()
        {
            if (contentRt == null) return;

            var allLevels = LevelRegistry.GetAllLevels();
            for (int i = 0; i < allLevels.Count; i++)
            {
                int stageNum = allLevels[i].levelId;
                Transform stageBtn = contentRt.Find($"Row{i / COLUMNS}/Stage{stageNum}Button");
                if (stageBtn == null) continue;

                Button btn = stageBtn.GetComponent<Button>();
                if (btn == null) continue;

                bool wasLocked = !btn.interactable;
                bool isNowUnlocked = !allLevels[i].isLocked;

                if (wasLocked && isNowUnlocked)
                {
                    btn.interactable = true;

                    // ★ 비주얼 갱신: 어두운 색상 → 원본 색상으로 복원
                    var levelData = allLevels[i];
                    var display = levelData.lobbyDisplay;
                    if (display != null)
                    {
                        // 배경 색상 복원
                        Image hexBg = stageBtn.GetComponent<Image>();
                        if (hexBg != null)
                        {
                            hexBg.color = display.backgroundColor;
                            // Button colors도 갱신
                            var btnColors = btn.colors;
                            btnColors.normalColor = Color.white;
                            btnColors.highlightedColor = display.backgroundColor * 1.3f;
                            btnColors.pressedColor = display.backgroundColor * 0.7f;
                            btn.colors = btnColors;
                        }

                        // 테두리 색상 복원
                        Transform borderTr = stageBtn.Find("HexBorder");
                        if (borderTr != null)
                        {
                            Image borderImg = borderTr.GetComponent<Image>();
                            if (borderImg != null)
                                borderImg.color = display.borderColor;
                        }
                    }

                    // 잠금 아이콘(🔒) → 레벨명으로 교체
                    Transform stageTextTr = stageBtn.Find("StageText");
                    if (stageTextTr != null)
                    {
                        Text stageText = stageTextTr.GetComponent<Text>();
                        if (stageText != null)
                        {
                            stageText.text = levelData.levelName ?? $"LEVEL {stageNum}";
                            stageText.fontSize = 28;
                            stageText.color = new Color(0.85f, 0.9f, 1f);
                        }
                    }

                    // 플레이 아이콘 또는 점수 표시 추가 (아직 없으면)
                    float btnSizeForUI = stageBtn.GetComponent<RectTransform>().sizeDelta.x;
                    if (scoreManager != null)
                    {
                        int levelBest = scoreManager.GetLevelHighScore(stageNum);
                        int personalBest = scoreManager.GetPersonalLevelBest(stageNum);

                        if (levelBest > 0 || personalBest > 0)
                        {
                            Transform playIcon = stageBtn.Find("PlayIcon");
                            if (playIcon != null) Destroy(playIcon.gameObject);
                            if (stageBtn.Find("LevelBest") == null)
                                CreateScoreTexts(stageBtn.gameObject, btnSizeForUI, levelBest, personalBest);
                        }
                        else if (stageBtn.Find("PlayIcon") == null && stageBtn.Find("LevelBest") == null)
                        {
                            CreatePlayIcon(stageBtn.gameObject, btnSizeForUI);
                        }
                    }

                    // ★ 획득 별 + 난이도 배지 재생성 (회색 → 활성 색상)
                    Transform oldStars = stageBtn.Find("DifficultyStars");
                    if (oldStars != null) Destroy(oldStars.gameObject);
                    Transform oldDiffBadge = stageBtn.Find("DifficultyBadge");
                    if (oldDiffBadge != null) Destroy(oldDiffBadge.gameObject);
                    CreateDifficultyStars(stageBtn.gameObject, btnSizeForUI, false, levelData.difficultyType, stageNum);

                    // ★ 몬스터 아이콘 재생성 (회색 tint → 원본 색상)
                    for (int mi = 0; mi < 20; mi++)
                    {
                        Transform oldIcon = stageBtn.Find($"MonsterIcon_{mi}");
                        if (oldIcon != null) Destroy(oldIcon.gameObject);
                        else break; // 연속된 인덱스가 아니면 더 이상 없음
                    }
                    CreateMonsterIcons(stageBtn.gameObject, btnSizeForUI, false, stageNum);

                    // ★ 튜토리얼 뱃지 재생성 (회색 → 활성 파랑)
                    Transform oldBadge = stageBtn.Find("TutorialBadge");
                    if (oldBadge != null)
                    {
                        Destroy(oldBadge.gameObject);
                        if (JewelsHexaPuzzle.Managers.TutorialManager.IsTutorialStage(stageNum))
                            CreateTutorialBadge(stageBtn.gameObject, btnSizeForUI, false);
                    }

                    Debug.Log($"[StageScrollBuilder] 레벨 {stageNum} 버튼 해금 + 비주얼 갱신 (별/몬스터/뱃지 색상 활성화)");
                }
            }
        }

        /// <summary>
        /// 모든 레벨 버튼의 interactable 상태를 강제 설정 (레벨 활성화 모드용)
        /// force=true: 잠긴 레벨도 클릭 가능 / force=false: 잠금 상태 복원
        /// </summary>
        public void ForceAllButtonsInteractable(bool force)
        {
            if (contentRt == null) return;
            var allLevels = LevelRegistry.GetAllLevels();
            for (int i = 0; i < allLevels.Count; i++)
            {
                int stageNum = allLevels[i].levelId;
                Transform stageBtn = contentRt.Find($"Row{i / COLUMNS}/Stage{stageNum}Button");
                if (stageBtn == null) continue;
                Button btn = stageBtn.GetComponent<Button>();
                if (btn == null) continue;

                btn.interactable = force || !allLevels[i].isLocked;
            }
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

            // --- 상단 경계: 히어로 카드 하단 우선, 없으면 타이틀 하단 ---
            float topOffset = -200f; // 폴백: 화면 상단에서 200px 아래
            if (parent != null)
            {
                Transform heroTr = parent.Find("LobbyHeroCard");
                if (heroTr != null)
                {
                    RectTransform heroRt = heroTr.GetComponent<RectTransform>();
                    if (heroRt != null)
                    {
                        // 상단 stretch 앵커(anchorMin.y=anchorMax.y=1): 하단 모서리 Y = offsetMin.y
                        topOffset = heroRt.offsetMin.y - 12f; // 히어로 하단에서 12px 여유
                    }
                }
                else
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
            }

            // --- 하단 경계: 하단 내비바 우선, 없으면 하단 버튼 중 가장 높은 상단 ---
            float bottomOffset = 170f; // 폴백: 화면 하단에서 170px 위
            Transform navTr = parent != null ? parent.Find("LobbyBottomNav") : null;
            if (navTr != null)
            {
                RectTransform navRt = navTr.GetComponent<RectTransform>();
                // 하단 stretch 앵커(anchorMin.y=anchorMax.y=0, pivot.y=0): 상단 모서리 = sizeDelta.y
                if (navRt != null) bottomOffset = navRt.sizeDelta.y + 10f; // 내비 위 10px 여유
            }
            else if (parent != null)
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

            // === Viewport — RectMask2D + 투명 Image (드래그 raycast 수신용) ===
            GameObject viewportObj = new GameObject("Viewport");
            viewportObj.transform.SetParent(transform, false);
            viewportRt = viewportObj.AddComponent<RectTransform>();
            viewportRt.anchorMin = Vector2.zero;
            viewportRt.anchorMax = Vector2.one;
            viewportRt.pivot = new Vector2(0.5f, 0.5f);
            viewportRt.offsetMin = Vector2.zero;
            viewportRt.offsetMax = Vector2.zero;
            // RectMask2D: 자식 콘텐츠 클리핑용 (Mask보다 가볍고 별도 텍스처 불필요)
            viewportObj.AddComponent<RectMask2D>();
            // ★ 투명 Image: 버튼이 없는 빈 영역에서도 드래그가 ScrollRect로 전달되도록
            //   raycastTarget=true 인 Graphic이 필요. alpha 0으로 시각적으로는 보이지 않음.
            Image vpRayTarget = viewportObj.AddComponent<Image>();
            vpRayTarget.color = new Color(0f, 0f, 0f, 0f);
            vpRayTarget.raycastTarget = true;

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

            // 프리미엄 프레임 오버레이 (클로드 디자인: 골드 메탈 림 + 상단 글래스 + 내부 비네팅)
            //   챕터색 hex 위에 깊이/질감을 더함. 중앙 투명 → 챕터색 유지. 내용(번호/별/배지)보다 아래 레이어.
            Sprite overlaySpr = Resources.Load<Sprite>("UI/hex_card_overlay");
            if (overlaySpr != null)
            {
                GameObject ovObj = new GameObject("FrameOverlay");
                ovObj.transform.SetParent(stageBtn.transform, false);
                RectTransform ovRt = ovObj.AddComponent<RectTransform>();
                ovRt.anchorMin = Vector2.zero; ovRt.anchorMax = Vector2.one;
                ovRt.offsetMin = Vector2.zero; ovRt.offsetMax = Vector2.zero;
                Image ovImg = ovObj.AddComponent<Image>();
                ovImg.sprite = overlaySpr;
                ovImg.type = Image.Type.Simple;
                ovImg.preserveAspect = true;
                ovImg.raycastTarget = false;
                if (isLocked) ovImg.color = new Color(0.62f, 0.62f, 0.68f, 0.85f); // 잠금 시 골드 디밍
            }

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
            stageText.fontSize = 28;
            stageText.alignment = TextAnchor.MiddleCenter;
            // 잠금 레벨도 번호를 회색으로 표시 (자물쇠 아이콘 대신 레벨 번호 유지)
            stageText.color = isLocked ? new Color(0.5f, 0.5f, 0.6f) : new Color(0.85f, 0.9f, 1f);
            stageText.raycastTarget = false;
            stageText.text = stageLabel;

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

            // 출연 몬스터 아이콘 (하단) — 실제 미션 데이터(Mission1StageData)에서 적군 타입 추출
            CreateMonsterIcons(stageBtn, btnSize, isLocked, stageNum);

            // 획득 별(3슬롯) + 난이도 배지 (상단)
            CreateDifficultyStars(stageBtn, btnSize, isLocked, difficultyType, stageNum);

            // 튜토리얼 뱃지 (오른쪽 상단 "T")
            if (JewelsHexaPuzzle.Managers.TutorialManager.IsTutorialStage(stageNum))
                CreateTutorialBadge(stageBtn, btnSize, isLocked);

            // 대기 미션 뱃지 — 하단 라인에 걸치게, 인게임과 동일한 대기 미션 수 표시
            int pendingMissions = GetMonsterWaveCount(stageNum);
            if (pendingMissions > 0)
                CreateMonsterWaveBadge(stageBtn, btnSize, pendingMissions, isLocked);

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

        private void CreatePlayIcon(GameObject stageBtn, float btnSize)
        {
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

        private void CreateDifficultyStars(GameObject stageBtn, float btnSize, bool isLocked, DifficultyType difficultyType, int stageNum)
        {
            // 획득 별(클리어 성과, 3슬롯) — 난이도가 아닌 실제 클리어 별
            int earned = (!isLocked && scoreManager != null) ? scoreManager.GetLevelStars(stageNum) : 0;

            GameObject starObj = new GameObject("DifficultyStars");
            starObj.transform.SetParent(stageBtn.transform, false);
            RectTransform starRt = starObj.AddComponent<RectTransform>();
            starRt.anchorMin = new Vector2(0.5f, 0.5f);
            starRt.anchorMax = new Vector2(0.5f, 0.5f);
            starRt.pivot = new Vector2(0.5f, 0.5f);
            starRt.anchoredPosition = new Vector2(0f, 56f); // 상단으로 이동 (기존 -64 → +56)
            starRt.sizeDelta = new Vector2(btnSize * 0.6f, 32f); // 높이 2배 (16 → 32)
            Text starText = starObj.AddComponent<Text>();
            starText.font = font;
            starText.fontSize = 28; // 별 크기 2배 (14 → 28)
            starText.alignment = TextAnchor.MiddleCenter;
            starText.color = Color.white;
            starText.supportRichText = true;
            starText.raycastTarget = false;

            const string FILLED = "<color=#FFD06B>\u2605</color>"; // \uae08\uc0c9 \ucc44\uc6c0
            const string EMPTY  = "<color=#42506F>\u2605</color>"; // \ud68c\uc0c9 \ube48\uce78
            starText.text = (earned >= 1 ? FILLED : EMPTY) + (earned >= 2 ? FILLED : EMPTY) + (earned >= 3 ? FILLED : EMPTY);

            Outline starOutline = starObj.AddComponent<Outline>();
            starOutline.effectColor = new Color(0f, 0f, 0f, 0.6f);
            starOutline.effectDistance = new Vector2(1, 1);

            // === \ub09c\uc774\ub3c4 \ubc30\uc9c0 (\uc0c9 + \uae00\ub9ac\ud504, \uc88c\uc0c1\ub2e8 \ucf54\ub108) \u2014 \ud68d\ub4dd \ubcc4\uacfc \uc644\uc804 \ubd84\ub9ac ===
            // \ud504\ub9ac\ubbf8\uc5c4 \ubcf4\uc11d \uba54\ub2ec\ub9ac\uc628 PNG(\ud074\ub85c\ub4dc \ub514\uc790\uc778) + \ud3f4\ubc31 \uae00\ub9ac\ud504
            string diffSpriteName; Color diffFallbackColor; string diffGlyph;
            switch (difficultyType)
            {
                case DifficultyType.Easy:   diffSpriteName = "UI/diff_easy";   diffFallbackColor = new Color(0.369f, 0.820f, 0.478f); diffGlyph = "\u25cf"; break; // \ucd08\ub85d=\uc26c\uc6c0
                case DifficultyType.Normal: diffSpriteName = "UI/diff_normal"; diffFallbackColor = new Color(1f, 0.812f, 0.302f);     diffGlyph = "\u25c6"; break; // \uace8\ub4dc=\ubcf4\ud1b5
                case DifficultyType.Hard:
                default:                    diffSpriteName = "UI/diff_hard";   diffFallbackColor = new Color(0.882f, 0.294f, 0.235f); diffGlyph = "\u25b2"; break; // \uc801\uc0c9=\uc5b4\ub824\uc6c0
            }

            GameObject diffObj = new GameObject("DifficultyBadge");
            diffObj.transform.SetParent(stageBtn.transform, false);
            RectTransform diffRt = diffObj.AddComponent<RectTransform>();
            diffRt.anchorMin = new Vector2(0.5f, 0.5f);
            diffRt.anchorMax = new Vector2(0.5f, 0.5f);
            diffRt.pivot = new Vector2(0.5f, 0.5f);
            diffRt.anchoredPosition = new Vector2(-btnSize * 0.31f, btnSize * 0.31f); // \uc88c\uc0c1\ub2e8(\ud29c\ud1a0\ub9ac\uc5bc T \ubc43\uc9c0\uc640 \ubd84\ub9ac)

            Sprite diffSpr = Resources.Load<Sprite>(diffSpriteName);
            if (diffSpr != null)
            {
                diffRt.sizeDelta = new Vector2(52f, 52f); // \uba54\ub2ec\ub9ac\uc628\uc774 \uc77d\ud788\ub3c4\ub85d \ud655\ub300(28\u219252)
                Image diffImg = diffObj.AddComponent<Image>();
                diffImg.sprite = diffSpr;
                diffImg.preserveAspect = true;
                diffImg.raycastTarget = false;
                diffImg.color = isLocked ? new Color(0.55f, 0.55f, 0.62f, 0.8f) : Color.white; // \uc7a0\uae08 \uc2dc \ub514\ubc0d
                Shadow diffShadow = diffObj.AddComponent<Shadow>();
                diffShadow.effectColor = new Color(0f, 0f, 0f, 0.45f);
                diffShadow.effectDistance = new Vector2(1.5f, -1.5f);
            }
            else
            {
                // \ud3f4\ubc31: \uae30\uc874 \uae00\ub9ac\ud504(\uc2a4\ud504\ub77c\uc774\ud2b8 \ub85c\ub4dc \uc2e4\ud328 \ub300\ube44)
                diffRt.sizeDelta = new Vector2(28f, 28f);
                Color c = isLocked ? new Color(diffFallbackColor.r, diffFallbackColor.g, diffFallbackColor.b, 0.45f) : diffFallbackColor;
                Text diffText = diffObj.AddComponent<Text>();
                diffText.font = font;
                diffText.fontSize = 20;
                diffText.alignment = TextAnchor.MiddleCenter;
                diffText.color = c;
                diffText.raycastTarget = false;
                diffText.text = diffGlyph;
                Outline diffOutline = diffObj.AddComponent<Outline>();
                diffOutline.effectColor = new Color(0f, 0f, 0f, 0.7f);
                diffOutline.effectDistance = new Vector2(1, -1);
            }
        }

        // ============================================================
        // 출연 몬스터 아이콘 (하단)
        // ============================================================

        /// <summary>
        /// subtitle 문자열을 파싱해 포함된 몬스터 타입의 아이콘을 나열 표시.
        /// 수량은 표시하지 않고 종류만 이미지로 구분.
        /// </summary>
        private void CreateMonsterIcons(GameObject stageBtn, float btnSize, bool isLocked, int stageNum)
        {
            // ★ 실제 미션 데이터에서 적군 타입 추출 (subtitle 키워드 매칭 폐기)
            //   subtitle 텍스트는 LevelRegistry에 하드코딩되어 실제 미션과 불일치(Stage 1: 블록 미션인데 "고블린" 단어 포함)
            //   대신 Mission1StageData.GetStage(N)의 missions에서 RemoveEnemy 미션의 EnemyType만 수집
            var iconCandidates = new List<(Sprite sprite, EnemyType enemyType)>();

            if (_cachedMission1Stages == null)
                _cachedMission1Stages = Mission1StageData.GetAllMission1Stages();

            if (_cachedMission1Stages.TryGetValue(stageNum, out var stageData)
                && stageData != null && stageData.missions != null)
            {
                var seen = new HashSet<EnemyType>();
                foreach (var m in stageData.missions)
                {
                    if (m == null) continue;
                    if (m.type != MissionType.RemoveEnemy) continue;
                    if (!seen.Add(m.targetEnemyType)) continue; // 중복 타입 한 번만

                    var sprite = GetEnemySprite(m.targetEnemyType);
                    if (sprite != null)
                        iconCandidates.Add((sprite, m.targetEnemyType));
                }
            }

            int count = iconCandidates.Count;
            if (count == 0) return;

            // count별 세부 레이아웃 결정: (1행 수, 2행 수, 1행 간격 조정, 2행 간격 조정)
            //   간격 조정값은 기본 spacing(2px)에서 빼는 픽셀 수 (공간 부족 시 아이콘을 조밀하게)
            const float DEFAULT_SPACING = 2f;
            int firstRowCount, secondRowCount;
            float firstSpacing, secondSpacing;
            switch (count)
            {
                case 1: case 2: case 3: case 4:
                    firstRowCount = count; secondRowCount = 0;
                    firstSpacing = DEFAULT_SPACING; secondSpacing = DEFAULT_SPACING;
                    break;
                case 5:
                    firstRowCount = 3; secondRowCount = 2;
                    firstSpacing = DEFAULT_SPACING; secondSpacing = DEFAULT_SPACING;
                    break;
                case 6:
                    firstRowCount = 3; secondRowCount = 3;
                    firstSpacing = DEFAULT_SPACING; secondSpacing = DEFAULT_SPACING;
                    break;
                case 7:
                    firstRowCount = 4; secondRowCount = 3;
                    firstSpacing = DEFAULT_SPACING; secondSpacing = DEFAULT_SPACING;
                    break;
                case 8:
                    firstRowCount = 5; secondRowCount = 3;
                    firstSpacing = DEFAULT_SPACING - 2f; secondSpacing = DEFAULT_SPACING;
                    break;
                case 9:
                    firstRowCount = 5; secondRowCount = 4;
                    firstSpacing = DEFAULT_SPACING - 2f; secondSpacing = DEFAULT_SPACING - 2f;
                    break;
                case 10:
                    firstRowCount = 6; secondRowCount = 4;
                    firstSpacing = DEFAULT_SPACING - 3f; secondSpacing = DEFAULT_SPACING - 2f;
                    break;
                case 11:
                    firstRowCount = 6; secondRowCount = 5;
                    firstSpacing = DEFAULT_SPACING - 3f; secondSpacing = DEFAULT_SPACING - 3f;
                    break;
                default: // 12+ (정의 외): 절반씩 분할, 조밀 배치
                    firstRowCount = count / 2 + count % 2;
                    secondRowCount = count / 2;
                    firstSpacing = DEFAULT_SPACING - 3f; secondSpacing = DEFAULT_SPACING - 3f;
                    break;
            }

            bool twoRows = secondRowCount > 0;

            // 아이콘 크기: 각 행별 가용 공간에서 최소값 선택 (두 행 공통 크기)
            float maxRowWidth = btnSize * 0.9f;
            float iconSize1 = firstRowCount > 0
                ? (maxRowWidth - (firstRowCount - 1) * firstSpacing) / firstRowCount
                : 26f;
            float iconSize2 = secondRowCount > 0
                ? (maxRowWidth - (secondRowCount - 1) * secondSpacing) / secondRowCount
                : iconSize1;
            float iconSize = Mathf.Clamp(Mathf.Min(iconSize1, iconSize2), 12f, 26f);

            // 2행 배치 시 y 기준선 조정
            float rowSpacing = 3f;
            float baseY = twoRows
                ? -48f + (iconSize + rowSpacing) * 0.5f
                : -48f;
            float row2Y = baseY - (iconSize + rowSpacing);

            Color tint = isLocked ? new Color(0.5f, 0.5f, 0.5f, 1f) : Color.white;

            // 1행 배치 (firstSpacing 사용)
            float row1Width = firstRowCount * iconSize + (firstRowCount - 1) * firstSpacing;
            float startX1 = -row1Width * 0.5f + iconSize * 0.5f;
            for (int i = 0; i < firstRowCount; i++)
            {
                CreateSingleMonsterIcon(stageBtn, iconCandidates[i].sprite, iconCandidates[i].enemyType,
                    tint, iconSize,
                    startX1 + i * (iconSize + firstSpacing), baseY, i);
            }

            // 2행 배치 (secondSpacing 사용)
            if (secondRowCount > 0)
            {
                float row2Width = secondRowCount * iconSize + (secondRowCount - 1) * secondSpacing;
                float startX2 = -row2Width * 0.5f + iconSize * 0.5f;
                for (int i = 0; i < secondRowCount; i++)
                {
                    var item = iconCandidates[firstRowCount + i];
                    CreateSingleMonsterIcon(stageBtn, item.sprite, item.enemyType, tint, iconSize,
                        startX2 + i * (iconSize + secondSpacing), row2Y, firstRowCount + i);
                }
            }
        }

        /// <summary>
        /// EnemyType → 미션 아이콘용 Sprite 매핑 (Lv2 엘리트도 지원).
        /// 인게임에서 정의되지 않은 적군은 null 반환 → 아이콘 미표시.
        /// </summary>
        private static Sprite GetEnemySprite(EnemyType type)
        {
            // ★ 로비 레벨 UI: 몬스터를 특징 소품(prop_*.png)으로 표현 — 방패/활/오크통폭탄 등
            Sprite prop = JewelsHexaPuzzle.Core.GoblinSystem.GetGoblinPropSprite(type);
            if (prop != null) return prop;

            // 소품 없는 타입(Lv2 등): 얼굴 클로즈업 → 전신 스프라이트 폴백
            Sprite face = JewelsHexaPuzzle.Core.GoblinSystem.GetGoblinFaceSprite(type);
            if (face != null) return face;

            switch (type)
            {
                case EnemyType.Goblin:           return JewelsHexaPuzzle.Core.GoblinSystem.GetGoblinSprite();
                case EnemyType.ArmoredGoblin:    return JewelsHexaPuzzle.Core.GoblinSystem.GetArmoredGoblinSprite();
                case EnemyType.ArcherGoblin:     return JewelsHexaPuzzle.Core.GoblinSystem.GetArcherGoblinSprite();
                case EnemyType.ShieldGoblin:     return JewelsHexaPuzzle.Core.GoblinSystem.GetShieldGoblinSprite();
                case EnemyType.BombGoblin:       return JewelsHexaPuzzle.Core.GoblinSystem.GetBombGoblinSprite();
                case EnemyType.HealerGoblin:     return JewelsHexaPuzzle.Core.GoblinSystem.GetGoblinSprite(); // 힐러는 기본 + 색상 tint
                case EnemyType.HeavyGoblin:      return JewelsHexaPuzzle.Core.GoblinSystem.GetHeavyGoblinSprite();
                case EnemyType.WizardGoblin:     return JewelsHexaPuzzle.Core.GoblinSystem.GetWizardGoblinSprite();
                case EnemyType.WitchGoblin:      return JewelsHexaPuzzle.Core.GoblinSystem.GetWitchGoblinSprite();
                case EnemyType.ThiefGoblin:      return JewelsHexaPuzzle.Core.GoblinSystem.GetThiefGoblinSprite();
                // Lv2 엘리트 (101+ 스테이지)
                case EnemyType.GoblinLv2:        return JewelsHexaPuzzle.Core.GoblinSystem.GetGoblinLv2Sprite();
                case EnemyType.ArmoredGoblinLv2: return JewelsHexaPuzzle.Core.GoblinSystem.GetArmoredLv2Sprite();
                case EnemyType.ArcherGoblinLv2:  return JewelsHexaPuzzle.Core.GoblinSystem.GetArcherLv2Sprite();
                case EnemyType.ShieldGoblinLv2:  return JewelsHexaPuzzle.Core.GoblinSystem.GetShieldLv2Sprite();
            }
            return null;
        }

        /// <summary>
        /// 몬스터 아이콘 1개 생성 (내부 헬퍼).
        /// 마법사/마녀 스프라이트는 텍스처 좌표계 차이로 인해 인게임에서도 180도 회전을 적용함.
        /// 로비 아이콘에서도 동일하게 180도 회전해야 모자가 위로 향함.
        /// </summary>
        private void CreateSingleMonsterIcon(GameObject stageBtn, Sprite sprite, EnemyType enemyType,
            Color tint, float iconSize, float x, float y, int index)
        {
            GameObject iconObj = new GameObject($"MonsterIcon_{index}");
            iconObj.transform.SetParent(stageBtn.transform, false);
            RectTransform iconRt = iconObj.AddComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0.5f, 0.5f);
            iconRt.anchorMax = new Vector2(0.5f, 0.5f);
            iconRt.pivot = new Vector2(0.5f, 0.5f);
            iconRt.anchoredPosition = new Vector2(x, y);
            iconRt.sizeDelta = new Vector2(iconSize, iconSize);

            // 미션 아이콘은 얼굴 PNG(정면·유색) 사용 → 마법사/마녀 회전 보정, 힐러 틴트 불필요.
            // (잠금 스테이지의 회색 처리는 tint 인자에 이미 반영됨)
            Image iconImg = iconObj.AddComponent<Image>();
            iconImg.sprite = sprite;
            iconImg.color = tint;
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;
        }

        // ============================================================
        // 튜토리얼 뱃지 (오른쪽 상단 원 + "T")
        // ============================================================

        /// <summary>
        /// 튜토리얼이 있는 스테이지에 표시할 작은 뱃지.
        /// 버튼 우상단에 원형 배경 + "T" 텍스트.
        /// </summary>
        private void CreateTutorialBadge(GameObject stageBtn, float btnSize, bool isLocked)
        {
            float badgeSize = 24f;

            GameObject badgeObj = new GameObject("TutorialBadge");
            badgeObj.transform.SetParent(stageBtn.transform, false);
            RectTransform badgeRt = badgeObj.AddComponent<RectTransform>();
            badgeRt.anchorMin = new Vector2(1f, 1f);
            badgeRt.anchorMax = new Vector2(1f, 1f);
            badgeRt.pivot = new Vector2(1f, 1f);
            badgeRt.anchoredPosition = new Vector2(-6f, -6f); // 우상단 여백
            badgeRt.sizeDelta = new Vector2(badgeSize, badgeSize);

            // 원형 배경 (HexBlock의 flash 스프라이트가 원형에 가깝지 않으므로 기본 Unity 원 스프라이트 사용)
            Image bgImg = badgeObj.AddComponent<Image>();
            bgImg.sprite = CreateCircleSprite();
            bgImg.color = isLocked
                ? new Color(0.3f, 0.45f, 0.7f, 0.7f)   // 잠금: 어두운 파랑
                : new Color(0.2f, 0.55f, 0.95f, 1f);   // 해금: 선명한 파랑
            bgImg.preserveAspect = true;
            bgImg.raycastTarget = false;

            // 테두리
            Outline bgOutline = badgeObj.AddComponent<Outline>();
            bgOutline.effectColor = new Color(0f, 0f, 0f, 0.75f);
            bgOutline.effectDistance = new Vector2(1.2f, -1.2f);

            // "T" 텍스트
            GameObject textObj = new GameObject("T");
            textObj.transform.SetParent(badgeObj.transform, false);
            RectTransform textRt = textObj.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;

            Text tText = textObj.AddComponent<Text>();
            tText.font = font;
            tText.fontSize = 16;
            tText.fontStyle = FontStyle.Bold;
            tText.alignment = TextAnchor.MiddleCenter;
            tText.color = isLocked ? new Color(0.85f, 0.85f, 0.85f) : Color.white;
            tText.raycastTarget = false;
            tText.text = "T";
        }

        // ============================================================
        // 몬스터 웨이브(대기) 뱃지 — 하단 라인에 걸치는 원형 숫자
        // ============================================================

        /// <summary>
        /// 해당 스테이지의 "대기 미션 수"를 반환 — 인게임 StageManager.InitializeMissions의 대기열과 정확히 일치.
        /// = max(0, (MissionBalance 보충 후 전체 미션 수) − 동시 활성 제한).
        ///   기존 버그: RemoveEnemy 미션만 세고 보충 미션을 무시 → 21+ 구간/수집 미션 스테이지에서 실제와 불일치.
        /// 산식은 MissionBalance.GetPendingMissionCount로 단일화(중복 방지). 미정의 스테이지(Infinite)는 0 반환.
        /// </summary>
        private static Dictionary<int, StageData> _cachedMission1Stages;
        private static int GetMonsterWaveCount(int stageNum)
        {
            if (_cachedMission1Stages == null)
                _cachedMission1Stages = Mission1StageData.GetAllMission1Stages();

            if (!_cachedMission1Stages.TryGetValue(stageNum, out var data) || data == null)
                return 0;

            return MissionBalance.GetPendingMissionCount(data);
        }

        /// <summary>
        /// 몬스터 웨이브 수를 보여주는 원형 뱃지 — 버튼 하단 라인에 걸치게 배치.
        /// 잠금 시 채도 낮춤. 가독성을 위해 흰 텍스트 + 검정 외곽선.
        /// </summary>
        private void CreateMonsterWaveBadge(GameObject stageBtn, float btnSize, int waveCount, bool isLocked)
        {
            float badgeSize = 34f;

            GameObject badgeObj = new GameObject("MonsterWaveBadge");
            badgeObj.transform.SetParent(stageBtn.transform, false);
            RectTransform badgeRt = badgeObj.AddComponent<RectTransform>();
            // 하단 중앙 앵커 + 중앙 피벗 → 뱃지 중심이 정확히 버튼 하단 라인 위에 위치 (절반은 안, 절반은 밖)
            badgeRt.anchorMin = new Vector2(0.5f, 0f);
            badgeRt.anchorMax = new Vector2(0.5f, 0f);
            badgeRt.pivot = new Vector2(0.5f, 0.5f);
            badgeRt.anchoredPosition = new Vector2(0f, 0f);
            badgeRt.sizeDelta = new Vector2(badgeSize * 1.12f, badgeSize); // 플랫탑 헥사(가로>세로) 비율

            // ★ 대기 미션 배지 — 인게임과 동일한 클로드 디자인 골드 헥사 스프라이트로 통일
            //   (헥사 카드와 형태 일치 + 골드 별과 톤 조화). 잠금은 회색조 디밍, 폴백은 기존 빨강 원.
            Image bgImg = badgeObj.AddComponent<Image>();
            Sprite badgeSpr = Resources.Load<Sprite>("UI/pending_badge");
            if (badgeSpr != null)
            {
                bgImg.sprite = badgeSpr;
                bgImg.color = isLocked ? new Color(0.62f, 0.62f, 0.64f, 0.9f) : Color.white;
            }
            else
            {
                bgImg.sprite = CreateCircleSprite();
                bgImg.color = isLocked ? new Color(0.55f, 0.25f, 0.25f, 0.85f) : new Color(0.92f, 0.28f, 0.22f, 1f);
            }
            bgImg.preserveAspect = true;
            bgImg.raycastTarget = false;

            // 카드 가장자리 대비용 드롭섀도 (스프라이트 자체 림과 별개, 가볍게)
            Outline bgOutline = badgeObj.AddComponent<Outline>();
            bgOutline.effectColor = new Color(0f, 0f, 0f, 0.55f);
            bgOutline.effectDistance = new Vector2(1f, -1f);

            // 숫자 텍스트
            GameObject textObj = new GameObject("WaveText");
            textObj.transform.SetParent(badgeObj.transform, false);
            RectTransform textRt = textObj.AddComponent<RectTransform>();
            textRt.anchorMin = new Vector2(0.5f, 0.5f);
            textRt.anchorMax = new Vector2(0.5f, 0.5f);
            textRt.pivot = new Vector2(0.5f, 0.5f);
            // 숫자 글리프 광학 보정 — 숫자마다 사이드베어링이 달라(4=좌편향, 8=대칭) 고정 nudge는
            // 모든 숫자 만족 불가 → 두 극단의 중간값(+1.3px)으로 전 숫자를 ±1.3px(시각상 중앙) 내로. 인게임과 동일.
            textRt.anchoredPosition = new Vector2(1.3f, 0f);
            textRt.sizeDelta = new Vector2(badgeSize * 1.12f, badgeSize);

            Text waveText = textObj.AddComponent<Text>();
            waveText.font = font;
            waveText.fontSize = 20;
            waveText.fontStyle = FontStyle.Bold;
            waveText.alignment = TextAnchor.MiddleCenter;
            waveText.color = isLocked ? new Color(0.95f, 0.95f, 0.95f) : Color.white;
            waveText.raycastTarget = false;
            waveText.text = waveCount.ToString();

            // 골드 헥사 위 가독성 — 다크골드 아웃라인 (인게임 배지와 통일)
            Outline textOutline = textObj.AddComponent<Outline>();
            textOutline.effectColor = new Color(0.30f, 0.18f, 0.02f, 0.9f);
            textOutline.effectDistance = new Vector2(1.1f, -1.1f);
        }

        /// <summary>
        /// 프로시저럴 원형 스프라이트 (튜토리얼 뱃지 배경용, 32x32, 캐시).
        /// </summary>
        private static Sprite _cachedCircleSprite;
        private static Sprite CreateCircleSprite()
        {
            if (_cachedCircleSprite != null) return _cachedCircleSprite;
            const int size = 32;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            float center = (size - 1) * 0.5f;
            float radius = center;
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    if (dist <= radius - 1f)
                        pixels[y * size + x] = Color.white;
                    else if (dist <= radius)
                        pixels[y * size + x] = new Color(1f, 1f, 1f, 1f - (dist - (radius - 1f)));
                    else
                        pixels[y * size + x] = Color.clear;
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            _cachedCircleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            return _cachedCircleSprite;
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
