using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using JewelsHexaPuzzle.Data;
using JewelsHexaPuzzle.Managers;

namespace JewelsHexaPuzzle.UI
{
    /// <summary>
    /// 아이템 게이지 컨트롤러 — 블록 제거 시 카운트 충전, 10개 도달 시 버튼 활성화.
    /// UIManager의 itemButtons 배열에서 자동으로 버튼을 찾음 (Inspector 연결 불필요).
    /// </summary>
    public class ItemGaugeController : MonoBehaviour
    {
        public static ItemGaugeController Instance { get; private set; }

        // 카운트 (0~MAX_COUNT)
        private int hammerCount = 0;
        private int swapCount = 0;
        private int lineCount = 0;
        private const int MAX_COUNT = 10;

        // UIManager에서 자동 탐색된 버튼 참조
        private Button hammerButton;
        private Button swapButton;
        private Button lineButton;
        private bool buttonsFound = false;

        private void Awake()
        {
            if (Instance == null)
                Instance = this;
            else if (Instance != this)
                Destroy(gameObject);
        }

        private void Start()
        {
            hammerCount = 0;
            swapCount = 0;
            lineCount = 0;

            // 1프레임 뒤 버튼 탐색 (UIManager가 아이템 버튼을 생성한 후)
            StartCoroutine(FindButtonsDelayed());
        }

        /// <summary>
        /// UIManager.itemButtons에서 ItemType별 버튼을 자동 탐색.
        /// UIManager가 버튼을 생성하는 시점보다 늦게 실행되도록 지연.
        /// </summary>
        private IEnumerator FindButtonsDelayed()
        {
            // UIManager가 아이템 버튼을 생성할 시간을 줌
            yield return new WaitForSeconds(0.5f);

            FindButtons();

            // 찾지 못했으면 1초 후 재시도
            if (!buttonsFound)
            {
                yield return new WaitForSeconds(1f);
                FindButtons();
            }
        }

        private void FindButtons()
        {
            var uiMgr = Object.FindObjectOfType<UIManager>();
            if (uiMgr == null) return;
            var itemBtns = uiMgr.ItemButtons;
            if (itemBtns == null) return;

            foreach (var ib in itemBtns)
            {
                if (ib == null) continue;
                var btn = ib.ButtonComponent;
                if (btn == null) continue;

                switch (ib.CurrentItemType)
                {
                    case ItemType.Hammer:
                        hammerButton = btn;
                        break;
                    case ItemType.Bomb: // Bomb = 스왑 아이템
                        swapButton = btn;
                        break;
                    case ItemType.SixWayLaser:
                        lineButton = btn;
                        break;
                }
            }

            buttonsFound = (hammerButton != null || swapButton != null || lineButton != null);

            // 초기 비활성화
            if (hammerButton != null) hammerButton.interactable = false;
            if (swapButton != null) swapButton.interactable = false;
            if (lineButton != null) lineButton.interactable = false;

            // 버튼 클릭 시 카운트 초기화 연결
            if (hammerButton != null)
                hammerButton.onClick.AddListener(() => OnItemUsed("hammer"));
            if (swapButton != null)
                swapButton.onClick.AddListener(() => OnItemUsed("swap"));
            if (lineButton != null)
                lineButton.onClick.AddListener(() => OnItemUsed("line"));

            Debug.Log($"[ItemGaugeController] 버튼 탐색 완료: hammer={hammerButton != null}, swap={swapButton != null}, line={lineButton != null}");

            RefreshUI();
        }

        // ============================================================
        // 블록 제거 연동 — BlockRemovalSystem에서 호출
        // ============================================================

        public void OnBlockRemoved(GemType gemType)
        {
            bool changed = false;

            if (gemType == GemType.Green && hammerCount < MAX_COUNT)
            {
                hammerCount++;
                changed = true;
            }
            else if (gemType == GemType.Red && swapCount < MAX_COUNT)
            {
                swapCount++;
                changed = true;
            }
            else if (gemType == GemType.Purple && lineCount < MAX_COUNT)
            {
                lineCount++;
                changed = true;
            }

            if (changed)
                RefreshUI();
        }

        // ============================================================
        // UI 갱신
        // ============================================================

        private void RefreshUI()
        {
            if (hammerButton != null)
                hammerButton.interactable = (hammerCount >= MAX_COUNT);
            if (swapButton != null)
                swapButton.interactable = (swapCount >= MAX_COUNT);
            if (lineButton != null)
                lineButton.interactable = (lineCount >= MAX_COUNT);
        }

        // ============================================================
        // 아이템 사용 후 초기화
        // ============================================================

        public void OnItemUsed(string itemName)
        {
            switch (itemName.ToLower())
            {
                case "hammer":
                    hammerCount = 0;
                    break;
                case "swap":
                    swapCount = 0;
                    break;
                case "line":
                    lineCount = 0;
                    break;
            }
            RefreshUI();
            Debug.Log($"[ItemGaugeController] 아이템 사용: {itemName} → 카운트 초기화");
        }

        public void OnItemUsed(ItemType type)
        {
            switch (type)
            {
                case ItemType.Hammer: OnItemUsed("hammer"); break;
                case ItemType.Bomb: OnItemUsed("swap"); break;
                case ItemType.SixWayLaser: OnItemUsed("line"); break;
            }
        }

        // ============================================================
        // 외부 조회
        // ============================================================

        public int GetCount(string itemName)
        {
            switch (itemName.ToLower())
            {
                case "hammer": return hammerCount;
                case "swap": return swapCount;
                case "line": return lineCount;
                default: return 0;
            }
        }

        public bool IsReady(string itemName)
        {
            return GetCount(itemName) >= MAX_COUNT;
        }
    }
}
