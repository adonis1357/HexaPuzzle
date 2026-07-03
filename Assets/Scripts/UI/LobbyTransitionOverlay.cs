using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace JewelsHexaPuzzle.UI
{
    /// <summary>
    /// 인게임 ↔ 로비 전환 시 화면 전체를 덮는 로딩 오버레이.
    ///
    /// 두 가지 모드:
    ///   - PlayTransition(action):           단순 어두운 페이드 (작업만 가리기)
    ///   - PlayTransitionWithImage(action):  Resources/Transitions/elf_beach 이미지 + 2초 노출
    ///
    /// ★ 별도 MonoBehaviour 컴포넌트로 둔 이유:
    ///   GameManager.ForceResetAllGameSystems가 StopAllCoroutines를 호출해
    ///   GameManager 본체에서 시작한 코루틴은 모두 중단된다. 별도 컴포넌트면
    ///   페이드 아웃 코루틴이 영향받지 않아 오버레이가 화면에 영원히 남는 일이 없다.
    ///
    /// ★ Time.timeScale 변경에도 안전하도록 unscaledDeltaTime 기준 페이드.
    /// </summary>
    public class LobbyTransitionOverlay : MonoBehaviour
    {
        public static LobbyTransitionOverlay Instance { get; private set; }

        // 배경 (검정)
        private Image backdrop;
        // 중앙 이미지 (Resources/Transitions/elf_beach)
        private Image splashImage;
        private static Sprite _splashSprite;

        // ★ 도메인 리로드 비활성(EnterPlayMode) 환경에서 정적 캐시가 옛 이미지로 남는 것 방지 — 매 Play 시작 시 리셋해 최신 에셋 재로드.
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticCache() { _splashSprite = null; }

        private bool isRunning;
        /// <summary>전환이 진행 중인지 (페이드 인부터 페이드 아웃 완료까지). 다른 코루틴이 대기할 때 사용.</summary>
        public bool IsRunning => isRunning;

        // 페이드 사양 (필요 시 외부에서 조정)
        public float FadeInDuration = 0.20f;
        public float ImageHoldSeconds = 2.0f;
        public float SimpleHoldSeconds = 0.30f;
        public float FadeOutDuration = 0.30f;
        public Color BackdropColor = new Color(0.04f, 0.03f, 0.06f, 1f);

        private void Awake()
        {
            Instance = this;
        }

        /// <summary>Canvas 자식으로 부착하고 풀스크린 RectTransform + 입력 차단 Image를 구성한다.</summary>
        public void Initialize(Transform canvasTransform)
        {
            transform.SetParent(canvasTransform, false);
            var rt = GetComponent<RectTransform>();
            if (rt == null) rt = gameObject.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;

            // 1. 검정 배경 (가장 뒤)
            backdrop = GetComponent<Image>();
            if (backdrop == null) backdrop = gameObject.AddComponent<Image>();
            var bc = BackdropColor; bc.a = 0f;
            backdrop.color = bc;
            backdrop.raycastTarget = true;  // 입력 차단

            // 2. 중앙 이미지 (자식) — 화면 전체 채움 + 비율 유지(cover).
            //   AspectRatioFitter EnvelopeParent = 부모(전체 화면)를 가득 덮되 비율 유지,
            //   넘치는 축은 화면 밖으로 잘림. (preserveAspect=letterbox는 빈 공간이 생겨 부적합)
            GameObject imgObj = new GameObject("SplashImage");
            imgObj.transform.SetParent(transform, false);
            var irt = imgObj.AddComponent<RectTransform>();
            irt.anchorMin = irt.anchorMax = new Vector2(0.5f, 0.5f);
            irt.pivot = new Vector2(0.5f, 0.5f);
            splashImage = imgObj.AddComponent<Image>();
            splashImage.preserveAspect = false;
            splashImage.raycastTarget = false;
            splashImage.color = new Color(1f, 1f, 1f, 0f);
            EnsureSplashSpriteLoaded();
            splashImage.sprite = _splashSprite;

            // cover 핏터 — 이미지 원본 비율로 부모를 가득 덮음
            var fitter = imgObj.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            if (_splashSprite != null && _splashSprite.rect.height > 0f)
                fitter.aspectRatio = _splashSprite.rect.width / _splashSprite.rect.height;
            else
                fitter.aspectRatio = 1080f / 1920f; // 폴백(세로 비율)

            splashImage.enabled = false;  // 기본: 단순 모드에서는 안 보임

            transform.SetAsLastSibling();
            gameObject.SetActive(false);
        }

        private static void EnsureSplashSpriteLoaded()
        {
            if (_splashSprite != null) return;

            // ★ 1차: Sprite 직접 로드 (textureType=Sprite로 import된 경우 가장 신뢰성 있음)
            _splashSprite = Resources.Load<Sprite>("Transitions/elf_beach");
            if (_splashSprite != null)
            {
                Debug.Log("[LobbyTransitionOverlay] Sprite 직접 로드 성공: Transitions/elf_beach");
                return;
            }

            // ★ 2차: Texture2D 로드 후 Sprite.Create
            var tex = Resources.Load<Texture2D>("Transitions/elf_beach");
            if (tex != null)
            {
                _splashSprite = Sprite.Create(
                    tex, new Rect(0, 0, tex.width, tex.height),
                    new Vector2(0.5f, 0.5f), 100f);
                Debug.Log($"[LobbyTransitionOverlay] Texture2D → Sprite 생성 성공: {tex.width}x{tex.height}");
                return;
            }

            // ★ 3차: Resources 루트 직접 검색 (서브폴더 인식 실패 케이스 폴백)
            var fallbackTex = Resources.Load<Texture2D>("elf_beach");
            if (fallbackTex != null)
            {
                _splashSprite = Sprite.Create(
                    fallbackTex, new Rect(0, 0, fallbackTex.width, fallbackTex.height),
                    new Vector2(0.5f, 0.5f), 100f);
                Debug.Log("[LobbyTransitionOverlay] 루트 폴백 로드 성공: elf_beach");
                return;
            }

            Debug.LogError("[LobbyTransitionOverlay] 엘프공주 이미지 로드 실패 — Resources/Transitions/elf_beach 및 Resources/elf_beach 모두 없음. .meta가 정상 import 되었는지 확인하세요.");
        }

        /// <summary>
        /// 단순 페이드 전환. onFadedIn은 페이드 인 완료 직후 호출 (작업 가리기 용도).
        /// </summary>
        public void PlayTransition(System.Action onFadedIn)
        {
            if (isRunning)
            {
                Debug.LogWarning("[LobbyTransitionOverlay] 이미 전환 중 — 중복 호출 무시");
                // ★ 이미 중복 호출이라도 작업은 누락되면 안 됨 → 즉시 실행 (안전망)
                try { onFadedIn?.Invoke(); } catch (System.Exception e) { Debug.LogError(e); }
                return;
            }
            // ★ StartCoroutine 호출 전 GameObject 활성화 — 비활성 상태에서 코루틴 시작 시 에러 발생
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            StartCoroutine(TransitionCoroutine(onFadedIn, showImage: false, hold: SimpleHoldSeconds));
        }

        /// <summary>
        /// 이미지 표시 전환 — Resources/Transitions/elf_beach를 2초간 띄우고 작업 실행 후 페이드 아웃.
        /// 인게임 ↔ 로비 화면 전환 시 사용 (사용자 요청 사양).
        /// </summary>
        public void PlayTransitionWithImage(System.Action onFadedIn)
        {
            if (isRunning)
            {
                Debug.LogWarning("[LobbyTransitionOverlay] 이미 전환 중 — 작업만 즉시 실행 (안전망)");
                try { onFadedIn?.Invoke(); } catch (System.Exception e) { Debug.LogError(e); }
                return;
            }
            // ★ StartCoroutine 호출 전 GameObject 활성화 — 비활성 상태에서 코루틴 시작 시 에러 발생
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            StartCoroutine(TransitionCoroutine(onFadedIn, showImage: true, hold: ImageHoldSeconds));
        }

        private IEnumerator TransitionCoroutine(System.Action onFadedIn, bool showImage, float hold)
        {
            isRunning = true;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();  // 매 호출 시 최상위 보장

            // ★ 이미지 모드면 sprite를 재시도 로드 (Initialize 시점에 Resources가 준비 안 된 경우 대비)
            if (showImage && _splashSprite == null)
            {
                EnsureSplashSpriteLoaded();
                if (splashImage != null && _splashSprite != null)
                    splashImage.sprite = _splashSprite;
            }

            // 이미지 활성/비활성
            if (splashImage != null)
            {
                bool canShow = showImage && _splashSprite != null;
                splashImage.enabled = canShow;
                if (canShow)
                {
                    var ic = splashImage.color; ic.a = 0f; splashImage.color = ic;
                    Debug.Log($"[LobbyTransitionOverlay] 엘프공주 이미지 표시 — sprite={_splashSprite.name}, size={_splashSprite.rect.size}");
                }
                else if (showImage)
                {
                    Debug.LogWarning("[LobbyTransitionOverlay] 이미지 모드 호출이지만 sprite 누락 — 검정 페이드만 진행");
                }
            }

            // 1. 페이드 인 (배경 + 이미지 동시에 0 → 1)
            yield return FadeAlpha(0f, 1f, FadeInDuration, showImage);

            // 2. 콜백 실행 (로비 reset + ShowLobby 또는 StartGame 등)
            try { onFadedIn?.Invoke(); }
            catch (System.Exception e) { Debug.LogError("[LobbyTransitionOverlay] 콜백 예외: " + e); }

            transform.SetAsLastSibling();  // 콜백이 UI를 재구성해도 최상위 유지

            // 3. 유지 (이미지 모드 2초, 단순 모드 0.3초)
            float t = 0f;
            while (t < hold) { t += Time.unscaledDeltaTime; yield return null; }

            // 4. 페이드 아웃 (1 → 0)
            yield return FadeAlpha(1f, 0f, FadeOutDuration, showImage);

            // 5. 정리
            if (splashImage != null) splashImage.enabled = false;
            gameObject.SetActive(false);
            isRunning = false;
        }

        private IEnumerator FadeAlpha(float fromA, float toA, float duration, bool fadeImage)
        {
            if (backdrop == null) yield break;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / duration);
                float a = Mathf.Lerp(fromA, toA, k);

                var bc = backdrop.color; bc.a = a; backdrop.color = bc;
                if (fadeImage && splashImage != null && splashImage.enabled)
                {
                    var ic = splashImage.color; ic.a = a; splashImage.color = ic;
                }
                yield return null;
            }
            var fc = backdrop.color; fc.a = toA; backdrop.color = fc;
            if (fadeImage && splashImage != null && splashImage.enabled)
            {
                var ic = splashImage.color; ic.a = toA; splashImage.color = ic;
            }
        }
    }
}
