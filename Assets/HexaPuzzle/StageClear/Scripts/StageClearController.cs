using UnityEngine;

namespace HexaPuzzle.StageClear
{
    /// <summary>
    /// 어디서든 정적으로 팝업을 띄울 수 있게 해주는 라우터.
    /// 씬에 <see cref="StageClearPopup"/> 가 1개 존재하면 자동으로 연결합니다.
    /// 여러 개 있으면 Inspector 의 explicit <see cref="popup"/> 가 우선합니다.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class StageClearController : MonoBehaviour
    {
        [Tooltip("씬에 여러 팝업이 있다면 여기에 명시. 비워두면 자동 검색.")]
        public StageClearPopup popup;

        private static StageClearController _instance;

        private void Awake()
        {
            _instance = this;
            if (popup == null) popup = FindObjectOfType<StageClearPopup>(includeInactive: true);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        // ─── Static API ──────────────────────────────────────────────────────

        /// <summary>아무 곳에서나 호출: <c>StageClearController.Show(result)</c>.</summary>
        public static void Show(StageClearResult result)
        {
            var p = ResolvePopup();
            if (p == null)
            {
                Debug.LogWarning("[StageClear] StageClearPopup 을 찾을 수 없습니다. " +
                    "씬에 프리팹을 배치했는지 확인하세요.");
                return;
            }
            p.Show(result);
        }

        public static void Hide()
        {
            var p = ResolvePopup();
            if (p != null) p.Hide();
        }

        private static StageClearPopup ResolvePopup()
        {
            if (_instance != null && _instance.popup != null) return _instance.popup;
            // Fallback — 컨트롤러가 씬에 없어도 동작하도록 직접 검색
            return FindObjectOfType<StageClearPopup>(includeInactive: true);
        }
    }
}
