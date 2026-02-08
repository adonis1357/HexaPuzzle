using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using JewelsHexaPuzzle.Data;
using System;
using System.Collections;

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

        // ��������Ʈ ĳ�� (�ν��Ͻ����� �������� �ʰ� static���� ����)
        private static Sprite hexFillSprite;      // Ǯ ������ (����)
        private static Sprite hexBorderSprite;     // �׵θ� �� (�׵θ���)
        private static Sprite hexGemSprite;        // �׵θ� ���� ������ (�� �����)
        private static Sprite drillVerticalSprite;
        private static Sprite drillSlashSprite;
        private static Sprite drillBackSlashSprite;

        private const float BORDER_WIDTH = 10f;
        private const float INNER_BORDER_WIDTH = 7f;  // ���� �׵θ� 30% ��� (10 * 0.7)

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
        /// ��������Ʈ�� ������ ���� (Play ��� ����� �ÿ��� ����)
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
                drillVerticalSprite = CreateArrowSprite(64, 0);      // r축: 화면 세로 ↕
                drillSlashSprite = CreateArrowSprite(64, -60);     // s축: 화면 / 방향 (세로에서 시계 60°)
                drillBackSlashSprite = CreateArrowSprite(64, 60);  // q축: 화면 \\ 방향 (세로에서 시계 120°)면 \\ 방향방향 (-60°)
            }
        }

        /// <summary>
        /// ������ flat-top ������ �������� signed distance (����=����, ���=�ٱ�)
        /// flat-top: �������� 0��,60��,120��... �� ���� ������ 30��,90��,150��...
        /// </summary>
        private static float HexSignedDistance(Vector2 point, Vector2 center, float radius)
        {
            Vector2 p = point - center;
            float maxDist = float.MinValue;
            for (int i = 0; i < 6; i++)
            {
                // flat-top ����: 30�� + i*60��
                float angle = (30f + i * 60f) * Mathf.Deg2Rad;
                Vector2 normal = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                // flat-top���� �߽�~�� �Ÿ� = radius * cos(30��) = radius * sqrt(3)/2
                float edgeDist = radius * 0.8660254f; // sqrt(3)/2
                float dist = Vector2.Dot(p, normal) - edgeDist;
                if (dist > maxDist) maxDist = dist;
            }
            return maxDist;
        }

        /// <summary>
        /// ��Ƽ�ٸ���� ���� ������ (fill �Ǵ� border)
        /// </summary>
        private static Sprite CreateAAHexSprite(int size, float borderWidth, bool isBorderOnly)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Color[] pixels = new Color[size * size];

            Vector2 center = new Vector2(size / 2f, size / 2f);
            float outerRadius = size / 2f - 2f;
            float innerRadius = outerRadius - borderWidth;
            float aa = 2.0f; // ��Ƽ�ٸ���� �� (�ȼ�)

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
        /// ��Ƽ�ٸ���� ���� ���� ������ (�� �����)
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

        // (�� �޼��� ���ŵ� - CreateAAHexSprite/CreateAAInnerHexSprite�� ��ü)

        private void SetupBorder()
        {
            // ��� �̹��� - ���� ������ (raycast ������)
            if (backgroundImage != null)
            {
                backgroundImage.sprite = hexFillSprite;
                backgroundImage.color = new Color(0.2f, 0.2f, 0.2f, 0.5f);
                backgroundImage.type = Image.Type.Simple;
            }

            // �� �̹��� - �׵θ� ���ʸ� ä��� ������ (�������� ����)
            if (gemImage == null)
            {
                GameObject gemObj = new GameObject("GemImage");
                gemObj.transform.SetParent(transform, false);

                gemImage = gemObj.AddComponent<Image>();

                // Ǯ������ ��Ŀ - ��������Ʈ ��ü�� inner hex�̹Ƿ� ��� ���ʿ�
                RectTransform rt = gemObj.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }

            gemImage.sprite = hexGemSprite;  // inner hex ��������Ʈ ���
            gemImage.raycastTarget = false;
            gemImage.type = Image.Type.Simple;

            // �׵θ� �̹��� - �� �̹��� ���� ��ġ�Ͽ� �������� �κ��� ����
            if (borderImage == null)
            {
                GameObject borderObj = new GameObject("Border");
                borderObj.transform.SetParent(transform, false);
                borderObj.transform.SetAsLastSibling();  // �� ���� �׸� (gem�� ����)

                borderImage = borderObj.AddComponent<Image>();

                RectTransform rt = borderObj.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }
            else
            {
                // �̹� �����ϸ� �� ���� �̵�
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
                drillObj.transform.SetAsLastSibling();  // �� ���� ǥ��

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

            // ��������Ʈ ��Ȯ�� (Initialize�� Awake �Ŀ� ȣ��ǹǷ�)
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
            // 기존 점멸 정지 (데이터가 바뀌므로)
            if (blinkCoroutine != null)
            {
                StopCoroutine(blinkCoroutine);
                blinkCoroutine = null;
            }
            isPendingActivation = false;

            blockData = data != null ? data.Clone() : new BlockData();
            isMatched = false;
            UpdateVisuals();

            // 새 데이터에 pendingActivation이 있으면 점멸 재시작
            if (blockData.pendingActivation)
            {
                isPendingActivation = true;
                StartWarningBlink(10f);
            }
        }

        /// <summary>
        /// ������ Ŭ���� - �� ������� ����� �ð������� ������ ����
        /// </summary>
        public void ClearData()
        {
            blockData = new BlockData();
            blockData.gemType = GemType.None;
            isMatched = false;
            isHighlighted = false;

            // ��� �ð������� ���� (�ܻ� ����)
            HideVisuals();
        }

        /// <summary>
        /// ��� �ð��� ��Ҹ� ��� ���� (�ܻ� ������)
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
            // ��������Ʈ Ȯ��
            EnsureSpritesCreated();

            if (blockData == null || blockData.gemType == GemType.None)
            {
                SetEmpty();
                return;
            }

            // ��� ���� (������ ���¿��� ����)
            if (backgroundImage != null)
            {
                backgroundImage.color = new Color(0.2f, 0.2f, 0.2f, 0.5f);
            }

            Color gemColor = GemColors.GetColor(blockData.gemType);
            SetGemColor(gemColor);

            if (borderImage != null)
            if (borderImage != null)
            {
                borderImage.enabled = true;
                // pendingActivation이면 빨간색 테두리 유지
                if (blockData != null && blockData.pendingActivation)
                    borderImage.color = new Color(1f, 0.15f, 0.1f, 1f);
                else
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
            // gemImage�� ������ �����ϰ� (�ܻ� ����)
            if (gemImage != null)
            {
                gemImage.color = Color.clear;  // ���� ����
                gemImage.enabled = false;      // ��Ȱ��ȭ
            }

            // ��浵 �����ϰ�
            if (backgroundImage != null)
            {
                backgroundImage.color = new Color(0.2f, 0.2f, 0.2f, 0.1f);
            }

            // �׵θ� ����
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
                // ���̶���Ʈ ������ ���� ������� ���� (����� ���)
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

// === 빨간색 테두리 점멸 (특수 블록 연쇄 발동 예고) ===
        private bool isPendingActivation;
        public bool IsPendingActivation => isPendingActivation;
        private Coroutine blinkCoroutine;
        private Color originalBorderColor;

        /// <summary>
        /// 빨간색 테두리 점멸 시작 (충돌 직후 호출)
        /// 시간이 지날수록 점멸 속도가 빨라져서 유저가 발동 시점을 예측 가능
        /// </summary>
public void StartWarningBlink(float totalDuration = 1.5f)
        {
            StopWarningBlink();
            if (borderImage != null)
            {
                originalBorderColor = borderImage.color;
                borderImage.enabled = true;
            }
            blinkCoroutine = StartCoroutine(WarningBlinkCoroutine(totalDuration));
        }

        /// <summary>
        /// 점멸 정지 및 원래 색상 복원
        /// </summary>
public void StopWarningBlink()
        {
            if (blinkCoroutine != null)
            {
                StopCoroutine(blinkCoroutine);
                blinkCoroutine = null;
            }
            // 빨간색 테두리 상태 해제 → 원래 색상 복원
            isPendingActivation = false;
            if (borderImage != null)
            {
                borderImage.color = isMatched ? Color.white : new Color(0.15f, 0.15f, 0.15f, 1f);
            }
        }

private IEnumerator WarningBlinkCoroutine(float totalDuration)
        {
            // 빨간색 테두리는 SetPendingActivation에서 이미 설정됨
            // 여기서는 점멸 속도를 점점 빨리 하여 발동 예고
            Color blinkColor = new Color(1f, 0.15f, 0.1f, 1f); // 빨간색
            Color dimColor = new Color(0.6f, 0.05f, 0.03f, 1f); // 어두운 빨간색
            float elapsed = 0f;

            // 초기 점멸 주기 0.4초 → 발동 직전 0.06초까지 가속
            float startInterval = 0.4f;
            float endInterval = 0.06f;

            while (elapsed < totalDuration)
            {
                float t = elapsed / totalDuration; // 0 → 1
                float currentInterval = Mathf.Lerp(startInterval, endInterval, t * t); // 점점 가속

                // 밝은 빨간색 ON
                if (borderImage != null)
                    borderImage.color = blinkColor;
                yield return new WaitForSeconds(currentInterval * 0.5f);
                elapsed += currentInterval * 0.5f;

                // 어두운 빨간색 OFF (완전히 사라지지 않고 빨간 톤 유지)
                if (borderImage != null)
                    borderImage.color = dimColor;
                yield return new WaitForSeconds(currentInterval * 0.5f);
                elapsed += currentInterval * 0.5f;
            }

            // 마지막에 밝은 빨간색으로 끝내서 발동 순간 강조
            if (borderImage != null)
                borderImage.color = blinkColor;

            blinkCoroutine = null;
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
    

/// <summary>
        /// 드릴에 의해 영향받은 즉시 호출 - 빨간색 테두리로 변경
        /// 이후 실제 발동 전에 StartWarningBlink로 점멸 가속
        /// </summary>
public void SetPendingActivation()
        {
            isPendingActivation = true;
            // Data에도 플래그 설정 (낙하 시 Data가 Clone되어 이동해도 플래그 유지)
            if (blockData != null)
                blockData.pendingActivation = true;
            if (borderImage != null)
            {
                borderImage.enabled = true;
                borderImage.color = new Color(1f, 0.15f, 0.1f, 1f);
            }
        }
}
}