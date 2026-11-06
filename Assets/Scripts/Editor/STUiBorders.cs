// 九宫格边距修复工具
//
// Unity 的 Sliced 依赖 spriteBorder。AssetPostprocessor 只在首次导入时生效，
// 已存在的资源不会重跑，所以这里提供一个显式的修复入口：
// 读取 PNG 尺寸后写入 border，并强制重新导入一次。

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace StarLane.EditorTools
{
    public static class STUiBorders
    {
        // 边距 = 贴图短边 × RATIO（与 STAssetPostprocessor.BorderRatio 保持一致）。
        // 运行时 STUi.FitSlice 会把四角按控件尺寸等比缩放，所以这里给的是
        // 素材自身的相对厚度；RATIO = 0.22 时内容区保留 56% 短边。
        private static readonly System.Collections.Generic.HashSet<string> BorderNames =
            new System.Collections.Generic.HashSet<string>
            {
                "panel", "panel-gold", "frame-plate", "card",
                "button-primary", "button-gold", "chip", "bar",
            };

        private const float BorderRatio = 0.22f;

        public static void FixAll()
        {
            const string dir = "Assets/Resources/Art/UI";
            int fixedCount = 0;

            foreach (var name in BorderNames)
            {
                var path = $"{dir}/{name}.png";
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                {
                    Debug.LogWarning("[STUiBorders] 找不到 " + path);
                    continue;
                }

                // 先确保是 Sprite，再读尺寸算边距上限
                if (importer.textureType != TextureImporterType.Sprite)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.SaveAndReimport();
                }

                importer.GetSourceTextureWidthAndHeight(out int w, out int h);
                float shortSide = Mathf.Min(w, h);
                float m = Mathf.Min(shortSide * BorderRatio, shortSide * 0.45f);

                importer.spriteBorder = new Vector4(m, m, m, m);
                importer.spritePixelsPerUnit = 100f;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();

                Debug.Log($"[STUiBorders] {name} {w}x{h} border={m}");
                fixedCount++;
            }

            AssetDatabase.Refresh();
            Debug.Log($"[STUiBorders] 完成，处理 {fixedCount} 张");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }
    }
}
