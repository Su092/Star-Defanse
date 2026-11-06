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
        // 边距按**比例**给出（占贴图短边的百分比），再乘实际尺寸写入 spriteBorder。
        // 不能写死像素：素材可能以 512/1024/1254 等不同分辨率生成，
        // 写死像素会让边框在拉伸时变形（九宫格切错位置）。
        private static readonly Dictionary<string, float> Ratio = new Dictionary<string, float>
        {
            { "panel", 0.086f },
            { "panel-gold", 0.086f },
            { "frame-plate", 0.070f },
            { "card", 0.070f },
            { "button-primary", 0.043f },
            { "button-gold", 0.036f },
            { "chip", 0.070f },
            { "bar", 0.051f },
        };

        public static void FixAll()
        {
            const string dir = "Assets/Resources/Art/UI";
            int fixedCount = 0;

            foreach (var kv in Ratio)
            {
                var path = $"{dir}/{kv.Key}.png";
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
                float m = Mathf.Min(shortSide * kv.Value, shortSide * 0.45f);

                importer.spriteBorder = new Vector4(m, m, m, m);
                importer.spritePixelsPerUnit = 100f;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();

                Debug.Log($"[STUiBorders] {kv.Key} {w}x{h} border={m}");
                fixedCount++;
            }

            AssetDatabase.Refresh();
            Debug.Log($"[STUiBorders] 完成，处理 {fixedCount} 张");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }
    }
}
