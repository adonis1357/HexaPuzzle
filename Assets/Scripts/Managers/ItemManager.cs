using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using JewelsHexaPuzzle.Core;
using JewelsHexaPuzzle.Data;

namespace JewelsHexaPuzzle.Managers
{
    /// <summary>
    /// 아이템 시스템 관리
    /// 기획서 6. 아이템 구현
    /// </summary>
    public class ItemManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private HexGrid hexGrid;
        [SerializeField] private InputSystem inputSystem;
        [SerializeField] private UIManager uiManager;
        
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
        
        // 아이템 보유 수량 (PlayerPrefs 저장)
        private Dictionary<ItemType, int> itemCounts = new Dictionary<ItemType, int>();
        
        private void Start()
        {
            LoadItemCounts();
            InitializeItems();
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
        
        /// <summary>
        /// 아이템 사용 시작
        /// </summary>
        public void UseItem(ItemType type)
        {
            // 수량 확인
            if (!itemCounts.ContainsKey(type) || itemCounts[type] <= 0)
            {
                Debug.Log($"No {type} available");
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
            ExecuteItem(activeItem.Value, targetBlock);
        }
        
        /// <summary>
        /// 아이템 효과 실행
        /// </summary>
        private void ExecuteItem(ItemType type, HexBlock targetBlock)
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
                    StartCoroutine(ExecuteSSD(targetBlock));
                    break;
            }
            
            // 아이템 소모
            itemCounts[type]--;
            SaveItemCounts();
            UpdateItemUI();
            
            // 상태 초기화
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
            
            // 이펙트 재생
            // TODO: 해머 이펙트
            
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
            
            // 폭발 이펙트
            // TODO: 폭발 파티클
            
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
        private ItemData GetItemData(ItemType type)
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
        TurnPlus5 = 5  // 턴 +5 (게임오버 시)
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
