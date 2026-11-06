// 星轨防线 · 资源访问（精灵 / 背景 / 界面九宫格 / 字体 / 模型 / 动画片段）
//
// 全部走 Resources 加载并做缓存：Unity 的 Resources.Load 自身有缓存，
// 但这里再包一层是为了统一命名规则（例如塔的贴图按职业 + 等级拼接）。

using System.Collections.Generic;
using UnityEngine;

namespace StarLane
{
    public static class STAssets
    {
        public const string SpriteDir = "Art/Sprites/";
        public const string UiDir = "Art/UI/";
        public const string BgDir = "Art/Backgrounds/";
        public const string ModelDir = "Art/Models/";
        public const string FontDir = "Fonts/";

        private static readonly Dictionary<string, Sprite> SpriteCache = new Dictionary<string, Sprite>();
        private static readonly Dictionary<string, Sprite> UiCache = new Dictionary<string, Sprite>();
        private static readonly Dictionary<string, Sprite> BgCache = new Dictionary<string, Sprite>();
        private static readonly Dictionary<string, Font> FontCache = new Dictionary<string, Font>();
        private static readonly Dictionary<string, GameObject> ModelCache = new Dictionary<string, GameObject>();
        private static readonly Dictionary<string, AnimationClip[]> ClipCache = new Dictionary<string, AnimationClip[]>();

        public static Font FontRegular => GetFont("DengXian-Regular");
        public static Font FontBold => GetFont("DengXian-Bold");

        private static Font GetFont(string name)
        {
            if (FontCache.TryGetValue(name, out var f) && f != null) return f;
            f = Resources.Load<Font>(FontDir + name);
            if (f == null)
            {
                // 回退到系统内置字体，至少保证文字可见
                var builtin = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                Debug.LogWarning("[STAssets] 字体缺失 " + name + "，回退内置字体");
                f = builtin;
            }
            FontCache[name] = f;
            return f;
        }

        // ---------------------------------------------------------------- 精灵
        private static Sprite LoadSprite(string dir, string name, Dictionary<string, Sprite> cache)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (cache.TryGetValue(name, out var s)) return s;
            s = Resources.Load<Sprite>(dir + name);
            cache[name] = s;
            return s;
        }

        /// <summary>战斗精灵（透明 PNG）。</summary>
        public static Sprite Sprite(string name) => LoadSprite(SpriteDir, name, SpriteCache);

        /// <summary>界面九宫格素材。</summary>
        public static Sprite Ui(string name) => LoadSprite(UiDir, name, UiCache);

        /// <summary>背景 / 立绘 / 关卡缩略图。</summary>
        public static Sprite Background(string name) => LoadSprite(BgDir, name, BgCache);

        public static Sprite TowerSprite(string classId, int level)
        {
            return Sprite("tower-" + classId + "-l" + Mathf.Clamp(level, 1, 4));
        }

        public static Sprite EnemySprite(string type) => Sprite("enemy-" + type);

        public static Sprite ThreatSprite(string kind) => Background(kind + "-threat");

        public static Sprite LevelThumb(string id) => Background("level-" + id);

        public static Sprite MapTexture(string mapName)
        {
            var s = Background(mapName);
            if (s == null) s = Background("map");
            return s;
        }

        // ---------------------------------------------------------------- 模型
        /// <summary>加载 FBX 模型 / 动画文件（返回 Resources 资源本身，调用方自行 Instantiate）。</summary>
        public static GameObject Model(string relativePath)
        {
            if (ModelCache.TryGetValue(relativePath, out var go)) return go;
            go = Resources.Load<GameObject>(ModelDir + relativePath);
            ModelCache[relativePath] = go;
            return go;
        }

        /// <summary>加载独立的动画 FBX 里的全部 AnimationClip。</summary>
        public static AnimationClip[] Clips(string relativePath)
        {
            if (ClipCache.TryGetValue(relativePath, out var clips)) return clips;
            clips = Resources.LoadAll<AnimationClip>(ModelDir + relativePath);
            ClipCache[relativePath] = clips;
            return clips;
        }

        /// <summary>按后缀匹配片段（导入后常带 "Armature|" 前缀）。</summary>
        public static AnimationClip FindClip(AnimationClip[] clips, string suffix)
        {
            if (clips == null || string.IsNullOrEmpty(suffix)) return null;
            foreach (var c in clips)
            {
                if (c == null) continue;
                if (c.name == suffix) return c;
                if (c.name.EndsWith("|" + suffix)) return c;
                if (c.name.EndsWith(suffix)) return c;
            }
            return null;
        }

        public static Texture2D ModelBaseColor(string folder)
        {
            // 每个模型目录下有一张 <folder>_basecolor.jpg（连字符命名的除外）
            var tex = Resources.Load<Texture2D>(ModelDir + folder + "/" + folder + "_basecolor");
            if (tex != null) return tex;
            return Resources.Load<Texture2D>(ModelDir + folder + "/" + folder.Replace('_', '-') + "_basecolor");
        }

        public static void ClearCache()
        {
            SpriteCache.Clear();
            UiCache.Clear();
            BgCache.Clear();
            ModelCache.Clear();
            ClipCache.Clear();
        }
    }
}
