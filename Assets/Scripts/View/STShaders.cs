// 星轨防线 · 着色器解析
//
// 为什么需要这一层：Shader.Find("StarLane/Unlit") 在构建后的播放器里，
// 只有当材质被场景/Resources 引用或加入 Always Included Shaders 时才返回有效对象。
// 本项目所有材质都是运行时 new Material(...) 创建的，因此曾经出现
// “编辑器里正常、打包后射程圆/血条/弹道全部消失”的问题（Shader.Find 返回 null，
// Unity 回退到内置的粉红错误材质，或因 alpha 而被完全忽略）。
//
// 这里统一走一个缓存 + 校验 + 明确告警的入口，并对缺失情况提供可用回退。

using System.Collections.Generic;
using UnityEngine;

namespace StarLane
{
    public static class STShaders
    {
        private static readonly Dictionary<string, Shader> Cache = new Dictionary<string, Shader>();

        private const string UnlitName = "StarLane/Unlit";
        private const string ModelLitName = "StarLane/ModelLit";

        /// <summary>无光照材质（弹道 / 特效 / 血条 / 射程 / 地图）。</summary>
        public static Shader Unlit => Resolve(UnlitName, "Unlit/Transparent");

        /// <summary>模型赛璐璐材质。</summary>
        public static Shader ModelLit => Resolve(ModelLitName, "Unlit/Texture");

        private static Shader Resolve(string name, string fallbackName)
        {
            if (Cache.TryGetValue(name, out var s) && s != null) return s;

            s = Shader.Find(name);
            if (s == null)
            {
                Debug.LogError($"[STShaders] 找不到着色器 {name}。" +
                               "请确认它已加入 Project Settings → Graphics → Always Included Shaders。");
                s = Shader.Find(fallbackName);
            }
            if (s == null) s = Shader.Find("Sprites/Default");

            Cache[name] = s;
            return s;
        }

        public static Material NewUnlit(Texture texture, Color color, string name = "ST-Unlit",
            float glow = 0f, bool depthTest = true, bool zWrite = false)
        {
            var mat = new Material(Unlit) { name = name };
            if (texture != null) mat.mainTexture = texture;
            mat.color = color;
            if (mat.HasProperty("_Glow")) mat.SetFloat("_Glow", glow);
            if (mat.HasProperty("_ZTest")) mat.SetFloat("_ZTest", depthTest ? 4f : 8f);
            if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", zWrite ? 1f : 0f);
            if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 0f);
            // 关键：确保混合模式可透明。某些回退着色器没有 _SrcBlend/_DstBlend 属性，
            // 这里只在属性存在时设置，避免静默失效。
            if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            return mat;
        }
    }
}
