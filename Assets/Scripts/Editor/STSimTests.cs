// 星轨防线 · 逻辑自检（对应 Godot tests/sim_test.gd 的 17 组断言）
//
// 通过 Unity 批处理入口执行：
//   Unity.exe -batchmode -quit -projectPath <ST> -executeMethod StarLane.EditorTools.STSetup.RunSimTests
// 结果写入 tests.log，失败时进程退出码为 1（便于 CI 判定）。

using System;
using System.Collections.Generic;
using UnityEngine;

namespace StarLane.EditorTools
{
    public static class STSimTests
    {
        private static int _pass;
        private static int _fail;
        private static readonly List<string> Log = new List<string>();

        private static readonly string[] Form = { "ran", "vega", "grom", "ella" };
        private static readonly string[] NoIntercept = { "ran", "vega", "grom" };

        public static int Run()
        {
            _pass = 0;
            _fail = 0;
            Log.Clear();

            var t0 = DateTime.UtcNow;
            STData.Load();

            TestWaves();
            TestScheduleBatches();
            TestPlatformsInitial();
            TestSummonBasic();
            TestSummonLimits();
            TestPlatformUnlockEveryThree();
            TestDragAndMerge();
            TestAffixDraw();
            TestAffixExhaustion();
            TestAffixEffects();
            TestGuardsPerTower();
            TestCollisionSpacing();
            TestHeroMove();
            TestDefeatWithoutDefense();
            TestBotVictory();
            TestDifficultyGradient();
            TestDeterminism();

            var ms = (int)(DateTime.UtcNow - t0).TotalMilliseconds;
            Log.Add("---");
            Log.Add($"RESULT {_pass}/{_pass + _fail} passed in {ms} ms");
            Debug.Log(string.Join("\n", Log));
            return _fail == 0 ? 0 : 1;
        }

        public static IReadOnlyList<string> Lines => Log;

        // ---------------------------------------------------------------- 断言
        private static void Ok(bool cond, string name, string detail = null)
        {
            if (cond)
            {
                _pass++;
                Log.Add("PASS  " + name);
            }
            else
            {
                _fail++;
                Log.Add("FAIL  " + name + (detail != null ? "   " + detail : ""));
            }
        }

        private static void Eq<T>(T a, T b, string name)
        {
            Ok(EqualityComparer<T>.Default.Equals(a, b), name, $"expected {b} got {a}");
        }

        private static RunState NewRun(int level = 0, int seed = 12345, string[] formation = null)
        {
            return new RunState(level, formation ?? Form, seed);
        }

        // ---------------------------------------------------------------- 用例
        private static void TestWaves()
        {
            Eq(STData.Waves.Count, 10, "每关 10 波");
            Eq(STData.Waves[4].warning ?? "", "elite", "第 5 波为精英波");
            Eq(STData.Waves[9].warning ?? "", "boss", "第 10 波为 BOSS 波");

            var bossWaves = new List<int>();
            for (int i = 0; i < STData.Waves.Count; i++)
            {
                foreach (var b in STData.Waves[i].batches)
                {
                    if (b.type == "boss") bossWaves.Add(i + 1);
                }
            }
            Ok(bossWaves.Count == 1 && bossWaves[0] == 10, "BOSS 只出现在第 10 波", string.Join(",", bossWaves));

            bool eliteInW5 = false;
            foreach (var b in STData.Waves[4].batches)
            {
                if (b.type == "elite") eliteInW5 = true;
            }
            Ok(eliteInW5, "第 5 波包含精英敌人");
        }

        private static void TestScheduleBatches()
        {
            var run = NewRun();
            var wave1 = new List<RunState.ScheduleItem>();
            foreach (var item in run.Schedule)
            {
                if (item.Wave == 0) wave1.Add(item);
            }
            Eq(wave1.Count, 8, "第 1 波 8 只敌人");
            float span = wave1[wave1.Count - 1].T - wave1[0].T;
            Ok(span > 5f, $"波内分批次出现 ({span:F1}s)");
        }

