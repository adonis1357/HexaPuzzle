using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

namespace HexaPuzzle
{
    /// <summary>
    /// 충전식 아이템 버튼.
    ///
    /// 동작:
    ///   - <see cref="fullChargeSeconds"/> 동안 천천히 게이지가 차오릅니다.
    ///   - 100%가 되면 글로우/펄스 효과가 표시되고 사용 가능 상태가 됩니다.
    ///   - 가득 찬 상태에서 탭 → <see cref="onUsed"/> 이벤트 발생 + 게이지 0으로 리셋.
    ///   - 충전 중 탭 → <see cref="onShake"/> 이벤트 + 흔들림 피드백.
    ///
    /// UI 구조 (Inspector에서 연결):
    ///   - colorFill   : Image (Type=Filled, Method=Vertical, Origin=Bottom) — 색이 차오르는 레이어
    ///   - glyphIcon   : Image — 아이템 글리프 (충전 중에는 반투명)
    ///   - readyGlow   : GameObject — 100% 충전 시 활성화되는 글로우 오브젝트
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class ChargeButton : MonoBehaviour
    {
        // ─── UI References ──────────────────────────────────────────────
        [Header("UI References")]
        [Tooltip("Image. Type=Filled, FillMethod=Vertical, FillOrigin=Bottom로 설정")]
        [SerializeField] private Image colorFill;
        [Tooltip("아이템 글리프 이미지. 충전 중에는 알파가 낮아집니다")]
        [SerializeField] private Image glyphIcon;
        [Tooltip("READY 상태일 때 활성화되는 글로우 오브젝트 (선택)")]
        [SerializeField] private GameObject readyGlow;
        [Tooltip("누적 충전(2번째 레이어 이상) 시 표시되는 옅은 색 바탕 — 그 위로 다음 레이어 fill이 대비되어 차오른다 (선택)")]
        [SerializeField] private Image baseFill;

        // ─── Settings ───────────────────────────────────────────────────
        [Header("Item")]
        [SerializeField] private ItemType itemType = ItemType.Hammer;

        [Header("Charge Settings")]
        [Tooltip("0%에서 100%까지 차오르는 데 걸리는 시간(초)")]
        [Range(1f, 60f)]
        [SerializeField] private float fullChargeSeconds = 10f;

        [Tooltip("시작 시 충전 비율 (0=빈 상태, 1=가득)")]
        [Range(0f, 1f)]
        [SerializeField] private float startCharge = 0f;

        [Tooltip("충전 중 글리프 최소 알파 (0=완전 투명, 1=완전 불투명)")]
        [Range(0f, 1f)]
        [SerializeField] private float dimGlyphAlpha = 0.5f;

        [Tooltip("자동 재충전 여부 — false면 SetCharge()/AddCharge()로만 충전")]
        [SerializeField] private bool autoRecharge = true;

        // ─── Events ─────────────────────────────────────────────────────
        [Header("Events")]
        [Tooltip("100% 충전 상태에서 탭하면 발생 — 실제 게임 효과를 여기에 연결")]
        public UnityEvent onUsed;
        [Tooltip("100%에 도달하는 순간 한 번 발생")]
        public UnityEvent onBecameReady;
        [Tooltip("충전 중 탭하면 발생 (사용 불가 피드백용)")]
        public UnityEvent onNotReady;
        [Tooltip("에디터 게이지 채우기 모드에서 탭하면 발생 — 아이템 발동 대신 게이지를 채운다(테스트용)")]
        public UnityEvent onEditorFill;

        // ─── State ──────────────────────────────────────────────────────
        private float _charge;
        private Button _button;
        private bool _wasReady;
        private Coroutine _flashRoutine;
        private Coroutine _shakeRoutine;
        private Vector3 _basePosition;
        private Vector3 _baseScale = Vector3.one; // ★ Awake 캡처 — 연타 시 FlashEffect 스케일 누적 방지 기준

        public float Charge => _charge;
        public bool IsReady => _charge >= 1f;
        public ItemType Type => itemType;

        // ─── Lifecycle ──────────────────────────────────────────────────
        private void Awake()
        {
            _button = GetComponent<Button>();
            _button.onClick.AddListener(OnTap);
            _basePosition = transform.localPosition;
            _baseScale = transform.localScale;

            // 아이템 색을 colorFill에 자동 적용
            if (colorFill != null)
            {
                var col = ItemColors.Get(itemType);
                colorFill.color = col;
            }

            _charge = Mathf.Clamp01(startCharge);
            _wasReady = _charge >= 1f;
            ApplyCharge();
        }

        private void Update()
        {
            if (!autoRecharge || _charge >= 1f) return;
            AddCharge(Time.deltaTime / fullChargeSeconds);
        }

        // ─── Public API ─────────────────────────────────────────────────
        /// <summary>충전 값을 직접 설정 (0~1)</summary>
        public void SetCharge(float value)
        {
            _charge = Mathf.Clamp01(value);
            ApplyCharge();
            CheckReadyTransition();
        }

        /// <summary>충전 값을 증가 (delta는 0~1 범위)</summary>
        public void AddCharge(float delta)
        {
            _charge = Mathf.Clamp01(_charge + delta);
            ApplyCharge();
            CheckReadyTransition();
        }

        /// <summary>사용한 것처럼 0%로 리셋 (이벤트는 발생하지 않음)</summary>
        public void Reset()
        {
            _charge = 0f;
            _wasReady = false;
            ApplyCharge();
        }

        // ─── Internal ───────────────────────────────────────────────────
        private void ApplyCharge()
        {
            if (colorFill != null)
                colorFill.fillAmount = _charge;

            if (glyphIcon != null)
            {
                var c = glyphIcon.color;
                c.a = IsReady ? 1f : Mathf.Lerp(dimGlyphAlpha, 0.95f, _charge);
                glyphIcon.color = c;
            }

            if (readyGlow != null && readyGlow.activeSelf != IsReady)
                readyGlow.SetActive(IsReady);

            // 단일 충전(SetCharge/AddCharge) 경로에서도 옅은 바탕은 IsReady에 동기화 (리셋 시 숨김 보장)
            if (baseFill != null && baseFill.gameObject.activeSelf != IsReady)
                baseFill.gameObject.SetActive(IsReady);
        }

        /// <summary>
        /// ★ 레이어형 충전 표시 — ChargeBar가 매 프레임 호출 (구버전 게이지 레이어 상태 반영).
        ///   charged=true (누적 충전 ≥1, 사용 가능):
        ///     · atMax=false (다음 단계로 더 차오를 수 있음): 옅은 색 바탕(baseFill)을 켜고 그 위로
        ///       "다음 레이어 진행"(layerProgress)을 선명한 fill로 채워 대비.
        ///     · atMax=true (최대 레이어 도달 → 다음 단계 진입 불가): 옅은 바탕 없이 "원래 색"을 가득 표시.
        ///   charged=false (첫 레이어 충전 중): 회색 바탕 그대로, 첫 충전 진행을 fill로 표시. 미사용 상태.
        ///   어느 경우든 charged면 _charge=1로 두어 사용 가능 상태(IsReady) 유지.
        /// </summary>
        public void SetLayeredCharge(bool charged, float layerProgress, bool atMax)
        {
            bool showPale = charged && !atMax;                    // 2단계 진입 가능할 때만 옅은 바탕
            float vis = atMax ? 1f : Mathf.Clamp01(layerProgress); // 최대치면 원래 색 가득
            _charge = charged ? 1f : vis;   // 사용 가능 여부 (charged면 IsReady) — fill 표시와 분리

            if (colorFill != null) colorFill.fillAmount = vis;
            if (baseFill != null && baseFill.gameObject.activeSelf != showPale)
                baseFill.gameObject.SetActive(showPale);

            if (glyphIcon != null)
            {
                var c = glyphIcon.color;
                c.a = charged ? 1f : Mathf.Lerp(dimGlyphAlpha, 0.95f, vis);
                glyphIcon.color = c;
            }
            if (readyGlow != null && readyGlow.activeSelf != charged)
                readyGlow.SetActive(charged);

            CheckReadyTransition();
        }

        private void CheckReadyTransition()
        {
            if (IsReady && !_wasReady)
            {
                _wasReady = true;
                onBecameReady?.Invoke();
            }
            else if (!IsReady)
            {
                _wasReady = false;
            }
        }

        private void OnTap()
        {
            // ★ 에디터 게이지 채우기 모드 — 사용/미사용 분기 전에 가로채 게이지만 채운다(구 게이지 버튼과 동일 동작).
            //   구 게이지 버튼은 숨겨져 직접 못 누르므로 새 ChargeButton이 그 역할을 대신한다.
            //   IsGaugeAddMode는 에디터 테스트 패널 토글 시에만 true → 빌드/실플레이엔 영향 없음.
            if (JewelsHexaPuzzle.Core.EditorTestSystem.IsGaugeAddMode())
            {
                onEditorFill?.Invoke();
                return;
            }

            if (IsReady)
            {
                if (_flashRoutine != null) StopCoroutine(_flashRoutine);
                _flashRoutine = StartCoroutine(FlashEffect());

                _charge = 0f;
                _wasReady = false;
                ApplyCharge();

                onUsed?.Invoke();
            }
            else
            {
                // ★ 위치 흔들림 제거 — 레이아웃 그룹 내 버튼이라 _basePosition(Awake 캡처)이 잘못돼
                //   클릭 시 엉뚱한 위치로 튕겨 고정되는 버그. 미충전 클릭은 위치 변화 없이 무시.
                onNotReady?.Invoke();
            }
        }

        // ─── Tween coroutines (no external dependency) ──────────────────
        private IEnumerator FlashEffect()
        {
            const float dur = 0.18f;
            float t = 0f;
            // ★ 연타 누적 버그 수정: live transform.localScale가 아니라 Awake에서 캡처한 고정 _baseScale 기준.
            //   이전엔 애니 도중 확대된 스케일을 base로 캡처해 연타 시 복리로 무한히 커졌다.
            transform.localScale = _baseScale; // 시작 시 기준으로 리셋 → 중첩 방지
            while (t < dur)
            {
                float p = t / dur;
                float s = 1f + 0.18f * (1f - p); // peak at start, decays
                transform.localScale = _baseScale * (1f + 0.1f * Mathf.Sin(p * Mathf.PI) + (s - 1f) * 0.4f);
                t += Time.deltaTime;
                yield return null;
            }
            transform.localScale = _baseScale;
        }

        private IEnumerator ShakeEffect()
        {
            const float dur = 0.32f;
            const float amplitude = 6f;
            float t = 0f;
            while (t < dur)
            {
                float decay = 1f - (t / dur);
                float x = Mathf.Sin(t * 60f) * amplitude * decay;
                transform.localPosition = _basePosition + new Vector3(x, 0f, 0f);
                t += Time.deltaTime;
                yield return null;
            }
            transform.localPosition = _basePosition;
        }
    }
}
