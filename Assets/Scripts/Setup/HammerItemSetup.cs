using UnityEngine;
using UnityEngine.UI;
using JewelsHexaPuzzle.Items;

namespace JewelsHexaPuzzle.Setup
{
    public class HammerItemSetup : MonoBehaviour
    {
        [SerializeField] private Canvas targetCanvas;

        private void Awake()
        {
            if (targetCanvas == null)
                targetCanvas = FindObjectOfType<Canvas>();
            if (targetCanvas == null)
            {
                Debug.LogError("[HammerItemSetup] No Canvas found!");
                return;
            }
            CreateHammerUI();
            Destroy(this);
        }

        private void CreateHammerUI()
        {
            GameObject overlayObj = new GameObject("HammerOverlay");
            overlayObj.transform.SetParent(targetCanvas.transform, false);
            var overlay = overlayObj.AddComponent<Image>();
            overlay.color = new Color(0.6f, 1f, 0.6f, 0.15f);
            overlay.raycastTarget = false;
            RectTransform overlayRt = overlayObj.GetComponent<RectTransform>();
            overlayRt.anchorMin = Vector2.zero;
            overlayRt.anchorMax = Vector2.one;
            overlayRt.offsetMin = Vector2.zero;
            overlayRt.offsetMax = Vector2.zero;
            overlayObj.SetActive(false);

            GameObject btnObj = new GameObject("HammerButton");
            btnObj.transform.SetParent(targetCanvas.transform, false);
            RectTransform btnRt = btnObj.AddComponent<RectTransform>();
            btnRt.anchorMin = new Vector2(1f, 0.5f);
            btnRt.anchorMax = new Vector2(1f, 0.5f);
            btnRt.pivot = new Vector2(1f, 0.5f);
            btnRt.anchoredPosition = new Vector2(-20f, -450f);
            btnRt.sizeDelta = new Vector2(80f, 80f);
            var btnImage = btnObj.AddComponent<Image>();
            btnImage.color = new Color(0.25f, 0.25f, 0.35f, 0.9f);
            var btn = btnObj.AddComponent<Button>();
            var btnColors = btn.colors;
            btnColors.highlightedColor = new Color(0.4f, 0.9f, 0.4f, 1f);
            btnColors.pressedColor = new Color(0.3f, 0.7f, 0.3f, 1f);
            btn.colors = btnColors;

            GameObject headObj = new GameObject("HammerHead");
            headObj.transform.SetParent(btnObj.transform, false);
            var headImg = headObj.AddComponent<Image>();
            headImg.color = new Color(0.7f, 0.7f, 0.75f, 1f);
            headImg.raycastTarget = false;
            RectTransform headRt = headObj.GetComponent<RectTransform>();
            headRt.anchoredPosition = new Vector2(0f, 12f);
            headRt.sizeDelta = new Vector2(36f, 18f);
            headRt.localRotation = Quaternion.Euler(0, 0, -15f);

            GameObject handleObj = new GameObject("HammerHandle");
            handleObj.transform.SetParent(btnObj.transform, false);
            var handleImg = handleObj.AddComponent<Image>();
            handleImg.color = new Color(0.55f, 0.35f, 0.15f, 1f);
            handleImg.raycastTarget = false;
            RectTransform handleRt = handleObj.GetComponent<RectTransform>();
            handleRt.anchoredPosition = new Vector2(2f, -12f);
            handleRt.sizeDelta = new Vector2(8f, 32f);
            handleRt.localRotation = Quaternion.Euler(0, 0, -15f);

            GameObject hlObj = new GameObject("HammerHL");
            hlObj.transform.SetParent(headObj.transform, false);
            var hlImg = hlObj.AddComponent<Image>();
            hlImg.color = new Color(1f, 1f, 1f, 0.3f);
            hlImg.raycastTarget = false;
            RectTransform hlRt = hlObj.GetComponent<RectTransform>();
            hlRt.anchoredPosition = new Vector2(-5f, 3f);
            hlRt.sizeDelta = new Vector2(10f, 6f);

            GameObject textObj = new GameObject("HammerLabel");
            textObj.transform.SetParent(btnObj.transform, false);
            var label = textObj.AddComponent<Text>();
            label.text = "망치";
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 12;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.raycastTarget = false;
            RectTransform textRt = textObj.GetComponent<RectTransform>();
            textRt.anchorMin = new Vector2(0f, 0f);
            textRt.anchorMax = new Vector2(1f, 0f);
            textRt.anchoredPosition = new Vector2(0f, 8f);
            textRt.sizeDelta = new Vector2(0f, 16f);

            var hammer = btnObj.AddComponent<HammerItem>();
            var type = typeof(HammerItem);
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            type.GetField("hammerButton", flags)?.SetValue(hammer, btn);
            type.GetField("backgroundOverlay", flags)?.SetValue(hammer, overlay);

            Debug.Log("[HammerItemSetup] Hammer UI created");
        }
    }
}
