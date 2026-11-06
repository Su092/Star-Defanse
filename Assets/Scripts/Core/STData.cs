// 星轨防线 · 静态数据（对应 Godot scripts/data.gd）
//
// 数据唯一来源是 Resources/Data/sldata.json：由 Godot 侧
// `tools/dump_sldata.gd` 从 scripts/data.gd 导出，改数值后需要重新导出。
// 本文件只做反序列化与纯查询，不含任何运行状态。

using System;
using System.Collections.Generic;
using UnityEngine;

namespace StarLane
{
    /// <summary>键值型加成（JSON 里的 {k,v} 记录）。键名统一为 camelCase。</summary>
    [Serializable]
    public class ModEntry
    {
        public string k;
        public float v;
    }

    [Serializable]
    public class Point2
    {
        public float x;
        public float y;

        public Vector2 ToVector2() => new Vector2(x, y);
    }

    [Serializable]
    public class Rect2
    {
        public float x;
        public float y;
        public float w;
        public float h;
    }

    [Serializable]
    public class Vec3
    {
        public float x;
        public float y;
        public float z;

        public Vector3 ToVector3() => new Vector3(x, y, z);
    }

    /// <summary>全局调参表（对应 SLData.TUNING）。</summary>
    [Serializable]
    public class TuningData
    {
        public float coreHp;
        public float startGold;
        public float summonBase;
        public float summonGrowth;
        public float upgradeBase;
        public float upgradeGrowth;
        public float firstWaveDelay;
        public float waveInterval;
        public int waveBonusBase;
        public int waveBonusPerWave;
        public int initialUnlocked;
        public int summonsPerUnlock;
        public int guardsPerTower;
        public float guardGap;
        public int maxTowerLevel;
        public float levelDmg;
        public float levelRate;
        public float levelRange;
        public float boardTop;
        public float boardBottom;
        public float heroDmg;
        public float heroRate;
        public float heroRange;
        public float heroSpeed;
    }

    /// <summary>职业（对应 SLData.CLASSES）。</summary>
    [Serializable]
    public class ClassData
    {
        public string id;
        public string name;
        public string en;
        public string role;
        public string desc;
        public string color;
        public string color2;
        public float dmg;
        public float rate;
        public float range;
        public float splash;
        public float turn;
        public float guardHp;
        public float guardDps;
        public float respawn;
        public int blockCap;

        [NonSerialized] private Color? _colorCache;

        public Color Accent
        {
            get
            {
                if (_colorCache == null) _colorCache = STColors.Parse(color);
                return _colorCache.Value;
            }
        }

        public bool HasSplash => splash > 0f;
    }

    /// <summary>干员（对应 SLData.OPERATIVES）。</summary>
    [Serializable]
    public class OperativeData
    {
        public string id;
        public string name;
        public string en;
        public string classId;
        public string title;
        public string portrait;
        public string color;
        public string trait;
        public List<ModEntry> mods = new List<ModEntry>();
        public string[] affixes = Array.Empty<string>();

        [NonSerialized] private Color? _colorCache;

        public Color Accent
        {
            get
            {
                if (_colorCache == null) _colorCache = STColors.Parse(color);
                return _colorCache.Value;
            }
        }

        public float GetMod(string key)
        {
            for (int i = 0; i < mods.Count; i++)
            {
                if (mods[i].k == key) return mods[i].v;
            }
            return 0f;
        }
    }

    /// <summary>词条（对应 SLData.AFFIXES / FILLER_AFFIX）。</summary>
    [Serializable]
    public class AffixData
    {
        public string id;
        public string name;
        public string family;
        public string icon;
        public string desc;
        public List<ModEntry> mods = new List<ModEntry>();
        public bool repeatable;

        public float GetMod(string key)
        {
            for (int i = 0; i < mods.Count; i++)
            {
                if (mods[i].k == key) return mods[i].v;
            }
            return 0f;
        }
    }

    /// <summary>敌人模板（对应 SLData.ENEMIES）。</summary>
    [Serializable]
    public class EnemyData
    {
        public string id;
        public string name;
        public float hp;
        public float speed;
        public float armor;
        public int reward;
        public int leak;
        public float radius;
        public float atk;
        public string color;
        public bool elite;
        public bool boss;
        public float slowResist;

