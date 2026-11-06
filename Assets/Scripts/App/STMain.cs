// 星轨防线 · 主控制器（Godot scripts/main.gd 的移植）
//
// 职责：
//   · 屏幕流程（菜单 / 编队 / 关卡 / 战斗）
//   · 输入（拖动合成、点地移动英雄、快捷键）
//   · 固定步长循环（1/60 秒，倍速时一帧多步）
//   · 事件 → 表现（音效 / 特效 / 横幅 / toast）
//   · 存档（PlayerPrefs）、截图自检、机器人试玩
//
// 唯一允许修改 app/run 状态的地方。

using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace StarLane
{
    public enum STMode { Menu, Formation, Levels, Battle }

    public class STMain : MonoBehaviour
    {
        public const float Step = 1f / 60f;
        private const string KeyFormation = "starlane.formation";
        private const string KeyCleared = "starlane.cleared";

        public STMode Mode = STMode.Menu;
        public RunState Run;
        public int LevelIndex;
        public int Speed = 1;
        public bool Paused;
        public bool Finished;
        public float Elapsed;

        // 输入状态
        private string _dragTowerId = "";
        private string _dragFrom = "";
        private string _dragTarget = "";
        private Vector2 _dragPos;
        private Vector2 _pressPos;
        private bool _pressing;
        private string _selectedPlatform = "";
        private float _selectedAt = -10f;

        // 节点
        private Transform _uiRoot;
        private Transform _hudLayer;
        private Transform _screensLayer;
        private Transform _modalLayer;
        private Transform _bannerLayer;
        private Transform _toastLayer;

        public HudView Hud;
        public BannerView Banner;
        public ToastView Toast;
        public STBattleView Battle;
        private SfxBank _sfx;

        private GameObject _screen;      // 当前全屏界面（菜单 / 编队 / 关卡）
        private GameObject _modal;       // 当前弹窗

        // 资源
        private Texture2D _deck;
        private readonly Dictionary<string, Sprite> _portraits = new Dictionary<string, Sprite>();

        // 存档
        public List<string> Formation = new List<string>();
        public List<bool> Cleared = new List<bool>();

        private float _acc;
        private float _lastHud;
        private bool _battleActive;

        // 自检
        private bool _hasModal => _modal != null;

        /// <summary>是否存在打开的弹窗（自检脚本用）。</summary>
        public bool HasModal => _hasModal;

        /// <summary>是否有战斗正在进行（供调试与自检读取）。</summary>
        public bool BattleActive => _battleActive;

        // ---------------------------------------------------------------- 启动
        private void Awake()
        {
            Application.targetFrameRate = 60;
            Screen.orientation = ScreenOrientation.Portrait;
#if !UNITY_EDITOR
            // 若命令行已指定窗口尺寸（-screen-width / -screen-height），尊重命令行，
            // 否则退到设计尺寸的一半（540×960）作为默认窗口大小。
            if (!HasCommandLineScreenSize()) Screen.SetResolution(540, 960, false);
#endif
            LoadSave();
            LoadTextures();

            BuildRig();
            _sfx = gameObject.AddComponent<SfxBank>();
            gameObject.AddComponent<STAutoShot>();
            gameObject.AddComponent<STLevelShot>();
            ShowMenu();
        }

        // ---------------------------------------------------------------- 自检入口
        // 注意：这些入口接收的已经是「战场像素（设计坐标）」，与 OnPress/OnMove/OnRelease
        // 的参数同域，因此不再经过 ScreenToBoard 换算（真实指针才需要换算）。
        public void OnPressForTest(Vector2 design) => OnPress(design);

        public void OnMoveForTest(Vector2 design) => OnMove(design);

        public void OnReleaseForTest(Vector2 design) => OnRelease(design);

        private void LoadTextures()
        {
            _deck = STAssets.MapTexture("map")?.texture;
            foreach (var op in STData.Operatives)
            {
                var sprite = STAssets.Background(op.portrait);
                if (sprite != null) _portraits[op.portrait] = sprite;
            }
        }

        private void BuildRig()
        {
            _uiRoot = transform;

            var worldGo = new GameObject("BattleView");
            worldGo.transform.SetParent(transform, false);
            Battle = worldGo.AddComponent<STBattleView>();

            _hudLayer = STUi.CreateCanvas(transform, "HudCanvas", 10).transform;
            _screensLayer = STUi.CreateCanvas(transform, "ScreenCanvas", 20).transform;
            _modalLayer = STUi.CreateCanvas(transform, "ModalCanvas", 30).transform;
            _bannerLayer = STUi.CreateCanvas(transform, "BannerCanvas", 40).transform;
            _toastLayer = STUi.CreateCanvas(transform, "ToastCanvas", 50).transform;

            EnsureEventSystem();

            Hud = new HudView();
            Hud.Build(_hudLayer, _portraits);
            Hud.PauseButton.onClick.AddListener(OnPauseClicked);
            Hud.SpeedButton.onClick.AddListener(CycleSpeed);
            Hud.HelpButton.onClick.AddListener(ShowGuide);
            Hud.SummonButton.onClick.AddListener(OnSummon);
            Hud.UpgradeButton.onClick.AddListener(OnUpgrade);

            Banner = new BannerView();
            Banner.Build(_bannerLayer);

            Toast = new ToastView();
            Toast.Build(_toastLayer);
        }

        private static bool HasCommandLineScreenSize()
        {
            return HasArg("-screen-width") || HasArg("-screen-height");
        }

        /// <summary>命令行是否带了某个开关（例如 -arrows 画敌人行进方向箭头）。</summary>
        private static bool HasArg(string name)
        {
            foreach (var arg in System.Environment.GetCommandLineArgs())
            {
                if (arg == name) return true;
            }
            return false;
        }

        private static void EnsureEventSystem()        {
            if (FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() != null) return;
            var go = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.EventSystems.StandaloneInputModule));
            DontDestroyOnLoad(go);
        }

        // ---------------------------------------------------------------- 存档
        private void LoadSave()
        {
            var csv = PlayerPrefs.GetString(KeyFormation, STData.DefaultFormationCsv);
            Formation = STData.NormalizeFormation(csv.Split(','));

            // 长度跟随实际关卡数，不能写死：加第 4 关时写死 3 位会让新关卡的通关状态存不下来。
            // 旧存档是 "000" / "101" 这种短串，按位读、缺位补 false，天然向后兼容。
            int count = STData.Levels.Count;
            Cleared = new List<bool>(count);
            var bits = PlayerPrefs.GetString(KeyCleared, "");
            for (int i = 0; i < count; i++)
            {
                Cleared.Add(i < bits.Length && bits[i] == '1');
            }
        }

        private void Save()
        {
            PlayerPrefs.SetString(KeyFormation, string.Join(",", Formation));
            var bits = "";
            foreach (var c in Cleared) bits += c ? "1" : "0";
            PlayerPrefs.SetString(KeyCleared, bits);
            PlayerPrefs.Save();
        }

        // ---------------------------------------------------------------- 屏幕
        private void ClearScreen()
        {
            if (_screen != null) Destroy(_screen);
            _screen = null;
        }

        private void CloseModal()
        {
            if (_modal != null) Destroy(_modal);
            _modal = null;
        }

        private void OpenModal(GameObject go)
        {
            CloseModal();
            _modal = go;
        }

        public void ShowMenu()
        {
            Mode = STMode.Menu;
            Run = null;
            Paused = false;
            Finished = false;
            _battleActive = false;
            Hud.SetVisible(false);
            Battle.ClearUnits();
            Battle.gameObject.SetActive(false);
            CloseModal();
            ClearScreen();

            int clearedCount = 0;
            foreach (var c in Cleared) if (c) clearedCount++;

            _screen = STScreens.BuildMenu(_screensLayer, clearedCount,
                () => { _sfx.Play("click"); ShowLevels(); },
                () => { _sfx.Play("click"); ShowFormation(STMode.Menu); },
                () => { _sfx.Play("click"); ShowGuide(); });
        }

        public void ShowFormation(STMode returnMode)
        {
            Mode = STMode.Formation;
            Hud.SetVisible(false);
            Battle.gameObject.SetActive(false);
            ClearScreen();

            _screen = STScreens.BuildFormation(_screensLayer, Formation, _portraits,
                id => ToggleOperative(id, returnMode),
                () => { _sfx.Play("click"); if (returnMode == STMode.Menu) ShowMenu(); else ShowLevels(); },
                () =>
                {
                    Save();
                    Toast.Show("编队已更新");
                    _sfx.Play("click");
                    if (returnMode == STMode.Menu) ShowMenu(); else ShowLevels();
                });
        }

        private void ToggleOperative(string id, STMode returnMode)
        {
            _sfx.Play("click");
            if (Formation.Contains(id))
            {
                if (Formation.Count <= 1)
                {
                    Toast.Show("至少保留 1 名干员", "warn");
                    return;
                }
                Formation.Remove(id);
            }
            else
            {
                if (Formation.Count >= 4)
                {
                    Toast.Show("最多上阵 4 名干员", "warn");
                    return;
                }
                Formation.Add(id);
            }
            Formation = STData.NormalizeFormation(Formation);
            ShowFormation(returnMode);
        }

        public void ShowLevels()
        {
            Mode = STMode.Levels;
            Hud.SetVisible(false);
            Battle.gameObject.SetActive(false);
            ClearScreen();

            _screen = STScreens.BuildLevels(_screensLayer, Cleared, Formation, _portraits,
                index => StartLevel(index),
                () => { _sfx.Play("click"); ShowMenu(); },
                () => { _sfx.Play("click"); ShowFormation(STMode.Levels); });
        }

        public void ShowGuide()
        {
            CloseModal();
            _modal = STScreens.BuildGuide(_modalLayer, () => { _sfx.Play("click"); CloseModal(); });
        }

        // ---------------------------------------------------------------- 战斗
        public void StartLevel(int index)
        {
            _sfx.Play("click");
            LevelIndex = index;
            Run = new RunState(index, Formation);
            Mode = STMode.Battle;
            Speed = 1;
            Paused = false;
            Finished = false;
            _acc = 0f;
            _dragTowerId = "";
            _dragTarget = "";
            _selectedPlatform = "";
            _battleActive = true;

            CloseModal();
            ClearScreen();
            _screensLayer.gameObject.SetActive(false);
            Battle.gameObject.SetActive(true);
            Hud.SetVisible(true);

            Battle.Setup(Run, HasArg("-arrows"));
            Hud.RefreshSquad(Run, _portraits);
            Hud.RefreshAffixes(Run);
            Hud.UpdateHud(Run, Speed);

            Banner.Show($"SECTOR 0{index + 1}", Run.Level.name, Run.Level.hint, "",
                STAssets.ThreatSprite("elite"));
        }

        public void OnSummon()
        {
            if (Mode != STMode.Battle || Run == null || Finished || _hasModal) return;

            var res = Run.Summon();
            if (!res.Ok)
            {
                if (res.Reason == "gold") Toast.Show($"金币不足，需要 {res.Cost}", "warn");
                else if (res.Reason == "no-platform") Toast.Show("塔台已满：合成或腾出空台再召唤", "warn");
                return;
            }

            _sfx.Play("summon");
            Toast.Show($"召唤：{STData.GetOperative(res.Tower.OpId).name} · {STData.GetClass(res.Tower.ClassId).name}");
            _selectedPlatform = res.Platform.Id;
            _selectedAt = Elapsed;
            Battle.SyncUnits();
            Hud.UpdateHud(Run, Speed);
        }

        public void OnUpgrade()
        {
            if (Mode != STMode.Battle || Run == null || Finished || _hasModal) return;

            var res = Run.PurchaseUpgrade();
            if (!res.Ok)
            {
                if (res.Reason == "gold") Toast.Show($"金币不足，需要 {res.Cost}", "warn");
                return;
            }

            _sfx.Play("click");
            OpenAffixModal(res.Choices);
        }

        private void OpenAffixModal(List<string> choices)
        {
            Paused = true;
            CloseModal();

            var modal = STScreens.BuildAffixModal(_modalLayer, choices, (int)Run.Gold,
                PickAffix,
                () =>
                {
                    CloseModal();
                    Paused = false;
                    Toast.Show("词条稍后可在「升级」中继续选择");
                });
            OpenModal(modal);
        }

        private void PickAffix(string id)
        {
            var res = Run.PickAffix(id);
            if (res.Ok)
            {
                _sfx.Play("upgrade");
                var a = STData.GetAffix(id);
                Toast.Show("词条生效：" + (a != null ? a.name : ""));
                Hud.RefreshAffixes(Run);
            }
            CloseModal();
            Paused = false;
            Hud.UpdateHud(Run, Speed);
        }

        public void OnPauseClicked()
        {
            if (Mode != STMode.Battle || Finished || _hasModal) return;
            Paused = true;

            var line = $"波次 {Run.WaveIndex}/10 · 核心 {(int)Run.CoreHp}/{(int)Run.CoreHpMax} · " +
                       $"召唤 {Run.Summons} · 词条 {Run.Upgrades}";

            OpenModal(STScreens.BuildPause(_modalLayer, line,
                () => { _sfx.Play("click"); CloseModal(); Paused = false; },
                () => { _sfx.Play("click"); CloseModal(); StartLevel(LevelIndex); },
                () => { _sfx.Play("click"); CloseModal(); ShowLevels(); }));
        }

        public void CycleSpeed()
        {
            Speed = Speed == 3 ? 1 : Speed + 1;
            Toast.Show($"战斗速度 {Speed}×");
            if (Run != null) Hud.UpdateHud(Run, Speed);
        }

        private void FinishRun()
        {
            if (Finished) return;
            Finished = true;
            Paused = true;

            if (Run.Status == "victory")
            {
                // 兜底：Cleared 由关卡数生成，正常不会短；若真短了就补齐再写，
                // 而不是静默丢弃——否则通关了却不记录，玩家会以为进度没保存。
                while (Cleared.Count <= LevelIndex) Cleared.Add(false);
                Cleared[LevelIndex] = true;
                Save();
                _sfx.Play("win");
            }
            else
            {
                _sfx.Play("lose");
            }

            StartCoroutine(ShowResultAfter(0.7f));
        }

        private IEnumerator ShowResultAfter(float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            CloseModal();
            OpenModal(STScreens.BuildResult(_modalLayer, Run,
                () => { _sfx.Play("click"); CloseModal(); StartLevel(LevelIndex); },
                () => { _sfx.Play("click"); CloseModal(); _screensLayer.gameObject.SetActive(true); ShowLevels(); }));
        }

        // ---------------------------------------------------------------- 事件 → 表现
        /// <summary>事件 → 表现（对应 Godot main.gd 的 _handle_events）。</summary>
        private void HandleEvents(List<STEvent> events)
        {
            if (events == null) return;
            var fx = Battle?.Fx;

            foreach (var ev in events)
            {
                switch (ev.Kind)
                {
                    case STEventKind.Hit:
                        {
                            bool crit = ev.Flag;
                            fx?.Spark(STWorld.ToWorld(ev.Pos, 0.9f),
                                crit ? STColors.Gold : STColors.White, crit ? 1.1f : 0.7f, 0.28f);
                            if (crit)
                            {
                                fx?.FloatText(STWorld.ToWorld(ev.Pos + new Vector2(0f, -12f), 1.4f),
                                    $"{Mathf.RoundToInt(ev.Amount)}!", STColors.Gold, 30, 0.7f);
                            }
                            break;
                        }

                    case STEventKind.Explosion:
                        _sfx.Play("explode");
                        fx?.Explosion(STWorld.ToWorld(ev.Pos, 0.5f), ev.Color, ev.Radius / 40f, 0.45f);
                        Battle?.SpawnBlob(STWorld.ToWorld(ev.Pos, 0.5f), ev.Color, ev.Radius / 40f, 0.45f);
                        break;

                    case STEventKind.Kill:
                        if (ev.Enemy != null)
                        {
                            fx?.Spark(STWorld.ToWorld(ev.Pos, 0.8f), ev.Enemy.Color, 1.0f, 0.35f);

                            // 金币必须每次击杀都看得见：以前只有精英/BOSS 飘字，
                            // 小怪击杀毫无反馈，玩家感知不到钱在涨。
                            // 三档字号区分重要性，小怪压低、缩短漂浮距离，避免刷屏。
                            bool big = ev.Enemy.Boss || ev.Enemy.Elite;
                            int coinSize = ev.Enemy.Boss ? 40 : (ev.Enemy.Elite ? 32 : 24);
                            float life = big ? 1.05f : 0.75f;
                            // 抬高到血条之上：血条实际高度 = BarY(1.5~1.8) × UnitScale(1.55) ≈ 2.3~2.8。
                            // 飘字从 4.2 起（小怪）/ 5.0（精英 BOSS），确保首帧就在血条上方，
                            // 配合 RisePx 继续上浮，全程不会被血条底板压住。
                            var at = STWorld.ToWorld(ev.Pos + new Vector2(0f, -24f),
                                big ? 5.0f : 4.2f);
                            // 金币图标 + 数字，比纯文本更容易一眼认出是进账
                            fx?.FloatCoin(at, ev.IntA,
                                big ? STColors.Gold : STColors.Hex("FFD86B"), coinSize, life);

                            if (big)
                            {
                                fx?.Ring(STWorld.ToWorld(ev.Pos, 0.4f), ev.Enemy.Color,
                                    ev.Enemy.Boss ? 260f / STWorld.PX : 120f / STWorld.PX, 0.7f);
                                Hud.FlashGold(ev.IntA);
                            }
                        }
                        break;

                    case STEventKind.Leak:
                        _sfx.Play("leak");
                        fx?.Ring(STWorld.ToWorld(ev.Pos, 0.4f), STColors.Red, 130f / STWorld.PX, 0.6f);
                        Battle?.SpawnBlob(STWorld.ToWorld(ev.Pos, 0.8f), STColors.Red, 3f, 0.6f);
                        Toast.Show($"核心受损 -{(int)ev.Amount}", "bad");
                        break;

                    case STEventKind.Unlock:
                        _sfx.Play("unlock");
                        if (ev.Platform != null)
                        {
                            fx?.Ring(STWorld.ToWorld(ev.Platform.Pos, 0.3f), STColors.Cyan, 90f / STWorld.PX, 0.9f);
                        }
                        Toast.Show($"新塔台解锁（共 {ev.IntA} 台）");
                        break;

                    case STEventKind.Merge:
                        _sfx.Play("merge");
                        if (ev.ToPlatform != null)
                        {
                            var at = STWorld.ToWorld(ev.ToPlatform.Pos, 0.3f);
                            fx?.Ring(at, STColors.Green, 90f / STWorld.PX, 0.8f);
                            fx?.Spark(at + Vector3.up * 0.5f, STColors.Green, 1.6f, 0.5f);
                            Battle?.SpawnBlob(at, STColors.Green, 1.6f, 0.5f);
                        }
                        Toast.Show($"合成成功：Lv.{ev.IntA} {STData.GetClass(ev.Tower.ClassId).name}");
                        break;

                    case STEventKind.Summon:
                        if (ev.Platform != null && ev.Tower != null)
                        {
                            var at = STWorld.ToWorld(ev.Platform.Pos, 0.2f);
                            fx?.Ring(at, STData.ClassColor(ev.Tower.ClassId), 60f / STWorld.PX, 0.6f);
                            fx?.FloatText(at + new Vector3(0f, 2.2f, 0f),
                                "-" + (int)ev.Amount, STColors.Gold, 24, 0.9f);
                        }
                        break;

                    case STEventKind.HeroShot:
                        Battle?.TriggerHeroShot();
                        break;

                    case STEventKind.Wave:
                        if (ev.Text == "boss")
                        {
                            Banner.Show("BOSS APPROACHING", "旗舰 · 湮灭级", "第 10 波 · 最终目标进入轨道",
                                "boss", STAssets.ThreatSprite("boss"));
                            _sfx.Play("boss");
                        }
                        else if (ev.Text == "elite")
                        {
                            Banner.Show("ELITE INBOUND", "精英 · 破阵者", $"第 {ev.IntA} 波 · 高护甲单位",
                                "elite", STAssets.ThreatSprite("elite"));
                            _sfx.Play("boss");
                        }
                        else
                        {
                            Banner.Show($"WAVE {ev.IntA:00}", $"第 {ev.IntA} 波 / 10", "", "", null);
                            _sfx.Play("wave");
                        }
                        break;

                    case STEventKind.WaveClear:
                        {
                            // 波次奖励是整局最大的一次进账，必须比单杀显眼：
                            // 位置飘字 + 金色扩散环 + 顶部金币条闪一下，三重反馈。
                            int bonus = (int)ev.Amount;
                            var at = STWorld.ToWorld(new Vector2(540f, 720f), 1.6f);
                            fx?.FloatText(at + new Vector3(0f, 1.6f, 0f), "波次清空",
                                STColors.Hex("CFE9FF"), 30, 1.5f);
                            fx?.FloatCoin(at, bonus, STColors.Gold, 44, 1.5f);
                            fx?.Ring(STWorld.ToWorld(new Vector2(540f, 720f), 0.6f),
                                STColors.Gold, 210f / STWorld.PX, 0.85f);
                            fx?.Ring(STWorld.ToWorld(new Vector2(540f, 720f), 0.6f),
                                STColors.Hex("FFD86B"), 130f / STWorld.PX, 0.6f);
                            Hud.FlashGold(bonus);
                        }
                        break;

                    case STEventKind.GuardDown:
                        if (ev.Guard != null)
                        {
                            fx?.Ring(STWorld.ToWorld(ev.Guard.Pos, 0.3f), STColors.Green, 50f / STWorld.PX, 0.4f);
                        }
                        break;

                    case STEventKind.Shot:
                        _sfx.Play("shoot_" + ev.ClassId);
                        if (ev.Tower != null && ev.Enemy != null)
                        {
                            Battle.TriggerTowerAttack(ev.Tower.Id);
                            var from = Battle.MuzzleWorld(ev.Tower);
                            var to = STWorld.ToWorld(ev.Enemy.Pos, 0.9f);
                            switch (ev.ClassId)
                            {
                                case "sniper": Battle.SpawnBullet(from, to, STColors.Violet, "beam"); break;
                                case "siege": Battle.SpawnBullet(from, to, STColors.Orange, "shell"); break;
                                default: Battle.SpawnBullet(from, to, STColors.Cyan, "bullet"); break;
                            }
                        }
                        break;
                }
            }

            if (Run != null && Run.Status != "running") FinishRun();
        }

        // ---------------------------------------------------------------- 主循环
        private void Update()
        {
            float delta = Time.deltaTime;
            Elapsed += delta;
            STInput.Sample();

            Banner.Tick(delta);
            Toast.Tick(delta);
            Hud?.Tick(delta);

            if (Mode == STMode.Battle && Run != null)
            {
                HandleHotkeys();
                HandlePointer();

                if (!Paused && !Finished && !_hasModal)
                {
                    _acc += delta * Speed;
                    int iterations = 0;
                    while (_acc >= Step && iterations < 480)
                    {
                        iterations++;
                        var events = Run.Step(Step);
                        if (events.Count > 0) HandleEvents(events);
                        _acc -= Step;
                        if (Run.Status != "running") break;
                    }
                }
                else
                {
                    _acc = 0f;
                }

                // 战斗结束判定独立于事件处理：机器人 / 快进路径可能丢弃事件，
                // 结算不能依赖某条分支是否调用了 HandleEvents。
                if (!Finished && Run.Status != "running") FinishRun();

                Battle.SyncUnits();
                // 选中后射程常驻显示（直到点空白取消），拖动时跟随指针。
                // 旧实现只显示 2.6 秒，玩家几乎来不及看到。
                bool showRange = !string.IsNullOrEmpty(_selectedPlatform) || _dragTowerId != "";
                Battle.UpdateView(showRange ? _selectedPlatform : "", _dragTowerId, _dragPos, _dragTarget,
                    Elapsed, _selectedAt);

                if (Elapsed - _lastHud > 0.12f)
                {
                    _lastHud = Elapsed;
                    Hud.UpdateHud(Run, Speed);
                }

                if (Hud.ArrowIcon != null)
                {
                    var p = Hud.ArrowIcon.anchoredPosition;
                    p.y = -(1612f + Mathf.Sin(Elapsed * 5f) * 10f);
                    Hud.ArrowIcon.anchoredPosition = p;
                }
            }
        }

        private void HandleHotkeys()
        {
            switch (STInput.ReadHotkeys())
            {
                case "pause":
                    // 与 Godot 一致：有弹窗时快捷键不响应
                    if (!_hasModal) OnPauseClicked();
                    break;
                case "summon": OnSummon(); break;
                case "upgrade": OnUpgrade(); break;
                case "mute":
                    _sfx.Muted = !_sfx.Muted;
                    Toast.Show(_sfx.Muted ? "已静音" : "已恢复音效");
                    break;
                case "speed1": Speed = 1; Hud.UpdateHud(Run, Speed); break;
                case "speed2": Speed = 2; Hud.UpdateHud(Run, Speed); break;
                case "speed3": Speed = 3; Hud.UpdateHud(Run, Speed); break;
            }
        }

        // ---------------------------------------------------------------- 指针
        /// <summary>设计坐标 → 战场像素（3D 相机射线打回 y=0 地面）。</summary>
        private Vector2 ScreenToBoard(Vector2 design)
        {
            var screen = STInput.DesignToScreen(design);
            var cam = Battle.Camera;
            if (cam == null) return design;

            float distance;
            var ray = cam.ScreenPointToRay(new Vector3(screen.x, screen.y, 0f));
            var plane = new Plane(Vector3.up, Vector3.zero);
            if (!plane.Raycast(ray, out distance)) return design;
            var world = ray.GetPoint(distance);
            return STWorld.ToDesign(world);
        }

        /// <summary>战场像素 → 屏幕设计坐标（用于自检脚本按逻辑位置模拟点击）。</summary>
        public Vector2 BoardToScreenDesign(Vector2 board)
        {
            var cam = Battle.Camera;
            if (cam == null) return board;
            var world = STWorld.ToWorld(board, 0f);
            var vp = cam.WorldToViewportPoint(world);
            return new Vector2(vp.x * STInput.Width, (1f - vp.y) * STInput.Height);
        }

        private void HandlePointer()
        {
            var design = STInput.PointerDesign;
            var board = ScreenToBoard(design);

            if (STInput.LeftPressed)
            {
                if (STInput.OverUIForTouch())
                {
                    _pressing = false;
                    return;
                }
                OnPress(board);
            }
            else if (STInput.LeftHeld && _pressing)
            {
                OnMove(board);
            }
            else if (STInput.LeftReleased && _pressing)
            {
                OnRelease(board);
            }
        }

        private bool InBoard(Vector2 p)
        {
            var tuning = STData.T;
            return p.y >= tuning.boardTop && p.y <= tuning.boardTop + (tuning.boardBottom - tuning.boardTop);
        }

        private PlatformState TowerAt(Vector2 p)
        {
            PlatformState best = null;
            float bestD = 70f;
            foreach (var plat in Run.Platforms)
            {
                if (plat.Tower == null) continue;
                float d = Vector2.Distance(plat.Pos, p);
                if (d < bestD) { bestD = d; best = plat; }
            }
            return best;
        }

        private PlatformState PlatformNear(Vector2 p, float radius)
        {
            PlatformState best = null;
            float bestD = radius;
            foreach (var plat in Run.Platforms)
            {
                float d = Vector2.Distance(plat.Pos, p);
                if (d < bestD) { bestD = d; best = plat; }
            }
            return best;
        }

        private void OnPress(Vector2 p)
        {
            _pressPos = p;
            _pressing = true;
            if (!InBoard(p)) return;

            var plat = TowerAt(p);
            if (plat != null)
            {
                _dragTowerId = plat.Tower.Id;
                _dragFrom = plat.Id;
                _dragPos = p;
                _dragTarget = "";
                _selectedPlatform = plat.Id;
                _selectedAt = Elapsed;
            }
            else
            {
                // 点空白处取消选中 → 射程指示随之隐藏
                _selectedPlatform = "";
            }
        }

        private void OnMove(Vector2 p)
        {
            if (!_pressing) return;
            if (!_dragTowerId.Equals(""))
            {
                _dragPos = p;
                var plat = PlatformNear(p, 100f);
                _dragTarget = (plat != null && plat.Id != _dragFrom) ? plat.Id : "";
            }
        }

        private void OnRelease(Vector2 p)
        {
            _pressing = false;

            if (string.IsNullOrEmpty(_dragTowerId))
            {
                if (Vector2.Distance(p, _pressPos) < 14f && InBoard(p))
                {
                    Run.MoveHero(p);
                }
                return;
            }

            // 落点重新判定一次，避免最后一次移动与抬起位置差一个采样点
            if (InBoard(p))
            {
                var near = PlatformNear(p, 100f);
                if (near != null && near.Id != _dragFrom) _dragTarget = near.Id;
            }

            var from = _dragFrom;
            var to = _dragTarget;
            _dragTowerId = "";
            _dragFrom = "";
            _dragTarget = "";
            if (string.IsNullOrEmpty(to)) return;

            var res = Run.DragTower(from, to);
            if (!res.Ok)
            {
                switch (res.Reason)
                {
                    case "different-tower": Toast.Show("不同的防御塔不能合成（需要同一种塔）", "warn"); break;
                    case "level-mismatch": Toast.Show("等级不同，无法合成（需要同等级）", "warn"); break;
                    case "max-level": Toast.Show("已是最高等级 Lv.4", "warn"); break;
                    case "locked": Toast.Show("该塔台尚未解锁", "warn"); break;
                }
                return;
            }

            HandleEvents(res.Events);
            _selectedPlatform = to;
            _selectedAt = Elapsed;
            Battle.SyncUnits();
            Hud.UpdateHud(Run, Speed);
        }

        // ---------------------------------------------------------------- 机器人（试玩 / 截图自检用）
        public void Bot(float seconds)
        {
            if (Run == null || Run.Status != "running") return;

            int total = (int)seconds;
            for (int i = 0; i < total; i++)
            {
                if (Run.Status != "running") return;

                if (Run.PendingChoices.Count > 0) PickAffix(Run.PendingChoices[0]);

                bool merged = false;
                var placed = new List<TowerState>(Run.Towers);
                foreach (var a in placed)
                {
                    if (a.Level >= 4) continue;
                    foreach (var b in placed)
                    {
                        if (a.Id == b.Id || a.Level != b.Level || a.OpId != b.OpId) continue;
                        var res = Run.DragTower(a.PlatformId, b.PlatformId);
                        if (res.Ok)
                        {
                            HandleEvents(res.Events);
                            merged = true;
                            break;
                        }
                    }
                    if (merged) break;
                }

                if (merged)
                {
                    Run.Advance(1f);
                    continue;
                }

                if (Run.FreePlatforms().Count > 0 && Run.Gold >= Run.SummonCost())
                {
                    var r2 = Run.Summon();
                    if (r2.Ok) HandleEvents(r2.Events);
                    Run.Advance(1f);
                    continue;
                }

                if (Run.Gold >= Run.UpgradeCost() * 1.15f)
                {
                    var up = Run.PurchaseUpgrade();
                    if (up.Ok) PickAffix(up.Choices[0]);
                    Run.Advance(1f);
                    continue;
                }

                var ev = Run.Advance(1f);
                HandleEvents(ev);

                if (Run.Enemies.Count > 0)
                {
                    EnemyState best = Run.Enemies[0];
                    foreach (var e in Run.Enemies)
                    {
                        if (e.Dist > best.Dist) best = e;
                    }
                    Run.MoveHero(best.Pos);
                }
            }
        }

        // ---------------------------------------------------------------- 截图
        public IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            var dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "Shots"));
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, name + ".png");
            ScreenCapture.CaptureScreenshot(path);
            yield return null;
            yield return null;
            Debug.Log("SHOT " + name);
        }
    }
}




