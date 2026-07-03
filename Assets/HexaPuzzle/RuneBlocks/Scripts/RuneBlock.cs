using UnityEngine;

namespace HexaPuzzle.RuneBlocks
{
    /// <summary>
    /// 하나의 룬 스톤 블록. 그리드에서 매칭 단위로 동작합니다.
    /// 스프라이트와 룬 문양 정보는 <see cref="RuneColor"/> 와 1:1 매칭되며
    /// <see cref="SetColor"/> 호출 시 자동으로 적절한 스프라이트가 적용됩니다.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class RuneBlock : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField] private RuneColor color = RuneColor.Ruby;

        [Header("Sprites (6개) — Editor 메뉴가 자동 채움")]
        [SerializeField] private Sprite[] sprites = new Sprite[6];

        [Header("Pop-in animation")]
        [Tooltip("Spawn 시 살짝 튕기듯 등장하는 효과 길이.")]
        [SerializeField] private float popInDuration = 0.22f;

        [Header("Destroy animation")]
        [Tooltip("매칭으로 사라질 때의 페이드/스케일 다운 길이.")]
        [SerializeField] private float destroyDuration = 0.26f;

        private SpriteRenderer _sr;
        public RuneColor Color => color;
        public SpriteRenderer Renderer => _sr ?? (_sr = GetComponent<SpriteRenderer>());

        private void Awake()
        {
            ApplySprite();
        }

        // ─── Public API ─────────────────────────────────────────────────
        /// <summary>이 블록의 색(=룬)을 변경합니다.</summary>
        public void SetColor(RuneColor c)
        {
            color = c;
            ApplySprite();
        }

        /// <summary>같은 색인지 비교 (매칭 로직용).</summary>
        public bool Matches(RuneBlock other) => other != null && other.color == color;

        /// <summary>스폰 애니메이션. Instantiate 직후 호출.</summary>
        public void PlayPopIn()
        {
            StopAllCoroutines();
            StartCoroutine(CoPopIn());
        }

        /// <summary>매칭/사용 시 사라짐 애니메이션 후 GameObject 파괴.</summary>
        public void PlayDestroyAndKill(System.Action onDone = null)
        {
            StopAllCoroutines();
            StartCoroutine(CoDestroy(onDone));
        }

        // ─── Internal ───────────────────────────────────────────────────
        private void ApplySprite()
        {
            if (sprites == null) return;
            int idx = (int)color;
            if (idx < 0 || idx >= sprites.Length) return;
            Renderer.sprite = sprites[idx];
        }

        private System.Collections.IEnumerator CoPopIn()
        {
            Vector3 baseScale = transform.localScale;
            transform.localScale = baseScale * 0.2f;
            float t = 0f;
            while (t < popInDuration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / popInDuration);
                // ease out back
                float s = 1.70158f;
                float x = k - 1f;
                float e = 1f + (s + 1f) * x * x * x + s * x * x;
                transform.localScale = Vector3.Lerp(baseScale * 0.2f, baseScale, e);
                yield return null;
            }
            transform.localScale = baseScale;
        }

        private System.Collections.IEnumerator CoDestroy(System.Action onDone)
        {
            Vector3 baseScale = transform.localScale;
            Color baseColor = Renderer.color;
            float t = 0f;
            while (t < destroyDuration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / destroyDuration);
                transform.localScale = Vector3.Lerp(baseScale * 1.18f, Vector3.zero, k);
                var c = baseColor;
                c.a = 1f - k;
                Renderer.color = c;
                yield return null;
            }
            onDone?.Invoke();
            Destroy(gameObject);
        }
    }
}
