// 星轨防线 · 程序化音效（对应 Godot main.gd 的 _wav / _play）
//
// Godot 版用 AudioStreamWAV 现场合成方波/锯齿/噪声；Unity 侧改为在启动时
// 逐条烘焙成 AudioClip 缓存，播放走固定的 8 个 AudioSource 轮询，
// 并在 1 秒窗口内限流 14 次，避免高射速时音效爆掉。

using System.Collections.Generic;
using UnityEngine;

namespace StarLane
{
    public class SfxBank : MonoBehaviour
    {
        private const int SampleRate = 22050;
        private const int VoiceCount = 8;

        private readonly Dictionary<string, AudioClip> _cache = new Dictionary<string, AudioClip>();
        private readonly List<AudioSource> _voices = new List<AudioSource>();
        private float _window;
        private int _count;

        public bool Muted;

        private struct Spec
        {
            public float Freq;
            public float Dur;
            public string Kind;
            public float Gain;

            public Spec(float freq, float dur, string kind, float gain)
            {
                Freq = freq;
                Dur = dur;
                Kind = kind;
                Gain = gain;
            }
        }

        private static readonly Dictionary<string, Spec> Table = new Dictionary<string, Spec>
        {
            { "shoot_assault", new Spec(660f, 0.05f, "square", 0.12f) },
            { "shoot_siege", new Spec(180f, 0.14f, "square", 0.16f) },
            { "shoot_sniper", new Spec(920f, 0.09f, "saw", 0.10f) },
            { "explode", new Spec(90f, 0.22f, "noise", 0.18f) },
            { "summon", new Spec(520f, 0.14f, "sine", 0.16f) },
            { "merge", new Spec(740f, 0.22f, "sine", 0.16f) },
            { "unlock", new Spec(880f, 0.26f, "sine", 0.16f) },
            { "upgrade", new Spec(980f, 0.30f, "sine", 0.16f) },
            { "leak", new Spec(120f, 0.32f, "saw", 0.22f) },
            { "wave", new Spec(420f, 0.18f, "sine", 0.16f) },
            { "boss", new Spec(80f, 0.90f, "saw", 0.22f) },
            { "click", new Spec(540f, 0.05f, "square", 0.10f) },
            { "win", new Spec(660f, 0.50f, "sine", 0.20f) },
            { "lose", new Spec(180f, 0.70f, "saw", 0.20f) },
        };

        private void Awake()
        {
            for (int i = 0; i < VoiceCount; i++)
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.volume = 0.42f;
                src.spatialBlend = 0f;
                _voices.Add(src);
            }
        }

        public void Play(string key)
        {
            if (Muted) return;
            if (!Table.TryGetValue(key, out var spec)) return;

            float now = Time.unscaledTime;
            if (now - _window > 1f)
            {
                _window = now;
                _count = 0;
            }
            _count++;
            if (_count > 14) return;

            var clip = GetClip(key, spec);
            foreach (var v in _voices)
            {
                if (!v.isPlaying)
                {
                    v.clip = clip;
                    v.Play();
                    return;
                }
            }
        }

        private AudioClip GetClip(string key, Spec spec)
        {
            if (_cache.TryGetValue(key, out var clip) && clip != null) return clip;

            int n = Mathf.Max(1, (int)(spec.Dur * SampleRate));
            var data = new float[n];
            var rng = new System.Random(1337);
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                float env = Mathf.Exp(-t * 5f / Mathf.Max(0.02f, spec.Dur));
                float ph = Mathf.PI * 2f * spec.Freq * t;
                float s;
                switch (spec.Kind)
                {
                    case "square": s = Mathf.Sin(ph) >= 0f ? 1f : -1f; break;
                    case "saw": s = Mathf.Repeat(ph / Mathf.PI, 2f) - 1f; break;
                    case "noise": s = (float)rng.NextDouble() * 2f - 1f; break;
                    default: s = Mathf.Sin(ph); break;
                }
                data[i] = Mathf.Clamp(s * env * spec.Gain * 2.4f, -1f, 1f);
            }

            clip = AudioClip.Create("sfx-" + key, n, 1, SampleRate, false);
            clip.SetData(data, 0);
            _cache[key] = clip;
            return clip;
        }
    }
}
