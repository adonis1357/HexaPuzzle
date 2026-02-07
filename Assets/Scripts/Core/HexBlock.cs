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
        private static Sprite hexFillSprite;      // 풀 육각형 (배경용)
        private static Sprite hexBorderSprite;     // 테두리 링 (테두리용)
        private static Sprite hexGemSprite;        // 테두리 안쪽 육각형 (젬 색상용)
        private static Sprite drillVerticalSprite;
        private static Sprite drillSlashSprite;
        private static Sprite drillBackSlashSprite;

        private const float BORDER_WIDTH = 10f;
        private const float INNER_BORDER_WIDTH = 7f;  // 안쪽 테두리 30% 축소 (10 * 0.7)

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
            const int TEX_SIZE = 512;
            float scale = TEX_SIZE / 128f; // 4x

            if (hexFillSprite == null)
                hexFillSprite = CreateAAHexSprite(TEX_SIZE, 0, false);
            if (hexBorderSprite == null)
                hexBorderSprite = CreateAAHexSprite(TEX_SIZE, INNER_BORDER_WIDTH * scale, true);
            if (hexGemSprite == null)
                hexGemSprite = CreateAAInnerHexSprite(TEX_SIZE, INNER_BORDER_WIDTH * scale);
            if (drillVerticalSprite == null)
            {
                drillVerticalSprite = CreateArrowSprite(64, 0);
                drillSlashSprite = CreateArrowSprite(64, -45);
                drillBackSlashSprite = CreateArrowSprite(64, 45);
            }
        }

        /// <summary>
        /// 점에서 flat-top 육각형 변까지의 signed distance (음수=안쪽, 양수=바깥)
        /// flat-top: 꼭지점이 0°,60°,120°... → 변의 법선은 30°,90°,150°...
        /// </summary>
        private static float HexSignedDistance(Vector2 point, Vector2 center, float radius)
        {
            Vector2 p = point - center;
            float maxDist = float.MinValue;
            for (int i = 0; i < 6; i++)
            {
                // flat-top 법선: 30° + i*60°
                float angle = (30f + i * 60f) * Mathf.Deg2Rad;
                Vector2 normal = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                // flat-top에서 중심~변 거리 = radius * cos(30°) = radius * sqrt(3)/2
                float edgeDist = radius * 0.8660254f; // sqrt(3)/2
                float dist = Vector2.Dot(p, normal) - edgeDist;
                if (dist > maxDist) maxDist = dist;
            }
            return maxDist;
        }

        /// <summary>
        /// 안티앨리어싱 적용 육각형 (fill 또는 border)
        /// </summary>
        private static Sprite CreateAAHexSprite(int size, float borderWidth, bool isBorderOnly)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Color[] pixels = new Color[size * size];

            Vector2 center = new Vector2(size / 2f, size / 2f);
            float outerRadius = size / 2f - 2f;
            float innerRadius = outerRadius - borderWidth;
            float aa = 2.0f; // 안티앨리어싱 폭 (픽셀)

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 point = new Vector2(x + 0.5f, y + 0.5f);
                    float outerDist = HexSignedDistance(point, center, outerRadius);

                    if (isBorderOnly)
                    {
                        float innerDist = HexSignedDistance(point, center, innerRadius);
                        float outerAlpha = Mathf.Clamp01(1f - outerDist / aa);
                        float innerAlpha = Mathf.Clamp01(innerDist / aa);
                        float alpha = outerAlpha * innerAlpha;
                        pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                    }
                    else
                    {
                        float alpha = Mathf.Clamp01(1f - outerDist / aa);
                        pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                    }
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>
        /// 안티앨리어싱 적용 내부 육각형 (젬 색상용)
        /// </summary>
        private static Sprite CreateAAInnerHexSprite(int size, float borderWidth)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Color[] pixels = new Color[size * size];

            Vector2 center = new Vector2(size / 2f, size / 2f);
            float outerRadius = size / 2f - 2f;
            float innerRadius = outerRadius - borderWidth;
            float aa = 2.0f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 point = new Vector2(x + 0.5f, y + 0.5f);
                    float dist = HexSignedDistance(point, center, innerRadius);
                    float alpha = Mathf.Clamp01(1f - dist / aa);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
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

        // (구 메서드 제거됨 - CreateAAHexSprite/CreateAAInnerHexSprite로 대체)

        private void SetupBorder()
        {
            // 배경 이미지 - 투명 육각형 (raycast 영역용)
            if (backgroundImage != null)
            {
                backgroundImage.sprite = hexFillSprite;
                backgroundImage.color = new Color(0.2f, 0.2f, 0.2f, 0.5f);
                backgroundImage.type = Image.Type.Simple;
            }

            // 젬 이미지 - 테두리 안쪽만 채우는 육각형 (삐져나감 방지)
            if (gemImage == null)
            {
                GameObject gemObj = new GameObject("GemImage");
                gemObj.transform.SetParent(transform, false);

                gemImage = gemObj.AddComponent<Image>();

                // 풀사이즈 앵커 - 스프라이트 자체가 inner hex이므로 축소 불필요
                RectTransform rt = gemObj.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }

            gemImage.sprite = hexGemSprite;  // inner hex 스프라이트 사용
            gemImage.raycastTarget = false;
            gemImage.type = Image.Type.Simple;

            // 테두리 이미지 - 젬 이미지 위에 배치하여 삐져나온 부분을 덮음
            if (borderImage == null)
            {
                GameObject borderObj = new GameObject("Border");
                borderObj.transform.SetParent(transform, false);
                borderObj.transform.SetAsLastSibling();  // 맨 위에 그림 (gem을 덮음)

                borderImage = borderObj.AddComponent<Image>();

                RectTransform rt = borderObj.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }
            else
            {
                // 이미 존재하면 맨 위로 이동
                borderImage.transform.SetAsLastSibling();
            }

            borderImage.sprite = hexBorderSprite;
            borderImage.color = new Color(0.15f, 0.15f, 0.15f, 1f);
            borderImage.raycastTarget = false;
            borderImage.type = Image.Type.Simple;
        }

        private void SetupDrillIndicator()
        {
            if (drillIndicator == null)
            {
                GameObject drillObj = new GameObject("DrillIndicator");
                drillObj.transform.SetParent(transform, false);
                drillObj.transform.SetAsLastSibling();  // 맨 위에 표시

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
                gemImage.sprite = hexGemSprite;

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
                    gemImage.sprite = hexGemSprite;
                gemImage.color = color;
                gemImage.enabled = true;
            }
            else if (backgroundImage != null)
            {
                backgroundImage.color = color;
            }

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