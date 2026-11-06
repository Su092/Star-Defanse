// 星轨防线 · 单位动画状态机（Godot scripts/unit_fsm.gd 的移植）
//
// 设计（与 Godot 版完全一致）：
//   · 每个模型的状态表由 STBattleView.FSM_SPEC 声明，这里只负责播放；
//   · 循环状态（idle / move / walk …）自动设为循环，可被任意切换打断；
//   · 一次性状态（attack / move_start / move_stop …）播完回到 base，
//     或按 chain 表进入下一状态；
//   · 同一一次性状态正在播放时重复触发不会重播（高射速下动画能播完）。
//
// Unity 用 Legacy Animation 组件播放：片段来自模型自带的 Animation，
// 或从同目录 Anim_*.fbx 合并进来（见 STBattleView.BuildModel）。

using System.Collections.Generic;
using UnityEngine;

namespace StarLane
{
    public class STUnitFSM
    {
        private readonly Animation _player;
        private readonly Dictionary<string, string> _clips = new Dictionary<string, string>();
        private readonly HashSet<string> _oneShots = new HashSet<string>();
        private readonly Dictionary<string, string> _chain = new Dictionary<string, string>();

        public readonly string Base;
        public string Current = "";
        public bool OneShot;
        public float Speed = 1f;

        private float _timer;

        public STUnitFSM(Animation player, IEnumerable<KeyValuePair<string, string>> clipMap, string baseState,
            float speed = 1f, IEnumerable<string> oneShotStates = null, IDictionary<string, string> chainMap = null)
        {
            _player = player;
            Speed = speed;
            Base = baseState ?? "";

            if (oneShotStates != null)
            {
                foreach (var s in oneShotStates) _oneShots.Add(s);
            }
            if (chainMap != null)
            {
                foreach (var kv in chainMap) _chain[kv.Key] = kv.Value;
            }

            if (clipMap != null)
            {
                foreach (var kv in clipMap)
                {
                    if (string.IsNullOrEmpty(kv.Value)) continue;
                    if (_player == null || _player.GetClip(kv.Value) == null) continue;
                    _clips[kv.Key] = kv.Value;
                    if (!_oneShots.Contains(kv.Key))
                    {
                        var clip = _player.GetClip(kv.Value);
                        clip.wrapMode = WrapMode.Loop;
                    }
                }
            }
        }

        public bool Has(string state) => _clips.ContainsKey(state);

        /// <summary>切换状态；一次性状态走 PlayOnce。</summary>
        public void SetState(string state, bool restart = false)
        {
            if (_player == null || !_clips.TryGetValue(state, out var clip)) return;

            if (_oneShots.Contains(state))
            {
                PlayOnce(state, 0f, restart);
                return;
            }
            if (Current == state && !restart) return;

            Current = state;
            OneShot = false;
            _timer = 0f;

            if (_player.clip != _player.GetClip(clip) || !_player.isPlaying)
            {
                _player.CrossFade(clip, 0.18f);
            }
            if (restart) _player.Rewind(clip);
        }

        public void PlayOnce(string state, float hold = 0f, bool allowRestart = false)
        {
            if (_player == null || !_clips.TryGetValue(state, out var clip)) return;
            if (OneShot && Current == state && !allowRestart) return;

            Current = state;
            OneShot = true;
            var c = _player.GetClip(clip);
            _player.CrossFade(clip, 0.08f);
            _player.Rewind(clip);
            _player.clip = c;
            _timer = c != null ? c.length / Mathf.Max(0.01f, Speed) + hold : 0.5f;
            _timer = Mathf.Max(0.08f, _timer);
        }

        /// <summary>每帧推进一次性状态计时。</summary>
        public void Tick(float delta)
        {
            if (_player == null) return;

            if (OneShot)
            {
                // 片段可能被 Speed 影响；这里用 clip.length 与实际播放状态双重兜底
                _timer -= delta * Mathf.Max(0.01f, Speed);
                var playing = _player.clip != null && _player.isPlaying;
                if (_timer <= 0f || !playing)
                {
                    OneShot = false;
                    _timer = 0f;
                    var next = _chain.TryGetValue(Current, out var ch) ? ch : Base;
                    // base 为空（如突击塔只有 attack 片段）时回到「无状态」，
                    // 不能用哨兵值冒充状态，否则调试输出会显示成 ":" 这类无名状态。
                    Current = string.Empty;
                    if (!string.IsNullOrEmpty(next) && _clips.ContainsKey(next)) SetState(next, true);
                }
                return;
            }

            if (!string.IsNullOrEmpty(Base) && _clips.ContainsKey(Base) && Current != Base) SetState(Base);
        }
    }
}
