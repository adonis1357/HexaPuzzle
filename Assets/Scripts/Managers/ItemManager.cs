using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using JewelsHexaPuzzle.Core;
using JewelsHexaPuzzle.Data;

namespace JewelsHexaPuzzle.Managers
{
    /// <summary>
    /// 아이템 시스템 관리
    /// - 보유 수량 (PlayerPrefs 영구 저장)
    /// - 게임당 사용 제한 (기본 3회, 증가 가능)
    /// - 골드 구매
    /// - 수량 변경 이벤트
    /// </summary>
    public class ItemManager : MonoBehaviour
    {
        public static ItemManager Instance { get; private set; }

        [Header("References")]
        [SerializeField] private HexGrid hexGrid;
        [SerializeField] private UIManager uiManager;
        [SerializeField] private BlockRemovalSystem blockRemovalSystem;
        [SerializeField] private InputSystem inputSystem;


        [Header("Item Data")]
        [SerializeField] private ItemData[] items;

        [Header("Visual")]
        [SerializeField] private GameObject itemTargetIndicator;
        [SerializeField] private LineRenderer laserLineRenderer;

        // 현재 사용 중인 아이템
        private ItemType? activeItem = null;
        private bool isSelectingTarget = false;

        // 이벤트
        public event System.Action<ItemType> OnItemUsed;
        public event System.Action<ItemType> OnItemActivated;
        public event System.Action OnItemCancelled;
        /// <summary>아이템 수량 변경 시 발생 (타입, 보유수량)</summary>
        public event System.Action<ItemType, int> OnItemCountChanged;

        // 아이템 보유 수량 (PlayerPrefs 저장)
        private Dictionary<ItemType, int> itemCounts = new Dictionary<ItemType, int>();

        // ============================================================
        // 게이지 시스템: 블록 제거로 아이템 카운트 충전 (10개=100%)
        // 망치=Green, 스왑=Red, 라인드로우=Purple, 역회전=게이지 없음
        // ============================================================

        /// <summary>아이템별 충전 카운트 (0~10)</summary>
        private int hammerCount = 0;
        private int swapCount = 0;  // Bomb = 스왑
        private int lineCount = 0;  // SixWayLaser = 라인드로우

        private const int GAUGE_MAX = 10;

        /// <summary>게이지 변경 이벤트 (타입, 현재 게이지 0f~1f)</summary>
        public event System.Action<ItemType, float> OnGaugeChanged;

        /// <summary>아이템 ↔ GemType 매핑 (역회전 제외)</summary>
        private static readonly Dictionary<ItemType, GemType> itemToGem = new Dictionary<ItemType, GemType>
        {
            { ItemType.Hammer, GemType.Green },
            { ItemType.Bomb, GemType.Red },
            { ItemType.SixWayLaser, GemType.Purple }
        };

        /// <summary>블록 제거 시 호출 — 해당 색상의 아이템 카운트 증가</summary>
        public void OnBlockRemoved(GemType type)
        {
            Debug.Log($"[아이템진단2] ItemManager.OnBlockRemoved 호출됨 type={type} hammer={hammerCount} swap={swapCount} line={lineCount}");
            bool changed = false;
            if (type == GemType.Green)
            {
                if (hammerCount < GAUGE_MAX) { hammerCount++; changed = true; }
                if (hammerCount >= GAUGE_MAX) hammerCount = GAUGE_MAX;
                if (changed) OnGaugeChanged?.Invoke(ItemType.Hammer, GetGauge(ItemType.Hammer));
            }
            else if (type == GemType.Red)
            {
                if (swapCount < GAUGE_MAX) { swapCount++; changed = true; }
                if (swapCount >= GAUGE_MAX) swapCount = GAUGE_MAX;
                if (changed) OnGaugeChanged?.Invoke(ItemType.Bomb, GetGauge(ItemType.Bomb));
            }
            else if (type == GemType.Purple)
            {
                if (lineCount < GAUGE_MAX) { lineCount++; changed = true; }
                if (lineCount >= GAUGE_MAX) lineCount = GAUGE_MAX;
                if (changed) OnGaugeChanged?.Invoke(ItemType.SixWayLaser, GetGauge(ItemType.SixWayLaser));
            }
        }

        /// <summary>레거시 호환: AddGauge → OnBlockRemoved 위임</summary>
        public void AddGauge(GemType color, int count)
        {
            for (int i = 0; i < count; i++)
                OnBlockRemoved(color);
        }

