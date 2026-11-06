// 星轨防线 · 局内模拟（Godot scripts/run_state.gd 的逐条移植）
//
// 纯逻辑：不引用任何 GameObject / 渲染 / 输入 API，因此可以在无场景的
// 情况下跑完整对局（编辑器自检与单元测试都走这条路径）。
// 所有随机数来自 mulberry32，种子相同 → 结果完全一致。

using System;
using System.Collections.Generic;
using UnityEngine;

namespace StarLane
{
    // ---------------------------------------------------------------- 状态对象
    public class PlatformState
    {
        public string Id;
        public int Index;
        public Vector2 Pos;
        public TowerState Tower;
    }

    public class TowerState
    {
        public string Id;
        public string PlatformId;
        public string ClassId;
        public string OpId;
        public int Level = 1;
        public float Cd;
        public float Angle = -Mathf.PI / 2f;
        public float Aim = -Mathf.PI / 2f;
        public float Flash;
        public float BornAt;
        public int Kills;
        public bool Merged;
    }

    public class EnemyState
    {
        public string Id;
        public string Type;
        public string Name;
        public float Hp;
        public float HpMax;
        public float Speed;
        public float Armor;
        public int Reward;
        public int Leak;
        public float Radius;
        public float Atk;
        public bool Elite;
        public bool Boss;
        public Color Color;
        public float SlowResist;
        public float Dist;
        public Vector2 Pos;
        public float Angle;
        public int Wave;
        public float SlowFactor;
        public float SlowUntil;
        public float BurnDps;
        public float BurnUntil;
        public int ShredStacks;
        public float? ShredTimer;
        public float Flash;
        public bool Dead;
        public string TowerKiller;
        public float Phase;
    }

    public class GuardState
    {
        public string Id;
        public string TowerId;
        public int Slot;
        public float Dist;
        public float Home;
        public float Leash = 120f;
        public float Speed = 52f;
        public float Radius = 34f;
        public Vector2 Pos;
        public float Angle;
        public float Hp;
        public float HpMax;
        public float Dps;
        public float Respawn;
        public float RespawnTimer;
        public List<string> Blocking = new List<string>();
        public float BornAt;
        public float LastDist;
    }

    public class ProjectileState
    {
        public string Type;           // beam / shell / bullet
        public Vector2 Pos;
        public Vector2 Start;
        public string TargetId;
        public Vector2 Target;
        public float Speed;
        public float Dmg;
        public float Splash;
        public string TowerId;
        public Color Color;
        public float Life;
        public float MaxLife;
        public float Angle;
        public bool FromHero;
    }

    public class HeroState
    {
        public Vector2 Pos;
        public Vector2 Target;
        public float Cd;
        public bool Moving;
        public float Angle = -Mathf.PI / 2f;
    }

    public class TowerStats
    {
        public int Level;
        public float Range;
        public float Rate;
        public float Dmg;
        public float Splash;
        public float GuardHp;
        public float GuardDps;
        public float Respawn;
        public int BlockCap;
    }

    public class HeroStats
    {
        public float Dmg;
        public float Rate;
        public float Range;
        public float Speed;
    }

    public class EndingInfo
    {
        public string Status;
        public int Wave;
        public int Kills;
        public int Leaked;
        public float Time;
        public float Gold;
        public int Summons;
        public int Upgrades;
    }

    // ---------------------------------------------------------------- 事件
    public enum STEventKind
    {
        Summon, Unlock, Move, Merge, Affix,
        GuardSpawn, GuardDown, GuardReturn,
        Hit, Kill, Leak, Spawn, Wave, Shot, HeroShot, Explosion, WaveClear,
        Victory, Defeat,
    }

    /// <summary>逻辑层向表现层广播的事件（对应 GDScript 里的 event Dictionary）。</summary>
    public class STEvent
    {
        public STEventKind Kind;
        public TowerState Tower;
        public PlatformState Platform;
        public PlatformState FromPlatform;
        public PlatformState ToPlatform;
        public EnemyState Enemy;
        public GuardState Guard;
        public Vector2 Pos;
        public Color Color = Color.white;
        public float Amount;
        public float Radius;
        public int IntA;
        public string Text;
        public string ClassId;
        public bool Flag;
    }

    /// <summary>操作结果（对应 GDScript 的返回 Dictionary）。</summary>
    public class RunResult
    {
        public bool Ok;
        public string Reason = "";
        public string Kind = "";
        public int Cost;
        public float Refund;
        public bool Free;
        public string Text = "";
        public TowerState Tower;
        public PlatformState Platform;
        public List<string> Choices;
        public List<STEvent> Events = new List<STEvent>();

        public static RunResult Fail(string reason) => new RunResult { Ok = false, Reason = reason };
    }

    // ---------------------------------------------------------------- 主类
    public class RunState
    {
        public const float Tau = Mathf.PI * 2f;

        public int LevelIndex;
        public LevelData Level;
        public List<string> Formation = new List<string>();

        public readonly List<Vector2> PathPts = new List<Vector2>();
        public readonly List<PathSeg> PathSegs = new List<PathSeg>();
        public float PathTotal;

        public readonly List<PlatformState> Platforms = new List<PlatformState>();
        public readonly List<TowerState> Towers = new List<TowerState>();
        public readonly List<EnemyState> Enemies = new List<EnemyState>();
        public readonly List<GuardState> Guards = new List<GuardState>();
        public readonly List<ProjectileState> Projectiles = new List<ProjectileState>();
        public HeroState Hero = new HeroState();

        public Vector2 CorePos;
        public Vector2 SpawnPos;
        public float CoreHp = 20f;
        public float CoreHpMax = 20f;
        public float Gold = 40f;
        public float Time;

        public int Summons;
        public int Upgrades;

        public readonly List<ScheduleItem> Schedule = new List<ScheduleItem>();
        public int ScheduleIndex;
        public int WaveIndex;
        public int[] WaveAlive = Array.Empty<int>();
        public bool[] WavePaid = Array.Empty<bool>();
        public int WaveCleared;

        public string Status = "running";
        public EndingInfo Ending;
        public readonly List<string> Affixes = new List<string>();
        public Dictionary<string, float> Mods = new Dictionary<string, float>();
        public readonly List<string> PendingChoices = new List<string>();

