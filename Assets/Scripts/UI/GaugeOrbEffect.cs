using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using JewelsHexaPuzzle.Data;

namespace JewelsHexaPuzzle.UI
{
    /// <summary>
    /// 블록(수정) 정화 시점에 빠져나오는 "영혼(soul)" 오브 — 작은 점이 꼬리 트레일을 그리며
    /// 베지어 곡선으로 대응 게이지 UI 위치로 포물선 비행, 도착 시 작은 punch 이펙트로 흡수되며 사라진다.
    /// ★ 실제 게이지 증가는 영혼이 게이지 통에 "도착하는 순간"(onArrived 콜백)에 처리되어
    ///   흡수 연출과 수치 증가가 정확히 싱크된다. (호출처 BlockRemovalSystem 5d 참조)
    ///
    /// 색상 매핑(블록 → 게이지):
    ///   Red    → HammerButton      (망치)
    ///   Green  → SwapButton        (스왑)
    ///   Purple → LineDrawButton    (라인)
    ///   Blue   → MPGaugeUI         (마나)
    ///   Orange → SkillProgressGauge (리워드)
    ///   Yellow → 표시 안 함 (대응 게이지 없음 → 즉시 onArrived)
    /// </summary>
    public class GaugeOrbEffect : MonoBehaviour
    {
        private static Sprite _glowSprite;   // 부드러운 방사형 글로우 (영혼 위스프 단일 소스)
        private static Canvas _cachedCanvas;
        private const float ORB_SIZE = 56f;  // 영혼 위스프 크기 (가시성 ↔ 영혼감 균형)

        // ★ Enter Play Mode Options(도메인 리로드 비활성) 환경 대비 — 매 Play 세션 시작 시 static 초기화.
        //   런타임 텍스처/스프라이트는 Play 종료 시 파괴되나 static 참조는 잔류 → 명시적으로 비워
        //   다음 세션이 항상 새 스프라이트/캐시를 만들게 한다(이전 세션 stale 참조 차단).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _glowSprite = null;
            _cachedCanvas = null;
            _targetCache.Clear();
            _impacting.Clear();
            _ghostPool.Clear();
        }

        /// <summary>
        /// 외부 호출 진입점 — 블록 위치(월드) + 색에서 영혼 발사.
        /// onArrived: 영혼이 게이지 통에 도착하는 순간 호출 (예: 마나 +1 싱크).
        /// </summary>
        public static void Spawn(Vector3 worldStart, GemType gemType, System.Action onArrived = null)
        {
            if (gemType == GemType.None) { onArrived?.Invoke(); return; }

            RectTransform target = ResolveTarget(gemType);
            if (target == null) { onArrived?.Invoke(); return; }  // 매핑 없는 색/게이지 비활성: 즉시 콜백(충전 누락 방지)

            // ★ 핵심 수정(2026-06-20): 영혼을 "도착 게이지가 실제로 렌더되는 캔버스"에 부모지정.
            //   기존엔 HexGrid 부모 캔버스(보드)에 붙였으나, HUD/게이지가 더 높은 sortingOrder의
            //   별도 캔버스에 있으면 보드 캔버스 오브가 가려져(또는 다른 좌표계라 화면 밖) "안 보임".
            //   타겟(게이지) 캔버스에 붙이면 게이지가 보이는 한 영혼도 반드시 보이고 도착 좌표도 정확.
            //   (작동 확인된 UIManager.PlayMoveRewardSoul이 turnText.canvas를 쓰는 것과 동일 원리)
            Canvas canvas = target.GetComponentInParent<Canvas>();
            if (canvas == null) canvas = ResolveCanvas();
            if (canvas == null) { Debug.LogWarning($"[GaugeOrb] 캔버스 미발견 — {gemType}"); onArrived?.Invoke(); return; }

            Color color = GetOrbColor(gemType);

            // 영혼 오브 루트(빈 RectTransform) — 본체도 자식으로 두어 뒤→앞 렌더 순서를 제어.
            //   (uGUI: 부모 자체 Graphic은 항상 자식보다 뒤에 그려지므로, 글로우를 '본체 뒤'에 두려면
            //    본체를 자식으로 분리해야 한다. 기존 구조는 글로우가 본체 앞에 그려지던 잠복 버그.)
            GameObject orbObj = new GameObject("GaugeOrb_" + gemType);
            orbObj.transform.SetParent(canvas.transform, false);
            var rt = orbObj.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(ORB_SIZE, ORB_SIZE);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            orbObj.transform.SetAsLastSibling();

            BuildOrbVisual(orbObj.transform, color);

            // 시작/종료 좌표를 캔버스 로컬(anchored)로 변환
            Vector2 startPos = WorldToCanvasLocal(canvas, worldStart);
            Vector2 endPos = TargetCanvasLocal(canvas, target);
            // 베지어 중간 control point — 직선 경로에서 약간 휘어진 곡선 만들기.
            Vector2 dir = endPos - startPos;
            float perpScale = Mathf.Clamp(dir.magnitude * 0.25f, 30f, 120f);
            Vector2 perp = new Vector2(-dir.y, dir.x).normalized * perpScale;
            // 무작위로 위/아래 살짝 흔들리게 (소울 비행 느낌)
            float jitter = (Mathf.PerlinNoise(Time.time * 7f, worldStart.x) - 0.5f) * 40f;
            Vector2 midPos = (startPos + endPos) * 0.5f + perp + new Vector2(0, jitter);

            rt.anchoredPosition = startPos;

            var runner = orbObj.AddComponent<GaugeOrbEffect>();
            runner.StartCoroutine(runner.Fly(rt, startPos, midPos, endPos, color, canvas, target, onArrived));
        }

