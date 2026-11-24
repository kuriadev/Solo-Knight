using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SoloKnight
{
    /// <summary>
    /// Builds uGUI elements from code. Used both at edit time (scene builder) and at runtime (fallback),
    /// so the UI never depends on hand-wired inspector references.
    /// </summary>
    public static class UIFactory
    {
        public static readonly Color PanelColor = new Color(0.06f, 0.06f, 0.08f, 0.82f);
        public static readonly Color WindowColor = new Color(0.09f, 0.09f, 0.12f, 0.97f);
        public static readonly Color AccentColor = new Color(1f, 0.78f, 0.3f);
        public static readonly Color ButtonColor = new Color(0.22f, 0.22f, 0.28f);

        static Font font;

        public static Font Font
        {
            get
            {
                if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return font;
            }
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5; // UI
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        /// <summary>Anchors the rect at a single point (0..1) with the given pivot, position and size.</summary>
        public static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        /// <summary>Top-left anchored placement inside a parent (x right, y down).</summary>
        public static RectTransform PlaceTL(RectTransform rt, float x, float y, float w, float h)
            => Place(rt, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, -y), new Vector2(w, h));

        public static RectTransform Stretch(RectTransform rt, float inset = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
            return rt;
        }

        public static Image Panel(string name, Transform parent, Color color, bool raycast = true)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = raycast;
            return img;
        }

        public static Text Label(string name, Transform parent, string text, int size, TextAnchor alignment, Color color, FontStyle style = FontStyle.Normal, bool shadow = true)
        {
            var rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.alignment = alignment;
            t.color = color;
            t.fontStyle = style;
            t.supportRichText = true;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            if (shadow)
            {
                var s = rt.gameObject.AddComponent<Shadow>();
                s.effectColor = new Color(0f, 0f, 0f, 0.8f);
                s.effectDistance = new Vector2(1.5f, -1.5f);
            }
            return t;
        }

        public static Button Button(string name, Transform parent, string label, int fontSize, Color background, out Text labelText)
        {
            var img = Panel(name, parent, background);
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.normalColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            colors.highlightedColor = Color.white;
            colors.selectedColor = new Color(0.9f, 0.9f, 0.9f, 1f);
            colors.pressedColor = new Color(0.65f, 0.65f, 0.65f, 1f);
            colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.6f);
            colors.fadeDuration = 0.06f;
            btn.colors = colors;
            var nav = btn.navigation;
            nav.mode = Navigation.Mode.None;
            btn.navigation = nav;

            labelText = Label("Label", img.transform, label, fontSize, TextAnchor.MiddleCenter, Color.white);
            Stretch(labelText.rectTransform, 4f);
            return btn;
        }

        /// <summary>A background bar with a child fill rect whose width follows SetFill().</summary>
        public static RectTransform Bar(string name, Transform parent, Color background, Color fill, out RectTransform fillRect)
        {
            var bg = Panel(name, parent, background, false);
            var f = Panel("Fill", bg.transform, fill, false);
            fillRect = f.rectTransform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.offsetMin = new Vector2(2f, 2f);
            fillRect.offsetMax = new Vector2(-2f, -2f);
            return bg.rectTransform;
        }

        public static void SetFill(RectTransform fill, float pct)
        {
            if (fill == null) return;
            fill.anchorMax = new Vector2(Mathf.Clamp01(pct), 1f);
        }

        /// <summary>Full-screen dim overlay + centred window. Returns the window.</summary>
        public static RectTransform Modal(string name, Transform parent, Vector2 size, out GameObject root, Color? windowColor = null)
        {
            var overlay = Panel(name, parent, new Color(0f, 0f, 0f, 0.65f));
            Stretch(overlay.rectTransform);
            root = overlay.gameObject;
            var win = Panel("Window", overlay.transform, windowColor ?? WindowColor);
            Place(win.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
            var outline = win.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(AccentColor.r, AccentColor.g, AccentColor.b, 0.35f);
            outline.effectDistance = new Vector2(2f, -2f);
            return win.rectTransform;
        }

        public static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            go.transform.SetParent(null);
        }

        public static void ConfigureCanvas(GameObject go, int sortingOrder = 0)
        {
            go.layer = 5;
            var canvas = go.GetComponent<Canvas>();
            if (canvas == null) canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            if (go.GetComponent<GraphicRaycaster>() == null) go.AddComponent<GraphicRaycaster>();
        }

        public static void ClearChildren(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--)
            {
                var c = t.GetChild(i).gameObject;
                if (Application.isPlaying) Object.Destroy(c);
                else Object.DestroyImmediate(c);
            }
        }
    }
}
