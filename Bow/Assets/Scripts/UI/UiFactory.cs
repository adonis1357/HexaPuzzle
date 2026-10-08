using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Bow.Art;

namespace Bow.UI
{
    /// <summary>uGUI 요소를 코드로 생성하는 헬퍼 (외부 프리팹/에셋 없음). 기준 해상도 1080×1920.</summary>
    public static class UiFactory
    {
        private static Font font;

        /// <summary>내장 폰트 (OS 폴백으로 한글 표시)</summary>
        public static Font DefaultFont
        {
            get
            {
                if (font == null)
                {
                    font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                }
                return font;
            }
        }

        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        public static Canvas CreateCanvas(string name, int sortingOrder)
        {
            GameObject go = new GameObject(name);
            Canvas c = go.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = sortingOrder;
            CanvasScaler sc = go.AddComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1080f, 1920f);
            sc.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return c;
        }

        public static RectTransform Rect(Transform parent, string name)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static RectTransform Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            return rt;
        }

        public static void Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = anchor; rt.anchorMax = anchor; rt.pivot = pivot;
            rt.anchoredPosition = pos; rt.sizeDelta = size;
        }

        public static Image MakeImage(Transform parent, string name, Sprite sprite, Color color, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            RectTransform rt = Rect(parent, name);
            Place(rt, anchor, pivot, pos, size);
            Image img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            if (sprite != null && sprite.border.sqrMagnitude > 0f) img.type = UnityEngine.UI.Image.Type.Sliced;
            return img;
        }

        public static Text MakeText(Transform parent, string name, string text, int size, Color color, TextAnchor align, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 boxSize, bool bold = false)
        {
            RectTransform rt = Rect(parent, name);
            Place(rt, anchor, pivot, pos, boxSize);
            Text t = rt.gameObject.AddComponent<Text>();
            t.font = DefaultFont;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        /// <summary>먹 테두리 사각 버튼 (아트 §6.2). 면/테두리/라벨 3장.</summary>
        public static Button MakeButton(Transform parent, string label, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, bool primary, UnityAction onClick)
        {
            RectTransform rt = Rect(parent, "btn_" + label);
            Place(rt, anchor, pivot, pos, size);
            Image face = rt.gameObject.AddComponent<Image>();
            face.sprite = ProceduralSprites.WhiteRect();
            face.color = Palette.WithAlpha(Palette.PaperL, 0.9f);
            Image border = MakeImage(rt, "border", ProceduralSprites.RoundedBox(), primary ? Palette.Red : Palette.Ink, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
            border.type = UnityEngine.UI.Image.Type.Sliced;
            Text t = MakeText(rt, "label", label, 52, Palette.Ink, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
            if (primary)
                MakeImage(rt, "dot", ProceduralSprites.WhiteRect(), Palette.Red, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-14f, -14f), new Vector2(24f, 24f));
            Button b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = face;
            ColorBlock cb = b.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(0.93f, 0.9f, 0.82f, 1f);
            cb.pressedColor = Palette.Ink;
            cb.disabledColor = new Color(1f, 1f, 1f, 0.5f);
            cb.fadeDuration = 0.06f;
            b.colors = cb;
            ButtonPressFx fx = rt.gameObject.AddComponent<ButtonPressFx>();
            fx.label = t;
            if (onClick != null) b.onClick.AddListener(onClick);
            b.onClick.AddListener(() => Bow.Audio.BowAudio.I.Play("ui_click", 0.6f, 0.97f, 1.03f));
            return b;
        }

        /// <summary>레거시 InputField (IP 입력용)</summary>
        public static InputField MakeInput(Transform parent, string placeholder, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            RectTransform rt = Rect(parent, "input");
            Place(rt, anchor, pivot, pos, size);
            Image bg = rt.gameObject.AddComponent<Image>();
            bg.sprite = ProceduralSprites.WhiteRect();
            bg.color = Palette.PaperL;
            MakeImage(rt, "border", ProceduralSprites.RoundedBox(), Palette.Ink, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size).type = UnityEngine.UI.Image.Type.Sliced;
            Text txt = MakeText(rt, "text", "", 44, Palette.Ink, TextAnchor.MiddleLeft, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size - new Vector2(40f, 0f));
            txt.supportRichText = false;
            Text ph = MakeText(rt, "placeholder", placeholder, 44, Palette.Grey, TextAnchor.MiddleLeft, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size - new Vector2(40f, 0f));
            InputField f = rt.gameObject.AddComponent<InputField>();
            f.targetGraphic = bg;
            f.textComponent = txt;
            f.placeholder = ph;
            f.contentType = UnityEngine.UI.InputField.ContentType.Standard;
            f.characterLimit = 48;
            return f;
        }
    }

    /// <summary>버튼 눌림 스케일 0.96 (0.06s) → 해제 EaseOutBack 0.12s, 라벨 색 반전</summary>
    public sealed class ButtonPressFx : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public Text label;
        private Coroutine routine;

        public void OnPointerDown(PointerEventData e)
        {
            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(Scale(1f, 0.96f, 0.06f, false));
            if (label != null) label.color = Palette.Paper;
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(Scale(0.96f, 1f, 0.12f, true));
            if (label != null) label.color = Palette.Ink;
        }

        private IEnumerator Scale(float a, float b, float dur, bool back)
        {
            float t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float u = back ? Ease.OutBack(t / dur) : Mathf.Clamp01(t / dur);
                float s = Mathf.LerpUnclamped(a, b, u);
                transform.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            transform.localScale = new Vector3(b, b, 1f);
        }
    }
}