        private IEnumerator Fly(RectTransform rt,
                                Vector2 start, Vector2 mid, Vector2 end,
                                Color color, Canvas canvas, RectTransform target,
                                System.Action onArrived)
        {
            float dur = 0.72f; // 영혼 비행 시간 — 떨림·트레일을 눈으로 따라가기 좋은 길이
            float t = 0f;
            float ghostTimer = 0f;
            const float ghostInterval = 0.025f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime; // ★ 게임 timeScale(3배속·hitstop)에 무관하게 실시간 비행
                if (rt == null) { onArrived?.Invoke(); yield break; } // 중간 소실 시에도 게이지 증가 보장
                float k = Mathf.Clamp01(t / dur);
                // ease-in-out cubic
                float ke = k < 0.5f ? 4f * k * k * k : 1f - Mathf.Pow(-2f * k + 2f, 3f) / 2f;
                // 베지어 (start, mid, end)
                Vector2 a = Vector2.Lerp(start, mid, ke);
                Vector2 b = Vector2.Lerp(mid, end, ke);
                Vector2 pos = Vector2.Lerp(a, b, ke);
                // ★ 영혼 떨림 — 살아있는 듯 미세 진동, 도착할수록 감쇠 (이동보상 영혼 MoveRewardSoul과 동일 계열)
                //   감쇠는 선형 진행(k) 기준 — 이징(ke)을 쓰면 도착 직전 떨림이 너무 일찍 죽어 원본과 어긋남.
                pos += new Vector2(Mathf.Sin(k * 22f) * 5f, Mathf.Cos(k * 18f) * 4f) * (1f - k);
                rt.anchoredPosition = pos;

                // ★ 스케일: 비행 중 호흡(sin)하며 점점 작아져 게이지에 "흡수"되는 느낌 (이동보상 영혼과 동일)
                float breath = 1f + Mathf.Sin(k * Mathf.PI) * 0.28f;
                rt.localScale = Vector3.one * Mathf.Lerp(1.1f, 0.5f, ke) * breath;

                // 트레일 잔상 — 위스피 ghost (fade out 후 풀 반납). 비행과 동일 unscaled 기준으로 균일 간격.
                ghostTimer += Time.unscaledDeltaTime;
                if (ghostTimer >= ghostInterval)
                {
                    ghostTimer = 0f;
                    SpawnGhost(canvas, pos, color);
                }
                yield return null;
            }
            // 도착 위치 스냅
            if (rt != null) rt.anchoredPosition = end;

            // ★ 도착 순간 게이지 증가 콜백 (마나 +1 등) — 영혼이 통에 들어가는 시점과 싱크.
            if (JewelsHexaPuzzle.Managers.AudioManager.Instance != null) JewelsHexaPuzzle.Managers.AudioManager.Instance.PlayOrbAbsorbSound(); // ★ 효과음: 영혼 흡수 톡
            onArrived?.Invoke();

