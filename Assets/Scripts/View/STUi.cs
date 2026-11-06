// 星轨防线 · 运行时 UI 构建工具（对应 Godot scripts/ui.gd 的 UGUI 版本）
//
// 设计约束：
//   · 设计分辨率固定 1080×1920（竖屏），CanvasScaler 等比缩放；
//   · 所有面板优先使用 Resources/Art/UI 下的九宫格素材，
//     缺图时才退回纯色圆角（与 Godot 端的退化策略一致）；
//   · 小控件（词条胶囊、编队条）保持平面样式，因为九宫格在 30–60px
//     高度上会把内容区吃掉。

using System;
using UnityEngine;
using UnityEngine.UI;

namespace StarLane
{
    public static class STUi
    {
        public const float DesignWidth = 1080f;
        public const float DesignHeight = 1920f;

        // ---------------------------------------------------------------- 字体与文字
        public static Text Label(Transform parent, string text, int size, Color color, bool bold = false)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.text = text;
            t.font = bold ? STAssets.FontBold : STAssets.FontRegular;
            t.fontSize = size;
            t.color = color;
            t.alignment = TextAnchor.UpperLeft;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.supportRichText = true;
            return t;
        }

        public static Text LabelCentered(Transform parent, string text, int size, Color color, bool bold = false)
        {
            var t = Label(parent, text, size, color, bold);
            t.alignment = TextAnchor.MiddleCenter;
            return t;
        }

        public static Text LabelRight(Transform parent, string text, int size, Color color, bool bold = false)
        {
            var t = Label(parent, text, size, color, bold);
            t.alignment = TextAnchor.UpperRight;
            return t;
        }

        /// <summary>带描边的标题文字（对应 Godot 的 font_outline）。</summary>
        public static Text Outline(Text t, Color color, float thickness = 3f)
        {
            var o = t.gameObject.AddComponent<Outline>();
            o.effectColor = color;
            o.effectDistance = new Vector2(thickness, -thickness);
            return t;
        }

        public static Text Shadow(Text t, Color color, float distance = 2f)
        {
            var o = t.gameObject.AddComponent<Shadow>();
            o.effectColor = color;
            o.effectDistance = new Vector2(distance, -distance);
            return t;
        }

        // ---------------------------------------------------------------- 布局
        public static RectTransform Rect(Transform parent, string name, float x, float y, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            SetRect(rt, x, y, w, h);
            return rt;
        }

        /// <summary>屏幕坐标 → 设计坐标：Unity 的 UI 原点在中心，Godot 在左上，这里统一换算。</summary>
        public static void SetRect(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
        }

        public static RectTransform Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        public static Canvas CreateCanvas(Transform parent, string name, int sortingOrder)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(parent, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(DesignWidth, DesignHeight);
            // 竖屏以宽度为基准等比缩放；matchWidthOrHeight=0 表示完全按宽度。
            // 宽高比高于 9:16 时纵向留边，STInput.ScreenToDesign 会把留边均分，
            // 因此设计坐标（左上原点）与屏幕始终一一对应。
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0f;
            scaler.referencePixelsPerUnit = 100f;
            return canvas;
        }

