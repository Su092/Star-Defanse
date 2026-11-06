// 星轨防线 · 战场世界空间特效（对应 Godot main.gd 的 fx 数组 + art.gd 的 draw_fx）
//
// Godot 版把特效放在一个 fx 列表里由 2D 绘制；Unity 侧因为战场是 3D，
// 这里用「始终朝向相机的公告板」来还原同样的观感：
//   ring       扩散光环（召唤 / 合成 / 解锁 / 漏怪）
//   spark      命中火花（短线放射）
//   explosion  爆炸实心圆
//   text       世界空间飘字（伤害 / 金币 / 波次奖励）
// 全部走 StarLane/Unlit，使用程序化生成的环形 / 圆形 / 文字贴图。

using System.Collections.Generic;
using UnityEngine;

namespace StarLane
{
    public class STFxSystem : MonoBehaviour
    {
        private class Fx
        {
            public GameObject Go;
            public Transform Tr;
            public MeshRenderer Mr;
            public float Life;
            public float MaxLife;
            public float R0;
            public float R1;
            public Color Color;
            public Vector3 Drift;
            public bool IsText;              // true = UI 层飘字，位置每帧按相机投影
            public RectTransform Rt;
            public UnityEngine.UI.Image Img;
            public Vector3 WorldPos;         // 飘字的世界锚点（用于投影）
            public float RisePx;             // 屏幕上浮累计像素
        }

        private readonly List<Fx> _items = new List<Fx>();
        private Camera _camera;
        private Transform _root;
        private static Texture2D _ringTex;
        private static Texture2D _discTex;
        private static Texture2D _sparkTex;
        private static Material _baseMat;

        // 飘字专用 Overlay Canvas：世界里的 3D Quad 会与血条/单位抢透明排序，
        // 实测会被血条底板整条压住。UI 层永远画在 3D 之上，最可靠。
        private Canvas _textCanvas;
        private RectTransform _textLayer;

        public void Init(Camera cam)
        {
            _camera = cam;
            _root = new GameObject("WorldFx").transform;
            _root.SetParent(transform, false);

            _ringTex ??= BuildRingTexture();
            _discTex ??= BuildDiscTexture();
            _sparkTex ??= BuildSparkTexture();

            if (_baseMat == null)
            {
                _baseMat = new Material(Shader.Find("StarLane/Unlit")) { name = "ST-Fx" };
                _baseMat.SetFloat("_ZTest", 4f);
                _baseMat.SetFloat("_ZWrite", 0f);
                _baseMat.SetFloat("_Cull", 0f);
            }

            BuildTextCanvas();
        }

        private void BuildTextCanvas()
        {
            if (_textCanvas != null) return;
            var go = new GameObject("FxTextCanvas");
            go.transform.SetParent(transform, false);
            _textCanvas = go.AddComponent<Canvas>();
            _textCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _textCanvas.sortingOrder = 200;      // 高于 HUD(0)，但仍低于横幅/弹窗
            var scaler = go.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 1f;
            _textLayer = (RectTransform)go.transform;
        }

        // ---------------------------------------------------------------- 贴图
        private static Texture2D BuildRingTexture()
        {
            const int n = 128;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "st-fx-ring",
            };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f;
                    float dy = (y + 0.5f) / n * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    // 环形：0.90–1.0 之间亮，向内快速衰减
                    float a = Mathf.Clamp01(1f - Mathf.Abs(d - 0.94f) / 0.06f);
                    a = Mathf.Pow(a, 1.6f);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return tex;
        }

