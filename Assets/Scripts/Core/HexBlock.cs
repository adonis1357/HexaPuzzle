using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using JewelsHexaPuzzle.Data;
using System;

namespace JewelsHexaPuzzle.Core
{
    [RequireComponent(typeof(Image))]
    public class HexBlock : MonoBehaviour, IPointerClickHandler, IPointerDownHandler, IPointerUpHandler
    {
        [Header("Visual Components")]
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image gemImage;
        [SerializeField] private Image borderImage;
        [SerializeField] private Image overlayImage;
        [SerializeField] private Image drillIndicator;
        [SerializeField] private Text timerText;

        private HexCoord coord;
        private HexGrid parentGrid;
        private BlockData blockData;

        private bool isSelected;
        private bool isHighlighted;
        private bool isMatched;

        // 스프라이트 캐시 (인스턴스별로 관리하지 않고 static으로 공유)
        private static Sprite hexFillSprite;
        private static Sprite hexBorderSprite;
        private static Sprite drillVerticalSprite;
        private static Sprite drillSlashSprite;
        private static Sprite drillBackSlashSprite;

        public event Action<HexBlock> OnBlockClicked;
        public event Action<HexBlock> OnBlockPressed;
        public event Action<HexBlock> OnBlockReleased;

        public HexCoord Coord => coord;
        public BlockData Data => blockData;
        public bool IsSelected => isSelected;
        public bool CanInteract => blockData != null && blockData.gemType != GemType.None && blockData.CanMove();

        private void Awake()
        {
            if (backgroundImage == null)
                backgroundImage = GetComponent<Image>();

            FindChildComponents();
            EnsureSpritesCreated();
            SetupBorder();
            SetupDrillIndicator();
        }

        private void FindChildComponents()
        {
            if (gemImage == null)
            {
                Transform t = transform.Find("GemImage");
                if (t != null) gemImage = t.GetComponent<Image>();
            }
            if (borderImage == null)
            {
                Transform t = transform.Find("BorderImage") ?? transform.Find("Border");
                if (t != null) borderImage = t.GetComponent<Image>();
            }
            if (overlayImage == null)
            {
                Transform t = transform.Find("OverlayImage");
                if (t != null) overlayImage = t.GetComponent<Image>();
            }
            if (drillIndicator == null)
            {
                Transform t = transform.Find("DrillIndicator");
                if (t != null) drillIndicator = t.GetComponent<Image>();
            }
            if (timerText == null)
            {
                Transform t = transform.Find("TimerText");
                if (t != null) timerText = t.GetComponent<Text>();
            }
        }

        /// <summary>
        /// 스프라이트가 없으면 생성 (Play 모드 재시작 시에도 동작)
        /// </summary>
        private void EnsureSpritesCreated()
        {
            // 스프라이트가 null이면 재생성 (Play 모드 재시작 대응)
            if (hexFillSprite == null)
            {
                hexFillSprite = CreateFlatTopHexSprite(128, 0, false);
            }
            if (hexBorderSprite == null)
            {
                hexBorderSprite = CreateFlatTopHexSprite(128, 10f, true);
            }
            if (drillVerticalSprite == null)
            {
                drillVerticalSprite = CreateArrowSprite(64, 0);
                drillSlashSprite = CreateArrowSprite(64, -45);
                drillBackSlashSprite = CreateArrowSprite(64, 45);
            }
        }

        private Sprite CreateArrowSprite(int size, float rotation)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[size * size];

            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = Color.clear;

            Vector2 center = new Vector2(size / 2f, size / 2f);
            float arrowLength = size * 0.4f;
            float arrowWidth = size * 0.15f;
            float rad = rotation * Mathf.Deg2Rad;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2(x - center.x, y - center.y);
                    float rx = p.x * Mathf.Cos(-rad) - p.y * Mathf.Sin(-rad);
                    float ry = p.x * Mathf.Sin(-rad) + p.y * Mathf.Cos(-rad);