        public int Kills;
        public int LeakedCount;
        public float TotalDamage;
        public float TotalGoldEarned;

        private uint _rngState;
        private int _uid = 1;
        public int SeedValue;

        public struct PathSeg
        {
            public Vector2 A;
            public Vector2 B;
            public float Len;
            public float Start;
            public float Angle;
        }

        public struct ScheduleItem
        {
            public float T;
            public string Type;
            public int Wave;
        }

        // ---------------------------------------------------------------- 构造
        public RunState(int levelIndex = 0, IEnumerable<string> formation = null, int? seed = null)
        {
            var levels = STData.Levels;
            LevelIndex = Mathf.Clamp(levelIndex, 0, levels.Count - 1);
            Level = STData.GetLevel(LevelIndex);
            Formation = STData.NormalizeFormation(formation);

            var csv = string.Join(",", Formation);
            SeedValue = seed ?? (int)(STHash.String(Level.id + "|" + csv) & 0xFFFFFFFFu);
            _rngState = (uint)SeedValue;

            BuildPath(Level.path);
            for (int i = 0; i < Level.platforms.Count; i++)
            {
                var p = Level.platforms[i];
                Platforms.Add(new PlatformState { Id = "p" + i, Index = i, Pos = p.ToVector2() });
            }

            var tuning = STData.T;
            CoreHpMax = tuning.coreHp;
            CoreHp = CoreHpMax;
            Gold = tuning.startGold;
            CorePos = PathPos(PathTotal);
            SpawnPos = PathPos(0f);

            BuildSchedule();

            WaveAlive = new int[STData.Waves.Count];
            WavePaid = new bool[STData.Waves.Count];

            var heroStart = PathPos(PathTotal * 0.62f);
            Hero = new HeroState { Pos = heroStart, Target = heroStart, Cd = 0f, Moving = false, Angle = -Mathf.PI / 2f };
            Mods = DefaultMods();
        }

        public static Dictionary<string, float> DefaultMods()
        {
            return new Dictionary<string, float>
            {
                { "dmgMul", 0f }, { "atkSpeedMul", 0f }, { "rangeMul", 0f },
                { "critChance", 0f }, { "critMul", 0f }, { "goldPerKill", 0f },
                { "waveBonus", 0f }, { "leakReduce", 0f },
                { "pierce", 0f }, { "sniperDmgMul", 0f }, { "execute", 0f },
                { "splashMul", 0f }, { "burnChance", 0f }, { "burnDps", 0f }, { "burnDur", 0f },
                { "guardHpMul", 0f }, { "guardDmgMul", 0f }, { "guardRespawnMul", 0f },
                { "summonGrowthMul", 0f }, { "mergeRefund", 0f },
                { "frostChance", 0f }, { "frostSlow", 0f }, { "frostDur", 0f },
                { "heroDmgMul", 0f }, { "heroRangeMul", 0f },
                { "shredPerHit", 0f }, { "shredMax", 0f }, { "reinforce", 0f },
            };
        }

        public float Mod(string key) => Mods.TryGetValue(key, out var v) ? v : 0f;

        // ---------------------------------------------------------------- RNG
        private uint Mul32(uint a, uint b) => a * b;

        private float Rng()
        {
            _rngState = _rngState + 0x6D2B79F5u;
            uint t = _rngState;
            t = Mul32(t ^ (t >> 15), t | 1u);
            t = t ^ (t + Mul32(t ^ (t >> 7), t | 61u));
            return (t ^ (t >> 14)) / 4294967296f;
        }

        private int Rint(int n) => n <= 0 ? 0 : (int)Mathf.Floor(Rng() * n);

        private List<T> Shuffle<T>(IEnumerable<T> src)
        {
            var a = new List<T>(src);
            for (int i = a.Count - 1; i > 0; i--)
            {
                int j = Rint(i + 1);
                (a[i], a[j]) = (a[j], a[i]);
            }
            return a;
        }

        private string NextId(string prefix)
        {
            _uid++;
            return prefix + _uid;
        }

        // ---------------------------------------------------------------- 路径
        private void BuildPath(List<Point2> pts)
        {
            PathPts.Clear();
            PathSegs.Clear();
            PathTotal = 0f;
            foreach (var p in pts) PathPts.Add(p.ToVector2());

            for (int i = 0; i < PathPts.Count - 1; i++)
            {
                var a = PathPts[i];
                var b = PathPts[i + 1];
                float len = Vector2.Distance(a, b);
                PathSegs.Add(new PathSeg
                {
                    A = a,
                    B = b,
                    Len = len,
                    Start = PathTotal,
                    Angle = Mathf.Atan2(b.y - a.y, b.x - a.x),
                });
                PathTotal += len;
            }
        }

        public Vector2 PathPos(float dist)
        {
            float d = Mathf.Clamp(dist, 0f, PathTotal);
            for (int i = 0; i < PathSegs.Count; i++)
            {
                var s = PathSegs[i];
                if (d <= s.Start + s.Len || i == PathSegs.Count - 1)
                {
                    float t = s.Len == 0f ? 0f : (d - s.Start) / s.Len;
                    return Vector2.Lerp(s.A, s.B, t);
                }
            }
            return PathPts.Count > 0 ? PathPts[PathPts.Count - 1] : Vector2.zero;
        }

        public float PathAngle(float dist)
        {
            float d = Mathf.Clamp(dist, 0f, PathTotal);
            for (int i = 0; i < PathSegs.Count; i++)
            {
                var s = PathSegs[i];
                if (d <= s.Start + s.Len || i == PathSegs.Count - 1) return s.Angle;
            }
            return 0f;
        }

        /// <summary>返回最近的车道里程与距离（用于拦截塔的锚点）。</summary>
        public void NearestPath(Vector2 p, out float dist, out float distance)
        {
            dist = 0f;
            distance = 0f;
            float bestD = float.PositiveInfinity;
            int steps = Mathf.Max(2, (int)(PathTotal / 6f));
            for (int i = 0; i <= steps; i++)
            {
                float d = (float)i / steps * PathTotal;
                var q = PathPos(d);
                float dd = (q - p).sqrMagnitude;
                if (dd < bestD)
                {
                    bestD = dd;
                    dist = d;
                }
            }
            distance = Mathf.Sqrt(bestD);
        }