        private static void TestPlatformsInitial()
        {
            var run = NewRun();
            Eq(run.UnlockedPlatformCount(), 6, "开局解锁 6 个塔台");
            Eq(run.Platforms.Count, 14, "关卡 1 共 14 个塔台");
            Ok(run.IsPlatformUnlocked(run.Platforms[5]), "第 6 台已解锁");
            Ok(!run.IsPlatformUnlocked(run.Platforms[6]), "第 7 台锁定");

            for (int i = 0; i < STData.Levels.Count; i++)
            {
                var r2 = NewRun(i, 1);
                Ok(r2.Platforms.Count >= 12 && r2.Platforms.Count <= 15,
                    $"关卡 {i + 1} 塔台数量在 12–15 之间: {r2.Platforms.Count}");

                var lv = STData.Levels[i];
                float total = 0f;
                for (int k = 0; k < lv.path.Count - 1; k++)
                {
                    total += Vector2.Distance(lv.path[k].ToVector2(), lv.path[k + 1].ToVector2());
                }
                Ok(lv.path.Count >= 8 && total > 1800f, $"关卡 {i + 1} 路径长度合理: {total:F0} px");
            }
        }

        private static void TestSummonBasic()
        {
            var run = NewRun();
            run.Gold = 9999f;
            var costs = new List<int>();
            for (int i = 0; i < 3; i++)
            {
                costs.Add(run.SummonCost());
                var res = run.Summon();
                Ok(res.Ok, $"第 {i + 1} 次召唤成功");
                Eq(res.Tower.Level, 1, "召唤出 1 级塔");
                Ok(run.IsPlatformUnlocked(run.PlatformById(res.Platform.Id)), "落在已解锁塔台");
                var op = STData.GetOperative(res.Tower.OpId);
                Ok(op != null && Array.IndexOf(Form, op.id) >= 0, "召唤职业来自编队");
            }
            Ok(costs[1] > costs[0] && costs[2] > costs[1], "召唤价格递增 " + string.Join("/", costs));
        }

        private static void TestSummonLimits()
        {
            var run = NewRun();
            run.Gold = 0f;
            var poor = run.Summon();
            Ok(!poor.Ok && poor.Reason == "gold", "金币不足时召唤失败");
            Eq(run.Towers.Count, 0, "失败不改变塔数");

            var run2 = NewRun();
            run2.Gold = 99999f;
            int okCount = 0;
            RunResult last = null;
            while (okCount < 40)
            {
                var r = run2.Summon();
                if (!r.Ok)
                {
                    last = r;
                    break;
                }
                okCount++;
            }
            Ok(okCount >= 6, $"连续召唤 {okCount} 次成功");
            Eq(run2.Towers.Count, run2.UnlockedPlatformCount(), "已解锁台面全满");
            Ok(last != null && last.Reason == "no-platform", "塔台已满时提示 no-platform");
        }

        private static void TestPlatformUnlockEveryThree()
        {
            var run = NewRun();
            run.Gold = 999999f;
            var unlockedAt = new List<int>();

            for (int i = 0; i < 27; i++)
            {
                while (run.FreePlatforms().Count == 0)
                {
                    var placed = new List<PlatformState>();
                    foreach (var p in run.Platforms)
                    {
                        if (p.Tower != null) placed.Add(p);
                    }
                    if (placed.Count < 2) break;

                    var a = placed[0];
                    var b = placed[1];
                    int lv = Mathf.Min(a.Tower.Level, b.Tower.Level);
                    if (lv >= STData.T.maxTowerLevel) lv = 1;
                    a.Tower.Level = lv;
                    b.Tower.Level = lv;
                    b.Tower.OpId = a.Tower.OpId;
                    b.Tower.ClassId = a.Tower.ClassId;
                    var r = run.DragTower(a.Id, b.Id);
                    Ok(r.Ok, "合成腾出空台");
                    if (!r.Ok) break;
                }

                int before = run.UnlockedPlatformCount();
                var res = run.Summon();
                Ok(res.Ok, $"第 {i + 1} 次召唤成功（解锁测试）");
                if (run.UnlockedPlatformCount() > before) unlockedAt.Add(run.Summons);
            }

            int total = run.Platforms.Count;
            int step = STData.T.summonsPerUnlock;
            var expect = new List<int>();
            for (int i = step; i <= (total - STData.T.initialUnlocked) * step; i += step) expect.Add(i);

            Ok(unlockedAt.Count == expect.Count, "每 3 次召唤解锁 1 个塔台",
                "actual " + string.Join(",", unlockedAt) + " expect " + string.Join(",", expect));
            Eq(run.UnlockedPlatformCount(), total, "最终解锁全部塔台");
        }

