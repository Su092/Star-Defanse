// 星轨防线 · 打包与资源校验（对应 AGENTS.md 里的 Unity CLI 命令）
//
//   StarLane.EditorTools.STBuild.VerifyResources  资源完整性（图片可加载、模型/动画/字体齐全、数据一致）
//   StarLane.EditorTools.STBuild.BuildPlayer      Windows x64 打包到 ST/Build/ST.exe

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
namespace StarLane.EditorTools
{
    public static class STBuild
    {
        private static readonly List<string> Lines = new List<string>();
        private static int _fail;

        // ---------------------------------------------------------------- 资源校验
        public static void VerifyResources()
        {
            _fail = 0;
            Lines.Clear();

            STData.Load();
            FixUiBorders();          // 顺带校准九宫格边距（幂等，仅写 spriteBorder）
            CheckData();
            CheckSprites();
            CheckModels();
            CheckFonts();

            Lines.Add("---");
            Lines.Add(_fail == 0 ? "RESULT 资源校验通过" : $"RESULT 资源校验失败 {_fail} 项");
            STSetup.WriteLog("verify.log", Lines);
            STSetup.Finish(_fail == 0 ? 0 : 1);
        }

        /// <summary>
        /// 校准 UI 九宫格边距（幂等）。
        ///
        /// 边距 = 贴图短边 × 0.22，依据见 STUiBorders 的注释。这里挂在资源校验里
        /// 一起跑，避免依赖单独的 executeMethod 入口（那条路径在 batchmode 下
        /// 偶发解析不到类）。
        /// </summary>
        private static void FixUiBorders()
        {
            const string dir = "Assets/Resources/Art/UI";
            string[] names =
            {
                "panel", "panel-gold", "frame-plate", "card",
                "button-primary", "button-gold", "chip", "bar",
                "plate-thin",
            };

            int fixedCount = 0;
            foreach (var n in names)
            {
                var importer = AssetImporter.GetAtPath($"{dir}/{n}.png") as TextureImporter;
                if (importer == null) continue;

                importer.GetSourceTextureWidthAndHeight(out int w, out int h);
                float shortSide = Mathf.Min(w, h);
                float m = Mathf.Min(shortSide * 0.22f, shortSide * 0.45f);
                var want = new Vector4(m, m, m, m);

                // 一致就跳过，避免每次都触发重新导入（打包会变慢）
                if ((importer.spriteBorder - want).sqrMagnitude < 0.01f) continue;

                importer.spriteBorder = want;
                importer.spritePixelsPerUnit = 100f;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
                fixedCount++;
            }

            if (fixedCount > 0)
            {
                AssetDatabase.Refresh();
                Debug.Log($"[ST] 九宫格边距已校准 {fixedCount} 张");
            }
        }

        private static void Check(string name, bool ok, string detail = null)
        {
            if (!ok) _fail++;
            Lines.Add((ok ? "PASS  " : "FAIL  ") + name + (detail != null ? "   " + detail : ""));
        }

