using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using JewelsHexaPuzzle.Data;

namespace JewelsHexaPuzzle.Managers
{
    /// <summary>
    /// MP(마나포인트) 시스템 관리
    /// - 기본 MP 100, 게임 시작/나갈 때 초기화
    /// - 특수 블록 클릭 발동 시 MP 소모
    /// - 아이템 사용 시 MP 소모
    /// - MP 부족 시 사용 불가
    /// </summary>
    public class MPManager : MonoBehaviour
    {
        public static MPManager Instance { get; private set; }

        // ============================================================
        // MP 데이터
        // ============================================================
        private int currentMP = 100;
        private int maxMP = 100;

        /// <summary>현재 MP</summary>
        public int CurrentMP => currentMP;

        /// <summary>최대 MP</summary>
        public int MaxMP => maxMP;

        /// <summary>현재 채움 비율 (0~1)</summary>
        public float FillRatio => maxMP > 0 ? (float)currentMP / maxMP : 0f;

        // ============================================================
        // 특수 블록 MP 비용
        // ============================================================
        private static readonly Dictionary<SpecialBlockType, int> specialBlockCosts = new Dictionary<SpecialBlockType, int>
        {
            { SpecialBlockType.Drill, 5 },
            { SpecialBlockType.Bomb, 6 },
            { SpecialBlockType.Drone, 7 },
            { SpecialBlockType.XBlock, 10 }
        };

        // ============================================================
        // 아이템 MP 비용
        // ============================================================
        private static readonly Dictionary<ItemType, int> itemCosts = new Dictionary<ItemType, int>
        {
            { ItemType.Hammer, 8 },            // 망치
            { ItemType.Bomb, 9 },              // 스왑 (ItemType.Bomb = 스왑 아이템)
            { ItemType.SSD, 11 },              // 라인 (ItemType.SSD = 라인 아이템)
            { ItemType.ReverseRotation, 12 }   // 역회전
        };

        // ============================================================
        // 이벤트
        // ============================================================

        /// <summary>MP 변경 시 발생 (currentMP, maxMP)</summary>
        public event System.Action<int, int> OnMPChanged;

