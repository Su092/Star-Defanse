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
        // 边距按素材设计尺寸给出（单位：像素，写入 spriteBorder）
        private static readonly Dictionary<string, float> Table = new Dictionary<string, float>
        {
            { "panel", 44f },
            { "panel-gold", 44f },
            { "frame-plate", 36f },
            { "card", 36f },
            { "button-primary", 22f },
            { "button-gold", 18f },
            { "chip", 36f },
            { "bar", 26f },
        };

        public static void FixAll()
        {
            const string dir = "Assets/Resources/Art/UI";
            int fixedCount = 0;

            foreach (var kv in Table)
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
                float limit = Mathf.Min(w, h) * 0.45f;
                float m = Mathf.Min(kv.Value, limit);

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
