// 星轨防线 · 自动截图与真实指针自检（对应 Godot main.gd 的 --shots 流程）
//
// 打包后运行：
//   ST.exe -autoshot -screen-width 540 -screen-height 960 -screen-fullscreen 0
//
// 会依次输出菜单 / 关卡 / 编队 / 战斗 / 词条 / 结算 / 说明 / 暂停 等截图到 ST/Shots，
// 并做 4 项真实指针输入验证（点召唤按钮、点地移动英雄、拖拽合成、等级不同拒绝合成）。

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace StarLane
{
    public class STAutoShot : MonoBehaviour
    {
        private STMain _main;

        public static bool Requested()
        {
            foreach (var arg in System.Environment.GetCommandLineArgs())
            {
                if (arg == "-autoshot") return true;
            }
            return false;
        }

        public static bool GalleryRequested()
        {
            foreach (var arg in System.Environment.GetCommandLineArgs())
            {
                if (arg == "-gallery") return true;
            }
            return false;
        }

        private IEnumerator Start()
        {
            _main = FindAnyObjectByType<STMain>();
            if (_main == null) yield break;

            yield return new WaitForSecondsRealtime(0.6f);

            if (GalleryRequested())
            {
                yield return RunGallery();
                yield break;
            }
            if (!Requested()) yield break;

            yield return RunShots();
        }

        private IEnumerator RunShots()
        {
            yield return Shot("01-menu");

            _main.ShowLevels();
            yield return new WaitForSecondsRealtime(0.35f);
            yield return Shot("02-levels");

            _main.ShowFormation(STMode.Levels);
            yield return new WaitForSecondsRealtime(0.35f);
            yield return Shot("03-formation");

            _main.StartLevel(1);
            yield return new WaitForSecondsRealtime(0.7f);
            yield return Shot("04-battle-start");

            for (int i = 0; i < 4; i++)
            {
                _main.OnSummon();
                yield return new WaitForSecondsRealtime(0.15f);
            }
            yield return Shot("05-after-summon");

            yield return VerifyInput();
            yield return Shot("05b-after-input");

            _main.Bot(70f);
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Shot("06-mid-run");
            Debug.Log("FSM-MID " + _main.Battle.DebugLine());
            Debug.Log("AIM " + _main.Battle.DebugAim());

            _main.Run.Gold += 300f;
            _main.OnUpgrade();
            yield return new WaitForSecondsRealtime(0.45f);
            yield return Shot("07-affix-modal");

            if (_main.Run.PendingChoices.Count > 0) PickFirstChoice();
            yield return new WaitForSecondsRealtime(0.1f);

            _main.Bot(60f);
            while (_main.Run.Status == "running" && _main.Run.WaveIndex < 5) _main.Bot(20f);
            yield return new WaitForSecondsRealtime(0.35f);
            yield return Shot("08-elite");

            while (_main.Run.Status == "running" && _main.Run.WaveIndex < 10) _main.Bot(30f);
            _main.Banner.Show("BOSS APPROACHING", "旗舰 · 湮灭级", "第 10 波 · 最终目标进入轨道",
                "boss", STAssets.ThreatSprite("boss"));
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Shot("09-boss");

            while (_main.Run.Status == "running") _main.Bot(30f);
            yield return new WaitForSecondsRealtime(1.4f);
            Debug.Log($"[ST] result check status={_main.Run.Status} wave={_main.Run.WaveIndex} " +
                      $"finished={_main.Finished} paused={_main.Paused} modal={_main.HasModal}");
            yield return Shot("10-result");

            _main.ShowGuide();
            yield return new WaitForSecondsRealtime(0.4f);
            yield return Shot("11-guide");

            _main.StartLevel(1);
            yield return new WaitForSecondsRealtime(0.6f);
            _main.OnPauseClicked();
            yield return new WaitForSecondsRealtime(0.4f);
            yield return Shot("12-pause");

            Debug.Log($"SHOTS DONE status={_main.Run.Status} wave={_main.Run.WaveIndex} core={(int)_main.Run.CoreHp}");
            Debug.Log("FSM " + _main.Battle.DebugLine());
            Debug.Log("AIM " + _main.Battle.DebugAim());

            yield return new WaitForSecondsRealtime(0.3f);
            Application.Quit();
        }

        private void PickFirstChoice()
        {
            var choices = new List<string>(_main.Run.PendingChoices);
            if (choices.Count == 0) return;
            // 通过反射调用私有 PickAffix 会破坏封装，这里直接走公开路径：
            // 模拟点击第一张词条卡片
            var canvas = GameObject.Find("ModalCanvas");
            if (canvas == null) return;
            foreach (var btn in canvas.GetComponentsInChildren<Button>())
            {
                if (btn.name.StartsWith("Affix-"))
                {
                    btn.onClick.Invoke();
                    return;
                }
            }
        }

        private IEnumerator RunGallery()
        {
            var canvasHud = GameObject.Find("HudCanvas");
            if (canvasHud != null) canvasHud.SetActive(false);
            var canvasScreen = GameObject.Find("ScreenCanvas");
            if (canvasScreen != null) canvasScreen.SetActive(false);

            _main.Battle.gameObject.SetActive(true);
            _main.Battle.BuildGallery();
            yield return new WaitForSecondsRealtime(1.2f);
            yield return Shot("gallery");
            Application.Quit();
        }

        private static IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            // 输出到工程根的 Shots/（与 AGENTS.md 记录的路径一致）
            string dir = System.IO.Path.Combine(
                System.IO.Directory.GetParent(Application.dataPath).FullName, "..", "Shots");
            dir = System.IO.Path.GetFullPath(dir);
            System.IO.Directory.CreateDirectory(dir);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, name + ".png"));
            yield return null;
            yield return new WaitForSecondsRealtime(0.15f);
            Debug.Log("SHOT " + name);
        }

        // ---------------------------------------------------------------- 真实指针输入验证
        private IEnumerator VerifyInput()
        {
            int pass = 0;
            int fail = 0;

            // 1) 点击召唤按钮
            _main.Run.Gold += 80f;
            _main.Hud.UpdateHud(_main.Run, _main.Speed);
            int before = _main.Run.Towers.Count;
            yield return ClickUi(_main.Hud.SummonButton);
            yield return new WaitForSecondsRealtime(0.1f);
            if (_main.Run.Towers.Count == before + 1)
            {
                pass++;
                Debug.Log("PASS  input: 点击召唤按钮出塔");
            }
            else
            {
                fail++;
                Debug.Log($"FAIL  input: 点击召唤按钮出塔 (towers={_main.Run.Towers.Count})");
            }

            // 2) 点击地面移动英雄
            var heroBefore = _main.Run.Hero.Pos;
            yield return ClickDesign(_main.BoardToScreenDesign(new Vector2(540f, 1130f)));            _main.Run.Advance(1.6f);
            float moved = Vector2.Distance(heroBefore, _main.Run.Hero.Pos);
            if (moved > 30f)
            {
                pass++;
                Debug.Log($"PASS  input: 点击地面移动英雄 ({moved:F0} px)");
            }
            else
            {
                fail++;
                Debug.Log($"FAIL  input: 点击地面移动英雄 (moved={moved:F1})");
            }

            // 3) 拖拽同一种塔合成
            var pair = FindSamePair();
            int prep = 0;
            while (pair.Count < 2 && prep < 24)
            {
                prep++;
                _main.Run.Gold += 80f;
                _main.Hud.UpdateHud(_main.Run, _main.Speed);
                _main.Run.Summon();
                pair = FindSamePair();
            }

            // 3a) 点击塔 → 显示射程
            if (_main.Run.Towers.Count > 0)
            {
                var clickPlat = _main.Run.Platforms.Find(p => p.Tower != null && p.Tower.ClassId != "intercept");
                if (clickPlat != null)
                {
                    yield return ClickDesign(_main.BoardToScreenDesign(clickPlat.Pos));
                    yield return new WaitForSecondsRealtime(0.05f);
                    if (_main.Battle.RangeVisible)
                    {
                        pass++;
                        Debug.Log("PASS  input: 点击防御塔显示攻击范围");
                    }
                    else
                    {
                        fail++;
                        Debug.Log("FAIL  input: 点击防御塔应显示攻击范围");
                    }
                    yield return Shot("13-range-click");
                }
            }

            // 3b) 拖动塔 → 射程跟随指针
            bool merged = false;
            if (pair.Count == 2)
            {
                int towersBefore = _main.Run.Towers.Count;

                // 按下并移动到中点，此时应处于拖动状态且射程可见
                STInput.BeginTestPointer();
                STInput.SetTestPointer(_main.BoardToScreenDesign(pair[0].Pos), true, false);
                yield return null;
                var mid = Vector2.Lerp(pair[0].Pos, pair[1].Pos, 0.5f);
                STInput.SetTestPointer(_main.BoardToScreenDesign(mid), false, false);
                yield return null;

                bool dragRangeOk = _main.Battle.RangeVisible;
                if (dragRangeOk)
                {
                    pass++;
                    Debug.Log("PASS  input: 拖动防御塔时显示攻击范围");
                }
                else
                {
                    fail++;
                    Debug.Log("FAIL  input: 拖动防御塔时应显示攻击范围");
                }
                yield return Shot("14-range-drag");

                STInput.SetTestPointer(_main.BoardToScreenDesign(pair[1].Pos), false, false);
                yield return null;
                STInput.SetTestPointer(_main.BoardToScreenDesign(pair[1].Pos), false, true);
                yield return null;
                yield return null;
                STInput.EndTestPointer();

                if (_main.Run.Towers.Count == towersBefore - 1)
                {
                    merged = true;
                    pass++;
                    Debug.Log("PASS  input: 同一种塔拖拽合成升级");
                }
            }
            if (!merged)
            {
                fail++;
                Debug.Log("FAIL  input: 同种塔拖拽合成未发生");
            }

            // 4) 拖到不同等级的塔上应被拒绝
            PlatformState lv1 = null, lv2 = null;
            foreach (var p in _main.Run.Platforms)
            {
                if (p.Tower == null) continue;
                if (p.Tower.Level == 1 && lv1 == null) lv1 = p;
                else if (p.Tower.Level >= 2 && lv2 == null) lv2 = p;
            }

            bool rejectOk = true;
            if (lv1 != null && lv2 != null)
            {
                int countBefore = _main.Run.Towers.Count;
                yield return DragDesign(_main.BoardToScreenDesign(lv1.Pos), _main.BoardToScreenDesign(lv2.Pos));
                rejectOk = _main.Run.Towers.Count == countBefore && lv1.Tower != null;
            }
            if (rejectOk)
            {
                pass++;
                Debug.Log("PASS  input: 等级不同拒绝合成");
            }
            else
            {
                fail++;
                Debug.Log("FAIL  input: 等级不同应该拒绝合成");
            }

            Debug.Log($"INPUT RESULT {pass}/{pass + fail}");

            // 5) 塔模型姿态自检（用户会反复从 Blender 重导模型，这里把结论固化）
            var probes = _main.Battle.SpawnTowerProbes();
            yield return null;
            yield return null;   // 等 STGroundSnap 落地收敛
            yield return new WaitForSecondsRealtime(0.1f);

            int modelFail = 0;
            foreach (var kv in probes)
            {
                var line = _main.Battle.CheckTowerNode(kv.Key, kv.Value);
                Debug.Log(line);
                if (line.StartsWith("FAIL")) modelFail++;
            }
            foreach (var kv in probes) Destroy(kv.Value);
            Debug.Log($"MODEL RESULT {probes.Count - modelFail}/{probes.Count}");

            // 6) 朝向自检：单位机头必须指向移动方向（用户反复反馈「敌人倒着走」）
            var facing = _main.Battle.SpawnFacingProbes();
            yield return null;
            yield return null;
            int faceChecked = 0, faceFail = 0;
            foreach (var kv in facing)
            {
                var line = _main.Battle.CheckUnitFacing(kv.Key, kv.Value);
                Debug.Log(line);
                if (line.StartsWith("SKIP")) continue;
                faceChecked++;
                if (line.StartsWith("FAIL")) faceFail++;
            }
            foreach (var kv in facing) Destroy(kv.Value);
            Debug.Log($"FACING RESULT {faceChecked - faceFail}/{faceChecked}");

            // 7) 通关存档：长度跟随关卡数，且最后一关能真的存下来
            //    （加第 4 关时存档仍写死 3 位，第 4 关通关状态会被丢弃）
            CheckSaveCoverage();

            // 8) 金币飘字必须真的能画出来
            //    （贴图烘焙曾因 cam.cullingMask=0 一直全透明，界面上毫无察觉）
            CheckFloatText();
        }

        private void CheckFloatText()
        {
            int fail = 0;

            // 贴图烘焙：必须有非透明像素，否则等于完全看不见
            var tex = STTextTexture.Get("+88", 40, STColors.Gold);
            if (tex == null)
            {
                fail++;
                Debug.Log("FAIL  fx: 金币飘字贴图烘焙失败（null）");
            }
            else
            {
                var px = tex.GetPixels32();
                int opaque = 0;
                for (int i = 0; i < px.Length; i++) if (px[i].a > 40) opaque++;
                if (opaque < 20)
                {
                    fail++;
                    Debug.Log($"FAIL  fx: 金币飘字贴图为空白（非透明像素 {opaque}）");
                }
                else
                {
                    Debug.Log($"PASS  fx: 金币飘字贴图有内容（{opaque} 像素，{tex.width}x{tex.height}）");
                }
            }

            // 世界尺寸：必须足够大，否则在俯视相机下缩成一个点
            var fx = _main.Battle != null ? _main.Battle.Fx : null;
            if (fx == null)
            {
                fail++;
                Debug.Log("FAIL  fx: 找不到 STFxSystem");
            }
            else
            {
                fx.FloatCoin(new Vector3(0f, 4f, 0f), 88, STColors.Gold, 40, 5f);
                var found = GameObject.Find("FxText");
                // 飘字现在挂在 Overlay Canvas 上（UI），量 RectTransform 的设计像素尺寸
                var rt = found != null ? found.GetComponent<RectTransform>() : null;
                if (rt == null)
                {
                    fail++;
                    Debug.Log("FAIL  fx: 找不到飘字节点或其 RectTransform");
                }
                else
                {
                    var size = rt.sizeDelta;
                    bool onTextCanvas = rt.parent != null && rt.parent.name == "FxTextCanvas";
                    // 30% 缩放后约 25–50 设计像素宽；低于 12 就是字看不清了
                    if (size.x < 12f || !onTextCanvas)
                    {
                        fail++;
                        Debug.Log($"FAIL  fx: 飘字尺寸异常或不在 UI 层（{size.x:F0}x{size.y:F0}, " +
                                  $"canvas={rt.parent?.name}）");
                    }
                    else
                    {
                        Debug.Log($"PASS  fx: 飘字 {size.x:F0}x{size.y:F0} 设计像素，绘制于 " +
                                  $"{rt.parent.name}（永远在 3D 之上）");
                    }
                }
            }

            Debug.Log($"FX RESULT {2 - fail}/2");
        }

        private void CheckSaveCoverage()
        {
            int n = STData.Levels.Count;
            int fail = 0;

            if (_main.Cleared.Count != n)
            {
                fail++;
                Debug.Log($"FAIL  save: Cleared 长度 {_main.Cleared.Count} != 关卡数 {n}");
            }
            else
            {
                Debug.Log($"PASS  save: Cleared 长度 == 关卡数 ({n})");
            }

            // 最后一关通关 -> 落盘 -> 读回
            string backup = PlayerPrefs.GetString("starlane.cleared", "");
            int last = n - 1;
            while (_main.Cleared.Count <= last) _main.Cleared.Add(false);
            _main.Cleared[last] = true;

            var save = typeof(STMain).GetMethod("Save",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var load = typeof(STMain).GetMethod("LoadSave",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            save.Invoke(_main, null);
            string raw = PlayerPrefs.GetString("starlane.cleared", "");
            load.Invoke(_main, null);

            if (raw.Length == n && raw[last] == '1'
                && _main.Cleared.Count == n && _main.Cleared[last])
            {
                Debug.Log($"PASS  save: 最后一关通关可落盘并读回（raw='{raw}'）");
            }
            else
            {
                fail++;
                Debug.Log($"FAIL  save: 最后一关通关未保存（raw='{raw}'）");
            }

            // 还原玩家真实存档
            if (string.IsNullOrEmpty(backup)) PlayerPrefs.DeleteKey("starlane.cleared");
            else PlayerPrefs.SetString("starlane.cleared", backup);
            PlayerPrefs.Save();
            load.Invoke(_main, null);

            Debug.Log($"SAVE RESULT {2 - fail}/2");
        }

        private List<PlatformState> FindSamePair()
        {
            var result = new List<PlatformState>();
            var byOp = new Dictionary<string, PlatformState>();
            foreach (var p in _main.Run.Platforms)
            {
                if (p.Tower == null) continue;
                if (p.Tower.Level >= 4) continue;
                var key = p.Tower.OpId + "_" + p.Tower.Level;
                if (byOp.TryGetValue(key, out var other))
                {
                    result.Add(other);
                    result.Add(p);
                    return result;
                }
                byOp[key] = p;
            }
            return result;
        }

        private IEnumerator ClickUi(Selectable target)
        {
            var rt = target.transform as RectTransform;
            var canvas = rt.GetComponentInParent<Canvas>();
            var cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            var world = rt.TransformPoint(rt.rect.center);
            var screen = RectTransformUtility.WorldToScreenPoint(cam, world);
            var design = STInput.ScreenToDesign(screen);

            yield return ClickDesign(design);
        }

        /// <summary>真实拖拽：按下起点 → 多次移动 → 在终点抬起（走真实输入采样路径）。</summary>
        private IEnumerator DragDesign(Vector2 from, Vector2 to)
        {
            STInput.BeginTestPointer();
            try
            {
                STInput.SetTestPointer(from, true, false);
                yield return null;
                yield return null;

                for (int i = 1; i <= 4; i++)
                {
                    var mid = Vector2.Lerp(from, to, i / 4f);
                    STInput.SetTestPointer(mid, false, false);
                    yield return null;
                }

                STInput.SetTestPointer(to, false, true);
                yield return null;
                yield return null;
            }
            finally
            {
                STInput.EndTestPointer();
            }
        }

        /// <summary>
        /// 一次真实点击：先按 UGUI 射线命中判定（命中 UI 就走 EventSystem 的
        /// pointerDown/Up/Click），否则按战场点击注入指针，走 STInput.Sample 的同一条路径。
        /// </summary>
        private IEnumerator ClickDesign(Vector2 design)
        {
            var screen = STInput.DesignToScreen(design);
            var data = new PointerEventData(EventSystem.current)
            {
                position = screen,
                button = PointerEventData.InputButton.Left,
            };

            var hits = new List<RaycastResult>();
            if (EventSystem.current != null) EventSystem.current.RaycastAll(data, hits);

            if (hits.Count > 0)
            {
                var handler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
                if (handler != null)
                {
                    ExecuteEvents.Execute(handler, data, ExecuteEvents.pointerDownHandler);
                    yield return null;
                    ExecuteEvents.Execute(handler, data, ExecuteEvents.pointerUpHandler);
                    ExecuteEvents.Execute(handler, data, ExecuteEvents.pointerClickHandler);
                    yield return null;
                    yield break;
                }
            }

            STInput.BeginTestPointer();
            try
            {
                STInput.SetTestPointer(design, true, false);
                yield return null;
                STInput.SetTestPointer(design, false, true);
                yield return null;
                yield return null;
            }
            finally
            {
                STInput.EndTestPointer();
            }
        }
    }
}