        private void BuildSchedule()
        {
            Schedule.Clear();
            var tuning = STData.T;
            for (int w = 0; w < STData.Waves.Count; w++)
            {
                var wave = STData.Waves[w];
                float waveStart = tuning.firstWaveDelay + w * tuning.waveInterval;
                var list = new List<ScheduleItem>();
                foreach (var batch in wave.batches)
                {
                    for (int i = 0; i < batch.count; i++)
                    {
                        list.Add(new ScheduleItem
                        {
                            T = waveStart + batch.delay + i * batch.interval,
                            Type = batch.type,
                            Wave = w,
                        });
                    }
                }
                list.Sort((a, b) => a.T.CompareTo(b.T));
                Schedule.AddRange(list);
            }
            Schedule.Sort((a, b) => a.T.CompareTo(b.T));
        }

        // ---------------------------------------------------------------- 查询
        public List<OperativeData> FormationOps()
        {
            var list = new List<OperativeData>();
            foreach (var id in Formation)
            {
                var op = STData.GetOperative(id);
                if (op != null) list.Add(op);
            }
            return list;
        }

        public int SummonCost()
        {
            var tuning = STData.T;
            float g = Mathf.Max(1.02f, tuning.summonGrowth * (1f + Mod("summonGrowthMul")));
            return Mathf.Max(1, STRound(tuning.summonBase * Mathf.Pow(g, Summons)));
        }

        public int UpgradeCost()
        {
            var tuning = STData.T;
            return Mathf.Max(1, STRound(tuning.upgradeBase * Mathf.Pow(tuning.upgradeGrowth, Upgrades)));
        }

        public int UnlockedPlatformCount()
        {
            var tuning = STData.T;
            int extra = (int)Mathf.Floor((float)Summons / tuning.summonsPerUnlock);
            return Mathf.Min(Platforms.Count, tuning.initialUnlocked + extra);
        }

        public bool IsPlatformUnlocked(PlatformState p) => p.Index < UnlockedPlatformCount();

        public List<PlatformState> FreePlatforms()
        {
            int n = UnlockedPlatformCount();
            var list = new List<PlatformState>();
            foreach (var p in Platforms)
            {
                if (p.Index < n && p.Tower == null) list.Add(p);
            }
            return list;
        }

        public PlatformState PlatformById(string id)
        {
            foreach (var p in Platforms)
            {
                if (p.Id == id) return p;
            }
            return null;
        }

        public TowerState TowerById(string id)
        {
            foreach (var t in Towers)
            {
                if (t.Id == id) return t;
            }
            return null;
        }

        public TowerState TowerOnPlatform(string platformId)
        {
            var p = PlatformById(platformId);
            return p?.Tower;
        }

        public TowerStats StatsOf(TowerState t)
        {
            var tuning = STData.T;
            var cls = STData.GetClass(t.ClassId);
            var op = STData.GetOperative(t.OpId);
            int lv = t.Level - 1;

            float dmgMul = 1f + Mod("dmgMul") + Mod("reinforce") + (op?.GetMod("dmgMul") ?? 0f);
            float rateMul = 1f + Mod("atkSpeedMul") + (op?.GetMod("atkSpeedMul") ?? 0f);
            float rangeMul = 1f + Mod("rangeMul") + (op?.GetMod("rangeMul") ?? 0f);
            float splashMul = 1f + Mod("splashMul") + (op?.GetMod("splashMul") ?? 0f);

            var st = new TowerStats
            {
                Level = t.Level,
                Range = STRound(cls.range * Mathf.Pow(tuning.levelRange, lv) * rangeMul),
                Rate = Mathf.Max(0.05f, cls.rate * Mathf.Pow(tuning.levelRate, lv) * rateMul),
                BlockCap = cls.blockCap,
            };

            float b = cls.dmg * Mathf.Pow(tuning.levelDmg, lv) * dmgMul;
            if (t.ClassId == "sniper") b *= 1f + Mod("sniperDmgMul");
            st.Dmg = Mathf.Max(1f, b);

            if (cls.HasSplash)
            {
                st.Splash = cls.splash * splashMul * Mathf.Pow(1.05f, lv);
            }

            if (t.ClassId == "intercept")
            {
                st.GuardHp = STRound(cls.guardHp * Mathf.Pow(tuning.levelDmg, lv)
                    * (1f + Mod("guardHpMul") + (op?.GetMod("guardHpMul") ?? 0f)));
                st.GuardDps = cls.guardDps * Mathf.Pow(tuning.levelDmg, lv) * (1f + Mod("guardDmgMul"));
                st.Respawn = cls.respawn * (1f + Mod("guardRespawnMul") + (op?.GetMod("guardRespawnMul") ?? 0f));
            }
            return st;
        }

        public HeroStats HeroStatsNow()
        {
            var tuning = STData.T;
            return new HeroStats
            {
                Dmg = tuning.heroDmg * (1f + Mod("heroDmgMul")),
                Rate = tuning.heroRate,
                Range = tuning.heroRange * (1f + Mod("heroRangeMul")),
                Speed = tuning.heroSpeed,
            };
        }

        // ---------------------------------------------------------------- 操作
        public RunResult Summon()
        {
            if (Status != "running") return RunResult.Fail("ended");

            var free = FreePlatforms();
            if (free.Count == 0) return RunResult.Fail("no-platform");

            int cost = SummonCost();
            if (Gold < cost) return new RunResult { Ok = false, Reason = "gold", Cost = cost };

            var ops = FormationOps();
            var op = ops[Rint(ops.Count)];
            var platform = free[Rint(free.Count)];

            var tower = new TowerState
            {
                Id = NextId("t"),
                PlatformId = platform.Id,
                ClassId = op.classId,
                OpId = op.id,
                Level = 1,
                Cd = 0f,
                Angle = -Mathf.PI / 2f,
                Aim = -Mathf.PI / 2f,
                Flash = 0f,
                BornAt = Time,
            };
            platform.Tower = tower;
            Towers.Add(tower);
            Gold -= cost;
            Summons++;

            var events = new List<STEvent>
            {
                new STEvent { Kind = STEventKind.Summon, Tower = tower, Platform = platform, Amount = cost },
            };

            var tuning = STData.T;
            if (Summons % tuning.summonsPerUnlock == 0)
            {
                int raw = tuning.initialUnlocked + (int)Mathf.Floor((float)Summons / tuning.summonsPerUnlock);
                int idx = UnlockedPlatformCount() - 1;
                if (raw <= Platforms.Count && idx >= 0)
                {
                    events.Add(new STEvent
                    {
                        Kind = STEventKind.Unlock,
                        Platform = Platforms[idx],
                        IntA = UnlockedPlatformCount(),
                    });
                }
            }

            EnsureGuards(events);
            return new RunResult { Ok = true, Tower = tower, Platform = platform, Cost = cost, Events = events };
        }

