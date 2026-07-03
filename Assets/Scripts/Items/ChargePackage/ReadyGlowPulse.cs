using UnityEngine;

namespace HexaPuzzle
{
    /// <summary>
    /// READY 상태 글로우의 펄스 애니메이션.
    /// 글로우 GameObject에 부착하면 활성화 동안 스케일/알파가 부드럽게 맥동합니다.
    ///
    /// 필수: 같은 오브젝트에 <see cref="CanvasGroup"/>이 있어야 알파 펄스가 동작합니다
    /// (없으면 스케일 펄스만 적용).
    /// </summary>
    public class ReadyGlowPulse : MonoBehaviour
    {
        [Tooltip("한 펄스 주기(초)")]
        [SerializeField] private float period = 1.6f;

        [Header("Scale")]
        [SerializeField] private float minScale = 0.95f;
        [SerializeField] private float maxScale = 1.10f;

        [Header("Alpha (CanvasGroup 필요)")]
        [SerializeField] private float minAlpha = 0.55f;
        [SerializeField] private float maxAlpha = 1.00f;

        private CanvasGroup _group;
        private RectTransform _rt;

        private void Awake()
        {
            _group = GetComponent<CanvasGroup>();
            _rt = transform as RectTransform;
        }

        private void OnEnable()
        {
            ApplyAt(0.5f);
        }

        private void Update()
        {
            // 0..1 sine wave
            float t = (Mathf.Sin(Time.time * Mathf.PI * 2f / period) + 1f) * 0.5f;
            ApplyAt(t);
        }

        private void ApplyAt(float t)
        {
            float s = Mathf.Lerp(minScale, maxScale, t);
            transform.localScale = new Vector3(s, s, 1f);
            if (_group != null) _group.alpha = Mathf.Lerp(minAlpha, maxAlpha, t);
        }
    }
}