        private static void TestDragAndMerge()
        {
            var run = NewRun();
            run.Gold = 999999f;

            var pair = new List<PlatformState>();
            int guard = 0;
            while (pair.Count == 0 && guard < 60)
            {
                guard++;
                run.Summon();
                var byOp = new Dictionary<string, PlatformState>();
                foreach (var p in run.Platforms)
                {
                    if (p.Tower == null) continue;
                    var op = p.Tower.OpId;
                    if (byOp.TryGetValue(op, out var other) && other.Tower.Level == p.Tower.Level)
                    {
                        pair.Add(other);
                        pair.Add(p);
                        break;
                    }
                    byOp[op] = p;
                }
            }

            Ok(pair.Count == 2, "召唤出两座相同的防御塔");
            if (pair.Count != 2) return;

            var opBefore = pair[0].Tower.OpId;
            var merged = run.DragTower(pair[0].Id, pair[1].Id);
            Ok(merged.Ok && merged.Kind == "merge", "相同的塔拖到一起合成");
            Eq(merged.Tower.Level, 2, "合成后等级 +1");
            Eq(merged.Tower.OpId, opBefore, "合成保留同一种防御塔");
            Ok(pair[0].Tower == null && pair[1].Tower != null && pair[1].Tower.Level == 2, "两塔合并只留新塔");

            var moved = run.DragTower(pair[1].Id, pair[0].Id);
            Ok(moved.Ok && moved.Kind == "move", "拖到空台换位");

            PlatformState a1 = null, a2 = null;
            foreach (var p in run.Platforms)
            {
                if (p.Tower == null || p.Tower.Level != 1) continue;
                if (a1 == null) a1 = p;
                else if (p.Tower.OpId != a1.Tower.OpId) { a2 = p; break; }
            }
            if (a1 != null && a2 != null)
            {
                var diff = run.DragTower(a2.Id, a1.Id);
                Ok(!diff.Ok && diff.Reason == "different-tower", "不同的防御塔拒绝合成");
            }

            PlatformState lv1 = null, lv2 = null;
            foreach (var p in run.Platforms)
            {
                if (p.Tower != null && p.Tower.Level >= 2 && p.Tower.OpId == opBefore) { lv2 = p; break; }
            }
            if (lv2 != null)
            {
                foreach (var p in run.Platforms)
                {
                    if (p.Tower == null || p == lv2) continue;
                    if (p.Tower.Level == 1 && p.Tower.OpId == opBefore) { lv1 = p; break; }
                }
            }
            if (lv1 != null && lv2 != null)
            {
                var mismatch = run.DragTower(lv1.Id, lv2.Id);
                Ok(!mismatch.Ok && mismatch.Reason == "level-mismatch", "等级不同拒绝合成");
            }
            else
            {
                Ok(true, "等级不同拒绝合成（无同种塔时跳过）");
            }

            var run4 = NewRun();
            run4.Gold = 999999f;
            run4.Summon();
            run4.Summon();
            var p4 = new List<PlatformState>();
            foreach (var p in run4.Platforms)
            {
                if (p.Tower != null) p4.Add(p);
            }
            p4[1].Tower.OpId = p4[0].Tower.OpId;
            p4[0].Tower.Level = 4;
            p4[1].Tower.Level = 4;
            var maxed = run4.DragTower(p4[1].Id, p4[0].Id);
            Ok(!maxed.Ok && maxed.Reason == "max-level", "4 级满级拒绝合成");
        }

        private static void TestAffixDraw()
        {
            var run = NewRun();
            run.Gold = 9999f;
            int cost = run.UpgradeCost();
            var res = run.PurchaseUpgrade();
            Ok(res.Ok, "购买升级成功");
            Eq(res.Choices.Count, 3, "抽取 3 个词条");

            var unique = new HashSet<string>(res.Choices);
            Eq(unique.Count, 3, "三个候选互不重复");

            var pool = STData.AffixPool(run.Formation);
            foreach (var c in res.Choices)
            {
                Ok(pool.Contains(c) || c == STData.FillerAffix.id, "候选来自编队词条池: " + c);
            }
            Eq((int)run.Gold, (int)(9999 - cost), "扣除升级费用");

            var bad = run.PickAffix("nope");
            Ok(!bad.Ok, "非法选择被拒绝");
            var pick = run.PickAffix(res.Choices[0]);
            Ok(pick.Ok, "选择词条生效");
            Eq(run.Affixes.Count, 1, "词条记录 1 条");
            Eq(run.PendingChoices.Count, 0, "选择后清空待选");
            Ok(run.UpgradeCost() > cost, "升级价格递增");
        }

