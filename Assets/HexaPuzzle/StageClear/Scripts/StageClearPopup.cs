using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace HexaPuzzle.StageClear
{
    /// <summary>
    /// 스테이지 클리어 팝업 컨트롤러.
    /// UI 계층(Canvas/Card/별/텍스트/버튼)에 대한 참조를 보관하고
    /// <see cref="Show"/> 호출 시 결과 데이터를 표시하면서 들어오는 애니메이션을 재생합니다.
    /// 일반적으로 에디터 메뉴 <c>HexaPuzzle → StageClear → ⚡ Do Everything</c>
    /// 가 자동으로 hierarchy 를 만들고 참조를 채워 줍니다.
    /// </summary>
    public class StageClearPopup : MonoBehaviour
    {
        [Header("Theme")]
        public StageClearTheme theme = StageClearTheme.Parchment;

        [Header("Root references")]
        public CanvasGroup canvasGroup;
        public RectTransform card;
        public GameObject backdrop;

        [Header("Stars (3개)")]
        public RectTransform[] stars = new RectTransform[3];
        public Image[] starImages   = new Image[3];

        [Header("Texts")]
        public Text titleText;
        public Text subtitleText;
        public Text scoreLabel;
        public Text scoreValue;
        public Text goldText;
        public Text stageBestLabel;
        public Text stageBestValue;
        public Text myBestLabel;
        public Text myBestValue;

        [Header("Badges & button")]
        public GameObject stageBestNewBadge;
        public GameObject myBestNewBadge;
        public Button confirmButton;
        public Text confirmText;

        [Header("Behavior")]
        [Tooltip("Show() 진입 애니메이션 길이 (초).")]
        public float showDuration = 0.35f;
        [Tooltip("Hide() 퇴장 애니메이션 길이 (초).")]
        public float hideDuration = 0.18f;
        [Tooltip("별이 하나씩 튀어 올라오는 간격 (초).")]
        public float starStagger  = 0.12f;

        [Header("Events")]
        public UnityEvent onConfirm;

        // ─── Public API ──────────────────────────────────────────────────────

        /// <summary>결과를 채워 넣고 팝업을 띄웁니다.</summary>
        public void Show(StageClearResult r)
        {
            ApplyResult(r);
            gameObject.SetActive(true);
            StopAllCoroutines();
            StartCoroutine(CoShow());
        }

        /// <summary>현재 화면에 떠 있는 팝업을 사라지게 합니다.</summary>
        public void Hide()
        {
            StopAllCoroutines();
            StartCoroutine(CoHide());
        }

        /// <summary>확인 버튼 클릭 핸들러 — Inspector 의 onConfirm 이벤트를 발사하고 사라집니다.</summary>
        public void HandleConfirm()
        {
            if (onConfirm != null) onConfirm.Invoke();
            Hide();
        }

        // ─── Data binding ────────────────────────────────────────────────────

        private void ApplyResult(StageClearResult r)
        {
            // Stars
            var pal = StageClearPalette.Resolve(theme);
            var litColor   = (r.starColor.a > 0.001f) ? r.starColor : pal.accent; // 난이도 색 오버라이드
            var unlitColor = theme == StageClearTheme.Mystic
                ? new Color(0.50f, 0.45f, 0.36f, 1f)
                : new Color(0.78f, 0.74f, 0.66f, 1f);
            for (int i = 0; i < starImages.Length; i++)
            {
                if (starImages[i] == null) continue;
                bool lit = i < r.stars;
                starImages[i].color = lit ? litColor : unlitColor;
                var cg = starImages[i].GetComponent<CanvasGroup>();
                if (cg != null) cg.alpha = lit ? 1f : 0.25f;
            }

            // Numbers
            if (scoreValue     != null) scoreValue.text     = r.score.ToString("N0");
            if (goldText       != null) goldText.text       = "+" + r.gold + " GOLD";
            if (stageBestValue != null) stageBestValue.text = r.stageBest.ToString("N0");
            if (myBestValue    != null) myBestValue.text    = r.myBest.ToString("N0");

            if (stageBestNewBadge != null) stageBestNewBadge.SetActive(r.isNewStageBest);
            if (myBestNewBadge    != null) myBestNewBadge.SetActive(r.isNewMyBest);
        }

        // ─── Animation ───────────────────────────────────────────────────────

        private IEnumerator CoShow()
        {
            if (canvasGroup == null || card == null) yield break;
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = true;
            card.localScale = Vector3.one * 0.82f;

            // Hide stars until bounce-in
            for (int i = 0; i < stars.Length; i++)
                if (stars[i] != null) stars[i].localScale = Vector3.zero;

            float t = 0f;
            while (t < showDuration)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / showDuration);
                canvasGroup.alpha = k;
                // Easing — overshoot pop
                float e = EaseOutBack(k);
                card.localScale = Vector3.Lerp(Vector3.one * 0.82f, Vector3.one, e);
                yield return null;
            }
            canvasGroup.alpha = 1f;
            card.localScale = Vector3.one;
            canvasGroup.interactable = true;

            // Stars bounce in sequentially
            for (int i = 0; i < stars.Length; i++)
            {
                if (stars[i] == null) continue;
                StartCoroutine(CoPopStar(stars[i]));
                yield return new WaitForSecondsRealtime(starStagger);
            }
        }

        private IEnumerator CoPopStar(RectTransform s)
        {
            float dur = 0.28f;
            float t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / dur);
                float e = EaseOutBack(k, 2.2f);
                s.localScale = Vector3.one * e;
                yield return null;
            }
            s.localScale = Vector3.one;
        }

        private IEnumerator CoHide()
        {
            if (canvasGroup == null || card == null) { gameObject.SetActive(false); yield break; }
            canvasGroup.interactable = false;
            float t = 0f;
            float startScale = card.localScale.x;
            while (t < hideDuration)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / hideDuration);
                canvasGroup.alpha = 1f - k;
                card.localScale = Vector3.one * Mathf.Lerp(startScale, 0.9f, k);
                yield return null;
            }
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            gameObject.SetActive(false);
        }

        private static float EaseOutBack(float k, float s = 1.70158f)
        {
            float c1 = s;
            float c3 = c1 + 1f;
            float x = k - 1f;
            return 1f + c3 * x * x * x + c1 * x * x;
        }

        // ─── Editor convenience ──────────────────────────────────────────────

        private void Reset()
        {
            // 컴포넌트가 처음 붙었을 때 onConfirm 이 null 인 상태로 InvalidOperationException 던지지 않게.
            if (onConfirm == null) onConfirm = new UnityEvent();
        }
    }
}
