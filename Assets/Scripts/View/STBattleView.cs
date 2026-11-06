// 星轨防线 · 3D 战场视图（Godot scripts/battle3d.gd 的移植）
//
// 职责边界：只把 RunState 的状态映射成模型与特效，绝不修改 run 状态。
//
// 与 Godot 端的关键差异（引擎层差异，已逐项处理）：
//   · 单位朝向：Godot 用 look_at(-Z)，Unity 用 Quaternion.LookRotation(+Z)；
//     模型零位朝向与 Godot 相同（+Z），因此敌人整体朝向角需要 +180° 补偿。
//   · 设计坐标系 X 取反（见 STWorld.ToWorld）。
//   · 炮塔只旋转骨骼里的 Yaw；Unity 的 transform.localRotation 直接可用，
//     无需像 Godot 那样在 AnimationPlayer 里剔除 Yaw 轨道——这里播放的是
//     独立 Anim_*.fbx 片段，Yaw 轨道在合并时按名字过滤掉。
//   · 动画用 Legacy Animation + 合并片段（Animator 需要预先烘焙 Controller，
//     而本项目的片段是运行时按后缀匹配的，用 Animation 更贴近 Godot 的实现）。

using System.Collections.Generic;
using UnityEngine;

namespace StarLane
{
    public class STBattleView : MonoBehaviour
    {
        // ---------------------------------------------------------------- 模型表（唯一接入点）
        private class ModelSpec
        {
            public string File;
            public string Anim;
            public string Folder;
            public float Scale = 1f;
            public float Y;
            public float Yaw;            // 设计坐标下的额外朝向（度）
            public float Pitch;          // 绕 X 轴的校正（度）：塔模型在 FBX 里是躺平的，需要立起来
            public string YawBone;
            public string[] Muzzles;
            public Vector3 MuzzleOffset;
            public float BarY = 1.5f;
            public float Bob;
            public string BaseColorName;
            public bool CastShadow = true;
        }

        private static readonly Dictionary<string, ModelSpec> Models = new Dictionary<string, ModelSpec>
        {
            ["tower-assault"] = new ModelSpec
            {
                Folder = "tower_assault", File = "Modle_Tower_Assault", Anim = "Anim_Tower_Assault",
                Scale = 1f, Y = 1.23f, Yaw = 0f,
                // 塔模型在 FBX 里是躺平的（炮口骨骼 y=-1.53 位于地下、网格沿 Z 最长），
                // 绕 X 轴 -90° 把它立起来，炮管朝 +Y 方向抬正。
                Pitch = -90f,
                YawBone = "Yaw", Muzzles = new[] { "Muzzle_R", "Muzzle_L" },
                BaseColorName = "tower-assault_basecolor",
            },
            ["tower-sniper"] = new ModelSpec
            {
                Folder = "tower_sniper", File = "Modle_Tower_Sniper",
                Scale = 1f, Y = 0.96f, Yaw = 0f, Pitch = -90f,
                YawBone = "Yaw", MuzzleOffset = new Vector3(0.15f, 0.95f, 1.55f),
                BaseColorName = "tower-sniper_basecolor",
            },
            ["tower-siege"] = new ModelSpec
            {
                Folder = "tower_siege", File = "Modle_Tower_Siepe",
                Scale = 1f, Y = 0.97f, Yaw = 0f, Pitch = -90f,
                YawBone = "Yaw", MuzzleOffset = new Vector3(0f, 0.45f, 0.95f),
                BaseColorName = "tower-siege_basecolor",
            },
            ["tower-intercept"] = new ModelSpec
            {
                Folder = "tower_intercept", File = "Modle_Tower_Intercept",
                Scale = 1f, Y = 0.88f, Yaw = 0f, Pitch = -90f,
                YawBone = "Yaw", MuzzleOffset = new Vector3(0f, 0.9f, 0f),
                BaseColorName = "tower-intercept_basecolor",
            },
            ["hero"] = new ModelSpec
            {
                Folder = "hero", File = "Modle_Chr_Hero",
                Scale = 1f, Y = 0.24f, Yaw = 180f,
                BaseColorName = "hero_basecolor",
            },
            ["guard"] = new ModelSpec
            {
                Folder = "guard", File = "Modle_Enemy_Guard", Anim = "Anim_Enemy_Guard",
                Scale = 1f, Y = 0f, Yaw = 180f, BarY = 1.5f,
                BaseColorName = "guard_basecolor",
            },
            ["enemy-grunt"] = new ModelSpec
            {
                Folder = "enemy_grunt", File = "Modle_Enemy_Grunt", Anim = "Anim_Enemy_Grunt",
                Scale = 1f, Y = 0f, Yaw = 180f, BarY = 1.55f,
                BaseColorName = "enemy-grunt_basecolor",
            },
            ["enemy-runner"] = new ModelSpec
            {
                Folder = "enemy_runner", File = "Modle_Enemy_Runner",
                Scale = 1f, Y = 0f, Yaw = 180f, Bob = 0.12f, BarY = 1.35f,
                BaseColorName = "enemy-runner_basecolor",
            },
            ["enemy-tank"] = new ModelSpec
            {
                Folder = "enemy_tank", File = "Modle_Enemy_Tank",
                Scale = 1f, Y = 0f, Yaw = 180f, Bob = 0.05f, BarY = 1.8f,
                BaseColorName = "enemy-tank_basecolor",
            },
            ["enemy-elite"] = new ModelSpec
            {
                Folder = "enemy_elite", File = "Modle_Enemy_Elite", Anim = "Anim_Enemy_Elite",
                Scale = 1f, Y = 0f, Yaw = 180f, BarY = 2.7f,
                BaseColorName = "enemy-elite_basecolor",
            },
            ["enemy-boss"] = new ModelSpec
            {
                Folder = "enemy_boss", File = "Modle_Enemy_Annihilation", Anim = "Anim_Enemy_Annihilation",
                Scale = 1f, Y = 1.09f, Yaw = 180f, BarY = 3.6f,
                BaseColorName = "enemy-boss_basecolor",
            },
            ["core"] = new ModelSpec
            {
                Folder = "core", File = "Modle_Prop_Crystal",
                Scale = 1.08f, Y = 0f, Yaw = 0f,
                BaseColorName = "core_basecolor",
            },
            ["spawn-gate"] = new ModelSpec
            {
                Folder = "spawn", File = "Modle_Prop_Spawn",
                Scale = 1.86f, Y = -0.15f, Yaw = 0f,
                BaseColorName = "spawn-gate_basecolor",
            },
        };

        /// <summary>动画状态表（唯一数据源；新增模型只加条目，不改状态机）。</summary>
        private static readonly Dictionary<string, FSMSpec> FsmTable = new Dictionary<string, FSMSpec>
        {
            ["tower-assault"] = new FSMSpec
            {
                // 突击塔只有 attack 片段。base 留空表示「无动画时不播放」，
                // 状态机在 base 为空时不会再回落到任何状态（见 STUnitFSM.Tick）。
                States = new Dictionary<string, string> { ["attack"] = "attack" },
                Base = "", OneShot = new[] { "attack" },
            },
            ["enemy-grunt"] = new FSMSpec
            {
                States = new Dictionary<string, string> { ["idle"] = "idle", ["attack"] = "attack" },
                Base = "idle", OneShot = new[] { "attack" },
            },
            ["enemy-elite"] = new FSMSpec
            {
                States = new Dictionary<string, string> { ["idle"] = "idle", ["attack"] = "attack" },
                Base = "idle", OneShot = new[] { "attack" },
            },
            ["enemy-boss"] = new FSMSpec
            {
                States = new Dictionary<string, string>
                {
                    ["idle"] = "idle", ["move_start"] = "move_start",
                    ["move"] = "move", ["move_stop"] = "move_stop",
                },
                Base = "idle", OneShot = new[] { "move_start", "move_stop" },
                Chain = new Dictionary<string, string> { ["move_start"] = "move", ["move_stop"] = "idle" },
            },
            ["guard"] = new FSMSpec
            {
                States = new Dictionary<string, string> { ["walk"] = "walk", ["idle"] = "walk" },
                Base = "walk",
            },
            ["core"] = new FSMSpec
            {
                States = new Dictionary<string, string> { ["idle"] = "idle" }, Base = "idle",
            },
        };

        private class FSMSpec
        {
            public Dictionary<string, string> States;
            public string Base;
            public string[] OneShot;
            public Dictionary<string, string> Chain;
        }

        private static readonly Dictionary<string, string> EnemyModel = new Dictionary<string, string>
        {
            ["grunt"] = "enemy-grunt",
            ["runner"] = "enemy-runner",
            ["tank"] = "enemy-tank",
            ["elite"] = "enemy-elite",
            ["boss"] = "enemy-boss",
        };

        // ---------------------------------------------------------------- 状态
        public RunState Run;
        public float Time;
        public bool DebugArrows;

        private const float UnitScale = 1.55f;

        private Camera _camera;
        private Transform _worldRoot;
        private Transform _groundRoot;
        private Transform _unitRoot;
        private Transform _fxRoot;
        private Transform _padRoot;

        private readonly Dictionary<string, GameObject> _towerNodes = new Dictionary<string, GameObject>();
        private readonly Dictionary<string, GameObject> _enemyNodes = new Dictionary<string, GameObject>();
        private readonly Dictionary<string, GameObject> _guardNodes = new Dictionary<string, GameObject>();
        private readonly Dictionary<string, GameObject> _bulletNodes = new Dictionary<string, GameObject>();

        private GameObject _heroNode;
        private GameObject _coreNode;
        private GameObject _gateNode;
        private readonly List<Transform> _pads = new List<Transform>();
        private readonly List<MeshRenderer> _padRings = new List<MeshRenderer>();
        private int _unlockedShown = -1;