                    bool inArrow = false;
                    if (Mathf.Abs(rx) < arrowWidth / 2f && Mathf.Abs(ry) < arrowLength)
                        inArrow = true;
                    if (ry > arrowLength * 0.5f && ry < arrowLength)
                    {
                        float arrowHead = (arrowLength - ry) / (arrowLength * 0.5f) * arrowWidth;
                        if (Mathf.Abs(rx) < arrowHead) inArrow = true;
                    }
                    if (ry < -arrowLength * 0.5f && ry > -arrowLength)
                    {
                        float arrowHead = (arrowLength + ry) / (arrowLength * 0.5f) * arrowWidth;
                        if (Mathf.Abs(rx) < arrowHead) inArrow = true;
                    }

                    if (inArrow) pixels[y * size + x] = Color.white;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private Sprite CreateFlatTopHexSprite(int size, float borderWidth, bool isBorderOnly)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[size * size];

            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = Color.clear;

            Vector2 center = new Vector2(size / 2f, size / 2f);
            float outerRadius = size / 2f - 1f;
            float innerRadius = outerRadius - borderWidth;

            Vector2[] outerVerts = new Vector2[6];
            Vector2[] innerVerts = new Vector2[6];

            for (int i = 0; i < 6; i++)
            {
                float angle = i * Mathf.PI / 3f;
                outerVerts[i] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * outerRadius;
                innerVerts[i] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * innerRadius;
            }

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 point = new Vector2(x, y);
                    bool inOuter = IsPointInHex(point, outerVerts);
                    bool inInner = IsPointInHex(point, innerVerts);

                    if (isBorderOnly)
                    {
                        if (inOuter && !inInner) pixels[y * size + x] = Color.white;
                    }
                    else
                    {
                        if (inOuter) pixels[y * size + x] = Color.white;
                    }
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static bool IsPointInHex(Vector2 p, Vector2[] verts)
        {
            int intersections = 0;
            for (int i = 0; i < 6; i++)
            {
                Vector2 v1 = verts[i];
                Vector2 v2 = verts[(i + 1) % 6];
                if ((v1.y > p.y) != (v2.y > p.y))
                {
                    float x = (v2.x - v1.x) * (p.y - v1.y) / (v2.y - v1.y) + v1.x;
                    if (p.x < x) intersections++;
                }
            }
            return intersections % 2 == 1;
        }

        private void SetupBorder()
        {
            // 배경 이미지에 육각형 스프라이트 적용
            if (backgroundImage != null)
            {
                backgroundImage.sprite = hexFillSprite;
                backgroundImage.color = new Color(0.2f, 0.2f, 0.2f, 0.5f);
                backgroundImage.type = Image.Type.Simple;
            }

            // 테두리 이미지 생성 또는 설정
            if (borderImage == null)
            {
                GameObject borderObj = new GameObject("Border");
                borderObj.transform.SetParent(transform, false);
                borderObj.transform.SetAsFirstSibling();

                borderImage = borderObj.AddComponent<Image>();

                RectTransform rt = borderObj.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }

            borderImage.sprite = hexBorderSprite;
            borderImage.color = new Color(0.15f, 0.15f, 0.15f, 1f);
            borderImage.raycastTarget = false;
            borderImage.type = Image.Type.Simple;

            // 젬 이미지 생성 또는 설정
            if (gemImage == null)
            {
                GameObject gemObj = new GameObject("GemImage");
                gemObj.transform.SetParent(transform, false);

                gemImage = gemObj.AddComponent<Image>();

                RectTransform rt = gemObj.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.08f, 0.08f);
                rt.anchorMax = new Vector2(0.92f, 0.92f);
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }

            gemImage.sprite = hexFillSprite;
            gemImage.raycastTarget = false;
            gemImage.type = Image.Type.Simple;
        }

        private void SetupDrillIndicator()
        {
            if (drillIndicator == null)
            {
                GameObject drillObj = new GameObject("DrillIndicator");
                drillObj.transform.SetParent(transform, false);

                drillIndicator = drillObj.AddComponent<Image>();

                RectTransform rt = drillObj.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(40f, 40f);
                rt.anchoredPosition = Vector2.zero;
            }
            drillIndicator.raycastTarget = false;
            drillIndicator.enabled = false;
        }

