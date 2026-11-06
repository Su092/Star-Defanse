// 九宫格四角自适应：尺寸一变就重算 pixelsPerUnitMultiplier。
//
// 为什么需要它：Image 的 Sliced 四角尺寸由 spriteBorder 决定，和控件尺寸无关。
// 布局组（VerticalLayoutGroup / ContentSizeFitter）在创建后才会算出真实尺寸，
// 创建当帧 rect 还是 0，算不出比例。挂这个组件等布局稳定后补算，
// 之后窗口缩放、卡片换行也会自动跟上。

using UnityEngine;
using UnityEngine.UI;

namespace StarLane
{
    [RequireComponent(typeof(Image))]
    public sealed class STSliceFit : MonoBehaviour
    {
        private Image _img;
        private Vector2 _last = new Vector2(-1f, -1f);

        private void Awake() => _img = GetComponent<Image>();

        private void OnEnable() => Apply();

        private void OnRectTransformDimensionsChange() => Apply();

        private void Apply()
        {
            if (_img == null) _img = GetComponent<Image>();
            if (_img == null || _img.rectTransform == null) return;

            var r = _img.rectTransform.rect.size;
            if (r.x < 1f || r.y < 1f) return;
            if (Mathf.Abs(r.x - _last.x) < 0.5f && Mathf.Abs(r.y - _last.y) < 0.5f) return;

            _last = r;
            STUi.ApplySlice(_img);
        }
    }
}