        private GameObject _rangeRing;
        private GameObject _rangeFill;
        private GameObject _rangeInner;
        private static Texture2D _rangeFillTex;
        private static Texture2D _rangeEdgeTex;
        private GameObject _ghost;
        private string _ghostKey = "";
        private string _hiddenTower = "";
        private string _padHighlight = "";
        private GameObject _mapPlane;
        private GameObject _backdropPlane;

        private Material _unitMaterialTemplate;
        private Material _groundMaterialTemplate;
        private readonly List<BulletFx> _bullets = new List<BulletFx>();
        private readonly List<BlobFx> _blobs = new List<BlobFx>();

        /// <summary>按守卫 id 取节点（自检脚本用）。</summary>
        public GameObject GuardNode(string guardId)
        {
            return _guardNodes.TryGetValue(guardId, out var n) ? n : null;
        }

        public STFxSystem Fx { get; private set; }

        /// <summary>射程指示根节点（自检与调试用）。</summary>
        public GameObject RangeIndicator => _rangeRoot;

        /// <summary>射程指示当前是否可见。</summary>
        public bool RangeVisible => _rangeRoot != null && _rangeRoot.activeSelf;

        /// <summary>按塔 id 取节点（自检脚本用）。</summary>
        public GameObject TowerNode(string towerId)
        {
            return _towerNodes.TryGetValue(towerId, out var n) ? n : null;
        }

        public int FsAttacks;
        public int FsHero;
        public string LastAimDebug = "";

        private class BulletFx
        {
            public GameObject Go;
            public Vector3 From;
            public Vector3 To;
            public float T;
            public float Dur;
            public string Kind;
        }

        private class BlobFx
        {
            public GameObject Go;
            public float T;
            public float Life;
            public float Grow;
            public Color Color;
        }

        // ---------------------------------------------------------------- 构建
        private void EnsureRoots()
        {
            if (_camera != null) return;

            var camGo = new GameObject("BattleCamera");
            camGo.transform.SetParent(transform, false);
            _camera = camGo.AddComponent<Camera>();
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = STColors.Hex("07040F");
            _camera.fieldOfView = 46f;
            _camera.nearClipPlane = 0.5f;
            _camera.farClipPlane = 220f;
            _camera.orthographic = false;

            _worldRoot = new GameObject("World").transform;
            _worldRoot.SetParent(transform, false);

            _groundRoot = new GameObject("Ground").transform;
            _groundRoot.SetParent(_worldRoot, false);

            _padRoot = new GameObject("Pads").transform;
            _padRoot.SetParent(_worldRoot, false);

            _unitRoot = new GameObject("Units").transform;
            _unitRoot.SetParent(_worldRoot, false);

            _fxRoot = new GameObject("Fx").transform;
            _fxRoot.SetParent(_worldRoot, false);

            BuildLights();
            BuildRangeRing();

            Fx = gameObject.AddComponent<STFxSystem>();
            Fx.Init(_camera);
        }

        private void BuildLights()
        {
            var sunGo = new GameObject("Sun");
            sunGo.transform.SetParent(transform, false);
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = STColors.Hex("CFE0FF");
            sun.intensity = 1.1f;
            sun.shadows = LightShadows.None;
            sunGo.transform.rotation = Quaternion.Euler(58f, 34f, 0f);

            var rimGo = new GameObject("Rim");
            rimGo.transform.SetParent(transform, false);
            var rim = rimGo.AddComponent<Light>();
            rim.type = LightType.Directional;
            rim.color = STColors.Hex("FF5BC8");
            rim.intensity = 0.55f;
            rim.shadows = LightShadows.None;
            rimGo.transform.rotation = Quaternion.Euler(24f, -150f, 0f);
        }

        public void Setup(RunState runState, bool debugArrows = false)
        {
            EnsureRoots();
            Run = runState;
            DebugArrows = debugArrows;
            _unlockedShown = -1;
            ApplyLevelCamera();
            BuildGround();
            BuildPads();
            SyncUnits(true);
        }

        private void ApplyLevelCamera()
        {
            if (Run?.Level == null) return;
            var c = Run.Level.camera;
            var pos = new Vector3(c.x, c.y, c.z);
            _camera.transform.position = pos;
            _camera.transform.LookAt(new Vector3(0f, 0f, 1.4f), Vector3.up);
        }

        public Camera Camera => _camera;

        private Material NewUnlitMaterial(Texture2D tex, Color tint, float glow = 0f, bool depthTest = true)
        {
            var shader = Shader.Find("StarLane/Unlit");
            var mat = new Material(shader) { name = "ST-Unlit" };
            if (tex != null) mat.mainTexture = tex;
            mat.color = tint;
            mat.SetFloat("_Glow", glow);
            mat.SetFloat("_ZTest", depthTest ? 4f : 8f);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetFloat("_Cull", 0f);
            return mat;
        }

        private void BuildGround()
        {
            for (int i = _groundRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(_groundRoot.GetChild(i).gameObject);
            }
            _mapPlane = null;
            _backdropPlane = null;

            // 深色底板（比地图略大，避免相机俯视时露出天空盒）
            var baseMesh = STMesh.Plane(
                -STWorld.PX * 30f, -STWorld.PX * 30f,
                STWorld.BoardW + STWorld.PX * 60f, STWorld.BoardH + STWorld.PX * 60f, 0f);

            var baseGo = new GameObject("BasePlate");
            baseGo.transform.SetParent(_groundRoot, false);
            var baseMf = baseGo.AddComponent<MeshFilter>();
            baseMf.sharedMesh = baseMesh;
            var baseMr = baseGo.AddComponent<MeshRenderer>();
            baseMr.sharedMaterial = NewUnlitMaterial(null, STColors.Hex("0E091B"));
            baseMr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            baseMr.receiveShadows = false;

            if (Run?.Level == null) return;

            // 关卡地图贴图按 mapRect 铺在底板上
            var sprite = STAssets.MapTexture(Run.Level.map);
            if (sprite == null) return;

            var rect = Run.Level.mapRect;
            var mesh = STMesh.Plane(rect.x, rect.y, rect.w, rect.h, 0.005f);
            var go = new GameObject("MapPlane");
            go.transform.SetParent(_groundRoot, false);
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = NewUnlitMaterial(sprite.texture, STColors.Hex("DBDBEE"));
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            _mapPlane = go;

            // 地图上沿之外补一块星空幕（俯视会越界）
            var backdrop = STAssets.Background("battle-backdrop");
            if (backdrop != null)
            {
                var quad = new GameObject("Backdrop");
                quad.transform.SetParent(_groundRoot, false);
                var qmf = quad.AddComponent<MeshFilter>();
                qmf.sharedMesh = STMesh.Quad(new Vector2(120f, 60f));
                var qmr = quad.AddComponent<MeshRenderer>();
                qmr.sharedMaterial = NewUnlitMaterial(backdrop.texture, STColors.Hex("B3ADD6"));
                qmr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                qmr.receiveShadows = false;

                var center = STWorld.ToWorld(new Vector2(STWorld.BoardCx, rect.y - 40f), 24f);
                center.z -= 6f;
                quad.transform.position = center;
                // 面向相机（相机在 +Z 后方，故朝向 -Z 需要旋转 180°）
                quad.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
                _backdropPlane = quad;
            }
        }

        private void BuildPads()
        {
            foreach (Transform t in _pads) Destroy(t.gameObject);
            _pads.Clear();
            _padRings.Clear();
            if (Run == null) return;

            var discMesh = STMesh.Cylinder(1.5f, 1.6f, 0.18f, 8);
            var ringMesh = STMesh.Torus(1.32f, 1.44f, 32, 4);

            foreach (var plat in Run.Platforms)
            {
                var root = new GameObject("Pad-" + plat.Id);
                root.transform.SetParent(_padRoot, false);
                root.transform.position = STWorld.ToWorld(plat.Pos);

                bool unlocked = Run.IsPlatformUnlocked(plat);

                var discGo = new GameObject("Disc");
                discGo.transform.SetParent(root.transform, false);
                discGo.transform.localPosition = new Vector3(0f, 0.09f, 0f);
                var dmf = discGo.AddComponent<MeshFilter>();
                dmf.sharedMesh = discMesh;
                var dmr = discGo.AddComponent<MeshRenderer>();
                var discMat = new Material(Shader.Find("StarLane/ModelLit")) { name = "Pad-Disc" };
                discMat.color = unlocked ? new Color(0.16f, 0.15f, 0.26f) : new Color(0.1f, 0.1f, 0.14f);
                dmr.sharedMaterial = discMat;
                dmr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                var ringGo = new GameObject("Ring");
                ringGo.transform.SetParent(root.transform, false);
                ringGo.transform.localPosition = new Vector3(0f, 0.19f, 0f);
                var rmf = ringGo.AddComponent<MeshFilter>();
                rmf.sharedMesh = ringMesh;
                var rmr = ringGo.AddComponent<MeshRenderer>();
                var ringMat = NewUnlitMaterial(null, unlocked ? STColors.Cyan : new Color(0.22f, 0.2f, 0.32f), 0.6f);
                rmr.sharedMaterial = ringMat;
                rmr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                _pads.Add(root.transform);
                _padRings.Add(rmr);
                root.name = "Pad-" + plat.Id;
            }
            _padHighlight = "__init__";
        }