        public RunResult PurchaseUpgrade()
        {
            if (Status != "running") return RunResult.Fail("ended");
            if (PendingChoices.Count > 0)
            {
                return new RunResult { Ok = true, Choices = new List<string>(PendingChoices), Free = true };
            }

            int cost = UpgradeCost();
            if (Gold < cost) return new RunResult { Ok = false, Reason = "gold", Cost = cost };

            Gold -= cost;
            PendingChoices.Clear();
            PendingChoices.AddRange(AffixChoices(3));
            return new RunResult { Ok = true, Choices = new List<string>(PendingChoices), Cost = cost };
        }

        public List<string> AffixChoices(int count = 3)
        {
            var owned = new List<string>(Affixes);
            var pool = STData.AffixPool(Formation);
            var available = new List<string>();
            foreach (var id in pool)
            {
                if (!owned.Contains(id) && STData.GetAffix(id) != null) available.Add(id);
            }

            var picks = Shuffle(available);
            if (picks.Count > count) picks.RemoveRange(count, picks.Count - count);

            if (picks.Count < count)
            {
                var extras = new List<string>();
                foreach (var a in STData.Affixes)
                {
                    if (!owned.Contains(a.id) && !picks.Contains(a.id)) extras.Add(a.id);
                }
                foreach (var id in Shuffle(extras))
                {
                    if (picks.Count >= count) break;
                    picks.Add(id);
                }
            }

            var fillerId = STData.FillerAffix.id;
            while (picks.Count < count) picks.Add(fillerId);
            return picks;
        }

        public RunResult PickAffix(string id)
        {
            if (Status != "running") return RunResult.Fail("ended");
            if (!PendingChoices.Contains(id)) return RunResult.Fail("not-offered");

            Affixes.Add(id);
            Upgrades++;
            PendingChoices.Clear();
            RecomputeMods();

            var events = new List<STEvent> { new STEvent { Kind = STEventKind.Affix, Text = id } };
            EnsureGuards(events);
            return new RunResult { Ok = true, Text = id, Events = events };
        }

        public Dictionary<string, float> RecomputeMods()
        {
            var m = DefaultMods();
            foreach (var id in Affixes)
            {
                var entry = STData.GetAffix(id);
                if (entry == null) continue;
                foreach (var mod in entry.mods)
                {
                    m[mod.k] = (m.TryGetValue(mod.k, out var cur) ? cur : 0f) + mod.v;
                }
            }
            Mods = m;
            return m;
        }

        public RunResult DragTower(string fromId, string toId)
        {
            if (Status != "running") return RunResult.Fail("ended");

            var from = PlatformById(fromId);
            var to = PlatformById(toId);
            if (from == null || to == null || from.Tower == null) return RunResult.Fail("invalid");
            if (!IsPlatformUnlocked(to)) return RunResult.Fail("locked");
            if (from.Id == to.Id) return RunResult.Fail("same");

            var mover = from.Tower;

            if (to.Tower == null)
            {
                from.Tower = null;
                to.Tower = mover;
                mover.PlatformId = to.Id;
                var events = new List<STEvent>
                {
                    new STEvent { Kind = STEventKind.Move, Tower = mover, FromPlatform = from, ToPlatform = to },
                };
                EnsureGuards(events);
                return new RunResult { Ok = true, Kind = "move", Tower = mover, Events = events };
            }

            var target = to.Tower;
            if (target.OpId != mover.OpId) return RunResult.Fail("different-tower");
            if (target.Level != mover.Level) return RunResult.Fail("level-mismatch");
            if (mover.Level >= STData.T.maxTowerLevel) return RunResult.Fail("max-level");

            RemoveTower(mover);
            RemoveTower(target);
            from.Tower = null;
            to.Tower = null;

            var op = STData.GetOperative(mover.OpId);
            var merged = new TowerState
            {
                Id = NextId("t"),
                PlatformId = to.Id,
                ClassId = op.classId,
                OpId = op.id,
                Level = mover.Level + 1,
                Cd = 0f,
                Angle = -Mathf.PI / 2f,
                Aim = -Mathf.PI / 2f,
                Flash = 0f,
                BornAt = Time,
                Merged = true,
            };
            to.Tower = merged;
            Towers.Add(merged);

            float refund = Mod("mergeRefund");
            if (refund > 0f)
            {
                Gold += refund;
                TotalGoldEarned += refund;
            }

            var evts = new List<STEvent>
            {
                new STEvent
                {
                    Kind = STEventKind.Merge, Tower = merged, FromPlatform = from, ToPlatform = to,
                    IntA = merged.Level, Amount = refund,
                },
            };
            EnsureGuards(evts);
            return new RunResult { Ok = true, Kind = "merge", Tower = merged, Events = evts };
        }

        private void RemoveTower(TowerState tower)
        {
            Towers.Remove(tower);
            for (int i = Guards.Count - 1; i >= 0; i--)
            {
                if (Guards[i].TowerId == tower.Id) Guards.RemoveAt(i);
            }
        }

        public void MoveHero(Vector2 target)
        {
            Hero.Target = new Vector2(Mathf.Clamp(target.x, 18f, 1080f - 18f), Mathf.Clamp(target.y, 18f, 1370f - 18f));
            Hero.Moving = true;
        }