        /// <summary>아이템 게이지 값 반환 (0f~1f)</summary>
        public float GetGauge(ItemType type)
        {
            if (type == ItemType.Hammer) return hammerCount / (float)GAUGE_MAX;
            if (type == ItemType.Bomb) return swapCount / (float)GAUGE_MAX;
            if (type == ItemType.SixWayLaser) return lineCount / (float)GAUGE_MAX;
            return 0f;
        }

        /// <summary>아이템 카운트 raw 반환 (0~10)</summary>
        public int GetGaugeCount(ItemType type)
        {
            if (type == ItemType.Hammer) return hammerCount;
            if (type == ItemType.Bomb) return swapCount;
            if (type == ItemType.SixWayLaser) return lineCount;
            return 0;
        }

        /// <summary>게이지가 100%인지 확인</summary>
        public bool IsGaugeFull(ItemType type)
        {
            return GetGaugeCount(type) >= GAUGE_MAX;
        }

        /// <summary>게이지 초기화 (아이템 사용 후)</summary>
        public void ResetGauge(ItemType type)
        {
            if (type == ItemType.Hammer) hammerCount = 0;
            else if (type == ItemType.Bomb) swapCount = 0;
            else if (type == ItemType.SixWayLaser) lineCount = 0;
            OnGaugeChanged?.Invoke(type, 0f);
        }

        /// <summary>아이템에 연결된 GemType 반환</summary>
        public static GemType GetLinkedGemType(ItemType type)
        {
            return itemToGem.ContainsKey(type) ? itemToGem[type] : GemType.None;
        }

        // ============================================================
        // 게임당 사용 제한 시스템
        // ============================================================
        private Dictionary<ItemType, int> perGameUsageCount = new Dictionary<ItemType, int>();
        private int perGameUsageLimit = 3; // 기본 게임당 3회 제한

        /// <summary>현재 게임당 사용 제한 횟수</summary>
        public int PerGameUsageLimit => perGameUsageLimit;

        // ============================================================
        // 아이템 골드 가격 (정수)
        // ============================================================
        private static readonly Dictionary<ItemType, int> itemGoldPrices = new Dictionary<ItemType, int>
        {
            { ItemType.Hammer, 100 },
            { ItemType.Bomb, 200 },
            { ItemType.SixWayLaser, 300 },
            { ItemType.SSD, 150 },
            { ItemType.TurnPlus5, 500 },
            { ItemType.ReverseRotation, 80 }
        };

        /// <summary>아이템의 골드 가격 반환</summary>
        public static int GetItemGoldPrice(ItemType type)
        {
            return itemGoldPrices.ContainsKey(type) ? itemGoldPrices[type] : 100;
        }

        // 아이템 한글 이름 매핑
        private static readonly Dictionary<ItemType, string> itemDisplayNames = new Dictionary<ItemType, string>
        {
            { ItemType.Hammer, "망치" },
            { ItemType.Bomb, "스왑" },
            { ItemType.SixWayLaser, "레이저" },
            { ItemType.SSD, "라인" },
            { ItemType.TurnPlus5, "턴+5" },
            { ItemType.ReverseRotation, "역회전" }
        };

        /// <summary>아이템의 표시 이름 반환</summary>
        public static string GetItemDisplayName(ItemType type)
        {
            return itemDisplayNames.ContainsKey(type) ? itemDisplayNames[type] : type.ToString();
        }

        private void Awake()
        {
            if (Instance == null)
                Instance = this;

            // Awake에서 먼저 로드 (GameManager.Start보다 앞서 데이터 준비)
            LoadItemCounts();

            // 게이지 카운트 초기화
            hammerCount = 0;
            swapCount = 0;
            lineCount = 0;

            Debug.Log($"[ItemManager] Awake: 아이템 수량 로드 + 게이지 초기화 완료");
        }

        private void Start()
        {
            InitializeItems();

            // ★ 진단용: 3초 후 망치 게이지 강제 100%
            Invoke("ForceActivateTest", 3f);

            if (blockRemovalSystem == null)
                blockRemovalSystem = FindObjectOfType<BlockRemovalSystem>();
        }