        // ============================================================
        // 초기화
        // ============================================================

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }
        }

        // ============================================================
        // 공개 API
        // ============================================================

        /// <summary>
        /// MP 초기화 (게임 시작 또는 나갈 때 호출)
        /// </summary>
        public void ResetMP()
        {
            currentMP = maxMP;
            OnMPChanged?.Invoke(currentMP, maxMP);
            Debug.Log($"[MPManager] MP 초기화: {currentMP}/{maxMP}");
        }

        /// <summary>
        /// 이벤트 없이 MP 초기화 (게이지 UI가 비활성 상태일 때 사용)
        /// </summary>
        public void ResetMPSilent()
        {
            currentMP = maxMP;
            Debug.Log($"[MPManager] MP 조용히 초기화: {currentMP}/{maxMP}");
        }

        /// <summary>
        /// MP가 충분한지 확인
        /// </summary>
        public bool CanAfford(int cost)
        {
            return currentMP >= cost;
        }

        /// <summary>
        /// MP 소모 시도. 성공 시 true 반환, 부족 시 false.
        /// worldPos가 지정되면 해당 위치에 "-N" 파란 텍스트 팝업 표시.
        /// </summary>
        public bool TryConsumeMP(int cost, Vector3? worldPos = null)
        {
            if (currentMP < cost)
            {
                Debug.Log($"[MPManager] MP 부족: 현재 {currentMP}, 필요 {cost}");
                return false;
            }

            currentMP -= cost;
            OnMPChanged?.Invoke(currentMP, maxMP);
            Debug.Log($"[MPManager] MP 소모 {cost}: {currentMP + cost} → {currentMP}/{maxMP}");

            // ★ MP 소모 팝업: 사용 위치에 파란색 "-N" 텍스트 표시
            if (worldPos.HasValue)
                SpawnMPPopup(cost, worldPos.Value);
            else
                SpawnMPPopupAtGauge(cost);

            return true;
        }

        // ============================================================
        // MP 소모 팝업
        // ============================================================

        /// <summary>
        /// 월드 좌표에 "-N" 파란 팝업 생성 (위로 올라가며 페이드아웃)
        /// </summary>
        private void SpawnMPPopup(int cost, Vector3 worldPos)
        {
            Canvas canvas = FindCanvas();
            if (canvas == null) return;

            GameObject popup = CreateMPPopupObject(cost, canvas);
            RectTransform rt = popup.GetComponent<RectTransform>();

            // 월드→캔버스 좌표 변환
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(Camera.main, worldPos);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.transform as RectTransform, screenPoint, canvas.worldCamera, out Vector2 localPoint);
            rt.anchoredPosition = localPoint;

            StartCoroutine(AnimateMPPopup(rt));
        }

        /// <summary>
        /// MP 게이지 위치에 "-N" 파란 팝업 생성 (좌표 없을 때 폴백)
        /// </summary>
        private void SpawnMPPopupAtGauge(int cost)
        {
            Canvas canvas = FindCanvas();
            if (canvas == null) return;

            GameObject popup = CreateMPPopupObject(cost, canvas);
            RectTransform rt = popup.GetComponent<RectTransform>();

            // 게이지 위치 (화면 좌측 상단 부근)
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(60, -80);

            StartCoroutine(AnimateMPPopup(rt));
        }

        private GameObject CreateMPPopupObject(int cost, Canvas canvas)
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            GameObject popup = new GameObject("MPPopup");
            popup.transform.SetParent(canvas.transform, false);
            RectTransform rt = popup.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(120, 50);

            Text text = popup.AddComponent<Text>();
            text.font = font;
            text.fontSize = 36;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(0.2f, 0.5f, 1f, 1f); // 파란색
            text.raycastTarget = false;
            text.text = $"-{cost}";

            // 검은 아웃라인
            Outline outline = popup.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0.2f, 0.8f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            return popup;
        }

        /// <summary>
        /// 팝업 애니메이션: 위로 60px 올라가면서 페이드아웃 (0.8초)
        /// </summary>
        private IEnumerator AnimateMPPopup(RectTransform rt)
        {
            if (rt == null) yield break;

            Vector2 startPos = rt.anchoredPosition;
            Text text = rt.GetComponent<Text>();
            Color startColor = text != null ? text.color : Color.blue;
            Outline outline = rt.GetComponent<Outline>();
            Color outlineStartColor = outline != null ? outline.effectColor : Color.black;

            float duration = 0.8f;
            float riseDistance = 60f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                if (rt == null) yield break;
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // 위로 이동 (EaseOutQuad)
                float eased = 1f - (1f - t) * (1f - t);
                rt.anchoredPosition = startPos + new Vector2(0, riseDistance * eased);

                // 페이드아웃 (후반 50%에서 시작)
                float alpha = t < 0.5f ? 1f : 1f - (t - 0.5f) * 2f;
                if (text != null)
                    text.color = new Color(startColor.r, startColor.g, startColor.b, alpha);
                if (outline != null)
                    outline.effectColor = new Color(outlineStartColor.r, outlineStartColor.g, outlineStartColor.b, alpha);

                yield return null;
            }

            if (rt != null)
                Destroy(rt.gameObject);
        }

        private Canvas FindCanvas()
        {
            // GameManager의 Canvas 사용
            if (GameManager.Instance != null)
            {
                Canvas c = GameManager.Instance.GetComponentInChildren<Canvas>();
                if (c != null) return c;
            }
            return Object.FindObjectOfType<Canvas>();
        }

        /// <summary>
        /// MP 회복 (미래 확장용)
        /// </summary>
        public void AddMP(int amount)
        {
            if (amount <= 0) return;
            currentMP = Mathf.Min(currentMP + amount, maxMP);
            OnMPChanged?.Invoke(currentMP, maxMP);
            Debug.Log($"[MPManager] MP 회복 +{amount}: {currentMP}/{maxMP}");
        }

        /// <summary>에디터용: MP를 특정 값으로 직접 설정</summary>
        public void SetMP(int value)
        {
            currentMP = Mathf.Clamp(value, 0, maxMP);
            OnMPChanged?.Invoke(currentMP, maxMP);
        }

        // ============================================================
        // 특수 블록 비용 조회
        // ============================================================

        /// <summary>특수 블록의 MP 소모량 반환 (미등록 시 0)</summary>
        public int GetSpecialBlockCost(SpecialBlockType type)
        {
            return specialBlockCosts.ContainsKey(type) ? specialBlockCosts[type] : 0;
        }

        /// <summary>특수 블록 발동 가능 여부 (MP 충분 여부)</summary>
        public bool CanActivateSpecialBlock(SpecialBlockType type)
        {
            int cost = GetSpecialBlockCost(type);
            if (cost <= 0) return true; // 비용 없는 특수 블록은 항상 가능
            return currentMP >= cost;
        }

        // ============================================================
        // 아이템 비용 조회
        // ============================================================

        /// <summary>아이템의 MP 소모량 반환 (미등록 시 0)</summary>
        public int GetItemCost(ItemType type)
        {
            return itemCosts.ContainsKey(type) ? itemCosts[type] : 0;
        }

        /// <summary>아이템 사용 가능 여부 (MP 충분 여부)</summary>
        public bool CanUseItem(ItemType type)
        {
            int cost = GetItemCost(type);
            if (cost <= 0) return true; // 비용 없는 아이템은 항상 가능
            return currentMP >= cost;
        }
    }
}