            // ★ 흡수 임팩트(링+코어 플래시+스파크) — 게이지당 1회만(가드)로 대량 동시 매칭 perf 보호.
            //   영혼 본체는 즉시 소멸(이미 작게 축소된 상태 → 통에 빨려든 느낌).
            SpawnImpact(canvas, end, color, target);
            if (rt != null) Destroy(rt.gameObject);
        }

        // ──────────────────────────────────────────────────────────────
        // 트레일 잔상 (짧은 fade-out 후 풀 반납)
        // ★ 풀링 (감사 M4) — 대형 캐스케이드에서 초당 수백 개 GameObject 생성/파괴로
        //   GC·캔버스 리빌드 스파이크가 발생하던 것을 재사용으로 차단.
        // ──────────────────────────────────────────────────────────────
        private static readonly Queue<GameObject> _ghostPool = new Queue<GameObject>();
        private const int GHOST_POOL_CAP = 64;

        private static void SpawnGhost(Canvas canvas, Vector2 pos, Color color)
        {
            GameObject g = null;
            // 씬 전환으로 파괴된 항목은 건너뛰며 꺼냄
            while (_ghostPool.Count > 0 && g == null)
                g = _ghostPool.Dequeue();

            OrbGhostRunner runner;
            if (g == null)
            {
                g = new GameObject("OrbGhost");
                var rt = g.AddComponent<RectTransform>();
                rt.sizeDelta = new Vector2(18f, 18f);   // 위스피 트레일 (소프트 글로우)
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                var img0 = g.AddComponent<Image>();
                img0.sprite = GetGlowSprite();
                img0.raycastTarget = false;
                runner = g.AddComponent<OrbGhostRunner>();
            }
            else
            {
                runner = g.GetComponent<OrbGhostRunner>();
                if (runner == null) runner = g.AddComponent<OrbGhostRunner>();
            }

            g.transform.SetParent(canvas.transform, false);
            var grt = (RectTransform)g.transform;
            grt.anchoredPosition = pos;
            g.transform.localScale = Vector3.one;
            var img = g.GetComponent<Image>();
            if (img != null) img.color = new Color(color.r, color.g, color.b, 0.45f); // 옅게 → 소프트 잔향
            g.SetActive(true);
            runner.Play(_ghostPool, GHOST_POOL_CAP);
        }

        // ──────────────────────────────────────────────────────────────
        // 도착 흡수 임팩트 (확장 링 + 방사형 스파크) — 게이지당 동시 1회만(가드).
        // ★ 같은 프레임에 다수 영혼이 같은 게이지에 도착해도 풀 임팩트는 1회만 → 대량 캐스케이드 perf 스파이크 차단.
        //   (트레일은 풀링, 임팩트는 가드 → 도착 절차가 항상 경량)
        // ──────────────────────────────────────────────────────────────
        private static readonly HashSet<RectTransform> _impacting = new HashSet<RectTransform>();

        private static void SpawnImpact(Canvas canvas, Vector2 pos, Color color, RectTransform target)
        {
            if (target != null)
            {
                if (_impacting.Contains(target)) return; // 이미 흡수 연출 중 → 중복 스킵
                _impacting.Add(target);
            }

            // 1) 확장 링 (게이지가 빛을 머금는 흡수 펄스)
            GameObject ring = new GameObject("OrbImpact");
            ring.transform.SetParent(canvas.transform, false);
            var rt = ring.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(40f, 40f);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            var img = ring.AddComponent<Image>();
            img.sprite = GetGlowSprite();
            img.color = new Color(color.r, color.g, color.b, 0.9f);
            img.raycastTarget = false;
            ring.transform.SetAsLastSibling();
            var runner = ring.AddComponent<OrbImpactRunner>();
            runner.guardSet = _impacting; runner.guardKey = target; // 소멸 시 가드 해제

            // 2) 방사형 스파크 6개 (흡수 시 튀는 빛)
            for (int i = 0; i < 6; i++)
            {
                float ang = i * 60f * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                GameObject sp = new GameObject("OrbSpark");
                sp.transform.SetParent(canvas.transform, false);
                var srt = sp.AddComponent<RectTransform>();
                srt.sizeDelta = new Vector2(12f, 12f);
                srt.anchorMin = srt.anchorMax = new Vector2(0.5f, 0.5f);
                srt.pivot = new Vector2(0.5f, 0.5f);
                srt.anchoredPosition = pos;
                var simg = sp.AddComponent<Image>();
                simg.sprite = GetGlowSprite();
                simg.color = color;
                simg.raycastTarget = false;
                sp.transform.SetAsLastSibling();
                sp.AddComponent<OrbSparkRunner>().Init(dir * Random.Range(34f, 58f), 0.38f);
            }
        }

        // ──────────────────────────────────────────────────────────────
        // 매핑 / 헬퍼
        // ──────────────────────────────────────────────────────────────
        private static Canvas ResolveCanvas()
        {
            if (_cachedCanvas != null && _cachedCanvas.gameObject.activeInHierarchy) return _cachedCanvas;
            // ★ HexGrid 부모 캔버스를 우선 사용 (블록과 동일 캔버스 → 좌표 변환 일관성).
            //   MPManager.FindCanvas와 동일 패턴. 다른 캔버스를 잡으면 영혼이 엉뚱한 위치로 가 안 보임.
            var hexGrid = Object.FindObjectOfType<JewelsHexaPuzzle.Core.HexGrid>();
            if (hexGrid != null)
            {
                var c = hexGrid.GetComponentInParent<Canvas>();
                if (c != null) { _cachedCanvas = c; return c; }
            }
            _cachedCanvas = Object.FindObjectOfType<Canvas>();
            return _cachedCanvas;
        }

        // ★ 타겟 캐시 (감사 M4) — 블록 1개당 GameObject.Find/FindObjectOfType 호출 제거.
        //   파괴/비활성화되면 자동 재검색 (Unity null + activeInHierarchy 검사).
        private static readonly Dictionary<GemType, RectTransform> _targetCache = new Dictionary<GemType, RectTransform>();

        private static RectTransform ResolveTarget(GemType gemType)
        {
            if (_targetCache.TryGetValue(gemType, out var cached) &&
                cached != null && cached.gameObject.activeInHierarchy)
                return cached;

            RectTransform found = null;
            string name = null;
            switch (gemType)
            {
                case GemType.Red:    name = "HammerButton";          break;
                case GemType.Green:  name = "SwapButton";            break;
                case GemType.Purple: name = "LineDrawButton";        break;
                // 리워드 게이지(SkillProgressGauge) — 주황 블록 10개당 스킬 학습 (스테이지 21+).
                //   게이지 비활성(레벨 미달/미표시) 시 Find 실패 → null → 즉시 onArrived(리워드 카운트는 정상 누적).
                case GemType.Orange: name = "SkillProgressGauge";    break;
                // 골드 카운터(HUD_GoldText, 우상단) — 노랑 블록 정화 영혼이 골드 표시로 흡수되며 골드 증가.
                case GemType.Yellow: name = "HUD_GoldText";          break;
                case GemType.Blue:
                {
                    // MP 게이지 UI — MPGaugeUI 컴포넌트 위치
                    var mpUI = Object.FindObjectOfType<MPGaugeUI>();
                    found = mpUI != null ? mpUI.GetComponent<RectTransform>() : null;
                    _targetCache[gemType] = found;
                    return found;
                }
            }
            if (string.IsNullOrEmpty(name)) return null;
            // Hierarchy에서 이름으로 찾기 (ChargeBar 자식 — LegacyButtonHider가 구버전엔 _legacy 접미사 붙임).
            //   비활성 상태 자손은 못 찾음. 따라서 활성 캔버스에 있는 ChargeBar 자식만 매칭됨.
            GameObject go = GameObject.Find(name);
            found = go != null ? go.GetComponent<RectTransform>() : null;
            _targetCache[gemType] = found;
            return found;
        }

        // ──────────────────────────────────────────────────────────────
        // 영혼 오브 비주얼 합성 — 이동보상 영혼(UIManager.MoveRewardSoul)과 동일 계열의 "영혼 위스프".
        //   전부 소프트 방사형 글로우. 하드 솔리드 원 + 흰 림/코어는 '공/사탕'처럼 보여 영혼 느낌을 해쳐 폐기.
        //   뒤→앞 3겹: ① 큰 후광(Halo) ② 본체(Body) ③ 밝은 속심(Heart, 흰빛 머금은 심장).
        //   영혼다움은 비주얼 못지않게 '떨림 모션 + 위스피 트레일'에서 나옴(Fly/SpawnGhost 참조).
        //   본체를 빈 루트의 자식으로 분리해 글로우가 진짜 본체 뒤에 오도록 함(uGUI 부모 그래픽=항상 뒤 회피).
        // ──────────────────────────────────────────────────────────────
        private static void BuildOrbVisual(Transform root, Color color)
        {
            Color heart = new Color(
                Mathf.Lerp(color.r, 1f, 0.6f), Mathf.Lerp(color.g, 1f, 0.6f), Mathf.Lerp(color.b, 1f, 0.6f), 0.92f);
            AddOrbChild(root, "Halo",  new Color(color.r, color.g, color.b, 0.40f), 22f);  // ① 큰 후광
            AddOrbChild(root, "Body",  new Color(color.r, color.g, color.b, 0.95f), 0f);   // ② 본체
            AddOrbChild(root, "Heart", heart,                                       -14f); // ③ 밝은 속심
        }

        // 오브 자식 원 추가(전부 소프트 글로우). expand>0 → 본체보다 크게, expand<0 → 작게.
        private static void AddOrbChild(Transform parent, string name, Color col, float expand)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(-expand, -expand);
            rt.offsetMax = new Vector2(expand, expand);
            var img = go.AddComponent<Image>();
            img.sprite = GetGlowSprite();
            img.color = col;
            img.raycastTarget = false;
        }

        private static Color GetOrbColor(GemType gemType)
        {
            Color c = GemColors.GetColor(gemType);
            // 영혼 느낌 — 채도 유지하며 더 밝고 선명하게 (블록보다 한 단계 밝아 대비↑).
            //   원색을 흰색 쪽으로 30% 끌어올려 같은 색 블록 위에서도 떠 보이게 한다. (속심 Heart가 추가 발광)
            float lift = 0.3f;
            return new Color(
                Mathf.Clamp01(c.r + (1f - c.r) * lift),
                Mathf.Clamp01(c.g + (1f - c.g) * lift),
                Mathf.Clamp01(c.b + (1f - c.b) * lift),
                1f);
        }

        private static Vector2 WorldToCanvasLocal(Canvas canvas, Vector3 worldPos)
        {
            // ★ MPManager 팝업과 동일 변환 — canvas.worldCamera 사용(Overlay면 null, Camera 모드면 카메라).
            var canvasRect = (RectTransform)canvas.transform;
            Camera cam = canvas.worldCamera;
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, worldPos);
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, cam, out local);
            return local;
        }

        private static Vector2 TargetCanvasLocal(Canvas canvas, RectTransform target)
        {
            var canvasRect = (RectTransform)canvas.transform;
            Camera cam = canvas.worldCamera;
            // ★ target.position은 '피벗' 월드 좌표 — MP/리워드 게이지는 pivot=(0.5,1)이라 피벗이 통의
            //   '상단 중앙'이라서 영혼이 게이지 위쪽에 흡수됐다. rect.center(피벗 보정된 사각형 중심)를
            //   월드로 변환해 항상 게이지의 '기하학적 중앙'에 흡수되도록 한다(모든 게이지 공통).
            Vector3 worldCenter = target.TransformPoint(target.rect.center);
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, worldCenter);
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, cam, out local);
            return local;
        }

        // 부드러운 방사형 글로우 — 영혼 본체/후광/속심/트레일/임팩트 전부 이 단일 소스 사용 ((1-d)² 페이드).
        //   ★ 텍스처 유효성까지 검사: 도메인 리로드 비활성 환경의 stale static 대비.
        private static Sprite GetGlowSprite()
        {
            if (_glowSprite != null && _glowSprite.texture != null) return _glowSprite;
            const int SZ = 48;
            var tex = new Texture2D(SZ, SZ, TextureFormat.RGBA32, false);
            Color[] px = new Color[SZ * SZ];
            float c = (SZ - 1) * 0.5f, r = SZ * 0.5f;
            for (int y = 0; y < SZ; y++)
                for (int x = 0; x < SZ; x++)
                {
                    float dn = Mathf.Clamp01(Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / r);
                    float a = (1f - dn); a *= a;
                    px[y * SZ + x] = new Color(1f, 1f, 1f, a);
                }
            tex.SetPixels(px); tex.Apply();
            tex.filterMode = FilterMode.Bilinear; tex.wrapMode = TextureWrapMode.Clamp;
            _glowSprite = Sprite.Create(tex, new Rect(0, 0, SZ, SZ), new Vector2(0.5f, 0.5f), 100f);
            return _glowSprite;
        }
    }

    /// <summary>잔상 점 — 0.25초간 fade out + scale down 후 풀 반납 (감사 M4: Destroy 대신 재사용).</summary>
    public class OrbGhostRunner : MonoBehaviour
    {
        private Queue<GameObject> _pool;
        private int _poolCap;
        private Coroutine _fade;

        /// <summary>풀에서 꺼낸 뒤(또는 신규 생성 후) 페이드 시작. 종료 시 풀 반납.</summary>
        public void Play(Queue<GameObject> pool, int poolCap)
        {
            _pool = pool;
            _poolCap = poolCap;
            if (_fade != null) StopCoroutine(_fade);
            _fade = StartCoroutine(FadeAndRelease());
        }

        private IEnumerator FadeAndRelease()
        {
            var img = GetComponent<Image>();
            if (img == null) { Release(); yield break; }
            float dur = 0.25f, t = 0f;
            Color c0 = img.color;
            Vector3 s0 = transform.localScale;
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / dur);
                img.color = new Color(c0.r, c0.g, c0.b, c0.a * (1f - k));
                transform.localScale = Vector3.Lerp(s0, s0 * 0.4f, k);
                yield return null;
            }
            Release();
        }

        private void Release()
        {
            _fade = null;
            if (_pool != null && _pool.Count < _poolCap)
            {
                gameObject.SetActive(false);
                _pool.Enqueue(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }

    /// <summary>도착 흡수 링 — 0.32초간 확장 + 페이드. 소멸 시 게이지 임팩트 가드 해제.</summary>
    public class OrbImpactRunner : MonoBehaviour
    {
        // ★ 게이지당 동시 1회 가드 — 소멸 시 해제하여 다음 매칭 버스트에서 다시 임팩트 가능.
        public HashSet<RectTransform> guardSet;
        public RectTransform guardKey;

        private void Start() { StartCoroutine(PunchAndDestroy()); }
        private IEnumerator PunchAndDestroy()
        {
            var img = GetComponent<Image>();
            if (img == null) { Destroy(gameObject); yield break; }
            float dur = 0.32f, t = 0f;
            Color c0 = img.color;
            Vector3 s0 = transform.localScale;
            Vector3 sEnd = s0 * 2.2f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / dur);
                // ease-out cubic
                float ke = 1f - Mathf.Pow(1f - k, 3f);
                img.color = new Color(c0.r, c0.g, c0.b, c0.a * (1f - k));
                transform.localScale = Vector3.Lerp(s0, sEnd, ke);
                yield return null;
            }
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (guardSet != null) guardSet.Remove(guardKey); // 파괴 경로 무관 항상 해제(스테일 방지)
        }
    }

    /// <summary>흡수 스파크 — 방사 방향으로 튀어나가며 0.38초간 페이드+축소 후 소멸.</summary>
    public class OrbSparkRunner : MonoBehaviour
    {
        private Vector2 _disp;
        private float _dur;

        public void Init(Vector2 disp, float dur)
        {
            _disp = disp; _dur = dur;
            StartCoroutine(Fly());
        }

        private IEnumerator Fly()
        {
            var img = GetComponent<Image>();
            var rt = (RectTransform)transform;
            Vector2 p0 = rt.anchoredPosition;
            Color c0 = img != null ? img.color : Color.white;
            float t = 0f;
            while (t < _dur && rt != null)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / _dur);
                float e = 1f - Mathf.Pow(1f - k, 2f); // ease-out
                rt.anchoredPosition = p0 + _disp * e;
                if (img != null) { var c = c0; c.a = c0.a * (1f - k); img.color = c; }
                rt.localScale = Vector3.one * (1f - 0.6f * k);
                yield return null;
            }
            if (rt != null) Destroy(rt.gameObject);
        }
    }
}