        /// <summary>진단용: 망치 게이지 강제 충전 후 UI 갱신 시도</summary>
        private void ForceActivateTest()
        {
            hammerCount = GAUGE_MAX;
            Debug.Log($"[아이템진단5] ForceActivateTest 실행: hammerCount={hammerCount}, IsGaugeFull={IsGaugeFull(ItemType.Hammer)}");
            OnGaugeChanged?.Invoke(ItemType.Hammer, 1f);

            // UIManager 아이템 버튼 강제 갱신
            if (uiManager != null && items != null)
            {
                Debug.Log($"[아이템진단5] UIManager.UpdateItemButtons 강제 호출");
                uiManager.UpdateItemButtons(items);
            }
            else
            {
                Debug.LogWarning($"[아이템진단5] uiManager={uiManager} items={items}");
            }
        }

        /// <summary>
        /// 아이템 초기화
        /// </summary>
        private void InitializeItems()
        {
            if (items == null || items.Length == 0)
            {
                // 기본 아이템 데이터 생성
                items = new ItemData[]
                {
                    new ItemData
                    {
                        type = ItemType.Hammer,
                        name = "HAMMER",
                        description = "터치한 곳의 블록을 하나 제거",
                        price = 0.99f,
                        unlockStage = 10
                    },
                    new ItemData
                    {
                        type = ItemType.Bomb,
                        name = "BOMB",
                        description = "터치한 곳과 주변 블록을 제거",
                        price = 1.29f,
                        unlockStage = 30
                    },
                    new ItemData
                    {
                        type = ItemType.SixWayLaser,
                        name = "6-WAY LASER",
                        description = "터치한 곳을 중심으로 6방향 모든 블록 제거",
                        price = 1.69f,
                        unlockStage = 50
                    },
                    new ItemData
                    {
                        type = ItemType.SSD,
                        name = "SSD",
                        description = "같은 종류 원석을 한붓그리기로 연결하여 제거",
                        price = 1.99f,
                        unlockStage = 100
                    }
                };
            }

            // UI 업데이트
            UpdateItemUI();
        }

        /// <summary>
        /// 아이템 수량 로드
        /// </summary>
        private void LoadItemCounts()
        {
            foreach (ItemType type in System.Enum.GetValues(typeof(ItemType)))
            {
                int count = PlayerPrefs.GetInt($"Item_{type}", 0);
                itemCounts[type] = count;
            }
        }

        /// <summary>
        /// 아이템 수량 저장
        /// </summary>
        private void SaveItemCounts()
        {
            foreach (var kvp in itemCounts)
            {
                PlayerPrefs.SetInt($"Item_{kvp.Key}", kvp.Value);
            }
            PlayerPrefs.Save();
        }

        // ============================================================
        // 게임당 사용 제한
        // ============================================================

        /// <summary>
        /// 새 게임 시작 시 게임당 사용 횟수 리셋 (GameManager에서 호출)
        /// </summary>
        public void ResetPerGameUsage()
        {
            perGameUsageCount.Clear();
            Debug.Log("[ItemManager] 게임당 사용 횟수 리셋");
        }

        /// <summary>
        /// 게임당 사용 제한 증가 (추후 버프/기능에서 사용)
        /// </summary>
        public void IncreasePerGameLimit(int amount)
        {
            perGameUsageLimit += amount;
            Debug.Log($"[ItemManager] 게임당 사용 제한 증가: {perGameUsageLimit}");
        }

        /// <summary>
        /// 특정 아이템의 이번 게임 사용 횟수
        /// </summary>
        public int GetPerGameUsageCount(ItemType type)
        {
            return perGameUsageCount.ContainsKey(type) ? perGameUsageCount[type] : 0;
        }

        /// <summary>
        /// 이번 게임에서 아이템 사용 가능 여부 (보유 수량 + 게임당 제한 모두 확인)
        /// </summary>
        public bool CanUseItem(ItemType type)
        {
            // 역회전: 항상 사용 가능 (게이지 없음)
            if (type == ItemType.ReverseRotation)
                return true;

            // 게이지 기반: 100% 충전 시에만 사용 가능
            if (itemToGem.ContainsKey(type))
                return IsGaugeFull(type);

            // 게이지 미적용 아이템(TurnPlus5 등): 기존 수량 기반
            int count = GetItemCount(type);
            if (count <= 0) return false;

            int used = GetPerGameUsageCount(type);
            if (used >= perGameUsageLimit) return false;

            return true;
        }

