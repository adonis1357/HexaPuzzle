using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using JewelsHexaPuzzle.Managers;

namespace JewelsHexaPuzzle.Items
{
    /// <summary>
    /// 패키지 ChargeButton(헥사 충전 게이지)을 사용해 4개 아이템 충전 바를 메인 씬에 생성한다.
    ///
    /// ★ 설계: ChargeBar는 "구버전 게이지/아이템 시스템 위의 비주얼 스킨"이다.
    ///   - 충전율: 기존 HammerGauge/SwapGauge/LineGauge의 레이어 상태에서 직접 읽는다
    ///     (gaugeLayer>=1 → 사용 가능=가득, 아니면 gaugeInLayer/50 진행률). 역회전은 항상 가득.
    ///   - 표시 여부: 대응하는 구버전 아이템 버튼(HammerItem 등이 붙은 GameObject)의
    ///     activeInHierarchy를 그대로 미러링 → 튜토리얼 기능 해금 게이팅이 자동 반영된다.
    ///   - 사용(onUsed): 구버전 게이지의 ActivateUseReady()(망치/스왑/라인) 또는
    ///     ReverseRotationItem의 버튼 onClick(역회전 토글)을 호출 → 기존 발동 로직 + 튜토리얼
    ///     이벤트(OnHammerActivated 등)가 그대로 발생한다.
    ///
    ///   기존 구버전 버튼은 GameManager가 CanvasGroup(alpha 0)로 숨기고 이름을 바꿔
    ///   화면에는 ChargeBar만 보이며, 튜토리얼 FindUITarget은 ChargeBar 버튼을 가리킨다.
    /// </summary>
    public class ChargeBarController : MonoBehaviour
    {
        private enum Kind { Hammer, Swap, Line, Reverse }

        private struct Entry { public Kind kind; public string objName; public string spriteKey; }
        private static readonly Entry[] Items = new Entry[]
        {
            // spriteKey = 클로드 디자인 패키지 스프라이트 접미사 (HexFill_{key}, Glyph_{key})
            new Entry { kind = Kind.Hammer,  objName = "HammerButton",          spriteKey = "Hammer" },
            new Entry { kind = Kind.Swap,    objName = "SwapButton",            spriteKey = "Swap" },
            new Entry { kind = Kind.Line,    objName = "LineDrawButton",        spriteKey = "Line" },
            new Entry { kind = Kind.Reverse, objName = "ReverseRotationButton", spriteKey = "Reverse" },
        };

        private const float LAYER_SIZE = 50f; // 구버전 게이지 레이어 1칸 크기

        private readonly List<HexaPuzzle.ChargeButton> _buttons = new List<HexaPuzzle.ChargeButton>();
        private readonly List<Kind> _kinds = new List<Kind>();
        // ★ 스택형: 누적 사용 횟수(게이지 레이어) 표시용 수량 배지
        private readonly List<GameObject> _countBadges = new List<GameObject>();
        private readonly List<Text> _countTexts = new List<Text>();
        private readonly List<Image> _countBadgeImgs = new List<Image>();

        // ★ 배지 스프라이트 2종 — 1~3: 기존 코랄(현행 유지), 4(풀): 검정 바탕+흰 테두리 (사용자 요청)
        private static Sprite _badgeNormalSpr, _badgeMaxSpr;
        private static Sprite BadgeNormalSprite()
        {
            if (_badgeNormalSpr == null || _badgeNormalSpr.texture == null)
                _badgeNormalSpr = JewelsHexaPuzzle.Utils.ClaudeTheme.MakeRounded(
                    48, 24, JewelsHexaPuzzle.Utils.ClaudeTheme.CoralDark, new Color(1f, 1f, 1f, 0.9f), 3);
            return _badgeNormalSpr;
        }
        private static Sprite BadgeMaxSprite()
        {
            if (_badgeMaxSpr == null || _badgeMaxSpr.texture == null)
                _badgeMaxSpr = JewelsHexaPuzzle.Utils.ClaudeTheme.MakeRounded(
                    48, 24, new Color(0.03f, 0.03f, 0.05f, 1f), Color.white, 3);
            return _badgeMaxSpr;
        }
        // ★ 화면에 표시되는 충전 비율(보간 캐시) — 매 프레임 목표치로 부드럽게 따라가도록 한다.
        private readonly List<float> _displayedRatios = new List<float>();
        // 보간 속도: 1초당 1.0 비율 따라잡음 → 0→1 풀 충전이 약 1초 걸쳐 부드럽게 차오름.
        //   값이 클수록 빠르게 도달, 작을수록 느림.
        private const float FillFollowSpeed = 1.0f;

