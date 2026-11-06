// 星轨防线 · 程序化网格（对应 Godot 的 PlaneMesh / CylinderMesh / TorusMesh / SphereMesh）
//
// 战场里的底盘、台面、光环、弹道、血条全部由这里生成网格，
// 顶点色用于让同一个材质渲染不同颜色而不产生材质实例爆炸。

using System.Collections.Generic;
using UnityEngine;

namespace StarLane
{
    public static class STMesh
    {
        private static readonly Dictionary<string, Mesh> Cache = new Dictionary<string, Mesh>();

        /// <summary>
        /// 平面（设计像素矩形 → 世界坐标四边形）。
        /// 注意：设计 X 在 Unity 里取反，因此这里显式构造顶点，保证贴图不被镜像。
        /// </summary>
        public static Mesh Plane(float designX, float designY, float designW, float designH, float y = 0f)
        {
            string key = $"plane:{designX:F1}:{designY:F1}:{designW:F1}:{designH:F1}:{y:F3}";
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;

            var lt = STWorld.ToWorld(new Vector2(designX, designY), y);
            var rt = STWorld.ToWorld(new Vector2(designX + designW, designY), y);
            var rb = STWorld.ToWorld(new Vector2(designX + designW, designY + designH), y);
            var lb = STWorld.ToWorld(new Vector2(designX, designY + designH), y);

            var mesh = new Mesh { name = "ST-Plane" };
            mesh.SetVertices(new List<Vector3> { lt, rt, rb, lb });
            // v=1 在贴图顶部，与设计坐标 y 向下对应
            mesh.SetUVs(0, new List<Vector2>
            {
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f), new Vector2(0f, 0f),
            });
            mesh.SetColors(new List<Color> { Color.white, Color.white, Color.white, Color.white });
            // 双面渲染已在着色器里关闭剔除，这里保证两个方向的三角形都存在会重复绘制，
            // 因此只保留一组，靠 Cull Off 处理背面。
            mesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
            mesh.RecalculateBounds();
            Cache[key] = mesh;
            return mesh;
        }