        public void Initialize(HexCoord coord, HexGrid grid)
        {
            this.coord = coord;
            this.parentGrid = grid;
            this.blockData = new BlockData();

            // 스프라이트 재확인 (Initialize가 Awake 후에 호출되므로)
            EnsureSpritesCreated();

            if (backgroundImage != null)
            {
                backgroundImage.raycastTarget = true;
                if (backgroundImage.sprite == null)
                    backgroundImage.sprite = hexFillSprite;
            }

            if (borderImage != null && borderImage.sprite == null)
                borderImage.sprite = hexBorderSprite;

            if (gemImage != null && gemImage.sprite == null)
                gemImage.sprite = hexFillSprite;

            SetupVisuals();
        }

        private void SetupVisuals()
        {
            if (backgroundImage != null) backgroundImage.color = new Color(0.2f, 0.2f, 0.2f, 0.5f);
            if (overlayImage != null) overlayImage.enabled = false;
            if (timerText != null) timerText.enabled = false;
            if (drillIndicator != null) drillIndicator.enabled = false;
        }

        public void SetBlockData(BlockData data)
        {
            blockData = data != null ? data.Clone() : new BlockData();
            isMatched = false;
            UpdateVisuals();
        }

        /// <summary>
        /// 데이터 클리어 - 빈 블록으로 만들고 시각적으로 완전히 숨김
        /// </summary>
        public void ClearData()
        {
            blockData = new BlockData();
            blockData.gemType = GemType.None;
            isMatched = false;
            isHighlighted = false;

            // 즉시 시각적으로 숨김 (잔상 방지)
            HideVisuals();
        }

        /// <summary>
        /// 모든 시각적 요소를 즉시 숨김 (잔상 방지용)
        /// </summary>
        public void HideVisuals()
        {
            if (gemImage != null)
            {
                gemImage.enabled = false;
                gemImage.color = Color.clear;
            }

            if (borderImage != null)
            {
                borderImage.enabled = false;
            }

            if (backgroundImage != null)
            {
                backgroundImage.color = new Color(0, 0, 0, 0);
            }

            if (overlayImage != null) overlayImage.enabled = false;
            if (timerText != null) timerText.enabled = false;
            if (drillIndicator != null) drillIndicator.enabled = false;
        }

        public void UpdateVisuals()
        {
            // 스프라이트 확인
            EnsureSpritesCreated();

            if (blockData == null || blockData.gemType == GemType.None)
            {
                SetEmpty();
                return;
            }

            // 배경 복원 (숨겨진 상태에서 복구)
            if (backgroundImage != null)
            {
                backgroundImage.color = new Color(0.2f, 0.2f, 0.2f, 0.5f);
            }

            Color gemColor = GemColors.GetColor(blockData.gemType);
            SetGemColor(gemColor);

            if (borderImage != null)
            {
                borderImage.enabled = true;
                borderImage.color = isMatched ? Color.white : new Color(0.15f, 0.15f, 0.15f, 1f);
            }

            HandleSpecialBlock();
            UpdateOverlay();
            UpdateDrillIndicator();
        }

        private void HandleSpecialBlock()
        {
            if (blockData == null) return;
            switch (blockData.specialType)
            {
                case SpecialBlockType.MoveBlock:
                    SetGemColor(new Color(0.5f, 0.5f, 0.5f, 0.8f));
                    break;
                case SpecialBlockType.FixedBlock:
                    SetGemColor(new Color(0.3f, 0.3f, 0.35f, 1f));
                    break;
                case SpecialBlockType.TimeBomb:
                    ShowTimerText(blockData.timeBombCount);
                    break;
            }
        }

        private void UpdateDrillIndicator()
        {
            if (drillIndicator == null) return;
            if (blockData != null && blockData.specialType == SpecialBlockType.Drill)
                ShowDrillIndicator(blockData.drillDirection);
            else
                drillIndicator.enabled = false;
        }

        public void ShowDrillIndicator(DrillDirection direction)
        {
            if (drillIndicator == null) return;
            drillIndicator.enabled = true;
            drillIndicator.color = Color.white;
            switch (direction)
            {
                case DrillDirection.Vertical:
                    drillIndicator.sprite = drillVerticalSprite;
                    break;
                case DrillDirection.Slash:
                    drillIndicator.sprite = drillSlashSprite;
                    break;
                case DrillDirection.BackSlash:
                    drillIndicator.sprite = drillBackSlashSprite;
                    break;
            }
        }