        // ---------------------------------------------------------------- 图形
        public static Image Solid(Transform parent, Color color, float x, float y, float w, float h)
        {
            var rt = Rect(parent, "Solid", x, y, w, h);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>图片（默认保持比例填充）。</summary>
        public static Image Picture(Transform parent, Sprite sprite, float x, float y, float w, float h,
            Image.Type type = Image.Type.Simple, Color? tint = null)
        {
            var rt = Rect(parent, "Picture", x, y, w, h);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.type = type;
            img.color = tint ?? Color.white;
            img.raycastTarget = false;
            img.preserveAspect = type == Image.Type.Simple;
            return img;
        }

        /// <summary>
        /// 等比铺满并裁切（对应 Godot 的 STRETCH_KEEP_ASPECT_COVERED）。
        ///
        /// Unity 没有等价的 Image 模式：preserveAspect 是「完整显示」（contain），
        /// 因此这里先按 cover 算出实际尺寸，再把 Image 放大到该尺寸并居中，
        /// 由外层的 RectMask2D 裁掉溢出部分。
        /// </summary>
        public static Image PictureCovered(Transform parent, Sprite sprite, float x, float y, float w, float h, Color? tint = null)
        {
            var img = Picture(parent, sprite, 0f, 0f, w, h, Image.Type.Simple, tint);
            img.preserveAspect = false;
            CoverInto(img.rectTransform, sprite, x, y, w, h);
            return img;
        }

        /// <summary>把 rect 按 cover 规则铺进 (x,y,w,h) 的目标框。</summary>
        public static void CoverInto(RectTransform rt, Sprite sprite, float x, float y, float w, float h)
        {
            float sw = sprite != null ? sprite.rect.width : 1f;
            float sh = sprite != null ? sprite.rect.height : 1f;
            if (sw <= 0f || sh <= 0f) { sw = w; sh = h; }

            float scale = Mathf.Max(w / sw, h / sh);
            float cw = sw * scale;
            float ch = sh * scale;

            SetRect(rt, x + (w - cw) * 0.5f, y + (h - ch) * 0.5f, cw, ch);
        }

        /// <summary>给容器加矩形裁切（用于 cover 溢出的部分）。</summary>
        public static RectMask2D Clip(Transform parent, float x, float y, float w, float h, string name = "Clip")
        {
            var rt = Rect(parent, name, x, y, w, h);
            return rt.gameObject.AddComponent<RectMask2D>();
        }

        /// <summary>九宫格素材（Sliced）。缺失时返回 null，调用方自行退回纯色。</summary>
        public static Image NineSlice(Transform parent, string key, float x, float y, float w, float h,
            Color? tint = null, bool fill = false)
        {
            var sprite = STAssets.Ui(key);
            if (sprite == null) return null;
            var img = Picture(parent, sprite, x, y, w, h, Image.Type.Sliced, tint);
            if (fill) Stretch(img.rectTransform);
            return img;
        }

        /// <summary>面板：优先九宫格，退回圆角纯色。</summary>
        public static Image Panel(Transform parent, float x, float y, float w, float h,
            Color bg, Color border, int radius = 16, int borderWidth = 2, string key = null)
        {
            if (key == null) key = IsGold(border) ? "panel-gold" : "panel";
            var img = NineSlice(parent, key, x, y, w, h, null);
            if (img != null) return img;

            var rt = Rect(parent, "Panel", x, y, w, h);
            var fallback = rt.gameObject.AddComponent<Image>();
            fallback.sprite = RoundSprite(radius, borderWidth > 0 ? border : Color.clear, borderWidth);
            fallback.type = Image.Type.Sliced;
            fallback.color = bg;
            fallback.raycastTarget = false;
            return fallback;
        }

        public static bool IsGold(Color c) => c.r > 0.78f && c.g > 0.55f && c.b < 0.58f;

        /// <summary>
        /// 纯色圆角面板（对应 Godot 的 StyleBoxFlat 版本 sb()）。
        /// 分节卡片、词条池、统计格这类小控件必须用它：九宫格素材的高光
        /// 在 100–250px 高度上会把内容区压成一片亮色（见 AGENTS.md 的视觉约定）。
        /// </summary>
        public static Image FlatPanel(Transform parent, float x, float y, float w, float h,
            Color bg, Color border, int radius = 14, int borderWidth = 1)
        {
            var rt = Rect(parent, "FlatPanel", x, y, w, h);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = RoundSprite(radius, border, borderWidth);
            img.type = Image.Type.Sliced;
            img.color = bg;
            img.raycastTarget = false;
            return img;
        }

        // ---------------------------------------------------------------- 圆角精灵缓存
        private static readonly System.Collections.Generic.Dictionary<int, Sprite> RoundCache =
            new System.Collections.Generic.Dictionary<int, Sprite>();

        /// <summary>
        /// 生成圆角矩形的九宫格精灵（只用于「没有对应素材」的小控件）。
        /// 缓存按半径区分，避免每帧创建纹理。
        /// </summary>
        public static Sprite RoundSprite(int radius, Color border, int borderWidth)
        {
            int key = radius * 1000 + borderWidth * 10 + (border.a > 0f ? 1 : 0);
            if (RoundCache.TryGetValue(key, out var cached) && cached != null) return cached;

            radius = Mathf.Clamp(radius, 2, 128);
            int size = radius * 2 + 4;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = "st-round-" + key,
            };
            var pixels = new Color32[size * size];
            var clear = new Color32(0, 0, 0, 0);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float cx = Mathf.Clamp(x + 0.5f, radius, size - radius);
                    float cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
                    float dx = x + 0.5f - cx;
                    float dy = y + 0.5f - cy;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    pixels[y * size + x] = d <= radius ? new Color32(255, 255, 255, 255) : clear;
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, false);