        [NonSerialized] private Color? _colorCache;

        public Color Accent
        {
            get
            {
                if (_colorCache == null) _colorCache = STColors.Parse(color);
                return _colorCache.Value;
            }
        }
    }

    [Serializable]
    public class BatchData
    {
        public string type;
        public int count;
        public float interval;
        public float delay;
    }

    /// <summary>波次（对应 SLData.WAVES）。</summary>
    [Serializable]
    public class WaveData
    {
        public string warning;
        public List<BatchData> batches = new List<BatchData>();
    }

    /// <summary>关卡（对应 SLData.LEVELS）。</summary>
    [Serializable]
    public class LevelData
    {
        public string id;
        public string name;
        public string en;
        public int index;
        public List<ModEntry> mul = new List<ModEntry>();
        public List<Point2> path = new List<Point2>();
        public List<Point2> platforms = new List<Point2>();
        public string map;
        public Rect2 mapRect;
        public Vec3 camera;
        public string hint;
        public string art;

        public float Mul(string key)
        {
            for (int i = 0; i < mul.Count; i++)
            {
                if (mul[i].k == key) return mul[i].v;
            }
            return 1f;
        }
    }

    /// <summary>sldata.json 根对象。</summary>
    [Serializable]
    public class SLDataRoot
    {
        public TuningData tuning;
        public List<ClassData> classes = new List<ClassData>();
        public List<OperativeData> operatives = new List<OperativeData>();
        public List<AffixData> affixes = new List<AffixData>();
        public List<EnemyData> enemies = new List<EnemyData>();
        public List<WaveData> waves = new List<WaveData>();
        public List<LevelData> levels = new List<LevelData>();
        public AffixData fillerAffix;
    }

    /// <summary>静态数据访问入口（对应 class_name SLData）。</summary>
    public static class STData
    {
        public const string ResourcePath = "Data/sldata";

        public const string DefaultFormationCsv = "ran,vega,grom,ella";

        private static SLDataRoot _root;
        private static readonly Dictionary<string, ClassData> ClassMap = new Dictionary<string, ClassData>();
        private static readonly Dictionary<string, OperativeData> OperativeMap = new Dictionary<string, OperativeData>();
        private static readonly Dictionary<string, AffixData> AffixMap = new Dictionary<string, AffixData>();
        private static readonly Dictionary<string, EnemyData> EnemyMap = new Dictionary<string, EnemyData>();

        public static SLDataRoot Root
        {
            get
            {
                if (_root == null) Load();
                return _root;
            }
        }

        public static TuningData T => Root.tuning;
        public static IReadOnlyList<ClassData> Classes => Root.classes;
        public static IReadOnlyList<OperativeData> Operatives => Root.operatives;
        public static IReadOnlyList<AffixData> Affixes => Root.affixes;
        public static IReadOnlyList<EnemyData> Enemies => Root.enemies;
        public static IReadOnlyList<WaveData> Waves => Root.waves;
        public static IReadOnlyList<LevelData> Levels => Root.levels;
        public static AffixData FillerAffix => Root.fillerAffix;

        public static void Load()
        {
            var text = Resources.Load<TextAsset>(ResourcePath);
            if (text == null)
            {
                Debug.LogError("[STData] 缺少 Resources/" + ResourcePath + ".json，请先运行 tools/dump_sldata.gd");
                _root = new SLDataRoot { tuning = new TuningData(), fillerAffix = new AffixData() };
                return;
            }

            _root = JsonUtility.FromJson<SLDataRoot>(text.text);
            RebuildMaps();
        }

        /// <summary>测试里可以用内存数据直接替换。</summary>
        public static void Override(SLDataRoot root)
        {
            _root = root;
            RebuildMaps();
        }

        private static void RebuildMaps()
        {
            ClassMap.Clear();
            OperativeMap.Clear();
            AffixMap.Clear();
            EnemyMap.Clear();

            foreach (var c in _root.classes) ClassMap[c.id] = c;
            foreach (var o in _root.operatives) OperativeMap[o.id] = o;
            foreach (var a in _root.affixes) AffixMap[a.id] = a;
            foreach (var e in _root.enemies) EnemyMap[e.id] = e;
        }