        /// <summary>
        /// 射程指示器：半透明填充圆 + 亮边环 + 内侧细环。
        ///
        /// 三个部件共用同一个节点，缩放时统一跟随；贴图是启动时生成的柔边圆盘，
        /// 比纯几何圆环更接近 Godot 版 draw_range 的观感（中心实、边缘淡）。
        /// </summary>
        private void BuildRangeRing()
        {
            var root = new GameObject("RangeIndicator");
            root.transform.SetParent(_worldRoot, false);

            _rangeFillTex ??= BuildRangeDiscTexture();
            _rangeEdgeTex ??= BuildRangeEdgeTexture();

            // 填充：柔边圆盘
            _rangeFill = MakeRangePart(root.transform, "Fill", _rangeFillTex,
                new Color(0.31f, 0.95f, 1f, 0.16f));

            // 外侧亮边：环形贴图
            _rangeRing = MakeRangePart(root.transform, "Edge", _rangeEdgeTex,
                new Color(0.55f, 0.98f, 1f, 0.85f));

            // 内侧细环：强调边界
            _rangeInner = MakeRangePart(root.transform, "Inner", _rangeEdgeTex,
                new Color(0.31f, 0.95f, 1f, 0.35f));

            root.SetActive(false);
            _rangeRoot = root;
        }

        private GameObject _rangeRoot;

        private static GameObject MakeRangePart(Transform parent, string name, Texture2D tex, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var mf = go.AddComponent<MeshFilter>();
            // 用水平四边形：无需旋转，直接按直径缩放 X/Z
            mf.sharedMesh = STMesh.GroundQuad();

            var mr = go.AddComponent<MeshRenderer>();
            // 用统一的着色器入口（避免 Shader.Find 在打包后返回 null 导致整个指示器不渲染）
            var mat = STShaders.NewUnlit(tex, color, "Range-" + name);
            // 射程圆要压在模型之下、地图之上：抬高一点并关闭深度写入，
            // 否则会被台面/单位挡掉一半。
            mat.SetFloat("_ZTest", 4f);
            mat.SetFloat("_ZWrite", 0f);
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return go;
        }

        /// <summary>柔边圆盘：中心半透明、边缘渐隐。</summary>
        private static Texture2D BuildRangeDiscTexture()
        {
            const int n = 256;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "st-range-disc",
            };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f;
                    float dy = (y + 0.5f) / n * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    // 内部 0.86 之前基本均匀，之后快速衰减到 0
                    float a = Mathf.Clamp01((1f - d) / 0.14f);
                    a = Mathf.Pow(a, 0.8f);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return tex;
        }