        private void SetGemColor(Color color)
        {
            if (gemImage != null)
            {
                if (gemImage.sprite == null)
                    gemImage.sprite = hexFillSprite;
                gemImage.color = color;
                gemImage.enabled = true;  // 반드시 활성화
            }
            else if (backgroundImage != null)
            {
                backgroundImage.color = color;
            }

            // 테두리도 활성화
            if (borderImage != null)
            {
                borderImage.enabled = true;
            }
        }

        public void SetEmpty()
        {
            // gemImage를 완전히 투명하게 (잔상 방지)
            if (gemImage != null)
            {
                gemImage.color = Color.clear;  // 완전 투명
                gemImage.enabled = false;      // 비활성화
            }

            // 배경도 투명하게
            if (backgroundImage != null)
            {
                backgroundImage.color = new Color(0.2f, 0.2f, 0.2f, 0.1f);
            }

            // 테두리 숨김
            if (borderImage != null)
            {
                borderImage.enabled = false;
            }

            if (overlayImage != null) overlayImage.enabled = false;
            if (timerText != null) timerText.enabled = false;
            if (drillIndicator != null) drillIndicator.enabled = false;
        }

        private void UpdateOverlay()
        {
            if (overlayImage == null || blockData == null) return;
            if (blockData.hasChain)
            {
                overlayImage.color = new Color(0.4f, 0.6f, 0.4f, 0.8f);
                overlayImage.enabled = true;
            }
            else if (blockData.vinylLayer > 0)
            {
                float alpha = blockData.vinylLayer == 2 ? 0.6f : 0.3f;
                overlayImage.color = new Color(1f, 1f, 1f, alpha);
                overlayImage.enabled = true;
            }
            else
            {
                overlayImage.enabled = false;
            }
        }

        private void ShowTimerText(int count)
        {
            if (timerText != null)
            {
                timerText.text = count.ToString();
                timerText.enabled = true;
                timerText.color = count <= 3 ? Color.red : Color.white;
            }
        }

        public void SetSelected(bool selected) { isSelected = selected; }

        public void SetHighlighted(bool highlighted)
        {
            isHighlighted = highlighted;
            if (borderImage != null)
            {
                // 하이라이트 색상을 밝은 흰색으로 변경 (노란색 대신)
                if (highlighted) borderImage.color = new Color(1f, 1f, 1f, 1f);
                else if (!isMatched) borderImage.color = new Color(0.15f, 0.15f, 0.15f, 1f);
            }
        }

        public void SetMatched(bool matched)
        {
            isMatched = matched;
            if (borderImage != null)
                borderImage.color = matched ? Color.white : new Color(0.15f, 0.15f, 0.15f, 1f);
        }

        public bool DecrementTimeBomb()
        {
            if (blockData != null && blockData.specialType == SpecialBlockType.TimeBomb)
            {
                blockData.timeBombCount--;
                ShowTimerText(blockData.timeBombCount);
                return blockData.timeBombCount <= 0;
            }
            return false;
        }

        public bool RemoveVinyl()
        {
            if (blockData != null && blockData.vinylLayer > 0)
            {
                blockData.vinylLayer--;
                UpdateOverlay();
                return blockData.vinylLayer == 0;
            }
            return true;
        }

        public void RemoveChain()
        {
            if (blockData != null)
            {
                blockData.hasChain = false;
                UpdateOverlay();
            }
        }

        public void OnPointerClick(PointerEventData eventData) { OnBlockClicked?.Invoke(this); }
        public void OnPointerDown(PointerEventData eventData) { OnBlockPressed?.Invoke(this); }
        public void OnPointerUp(PointerEventData eventData) { OnBlockReleased?.Invoke(this); }

        public void SwapDataWith(HexBlock other)
        {
            if (other == null) return;
            BlockData temp = this.blockData.Clone();
            this.SetBlockData(other.blockData);
            other.SetBlockData(temp);
        }
    }
}