        private static void TestAffixExhaustion()
        {
            var run = NewRun();
            run.Gold = 1e9f;
            int guard = 0;
            while (guard < 40)
            {
                guard++;
                var res = run.PurchaseUpgrade();
                if (!res.Ok) break;
                run.PickAffix(res.Choices[0]);
            }

            bool allOwned = true;
            foreach (var id in STData.AffixPool(run.Formation))
            {
                if (!run.Affixes.Contains(id)) allOwned = false;
            }
            Ok(allOwned, "池中词条可全部获得");

            float before = run.Mod("reinforce");
            var res2 = run.PurchaseUpgrade();
            bool onlyFiller = true;
            foreach (var c in res2.Choices)
            {
                if (c != STData.FillerAffix.id) onlyFiller = false;
            }
            Ok(onlyFiller, "池子耗尽后只出通用强化");
            run.PickAffix(res2.Choices[0]);
            Ok(run.Mod("reinforce") > before, "通用强化可叠加");
        }

        private static void TestAffixEffects()
        {
            var run = new RunState(0, NoIntercept, 12345);
            run.Gold = 99999f;
            run.Summon();
            var tower = run.Towers[0];
            Ok(tower.ClassId != "intercept", "测试编队不会召唤到拦截塔");

            var baseStats = run.StatsOf(tower);
            run.Affixes.Add("g_dmg");
            run.Affixes.Add("a_overclock");
            run.RecomputeMods();
            var buffed = run.StatsOf(tower);
            Ok(buffed.Dmg > baseStats.Dmg, "高爆核心提升伤害");
            Ok(buffed.Rate > baseStats.Rate, "超频协议提升攻速");
        }

        private static void TestGuardsPerTower()
        {
            var run = new RunState(0, new[] { "nova", "ella" }, 4242);
            run.Gold = 99999f;

            int guard = 0;
            while (guard < 40 && (run.Towers.Count == 0 || run.Towers[0].ClassId != "intercept"))
            {
                guard++;
                run.Summon();
            }

            bool found = false;
            foreach (var t in run.Towers)
            {
                if (t.ClassId == "intercept") { found = true; break; }
            }
            if (!found)
            {
                Ok(true, "编队无拦截职业，跳过路障数量检查");
                return;
            }

            run.Advance(0.02f);

            int perTower = 0;
            bool capOk = true;
            foreach (var t in run.Towers)
            {
                if (t.ClassId != "intercept") continue;
                int n = 0;
                foreach (var g in run.Guards)
                {
                    if (g.TowerId == t.Id) n++;
                }
                perTower = n;
                foreach (var g in run.Guards)
                {
                    if (g.Blocking.Count > 2) capOk = false;
                }
            }
            Eq(perTower, 2, "每座拦截塔生成 2 只拦截单位");
            Ok(capOk, "每只拦截单位最多拦 2 个敌人");
        }

        private static void TestCollisionSpacing()
        {
            var run = new RunState(0, NoIntercept, 777);
            run.Gold = 99999f;
            run.Advance(6f);

            int bad = 0;
            int checkedCount = 0;
            for (int i = 0; i < run.Enemies.Count; i++)
            {
                for (int j = i + 1; j < run.Enemies.Count; j++)
                {
                    var a = run.Enemies[i];
                    var b = run.Enemies[j];
                    if (a.Dead || b.Dead) continue;
                    checkedCount++;
                    float gap = (a.Radius + b.Radius) * 0.55f;
                    if (Mathf.Abs(a.Dist - b.Dist) < gap) bad++;
                }
            }
            Ok(checkedCount > 0 && bad == 0, $"同车道敌人保持碰撞间距（检查 {checkedCount} 对）");
        }