        // ---------------------------------------------------------------- 拦截单位
        private void EnsureGuards(List<STEvent> events)
        {
            var tuning = STData.T;
            int per = Mathf.Max(1, tuning.guardsPerTower);
            float gap = tuning.guardGap;

            var wanted = new List<(TowerState tower, float dist, int slot)>();
            foreach (var t in Towers)
            {
                if (t.ClassId != "intercept") continue;
                var p = PlatformById(t.PlatformId);
                if (p == null) continue;

                NearestPath(p.Pos, out float baseD, out _);
                for (int slot = 0; slot < per; slot++)
                {
                    float off = per <= 1 ? 0f : (slot - (per - 1) * 0.5f) * gap;
                    // 锚点必须夹在车道内：塔位若靠近路径起终点，
                    // baseD ± gap 会算出负值或超过总长，守卫会被摆到路面之外
                    // （表现为"守卫没在路上"/看着无法阻挡）。
                    float anchor = Mathf.Clamp(baseD + off, 40f, PathTotal - 40f);
                    wanted.Add((t, anchor, slot));
                }
            }

            for (int i = Guards.Count - 1; i >= 0; i--)
            {
                var g = Guards[i];
                bool keep = false;
                foreach (var w in wanted)
                {
                    if (w.tower.Id == g.TowerId && w.slot == g.Slot) { keep = true; break; }
                }
                if (!keep) Guards.RemoveAt(i);
            }

            foreach (var w in wanted)
            {
                var st = StatsOf(w.tower);
                GuardState found = null;
                foreach (var cand in Guards)
                {
                    if (cand.TowerId == w.tower.Id && cand.Slot == w.slot) { found = cand; break; }
                }

                if (found == null)
                {
                    var g = new GuardState
                    {
                        Id = NextId("g"),
                        TowerId = w.tower.Id,
                        Slot = w.slot,
                        Dist = w.dist,
                        Home = w.dist,
                        Pos = PathPos(w.dist),
                        Angle = PathAngle(w.dist),
                        Hp = st.GuardHp,
                        HpMax = st.GuardHp,
                        Dps = st.GuardDps,
                        Respawn = st.Respawn,
                        BornAt = Time,
                    };
                    Guards.Add(g);
                    events.Add(new STEvent { Kind = STEventKind.GuardSpawn, Guard = g });
                }
                else
                {
                    found.Home = w.dist;
                    found.HpMax = st.GuardHp;
                    found.Dps = st.GuardDps;
                    found.Respawn = st.Respawn;
                    found.Dist = Mathf.Clamp(found.Dist, found.Home - found.Leash, found.Home + found.Leash);
                    found.Pos = PathPos(found.Dist);
                    if (found.Hp > found.HpMax) found.Hp = found.HpMax;
                }
            }
        }

        // ---------------------------------------------------------------- 战斗
        /// <summary>同车道排队，避免敌人互相穿模（后面被前面顶住）。</summary>
        private void SeparateEnemies()
        {
            var ordered = new List<EnemyState>();
            foreach (var e in Enemies)
            {
                if (!e.Dead) ordered.Add(e);
            }
            if (ordered.Count < 2) return;

            ordered.Sort((a, b) => b.Dist.CompareTo(a.Dist));
            for (int i = 1; i < ordered.Count; i++)
            {
                var front = ordered[i - 1];
                var back = ordered[i];
                float gap = (front.Radius + back.Radius) * 0.62f;
                if (front.Dist - back.Dist < gap)
                {
                    back.Dist = Mathf.Max(0f, front.Dist - gap);
                    back.Pos = PathPos(back.Dist);
                    back.Angle = PathAngle(back.Dist);
                }
            }
        }

        /// <summary>拦截单位之间同样不重叠。</summary>
        private void SeparateGuards()
        {
            if (Guards.Count < 2) return;
            var ordered = new List<GuardState>();
            foreach (var g in Guards)
            {
                if (g.Hp > 0f) ordered.Add(g);
            }
            if (ordered.Count < 2) return;

            ordered.Sort((a, b) => b.Dist.CompareTo(a.Dist));
            for (int i = 1; i < ordered.Count; i++)
            {
                var front = ordered[i - 1];
                var back = ordered[i];
                float gap = front.Radius + back.Radius;
                if (front.Dist - back.Dist < gap)
                {
                    back.Dist = Mathf.Max(back.Home - back.Leash, front.Dist - gap);
                    back.Pos = PathPos(back.Dist);
                    back.Angle = PathAngle(back.Dist);
                }
            }
        }

        private float EnemySpeed(EnemyState e)
        {
            float s = e.Speed;
            if (e.SlowUntil > 0f)
            {
                float resist = e.Boss ? e.SlowResist : 0f;
                s *= 1f - e.SlowFactor * (1f - resist);
            }
            return s;
        }

        public List<(EnemyState enemy, float d)> EnemiesInRange(Vector2 pos, float radius)
        {
            var list = new List<(EnemyState, float)>();
            foreach (var e in Enemies)
            {
                if (e.Dead) continue;
                float d = Vector2.Distance(pos, e.Pos);
                if (d <= radius + e.Radius * 0.5f) list.Add((e, d));
            }
            return list;
        }

        /// <summary>射程内最靠近核心（里程最大）的敌人。</summary>
        public EnemyState PickTarget(Vector2 pos, float radius)
        {
            EnemyState best = null;
            float bestDist = float.NegativeInfinity;
            foreach (var e in Enemies)
            {
                if (e.Dead) continue;
                float d = Vector2.Distance(pos, e.Pos);
                if (d > radius + e.Radius * 0.5f) continue;
                if (e.Dist > bestDist)
                {
                    bestDist = e.Dist;
                    best = e;
                }
            }
            return best;
        }