        /// <summary>
        /// 이번 게임에서 남은 사용 가능 횟수
        /// </summary>
        public int GetRemainingUsesThisGame(ItemType type)
        {
            int used = GetPerGameUsageCount(type);
            int remaining = perGameUsageLimit - used;
            int owned = GetItemCount(type);
            return Mathf.Min(remaining, owned);
        }

        /// <summary>
        /// 아이템 1회 소모 (보유 수량 감소 + 게임당 사용 횟수 증가)
        /// </summary>
        public void ConsumeItem(ItemType type)
        {
            // 역회전: 소모 없음 (게이지/수량 모두 불필요)
            if (type == ItemType.ReverseRotation)
            {
                Debug.Log($"[ItemManager] {type} 사용 (소모 없음)");
            }
            // 게이지 기반 아이템: 게이지 초기화
            else if (itemToGem.ContainsKey(type))
            {
                ResetGauge(type);
                Debug.Log($"[ItemManager] {type} 게이지 소모 → 0%");
            }
            else
            {
                // 게이지 미적용 아이템: 기존 수량 감소
                if (itemCounts.ContainsKey(type) && itemCounts[type] > 0)
                {
                    itemCounts[type]--;
                    SaveItemCounts();
                }
            }

            // 게임당 사용 횟수 증가
            if (!perGameUsageCount.ContainsKey(type))
                perGameUsageCount[type] = 0;
            perGameUsageCount[type]++;

            // 이벤트 발생
            OnItemCountChanged?.Invoke(type, GetItemCount(type));
            UpdateItemUI();

            Debug.Log($"[ItemManager] {type} 소모됨. 게이지: {GetGauge(type):P0}, 이번게임 사용: {perGameUsageCount[type]}/{perGameUsageLimit}");
        }

        // ============================================================
        // 구매 시스템
        // ============================================================

        /// <summary>
        /// 골드로 아이템 구매
        /// </summary>
        /// <returns>구매 성공 여부</returns>
        public bool PurchaseItem(ItemType type, int quantity = 1)
        {
            int price = GetItemGoldPrice(type) * quantity;

            if (GameManager.Instance == null)
            {
                Debug.LogWarning("[ItemManager] GameManager 없음, 구매 불가");
                return false;
            }

            if (GameManager.Instance.CurrentGold < price)
            {
                Debug.Log($"[ItemManager] 골드 부족: 보유 {GameManager.Instance.CurrentGold}, 필요 {price}");
                return false;
            }

            // 골드 차감
            GameManager.Instance.SpendGold(price);

            // 아이템 추가
            AddItem(type, quantity);

            Debug.Log($"[ItemManager] {type} x{quantity} 구매 완료 (골드 -{price})");
            return true;
        }

        /// <summary>
        /// 아이템 사용 시작
        /// </summary>
        public void UseItem(ItemType type)
        {
            // 게이지/수량 확인
            if (!CanUseItem(type))
            {
                Debug.Log($"[ItemManager] {type} 사용 불가 (게이지 미충전 또는 제한 초과)");
                return;
            }

            // 해금 확인
            ItemData itemData = GetItemData(type);
            if (itemData != null && GameManager.Instance.CurrentStage < itemData.unlockStage)
            {
                Debug.Log($"{type} is locked until stage {itemData.unlockStage}");
                return;
            }

            activeItem = type;
            isSelectingTarget = true;

            // 입력 모드 변경
            if (inputSystem != null)
            {
                inputSystem.SetEnabled(false);
            }

            // 타겟 선택 UI 표시
            if (itemTargetIndicator != null)
            {
                itemTargetIndicator.SetActive(true);
            }

            OnItemActivated?.Invoke(type);
            Debug.Log($"Item {type} activated. Select target.");
        }

        /// <summary>
        /// 아이템 사용 취소
        /// </summary>
        public void CancelItem()
        {
            activeItem = null;
            isSelectingTarget = false;

            if (inputSystem != null)
            {
                inputSystem.SetEnabled(true);
            }

            if (itemTargetIndicator != null)
            {
                itemTargetIndicator.SetActive(false);
            }

            OnItemCancelled?.Invoke();
        }

        /// <summary>
        /// 타겟 선택 (화면 터치 시 호출)
        /// </summary>
        public void SelectTarget(Vector2 screenPosition)
        {
            if (!isSelectingTarget || !activeItem.HasValue) return;

            // 화면 좌표를 그리드 좌표로 변환
            Vector2 worldPos = Camera.main.ScreenToWorldPoint(screenPosition);
            HexCoord coord = HexCoord.FromWorldPosition(worldPos, hexGrid.HexSize);

            HexBlock targetBlock = hexGrid.GetBlock(coord);
            if (targetBlock == null)
            {
                Debug.Log("Invalid target position");
                return;
            }

            // 아이템 효과 실행
            StartCoroutine(ExecuteItem(activeItem.Value, targetBlock));
        }

