// 把项目的自定义着色器加入 Always Included Shaders。
//
// 本工程所有材质都在运行时通过 Shader.Find 创建，若着色器没有被任何资源引用、
// 也没加入 Always Included，构建后的播放器里 Shader.Find 会返回 null，
// 表现为：编辑器正常、打包后射程圆/血条/弹道/模型全部异常或消失。
//
// 该入口可重复执行（幂等）。

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace StarLane.EditorTools
{
    public static class STShaderSetup
    {
        private static readonly string[] CustomShaderPaths =
        {
            "Assets/Resources/Shaders/STUnlit.shader",
            "Assets/Resources/Shaders/STModelLit.shader",
        };

        public static void Apply()
        {
            var graphics = AssetDatabase.LoadAssetAtPath<Object>(
                "ProjectSettings/GraphicsSettings.asset");
            if (graphics == null)
            {
                Debug.LogError("[STShaderSetup] 找不到 GraphicsSettings.asset");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }

            var so = new SerializedObject(graphics);
            var list = so.FindProperty("m_AlwaysIncludedShaders");
            if (list == null)
            {
                Debug.LogError("[STShaderSetup] 找不到 m_AlwaysIncludedShaders");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }

            var existing = new HashSet<string>();
            for (int i = 0; i < list.arraySize; i++)
            {
                var sh = list.GetArrayElementAtIndex(i).objectReferenceValue as Shader;
                if (sh != null) existing.Add(sh.name);
            }

            int added = 0;
            foreach (var path in CustomShaderPaths)
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
                if (shader == null)
                {
                    Debug.LogWarning("[STShaderSetup] 缺失 " + path);
                    continue;
                }
                if (existing.Contains(shader.name)) continue;

                list.InsertArrayElementAtIndex(list.arraySize);
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = shader;
                existing.Add(shader.name);
                added++;
                Debug.Log("[STShaderSetup] 已加入 Always Included: " + shader.name);
            }

            so.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
            Debug.Log($"[STShaderSetup] 完成，新增 {added} 个着色器");

            // 校验
            for (int i = 0; i < list.arraySize; i++)
            {
                var sh = list.GetArrayElementAtIndex(i).objectReferenceValue as Shader;
                if (sh != null && sh.name.StartsWith("StarLane/"))
                {
                    Debug.Log($"[STShaderSetup] 校验通过: {sh.name}");
                }
            }

            if (Application.isBatchMode) EditorApplication.Exit(0);
        }
    }
}