        public float DealDamage(EnemyState e, float amount, float pierce = 0f, List<STEvent> events = null)
        {
            if (e == null || e.Dead || e.Hp <= 0f) return 0f;

            float dmg = amount;
            float execute = Mod("execute");
            if (execute > 0f && e.Hp < e.HpMax * execute) dmg *= 2f;

            bool crit = false;
            if (Mod("critChance") > 0f && Rng() < Mod("critChance"))
            {
                crit = true;
                dmg *= 1f + Mod("critMul");
            }

            float armor = Mathf.Max(0f, e.Armor * (1f - pierce));
            dmg = Mathf.Max(dmg * 0.15f, dmg - armor);
            if (e.ShredStacks > 0) dmg *= 1f + e.ShredStacks * Mod("shredPerHit");
            dmg = Mathf.Max(0.1f, dmg);

            e.Hp -= dmg;
            e.Flash = 0.12f;
            TotalDamage += dmg;
            if (events != null)
            {
                events.Add(new STEvent
                {
                    Kind = STEventKind.Hit, Enemy = e, Amount = dmg, Flag = crit, Pos = e.Pos,
                });
            }

            if (Mod("frostChance") > 0f && Rng() < Mod("frostChance"))
            {
                float resist = e.Boss ? e.SlowResist : 0f;
                e.SlowFactor = Mathf.Max(e.SlowFactor, Mod("frostSlow") * (1f - resist));
                e.SlowUntil = Mathf.Max(e.SlowUntil, Mod("frostDur"));
            }
            if (Mod("burnChance") > 0f && Rng() < Mod("burnChance"))
            {
                e.BurnUntil = Mathf.Max(e.BurnUntil, Mod("burnDur"));
                e.BurnDps = Mathf.Max(e.BurnDps, Mod("burnDps"));
            }
            if (Mod("shredMax") > 0f)
            {
                e.ShredStacks = Mathf.Min((int)Mod("shredMax"), e.ShredStacks + 1);
                e.ShredTimer = 4f;
            }

            if (e.Hp <= 0f) KillEnemy(e, events);
            return dmg;
        }

        public void KillEnemy(EnemyState e, List<STEvent> events = null)
        {
            if (e.Dead) return;
            e.Dead = true;
            e.Hp = 0f;

            int reward = STRound(e.Reward * Level.Mul("reward")) + (int)Mod("goldPerKill");
            Gold += reward;
            TotalGoldEarned += reward;
            Kills++;
            events?.Add(new STEvent { Kind = STEventKind.Kill, Enemy = e, IntA = reward, Pos = e.Pos });

            Enemies.Remove(e);
            if (e.Wave >= 0 && e.Wave < WaveAlive.Length) WaveAlive[e.Wave] = Mathf.Max(0, WaveAlive[e.Wave] - 1);
        }

        public void LeakEnemy(EnemyState e, List<STEvent> events = null)
        {
            if (e.Dead) return;
            e.Dead = true;

            float dmg = Mathf.Max(1f, e.Leak - Mod("leakReduce"));
            CoreHp -= dmg;
            LeakedCount++;
            Enemies.Remove(e);
            if (e.Wave >= 0 && e.Wave < WaveAlive.Length) WaveAlive[e.Wave] = Mathf.Max(0, WaveAlive[e.Wave] - 1);

            events?.Add(new STEvent { Kind = STEventKind.Leak, Enemy = e, Amount = dmg, Pos = CorePos });

            if (CoreHp <= 0f)
            {
                CoreHp = 0f;
                Finish("defeat", events);
            }
        }

        private void Finish(string result, List<STEvent> events)
        {
            if (Status != "running") return;
            Status = result;
            Ending = new EndingInfo
            {
                Status = result, Wave = WaveIndex, Kills = Kills, Leaked = LeakedCount,
                Time = Time, Gold = Gold, Summons = Summons, Upgrades = Upgrades,
            };
            events?.Add(new STEvent
            {
                Kind = result == "victory" ? STEventKind.Victory : STEventKind.Defeat,
            });
        }

        private EnemyState SpawnEnemy(ScheduleItem item, List<STEvent> events)
        {
            var baseData = STData.GetEnemy(item.Type);
            float hp = STRound(baseData.hp * Level.Mul("hp"));

            var e = new EnemyState
            {
                Id = NextId("e"),
                Type = item.Type,
                Name = baseData.name,
                Hp = hp,
                HpMax = hp,
                Speed = baseData.speed * Level.Mul("speed"),
                Armor = baseData.armor,
                Reward = baseData.reward,
                Leak = baseData.leak,
                Radius = baseData.radius,
                Atk = baseData.atk,
                Elite = baseData.elite,
                Boss = baseData.boss,
                Color = baseData.Accent,
                SlowResist = baseData.slowResist,
                Dist = 0f,
                Pos = SpawnPos,
                Angle = PathAngle(0f),
                Wave = item.Wave,
                Phase = Rng() * Tau,
            };

            Enemies.Add(e);
            WaveAlive[item.Wave]++;

            if (item.Wave + 1 > WaveIndex)
            {
                WaveIndex = item.Wave + 1;
                var wave = STData.Waves[item.Wave];
                events.Add(new STEvent
                {
                    Kind = STEventKind.Wave, IntA = WaveIndex, Text = wave.warning ?? "",
                });
            }

            events.Add(new STEvent { Kind = STEventKind.Spawn, Enemy = e });
            return e;
        }

        private bool FireTower(TowerState t, TowerStats st, List<STEvent> events)
        {
            var platform = PlatformById(t.PlatformId);
            if (platform == null) return false;
            if (t.ClassId == "intercept") return false;

            var ppos = platform.Pos;
            var target = PickTarget(ppos, st.Range);
            if (target == null) return false;

            t.Flash = 0.12f;
            var muzzle = ppos + new Vector2(0f, -10f);

            if (t.ClassId == "sniper")
            {
                Projectiles.Add(new ProjectileState
                {
                    Type = "beam", Pos = muzzle, Start = muzzle, Target = target.Pos,
                    Life = 0.2f, MaxLife = 0.2f,
                    Color = STData.GetClass("sniper").Accent,
                });
                DealDamage(target, st.Dmg, Mod("pierce"), events);
                target.TowerKiller = t.Id;
                events.Add(new STEvent
                {
                    Kind = STEventKind.Shot, Tower = t, Enemy = target, ClassId = "sniper",
                });
                return true;
            }

            if (t.ClassId == "siege")
            {
                Projectiles.Add(new ProjectileState
                {
                    Type = "shell", Pos = muzzle, Start = muzzle,
                    TargetId = target.Id, Target = target.Pos, Speed = 430f,
                    Dmg = st.Dmg, Splash = st.Splash, TowerId = t.Id,
                    Color = STData.GetClass("siege").Accent,
                });
                events.Add(new STEvent
                {
                    Kind = STEventKind.Shot, Tower = t, Enemy = target, ClassId = "siege",
                });
                return true;
            }

            Projectiles.Add(new ProjectileState
            {
                Type = "bullet", Pos = muzzle, TargetId = target.Id, Target = target.Pos,
                Speed = 1000f, Dmg = st.Dmg, TowerId = t.Id,
                Color = STData.GetClass("assault").Accent,
            });
            events.Add(new STEvent
            {
                Kind = STEventKind.Shot, Tower = t, Enemy = target, ClassId = "assault",
            });
            return true;
        }