        /// <summary>单位圆环（外径贴满贴图边界）。</summary>
        private static Texture2D BuildRangeEdgeTexture()
        {
            const int n = 256;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "st-range-edge",
            };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f;
                    float dy = (y + 0.5f) / n * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    // 环心在 d=0.95，宽度 0.07 —— 射程圆往往直径很大，
                    // 环太细在透视俯视下会糊掉，这里刻意加宽
                    float a = Mathf.Clamp01(1f - Mathf.Abs(d - 0.95f) / 0.07f);
                    a = Mathf.Pow(a, 1.2f);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return tex;
        }

        // ---------------------------------------------------------------- 模型实例化
        private GameObject SpawnModel(string key)
        {
            if (!Models.TryGetValue(key, out var spec))
            {
                var empty = new GameObject("Model-" + key);
                return empty;
            }

            var holder = new GameObject("Unit-" + key);

            var modelAsset = STAssets.Model(spec.Folder + "/" + spec.File);
            if (modelAsset == null)
            {
                Debug.LogWarning("[STBattleView] 模型缺失: " + spec.Folder + "/" + spec.File);
                return holder;
            }

            var inst = Instantiate(modelAsset, holder.transform, false);
            inst.name = spec.File;

            // 材质替换：用同目录 basecolor 贴图 + 赛璐璐着色器
            var baseColor = LoadBaseColor(spec);
            var litShader = Shader.Find("StarLane/ModelLit");
            // 每个单位一份材质实例：受击闪白（_Flash）与幽灵虚影都需要独立控制，
            // 共享材质会让同型号的所有单位一起闪。
            var mat = new Material(litShader) { name = "ST-Model-" + key };
            if (baseColor != null) mat.mainTexture = baseColor;
            mat.color = Color.white;

            foreach (var r in inst.GetComponentsInChildren<MeshRenderer>(true))
            {
                r.sharedMaterial = mat;
                r.shadowCastingMode = spec.CastShadow
                    ? UnityEngine.Rendering.ShadowCastingMode.Off
                    : UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
            foreach (var r in inst.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                r.sharedMaterial = mat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }

            // Body 层：负责朝向 / 缩放 / 落地（与 Godot 的 body 节点等价）
            //
            // 两个静态校正必须一起作用，缺一不可：
            //   · Pitch：塔模型在 FBX 里是躺平的，绕 X 转 -90° 才立起来；
            //   · Yaw  ：模型机头在局部 +Z，敌人要补 180° 才与「holder 的 +Z 即背面」的
            //            朝向约定一致（见 ApplyFacing）。漏掉它敌人就会倒着走。
            // Unity 的 Euler(x, y, z) 按 Z→X→Y 复合，等价于「先 Pitch 立起、再绕世界 Y 转向」，
            // 正是这里要的顺序。
            var body = new GameObject("Body");
            body.transform.SetParent(holder.transform, false);
            body.transform.localRotation = Quaternion.Euler(spec.Pitch, spec.Yaw, 0f);

            float s = spec.Scale;
            body.transform.localScale = Vector3.one * (s * UnitScale);
            body.transform.localPosition = Vector3.zero;

            inst.transform.SetParent(body.transform, false);
            inst.transform.localPosition = Vector3.zero;
            inst.transform.localRotation = Quaternion.identity;
            inst.transform.localScale = Vector3.one;

            // 落地：量出「已应用 Pitch + Scale」后的真实包围盒，把最低点压到 y=0。
            // 不能沿用配置里的固定 Y（Pitch 改变竖直范围后会让塔悬空 1.4–1.9m）。
            // 这里只补偿模型自身最低点，配置里的 Y 仍作为微调叠加。
            // 落地：先按配置给个初值，再由 STGroundSnap 在矩阵刷新后精确压到地面。
            // 之所以不在这一帧直接量：Body 的 localPosition 还没设定，
            // Renderer.bounds 读到的是过期矩阵（实测恒返回 0），手写局部变换又容易漏掉
            // 子节点的旋转/缩放链。交给 STGroundSnap 用真实的 bounds 处理最可靠。
            body.transform.localPosition = new Vector3(0f, spec.Y * s * UnitScale, 0f);
            var snap = holder.AddComponent<STGroundSnap>();
            snap.Configure(body.transform, inst.transform, 0f);            var holderInfo = holder.AddComponent<STUnitView>();
            holderInfo.Body = body.transform;
            holderInfo.SpecYaw = spec.Yaw;
            holderInfo.BarY = spec.BarY;
            holderInfo.ModelRoot = inst.transform;
            holderInfo.BaseScale = s;
            holderInfo.YawBone = FindDeep(inst.transform, spec.YawBone);

            // 记录 Yaw 骨骼的静止局部旋转，以及静止姿态下炮口的水平朝向。
            if (holderInfo.YawBone != null)
            {
                holderInfo.YawRestLocal = holderInfo.YawBone.localRotation;
            }
            if (spec.Muzzles != null)
            {
                holderInfo.Muzzles = new Transform[spec.Muzzles.Length];
                for (int i = 0; i < spec.Muzzles.Length; i++)
                {
                    holderInfo.Muzzles[i] = FindDeep(inst.transform, spec.Muzzles[i]);
                }
            }

            // 自校准基准：静止姿态下「枢轴 → 炮口」的水平方向。
            // 必须在这里（Pitch 已生效、Yaw 为静止姿态时）采样，
            // 之后 AimTower 只用它做差分旋转，不再需要任何手工角度常数。
            if (holderInfo.YawBone != null)
            {
                holder.transform.rotation = Quaternion.identity;   // 采样时排除 holder 朝向
                Vector3 pivot = holderInfo.YawBone.position;
                var dir = Vector3.zero;
                int n = 0;
                if (holderInfo.Muzzles != null)
                {
                    foreach (var m in holderInfo.Muzzles)
                    {
                        if (m == null) continue;
                        dir += m.position - pivot;
                        n++;
                    }
                }
                if (n > 0) dir /= n;

                dir.y = 0f;
                if (dir.sqrMagnitude > 1e-4f)
                {
                    holderInfo.YawRestDir = dir.normalized;
                    holderInfo.YawRestDirValid = true;
                }
                else
                {
                    // 炮口骨骼正好在枢轴正上方（如拦截塔的 Head），
                    // 水平偏移为 0 → 改用配置的 MuzzleOffset 方向作为静止朝向。
                    var off = spec.MuzzleOffset;
                    var flat = new Vector3(off.x, 0f, off.z);
                    if (flat.sqrMagnitude > 1e-4f)
                    {
                        holderInfo.YawRestDir = flat.normalized;
                    }
                    else
                    {
                        // 再退一步：模型零位炮口朝 +Z（已由所有塔的骨骼实测确认）
                        holderInfo.YawRestDir = Vector3.forward;
                    }
                    holderInfo.YawRestDirValid = true;
                }
            }

            // 动画：模型自带片段 + 同目录 Anim_*.fbx 合并（剔除 Yaw 轨道）
            var anim = inst.GetComponent<Animation>();
            if (anim == null) anim = inst.AddComponent<Animation>();
            anim.playAutomatically = false;
            anim.cullingType = AnimationCullingType.AlwaysAnimate;

            foreach (var clip in STAssets.Clips(spec.Folder + "/" + spec.File))
            {
                if (clip != null) anim.AddClip(clip, clip.name);
            }
            if (!string.IsNullOrEmpty(spec.Anim))
            {
                foreach (var clip in STAssets.Clips(spec.Folder + "/" + spec.Anim))
                {
                    if (clip == null) continue;
                    if (anim.GetClip(clip.name) != null) continue;
                    // Yaw 骨骼的轨道已在导入阶段剔除（见 STAssetPostprocessor），
                    // 这里直接合并，避免攻击动画把炮塔扭回零位。
                    anim.AddClip(clip, clip.name);
                }
            }
            holderInfo.Anim = anim;

            // 状态机
            if (FsmTable.TryGetValue(key, out var fsmSpec) && anim.GetClipCount() > 0)
            {
                var resolved = new Dictionary<string, string>();
                foreach (var kv in fsmSpec.States)
                {
                    var clip = FindClipBySuffix(anim, kv.Value);
                    if (clip != null) resolved[kv.Key] = clip;
                }
                if (resolved.Count > 0)
                {
                    var fsm = new STUnitFSM(anim, resolved, fsmSpec.Base, 1f, fsmSpec.OneShot, fsmSpec.Chain);
                    holderInfo.Fsm = fsm;
                    if (!string.IsNullOrEmpty(fsmSpec.Base) && fsm.Has(fsmSpec.Base)) fsm.SetState(fsmSpec.Base, true);
                }
            }

            holderInfo.SyncShadowBlob();
            return holder;
        }

        private static Texture2D LoadBaseColor(ModelSpec spec)
        {
            var tex = Resources.Load<Texture2D>("Art/Models/" + spec.Folder + "/" + spec.BaseColorName);
            if (tex != null) return tex;
            return null;
        }

        private static string FindClipBySuffix(Animation anim, string suffix)
        {
            if (string.IsNullOrEmpty(suffix)) return null;
            foreach (AnimationState st in anim)
            {
                var n = st.clip.name;
                if (n == suffix || n.EndsWith("|" + suffix) || n.EndsWith(suffix)) return n;
            }
            return null;
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root == null || string.IsNullOrEmpty(name)) return null;
            if (root.name == name) return root;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name) return t;
            }
            return null;
        }

        // ---------------------------------------------------------------- 同步
        /// <summary>
        /// 每帧同步：位置、朝向、缩放、血条、特效。由 STMain.Update 调用。
        /// 注意：不要把这里放进 LateUpdate —— STMain 会显式控制调用顺序，
        /// 避免同一帧内 SyncUnits 与 UpdateView 互相覆盖。
        /// </summary>
        public void SyncUnits(bool force = false)
        {
            if (Run == null) return;

            // 核心 / 出生门
            if (_coreNode == null || force)
            {
                if (_coreNode != null) Destroy(_coreNode);
                _coreNode = SpawnModel("core");
                _coreNode.transform.SetParent(_unitRoot, false);
                _coreNode.transform.position = STWorld.ToWorld(Run.CorePos);
            }
            if (_gateNode == null || force)
            {
                if (_gateNode != null) Destroy(_gateNode);
                _gateNode = SpawnModel("spawn-gate");
                _gateNode.transform.SetParent(_unitRoot, false);
                _gateNode.transform.position = STWorld.ToWorld(Run.SpawnPos);
                BuildGateVortex(_gateNode);
            }

            // 台面解锁数量变化时重建
            int unlockedNow = Run.UnlockedPlatformCount();
            if (unlockedNow != _unlockedShown)
            {
                _unlockedShown = unlockedNow;
                BuildPads();
            }

            // 塔
            var aliveTowers = new HashSet<string>();
            foreach (var t in Run.Towers)
            {
                string id = t.Id;
                aliveTowers.Add(id);
                if (!_towerNodes.TryGetValue(id, out var node) || node == null)
                {
                    string key = "tower-" + t.ClassId;
                    node = SpawnModel(key);
                    node.transform.SetParent(_unitRoot, false);
                    var plat = Run.PlatformById(t.PlatformId);
                    if (plat != null) node.transform.position = STWorld.ToWorld(plat.Pos);
                    var view = node.GetComponent<STUnitView>();
                    if (view != null)
                    {
                        view.Key = key;
                        view.ClassId = t.ClassId;
                        view.SwayPhase = Random.Range(0f, Mathf.PI * 2f);
                    }
                    _towerNodes[id] = node;
                    PlayBase(node);
                }
                else
                {
                    var plat2 = Run.PlatformById(t.PlatformId);
                    if (plat2 != null) node.transform.position = STWorld.ToWorld(plat2.Pos);
                }

                // 等级缩放（每级 +8%），缩放后重新落地
                var v = node.GetComponent<STUnitView>();
                if (v != null && Models.TryGetValue(v.Key ?? "", out var spec))
                {
                    float s = spec.Scale * (1f + (t.Level - 1) * 0.08f);
                    float want = s * UnitScale;
                    if (!Mathf.Approximately(v.Body.localScale.x, want))
                    {
                        v.Body.localScale = Vector3.one * want;
                        var snap = node.GetComponent<STGroundSnap>();
                        snap?.Configure(v.Body, v.ModelRoot, 0f);
                    }
                }
            }
            PruneNodes(_towerNodes, aliveTowers);

            // 敌人
            var aliveEnemies = new HashSet<string>();
            foreach (var e in Run.Enemies)
            {
                string eid = e.Id;
                aliveEnemies.Add(eid);
                string key = EnemyModel.TryGetValue(e.Type, out var mk) ? mk : "enemy-grunt";
                if (!_enemyNodes.TryGetValue(eid, out var node) || node == null)
                {
                    node = SpawnModel(key);
                    node.transform.SetParent(_unitRoot, false);
                    var v = node.GetComponent<STUnitView>();
                    if (v != null) v.Key = key;
                    _enemyNodes[eid] = node;
                    PlayBase(node);
                }
                node.transform.position = STWorld.ToWorld(e.Pos);
                // 敌人朝向 = 车道切线（即移动方向），用平滑插值避免拐角处突变。
                // 用户要求「根据移动方向修改朝向」，这就是移动方向驱动。
                var targetRot = Quaternion.Euler(0f, (e.Angle + Mathf.PI * 0.5f) * Mathf.Rad2Deg, 0f);
                node.transform.rotation = Quaternion.RotateTowards(node.transform.rotation, targetRot,
                    540f * UnityEngine.Time.deltaTime);

                if (DebugArrows) EnsureArrow(node);
            }
            PruneNodes(_enemyNodes, aliveEnemies);

            // 拦截单位
            var aliveGuards = new HashSet<string>();
            foreach (var g in Run.Guards)
            {
                string gid = g.Id;
                aliveGuards.Add(gid);
                if (!_guardNodes.TryGetValue(gid, out var node) || node == null)
                {
                    node = SpawnModel("guard");
                    node.transform.SetParent(_unitRoot, false);
                    var v = node.GetComponent<STUnitView>();
                    if (v != null) v.Key = "guard";
                    _guardNodes[gid] = node;
                    PlayBase(node);
                }
                node.transform.position = STWorld.ToWorld(g.Pos);

                // 死亡守卫仍然隐藏（重建期间留空），这是与 Godot 版一致的表现。
                // 之所以不改成半透明：模型用的是 Opaque 赛璐璐着色器，
                // 半透明需要改渲染队列与混合模式，会波及所有单位的排序。
                node.SetActive(g.Hp > 0f);

                ApplyFacing(node, g.Angle);
            }
            PruneNodes(_guardNodes, aliveGuards);

            // 英雄
            if (_heroNode == null)
            {
                _heroNode = SpawnModel("hero");
                _heroNode.transform.SetParent(_unitRoot, false);
                var v = _heroNode.GetComponent<STUnitView>();
                if (v != null) v.Key = "hero";
            }
            _heroNode.transform.position = STWorld.ToWorld(Run.Hero.Pos);
            float heroAngle = Run.Hero.Moving
                ? Mathf.Atan2(Run.Hero.Target.y - Run.Hero.Pos.y, Run.Hero.Target.x - Run.Hero.Pos.x)
                : Run.Hero.Angle;
            ApplyFacing(_heroNode, heroAngle);

            // 塔瞄准
            foreach (var t in Run.Towers)
            {
                if (_towerNodes.TryGetValue(t.Id, out var node) && node != null) AimTower(node, t);
            }
        }

        private void PruneNodes(Dictionary<string, GameObject> dict, HashSet<string> alive)
        {
            List<string> dead = null;
            foreach (var kv in dict)
            {
                if (!alive.Contains(kv.Key)) (dead ??= new List<string>()).Add(kv.Key);
            }
            if (dead == null) return;
            foreach (var id in dead)
            {
                if (dict[id] != null) Destroy(dict[id]);
                dict.Remove(id);
            }
        }

        /// <summary>
        /// 朝向（用于敌人 / 拦截单位 / 英雄）。
        ///
        /// 推导：设计角度 θ 的方向在设计坐标是 (cosθ, sinθ)，换算到 Unity 世界是
        /// d = (-cosθ, 0, sinθ)（X 取反，见 STWorld.ToWorld）。
        /// 这些模型在 Models 表里 Yaw 已补 180°，即模型机头（局部 +Z）在 holder 上是 -Z；
        /// 要让机头对准 d，需 holder 的 +Z 等于 -d：
        ///   (sin yaw, cos yaw) = (cosθ, -sinθ)  →  yaw = θ + π/2
        /// </summary>
        private static void ApplyFacing(GameObject node, float angle)
        {
            node.transform.rotation = Quaternion.Euler(0f, (angle + Mathf.PI * 0.5f) * Mathf.Rad2Deg, 0f);
        }

        private void PlayBase(GameObject node)
        {
            var v = node.GetComponent<STUnitView>();
            if (v?.Fsm != null)
            {
                if (v.Fsm.Has(v.Fsm.Base)) v.Fsm.SetState(v.Fsm.Base, true);
                return;
            }
            if (v?.Anim != null)
            {
                foreach (AnimationState st in v.Anim)
                {
                    st.clip.wrapMode = WrapMode.Loop;
                    v.Anim.Play(st.clip.name);
                    break;
                }
            }
        }

        public void ClearUnits()
        {
            foreach (var dict in new[] { _towerNodes, _enemyNodes, _guardNodes })
            {
                foreach (var kv in dict) if (kv.Value != null) Destroy(kv.Value);
                dict.Clear();
            }
            _bullets.Clear();
            _blobs.Clear();
            Fx?.Clear();
            if (_heroNode != null) { Destroy(_heroNode); _heroNode = null; }
            if (_coreNode != null) { Destroy(_coreNode); _coreNode = null; }
            if (_gateNode != null) { Destroy(_gateNode); _gateNode = null; }
        }

        // ---------------------------------------------------------------- 瞄准 / 炮口
        /// <summary>
        /// 炮塔瞄准：旋转**整个模型节点**（不碰任何骨骼）。
        ///
        /// 为什么不用骨骼：Yaw 骨骼的父级带 RotX(≈90°)，它的局部 Y 轴不是世界 up 轴，
        /// 绕它旋转会变成绕世界 Z 翻滚，结果看起来始终是歪的/反的。
        /// 直接转 holder 节点则只涉及世界 Y 轴，行为直观且与底座无关。
        ///
        /// 角度推导：模型零位炮口朝 +Z（实测 Muzzle_R 在 Yaw 下的 z = +0.71）。
        /// 绕 Y 转 ψ 后 +Z 指向 (sinψ, 0, cosψ)；设计角度 θ 的期望世界方向是
        /// (-cosθ, 0, sinθ)。令两者相等解得 ψ = θ - 90°。
        /// </summary>
        /// <summary>
        /// 炮塔瞄准：**底座不动，只旋转 Yaw 骨骼**（用户明确要求）。
        ///
        /// Pitch=-90 校正后，Yaw 骨骼的父空间 up 正好是 (0,1,0)，
        /// 因此绕骨骼局部 Y 轴旋转 = 绕世界 up 旋转，炮塔在水平面内转向而底座纹丝不动。
        ///
        /// 转向量是**自校准**的：生成时记录静止姿态下炮口的水平朝向 YawRestDir，
        /// 瞄准时求「从该朝向转到期望世界方向」的有符号角，再叠加到静止姿态上。
        /// 这样不依赖任何手工推导的 ±90° 偏移，换模型也不会错。
        /// </summary>
        private void AimTower(GameObject node, TowerState t)
        {
            var plat = Run.PlatformById(t.PlatformId);
            if (plat == null) return;

            var v = node.GetComponent<STUnitView>();
            if (v == null) return;

            var target = PickTargetWorld(STWorld.ToWorld(plat.Pos), Run.StatsOf(t).Range / STWorld.PX);
            float angle = t.Aim;
            bool hasTarget = target != null;

            if (!hasTarget)
            {
                // 无目标时朝向最近车道并缓慢摆动（而不是停在逻辑初始值，否则看着像朝向错）
                angle = AngleToLane(plat.Pos, t) + Mathf.Sin(Time * 0.7f + v.SwayPhase) * 0.10f;
            }

            var expected = STWorld.DesignDirection(angle);   // (-cosθ, 0, sinθ)

            if (v.YawBone != null && v.YawRestDirValid)
            {
                // 从静止朝向转到期望朝向的有符号角（绕世界 up）
                float delta = Vector3.SignedAngle(v.YawRestDir, expected, Vector3.up);
                // 平滑转速：与逻辑层的转速一致，避免视觉上瞬移
                float turnRate = (STData.GetClass(t.ClassId)?.turn ?? 4f) * Mathf.Rad2Deg;
                float cur = v.YawApplied;
                float stepMax = turnRate * UnityEngine.Time.deltaTime;
                float next = hasTarget ? Mathf.MoveTowardsAngle(cur, delta, stepMax)
                                       : Mathf.MoveTowardsAngle(cur, delta, stepMax * 0.35f);

                // 角度绕回，避免长时间累积
                if (next > 180f) next -= 360f;
                if (next < -180f) next += 360f;
                v.YawApplied = next;

                v.YawBone.localRotation = Quaternion.AngleAxis(next, Vector3.up) * v.YawRestLocal;
            }
            else if (v.YawBone != null)
            {
                v.YawBone.localRotation = v.YawRestLocal;
            }

            // 底座保持生成时的朝向（不做任何改动）—— node.transform.rotation 不再被瞄准触碰
        }

        /// <summary>塔位指向最近路径点的设计角度（无目标时的默认朝向）。</summary>
        private float AngleToLane(Vector2 platformPos, TowerState t)
        {
            Run.NearestPath(platformPos, out float dist, out _);
            var lane = Run.PathPos(dist);
            var d = lane - platformPos;
            if (d.sqrMagnitude < 1e-4f) return t.Aim;
            return Mathf.Atan2(d.y, d.x);
        }

        private EnemyState PickTargetWorld(Vector3 from, float radiusM)
        {
            EnemyState best = null;
            float bestDist = float.NegativeInfinity;
            foreach (var e in Run.Enemies)
            {
                var p = STWorld.ToWorld(e.Pos);
                if (Vector3.Distance(p, from) > radiusM + 1f) continue;
                if (e.Dist > bestDist)
                {
                    bestDist = e.Dist;
                    best = e;
                }
            }
            return best;
        }

        /// <summary>当前塔的炮口世界坐标（骨骼优先，否则用配置偏移）。</summary>
        public Vector3 MuzzleWorld(TowerState t)
        {
            if (!_towerNodes.TryGetValue(t.Id, out var node) || node == null) return Vector3.zero;
            var v = node.GetComponent<STUnitView>();
            if (v == null) return node.transform.position;

            if (v.Muzzles != null && v.Muzzles.Length > 0)
            {
                var pick = v.Muzzles[Random.Range(0, v.Muzzles.Length)];
                if (pick != null) return pick.position;
            }
            if (v.YawBone != null)
            {
                if (Models.TryGetValue(v.Key ?? "", out var spec))
                {
                    var dir = STWorld.DesignDirection(t.Angle);
                    return v.YawBone.position + dir * Mathf.Abs(spec.MuzzleOffset.z)
                        + new Vector3(0f, spec.MuzzleOffset.y, 0f);
                }
            }
            return node.transform.position + new Vector3(0f, 1.2f, 0f);
        }

        public void TriggerTowerAttack(string towerId)
        {
            FsAttacks++;
            if (!_towerNodes.TryGetValue(towerId, out var node) || node == null) return;
            var v = node.GetComponent<STUnitView>();
            if (v?.Fsm != null && v.Fsm.Has("attack")) v.Fsm.PlayOnce("attack");
        }

        public void TriggerHeroShot()
        {
            FsHero++;
            if (_heroNode == null) return;
            var v = _heroNode.GetComponent<STUnitView>();
            if (v?.Fsm != null && v.Fsm.Has("shoot")) v.Fsm.PlayOnce("shoot", 0.05f);
        }

        // ---------------------------------------------------------------- 出生门漩涡
        private void BuildGateVortex(GameObject gate)
        {
            var existing = gate.transform.Find("Vortex");
            if (existing != null) Destroy(existing.gameObject);

            var root = new GameObject("Vortex");
            root.transform.SetParent(gate.transform, false);
            root.transform.localPosition = new Vector3(0f, 1.85f, 0.75f);
            root.transform.localScale = Vector3.one * 1.15f;

            var sphere = new GameObject("Glow");
            sphere.transform.SetParent(root.transform, false);
            var smf = sphere.AddComponent<MeshFilter>();
            smf.sharedMesh = STMesh.Sphere(0.34f, 16, 8);
            var smr = sphere.AddComponent<MeshRenderer>();
            smr.sharedMaterial = NewUnlitMaterial(null, new Color(1f, 0.23f, 0.68f, 0.55f), 1.2f);
            smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var arms = new GameObject("Arms");
            arms.transform.SetParent(root.transform, false);
            for (int i = 0; i < 6; i++)
            {
                var arm = new GameObject("Arm" + i);
                arm.transform.SetParent(arms.transform, false);
                var amf = arm.AddComponent<MeshFilter>();
                amf.sharedMesh = STMesh.Quad(new Vector2(0.85f, 0.1f));
                var amr = arm.AddComponent<MeshRenderer>();
                amr.sharedMaterial = NewUnlitMaterial(null, new Color(1f, 0.55f, 0.85f, 0.9f), 1.4f);
                amr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                float a = (float)i / 6f * Mathf.PI * 2f;
                float radius = i % 2 == 0 ? 0.45f : 0.62f;
                arm.transform.localPosition = new Vector3(
                    Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0.02f * (i % 2 == 0 ? 1f : -1f));
                arm.transform.localRotation = Quaternion.Euler(0f, 0f, (a + 0.5f) * Mathf.Rad2Deg);
            }

            var ring = new GameObject("Ring");
            ring.transform.SetParent(root.transform, false);
            var rmf = ring.AddComponent<MeshFilter>();
            rmf.sharedMesh = STMesh.Torus(0.66f, 0.74f, 32, 4);
            var rmr = ring.AddComponent<MeshRenderer>();
            rmr.sharedMaterial = NewUnlitMaterial(null, STColors.Magenta, 1.2f);
            rmr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            var vortex = gate.AddComponent<STGateVortex>();
            vortex.Arms = arms.transform;
            vortex.Glow = smr;
        }

        // ---------------------------------------------------------------- 视图状态
        /// <summary>
        /// 视图状态同步：射程指示、拖动虚影、台面高亮。
        ///
        /// 射程指示的两种来源（与 Godot 版一致）：
        ///   · 选中某座塔 → 在塔位显示它的射程；
        ///   · 拖动某座塔 → 在指针位置显示它的射程（虚影所在处）。
        /// 两者都不会显示拦截塔的射程——它的作用范围由拦截单位体现，画射程会误导。
        /// </summary>
        public void UpdateView(string selectedPlatform, string dragTowerId, Vector2 dragPos, string dragTarget,
            float elapsed, float selectedAt)
        {
            if (Run == null) return;
            UpdateGhost(dragTowerId, dragPos);

            bool shown = false;

            // 拖动优先：跟随指针位置
            if (!string.IsNullOrEmpty(dragTowerId))
            {
                var dragged = Run.TowerById(dragTowerId);
                if (dragged != null && dragged.ClassId != "intercept")
                {
                    ShowRange(dragPos, Run.StatsOf(dragged).Range, 1f);
                    shown = true;
                }
            }

            // 否则显示选中塔的射程
            if (!shown && !string.IsNullOrEmpty(selectedPlatform))
            {
                var plat = Run.PlatformById(selectedPlatform);
                if (plat?.Tower != null && plat.Tower.ClassId != "intercept")
                {
                    ShowRange(plat.Pos, Run.StatsOf(plat.Tower).Range, 1f);
                    shown = true;
                }
            }

            if (!shown && _rangeRoot != null) _rangeRoot.SetActive(false);

            UpdatePadHighlight(dragTarget);
        }

        /// <summary>在指定塔位显示射程圆（rangePx 为设计像素半径）。</summary>
        private void ShowRange(Vector2 centerPx, float rangePx, float alpha)
        {
            if (_rangeRoot == null) return;

            float r = Mathf.Max(0.5f, rangePx / STWorld.PX);
            _rangeRoot.SetActive(true);
            _rangeRoot.transform.position = STWorld.ToWorld(centerPx, 0.05f);

            // 填充圆：GroundQuad 位于 XZ 平面，直径缩放必须作用于 X/Z，Y 保持 1
            float d = r * 2f;
            _rangeFill.transform.localScale = new Vector3(d, 1f, d);
            _rangeRing.transform.localScale = new Vector3(d, 1f, d);
            _rangeInner.transform.localScale = new Vector3(d * 0.94f, 1f, d * 0.94f);

            if (_rangeFill.TryGetComponent<MeshRenderer>(out var fillMr) && fillMr.sharedMaterial != null)
            {
                // 填充浓度要够：俯视角下射程圆往往大于视野，太淡会完全看不见
                fillMr.sharedMaterial.color = new Color(0.31f, 0.95f, 1f, 0.26f * alpha);
            }
            if (_rangeRing.TryGetComponent<MeshRenderer>(out var edgeMr) && edgeMr.sharedMaterial != null)
            {
                edgeMr.sharedMaterial.color = new Color(0.70f, 1f, 1f, 1f * alpha);
            }
            if (_rangeInner.TryGetComponent<MeshRenderer>(out var innerMr) && innerMr.sharedMaterial != null)
            {
                innerMr.sharedMaterial.color = new Color(0.31f, 0.95f, 1f, 0.55f * alpha);
            }
        }

        private void UpdatePadHighlight(string targetId)
        {
            if (_padHighlight == targetId) return;
            _padHighlight = targetId;

            GameObject target = targetId == "" ? null : null;
            for (int i = 0; i < _pads.Count; i++)
            {
                var mat = _padRings[i].sharedMaterial;
                if (mat == null) continue;
                bool isTarget = false;
                if (!string.IsNullOrEmpty(targetId) && i < Run.Platforms.Count)
                {
                    isTarget = Run.Platforms[i].Id == targetId;
                }

                if (isTarget)
                {
                    mat.color = STColors.Gold;
                    mat.SetFloat("_Glow", 1.6f);
                }
                else
                {
                    bool unlocked = Run.Platforms[i] != null && Run.IsPlatformUnlocked(Run.Platforms[i]);
                    mat.color = unlocked ? STColors.Cyan : new Color(0.22f, 0.2f, 0.32f);
                    mat.SetFloat("_Glow", unlocked ? 0.6f : 0f);
                }
            }
        }

        private void UpdateGhost(string towerId, Vector2 dragPos)
        {
            if (!string.IsNullOrEmpty(_hiddenTower))
            {
                if (_towerNodes.TryGetValue(_hiddenTower, out var old) && old != null) old.SetActive(true);
                _hiddenTower = "";
            }

            if (string.IsNullOrEmpty(towerId))
            {
                if (_ghost != null) { Destroy(_ghost); _ghost = null; }
                _ghostKey = "";
                return;
            }

            TowerState tw = null;
            foreach (var t in Run.Towers)
            {
                if (t.Id == towerId) { tw = t; break; }
            }
            if (tw == null) return;

            string key = "tower-" + tw.ClassId;
            if (_ghost == null || _ghostKey != key)
            {
                if (_ghost != null) Destroy(_ghost);
                _ghost = SpawnModel(key);
                _ghost.transform.SetParent(_fxRoot, false);
                ApplyGhostMaterial(_ghost);
                _ghostKey = key;
            }

            _hiddenTower = towerId;
            if (_towerNodes.TryGetValue(towerId, out var src) && src != null) src.SetActive(false);

            _ghost.transform.position = STWorld.ToWorld(dragPos, 0.08f);
            if (src != null) _ghost.transform.rotation = src.transform.rotation;
        }

        private void ApplyGhostMaterial(GameObject node)
        {
            var shader = Shader.Find("StarLane/Unlit");
            var mat = new Material(shader) { name = "ST-Ghost" };
            mat.color = new Color(0.55f, 0.95f, 1f, 0.5f);
            mat.SetFloat("_Glow", 0.8f);
            mat.SetFloat("_ZTest", 4f);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetFloat("_Cull", 0f);
            foreach (var r in node.GetComponentsInChildren<Renderer>(true))
            {
                r.sharedMaterial = mat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        // ---------------------------------------------------------------- 每帧
        private void LateUpdate()
        {
            float dt = UnityEngine.Time.deltaTime;
            Time += dt;

            UpdateBullets(dt);
            UpdateBlobs(dt);

            if (Run == null) return;

            UpdateUnitStates();
            TickFsm(_towerNodes);
            TickFsm(_enemyNodes);
            TickFsm(_guardNodes);
            if (_heroNode != null) _heroNode.GetComponent<STUnitView>()?.Fsm?.Tick(dt);
            if (_coreNode != null) _coreNode.GetComponent<STUnitView>()?.Fsm?.Tick(dt);
        }

        private static void TickFsm(Dictionary<string, GameObject> nodes)
        {
            foreach (var kv in nodes)
            {
                var v = kv.Value != null ? kv.Value.GetComponent<STUnitView>() : null;
                v?.Fsm?.Tick(UnityEngine.Time.deltaTime);
            }
        }

        private void UpdateUnitStates()
        {
            foreach (var e in Run.Enemies)
            {
                if (!_enemyNodes.TryGetValue(e.Id, out var node) || node == null) continue;
                var v = node.GetComponent<STUnitView>();
                var fsm = v?.Fsm;
                if (fsm == null) continue;

                bool blocked = false;
                foreach (var g in Run.Guards)
                {
                    if (g.Hp <= 0f) continue;
                    if (g.Dist > e.Dist - 8f && g.Dist < e.Dist + 60f) { blocked = true; break; }
                }

                if (fsm.Has("move"))
                {
                    if (blocked)
                    {
                        if (fsm.Current != "idle" && !fsm.OneShot) fsm.PlayOnce("move_stop");
                    }
                    else if (fsm.Current == "idle" || fsm.Current == "move_stop")
                    {
                        fsm.PlayOnce("move_start");
                    }
                    else if (!fsm.OneShot)
                    {
                        fsm.SetState("move");
                    }
                }
                else if (blocked)
                {
                    if (!fsm.OneShot) fsm.PlayOnce("attack", 0.25f);
                }
                else if (fsm.Has("idle"))
                {
                    fsm.SetState("idle");
                }

                // 血条与受击闪白
                UpdateHpBar(v, e.Hp / Mathf.Max(1f, e.HpMax), STColors.Red);
                SetFlash(node, e.Flash > 0f);
                if (v != null && v.Bob > 0f)
                {
                    var b = v.Body;
                    var lp = b.localPosition;
                    lp.y = v.BaseY + Mathf.Sin(Time * 2.6f + e.Phase) * v.Bob;
                    b.localPosition = lp;
                }
            }

            foreach (var g in Run.Guards)
            {
                if (!_guardNodes.TryGetValue(g.Id, out var node) || node == null) continue;
                var v = node.GetComponent<STUnitView>();
                var fsm = v?.Fsm;

                // 守卫 AI 朝向：
                //   · 活着时优先面向「它正在拦住的敌人」；
                //   · 没有拦敌人但正在巡逻移动 → 面向移动方向；
                //   · 都没有 → 面向车道前进方向（保持有意义的朝向，而不是固定一个方向）。
                // 这样它不会永远朝同一个方向。
                bool moving = Mathf.Abs(g.Dist - g.LastDist) > 0.4f;
                g.LastDist = g.Dist;

                float faceAngle = g.Angle;
                if (g.Hp > 0f && g.Blocking.Count > 0)
                {
                    // 找第一个正在被它阻挡的敌人，面向它
                    foreach (var e in Run.Enemies)
                    {
                        if (e.Dead || !g.Blocking.Contains(e.Id)) continue;
                        var d = e.Pos - g.Pos;
                        if (d.sqrMagnitude > 1e-4f) faceAngle = Mathf.Atan2(d.y, d.x);
                        break;
                    }
                }
                else if (!moving && g.Angle != 0f)
                {
                    faceAngle = g.Angle;
                }

                // 平滑转向，避免瞬间跳到新方向
                node.transform.rotation = Quaternion.RotateTowards(
                    node.transform.rotation,
                    Quaternion.Euler(0f, (faceAngle + Mathf.PI * 0.5f) * Mathf.Rad2Deg, 0f),
                    360f * UnityEngine.Time.deltaTime);

                if (fsm != null && fsm.Has("idle")) fsm.SetState(moving ? "walk" : "idle");

                if (v != null)
                {
                    float ratio = g.Hp / Mathf.Max(1f, g.HpMax);
                    var col = ratio > 0.35f ? STColors.Hex("5BF3C8") : STColors.Orange;
                    UpdateHpBar(v, ratio, col);
                }
            }
        }

        private static void SetFlash(GameObject node, bool on)
        {
            var v = node.GetComponent<STUnitView>();
            if (v?.Body == null) return;
            v.FlashAmount = Mathf.Lerp(v.FlashAmount, on ? 1f : 0f, 0.5f);

            // 材质是按单位共享的实例，直接改属性即可，无需每帧取 .material（那会克隆材质）
            foreach (var r in v.ModelRoot.GetComponentsInChildren<Renderer>(true))
            {
                var mat = r.sharedMaterial;
                if (mat != null && mat.HasProperty("_Flash")) mat.SetFloat("_Flash", v.FlashAmount * 0.55f);
            }
        }

        private static void UpdateHpBar(STUnitView v, float ratio, Color tint)
        {
            if (v == null) return;
            v.UpdateHpBar(ratio, tint);
        }

        // ---------------------------------------------------------------- 弹道 / 特效
        public void SpawnBullet(Vector3 from, Vector3 to, Color color, string kind = "bullet")
        {
            var go = new GameObject("Bullet-" + kind);
            go.transform.SetParent(_fxRoot, false);

            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.sharedMaterial = NewUnlitMaterial(null, color, 1f, false);

            if (kind == "beam")
            {
                mf.sharedMesh = STMesh.Cylinder(0.07f, 0.07f, 1f, 6);
                var d = to - from;
                go.transform.position = from + d * 0.5f;
                go.transform.localScale = new Vector3(1f, 1f, Mathf.Max(0.01f, d.magnitude));
                if (d.sqrMagnitude > 0.0001f) go.transform.rotation = Quaternion.LookRotation(d.normalized);
            }
            else
            {
                float r = kind == "bullet" ? 0.16f : 0.24f;
                mf.sharedMesh = STMesh.Sphere(r, 12, 6);
                go.transform.position = from;
            }

            float dur = kind == "beam" ? 0.06f : Mathf.Clamp(Vector3.Distance(from, to) / 26f, 0.06f, 0.5f);
            _bullets.Add(new BulletFx { Go = go, From = from, To = to, T = 0f, Dur = dur, Kind = kind });
        }

        public void SpawnBlob(Vector3 pos, Color color, float radius, float life = 0.5f)
        {
            var go = new GameObject("Blob");
            go.transform.SetParent(_fxRoot, false);
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = STMesh.Sphere(1f, 16, 8);
            var mr = go.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.sharedMaterial = NewUnlitMaterial(null, new Color(color.r, color.g, color.b, 0.55f), 0.8f, false);
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * Mathf.Max(0.05f, radius * 0.35f);
            _blobs.Add(new BlobFx { Go = go, T = 0f, Life = Mathf.Max(0.08f, life), Grow = radius, Color = color });
        }

        private void UpdateBullets(float dt)
        {
            for (int i = _bullets.Count - 1; i >= 0; i--)
            {
                var b = _bullets[i];
                if (b.Go == null) { _bullets.RemoveAt(i); continue; }
                b.T += dt;
                float k = Mathf.Clamp01(b.T / b.Dur);
                if (b.Kind == "beam") b.Go.SetActive(k < 1f);
                else b.Go.transform.position = Vector3.Lerp(b.From, b.To, k);
                if (k >= 1f)
                {
                    Destroy(b.Go);
                    _bullets.RemoveAt(i);
                }
            }
        }

        private void UpdateBlobs(float dt)
        {
            for (int i = _blobs.Count - 1; i >= 0; i--)
            {
                var bl = _blobs[i];
                if (bl.Go == null) { _blobs.RemoveAt(i); continue; }
                bl.T += dt;
                float k = Mathf.Clamp01(bl.T / bl.Life);
                float r = Mathf.Lerp(bl.Grow * 0.35f, bl.Grow, k);
                bl.Go.transform.localScale = Vector3.one * r;
                var mat = bl.Go.GetComponent<MeshRenderer>().sharedMaterial;
                if (mat != null) mat.color = new Color(bl.Color.r, bl.Color.g, bl.Color.b, 0.55f * (1f - k));
                if (k >= 1f)
                {
                    Destroy(bl.Go);
                    _blobs.RemoveAt(i);
                }
            }
        }

        private static void EnsureArrow(GameObject node)
        {
            if (node.transform.Find("Arrow") != null) return;
            var go = new GameObject("Arrow");
            go.transform.SetParent(node.transform, false);
            go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = BuildArrowMesh();
            var mr = go.AddComponent<MeshRenderer>();
            var shader = Shader.Find("StarLane/Unlit");
            var mat = new Material(shader) { color = STColors.Hex("FF3B3B") };
            mat.SetFloat("_Cull", 0f);
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private static Mesh _arrowMesh;

        private static Mesh BuildArrowMesh()
        {
            if (_arrowMesh != null) return _arrowMesh;
            var mesh = new Mesh { name = "ST-Arrow" };
            var verts = new List<Vector3>
            {
                new Vector3(0f, 0.25f, 3.2f),
                new Vector3(-0.35f, 0.1f, 1.8f),
                new Vector3(0.35f, 0.1f, 1.8f),
                new Vector3(0.35f, 0.4f, 1.8f),
                new Vector3(-0.35f, 0.4f, 1.8f),
            };
            var tris = new List<int> { 0, 1, 2, 0, 2, 3, 0, 3, 4, 0, 4, 1 };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.SetColors(new List<Color> { Color.white, Color.white, Color.white, Color.white, Color.white });
            mesh.RecalculateBounds();
            _arrowMesh = mesh;
            return mesh;
        }

        // ---------------------------------------------------------------- 调试输出
        public string DebugLine()
        {
            var towerStates = new Dictionary<string, int>();
            foreach (var kv in _towerNodes)
            {
                var s = kv.Value?.GetComponent<STUnitView>()?.Fsm?.Current;
                if (!string.IsNullOrEmpty(s)) towerStates[s] = (towerStates.TryGetValue(s, out var n) ? n : 0) + 1;
            }
            var enemyStates = new Dictionary<string, int>();
            foreach (var kv in _enemyNodes)
            {
                var s = kv.Value?.GetComponent<STUnitView>()?.Fsm?.Current;
                if (!string.IsNullOrEmpty(s)) enemyStates[s] = (enemyStates.TryGetValue(s, out var n) ? n : 0) + 1;
            }
            var core = _coreNode?.GetComponent<STUnitView>();
            string coreInfo = core?.Fsm != null ? core.Fsm.Current : "none";
            return $"tower_attacks={FsAttacks} hero_shots={FsHero} tower_states={Fmt(towerStates)} " +
                   $"enemy_states={Fmt(enemyStates)} core={coreInfo}";
        }

        private static string Fmt(Dictionary<string, int> d)
        {
            var parts = new List<string>();
            foreach (var kv in d) parts.Add(kv.Key + ":" + kv.Value);
            return "{" + string.Join(",", parts) + "}";
        }

        public string DebugAim()
        {
            var parts = new List<string>();
            foreach (var t in Run.Towers)
            {
                if (!_towerNodes.TryGetValue(t.Id, out var node) || node == null) continue;
                var v = node.GetComponent<STUnitView>();
                if (v?.YawBone == null) continue;

                // 炮口：优先骨骼，否则用 Yaw 骨 + 配置偏移（与 MuzzleWorld 同一规则）。
                // 多炮口（突击塔左右各一）必须取平均：单侧炮口相对 Yaw 有横向偏移，
                // 直接用单侧会得到约 23° 的固有偏差（假的 dot≈0.92）。
                Vector3 muzzle;
                if (v.Muzzles != null && v.Muzzles.Length > 0)
                {
                    var sum = Vector3.zero;
                    int n = 0;
                    foreach (var m in v.Muzzles)
                    {
                        if (m == null) continue;
                        sum += m.position;
                        n++;
                    }
                    if (n == 0) continue;
                    muzzle = sum / n;
                }
                else if (Models.TryGetValue(v.Key ?? "", out var spec))
                {
                    var dir = STWorld.DesignDirection(t.Angle);
                    muzzle = v.YawBone.position + dir * Mathf.Abs(spec.MuzzleOffset.z)
                             + new Vector3(0f, spec.MuzzleOffset.y, 0f);
                }
                else
                {
                    continue;
                }

                var barrel = muzzle - v.YawBone.position;
                barrel.y = 0f;
                if (barrel.sqrMagnitude < 1e-6f) continue;
                barrel.Normalize();

                var plat = Run.PlatformById(t.PlatformId);
                if (plat == null) continue;
                var target = PickTargetWorld(STWorld.ToWorld(plat.Pos), Run.StatsOf(t).Range / STWorld.PX);
                if (target == null) continue;

                var toTarget = STWorld.ToWorld(target.Pos, 0.9f) - v.YawBone.position;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude < 1e-6f) continue;
                toTarget.Normalize();

                parts.Add($"{t.ClassId} lv{t.Level} dot={Vector3.Dot(barrel, toTarget):F2}");
            }
            LastAimDebug = "aim: " + (parts.Count > 0 ? string.Join(", ", parts) : "no-target");
            return LastAimDebug;
        }

        // ---------------------------------------------------------------- 塔模型自检
        // 用户会不断从 Blender 重新导出模型，骨骼层级与轴转换都可能变。
        // 这里把「塔必须怎样」固化成可执行的断言，由 STAutoShot 在 -autoshot 里调用：
        // 落地为 0、动画不扭动模型根节点、只转 Yaw 时底座纹丝不动、炮口跟着转向。

        /// <summary>
        /// 朝向自检：单位机头必须指向**移动方向**（用户反复反馈「敌人倒着走」）。
        ///
        /// 判据不能是「前向矢量和自己比」（那恒等于 1），必须取模型上真实位于身前的
        /// 骨骼（grunt/hero 的手、guard 的前脚），量它相对身体中心的水平偏移，
        /// 再投影到该角度对应的世界移动方向 d=(-cosθ,0,sinθ) 上。
        /// dot≈+1 表示机头朝前；≈-1 表示倒着走 —— 这正是 ModelSpec.Yaw 漏应用时的症状。
        /// </summary>
        private static readonly Dictionary<string, string[]> FrontBoneTable =
            new Dictionary<string, string[]>
            {
                ["enemy-grunt"] = new[] { "L_Hand", "R_Hand" },
                ["enemy-runner"] = new[] { "L_Hand", "R_Hand" },
                ["guard"] = new[] { "Foot_LF", "Foot_RF" },
                ["hero"] = new[] { "Hand.L", "Hand.R" },
            };

        /// <summary>逐个角度验证某单位的机头朝向，返回 PASS/FAIL 行。</summary>
        public string CheckUnitFacing(string key, GameObject node)
        {
            var v = node != null ? node.GetComponent<STUnitView>() : null;
            if (v == null || v.Body == null) return $"FAIL  {key}  缺少 STUnitView";
            if (!FrontBoneTable.TryGetValue(key, out var names))
                return $"SKIP  {key}  没有身前骨骼参考";

            var refs = new List<Transform>();
            foreach (var n in names)
            {
                var t = FindDeep(v.ModelRoot, n);
                if (t != null) refs.Add(t);
            }
            if (refs.Count == 0) return $"SKIP  {key}  骨骼 {string.Join("/", names)} 未找到";

            float worst = float.MaxValue;
            float worstDeg = 0f;
            foreach (var deg in new[] { 0f, 90f, 180f, 270f })
            {
                float a = deg * Mathf.Deg2Rad;
                // 复刻 SyncUnits 里敌人的朝向写法
                node.transform.rotation = Quaternion.Euler(0f, (a + Mathf.PI * 0.5f) * Mathf.Rad2Deg, 0f);

                var d = STWorld.DesignDirection(a);
                var sum = Vector3.zero;
                foreach (var r in refs) sum += r.position;
                var off = sum / refs.Count - v.Body.position;
                off.y = 0f;
                float dot = off.sqrMagnitude > 1e-8f ? Vector3.Dot(off.normalized, d) : 0f;
                if (dot < worst) { worst = dot; worstDeg = deg; }
            }

            return (worst > 0.3f ? "PASS  " : "FAIL  ") + key +
                   (worst > 0.3f
                       ? $"  机头朝前 dot={worst:F2}"
                       : $"  机头与移动方向相反 dot={worst:F2}（{worstDeg:F0}° 处）");
        }

        /// <summary>生成朝向自检用的单位节点（调用方负责销毁）。</summary>
        public List<KeyValuePair<string, GameObject>> SpawnFacingProbes()
        {
            EnsureRoots();
            var list = new List<KeyValuePair<string, GameObject>>();
            int i = 0;
            foreach (var key in FrontBoneTable.Keys)
            {
                var node = SpawnModel(key);
                node.transform.SetParent(_unitRoot, false);
                node.transform.position = new Vector3(-700f - i * 12f, 0f, -700f);
                i++;
                var v = node.GetComponent<STUnitView>();
                if (v != null) v.Key = key;
                list.Add(new KeyValuePair<string, GameObject>(key, node));
            }
            return list;
        }

        /// <summary>为每个塔型生成一个远离战场的节点（自检用，调用方负责销毁）。</summary>
        public List<KeyValuePair<string, GameObject>> SpawnTowerProbes()
        {
            EnsureRoots();
            var list = new List<KeyValuePair<string, GameObject>>();
            int i = 0;
            foreach (var key in Models.Keys)
            {
                if (!key.StartsWith("tower-")) continue;
                var node = SpawnModel(key);
                node.transform.SetParent(_unitRoot, false);
                node.transform.position = new Vector3(-600f - i * 14f, 0f, -600f);
                i++;
                PlayBase(node);
                var v = node.GetComponent<STUnitView>();
                if (v != null)
                {
                    v.Key = key;
                    v.SyncShadowBlob();
                }
                list.Add(new KeyValuePair<string, GameObject>(key, node));
            }
            return list;
        }

        /// <summary>单个塔节点的姿态断言，返回 PASS/FAIL 行。</summary>
        public string CheckTowerNode(string key, GameObject node)
        {
            var v = node != null ? node.GetComponent<STUnitView>() : null;
            if (v == null || v.Body == null) return $"FAIL  {key}  缺少 STUnitView";

            var bad = new List<string>();
            float minY = ModelMinY(v.ModelRoot);

            // 1) 落地：静止姿态最低点应在 y=0
            if (Mathf.Abs(minY) > 0.02f) bad.Add($"未落地 minY={minY:F2}");

            // 2) 动画不得扭动模型根节点：塔的姿态由 ModelSpec.Pitch 唯一决定，
            //    FBX 烘焙的根轨道 RotX(-90°) 叠加后会直接翻倒。
            var rootRot0 = v.ModelRoot.localRotation;
            if (v.Anim != null)
            {
                foreach (AnimationState st in v.Anim)
                {
                    if (st.clip == null) continue;
                    if (st.name.IndexOf("attack", System.StringComparison.OrdinalIgnoreCase) < 0) continue;

                    v.Anim.Play(st.name);
                    for (int i = 0; i <= 8; i++)
                    {
                        st.time = st.clip.length * i / 8f;
                        v.Anim.Sample();
                        minY = Mathf.Min(minY, ModelMinY(v.ModelRoot));
                        if (Quaternion.Angle(rootRot0, v.ModelRoot.localRotation) > 1f)
                        {
                            bad.Add("攻击动画扭动模型根节点");
                            break;
                        }
                    }
                    v.Anim.Stop();
                    break;
                }
            }
            v.ModelRoot.localRotation = rootRot0;
            if (minY < -0.02f) bad.Add($"动画中陷地 minY={minY:F2}");

            // 3) 只转 Yaw 骨骼：底座世界旋转与位移必须不变
            var baseBone = FindDeep(v.ModelRoot, "Base");
            var yaw = v.YawBone;
            if (yaw != null && baseBone != null)
            {
                var rest = yaw.localRotation;
                var rot0 = baseBone.rotation;
                var pos0 = baseBone.position;

                foreach (var deg in new[] { 90f, 180f, 270f })
                {
                    yaw.localRotation = Quaternion.AngleAxis(deg, Vector3.up) * rest;

                    float dRot = Quaternion.Angle(rot0, baseBone.rotation);
                    float dPos = Vector3.Distance(pos0, baseBone.position);
                    if (dRot > 0.5f) bad.Add($"Yaw {deg:F0}° 时底座转了 {dRot:F1}°");
                    if (dPos > 0.01f) bad.Add($"Yaw {deg:F0}° 时底座位移 {dPos:F3}");

                    // 炮口跟着转到位（仅有炮口骨骼的塔才有这一项）
                    if (v.Muzzles != null && v.Muzzles.Length > 0)
                    {
                        var sum = Vector3.zero;
                        int n = 0;
                        foreach (var m in v.Muzzles)
                        {
                            if (m == null) continue;
                            sum += m.position - yaw.position;
                            n++;
                        }
                        if (n > 0)
                        {
                            var dir = sum / n;
                            dir.y = 0f;
                            var expect = Quaternion.AngleAxis(deg, Vector3.up) * v.YawRestDir;
                            if (dir.sqrMagnitude > 1e-6f && Vector3.Dot(dir.normalized, expect) < 0.95f)
                            {
                                bad.Add($"Yaw {deg:F0}° 时炮口没转到位");
                            }
                        }
                    }
                }
                yaw.localRotation = rest;
            }
            else if (yaw == null)
            {
                bad.Add("未找到 Yaw 骨骼");
            }

            return (bad.Count == 0 ? "PASS  " : "FAIL  ") + key +
                   (bad.Count == 0 ? $"  最低y={minY:F2}" : "  " + string.Join("；", bad));
        }

        /// <summary>模型（不含投影贴片与血条）的整体包围盒最低点。</summary>
        private static float ModelMinY(Transform root)        {
            var b = new Bounds();
            bool init = false;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                var n = r.gameObject.name;
                if (n == "ShadowBlob" || n == "HpBar") continue;
                if (!init) { b = r.bounds; init = true; }
                else b.Encapsulate(r.bounds);
            }
            return init ? b.min.y : 0f;
        }

        /// <summary>模型画廊（调试用）。</summary>
        public void BuildGallery()
        {
            EnsureRoots();

            // 画廊没有 run，需要手动摆放相机，否则相机停在原点什么都拍不到
            _camera.transform.position = new Vector3(0f, 12f, 26f);
            _camera.transform.LookAt(new Vector3(0f, 1.4f, 0f), Vector3.up);

            var keys = new List<string>(Models.Keys);
            float x = -keys.Count * 2.6f * 0.5f;
            foreach (var k in keys)
            {
                var node = SpawnModel(k);
                node.transform.SetParent(_unitRoot, false);
                node.transform.position = new Vector3(x, 0f, 0f);
                PlayBase(node);
                var v = node.GetComponent<STUnitView>();
                if (v != null)
                {
                    v.Key = k;
                    v.SyncShadowBlob();
                    v.SetShadowScale(1.2f);
                }
                x += 2.6f;
            }
        }
    }
}