        /// <summary>
        /// 아이템 효과 실행
        /// </summary>
        private IEnumerator ExecuteItem(ItemType type, HexBlock targetBlock)
        {
            switch (type)
            {
                case ItemType.Hammer:
                    ExecuteHammer(targetBlock);
                    break;
                case ItemType.Bomb:
                    ExecuteBomb(targetBlock);
                    break;
                case ItemType.SixWayLaser:
                    ExecuteSixWayLaser(targetBlock);
                    break;
                case ItemType.SSD:
                    yield return StartCoroutine(ExecuteSSD(targetBlock));
                    break;
            }

            // 아이템 소모는 개별 아이템 스크립트의 ConsumeItem()에서 처리
            UpdateItemUI();

            // ★ 새 독립 게이지 컨트롤러에 사용 알림
            if (JewelsHexaPuzzle.UI.ItemGaugeController.Instance != null)
                JewelsHexaPuzzle.UI.ItemGaugeController.Instance.OnItemUsed(type);

            // 상태 초기화
            activeItem = null;
            isSelectingTarget = false;

            // 낙하 트리거 (빈 공간 채우기 + 연쇄 매칭)
            if (blockRemovalSystem != null)
            {
                blockRemovalSystem.TriggerFallOnly();
                while (blockRemovalSystem.IsProcessing)
                    yield return null;
            }

            if (inputSystem != null)
            {
                inputSystem.SetEnabled(true);
            }

            if (itemTargetIndicator != null)
            {
                itemTargetIndicator.SetActive(false);
            }

            OnItemUsed?.Invoke(type);
        }

        /// <summary>
        /// 해머 - 단일 블록 제거
        /// </summary>
        private void ExecuteHammer(HexBlock target)
        {
            if (target.Data == null) return;

            // 제거 가능한 블록인지 확인
            if (target.Data.specialType == SpecialBlockType.FixedBlock)
            {
                Debug.Log("Cannot remove fixed block with hammer");
                return;
            }

            // 비닐 제거
            if (target.Data.vinylLayer > 0)
            {
                target.RemoveVinyl();
                return;
            }

            // 체인 제거
            if (target.Data.hasChain)
            {
                target.RemoveChain();
                return;
            }

            // 블록 제거
            target.SetBlockData(new BlockData());

            Debug.Log($"Hammer used on {target.Coord}");
        }

        /// <summary>
        /// 폭탄 - 주변 블록 제거
        /// </summary>
        private void ExecuteBomb(HexBlock target)
        {
            List<HexBlock> blocksToRemove = new List<HexBlock> { target };
            blocksToRemove.AddRange(hexGrid.GetNeighbors(target.Coord));

            foreach (var block in blocksToRemove)
            {
                if (block.Data != null &&
                    block.Data.specialType != SpecialBlockType.FixedBlock)
                {
                    block.SetBlockData(new BlockData());
                }
            }

            Debug.Log($"Bomb used on {target.Coord}, affected {blocksToRemove.Count} blocks");
        }

        /// <summary>
        /// 6방향 레이저 - 6방향 모든 블록 제거
        /// </summary>
        private void ExecuteSixWayLaser(HexBlock target)
        {
            List<HexBlock> blocksToRemove = new List<HexBlock> { target };

            // 6방향으로 끝까지
            for (int dir = 0; dir < 6; dir++)
            {
                HexCoord current = target.Coord;

                while (true)
                {
                    current = current.GetNeighbor(dir);
                    HexBlock block = hexGrid.GetBlock(current);

                    if (block == null) break;

                    blocksToRemove.Add(block);
                }
            }

            // 레이저 비주얼
            if (laserLineRenderer != null)
            {
                StartCoroutine(ShowLaserEffect(target.transform.position));
            }

            foreach (var block in blocksToRemove)
            {
                if (block.Data != null &&
                    block.Data.specialType != SpecialBlockType.FixedBlock)
                {
                    block.SetBlockData(new BlockData());
                }
            }

            Debug.Log($"6-Way Laser used on {target.Coord}, affected {blocksToRemove.Count} blocks");
        }

