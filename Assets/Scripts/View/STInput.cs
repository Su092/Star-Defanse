// 星轨防线 · 输入适配层
//
// 工程使用 Unity 传统输入（Player Settings → Active Input Handling = Input Manager），
// 与 UGUI 的 StandaloneInputModule 保持一致，无需额外安装 Input System 包。
//
// 这里把「屏幕像素 → 1080×1920 设计坐标」统一换算，使战斗层的坐标系与
// Godot 版本完全一致（左上原点、y 向下）。
// 同时保留键盘快捷键：空格/ESC 暂停、S 召唤、U 升级、1/2/3 倍速、M 静音。

using UnityEngine;
using UnityEngine.EventSystems;

namespace StarLane
{
    public static class STInput
    {
        public const float Width = STUi.DesignWidth;
        public const float Height = STUi.DesignHeight;

        private static Vector2 _pointer;
        private static bool _pressed;
        private static bool _held;
        private static bool _released;
        private static bool _pointerValid;

        /// <summary>最近一次的指针位置（设计坐标）。</summary>
        public static Vector2 PointerDesign => _pointerValid ? _pointer : ScreenToDesign(Input.mousePosition);

        public static bool LeftPressed => _pressed;
        public static bool LeftHeld => _held;
        public static bool LeftReleased => _released;

        /// <summary>每帧开头由 Main 调用一次，统一采样输入。</summary>
        public static void Sample()
        {
            if (_testActive)
            {
                // 自检模式：使用注入的指针状态，其余逻辑完全一致
                _pressed = _testPressed;
                _held = _testHeld;
                _released = _testReleased;
                _pointer = _testPointer;
                _pointerValid = true;
                _testPressed = false;
                _testReleased = false;
                return;
            }

            bool pressed = false;
            bool held = false;
            bool released = false;

            if (Input.touchCount > 0)
            {
                var t = Input.GetTouch(0);
                var pos = ScreenToDesign(t.position);
                switch (t.phase)
                {
                    case TouchPhase.Began:
                        pressed = true;
                        held = true;
                        break;
                    case TouchPhase.Moved:
                    case TouchPhase.Stationary:
                        held = true;
                        break;
                    case TouchPhase.Ended:
                    case TouchPhase.Canceled:
                        released = true;
                        break;
                }
                _pointer = pos;
                _pointerValid = true;
            }
            else
            {
                var pos = ScreenToDesign(Input.mousePosition);
                _pointer = pos;
                _pointerValid = true;
                pressed = Input.GetMouseButtonDown(0);
                held = Input.GetMouseButton(0);
                released = Input.GetMouseButtonUp(0);
            }

            _pressed = pressed;
            _held = held;
            _released = released;
        }

        /// <summary>指针是否落在 UI 上（避免点按钮时同时操作战场）。</summary>
        public static bool OverUI()
        {
            var es = EventSystem.current;
            if (es == null) return false;
            return es.IsPointerOverGameObject();
        }

        /// <summary>手指/鼠标是否压在当前 UI 上（触摸用 fingerId 查询）。</summary>
        public static bool OverUIForTouch()
        {
            var es = EventSystem.current;
            if (es == null) return false;
            if (Input.touchCount > 0) return es.IsPointerOverGameObject(Input.GetTouch(0).fingerId);
            return es.IsPointerOverGameObject();
        }

        /// <summary>
        /// 屏幕像素 → 设计坐标。
        /// CanvasScaler 采用 match-width 等比缩放，因此纵向可能留边，
        /// 这里把留边均分到上下，保证映射与 Canvas 内的坐标系一致。
        /// </summary>
        public static Vector2 ScreenToDesign(Vector2 screen)
        {
            if (Screen.width <= 0 || Screen.height <= 0) return Vector2.zero;

            float scale = Screen.width / Width;
            float designH = Screen.height / scale;
            float x = screen.x / scale;
            float y = (Screen.height - screen.y) / scale;
            y -= Mathf.Max(0f, designH - Height) * 0.5f;
            return new Vector2(x, y);
        }

        /// <summary>设计坐标 → 屏幕像素（用于模拟点击与截图自检）。</summary>
        public static Vector2 DesignToScreen(Vector2 design)
        {
            float scale = Screen.width / Width;
            float designH = Screen.height / scale;
            design.y += Mathf.Max(0f, designH - Height) * 0.5f;
            return new Vector2(design.x * scale, Screen.height - design.y * scale);
        }

        // ---------------------------------------------------------------- 自检注入
        // 自动截图脚本用这套接口喂入指针状态，走的是与真实输入完全相同的
        // Sample → HandlePointer → OnPress/OnMove/OnRelease 路径。

        private static bool _testActive;
        private static bool _testPressed;
        private static bool _testHeld;
        private static bool _testReleased;
        private static Vector2 _testPointer;

        public static void BeginTestPointer() => _testActive = true;
        public static void EndTestPointer() => _testActive = false;

        public static void SetTestPointer(Vector2 design, bool down, bool up)
        {
            _testPointer = design;
            _testPressed = down;
            _testHeld = !up;
            _testReleased = up;
            _pointerValid = true;
        }

        /// <summary>键盘快捷键（返回动作标识，无输入返回空串）。</summary>
        public static string ReadHotkeys()
        {
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Escape)) return "pause";
            if (Input.GetKeyDown(KeyCode.S)) return "summon";
            if (Input.GetKeyDown(KeyCode.U)) return "upgrade";
            if (Input.GetKeyDown(KeyCode.M)) return "mute";
            if (Input.GetKeyDown(KeyCode.Alpha1)) return "speed1";
            if (Input.GetKeyDown(KeyCode.Alpha2)) return "speed2";
            if (Input.GetKeyDown(KeyCode.Alpha3)) return "speed3";
            return "";
        }
    }
}
