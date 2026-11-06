// 星轨防线 · 出生门漩涡动画（对应 Godot battle3d.gd 的 _gate_vortex 自转）
//
// Godot 版用 _process 累计角度；Unity 侧挂一个小脚本驱动，
// 避免在 STBattleView.LateUpdate 里再维护一份节点引用。

using UnityEngine;

namespace StarLane
{
    public class STGateVortex : MonoBehaviour
    {
        public Transform Arms;
        public MeshRenderer Glow;

        private float _phase;

        private void Update()
        {
            float dt = Time.deltaTime;
            _phase += dt;

            if (Arms != null) Arms.Rotate(0f, 0f, -95f * dt, Space.Self);

            if (Glow != null && Glow.material != null)
            {
                float pulse = 0.45f + Mathf.Sin(_phase * 3.4f) * 0.12f;
                var c = Glow.material.color;
                Glow.material.color = new Color(c.r, c.g, c.b, pulse);
            }
        }
    }
}
