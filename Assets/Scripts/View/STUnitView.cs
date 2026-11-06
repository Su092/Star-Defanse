// 星轨防线 · 单个单位节点的附加数据（模型层与状态机的容器）
//
// 由 STBattleView.SpawnModel 创建并填充；战斗视图每帧读取它来推进动画、
// 更新血条、播放受击闪白，避免在视图里到处 GetComponent。

using UnityEngine;
using UnityEngine.UI;

namespace StarLane
{
    public class STUnitView : MonoBehaviour
    {
        public Transform Body;         // 朝向 / 缩放 / 落地高度（对应 Godot 的 body 节点）
        public Transform ModelRoot;    // FBX 实例根
        public Animation Anim;
        public STUnitFSM Fsm;

        public string Key;
        public string ClassId;
        public string Folder;

        public float SpecYaw;
        public float BaseScale = 1f;
        public float BarY = 1.5f;
        public float Bob;
        public float SwayPhase;
        public float FlashAmount;
        public float BaseY;

        public Transform YawBone;
        public Transform[] Muzzles;

        /// <summary>
        /// Yaw 骨骼的静止局部旋转（无瞄准时的姿态）。
        /// 该骨骼父级带 RotX(≈90°)，其局部 Y 轴不是世界 up 轴，
        /// 因此瞄准不能直接覆盖 localRotation，必须把旋转叠加到静止姿态上。
        /// </summary>
        public Quaternion YawRestLocal = Quaternion.identity;

        /// <summary>
        /// 静止姿态下炮口的**水平**世界朝向（单位向量）。
        /// 瞄准时用它作为旋转基准，求到目标方向的有符号角，
        /// 这样无需手工推导 ±90° 偏移，换模型也不会错。
        /// </summary>
        public Vector3 YawRestDir = Vector3.forward;
        public bool YawRestDirValid;

        /// <summary>当前已施加到 Yaw 骨骼的水平转角（度，绕世界 up）。</summary>
        public float YawApplied;

        private GameObject _hpBar;
        private Transform _hpFill;
        private MeshRenderer _hpFillRenderer;
        private GameObject _shadow;
        // 血条尺寸（世界单位）。1 单位 = 19.6 px 设计像素。
        // 4.8 在俯视透视下投影后只剩约 3.4，屏幕上仍偏细，因此再加宽到 6.2
        // 并把高度从 0.42 提到 0.56，保证小单位在复杂地表上也能一眼看到。
        private const float HpBarWidth = 6.2f;
        private const float HpBarHeight = 0.56f;

        private void Awake()
        {
            if (Body != null) BaseY = Body.localPosition.y;
        }

        /// <summary>程序化投影贴片：俯视相机下没有实时阴影，用柔和暗斑补足落地感。</summary>
        public void SyncShadowBlob()
        {
            if (_shadow != null) return;
            var go = new GameObject("ShadowBlob");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = STMesh.Disc(1f, 24);
            var mr = go.AddComponent<MeshRenderer>();
            var shader = Shader.Find("StarLane/Unlit");
            var mat = new Material(shader) { name = "ST-ShadowBlob" };
            mat.color = new Color(0f, 0f, 0f, 0.42f);
            mat.SetFloat("_ZTest", 4f);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetFloat("_Cull", 0f);
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            _shadow = go;
        }

        public void SetShadowScale(float radiusM)
        {
            if (_shadow != null) _shadow.transform.localScale = new Vector3(radiusM, 1f, radiusM * 0.75f);
        }

        /// <summary>创建 / 更新血条（billboard，无深度测试，始终朝向相机）。</summary>
        public void UpdateHpBar(float ratio, Color tint)
        {
            if (_hpBar == null) BuildHpBar();

            float r = Mathf.Clamp01(ratio);
            // 与 Godot 一致：满血不显示血条，掉血或死亡附近才出现
            _hpBar.SetActive(r > 0f && r < 0.999f);
            if (!_hpBar.activeSelf) return;

            _hpFill.localScale = new Vector3(Mathf.Max(0.001f, r), 1f, 1f);
            // 填充条从左侧收缩：quad 以中心为原点，宽 W 缩到 W*r 后中心需左移
            // -(W/2)*(1-r) 才能与底条的左边缘对齐。少写一半会让血条整体左偏。
            _hpFill.localPosition = new Vector3(-(HpBarWidth * 0.92f * 0.5f) * (1f - r), 0f, 0.01f);

            if (_hpFillRenderer != null)
            {
                // 血条填充是每个单位独立实例，直接改颜色
                var mat = _hpFillRenderer.sharedMaterial;
                if (mat != null) mat.color = tint;
            }
        }

        private void BuildHpBar()
        {
            _hpBar = new GameObject("HpBar");
            _hpBar.transform.SetParent(transform, false);
            _hpBar.transform.localPosition = new Vector3(0f, BarY, 0f);

            var shader = Shader.Find("StarLane/Unlit");

            var bgGo = new GameObject("Bg");
            bgGo.transform.SetParent(_hpBar.transform, false);
            var bgMf = bgGo.AddComponent<MeshFilter>();
            bgMf.sharedMesh = STMesh.Quad(new Vector2(HpBarWidth, HpBarHeight));
            var bgMr = bgGo.AddComponent<MeshRenderer>();
            var bgMat = new Material(shader) { name = "ST-HpBg" };
            bgMat.color = new Color(0.02f, 0.02f, 0.06f, 0.78f);
            bgMat.SetFloat("_ZTest", 8f);
            bgMat.SetFloat("_ZWrite", 0f);
            bgMat.SetFloat("_Cull", 0f);
            // 明确排在飘字之下（飘字 4500）：两者都是 Always+Transparent，
            // 不显式指定 queue 会按距离排序，血条就可能盖住金币飘字。
            bgMat.renderQueue = 3100;
            bgMr.sharedMaterial = bgMat;
            bgMr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(_hpBar.transform, false);
            fillGo.transform.localPosition = new Vector3(0f, 0f, 0.01f);
            var fillMf = fillGo.AddComponent<MeshFilter>();
            fillMf.sharedMesh = STMesh.Quad(new Vector2(HpBarWidth * 0.92f, HpBarHeight * 0.58f));
            _hpFillRenderer = fillGo.AddComponent<MeshRenderer>();
            var fillMat = new Material(shader) { name = "ST-HpFill" };
            fillMat.color = STColors.Red;
            fillMat.SetFloat("_ZTest", 8f);
            fillMat.SetFloat("_ZWrite", 0f);
            fillMat.SetFloat("_Cull", 0f);
            fillMat.renderQueue = 3101;   // 紧贴底板之上、仍低于飘字 4500
            _hpFillRenderer.sharedMaterial = fillMat;
            _hpFillRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            _hpFill = fillGo.transform;
            _hpBar.SetActive(false);

            // 血条始终朝向主相机，这里缓存一次引用，避免每帧 Camera.main（内部会做查找）
            _camera = Camera.main;
        }

        private Camera _camera;

        private void LateUpdate()
        {
            if (_hpBar == null || !_hpBar.activeSelf) return;
            if (_camera == null) _camera = Camera.main;
            if (_camera != null) _hpBar.transform.rotation = _camera.transform.rotation;
        }
    }
}