        // ---------------------------------------------------------------- 查询
        public static ClassData GetClass(string id)
        {
            if (id != null && ClassMap.TryGetValue(id, out var c)) return c;
            return null;
        }

        public static OperativeData GetOperative(string id)
        {
            if (id != null && OperativeMap.TryGetValue(id, out var o)) return o;
            return null;
        }

        public static AffixData GetAffix(string id)
        {
            if (id == null) return null;
            if (id == FillerAffix?.id) return FillerAffix;
            return AffixMap.TryGetValue(id, out var a) ? a : null;
        }

        public static EnemyData GetEnemy(string id)
        {
            if (id != null && EnemyMap.TryGetValue(id, out var e)) return e;
            return null;
        }

        public static LevelData GetLevel(int index)
        {
            var levels = Root.levels;
            if (index < 0 || index >= levels.Count) return levels[0];
            return levels[index];
        }

        /// <summary>编队规范化：去重、去非法、上限 4 名，空则回退默认编队。</summary>
        public static List<string> NormalizeFormation(IEnumerable<string> ids)
        {
            var valid = new List<string>();
            if (ids != null)
            {
                foreach (var raw in ids)
                {
                    if (valid.Count >= 4) break;
                    if (string.IsNullOrEmpty(raw)) continue;
                    if (GetOperative(raw) == null) continue;
                    if (valid.Contains(raw)) continue;
                    valid.Add(raw);
                }
            }

            if (valid.Count == 0)
            {
                valid.AddRange(DefaultFormationCsv.Split(','));
            }
            return valid;
        }

        /// <summary>编队词条池：上阵干员的签名词条 + 保底的精简流程。</summary>
        public static List<string> AffixPool(IEnumerable<string> formation)
        {
            var pool = new List<string>();
            foreach (var id in formation)
            {
                var op = GetOperative(id);
                if (op == null) continue;
                foreach (var a in op.affixes)
                {
                    if (!pool.Contains(a)) pool.Add(a);
                }
            }
            if (!pool.Contains("u_cheap") && GetAffix("u_cheap") != null) pool.Add("u_cheap");
            return pool;
        }

        public static Color ClassColor(string classId)
        {
            var c = GetClass(classId);
            return c != null ? c.Accent : Color.white;
        }

        /// <summary>词条颜色：通用=金，其余跟随所属职业。</summary>
        public static Color AffinityColor(string affixId)
        {
            if (affixId == FillerAffix?.id) return STColors.Gold;
            var a = GetAffix(affixId);
            if (a == null) return STColors.Gold;
            if (a.family == "universal") return STColors.Gold;
            return ClassColor(a.family);
        }

        public static string FamilyName(string family)
        {
            switch (family)
            {
                case "assault": return "突击";
                case "sniper": return "狙击";
                case "siege": return "重炮";
                case "intercept": return "拦截";
                default: return "通用";
            }
        }
    }

    /// <summary>统一调色板（对应 SLArt 的颜色常量）。</summary>
    public static class STColors
    {
        public static readonly Color Ink = Hex("07040F");
        public static readonly Color Ink2 = Hex("140E28");
        public static readonly Color Panel = Hex("151130");
        public static readonly Color Line = Hex("3B2F6E");
        public static readonly Color Cyan = Hex("4FF3FF");
        public static readonly Color CyanDim = Hex("1B7C9A");
        public static readonly Color Magenta = Hex("FF3DAE");
        public static readonly Color Gold = Hex("FFD166");
        public static readonly Color Violet = Hex("A78BFF");
        public static readonly Color Green = Hex("5BE39A");
        public static readonly Color Orange = Hex("FFAE3A");
        public static readonly Color Red = Hex("FF5B6E");
        public static readonly Color White = Hex("EAF6FF");
        public static readonly Color Dim = Hex("9F93D8");
        public static readonly Color Dim2 = Hex("8B80BE");

        public static Color Hex(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return Color.white;
            if (hex[0] != '#') hex = "#" + hex;
            return ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.white;
        }

        public static Color Parse(string hex) => Hex(hex);

        public static Color WithAlpha(this Color c, float a) => new Color(c.r, c.g, c.b, a);
    }
}