        // 구버전 아이템 컴포넌트 캐시 (지연 탐색 — 비활성 포함)
        private HammerItem _hammer;
        private SwapItem _swap;
        private LineDrawItem _line;
        private ReverseRotationItem _reverse;

        private const float ButtonSize = 110f;
        private const float Spacing = 16f;

        /// <summary>
        /// ChargeBar 빌드.
        /// positions == null  → 하단 가로 바(폴백)
        /// positions.Length==Items.Length → 캔버스 중앙 기준 좌표에 개별 배치
        /// (기존 아이템 버튼 위치 재현용; 새 버튼이 그 자리에 들어감)
        /// </summary>
        public void Build(Transform canvas, Vector2[] positions = null)
        {
            GameObject barObj = new GameObject("ChargeBar", typeof(RectTransform));
            barObj.transform.SetParent(canvas, false);
            var rt = (RectTransform)barObj.transform;

            bool customPos = positions != null && positions.Length == Items.Length;

            if (customPos)
            {
                // 캔버스 전체에 스트레치(레이아웃 그룹 없음) — 자식 버튼은 캔버스 중앙 기준 좌표로 직접 배치
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            }
            else
            {
                // 폴백: 하단 가로 바
                rt.anchorMin = new Vector2(0.5f, 0f);
                rt.anchorMax = new Vector2(0.5f, 0f);
                rt.pivot = new Vector2(0.5f, 0f);
                rt.anchoredPosition = new Vector2(0f, 30f);
                rt.sizeDelta = new Vector2(ButtonSize * 4 + Spacing * 3, ButtonSize);

                var hl = barObj.AddComponent<HorizontalLayoutGroup>();
                hl.childAlignment = TextAnchor.MiddleCenter;
                hl.spacing = Spacing;
                hl.childControlHeight = false; hl.childControlWidth = false;
                hl.childForceExpandHeight = false; hl.childForceExpandWidth = false;
            }

            Sprite glow = Resources.Load<Sprite>("Items/Charge/Glow_Hex");          // 클로드 디자인 글로우
            if (glow == null) glow = Resources.Load<Sprite>("Items/Charge/charge_glow"); // 폴백

            for (int i = 0; i < Items.Length; i++)
            {
                var e = Items[i];
                var btn = CreateButton(barObj.transform, e, glow);
                if (btn == null) continue;

                if (customPos)
                {
                    // 캔버스 중앙 기준 개별 배치 (구 아이템 버튼 자리)
                    var brt = (RectTransform)btn.transform;
                    brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0.5f);
                    brt.pivot = new Vector2(0.5f, 0.5f);
                    brt.anchoredPosition = positions[i];
                }
                _buttons.Add(btn);
                _kinds.Add(e.kind);
                _displayedRatios.Add(0f);  // 초기 표시값 0
            }
            Debug.Log($"[ChargeBarController] ChargeBar 생성 완료 — {_buttons.Count}개 버튼 ({(customPos ? "개별 위치(구 버튼 자리)" : "하단 가로 바")})");
        }