        /// <summary>以世界坐标中心与尺寸构造的平面（朝向 +Z，用于背景板）。</summary>
        public static Mesh Quad(Vector2 size)
        {
            string key = $"quad:{size.x:F3}:{size.y:F3}";
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;

            float hx = size.x * 0.5f;
            float hy = size.y * 0.5f;
            var mesh = new Mesh { name = "ST-Quad" };
            mesh.SetVertices(new List<Vector3>
            {
                new Vector3(-hx, hy, 0f),
                new Vector3(hx, hy, 0f),
                new Vector3(hx, -hy, 0f),
                new Vector3(-hx, -hy, 0f),
            });
            mesh.SetUVs(0, new List<Vector2>
            {
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f), new Vector2(0f, 0f),
            });
            mesh.SetColors(new List<Color> { Color.white, Color.white, Color.white, Color.white });
            mesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
            mesh.RecalculateBounds();
            Cache[key] = mesh;
            return mesh;
        }

        /// <summary>圆柱（台面底座）。</summary>
        public static Mesh Cylinder(float topRadius, float bottomRadius, float height, int segments = 12)
        {
            string key = $"cyl:{topRadius:F3}:{bottomRadius:F3}:{height:F3}:{segments}";
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;

            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            float hy = height * 0.5f;

            for (int i = 0; i <= segments; i++)
            {
                float a = (float)i / segments * Mathf.PI * 2f;
                float cos = Mathf.Cos(a), sin = Mathf.Sin(a);
                verts.Add(new Vector3(cos * topRadius, hy, sin * topRadius));
                verts.Add(new Vector3(cos * bottomRadius, -hy, sin * bottomRadius));
                uvs.Add(new Vector2((float)i / segments, 1f));
                uvs.Add(new Vector2((float)i / segments, 0f));
            }

            for (int i = 0; i < segments; i++)
            {
                int b = i * 2;
                tris.Add(b); tris.Add(b + 1); tris.Add(b + 2);
                tris.Add(b + 2); tris.Add(b + 1); tris.Add(b + 3);
            }

            // 顶盖
            int topCenter = verts.Count;
            verts.Add(new Vector3(0f, hy, 0f));
            uvs.Add(new Vector2(0.5f, 0.5f));
            for (int i = 0; i < segments; i++)
            {
                float a0 = (float)i / segments * Mathf.PI * 2f;
                float a1 = (float)(i + 1) / segments * Mathf.PI * 2f;
                int i0 = verts.Count;
                verts.Add(new Vector3(Mathf.Cos(a0) * topRadius, hy, Mathf.Sin(a0) * topRadius));
                verts.Add(new Vector3(Mathf.Cos(a1) * topRadius, hy, Mathf.Sin(a1) * topRadius));
                uvs.Add(new Vector2(0.5f, 0.5f));
                uvs.Add(new Vector2(0.5f, 0.5f));
                tris.Add(topCenter); tris.Add(i0 + 1); tris.Add(i0);
            }

            var mesh = new Mesh { name = "ST-Cylinder" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(Repeat(Color.white, verts.Count));
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            Cache[key] = mesh;
            return mesh;
        }

        /// <summary>圆环（台面光环 / 射程圈）。</summary>
        public static Mesh Torus(float innerRadius, float outerRadius, int segments = 48, int tubeSegments = 6)
        {
            string key = $"torus:{innerRadius:F3}:{outerRadius:F3}:{segments}:{tubeSegments}";
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;

            float mid = (innerRadius + outerRadius) * 0.5f;
            float tube = (outerRadius - innerRadius) * 0.5f;

            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            for (int i = 0; i <= segments; i++)
            {
                float a = (float)i / segments * Mathf.PI * 2f;
                var center = new Vector3(Mathf.Cos(a) * mid, 0f, Mathf.Sin(a) * mid);
                var outward = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                for (int j = 0; j <= tubeSegments; j++)
                {
                    float b = (float)j / tubeSegments * Mathf.PI * 2f;
                    var offset = outward * (Mathf.Cos(b) * tube) + Vector3.up * (Mathf.Sin(b) * tube);
                    verts.Add(center + offset);
                    uvs.Add(new Vector2((float)i / segments, (float)j / tubeSegments));
                }
            }

            int ring = tubeSegments + 1;
            for (int i = 0; i < segments; i++)
            {
                for (int j = 0; j < tubeSegments; j++)
                {
                    int a = i * ring + j;
                    int b = a + ring;
                    tris.Add(a); tris.Add(b); tris.Add(a + 1);
                    tris.Add(a + 1); tris.Add(b); tris.Add(b + 1);
                }
            }

            var mesh = new Mesh { name = "ST-Torus" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(Repeat(Color.white, verts.Count));
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            Cache[key] = mesh;
            return mesh;
        }

        /// <summary>球体（弹丸 / 爆炸）。</summary>
        public static Mesh Sphere(float radius, int segments = 16, int rings = 8)
        {
            string key = $"sphere:{radius:F3}:{segments}:{rings}";
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;

            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            for (int j = 0; j <= rings; j++)
            {
                float v = (float)j / rings;
                float phi = v * Mathf.PI;
                for (int i = 0; i <= segments; i++)
                {
                    float u = (float)i / segments;
                    float theta = u * Mathf.PI * 2f;
                    verts.Add(new Vector3(
                        Mathf.Sin(phi) * Mathf.Cos(theta) * radius,
                        Mathf.Cos(phi) * radius,
                        Mathf.Sin(phi) * Mathf.Sin(theta) * radius));
                    uvs.Add(new Vector2(u, 1f - v));
                }
            }

            int stride = segments + 1;
            for (int j = 0; j < rings; j++)
            {
                for (int i = 0; i < segments; i++)
                {
                    int a = j * stride + i;
                    int b = a + stride;
                    tris.Add(a); tris.Add(b); tris.Add(a + 1);
                    tris.Add(a + 1); tris.Add(b); tris.Add(b + 1);
                }
            }

            var mesh = new Mesh { name = "ST-Sphere" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(Repeat(Color.white, verts.Count));
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            Cache[key] = mesh;
            return mesh;
        }

        /// <summary>扁平圆盘（特效光环）。</summary>
        /// <summary>
        /// 水平单位正方形（法线朝 +Y，位于 XZ 平面）。
        /// 射程指示器用它：直接缩放 X/Z 即可得到直径，不需要旋转节点。
        /// </summary>
        public static Mesh GroundQuad()
        {
            const string key = "groundquad";
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;

            var mesh = new Mesh { name = "ST-GroundQuad" };
            mesh.SetVertices(new List<Vector3>
            {
                new Vector3(-0.5f, 0f, 0.5f),
                new Vector3(0.5f, 0f, 0.5f),
                new Vector3(0.5f, 0f, -0.5f),
                new Vector3(-0.5f, 0f, -0.5f),
            });
            mesh.SetUVs(0, new List<Vector2>
            {
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f), new Vector2(0f, 0f),
            });
            mesh.SetColors(Repeat(Color.white, 4));
            mesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            Cache[key] = mesh;
            return mesh;
        }

        public static Mesh Disc(float radius, int segments = 40)
        {
            string key = $"disc:{radius:F3}:{segments}";
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;

            var verts = new List<Vector3> { Vector3.zero };
            var uvs = new List<Vector2> { new Vector2(0.5f, 0.5f) };
            var tris = new List<int>();
            for (int i = 0; i <= segments; i++)
            {
                float a = (float)i / segments * Mathf.PI * 2f;
                verts.Add(new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius));
                uvs.Add(new Vector2(0.5f + Mathf.Cos(a) * 0.5f, 0.5f + Mathf.Sin(a) * 0.5f));
                if (i > 0)
                {
                    tris.Add(0); tris.Add(i); tris.Add(i + 1);
                }
            }

            var mesh = new Mesh { name = "ST-Disc" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(Repeat(Color.white, verts.Count));
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            Cache[key] = mesh;
            return mesh;
        }

        private static List<Color> Repeat(Color c, int count)
        {
            var list = new List<Color>(count);
            for (int i = 0; i < count; i++) list.Add(c);
            return list;
        }
    }

    /// <summary>设计像素 ↔ Unity 世界坐标（含 X 轴镜像，见 AGENTS.md）。</summary>
    public static class STWorld
    {
        /// <summary>每米像素数（与 Godot 端 SLBattle3D.PX 一致）。</summary>
        public const float PX = 19.6f;

        public const float BoardW = 1080f;
        public const float BoardH = 1920f;
        public const float BoardCx = 540f;
        public const float BoardCy = 960f;

        /// <summary>
        /// 设计像素 → 世界坐标。
        /// Unity 相机前向为 +Z，而 Godot 为 -Z，导致两者的屏幕右方向相反，
        /// 因此 X 取反、Z 保持，才能在同样的相机机位下得到同样的构图。
        /// </summary>
        public static Vector3 ToWorld(Vector2 p, float y = 0f)
        {
            return new Vector3(-(p.x - BoardCx) / PX, y, (p.y - BoardCy) / PX);
        }

        public static Vector2 ToDesign(Vector3 world)
        {
            return new Vector2(-world.x * PX + BoardCx, world.z * PX + BoardCy);
        }

        public static float RadiusM(float px) => px / PX;

        /// <summary>设计角度（弧度，x 向右 y 向下）→ Unity 绕 Y 的朝向角。</summary>
        public static Quaternion DesignRotation(float angle)
        {
            // 设计方向 (cosθ, sinθ) 在 Unity 世界是 (-cosθ, 0, sinθ)，
            // 而 Unity 的 +Z 前向绕 Y 旋转 yaw 后指向 (sin yaw, 0, cos yaw)，
            // 因此 yaw = θ - 90°。
            return Quaternion.Euler(0f, angle * Mathf.Rad2Deg - 90f, 0f);
        }

        public static Vector3 DesignDirection(float angle)
        {
            return new Vector3(-Mathf.Cos(angle), 0f, Mathf.Sin(angle));
        }
    }
}
