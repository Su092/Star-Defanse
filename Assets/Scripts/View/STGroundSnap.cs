// 星轨防线 · 单位自动落地
//
// 为什么需要独立组件：
// 模型在 FBX 里可能躺平、有顶点偏移或非零原点，手填的 localPosition.y 很容易让单位
// 悬空或陷进地面。生成时矩阵尚未刷新，无法直接量 Renderer.bounds，
// 因此这里在生成后的第一帧（矩阵已刷新）做一次精确测量，把最低点压到地平面，
// 之后每帧只做廉价的位置校正，直到稳定。
//
// 用法：STBattleView.SpawnModel 里挂上，并调用 Configure(地面高度)。

using UnityEngine;

namespace StarLane
{
    public class STGroundSnap : MonoBehaviour
    {
        /// <summary>世界坐标下的地平面高度（通常为 0）。</summary>
        public float GroundY;

        /// <summary>Body 节点：缩放/俯仰都在它上面，位置由本组件修正。</summary>
        public Transform Body;

        /// <summary>要参与落地计算的模型根。</summary>
        public Transform ModelRoot;

        private bool _done;
        private int _frames;

        public void Configure(Transform body, Transform modelRoot, float groundY = 0f)
        {
            Body = body;
            ModelRoot = modelRoot;
            GroundY = groundY;
            _done = false;
            _frames = 0;
        }

        private void LateUpdate()
        {
            if (_done || Body == null || ModelRoot == null) return;

            // 等一帧让世界矩阵刷新，否则 Renderer.bounds 读到的是旧值
            if (_frames++ < 1) return;

            var bounds = new Bounds();
            bool init = false;
            foreach (var r in ModelRoot.GetComponentsInChildren<Renderer>(true))
            {
                // 血条与投影贴片不参与落地
                if (r.gameObject.name == "ShadowBlob" || r.gameObject.name == "HpBar") continue;
                if (!r.enabled || !r.gameObject.activeInHierarchy) continue;

                if (!init) { bounds = r.bounds; init = true; }
                else bounds.Encapsulate(r.bounds);
            }
            if (!init) { _done = true; return; }

            float delta = bounds.min.y - GroundY;
            if (Mathf.Abs(delta) > 0.002f)
            {
                var lp = Body.localPosition;
                Body.localPosition = new Vector3(lp.x, lp.y - delta, lp.z);
            }

            // 再等一帧确认收敛（防止父子层级导致的二次偏移）
            if (Mathf.Abs(delta) <= 0.002f || _frames > 4) _done = true;
        }
    }
}