        private HexaPuzzle.ChargeButton CreateButton(Transform parent, Entry e, Sprite glow)
        {
            // ★ 튜토리얼 FindUITarget이 찾도록 정식 이름 사용 (구버전 버튼은 숨김+개명됨)
            var go = new GameObject(e.objName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(ButtonSize, ButtonSize);

            // ★ 클로드 디자인 헥사 버튼 (레이어 합성):
            //   골드 프레임 → 회색 내부(미충전) → 컬러 채움(Filled, 충전 비율) → 글리프 → READY 글로우
            Sprite frame   = Resources.Load<Sprite>("Items/Charge/HexFrame_Gold");
            Sprite inner   = Resources.Load<Sprite>("Items/Charge/HexInner_Gray");
            Sprite fillSp  = Resources.Load<Sprite>($"Items/Charge/HexFill_{e.spriteKey}");
            Sprite glyphSp = Resources.Load<Sprite>($"Items/Charge/Glyph_{e.spriteKey}");
            // 폴백: 신규 스프라이트 누락 시 기존 프로시저럴 헥사 사용
            if (fillSp == null) { inner = Resources.Load<Sprite>($"Items/Charge/charge_hex_{e.spriteKey.ToLower()}_gray"); fillSp = Resources.Load<Sprite>($"Items/Charge/charge_hex_{e.spriteKey.ToLower()}"); frame = null; }

            // 1. 회색 내부 바탕 (미충전 베이스)
            AddImg(go, "HexInnerGray", inner, Color.white);

            // 1b. ★ 2번째+ 레이어용 "옅은 색" 바탕 — 흰 헥사 스프라이트를 아이템 색의 연한 톤(흰색으로 밝힌)으로 틴트해
            //    fill 뒤에 깐다. (곱연산 틴트는 밝아지지 않으므로 흰 헥사를 써서 진짜 밝은 연한 색을 낸다.)
            //    누적 충전(GaugeLayer>=1)일 때만 ChargeButton.baseFill 토글로 활성화 → 그 위로 선명한 fill이 차올라 대비.
            var paleBase = AddImg(go, "HexPaleBase", WhiteHexSprite(), PaleColor(e.kind));
            paleBase.gameObject.SetActive(false);

            // 2. 컬러 채움 (Filled Vertical Bottom — 충전 비율만큼 아래→위)
            var fillImg = AddImg(go, "HexColorFill", fillSp, Color.white);
            fillImg.type = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Vertical;
            fillImg.fillOrigin = (int)Image.OriginVertical.Bottom;
            fillImg.fillAmount = 0f;

            // 3. 골드 프레임 (테두리 — 채움 위에 얹어 항상 또렷)
            if (frame != null) AddImg(go, "HexFrame", frame, Color.white);

            // 4. 글리프 아이콘 (미충전 시 ChargeButton이 알파 디밍)
            Image glyphImg = (glyphSp != null) ? AddImg(go, "Glyph", glyphSp, Color.white) : null;

            // 5. READY 글로우 (100% 충전 시 활성) — 버튼 뒤(첫 자식)에 배치해 헥사 뒤로 후광만 비치게
            var glowObj = AddImg(go, "ReadyGlow", glow, Color.white).gameObject;
            glowObj.AddComponent<CanvasGroup>();
            glowObj.AddComponent<HexaPuzzle.ReadyGlowPulse>();
            glowObj.SetActive(false);
            glowObj.transform.SetAsFirstSibling(); // ★ 최하위(뒤)로 — 헥사 버튼이 글로우를 가림

            // 클릭 타겟 (투명 풀사이즈)
            var clickImg = go.AddComponent<Image>();
            clickImg.color = new Color(0f, 0f, 0f, 0f);
            clickImg.raycastTarget = true;
            go.AddComponent<Button>();

            var charge = go.AddComponent<HexaPuzzle.ChargeButton>();
            BindCharge(charge, fillImg, glyphImg, glowObj, paleBase);

            // ★ 런타임 AddComponent 시 UnityEvent는 직렬화 초기화가 안 되어 null → 명시 초기화
            if (charge.onUsed == null) charge.onUsed = new UnityEngine.Events.UnityEvent();
            if (charge.onBecameReady == null) charge.onBecameReady = new UnityEngine.Events.UnityEvent();
            if (charge.onNotReady == null) charge.onNotReady = new UnityEngine.Events.UnityEvent();
            if (charge.onEditorFill == null) charge.onEditorFill = new UnityEngine.Events.UnityEvent();

            Kind kind = e.kind;
            charge.onUsed.AddListener(() => OnChargeUsed(kind));
            charge.onEditorFill.AddListener(() => OnEditorFill(kind)); // ★ 에디터 게이지 채우기 위임

            // ★ 스택 수량 배지 (게이지 레이어 = 누적 사용 횟수) — 우상단, 버튼 위에 표시
            var badgeGo = new GameObject("StackBadge", typeof(RectTransform));
            badgeGo.transform.SetParent(go.transform, false);
            var bRt = (RectTransform)badgeGo.transform;
            bRt.anchorMin = bRt.anchorMax = new Vector2(1f, 1f);
            bRt.pivot = new Vector2(0.5f, 0.5f); // 중앙 피벗 — 크기 변경 시 중심 고정(현 위치 유지)
            bRt.anchoredPosition = new Vector2(-21f, -24f); // 기존 중심(우상단 모서리, 왼쪽 이동분 포함) 유지
            bRt.sizeDelta = new Vector2(29f, 29f); // 22→29 (원형 30% 확대, 사용자 요청)
            var bImg = badgeGo.AddComponent<Image>();
            bImg.sprite = BadgeNormalSprite(); // 1~3 기본 코랄 (4 풀 시 갱신부에서 검정+흰테두리로 교체)
            bImg.type = Image.Type.Simple;
            bImg.raycastTarget = false;
            var txtGo = new GameObject("Count", typeof(RectTransform));
            txtGo.transform.SetParent(badgeGo.transform, false);
            var tRt = (RectTransform)txtGo.transform;
            tRt.anchorMin = Vector2.zero; tRt.anchorMax = Vector2.one;
            tRt.offsetMin = Vector2.zero; tRt.offsetMax = Vector2.zero;
            var txt = txtGo.AddComponent<Text>();
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.text = "1";
            txt.fontSize = 22; // 17→22 (게이지 숫자 추가 30% 확대, 사용자 요청)
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
            txt.raycastTarget = false;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow; // 큰 폰트가 22px 원에서 잘리지 않게
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            badgeGo.SetActive(false);
            _countBadges.Add(badgeGo);
            _countTexts.Add(txt);
            _countBadgeImgs.Add(bImg);

            // ★ "mp -N" 마나 소모량 라벨 (사용자 요청: 육각 프레임 "안" 하단에 표시, 폰트 2pt↓)
            int mpCost = GetMpCost(e.kind);
            var costGo = new GameObject("MpCostLabel", typeof(RectTransform));
            costGo.transform.SetParent(go.transform, false);
            var cRt = (RectTransform)costGo.transform;
            cRt.anchorMin = cRt.anchorMax = new Vector2(0.5f, 0.5f);
            cRt.pivot = new Vector2(0.5f, 0.5f);
            cRt.anchoredPosition = new Vector2(0f, -ButtonSize * 0.29f + 6f); // 육각형 내부 하단 + 위로 6px(3+3, 사용자 요청)
            cRt.sizeDelta = new Vector2(ButtonSize, 22f);
            var cTxt = costGo.AddComponent<Text>();
            cTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            cTxt.text = $"mp -{mpCost}";
            cTxt.fontSize = 13; // 15 → 13 (추가 2pt 축소, 사용자 요청)
            cTxt.fontStyle = FontStyle.Bold;
            cTxt.alignment = TextAnchor.MiddleCenter;
            cTxt.color = new Color(0.06f, 0.22f, 0.65f, 1f); // 진한 파란색 (사용자 요청: 시안블루 → 딥블루)
            cTxt.raycastTarget = false;
            cTxt.horizontalOverflow = HorizontalWrapMode.Overflow;
            cTxt.verticalOverflow = VerticalWrapMode.Overflow;
            var cOutline = costGo.AddComponent<Outline>();
            cOutline.effectColor = new Color(0.75f, 0.88f, 1f, 0.9f); // 진한 파랑 가독용 라이트 아웃라인(구 다크 → 반전)
            cOutline.effectDistance = new Vector2(1.4f, -1.4f);

            return charge;
        }

        /// <summary>아이템 종류별 MP 소모량 — MPManager 비용표 단일 출처(없으면 폴백).</summary>
        private int GetMpCost(Kind kind)
        {
            if (MPManager.Instance != null)
            {
                JewelsHexaPuzzle.Managers.ItemType t;
                switch (kind)
                {
                    case Kind.Hammer:  t = JewelsHexaPuzzle.Managers.ItemType.Hammer; break;
                    case Kind.Swap:    t = JewelsHexaPuzzle.Managers.ItemType.Bomb; break;            // Bomb = 스왑
                    case Kind.Line:    t = JewelsHexaPuzzle.Managers.ItemType.SSD; break;             // SSD = 라인
                    case Kind.Reverse: t = JewelsHexaPuzzle.Managers.ItemType.ReverseRotation; break;
                    default: return 0;
                }
                int c = MPManager.Instance.GetItemCost(t);
                if (c > 0) return c;
            }
            // 폴백(인스턴스 미초기화 시): 비용표 하드코딩 (망치10/스왑9/라인8/역회전5 — 2026-07-02 조정)
            switch (kind)
            {
                case Kind.Hammer:  return 10;
                case Kind.Swap:    return 9;
                case Kind.Line:    return 8;
                case Kind.Reverse: return 5;
                default: return 0;
            }
        }

        private JewelsHexaPuzzle.Core.BlockRemovalSystem _brsCache;

        /// <summary>ChargeBar 탭 → 구버전 게이지/아이템 발동에 위임 (튜토리얼 이벤트 포함).</summary>
        private void OnChargeUsed(Kind kind)
        {
            ResolveRefs();

            // ★ 게임 상태 게이팅 (감사 H5) — Playing 외 상태/캐스케이드(낙하·리필) 중 발동 차단.
            //   체인 전체(ChargeButton→Gauge→Item)에 상태 체크가 없어 캐스케이드 도중 망치 타격으로
            //   그리드 데이터가 경합하는 문제 방지. 표시 충전량은 매 프레임 게이지에서 재동기화되므로 유실 없음.
            if (GameManager.Instance != null &&
                GameManager.Instance.CurrentState != GameState.Playing)
            {
                Debug.Log($"[ChargeBarController] {kind} 발동 차단 — 상태 {GameManager.Instance.CurrentState}");
                return;
            }
            if (_brsCache == null) _brsCache = FindObjectOfType<JewelsHexaPuzzle.Core.BlockRemovalSystem>();
            if (_brsCache != null && _brsCache.IsProcessing)
            {
                Debug.Log($"[ChargeBarController] {kind} 발동 차단 — 캐스케이드 진행 중");
                return;
            }

            switch (kind)
            {
                case Kind.Hammer:
                    if (HammerGauge.Instance != null) HammerGauge.Instance.ActivateUseReady();
                    break;
                case Kind.Swap:
                    if (SwapGauge.Instance != null) SwapGauge.Instance.ActivateUseReady();
                    break;
                case Kind.Line:
                    if (LineGauge.Instance != null) LineGauge.Instance.ActivateUseReady();
                    break;
                case Kind.Reverse:
                    // 토글 — 구버전 버튼 onClick(OnButtonClicked)을 호출해 켜고/끄기 동일 동작
                    if (_reverse != null)
                    {
                        if (_reverse.ReverseButton != null) _reverse.ReverseButton.onClick.Invoke();
                        else _reverse.Activate();
                    }
                    break;
            }
        }

        /// <summary>★ 에디터 게이지 채우기 모드 탭 → 구버전 게이지 AddGaugeEditor 위임.
        /// 구 게이지 버튼이 숨겨져 직접 못 누르므로, ChargeBar가 채우기 입력을 대신 받는다.</summary>
        private void OnEditorFill(Kind kind)
        {
            switch (kind)
            {
                case Kind.Hammer: if (HammerGauge.Instance != null) HammerGauge.Instance.AddGaugeEditor(); break;
                case Kind.Swap:   if (SwapGauge.Instance != null) SwapGauge.Instance.AddGaugeEditor(); break;
                case Kind.Line:   if (LineGauge.Instance != null) LineGauge.Instance.AddGaugeEditor(); break;
                case Kind.Reverse: break; // 역회전은 게이지 없음 — 항상 사용 가능, 채울 것 없음
            }
        }

        /// <summary>ChargeButton의 private SerializeField에 reflection으로 값 주입 + autoRecharge off</summary>
        private static void BindCharge(HexaPuzzle.ChargeButton btn, Image fill, Image glyph, GameObject glow, Image paleBase)
        {
            var t = typeof(HexaPuzzle.ChargeButton);
            var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            t.GetField("colorFill", bf)?.SetValue(btn, fill);
            t.GetField("glyphIcon", bf)?.SetValue(btn, glyph); // 글리프 레이어 → 미충전 시 알파 디밍
            t.GetField("readyGlow", bf)?.SetValue(btn, glow);
            t.GetField("baseFill", bf)?.SetValue(btn, paleBase); // 2번째+ 레이어 옅은 바탕
            t.GetField("autoRecharge", bf)?.SetValue(btn, false); // 시간충전 끔 → 게이지 동기화
        }

        private static Image AddImg(GameObject parent, string name, Sprite sp, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent.transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.sprite = sp; img.color = color; img.raycastTarget = false;
            return img;
        }

        private void ResolveRefs()
        {
            if (_hammer == null)  _hammer  = Object.FindObjectOfType<HammerItem>(true);
            if (_swap == null)    _swap    = Object.FindObjectOfType<SwapItem>(true);
            if (_line == null)    _line    = Object.FindObjectOfType<LineDrawItem>(true);
            if (_reverse == null) _reverse = Object.FindObjectOfType<ReverseRotationItem>(true);
        }

        /// <summary>
        /// 외부에서 호출하여 표시 충전 비율을 즉시 0으로 리셋 (게임 시작 시).
        /// 보간이 진행 중이던 잔재 값(예: 이전 게임 종료 시 풀)을 깔끔히 비운다.
        /// </summary>
        public void ResetDisplayCharges()
        {
            for (int i = 0; i < _displayedRatios.Count; i++)
                _displayedRatios[i] = 0f;
            for (int i = 0; i < _buttons.Count; i++)
                if (_buttons[i] != null) _buttons[i].SetCharge(0f);
        }

        private void Update()
        {
            ResolveRefs();
            for (int i = 0; i < _buttons.Count; i++)
            {
                var btn = _buttons[i];
                if (btn == null) continue;

                // 대응 구버전 아이템 버튼의 활성 상태를 미러링 (기능 해금 게이팅 반영)
                GameObject host = HostObject(_kinds[i]);
                bool visible = host != null && host.activeInHierarchy;
                if (btn.gameObject.activeSelf != visible)
                    btn.gameObject.SetActive(visible);
                if (!visible) continue;

                // ★ 레이어형 표시: 누적 충전(GaugeLayer)≥1이면 "충전됨"(옅은 바탕) + 다음 레이어 진행을 fill로,
                //   0이면 첫 레이어 진행을 회색 바탕 위 fill로. 진행값은 MoveTowards로 부드럽게 보간.
                int stacks = GetStackCount(_kinds[i]);
                int maxLayer = GetMaxLayer(_kinds[i]);
                bool charged = _kinds[i] == Kind.Reverse ? true : stacks >= 1;
                // ★ 최대 레이어 도달(다음 단계로 진입 불가) → 옅은 바탕 없이 원래 색 풀 표시.
                //   런 초반 스킬 미해금 시 maxLayer=1 → 1단계 충전이 곧 최대 → 원래 색.
                bool atMax = _kinds[i] == Kind.Reverse ? true : (maxLayer >= 1 && stacks >= maxLayer);
                float target = GetLayerProgress(_kinds[i]);
                float displayed = _displayedRatios[i];
                float speed = target < displayed ? FillFollowSpeed * 3f : FillFollowSpeed;
                displayed = Mathf.MoveTowards(displayed, target, speed * Time.deltaTime);
                _displayedRatios[i] = displayed;
                btn.SetLayeredCharge(charged, displayed, atMax);

                // ★ 스택 수량 배지 갱신 (누적 사용 횟수 ≥1일 때 표시) — 위에서 구한 stacks 재사용
                if (i < _countBadges.Count && _countBadges[i] != null)
                {
                    bool showCount = stacks >= 1;
                    if (_countBadges[i].activeSelf != showCount) _countBadges[i].SetActive(showCount);
                    if (showCount && _countTexts[i] != null) JewelsHexaPuzzle.Utils.NumberRoller.Roll(_countTexts[i], stacks, v => v.ToString(), 0.25f);
                    // ★ 4게이지 풀 = 검정 바탕+흰 테두리(숫자 흰색), 1~3 = 기존 코랄 유지 (사용자 요청)
                    if (showCount && i < _countBadgeImgs.Count && _countBadgeImgs[i] != null)
                    {
                        var wantSpr = stacks >= 4 ? BadgeMaxSprite() : BadgeNormalSprite();
                        if (_countBadgeImgs[i].sprite != wantSpr) _countBadgeImgs[i].sprite = wantSpr;
                    }
                }
            }
        }

        /// <summary>현재 최대 레이어 수(스킬 레벨 기반, 1+level). 1이면 2단계로 진입 불가. 역회전/없음은 1.</summary>
        private int GetMaxLayer(Kind kind)
        {
            switch (kind)
            {
                case Kind.Hammer: return HammerGauge.Instance != null ? HammerGauge.Instance.GetCurrentMaxLayer() : 1;
                case Kind.Swap:   return SwapGauge.Instance != null ? SwapGauge.Instance.GetCurrentMaxLayer() : 1;
                case Kind.Line:   return LineGauge.Instance != null ? LineGauge.Instance.GetCurrentMaxLayer() : 1;
                default:          return 1;
            }
        }

        /// <summary>누적 사용 횟수(스택) = 구버전 게이지 레이어 수. 역회전은 누적 없음.</summary>
        private int GetStackCount(Kind kind)
        {
            switch (kind)
            {
                case Kind.Hammer: return HammerGauge.Instance != null ? HammerGauge.Instance.GaugeLayer : 0;
                case Kind.Swap:   return SwapGauge.Instance != null ? SwapGauge.Instance.GaugeLayer : 0;
                case Kind.Line:   return LineGauge.Instance != null ? LineGauge.Instance.GaugeLayer : 0;
                default:          return 0; // Reverse: 누적 개념 없음
            }
        }

        private GameObject HostObject(Kind kind)
        {
            switch (kind)
            {
                case Kind.Hammer:  return _hammer != null ? _hammer.gameObject : null;
                case Kind.Swap:    return _swap != null ? _swap.gameObject : null;
                case Kind.Line:    return _line != null ? _line.gameObject : null;
                case Kind.Reverse: return _reverse != null ? _reverse.gameObject : null;
            }
            return null;
        }

        // ★ 옅은 바탕 색 — 아이템 색을 흰색 쪽으로 크게 섞은 연한 톤(밝게). vivid 채움과 명확히 대비.
        private static Color PaleColor(Kind kind)
        {
            Color c;
            switch (kind)
            {
                case Kind.Hammer:  c = new Color(0.93f, 0.18f, 0.18f); break; // 빨강
                case Kind.Swap:    c = new Color(0.18f, 0.78f, 0.28f); break; // 초록
                case Kind.Line:    c = new Color(0.62f, 0.20f, 0.88f); break; // 보라
                default:           c = new Color(0.22f, 0.60f, 0.95f); break; // 역회전: 파랑
            }
            // 흰색으로 0.62 섞어 "옅은(밝은) 색"으로, 알파는 높게 유지(또렷한 바탕)
            return new Color(
                Mathf.Lerp(c.r, 1f, 0.62f),
                Mathf.Lerp(c.g, 1f, 0.62f),
                Mathf.Lerp(c.b, 1f, 0.62f),
                0.95f);
        }

        // ★ 흰색 flat-top 육각형 스프라이트 (프로시저럴, 1회 생성 캐시) — 옅은 바탕 틴트용(곱연산으로 밝히기 위해 흰 베이스 필요).
        private static Sprite _whiteHexSprite;
        private static Sprite WhiteHexSprite()
        {
            if (_whiteHexSprite != null) return _whiteHexSprite;
            const int SZ = 64;
            var tex = new Texture2D(SZ, SZ, TextureFormat.RGBA32, false);
            var px = new Color[SZ * SZ];
            float c = (SZ - 1) * 0.5f;
            float R = SZ * 0.46f;                 // 중심→좌우 꼭짓점 (프레임 안쪽에 살짝 인셋)
            float h = Mathf.Sqrt(3f) * 0.5f * R;  // 상/하 평평한 변까지 거리
            float sqrt3 = Mathf.Sqrt(3f);
            for (int y = 0; y < SZ; y++)
                for (int x = 0; x < SZ; x++)
                {
                    float qx = Mathf.Abs(x - c), ry = Mathf.Abs(y - c);
                    // flat-top 육각형 내부 판정 + 가장자리 1.5px 안티앨리어스
                    float d1 = h - ry;                       // 상/하 변
                    float d2 = (sqrt3 * R - (sqrt3 * qx + ry)) / 2f; // 빗변(정규화 거리)
                    float dist = Mathf.Min(d1, d2);
                    float a = Mathf.Clamp01(dist / 1.5f);
                    px[y * SZ + x] = new Color(1f, 1f, 1f, a);
                }
            tex.SetPixels(px);
            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            _whiteHexSprite = Sprite.Create(tex, new Rect(0, 0, SZ, SZ), new Vector2(0.5f, 0.5f), 100f);
            return _whiteHexSprite;
        }

        /// <summary>현재 레이어 내 진행 비율(GaugeInLayer/50). 누적 충전 여부와 무관하게 "지금 차는 레이어"의 진행만.
        /// (구버전: GaugeLayer>=1이면 1 고정이라 2번째+ 레이어 진행이 안 보였음 → 항상 InLayer 진행을 표시하도록 변경)</summary>
        private float GetLayerProgress(Kind kind)
        {
            switch (kind)
            {
                case Kind.Hammer:
                    return HammerGauge.Instance != null ? HammerGauge.Instance.CurrentLayerFillRatio : 0f; // ★ 단계별 임계값(10/8/6/4) 기준 진행
                case Kind.Swap:
                    return SwapGauge.Instance != null ? SwapGauge.Instance.CurrentLayerFillRatio : 0f;
                case Kind.Line:
                    return LineGauge.Instance != null ? LineGauge.Instance.CurrentLayerFillRatio : 0f;
                case Kind.Reverse:
                    return 1f; // 역회전은 충전 개념 없음 — 항상 사용 가능(가득 표시)
            }
            return 0f;
        }
    }
}