        /// <summary>
        /// SSD - 한붓그리기 연결 제거
        /// </summary>
        private IEnumerator ExecuteSSD(HexBlock startBlock)
        {
            GemType targetType = startBlock.Data.gemType;
            List<HexBlock> connectedBlocks = new List<HexBlock>();
            HashSet<HexBlock> visited = new HashSet<HexBlock>();

            // BFS로 연결된 같은 타입 또는 보석 찾기
            Queue<HexBlock> queue = new Queue<HexBlock>();
            queue.Enqueue(startBlock);
            visited.Add(startBlock);

            while (queue.Count > 0)
            {
                HexBlock current = queue.Dequeue();
                connectedBlocks.Add(current);

                // 하이라이트 효과
                current.SetHighlighted(true);
                yield return new WaitForSeconds(0.05f);

                foreach (var neighbor in hexGrid.GetNeighbors(current.Coord))
                {
                    if (visited.Contains(neighbor)) continue;
                    if (neighbor.Data == null) continue;

                    // 같은 타입이거나 보석/특수보석
                    bool canConnect =
                        neighbor.Data.gemType == targetType ||
                        neighbor.Data.tier >= BlockTier.ProcessedGem;

                    if (canConnect)
                    {
                        queue.Enqueue(neighbor);
                        visited.Add(neighbor);
                    }
                }
            }

            yield return new WaitForSeconds(0.3f);

            // 모두 제거
            foreach (var block in connectedBlocks)
            {
                block.SetHighlighted(false);
                block.SetBlockData(new BlockData());
            }

            Debug.Log($"SSD used, connected {connectedBlocks.Count} blocks of type {targetType}");
        }

        /// <summary>
        /// 레이저 이펙트 표시
        /// </summary>
        private IEnumerator ShowLaserEffect(Vector3 center)
        {
            if (laserLineRenderer == null) yield break;

            laserLineRenderer.enabled = true;

            // 6방향 레이저 그리기
            float duration = 0.5f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float alpha = 1f - (elapsed / duration);

                // 레이저 색상 페이드
                Color color = laserLineRenderer.startColor;
                color.a = alpha;
                laserLineRenderer.startColor = color;
                laserLineRenderer.endColor = color;

                yield return null;
            }

            laserLineRenderer.enabled = false;
        }

        /// <summary>
        /// 아이템 UI 업데이트
        /// </summary>
        private void UpdateItemUI()
        {
            if (uiManager != null && items != null)
            {
                for (int i = 0; i < items.Length; i++)
                {
                    items[i].count = itemCounts.ContainsKey(items[i].type) ?
                        itemCounts[items[i].type] : 0;
                }
                uiManager.UpdateItemButtons(items);
            }
        }

        /// <summary>
        /// 아이템 데이터 가져오기
        /// </summary>
        public ItemData GetItemData(ItemType type)
        {
            if (items == null) return null;

            foreach (var item in items)
            {
                if (item.type == type) return item;
            }
            return null;
        }

        /// <summary>
        /// 아이템 추가 (구매, 보상 등)
        /// </summary>
        public void AddItem(ItemType type, int amount)
        {
            if (!itemCounts.ContainsKey(type))
                itemCounts[type] = 0;

            itemCounts[type] += amount;
            SaveItemCounts();
            UpdateItemUI();

            // 이벤트 발생
            OnItemCountChanged?.Invoke(type, itemCounts[type]);

            Debug.Log($"Added {amount} {type}. Total: {itemCounts[type]}");
        }

        /// <summary>
        /// 아이템 보유 수량 확인
        /// </summary>
        public int GetItemCount(ItemType type)
        {
            return itemCounts.ContainsKey(type) ? itemCounts[type] : 0;
        }
    }

    /// <summary>
    /// 아이템 타입
    /// </summary>
    public enum ItemType
    {
        Hammer = 1,
        Bomb = 2,
        SixWayLaser = 3,
        SSD = 4,
        TurnPlus5 = 5,          // 턴 +5 (게임오버 시)
        ReverseRotation = 6     // 역회전 (1회성 반시계 회전)
    }

    /// <summary>
    /// 아이템 데이터
    /// </summary>
    [System.Serializable]
    public class ItemData
    {
        public ItemType type;
        public string name;
        public string description;
        public Sprite icon;
        public float price;
        public int unlockStage;
        public int count;
    }
}