        private static Texture2D BuildDiscTexture()
        {
            const int n = 128;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "st-fx-disc",
            };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f;
                    float dy = (y + 0.5f) / n * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - d);
                    a = Mathf.Pow(a, 1.4f);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return tex;
        }

        private static Texture2D BuildSparkTexture()
        {
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "st-fx-spark",
            };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f;
                    float dy = (y + 0.5f) / n * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - d / 0.55f);
                    a *= a;
                    a *= 0.6f + 0.4f * Mathf.Cos(dx * 14f) * Mathf.Cos(dy * 14f);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(Mathf.Max(0f, a) * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return tex;
        }

        // ---------------------------------------------------------------- 生成
        private Fx Spawn(Texture2D tex, Vector3 pos, Color color, float life, float r0, float r1, Vector3 drift)
        {
            var go = new GameObject("Fx");
            go.transform.SetParent(_root, false);
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = STMesh.Quad(Vector2.one);
            var mr = go.AddComponent<MeshRenderer>();
            var mat = new Material(_baseMat) { mainTexture = tex, color = color };
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            go.transform.position = pos;

            var fx = new Fx
            {
                Go = go, Tr = go.transform, Mr = mr,
                Life = life, MaxLife = life, R0 = r0, R1 = r1, Color = color, Drift = drift,
            };
            Apply(fx, 0f);
            _items.Add(fx);
            return fx;
        }

        /// <summary>扩散光环。</summary>
        public void Ring(Vector3 pos, Color color, float radius, float life = 0.6f)
        {
            Spawn(_ringTex, pos, color, life, radius * 0.25f, radius, Vector3.zero);
        }

        /// <summary>命中火花。</summary>
        public void Spark(Vector3 pos, Color color, float size = 0.8f, float life = 0.35f)
        {
            Spawn(_sparkTex, pos, color, life, size, size * 1.6f, Vector3.zero);
        }

        /// <summary>爆炸实心圆。</summary>
        public void Explosion(Vector3 pos, Color color, float radius, float life = 0.4f)
        {
            Spawn(_discTex, pos, color, life, radius * 0.3f, radius, Vector3.zero);
        }

        /// <summary>世界空间飘字（用 Sprite 贴图实现，避免依赖 TextMeshPro）。</summary>
        public void FloatText(Vector3 pos, string text, Color color, int size = 26, float life = 0.9f)
        {
            var tex = STTextTexture.Get(text, size, color);
            if (tex == null)
            {
                Debug.LogWarning("[STFxSystem] FloatText 贴图烘焙失败: " + text);
                return;
            }
            SpawnTextQuad(pos, tex, life, 1f);
        }

        /// <summary>金币飘字：金币图标 + 数字（尺寸与普通飘字同一档）。</summary>
        public void FloatCoin(Vector3 pos, int amount, Color color, int size = 26, float life = 0.9f)
        {
            var tex = STTextTexture.GetCoinGain(amount, size, color);
            if (tex != null) SpawnTextQuad(pos, tex, life, 1f);
            else FloatText(pos, "+" + amount, color, size, life);
        }

        private void SpawnTextQuad(Vector3 pos, Texture2D tex, float life, float scaleMul)
        {
            BuildTextCanvas();

            var go = new GameObject("FxText", typeof(RectTransform), typeof(CanvasRenderer),
                typeof(UnityEngine.UI.Image));
            go.transform.SetParent(_textLayer, false);
            var img = go.GetComponent<UnityEngine.UI.Image>();
            img.sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height),
                new Vector2(0.5f, 0.5f), 100f);
            img.color = Color.white;
            img.raycastTarget = false;
            img.preserveAspect = true;

            // 尺寸：贴图像素 → 设计像素（1:1），再乘缩放系数。
            // 原来 3D Quad 用的是 tex.width/19.6*2.2 ≈ 10.8 世界单位（约 100px，占屏 1/5），
            // 用户要求缩到 30%，因此这里直接按 0.30 折算成设计像素。
            float k = 0.30f * scaleMul;
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(tex.width * k, tex.height * k);

            var fx = new Fx
            {
                Go = go, Tr = go.transform, Rt = rt, Img = img,
                Life = life, MaxLife = life, R0 = 1.25f, R1 = 1f, Color = Color.white,
                IsText = true, WorldPos = pos, RisePx = 0f,
            };
            UpdateTextPosition(fx);
            _items.Add(fx);
        }

        /// <summary>把世界坐标投影到 Overlay Canvas 的 anchoredPosition。</summary>
        private void UpdateTextPosition(Fx fx)
        {
            if (fx.Rt == null) return;
            if (_camera == null) { fx.Rt.anchoredPosition = new Vector2(-9999f, -9999f); return; }

            var sp = _camera.WorldToScreenPoint(fx.WorldPos);
            if (sp.z <= 0f)
            {
                fx.Rt.anchoredPosition = new Vector2(-9999f, -9999f);   // 相机背后
                return;
            }
            // 屏幕像素 → 设计像素（CanvasScaler 以 1080×1920 为参考，match=1 按高度缩放）
            float s = 1920f / Mathf.Max(1, Screen.height);
            var anchored = new Vector2((sp.x - Screen.width * 0.5f) * s,
                (sp.y - Screen.height * 0.5f) * s + fx.RisePx);
            fx.Rt.anchoredPosition = anchored;
        }

        // ---------------------------------------------------------------- 每帧
        private void Apply(Fx fx, float k)
        {
            if (fx.IsText)
            {
                // UI 飘字：只做缩放、上浮与淡出
                float sc = Mathf.Lerp(fx.R0, fx.R1, k);
                if (fx.Rt != null) fx.Rt.localScale = new Vector3(sc, sc, 1f);

                fx.RisePx += 90f * Time.deltaTime;    // 每秒上浮 90 设计像素
                UpdateTextPosition(fx);

                if (fx.Img != null)
                {
                    var c = fx.Color;
                    float a = k < 0.06f ? Mathf.Lerp(0.55f, 1f, k / 0.06f)
                        : (k > 0.55f ? Mathf.Lerp(1f, 0f, (k - 0.55f) / 0.45f) : 1f);
                    fx.Img.color = new Color(c.r, c.g, c.b, c.a * a);
                }
                return;
            }

            float r = Mathf.Lerp(fx.R0, fx.R1, k);
            fx.Tr.localScale = new Vector3(r, r, 1f);
            fx.Tr.position += fx.Drift * Time.deltaTime;

            var mat = fx.Mr.sharedMaterial;
            if (mat != null)
            {
                var c = fx.Color;
                // 淡入压到 6%：原来 15% 意味着 0.9s 寿命里有 135ms 几乎全透明，
                // 击杀瞬间截图/录像经常正好落在这一段，看着像「没有飘字」。
                float a = k < 0.06f ? Mathf.Lerp(0.55f, 1f, k / 0.06f)
                    : (k > 0.55f ? Mathf.Lerp(1f, 0f, (k - 0.55f) / 0.45f) : 1f);
                mat.color = new Color(c.r, c.g, c.b, c.a * a);
            }

            if (_camera != null)
            {
                // billboard：让贴片正对相机。
                // 直接把 rotation 设成相机 rotation 会让 Quad(+Z 法线) 背对相机；
                // 这里用 LookAt 让 +Z 指向相机方向的反向，正面朝向镜头。
                var dir = fx.Tr.position - _camera.transform.position;
                if (dir.sqrMagnitude > 1e-6f)
                    fx.Tr.rotation = Quaternion.LookRotation(dir.normalized, _camera.transform.up);
            }
        }

        private void LateUpdate()
        {
            for (int i = _items.Count - 1; i >= 0; i--)
            {
                var fx = _items[i];
                if (fx.Go == null)
                {
                    _items.RemoveAt(i);
                    continue;
                }
                fx.Life -= Time.deltaTime;
                if (fx.Life <= 0f)
                {
                    Destroy(fx.Go);
                    _items.RemoveAt(i);
                    continue;
                }
                Apply(fx, 1f - fx.Life / fx.MaxLife);
            }
        }

        public void Clear()
        {
            foreach (var fx in _items)
            {
                if (fx.Go != null) Destroy(fx.Go);
            }
            _items.Clear();
        }
    }

    /// <summary>把一小段文字烘焙成贴图（战场飘字用；不依赖 TextMeshPro 字体资源）。</summary>
    public static class STTextTexture
    {
        private static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();

        /// <summary>
        /// 金币飘字专用：左侧金币图标 + 右侧数字，合成一张贴图。
        ///
        /// 和 Get 用同一条 RT 烘焙管线：把 Image(金币) 与 Text(数字) 摆进同一个
        /// 临时 Canvas 再一次性渲染。这样不需要读贴图（导入资源默认不可读），
        /// 也保证图标与数字的垂直居中完全由布局决定。
        /// </summary>
        public static Texture2D GetCoinGain(int amount, int size, Color color)
        {
            string key = "coin|" + amount + "|" + size + "|" + ColorUtility.ToHtmlStringRGBA(color);
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;

            var font = STAssets.FontBold;
            string text = "+" + amount;

            // 量数字
            var probe = new GameObject("probe", typeof(CanvasRenderer), typeof(UnityEngine.UI.Text));
            var pt = probe.GetComponent<UnityEngine.UI.Text>();
            pt.font = font;
            pt.fontSize = size;
            pt.text = text;
            pt.horizontalOverflow = HorizontalWrapMode.Overflow;
            pt.verticalOverflow = VerticalWrapMode.Overflow;
            var gen = pt.cachedTextGeneratorForLayout;
            gen.Populate(text, pt.GetGenerationSettings(new Vector2(1200f, 200f)));
            int numW = Mathf.CeilToInt(gen.GetPreferredWidth(text, pt.GetGenerationSettings(Vector2.zero)) / pt.pixelsPerUnit);
            int numH = Mathf.CeilToInt(gen.GetPreferredHeight(text, pt.GetGenerationSettings(Vector2.zero)) / pt.pixelsPerUnit);
            Object.DestroyImmediate(probe);

            int iconSize = Mathf.RoundToInt(numH * 1.0f);
            int gap = Mathf.Max(3, size / 5);
            int pad = 6;
            var coinPre = STAssets.Ui("icon-coin");
            int contentW = numW + (coinPre != null ? iconSize + gap : 0);
            int w = Mathf.Clamp(contentW + pad * 2, 48, 512);
            int h = Mathf.Clamp(Mathf.Max(numH, iconSize) + pad * 2, 32, 256);

            var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32);
            rt.filterMode = FilterMode.Bilinear;

            var camGo = new GameObject("coinCam");
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = h * 0.5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.cullingMask = ~0;
            cam.nearClipPlane = -10f;
            cam.farClipPlane = 100f;
            cam.enabled = false;
            cam.targetTexture = rt;
            camGo.transform.position = new Vector3(0f, 0f, -50f);
            camGo.transform.rotation = Quaternion.identity;

            var canvasGo = new GameObject("coinCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = cam;
            canvas.sortingOrder = 0;
            var crt = (RectTransform)canvasGo.transform;
            crt.sizeDelta = new Vector2(w, h);

            var coin = STAssets.Ui("icon-coin");

            // 用 anchoredPosition 配合中心锚点做绝对布局：
            // 之前用 (0,0.5) 锚点 + pivot 组合容易把元素推到画布外，
            // 这里统一改成中心锚点，坐标从画布中心量起。
            int iconCols = coin != null ? iconSize + gap : 0;
            float leftEdge = -(numW + iconCols) * 0.5f;

            // 数字（右侧，垂直居中）
            var textGo = new GameObject("t", typeof(CanvasRenderer), typeof(UnityEngine.UI.Text));
            textGo.transform.SetParent(canvasGo.transform, false);
            var t = textGo.GetComponent<UnityEngine.UI.Text>();
            t.font = font;
            t.fontSize = size;
            t.color = Color.white;
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.text = text;
            t.raycastTarget = false;
            var trt = (RectTransform)t.transform;
            trt.anchorMin = trt.anchorMax = trt.pivot = new Vector2(0.5f, 0.5f);
            trt.sizeDelta = new Vector2(numW + 4, numH);
            trt.anchoredPosition = new Vector2(leftEdge + iconCols + (numW + 4) * 0.5f, 0f);

            // 金币图标（左端）
            if (coin != null)
            {
                var imgGo = new GameObject("coin", typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
                imgGo.transform.SetParent(canvasGo.transform, false);
                var img = imgGo.GetComponent<UnityEngine.UI.Image>();
                img.sprite = coin;
                img.color = Color.white;
                img.raycastTarget = false;
                img.preserveAspect = true;
                var irt = (RectTransform)imgGo.transform;
                irt.anchorMin = irt.anchorMax = irt.pivot = new Vector2(0.5f, 0.5f);
                irt.sizeDelta = new Vector2(iconSize, iconSize);
                irt.anchoredPosition = new Vector2(
                    leftEdge + iconSize * 0.5f, 0f);
            }

            cam.Render();

            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "st-coin-gain",
            };
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply(false, false);
            RenderTexture.active = prev;

            cam.targetTexture = null;
            RenderTexture.ReleaseTemporary(rt);
            Object.DestroyImmediate(canvasGo);
            Object.DestroyImmediate(camGo);

            // 染色：数字部分（白色）染成目标色；金币图标保留原色
            // （用 alpha 与白度区分：图标本身是黄+白，染成纯金会糊掉细节，
            //  所以只把接近纯白且高亮的像素当文字上色）
            var px = tex.GetPixels();
            var target = color;
            for (int i = 0; i < px.Length; i++)
            {
                var c = px[i];
                if (c.a <= 0.01f) continue;
                // 文字的烘焙色是纯白(1,1,1)；只要不是纯白就认为属于图标，保持原样
                bool isText = c.r > 0.93f && c.g > 0.93f && c.b > 0.93f;
                if (!isText) continue;
                px[i] = new Color(target.r, target.g, target.b, c.a * target.a);
            }
            tex.SetPixels(px);
            tex.Apply(false, false);

            Cache[key] = tex;
            return tex;
        }

        public static Texture2D Get(string text, int size, Color color)
        {
            string key = text + "|" + size + "|" + ColorUtility.ToHtmlStringRGBA(color);
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;

            var font = STAssets.FontBold;

            // 先量一次尺寸
            var probe = new GameObject("probe", typeof(CanvasRenderer), typeof(UnityEngine.UI.Text));
            var pt = probe.GetComponent<UnityEngine.UI.Text>();
            pt.font = font;
            pt.fontSize = size;
            pt.text = text;
            pt.horizontalOverflow = HorizontalWrapMode.Overflow;
            pt.verticalOverflow = VerticalWrapMode.Overflow;
            var gen = pt.cachedTextGeneratorForLayout;
            gen.Populate(text, pt.GetGenerationSettings(new Vector2(1200f, 200f)));
            int w = Mathf.CeilToInt(gen.GetPreferredWidth(text, pt.GetGenerationSettings(Vector2.zero)) / pt.pixelsPerUnit) + 24;
            int h = Mathf.CeilToInt(gen.GetPreferredHeight(text, pt.GetGenerationSettings(Vector2.zero)) / pt.pixelsPerUnit) + 20;
            Object.DestroyImmediate(probe);

            w = Mathf.Clamp(w, 32, 1024);
            h = Mathf.Clamp(h, 24, 256);

            var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32);
            rt.filterMode = FilterMode.Bilinear;

            var camGo = new GameObject("textCam");
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = h * 0.5f;      // 正交相机必须给尺寸，否则视野为 0
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            // 关键：cullingMask 不能是 0，否则相机什么都不渲染，烘出来的贴图永远全透明
            cam.cullingMask = ~0;
            cam.nearClipPlane = -10f;
            cam.farClipPlane = 100f;
            cam.enabled = false;
            cam.targetTexture = rt;
            camGo.transform.position = new Vector3(0f, 0f, -50f);
            camGo.transform.rotation = Quaternion.identity;

            var canvasGo = new GameObject("textCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            // WorldSpace 比 ScreenSpaceCamera 更可控：位置/尺寸都能显式给定，
            // 不受 planeDistance 与像素比例影响。
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = cam;
            canvas.sortingOrder = 0;
            var crt = (RectTransform)canvasGo.transform;
            crt.sizeDelta = new Vector2(w, h);
            crt.position = Vector3.zero;
            crt.localScale = Vector3.one;

            var textGo = new GameObject("t", typeof(CanvasRenderer), typeof(UnityEngine.UI.Text));
            textGo.transform.SetParent(canvasGo.transform, false);
            var t = textGo.GetComponent<UnityEngine.UI.Text>();
            t.font = font;
            t.fontSize = size;
            t.color = Color.white;
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.text = text;
            t.raycastTarget = false;
            var trt = (RectTransform)t.transform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;

            cam.Render();

            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "st-text",
            };
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply(false, false);
            RenderTexture.active = prev;

            cam.targetTexture = null;
            RenderTexture.ReleaseTemporary(rt);
            Object.DestroyImmediate(canvasGo);
            Object.DestroyImmediate(camGo);

            // 统一染色：把烘焙出的白字染成目标色
            var px = tex.GetPixels32();
            for (int i = 0; i < px.Length; i++)
            {
                float a = px[i].a / 255f;
                if (a <= 0f) continue;
                px[i] = new Color32((byte)(color.r * 255f), (byte)(color.g * 255f),
                    (byte)(color.b * 255f), (byte)(a * color.a * 255f));
            }
            tex.SetPixels32(px);
            tex.Apply(false, false);

            if (!Cache.ContainsKey(key)) Cache[key] = tex;
            return tex;
        }
    }
}
