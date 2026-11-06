// 星轨防线 · 战斗 HUD（Godot scripts/ui.gd build_hud 的移植）
//
// 全部使用 Resources/Art/UI 下的生成素材，缺图时退回圆角纯色，
// 与 Godot 端的退化路径保持一致（不引入 SVG 或文字符号图标）。

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace StarLane
{
    public class HudView
    {
        public GameObject Root;

        public Text LevelName;
        public Text WaveNum;
        public Text Gold;
        public Image CoreFill;
        public Text CoreNum;
        public Button PauseButton;
        public Button SpeedButton;
        public Text SpeedLabel;
        public Button HelpButton;
        public Button SummonButton;
        public Button UpgradeButton;
        public Text SummonCost;
        public Text UpgradeCost;
        public RectTransform SquadRow;
        public RectTransform AffixRow;
        public RectTransform ArrowIcon;

        private RectTransform _rootRect;

        // 金币进账反馈：数字弹跳 + 右侧 "+N" 上浮淡出
        private RectTransform _goldRect;
        private Vector2 _goldBaseSize;
        private Text _goldGain;
        private RectTransform _goldGainRect;
        private float _goldPulse;      // 1 -> 0，控制缩放弹跳
        private float _goldGainLife;   // "+N" 剩余寿命

        // ---------------------------------------------------------------- 构建
        public void Build(Transform parent, Dictionary<string, Sprite> portraits)
        {
            var canvasRoot = STUi.Rect(parent, "Hud", 0f, 0f, STUi.DesignWidth, STUi.DesignHeight);
            _rootRect = canvasRoot;
            var ignore = canvasRoot.gameObject.AddComponent<CanvasGroup>();
            ignore.blocksRaycasts = true;
            Root = canvasRoot.gameObject;

            BuildPlates(canvasRoot);
            BuildRoundButtons(canvasRoot);
            BuildPortrait(canvasRoot, portraits);
            BuildAffixTray(canvasRoot);
            BuildBottomBar(canvasRoot);
            Root.SetActive(false);
        }

        private void BuildPlates(RectTransform root)
        {
            var plateBg = new Color(0.06f, 0.05f, 0.14f, 0.82f);
            var plateBorder = new Color(0.42f, 0.36f, 0.72f, 0.75f);

            // 左上一块长信息牌：用 plate-thin（窄条专用素材，装饰带仅 20%），
            // 而不是 frame-plate —— 后者装饰带占短边一半以上，塞进 72px 高的
            // 牌子里，九宫格四角会把内容区吃光（文字压在花纹上）。
            var namePlate = STUi.Rect(root, "NamePlate", 22f, 22f, 320f, 72f);
            var np = namePlate.gameObject.AddComponent<Image>();
            var npSprite = STAssets.Ui("plate-thin") ?? STAssets.Ui("frame-plate");
            if (npSprite != null) { np.sprite = npSprite; np.type = Image.Type.Sliced; np.color = Color.white; }
            else { np.sprite = STUi.RoundSprite(14, plateBorder, 2); np.type = Image.Type.Sliced; np.color = plateBg; }
            np.raycastTarget = false;
            STUi.FitSlice(np);

            LevelName = STUi.Label(namePlate, "星港回廊", 32, STColors.White, true);
            STUi.SetRect(LevelName.rectTransform, 40f, 18f, 270f, 44f);

            // 左上三块信息牌：frame-plate 边距 21，内容区 = 高 - 42。
            // 原来 56 高只剩 14 高可用，数字和「/ 10 波」全压在上下边框上；
            // 抬到 72 才有 30 高，够放 30px 字。
            var wavePlate = MakeSmallPlate(root, "WavePlate", 22f, 94f, 240f, 72f);
            WaveNum = STUi.Label(wavePlate, "1", 32, STColors.White, true);
            STUi.SetRect(WaveNum.rectTransform, 34f, 22f, 80f, 40f);
            var waveTail = STUi.Label(wavePlate, "/ 10 波", 22, STColors.Hex("C5BAF5"), true);
            STUi.SetRect(waveTail.rectTransform, 92f, 28f, 130f, 30f);

            var goldPlate = MakeSmallPlate(root, "GoldPlate", 22f, 174f, 240f, 72f);
            var coin = STAssets.Ui("icon-coin");
            if (coin != null) STUi.Picture(goldPlate, coin, 18f, 22f, 30f, 30f);
            Gold = STUi.Label(goldPlate, "40", 29, STColors.Gold, true);
            STUi.SetRect(Gold.rectTransform, 58f, 22f, 150f, 38f);

            _goldRect = (RectTransform)Gold.transform;
            _goldBaseSize = _goldRect.sizeDelta;

            // "+N" 进账提示：叠在金币数字右侧，默认透明/不可见
            _goldGain = STUi.Label(goldPlate, "", 25, STColors.Hex("FFE58A"), true);
            _goldGainRect = (RectTransform)_goldGain.transform;
            STUi.SetRect(_goldGainRect, 150f, 24f, 110f, 36f);
            _goldGain.text = "";
        }

        private static RectTransform MakeSmallPlate(RectTransform parent, string name, float x, float y, float w, float h)
        {
            var rt = STUi.Rect(parent, name, x, y, w, h);
            var img = rt.gameObject.AddComponent<Image>();
            // plate-thin 专为窄条信息牌设计（装饰带仅占短边 20%），
            // frame-plate 的装饰带过半，放在 72px 高的牌子上会吃掉内容区。
            var sprite = STAssets.Ui("plate-thin") ?? STAssets.Ui("frame-plate");
            if (sprite != null) { img.sprite = sprite; img.type = Image.Type.Sliced; img.color = Color.white; }
            else
            {
                img.sprite = STUi.RoundSprite(12, STColors.Hex("E5BF59").WithAlpha(0.7f), 2);
                img.type = Image.Type.Sliced;
                img.color = new Color(0.04f, 0.03f, 0.1f, 0.86f);
            }
            img.raycastTarget = false;
            STUi.FitSlice(img);
            return rt;
        }

        private void BuildRoundButtons(RectTransform root)
        {
            var pause = STUi.RoundButton(root, new Vector2(72f, 300f), 44f, STColors.White, "pause");
            PauseButton = pause;

            var speed = STUi.RoundButton(root, new Vector2(72f, 402f), 44f, STColors.Cyan, "");
            SpeedButton = speed;
            SpeedLabel = STUi.LabelCentered(speed.transform, "1×", 26, STColors.Cyan, true);
            STUi.Stretch(SpeedLabel.rectTransform);

            HelpButton = STUi.RoundButton(root, new Vector2(72f, 504f), 44f, STColors.Hex("C5BAF5"), "help");
        }

        private void BuildPortrait(RectTransform root, Dictionary<string, Sprite> portraits)
        {
            var rt = STUi.Rect(root, "PortraitPanel", 986f, 24f, 74f, 74f);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = STUi.RoundSprite(999, STColors.Hex("6BD1F2").WithAlpha(0.8f), 3);
            img.type = Image.Type.Sliced;
            img.color = new Color(0.07f, 0.05f, 0.16f, 0.9f);
            img.raycastTarget = false;

            if (portraits != null && portraits.TryGetValue("op-ran", out var tex) && tex != null)
            {
                var mask = rt.gameObject.AddComponent<Mask>();
                mask.showMaskGraphic = true;
                var avatar = STUi.PictureCovered(rt, tex, 4f, 4f, 66f, 66f);
                avatar.rectTransform.SetParent(rt, true);
            }

            var squad = STUi.Rect(root, "SquadRow", 990f, 108f, 72f, 0f);
            var layout = squad.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 6f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            SquadRow = squad;
        }

        private void BuildAffixTray(RectTransform root)
        {
            var row = STUi.Rect(root, "AffixRow", 22f, 576f, 250f, 0f);
            var layout = row.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 6f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            AffixRow = row;
        }

        private void BuildBottomBar(RectTransform root)
        {
            // 底部渐变压暗
            var bottom = STUi.Gradient(root, 0f, 1600f, 1080f, 320f,
                new Color(0.02f, 0.012f, 0.055f, 0.0f), new Color(0.015f, 0.01f, 0.045f, 1f));
            bottom.raycastTarget = false;

            // 强化按钮
            var upgrade = STUi.BigButton(root, "强化", "3 选 1 词条", STColors.Gold, 26f, 1700f, 300f, 148f);
            UpgradeButton = upgrade;

            // 成本行贴着按钮底部内沿排（不能写死 y=104：按钮高 148、边距 32，
            // 内容区底边在 y=116，104+28 会压到下边框上）
            var upImg = upgrade.GetComponent<Image>();
            float upBottom = 148f - Mathf.Max(STUi.SliceInsetY(upImg, 300f, 148f), 18f) - 26f;
            var coin = STAssets.Ui("icon-coin");
            if (coin != null) STUi.Picture(upgrade.transform, coin, 40f, upBottom, 26f, 26f);
            UpgradeCost = STUi.Label(upgrade.transform, "24", 26, STColors.Gold, true);
            STUi.SetRect(UpgradeCost.rectTransform, 72f, upBottom - 3f, 140f, 32f);

            // 核心耐久：用 plate-thin（窄条素材），装饰带 20%，内容区够放三层
            var corePanel = STUi.Rect(root, "CorePanel", 356f, 1688f, 330f, 100f);
            var coreImg = corePanel.gameObject.AddComponent<Image>();
            var coreSprite = STAssets.Ui("plate-thin") ?? STAssets.Ui("frame-plate");
            if (coreSprite != null) { coreImg.sprite = coreSprite; coreImg.type = Image.Type.Sliced; coreImg.color = Color.white; }
            else
            {
                coreImg.sprite = STUi.RoundSprite(12, STColors.Cyan.WithAlpha(0.6f), 2);
                coreImg.type = Image.Type.Sliced;
                coreImg.color = new Color(0.04f, 0.03f, 0.1f, 0.9f);
            }
            coreImg.raycastTarget = false;
            STUi.FitSlice(coreImg);

            // 内容区（按实测 inset 排布，而不是猜坐标）
            float cIns = STUi.SliceInsetY(coreImg, 330f, 100f);
            float cTop = cIns + 2f;
            float cBot = 100f - cIns - 2f;
            float cH = cBot - cTop;

            var crystal = STAssets.Ui("icon-crystal");
            if (crystal != null) STUi.Picture(corePanel, crystal, Mathf.Max(cIns, 10f), cTop, 28f, 28f);

            var coreLabel = STUi.Label(corePanel, "核心", 19, STColors.Hex("9FD8E8"), true);
            STUi.SetRect(coreLabel.rectTransform, Mathf.Max(cIns, 10f) + 36f, cTop, 80f, 26f);

            // 进度条横贯内容区，数字右侧对齐
            var barBg = STUi.Rect(corePanel, "BarBg", Mathf.Max(cIns, 10f) + 36f, cTop + cH - 30f,
                                  330f - Mathf.Max(cIns, 10f) * 2f - 66f, 24f);
            var barImg = barBg.gameObject.AddComponent<Image>();
            var barSprite = STAssets.Ui("bar");
            if (barSprite != null) { barImg.sprite = barSprite; barImg.type = Image.Type.Sliced; barImg.color = Color.white; }
            else
            {
                barImg.sprite = STUi.RoundSprite(8, STColors.Cyan.WithAlpha(0.55f), 2);
                barImg.type = Image.Type.Sliced;
                barImg.color = new Color(0.04f, 0.03f, 0.1f, 0.9f);
            }
            barImg.raycastTarget = false;
            STUi.FitSlice(barImg);

            // 填充条内缩 2px，位置跟随容器
            var fill = STUi.Solid(corePanel, STColors.Cyan,
                Mathf.Max(cIns, 10f) + 38f, cTop + cH - 28f,
                330f - Mathf.Max(cIns, 10f) * 2f - 70f, 20f);
            fill.sprite = STUi.RoundSprite(8, Color.clear, 0);
            fill.type = Image.Type.Sliced;
            CoreFill = fill;

            // 数字放在内容区右侧
            CoreNum = STUi.LabelRight(corePanel, "20", 24, STColors.White, true);
            STUi.SetRect(CoreNum.rectTransform, 330f - Mathf.Max(cIns, 10f) - 62f, cTop + cH - 32f, 56f, 28f);

            // 召唤大按钮
            var summon = STUi.RoundButton(root, new Vector2(954f, 1760f), 88f, STColors.Cyan, "", "", 0);
            SummonButton = summon;

            var star = STAssets.Ui("icon-star");
            if (star != null) STUi.Picture(summon.transform, star, 44f, 22f, 88f, 88f);

            var sKey = STUi.LabelCentered(summon.transform, "召唤", 22, STColors.White, true);
            STUi.SetRect(sKey.rectTransform, 0f, 112f, 176f, 28f);

            SummonCost = STUi.LabelCentered(root, "10", 28, STColors.Gold, true);
            STUi.SetRect(SummonCost.rectTransform, 866f, 1852f, 176f, 36f);

            // 跳动箭头（提示召唤按钮位置）
            var arrowSprite = STAssets.Ui("icon-arrow");
            if (arrowSprite != null)
            {
                var arrow = STUi.Picture(root, arrowSprite, 946f, 1612f, 62f, 56f);
                ArrowIcon = arrow.rectTransform;
            }
        }

        // ---------------------------------------------------------------- 刷新
        public void SetVisible(bool visible) => STUi.SetActive(Root, visible);

        public void UpdateHud(RunState run, int speed)
        {
            if (run == null) return;

            WaveNum.text = Mathf.Max(1, run.WaveIndex).ToString();
            LevelName.text = run.Level.name;

            float pct = Mathf.Clamp01(run.CoreHp / run.CoreHpMax);
            var col = pct < 0.4f ? STColors.Red : (pct < 0.7f ? STColors.Orange : STColors.Cyan);
            CoreFill.color = col;
            var rt = CoreFill.rectTransform;
            rt.sizeDelta = new Vector2(196f * pct, 14f);

            CoreNum.text = ((int)run.CoreHp).ToString();
            CoreNum.color = pct < 0.4f ? col : STColors.Hex("CFE9FF");

            Gold.text = ((int)run.Gold).ToString();
            SummonCost.text = run.SummonCost().ToString();
            UpgradeCost.text = run.UpgradeCost().ToString();
            SpeedLabel.text = speed + "×";

            bool canSummon = run.Gold >= run.SummonCost() && run.FreePlatforms().Count > 0;
            SummonButton.interactable = canSummon;

            bool canUpgrade = run.Gold >= run.UpgradeCost() || run.PendingChoices.Count > 0;
            UpgradeButton.interactable = canUpgrade;
        }

        // ---------------------------------------------------------------- 金币进账反馈
        /// <summary>
        /// 一次进账的醒目提示：金币数字弹一下 + 右侧 "+N" 上浮淡出。
        /// 波次结算与精英/BOSS 击杀调用；普通击杀只走战场飘字，避免 HUD 一直抖。
        /// </summary>
        public void FlashGold(int amount)
        {
            if (amount <= 0) return;
            _goldPulse = 1f;
            _goldGainLife = 1f;
            if (_goldGain != null)
            {
                _goldGain.text = "+" + amount;
                _goldGain.color = STColors.Hex("FFE58A");
            }
        }

        /// <summary>由 STMain 每帧驱动（HUD 不是 MonoBehaviour，不能自己 Update）。</summary>
        public void Tick(float dt)
        {
            if (_goldRect == null) return;

            // 数字弹跳：1 -> 0 的余弦衰减，最大放大 22%
            if (_goldPulse > 0f)
            {
                _goldPulse = Mathf.Max(0f, _goldPulse - dt / 0.42f);
                float pop = 1f + 0.22f * Mathf.Sin(_goldPulse * Mathf.PI);
                _goldRect.localScale = new Vector3(pop, pop, 1f);
                if (_goldPulse <= 0f) _goldRect.localScale = Vector3.one;
            }

            // "+N" 上浮 + 淡出
            if (_goldGainLife > 0f && _goldGain != null)
            {
                _goldGainLife = Mathf.Max(0f, _goldGainLife - dt / 0.9f);
                float k = 1f - _goldGainLife;                 // 0 -> 1
                var p = _goldGainRect.anchoredPosition;
                _goldGainRect.anchoredPosition = new Vector2(p.x, 8f + k * 30f);
                float a = k < 0.15f ? Mathf.Lerp(0f, 1f, k / 0.15f)
                        : (k > 0.6f ? Mathf.Lerp(1f, 0f, (k - 0.6f) / 0.4f) : 1f);
                var c = _goldGain.color;
                _goldGain.color = new Color(c.r, c.g, c.b, a);
                if (_goldGainLife <= 0f) _goldGain.text = "";
            }
        }

        public void RefreshSquad(RunState run, Dictionary<string, Sprite> portraits)
        {
            STUi.ClearChildren(SquadRow);

            var formation = run?.Formation ?? STData.NormalizeFormation(null);
            foreach (var id in formation)
            {
                var op = STData.GetOperative(id);
                if (op == null) continue;

                var chip = STUi.Rect(SquadRow, "Chip-" + id, 0f, 0f, 60f, 60f);
                var img = chip.gameObject.AddComponent<Image>();
                img.sprite = STUi.RoundSprite(999, op.Accent, 2);
                img.type = Image.Type.Sliced;
                img.color = new Color(0.07f, 0.05f, 0.16f, 0.92f);
                img.raycastTarget = false;

                var layoutElement = chip.gameObject.AddComponent<LayoutElement>();
                layoutElement.preferredWidth = 60f;
                layoutElement.preferredHeight = 60f;

                if (portraits != null && portraits.TryGetValue(op.portrait, out var tex) && tex != null)
                {
                    var clip = STUi.Clip(chip, 3f, 3f, 54f, 54f, "FaceClip");
                    var avatar = STUi.PictureCovered(clip.transform, tex, 0f, 0f, 54f, 54f);
                    avatar.rectTransform.SetParent(clip.transform, true);
                }
            }
        }

        public void RefreshAffixes(RunState run)
        {
            STUi.ClearChildren(AffixRow);

            foreach (var id in run.Affixes)
            {
                var a = STData.GetAffix(id);
                if (a == null) continue;

                var chip = STUi.Chip(AffixRow, a.name, 0f, 0f, new Color(0.59f, 0.5f, 0.94f, 0.55f), 16);
                var text = chip.GetComponentInChildren<Text>();
                float w = (text != null ? text.preferredWidth : 80f) + 26f;
                chip.rectTransform.sizeDelta = new Vector2(w, 32f);

                var le = chip.gameObject.AddComponent<LayoutElement>();
                le.preferredWidth = w;
                le.preferredHeight = 32f;
            }
        }
    }
}