        private static void CheckData()
        {
            Check("数据已加载", STData.Root != null);
            if (STData.Root == null) return;

            Check("10 个波次", STData.Waves.Count == 10, STData.Waves.Count.ToString());
            Check("4 个关卡", STData.Levels.Count == 4, STData.Levels.Count.ToString());
            Check("4 个职业", STData.Classes.Count == 4, STData.Classes.Count.ToString());
            Check("8 名干员", STData.Operatives.Count == 8, STData.Operatives.Count.ToString());
            Check("5 类敌人", STData.Enemies.Count == 5, STData.Enemies.Count.ToString());
            Check("词条表非空", STData.Affixes.Count > 0, STData.Affixes.Count.ToString());
            Check("保底词条存在", STData.FillerAffix != null && !string.IsNullOrEmpty(STData.FillerAffix.id));

            // 数据 → 资源的引用完整性
            foreach (var op in STData.Operatives)
            {
                Check($"干员 {op.id} 的职业存在", STData.GetClass(op.classId) != null, op.classId);
                Check($"干员 {op.id} 的立绘存在",
                    Resources.Load<Sprite>("Art/Backgrounds/" + op.portrait) != null, op.portrait);
                foreach (var a in op.affixes)
                {
                    Check($"干员 {op.id} 的词条 {a} 存在", STData.GetAffix(a) != null);
                }
            }

            foreach (var lv in STData.Levels)
            {
                Check($"关卡 {lv.id} 有路径点", lv.path.Count >= 8, lv.path.Count.ToString());
                Check($"关卡 {lv.id} 有塔台", lv.platforms.Count >= 12, lv.platforms.Count.ToString());
                Check($"关卡 {lv.id} 的地图贴图存在",
                    Resources.Load<Texture2D>("Art/Backgrounds/" + lv.map) != null, lv.map);
                Check($"关卡 {lv.id} 的缩略图存在",
                    Resources.Load<Sprite>("Art/Backgrounds/level-" + lv.id) != null);
            }
        }

        private static void CheckSprites()
        {
            string[] sprites =
            {
                "core", "enemy-boss", "enemy-elite", "enemy-grunt", "enemy-runner", "enemy-tank",
                "guard", "hero", "platform", "platform-locked", "spawn-gate",
            };
            foreach (var s in sprites) Check("精灵 " + s, Resources.Load<Sprite>("Art/Sprites/" + s) != null);

            foreach (var cls in STData.Classes)
            {
                for (int lv = 1; lv <= 4; lv++)
                {
                    var name = $"tower-{cls.id}-l{lv}";
                    Check("精灵 " + name, Resources.Load<Sprite>("Art/Sprites/" + name) != null);
                }
            }

            string[] ui =
            {
                "panel", "panel-gold", "button-primary", "button-gold", "chip", "bar", "card",
                "plate-thin",
                "icon-star", "icon-coin", "icon-crystal", "icon-pause", "icon-speed", "icon-help",
                "icon-arrow", "icon-book", "frame-plate", "frame-round",
            };
            foreach (var u in ui) Check("UI " + u, Resources.Load<Sprite>("Art/UI/" + u) != null);

            Check("菜单 KV", Resources.Load<Sprite>("Art/Backgrounds/menu-kv") != null);
            Check("精英威胁插画", Resources.Load<Sprite>("Art/Backgrounds/elite-threat") != null);
            Check("BOSS 威胁插画", Resources.Load<Sprite>("Art/Backgrounds/boss-threat") != null);
        }

        private static readonly (string folder, string model, string anim, string baseColor)[] ModelTable =
        {
            ("tower_assault", "Modle_Tower_Assault", "Anim_Tower_Assault", "tower-assault_basecolor"),
            ("tower_sniper", "Modle_Tower_Sniper", "", "tower-sniper_basecolor"),
            ("tower_siege", "Modle_Tower_Siepe", "", "tower-siege_basecolor"),
            ("tower_intercept", "Modle_Tower_Intercept", "", "tower-intercept_basecolor"),
            ("enemy_grunt", "Modle_Enemy_Grunt", "Anim_Enemy_Grunt", "enemy-grunt_basecolor"),
            ("enemy_runner", "Modle_Enemy_Runner", "", "enemy-runner_basecolor"),
            ("enemy_tank", "Modle_Enemy_Tank", "", "enemy-tank_basecolor"),
            ("enemy_elite", "Modle_Enemy_Elite", "Anim_Enemy_Elite", "enemy-elite_basecolor"),
            ("enemy_boss", "Modle_Enemy_Annihilation", "Anim_Enemy_Annihilation", "enemy-boss_basecolor"),
            ("guard", "Modle_Enemy_Guard", "Anim_Enemy_Guard", "guard_basecolor"),
            ("hero", "Modle_Chr_Hero", "", "hero_basecolor"),
            ("core", "Modle_Prop_Crystal", "", "core_basecolor"),
            ("spawn", "Modle_Prop_Spawn", "", "spawn-gate_basecolor"),
        };