        private void UpdateProjectiles(float dt, List<STEvent> events)
        {
            for (int i = Projectiles.Count - 1; i >= 0; i--)
            {
                var p = Projectiles[i];
                if (p.Type == "beam")
                {
                    p.Life -= dt;
                    if (p.Life <= 0f) Projectiles.RemoveAt(i);
                    continue;
                }

                if (!string.IsNullOrEmpty(p.TargetId))
                {
                    foreach (var e in Enemies)
                    {
                        if (e.Id == p.TargetId && !e.Dead)
                        {
                            p.Target = e.Pos;
                            break;
                        }
                    }
                }

                var delta = p.Target - p.Pos;
                float d = delta.magnitude;
                float step = p.Speed * dt;

                if (d <= step || d < 1f)
                {
                    if (p.Type == "shell")
                    {
                        ApplySplash(p.Target, p.Splash, p.Dmg, p.TowerId, events);
                        events.Add(new STEvent
                        {
                            Kind = STEventKind.Explosion, Pos = p.Target, Radius = p.Splash, Color = p.Color,
                        });
                    }
                    else
                    {
                        EnemyState hit = null;
                        foreach (var e in Enemies)
                        {
                            if (e.Id == p.TargetId && !e.Dead) { hit = e; break; }
                        }
                        if (hit != null)
                        {
                            DealDamage(hit, p.Dmg, Mod("pierce"), events);
                            hit.TowerKiller = p.TowerId;
                        }
                    }
                    Projectiles.RemoveAt(i);
                }
                else
                {
                    p.Pos += delta / d * step;
                    p.Angle = Mathf.Atan2(delta.y, delta.x);
                }
            }
        }

        private void ApplySplash(Vector2 pos, float radius, float dmg, string towerId, List<STEvent> events)
        {
            foreach (var h in EnemiesInRange(pos, radius))
            {
                var e = h.enemy;
                DealDamage(e, dmg, Mod("pierce"), events);
                if (!e.Dead) e.TowerKiller = towerId;
            }
        }

        private void UpdateHero(float dt, List<STEvent> events)
        {
            var st = HeroStatsNow();
            var pos = Hero.Pos;

            if (Hero.Moving)
            {
                var delta = Hero.Target - pos;
                float d = delta.magnitude;
                float step = st.Speed * dt;
                if (d <= step || d < 0.5f)
                {
                    Hero.Pos = Hero.Target;
                    Hero.Moving = false;
                }
                else
                {
                    Hero.Pos = pos + delta / d * step;
                    Hero.Angle = Mathf.Atan2(delta.y, delta.x);
                }
                pos = Hero.Pos;
            }

            Hero.Cd -= dt;
            if (Hero.Cd <= 0f)
            {
                var target = PickTarget(pos, st.Range);
                if (target != null)
                {
                    Hero.Angle = Mathf.Atan2(target.Pos.y - pos.y, target.Pos.x - pos.x);
                    Hero.Cd = 1f / st.Rate;
                    Projectiles.Add(new ProjectileState
                    {
                        Type = "bullet", Pos = pos + new Vector2(0f, -8f), TargetId = target.Id,
                        Target = target.Pos, Speed = 1100f, Dmg = st.Dmg,
                        TowerId = "", Color = Color.white, FromHero = true,
                    });
                    events.Add(new STEvent { Kind = STEventKind.HeroShot, Pos = pos });
                }
                else
                {
                    Hero.Cd = 0.1f;
                }
            }
        }

        private void CheckWaves(List<STEvent> events)
        {
            var tuning = STData.T;
            for (int w = 0; w < STData.Waves.Count; w++)
            {
                if (WavePaid[w]) continue;
                if (WaveIndex > w && WaveAlive[w] == 0)
                {
                    int bonus = tuning.waveBonusBase + tuning.waveBonusPerWave * (w + 1) + (int)Mod("waveBonus");
                    Gold += bonus;
                    TotalGoldEarned += bonus;
                    WavePaid[w] = true;
                    WaveCleared = w + 1;
                    events.Add(new STEvent { Kind = STEventKind.WaveClear, IntA = w + 1, Amount = bonus });
                }
            }

            if (ScheduleIndex >= Schedule.Count && Enemies.Count == 0 && Status == "running")
            {
                Finish("victory", events);
            }
        }

