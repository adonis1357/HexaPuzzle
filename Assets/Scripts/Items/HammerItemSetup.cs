using UnityEngine;
using UnityEngine.UI;
using JewelsHexaPuzzle.Items;

namespace JewelsHexaPuzzle.Core
{
    /// <summary>
    /// 망치 아이템 UI를 코드로 생성
    /// Canvas 하위에 자동 배치
    /// </summary>
    public class HammerItemSetup : MonoBehaviour
    {
        private void Start()
        {
            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas == null) return;

            // 이미 존재하면 스킵
            if (FindObjectOfType<HammerItem>() != null) return;

            CreateHammerUI(canvas.transform);
        }

        private void CreateHammerUI(Transform canvasTransform)
        {
            // === 화면 오버레이 (연두색 배경) ===
            GameObject overlayObj = new GameObject("HammerOverlay");
            overlayObj.transform.SetParent(canvasTransform, false);
            overlayObj.transform.SetAsFirstSibling(); // 가장 뒤에 렌더링

            RectTransform overlayRt = overlayObj.AddComponent<RectTransform>();
            overlayRt.anchorMin = Vector2.zero;
            overlayRt.anchorMax = Vector2.one;
            overlayRt.offsetMin = Vector2.zero;
            overlayRt.offsetMax = Vector2.zero;

            Image overlayImage = overlayObj.AddComponent<Image>();
            overlayImage.color = Color.clear;
            overlayImage.raycastTarget = false;
            overlayObj.SetActive(false);

            // === 망치 버튼 컨테이너 ===
            GameObject btnObj = new GameObject("HammerButton");
            btnObj.transform.SetParent(canvasTransform, false);

            RectTransform btnRt = btnObj.AddComponent<RectTransform>();
            btnRt.anchorMin = new Vector2(1f, 0f);
            btnRt.anchorMax = new Vector2(1f, 0f);
            btnRt.pivot = new Vector2(1f, 0f);
            btnRt.anchoredPosition = new Vector2(-20f, 20f);
            btnRt.sizeDelta = new Vector2(70f, 70f);

            // 버튼 배경
            Image btnBg = btnObj.AddComponent<Image>();
            btnBg.color = new Color(0.25f, 0.25f, 0.3f, 0.9f);

            // 둥근 모서리 효과를 위한 Outline
            var outline = btnObj.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 1f, 1f, 0.3f);
            outline.effectDistance = new Vector2(1, -1);

            Button button = btnObj.AddComponent<Button>();
            var colors = button.colors;
            colors.highlightedColor = new Color(0.4f, 0.9f, 0.4f, 1f);
            colors.pressedColor = new Color(0.2f, 0.7f, 0.2f, 1f);
            button.colors = colors;

            // === 망치 아이콘 (코드로 생성) ===
            GameObject iconObj = new GameObject("HammerIcon");
            iconObj.transform.SetParent(btnObj.transform, false);

            RectTransform iconRt = iconObj.AddComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0.1f, 0.1f);
            iconRt.anchorMax = new Vector2(0.9f, 0.9f);
            iconRt.offsetMin = Vector2.zero;
            iconRt.offsetMax = Vector2.zero;

            Image iconImage = iconObj.AddComponent<Image>();
            iconImage.sprite = CreateHammerSprite();
            iconImage.raycastTarget = false;
            iconImage.preserveAspect = true;

            // === HammerItem 컴포넌트 연결 ===
            HammerItem hammerItem = btnObj.AddComponent<HammerItem>();
            // SerializeField를 런타임에 설정
            SetPrivateField(hammerItem, "hammerButton", button);
            SetPrivateField(hammerItem, "hammerIcon", iconImage);
            SetPrivateField(hammerItem, "buttonBackground", btnBg);
            SetPrivateField(hammerItem, "screenOverlay", overlayImage);

            Debug.Log("[HammerItemSetup] Hammer button created at bottom-right");
        }

        /// <summary>
        /// 망치 모양 Sprite를 코드로 생성
        /// </summary>
        private Sprite CreateHammerSprite()
        {
            int size = 64;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;

            // 투명 배경
            Color clear = new Color(0, 0, 0, 0);
            Color[] pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = clear;

            Color handleColor = new Color(0.65f, 0.45f, 0.25f, 1f);
            Color handleDark = new Color(0.5f, 0.35f, 0.18f, 1f);
            Color headColor = new Color(0.75f, 0.75f, 0.8f, 1f);
            Color headLight = new Color(0.9f, 0.9f, 0.95f, 1f);
            Color headDark = new Color(0.55f, 0.55f, 0.6f, 1f);

            // 자루 (대각선으로 그림 - 좌하단에서 중앙으로)
            for (int i = 0; i < 28; i++)
            {
                int x = 10 + i * 1;
                int y = 6 + i * 1;
                FillRect(pixels, size, x, y, 5, 5, handleColor);
                FillRect(pixels, size, x + 1, y + 1, 3, 3, handleDark);
            }

            // 머리 (상단 - 가로로 넓은 직사각형)
            FillRect(pixels, size, 18, 42, 30, 14, headColor);
            FillRect(pixels, size, 20, 44, 26, 10, headLight);
            FillRect(pixels, size, 18, 42, 30, 2, headDark);
            FillRect(pixels, size, 18, 54, 30, 2, headLight);

            // 머리 좌우 면
            FillRect(pixels, size, 18, 42, 3, 14, headDark);
            FillRect(pixels, size, 45, 42, 3, 14, headColor);

            tex.SetPixels(pixels);
            tex.Apply();

            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        private void FillRect(Color[] pixels, int texSize, int x, int y, int w, int h, Color color)
        {
            for (int py = y; py < y + h && py < texSize; py++)
            {
                for (int px = x; px < x + w && px < texSize; px++)
                {
                    if (px >= 0 && px < texSize && py >= 0 && py < texSize)
                        pixels[py * texSize + px] = color;
                }
            }
        }

        private void SetPrivateField(object obj, string fieldName, object value)
        {
            var field = obj.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
                field.SetValue(obj, value);
            else
                Debug.LogWarning($"[HammerItemSetup] Field '{fieldName}' not found on {obj.GetType().Name}");
        }
    }
}