            var sprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f,
                0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
            sprite.name = "st-round-" + key;
            RoundCache[key] = sprite;
            return sprite;
        }

        // ---------------------------------------------------------------- 按钮
        /// <summary>
        /// 圆形按钮（对应 SLUi._round_button）：优先用 frame-round 整图，缺图退回圆角纯色。
        /// </summary>
        public static Button RoundButton(Transform parent, Vector2 center, float radius, Color accent,
            string iconKey = "", string glyph = "", int glyphSize = 32)
        {
            float d = radius * 2f;
            var rt = Rect(parent, "RoundButton", center.x - radius, center.y - radius, d, d);
            var btn = rt.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.ColorTint;

            // 圆形底：优先用 frame-round 整图（圆形不能用九宫格，整图拉伸即可）。
            // 注意只能在根节点挂一个 Image，多挂一层会露出没有贴图的白方块。
            var img = rt.gameObject.AddComponent<Image>();
            var frame = STAssets.Ui("frame-round");
            if (frame != null)
            {
                img.sprite = frame;
                img.type = Image.Type.Simple;
                img.color = Color.white;
            }
            else
            {
                img.sprite = RoundSprite(999, accent, 3);
                img.type = Image.Type.Sliced;
                img.color = new Color(0.05f, 0.04f, 0.12f, 0.92f);
            }
            btn.targetGraphic = img;

            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.12f, 1.16f, 1.2f, 1f);
            colors.pressedColor = new Color(0.8f, 0.82f, 0.88f, 1f);
            colors.selectedColor = colors.normalColor;
            colors.disabledColor = new Color(0.55f, 0.55f, 0.6f, 0.8f);
            colors.fadeDuration = 0.08f;
            btn.colors = colors;

            if (!string.IsNullOrEmpty(iconKey))
            {
                var icon = STAssets.Ui("icon-" + iconKey);
                if (icon != null)
                {
                    var size = radius * 0.96f;
                    Picture(rt, icon, radius - size * 0.5f, radius - size * 0.5f, size, size);
                }
            }

            if (!string.IsNullOrEmpty(glyph))
            {
                var l = LabelCentered(rt, glyph, glyphSize, Color.white, true);
                Stretch(l.rectTransform);
            }
            return btn;
        }

        /// <summary>大按钮（对应 SLUi.big_button）：底图 + 主标题 + 副标题。</summary>
        public static Button BigButton(Transform parent, string title, string sub, Color accent,
            float x, float y, float w, float h)
        {
            var rt = Rect(parent, "BigButton", x, y, w, h);
            var btn = rt.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.ColorTint;

            var bg = rt.gameObject.AddComponent<Image>();
            var key = IsGold(accent) ? "button-gold" : "button-primary";
            var sprite = STAssets.Ui(key);
            if (sprite != null)
            {
                bg.sprite = sprite;
                bg.type = Image.Type.Sliced;
                bg.color = Color.white;
            }
            else
            {
                bg.sprite = RoundSprite(14, accent, 2);
                bg.type = Image.Type.Sliced;
                bg.color = new Color(0.035f, 0.052f, 0.095f, 1f);
            }
            btn.targetGraphic = bg;

            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.1f, 1.14f, 1.2f, 1f);
            colors.pressedColor = new Color(0.78f, 0.8f, 0.88f, 1f);
            colors.selectedColor = colors.normalColor;
            colors.disabledColor = new Color(0.5f, 0.5f, 0.58f, 1f);
            colors.fadeDuration = 0.08f;
            btn.colors = colors;

            float titleY = sub.Length > 0 ? h * 0.5f - 46f : h * 0.5f - 26f;
            var t = Label(rt, title, 40, STColors.White, true);
            SetRect(t.rectTransform, 40f, titleY, w - 80f, 54f);

            if (sub.Length > 0)
            {
                var s = Label(rt, sub, 18, STColors.Hex("A79CD6"));
                SetRect(s.rectTransform, 40f, h * 0.5f + 6f, w - 80f, 26f);
            }
            return btn;
        }

        /// <summary>小工具按钮（返回，取消等）。</summary>
        public static Button SmallButton(Transform parent, string text, float x, float y, float w, float h,
            Color accent, Color textColor)
        {
            var rt = Rect(parent, "SmallButton", x, y, w, h);
            var btn = rt.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.ColorTint;

            var bg = rt.gameObject.AddComponent<Image>();
            bg.sprite = RoundSprite(12, accent, 2);
            bg.type = Image.Type.Sliced;
            bg.color = new Color(0.1f, 0.08f, 0.22f, 0.95f);
            btn.targetGraphic = bg;

            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.15f, 1.18f, 1.25f, 1f);
            colors.pressedColor = new Color(0.75f, 0.78f, 0.85f, 1f);
            colors.selectedColor = colors.normalColor;
            colors.disabledColor = new Color(0.55f, 0.55f, 0.6f, 1f);
            btn.colors = colors;

            var l = LabelCentered(rt, text, 22, textColor, true);
            Stretch(l.rectTransform);
            return btn;
        }

        // ---------------------------------------------------------------- 胶囊 / 进度条
        public static Image Chip(Transform parent, string text, float x, float y, Color accent, int fontSize = 16)
        {
            var bg = Solid(parent, new Color(0.18f, 0.14f, 0.36f, 0.85f), x, y, 10f, 32f);
            bg.sprite = RoundSprite(999, accent, 1);
            bg.type = Image.Type.Sliced;

            var l = LabelCentered(bg.rectTransform, text, fontSize, STColors.Hex("D8CDFF"));
            Stretch(l.rectTransform);
            return bg;
        }

        /// <summary>按文字宽度自适应胶囊宽度。返回胶囊 Image。</summary>
        public static Image AutoChip(Transform parent, string text, Color accent, int fontSize = 16, float height = 32f)
        {
            var probe = Label(null, text, fontSize, Color.white);
            float w = probe.preferredWidth + 26f;
            UnityEngine.Object.DestroyImmediate(probe.gameObject);
            return Chip(parent, text, 0f, 0f, accent, fontSize) is Image img
                ? ResizeChip(img, w, height)
                : null;
        }

        private static Image ResizeChip(Image img, float w, float h)
        {
            var rt = img.rectTransform;
            rt.sizeDelta = new Vector2(w, h);
            return img;
        }

        public static Image Gradient(Transform parent, float x, float y, float w, float h, Color top, Color bottom)
        {
            var tex = new Texture2D(1, 2, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = "st-grad",
            };
            tex.SetPixel(0, 0, bottom);
            tex.SetPixel(0, 1, top);
            tex.Apply();
            var sprite = Sprite.Create(tex, new Rect(0f, 0f, 1f, 2f), new Vector2(0.5f, 0.5f));
            var img = Picture(parent, sprite, x, y, w, h);
            img.preserveAspect = false;
            return img;
        }

        // ---------------------------------------------------------------- 便捷
        public static void SetActive(GameObject go, bool active)
        {
            if (go != null && go.activeSelf != active) go.SetActive(active);
        }

        public static void ClearChildren(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--)
            {
                var c = t.GetChild(i).gameObject;
                if (Application.isPlaying) UnityEngine.Object.Destroy(c);
                else UnityEngine.Object.DestroyImmediate(c);
            }
        }

        public static Color Rgb(string hex) => STColors.Hex(hex);
    }
}