        // ---------------------------------------------------------------- 主步进
        public List<STEvent> Step(float dt)
        {
            var events = new List<STEvent>();
            if (Status != "running") return events;

            dt = Mathf.Clamp(dt, 0f, 0.06f);
            Time += dt;

            // 1. 刷怪
            while (ScheduleIndex < Schedule.Count && Schedule[ScheduleIndex].T <= Time)
            {
                SpawnEnemy(Schedule[ScheduleIndex], events);
                ScheduleIndex++;
            }

            // 2. 状态衰减 / 灼烧
            var snapshot = new List<EnemyState>(Enemies);
            foreach (var e in snapshot)
            {
                if (e.Dead) continue;

                if (e.SlowUntil > 0f)
                {
                    e.SlowUntil = Mathf.Max(0f, e.SlowUntil - dt);
                    if (e.SlowUntil == 0f) e.SlowFactor = 0f;
                }
                if (e.Flash > 0f) e.Flash = Mathf.Max(0f, e.Flash - dt);

                if (e.BurnUntil > 0f)
                {
                    e.BurnUntil = Mathf.Max(0f, e.BurnUntil - dt);
                    e.Hp -= e.BurnDps * dt;
                    TotalDamage += e.BurnDps * dt;
                    if (e.Hp <= 0f)
                    {
                        KillEnemy(e, events);
                        continue;
                    }
                }

                if (e.ShredStacks > 0 && e.ShredTimer.HasValue)
                {
                    e.ShredTimer = e.ShredTimer.Value - dt;
                    if (e.ShredTimer.Value <= 0f)
                    {
                        e.ShredStacks = 0;
                        e.ShredTimer = null;
                    }
                }
            }

            // 3. 守卫复活 + 在锚点附近移动拦截
            foreach (var g in Guards)
            {
                g.Blocking.Clear();
                if (g.Hp <= 0f)
                {
                    g.RespawnTimer -= dt;
                    if (g.RespawnTimer <= 0f)
                    {
                        g.Hp = g.HpMax;
                        g.RespawnTimer = 0f;
                        g.Dist = g.Home;
                        g.Pos = PathPos(g.Dist);
                        events.Add(new STEvent { Kind = STEventKind.GuardReturn, Guard = g });
                    }
                    continue;
                }

                float home = g.Home;
                float leash = g.Leash;
                float targetDist = home;
                float best = float.PositiveInfinity;

                foreach (var e in Enemies)
                {
                    if (e.Dead) continue;
                    float ed = e.Dist;
                    if (ed < home - leash - 90f || ed > home + leash + 70f) continue;
                    float dd = Mathf.Abs(ed - home);
                    if (dd < best)
                    {
                        best = dd;
                        targetDist = Mathf.Clamp(ed - 34f, home - leash, home + leash);
                    }
                }

                float cur = g.Dist;
                if (Mathf.Abs(targetDist - cur) > 1.5f)
                {
                    g.Dist = Mathf.MoveTowards(cur, targetDist, g.Speed * dt);
                    g.Pos = PathPos(g.Dist);
                    g.Angle = PathAngle(g.Dist);
                }
            }

            // 4. 敌人移动 / 攻守
            var intercept = STData.GetClass("intercept");
            int blockCap = intercept != null ? intercept.blockCap : 2;

            foreach (var e in new List<EnemyState>(Enemies))
            {
                if (e.Dead) continue;

                GuardState blocker = null;
                foreach (var g in Guards)
                {
                    if (g.Hp <= 0f) continue;
                    if (g.Dist > e.Dist - 8f && g.Dist < e.Dist + 170f && g.Blocking.Count < blockCap)
                    {
                        if (blocker == null || g.Dist < blocker.Dist) blocker = g;
                    }
                }

                if (blocker != null)
                {
                    float stopAt = blocker.Dist - 30f;
                    if (e.Dist >= stopAt)
                    {
                        blocker.Blocking.Add(e.Id);
                        blocker.Hp -= e.Atk * dt;
                        if (blocker.Hp <= 0f)
                        {
                            blocker.Hp = 0f;
                            blocker.RespawnTimer = blocker.Respawn;
                            events.Add(new STEvent { Kind = STEventKind.GuardDown, Guard = blocker });
                            blocker.Blocking.Clear();
                        }
                        else
                        {
                            TotalDamage += blocker.Dps * dt;
                        }

                        e.Hp -= blocker.Dps * dt;
                        if (e.Hp <= 0f)
                        {
                            KillEnemy(e, events);
                            continue;
                        }

                        e.Pos = PathPos(e.Dist);
                        e.Angle = PathAngle(e.Dist);
                        continue;
                    }
                }

                e.Dist += EnemySpeed(e) * dt;
                if (e.Dist >= PathTotal)
                {
                    LeakEnemy(e, events);
                    continue;
                }
                e.Pos = PathPos(e.Dist);
                e.Angle = PathAngle(e.Dist);
            }

            // 4b. 碰撞体积
            SeparateEnemies();

            if (Status != "running") return events;

            // 5. 开火（必须先转到目标角度）
            foreach (var t in Towers)
            {
                if (t.Flash > 0f) t.Flash = Mathf.Max(0f, t.Flash - dt);
                if (t.ClassId == "intercept") continue;

                t.Cd -= dt;
                var st = StatsOf(t);
                var plat = PlatformById(t.PlatformId);
                if (plat == null) continue;

                var target = PickTarget(plat.Pos, st.Range);
                if (target == null) continue;

                float desired = Mathf.Atan2(target.Pos.y - plat.Pos.y, target.Pos.x - plat.Pos.x);
                var cls = STData.GetClass(t.ClassId);
                float turnRate = cls != null ? cls.turn : 4f;
                float aim = t.Aim;
                // 注意：Mathf.DeltaAngle 的形参是「度」，这里是弧度。
                // 直接把弧度传进去会把差值缩小 57 倍（炮塔几乎不转），
                // 因此改用 WrapPi 求最短角差（与 Godot 的 wrapf(…, -PI, PI) 等价）。
                float diff = WrapPi(desired - aim);
                float stepAim = Mathf.Clamp(diff, -turnRate * dt, turnRate * dt);
                t.Aim = WrapPi(aim + stepAim);
                t.Angle = t.Aim;

                if (Mathf.Abs(WrapPi(desired - t.Aim)) <= 0.14f && t.Cd <= 0f)
                {
                    bool fired = FireTower(t, st, events);
                    t.Cd = fired ? 1f / st.Rate : 0f;
                }
            }

            // 6. 弹道
            UpdateProjectiles(dt, events);

            // 7. 英雄
            UpdateHero(dt, events);

            // 8. 波次结算
            CheckWaves(events);

            return events;
        }

        /// <summary>固定步长推进若干秒（测试与机器人用）。</summary>
        public List<STEvent> Advance(float seconds, float dt = 1f / 60f)
        {
            float remaining = Mathf.Max(0f, seconds);
            var all = new List<STEvent>();
            int guard = 0;
            while (remaining > 0f && Status == "running" && guard < 300000)
            {
                guard++;
                float s = Mathf.Min(dt, remaining);
                all.AddRange(Step(s));
                remaining -= s;
            }
            return all;
        }

        // ---------------------------------------------------------------- 数学工具
        public static float WrapPi(float a)
        {
            while (a > Mathf.PI) a -= Tau;
            while (a < -Mathf.PI) a += Tau;
            return a;
        }

        /// <summary>四舍五入（半数远离零，与 GDScript round() 一致）。</summary>
        public static int STRound(float v) => (int)Math.Round(v, MidpointRounding.AwayFromZero);
    }

    /// <summary>Godot String::hash 的等价实现（djb2 变体），保证种子与 Godot 端一致。</summary>
    public static class STHash
    {
        public static uint String(string s)
        {
            uint hash = 5381u;
            if (string.IsNullOrEmpty(s)) return 0u;
            foreach (char c in s)
            {
                hash = ((hash << 5) + hash) ^ c;
            }
            return hash;
        }
    }
}