        private static void CheckModels()
        {
            foreach (var (folder, model, anim, baseColor) in ModelTable)
            {
                var go = Resources.Load<GameObject>($"Art/Models/{folder}/{model}");
                Check($"模型 {folder}/{model}", go != null);
                if (go == null) continue;

                int meshes = go.GetComponentsInChildren<MeshFilter>(true).Length
                             + go.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length;
                Check($"模型 {model} 含网格", meshes > 0, meshes + " 个");

                Check($"模型 {folder} 的 basecolor 贴图",
                    Resources.Load<Texture2D>($"Art/Models/{folder}/{baseColor}") != null, baseColor);

                if (!string.IsNullOrEmpty(anim))
                {
                    var clips = Resources.LoadAll<AnimationClip>($"Art/Models/{folder}/{anim}");
                    Check($"动画 {anim}", clips != null && clips.Length > 0, (clips?.Length ?? 0) + " 段");
                }
            }

            CheckTowerAnimTracks();
        }

        /// <summary>
        /// 塔动画的轨道约束（2026-11-06 回归的护栏）。
        ///
        /// FBX 会把轴转换烘焙成一条「模型根节点 RotX(-90°)」轨道 —— 敌人靠它立起来，
        /// 但塔的姿态已由 ModelSpec.Pitch 校正，动画再转根节点就会翻倒并陷进地面。
        /// 瞄准骨骼 Yaw 同理：由代码每帧写 localRotation，动画若也驱动会被扭回零位。
        /// 这两类轨道必须被 STAssetPostprocessor 剔除，而后代骨骼（Head / Barrel /
        /// Muzzle）的轨道必须保留 —— 它们承载后坐与炮口闪光。
        /// </summary>
        private static void CheckTowerAnimTracks()
        {
            foreach (var (folder, model, anim, _) in ModelTable)
            {
                if (!folder.StartsWith("tower_") || string.IsNullOrEmpty(anim)) continue;

                var clips = Resources.LoadAll<AnimationClip>($"Art/Models/{folder}/{anim}");
                if (clips == null || clips.Length == 0) continue;

                int root = 0, aim = 0, descendants = 0;
                foreach (var c in clips)
                {
                    foreach (var b in AnimationUtility.GetCurveBindings(c))
                    {
                        if (b.path.Length == 0) { root++; continue; }
                        var leaf = b.path.Substring(b.path.LastIndexOf('/') + 1);
                        if (leaf.Equals("Yaw", System.StringComparison.OrdinalIgnoreCase)) aim++;
                        else descendants++;
                    }
                }

                Check($"动画 {anim} 不驱动模型根节点", root == 0, root + " 条根轨道");
                Check($"动画 {anim} 不驱动瞄准骨骼 Yaw", aim == 0, aim + " 条 Yaw 轨道");
                Check($"动画 {anim} 保留后代骨骼轨道", descendants > 0, descendants + " 条");
            }
        }

        private static void CheckFonts()
        {
            Check("常规字体", STAssets.FontRegular != null);
            Check("粗体字体", STAssets.FontBold != null);
        }

        // ---------------------------------------------------------------- 打包
        public static void BuildPlayer()
        {
            var output = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Build", "ST.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(output));

            var options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/Main.unity" },
                locationPathName = output,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };

            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;

            var lines = new List<string>
            {
                "RESULT " + summary.result,
                "OUTPUT " + summary.outputPath,
                "SIZE   " + summary.totalSize,
                "ERRORS " + summary.totalErrors,
                "TIME   " + summary.totalTime,
            };
            STSetup.WriteLog("build.log", lines);
            Debug.Log("[STBuild] " + string.Join(" | ", lines));

            STSetup.Finish(summary.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