        private static void TestHeroMove()
        {
            var run = NewRun();
            run.MoveHero(new Vector2(300f, 900f));
            run.Advance(3f);
            Ok(Vector2.Distance(run.Hero.Pos, new Vector2(300f, 900f)) < 2f, "英雄移动到目标点");
            run.MoveHero(new Vector2(-5000f, 99999f));
            Ok(run.Hero.Target.x >= 18f && run.Hero.Target.y <= 1370f, "目标点被限制在战场内");
        }

        private static void TestDefeatWithoutDefense()
        {
            var run = NewRun();
            run.Advance(600f);
            Eq(run.Status, "defeat", "不设防时判负");
            Eq(run.CoreHp, 0f, "核心耐久归零");
            Ok(run.LeakedCount > 0, "有漏怪记录");
        }

        private static void TestBotVictory()
        {
            var run = RunBot(0, Form, 7);
            Eq(run.Status, "victory", "机器人通关第 1 关");
            Ok(run.Gold >= 0f, "金币不会为负");

            int paid = 0;
            foreach (var p in run.WavePaid)
            {
                if (p) paid++;
            }
            Eq(paid, 10, "10 个波次各结算一次奖励");
            Eq(run.Enemies.Count, 0, "通关时场上无敌人");
        }

        private static void TestDifficultyGradient()
        {
            int[] seeds = { 1, 3, 7, 11, 42, 99, 777 };
            float hp1 = 0f, hp3 = 0f;
            int wins1 = 0;
            foreach (var s in seeds)
            {
                var r1 = RunBot(0, Form, s);
                var r3 = RunBot(2, Form, s);
                hp1 += r1.CoreHp;
                hp3 += r3.CoreHp;
                if (r1.Status == "victory") wins1++;
            }
            Eq(wins1, seeds.Length, "第 1 关稳定通关");
            Ok(hp3 < hp1, $"第 3 关消耗更多核心耐久 ({hp3:F0} < {hp1:F0})");
        }

        private static void TestDeterminism()
        {
            var a = RunBot(0, Form, 2024);
            var b = RunBot(0, Form, 2024);
            Eq(a.Status, b.Status, "同种子结果一致");
            Eq((int)a.CoreHp, (int)b.CoreHp, "同种子核心耐久一致");
            Eq(a.Summons, b.Summons, "同种子召唤次数一致");
        }

        // ---------------------------------------------------------------- 机器人
        /// <summary>与 Godot 端 run_bot 等价的简单机器人：合成 → 召唤 → 升级 → 移动英雄。</summary>
        public static RunState RunBot(int level, IReadOnlyList<string> formation, int seed)
        {
            var run = new RunState(level, formation, seed);
            const float dt = 1f / 60f;
            float acc = 0f;
            int guard = 0;

            while (run.Status == "running" && run.Time < 1200f && guard < 200000)
            {
                guard++;
                run.Step(dt);
                acc += dt;
                if (acc >= 1f)
                {
                    acc = 0f;
                    BotAct(run);
                    EnemyState best = null;
                    foreach (var e in run.Enemies)
                    {
                        if (best == null || e.Dist > best.Dist) best = e;
                    }
                    if (best != null) run.MoveHero(best.Pos);
                }
            }
            return run;
        }

        private static void BotAct(RunState run)
        {
            var placed = new List<TowerState>(run.Towers);
            placed.Sort((a, b) => a.Level.CompareTo(b.Level));

            foreach (var a in placed)
            {
                if (a.Level >= 4) continue;
                foreach (var b in placed)
                {
                    if (a.Id == b.Id || a.Level != b.Level || a.OpId != b.OpId) continue;

                    run.NearestPath(run.PlatformById(a.PlatformId).Pos, out _, out float da);
                    run.NearestPath(run.PlatformById(b.PlatformId).Pos, out _, out float db);
                    var from = da < db ? a : b;
                    var to = from.Id == a.Id ? b : a;
                    var res = run.DragTower(from.PlatformId, to.PlatformId);
                    if (res.Ok) return;
                }
            }

            if (run.FreePlatforms().Count > 0 && run.Gold >= run.SummonCost())
            {
                run.Summon();
                return;
            }

            if (run.PendingChoices.Count > 0)
            {
                run.PickAffix(run.PendingChoices[0]);
                return;
            }

            if (run.Gold >= run.UpgradeCost() * 1.15f)
            {
                var up = run.PurchaseUpgrade();
                if (up.Ok) run.PickAffix(up.Choices[0]);
            }
        }
    }
}
