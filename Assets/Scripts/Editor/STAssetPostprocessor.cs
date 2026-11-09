// 星轨防线 · 资源导入规则
//
// 统一在导入阶段设置纹理类型与模型动画类型，避免依赖手工在 Inspector 里点。
// 规则只作用于 Resources/Art 下的资源，不影响工程内其他内容。

using UnityEditor;
using UnityEngine;

namespace StarLane.EditorTools
{
    public class STAssetPostprocessor : AssetPostprocessor
    {
        private const string ArtRoot = "Assets/Resources/Art/";
        private const string FontRoot = "Assets/Resources/Fonts/";

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(ArtRoot)) return;

            var importer = (TextureImporter)assetImporter;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.sRGBTexture = true;
            importer.maxTextureSize = 2048;
            importer.spritePixelsPerUnit = 100f;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spriteBorder = Vector4.zero;

            if (assetPath.Contains("/Art/UI/"))
            {
                ApplyBorder(importer, assetPath);
            }

            if (assetPath.Contains("/Art/UI/") || assetPath.Contains("/Art/Sprites/"))
            {
                // 界面与战斗精灵都要保留透明通道，且不压缩出色带
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = 1024;
                importer.filterMode = FilterMode.Bilinear;
            }
            else
            {
                // 场景底图 / 立绘：体积优先，质量其次
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.maxTextureSize = 2048;
            }
        }

        // ---------------------------------------------------------------- 九宫格边距
        // Unity 的 Sliced 依赖 spriteBorder，未设置时会退化成拉伸（边框被拉变形）。
        // 注意：AssetPostprocessor 对已导入的资源不会重跑，批量设置见 STBuild.FixUiBorders。
        //
        // 这里的值由 tools/ui_borders.py 从 AI 生成的素材**实测**得出
        // （沿四条边各取 9 条扫描线，去掉极值取中位数），再经 tools/ui_install.py
        // 按实际缩放系数换算后写入 tools/ui-sprite-borders.json。
        //
        // 为什么必须四边独立：AI 生成的边框天然不等厚（panel-a 上 61 下 53、
        // plate-b 左 134 右 133），而 Unity 的 spriteBorder 正是
        // (left, bottom, right, top) 四值语义，照实测写入即可。
        // 早期版本用「短边 × 固定比例」给四个相等值，结果某一侧的白带会进了
        // 可拉伸区，拉伸时被抹开 —— 那就是肉眼看到的「UI 被压扁」。
        private const string BorderJson = "tools/ui-sprite-borders.json";

        private static System.Collections.Generic.Dictionary<string, Vector4> _borderCache;

        private static System.Collections.Generic.Dictionary<string, Vector4> LoadSpriteBorders()
        {
            if (_borderCache != null) return _borderCache;
            _borderCache = new System.Collections.Generic.Dictionary<string, Vector4>();

            // Application.dataPath 形如 <proj>/Assets，工程根是它的父目录
            string root = System.IO.Path.GetDirectoryName(Application.dataPath);
            string path = System.IO.Path.Combine(root ?? "", BorderJson);
            if (!System.IO.File.Exists(path))
            {
                Debug.LogWarning("[ST] 缺少 " + BorderJson + "，九宫格边距退回等比兜底");
                return _borderCache;
            }

            try
            {
                string json = System.IO.File.ReadAllText(path);
                var wrapper = JsonUtility.FromJson<BorderFile>(json);
                if (wrapper?.items != null)
                {
                    foreach (var it in wrapper.items)
                        _borderCache[it.name] = new Vector4(it.left, it.bottom, it.right, it.top);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[ST] 解析 " + BorderJson + " 失败：" + e.Message);
            }
            return _borderCache;
        }

        [System.Serializable]
        private class BorderFile { public BorderEntry[] items; }

        [System.Serializable]
        private class BorderEntry
        {
            public string name;
            public float left, bottom, right, top;
        }

        private static void ApplyBorder(TextureImporter importer, string path)
        {
            var name = System.IO.Path.GetFileNameWithoutExtension(path);
            if (!BorderNames.Contains(name)) return;

            var table = LoadSpriteBorders();
            if (!table.TryGetValue(name, out var border))
            {
                // 兜底：等比取短边 22%，至少不会把图完全压扁
                importer.GetSourceTextureWidthAndHeight(out int w0, out int h0);
                float s0 = Mathf.Min(w0, h0) * 0.22f;
                border = new Vector4(s0, s0, s0, s0);
            }

            importer.GetSourceTextureWidthAndHeight(out int w, out int h);
            // 保险：任意两片之和必须小于尺寸，否则 Unity 会强行压缩
            if (border.x + border.z >= w)
            {
                float f = w * 0.9f / (border.x + border.z);
                border.x *= f; border.z *= f;
            }
            if (border.y + border.w >= h)
            {
                float f = h * 0.9f / (border.y + border.w);
                border.y *= f; border.w *= f;
            }
            importer.spriteBorder = border;
        }

        private static readonly System.Collections.Generic.HashSet<string> BorderNames =
            new System.Collections.Generic.HashSet<string>
            {
                "panel", "panel-gold", "frame-plate", "card",
                "button-primary", "button-gold", "chip", "bar",
                "plate-thin",
            };

        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(ArtRoot + "Models/")) return;

            var importer = (ModelImporter)assetImporter;

            // 动画与模型分文件导出（Anim_*.fbx / Modle_*.fbx），统一走 Legacy 动画，
            // 这样可以在运行时把独立动画片段挂到模型的 Animation 组件上播放。
            importer.animationType = ModelImporterAnimationType.Legacy;
            importer.importAnimation = true;
            importer.animationCompression = ModelImporterAnimationCompression.KeyframeReduction;
            importer.importBlendShapes = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importVisibility = false;
            importer.importConstraints = false;

            // 材质在运行时按同目录的 *_basecolor.jpg 重建，这里不导入 FBX 内嵌材质
            importer.materialImportMode = ModelImporterMaterialImportMode.None;

            importer.useFileScale = true;
            importer.globalScale = 1f;
            importer.bakeAxisConversion = false;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.None;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.isReadable = false;

            var fileName = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            // 动画文件里没有网格，关闭网格优化可减少导入开销
            if (fileName.StartsWith("Anim_"))
            {
                importer.optimizeMeshPolygons = false;
                importer.optimizeMeshVertices = false;
            }
        }

        private void OnPreprocessAsset()
        {
            if (!assetPath.StartsWith(FontRoot)) return;
            if (assetImporter is not TrueTypeFontImporter font) return;
            font.fontTextureCase = FontTextureCase.Dynamic;
            font.includeFontData = true;
        }

        /// <summary>
        /// 剔除动画片段里不该由动画驱动的轨道：
        ///   1. 瞄准骨骼本身（Yaw）—— 瞄准由代码每帧写 localRotation；
        ///   2. 塔类动画的**模型根节点** —— 根轨道是把 FBX 轴转换烘焙进去的
        ///      RotX(-90°)，塔的姿态已由 ModelSpec.Pitch 校正，两者叠加会翻倒。
        ///
        /// 匹配 Yaw 必须是**路径末段恰好是 Yaw**，不能用子串匹配：
        /// 2026-11-06 重新导出的突击塔把 Head / Barrel / Muzzle 全部挂在 Yaw 之下，
        /// 按子串匹配会把 60 条后代轨道一并删掉，整段攻击动画（后坐、炮管回弹、
        /// 炮口闪光缩放）被清空，只剩根节点与 Base 的轨道。
        /// </summary>
        private void OnPostprocessModel(GameObject root)
        {
            if (!assetPath.StartsWith(ArtRoot + "Models/")) return;
            var fileName = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            if (!fileName.StartsWith("Anim_")) return;

            // 塔：姿态由 Pitch 决定，动画不该再旋转根节点
            bool stripRoot = fileName.StartsWith("Anim_Tower_", System.StringComparison.OrdinalIgnoreCase);

            foreach (var clip in AnimationUtility.GetAnimationClips(root))
            {
                if (clip == null) continue;
                var bindings = AnimationUtility.GetCurveBindings(clip);
                foreach (var b in bindings)
                {
                    bool isAim = IsAimBonePath(b.path);
                    bool isRoot = stripRoot && b.path.Length == 0;
                    if (!isAim && !isRoot) continue;

                    var curve = AnimationUtility.GetEditorCurve(clip, b);
                    AnimationUtility.SetEditorCurve(clip, b, null);
                    Debug.Log($"[ST] 剔除{(isRoot ? "根" : "Yaw")}轨道 {fileName}/{clip.name} -> " +
                              $"{(b.path.Length == 0 ? "<root>" : b.path)}.{b.propertyName} " +
                              $"(keys={curve?.length ?? 0})");
                }
            }
        }

        /// <summary>路径的**最后一段**是 Yaw 骨骼（不含其子节点）。</summary>
        private static bool IsAimBonePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            int slash = path.LastIndexOf('/');
            var leaf = slash >= 0 ? path.Substring(slash + 1) : path;
            return leaf.Equals("Yaw", System.StringComparison.OrdinalIgnoreCase);
        }
    }
}

