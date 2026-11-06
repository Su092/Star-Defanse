// 星轨防线 · 全屏界面（Godot scripts/ui.gd 的菜单 / 编队 / 关卡 / 弹窗移植）
//
// 约定：
//   · 所有坐标沿用 Godot 的设计像素（1080×1920，左上原点）；
//   · 面板与按钮优先套用 Resources/Art/UI 的九宫格素材；
//   · 词条胶囊、编队条等小控件保持平面样式（九宫格会吃掉内容区）。

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace StarLane
{
    public static class STScreens
    {
        // ---------------------------------------------------------------- 主菜单
        public static GameObject BuildMenu(Transform parent, int clearedCount,
            Action onStart, Action onFormation, Action onGuide)
        {
            var root = STUi.Rect(parent, "Menu", 0f, 0f, STUi.DesignWidth, STUi.DesignHeight);

            var kv = STAssets.Background("menu-kv");
            if (kv != null)
            {
                // KV 是 1080×1620，屏幕 1080×1920：按 cover 铺满并上下裁切（与 Godot 一致）
                var bg = STUi.PictureCovered(root, kv, 0f, 0f, STUi.DesignWidth, STUi.DesignHeight);
                bg.rectTransform.SetAsFirstSibling();
            }

            // 上 / 下压暗
            STUi.Gradient(root, 0f, 0f, STUi.DesignWidth, 520f,
                new Color(0.02f, 0.012f, 0.055f, 0.92f), new Color(0.02f, 0.012f, 0.055f, 0f));
            STUi.Gradient(root, 0f, 1180f, STUi.DesignWidth, 740f,
                new Color(0.02f, 0.012f, 0.055f, 0f), new Color(0.02f, 0.012f, 0.055f, 0.95f));

            var title = STUi.Label(root, "星轨防线", 108, STColors.Hex("9BF6FF"), true);
            STUi.SetRect(title.rectTransform, 54f, 120f, 1000f, 130f);
            STUi.Outline(title, STColors.Cyan.WithAlpha(0.55f), 3f);

            var en = STUi.Label(root, "S T A R L A N E   B U L W A R K", 26, STColors.Hex("A99BE0"), true);
            STUi.SetRect(en.rectTransform, 58f, 250f, 1000f, 40f);

            var rule = STUi.Solid(root, STColors.Cyan.WithAlpha(0.8f), 58f, 302f, 320f, 3f);
            rule.raycastTarget = false;

            var tag = STUi.Label(root, "竖屏星域塔防 · 编队召唤 · 同级合成", 21, STColors.Hex("C9C0ED"));
            STUi.SetRect(tag.rectTransform, 58f, 320f, 980f, 36f);

            var start = STUi.BigButton(root, "出击", "进入关卡选择", STColors.Cyan, 54f, 1450f, 972f, 132f);
            start.onClick.AddListener(() => onStart?.Invoke());

            var form = STUi.BigButton(root, "编队", "选择上阵干员", STColors.Hex("9E8CF2"), 54f, 1600f, 477f, 112f);
            form.onClick.AddListener(() => onFormation?.Invoke());

            var guide = STUi.BigButton(root, "玩法", "规则与词条", STColors.Hex("9E8CF2"), 549f, 1600f, 477f, 112f);
            guide.onClick.AddListener(() => onGuide?.Invoke());

            var foot = STUi.Label(root,
                $"已通关 {clearedCount} / {STData.Levels.Count}      每关 10 波 · 第 5 波精英 · 第 10 波 BOSS",
                18, STColors.Dim2);
            STUi.SetRect(foot.rectTransform, 58f, 1740f, 980f, 30f);

            return root.gameObject;
        }

        // ---------------------------------------------------------------- 编队
        public static GameObject BuildFormation(Transform parent, IList<string> formation,
            Dictionary<string, Sprite> portraits, Action<string> onToggle, Action onBack, Action onConfirm)
        {
            var root = MakeScreenRoot(parent, "Formation");
            AddBackdrop(root);

            var back = STUi.SmallButton(root, "‹ 返回", 46f, 54f, 140f, 56f, STColors.Hex("7A6BC7"),
                STColors.Hex("B9AEF0"));
            back.onClick.AddListener(() => onBack?.Invoke());

            var title = STUi.LabelCentered(root, "编队 · SQUAD", 34, STColors.White, true);
            STUi.SetRect(title.rectTransform, 0f, 60f, STUi.DesignWidth, 44f);

            var count = STUi.LabelRight(root, $"{formation.Count} / 4", 24, STColors.Cyan, true);
            STUi.SetRect(count.rectTransform, 830f, 62f, 200f, 36f);

            var note = STUi.Label(root, "上阵干员的职业决定召唤池，他们的签名词条进入本局词条池。最多 4 名。",
                19, STColors.Hex("948AC4"));
            STUi.SetRect(note.rectTransform, 46f, 128f, 988f, 30f);

            // 干员卡片网格（2 列，滚动）
            var scroll = MakeScroll(root, 40f, 176f, 1000f, 1330f, out var content);
            var grid = content.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(491f, 248f);
            grid.spacing = new Vector2(18f, 18f);
            grid.padding = new RectOffset(0, 0, 0, 20);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;
            grid.childAlignment = TextAnchor.UpperLeft;

            // GridLayoutGroup 不会撑高 Content，这里显式给出高度，避免 ScrollRect 无法滚动。
            // 注意横向宽度必须保持 0（锚点已左右拉伸），否则 Content 会变成 2 倍宽并整体偏移。
            int rows = Mathf.CeilToInt(STData.Operatives.Count / 2f);
            content.sizeDelta = new Vector2(0f, rows * 248f + (rows - 1) * 18f + 20f);
            var fitter = content.GetComponent<ContentSizeFitter>();
            if (fitter != null) fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

            foreach (var op in STData.Operatives)
            {
                bool selected = formation.Contains(op.id);
                portraits.TryGetValue(op.portrait, out var tex);
                var card = BuildOperativeCard(content, op, selected, tex);
                var id = op.id;
                card.onClick.AddListener(() => onToggle?.Invoke(id));
            }

            // 词条池
            var poolImg = STUi.FlatPanel(root, 40f, 1524f, 1000f, 130f,
                new Color(0.086f, 0.066f, 0.19f, 0.85f), new Color(0.38f, 0.32f, 0.66f, 0.55f), 16, 2);
            var poolPanel = poolImg.rectTransform;

            var poolLabel = STUi.Label(poolPanel, "本局词条池", 17, STColors.Hex("8B80BE"), true);
            STUi.SetRect(poolLabel.rectTransform, 20f, 12f, 400f, 24f);

            float px = 20f;
            float py = 44f;
            foreach (var id in STData.AffixPool(formation))
            {
                var a = STData.GetAffix(id);
                if (a == null) continue;

                var chip = STUi.Chip(poolPanel, a.name, px, py, new Color(0.59f, 0.5f, 0.94f, 0.55f), 16);
                var chipText = chip.GetComponentInChildren<Text>();
                float w = (chipText != null ? chipText.preferredWidth : 80f) + 26f;
                chip.rectTransform.sizeDelta = new Vector2(w, 32f);
                chip.rectTransform.anchoredPosition = new Vector2(px, -py);
                px += w + 10f;
                if (px > 900f) { px = 20f; py += 40f; }
            }

            var confirm = STUi.BigButton(root, "确认编队", "", STColors.Cyan, 40f, 1674f, 1000f, 120f);
            confirm.onClick.AddListener(() => onConfirm?.Invoke());

            return root.gameObject;
        }

        /// <summary>
        /// 卡片底（对应 Godot SLUi.style_button 的 big 分支）：
        /// 大按钮/卡片统一套 ui-button-primary（金色强调用 ui-button-gold）。
        /// 注意 card.png 是竖幅素材，只适合作插画底，不适合当 232–248px 高的横向卡片。
        /// </summary>
        private static Image CardBackground(RectTransform rt, Color accent, bool useCardArt = false)
        {
            var key = useCardArt ? "card" : (STUi.IsGold(accent) ? "button-gold" : "button-primary");
            var sprite = STAssets.Ui(key);

            var img = rt.gameObject.AddComponent<Image>();
            if (sprite != null)
            {
                img.sprite = sprite;
                img.type = Image.Type.Sliced;
                img.color = Color.white;
            }
            else
            {
                img.sprite = STUi.RoundSprite(18, accent, 2);
                img.type = Image.Type.Sliced;
                img.color = new Color(0.098f, 0.075f, 0.22f, 1f);
            }
            return img;
        }

        private static Button BuildOperativeCard(Transform parent, OperativeData op, bool selected, Sprite portrait)
        {
            var cls = STData.GetClass(op.classId);
            var accent = selected ? op.Accent : new Color(0.38f, 0.32f, 0.66f);

            var rt = STUi.Rect(parent, "Card-" + op.id, 0f, 0f, 491f, 248f);
            var btn = rt.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.ColorTint;

            var bg = CardBackground(rt, accent);
            btn.targetGraphic = bg;

            var colors = btn.colors;
            colors.highlightedColor = new Color(1.12f, 1.16f, 1.2f, 1f);
            colors.pressedColor = new Color(0.85f, 0.87f, 0.92f, 1f);
            btn.colors = colors;

            if (portrait != null)
            {
                // 立绘 660×990，卡片位 142×204：cover 会横向裁掉一部分，需要外层裁切
                var clip = STUi.Clip(rt, 24f, 22f, 142f, 204f, "PortraitClip");
                var img = STUi.PictureCovered(clip.transform, portrait, 0f, 0f, 142f, 204f);
                img.rectTransform.SetParent(clip.transform, true);
            }

            const float x = 184f;
            var name = STUi.Label(rt, op.name, 36, STColors.White, true);
            STUi.SetRect(name.rectTransform, x, 14f, 260f, 46f);

            var en = STUi.Label(rt, op.en, 14, STColors.Dim2, true);
            STUi.SetRect(en.rectTransform, x, 58f, 260f, 22f);

            var title = STUi.Label(rt, op.title, 18, STColors.Hex("C5BAF5"));
            STUi.SetRect(title.rectTransform, x, 78f, 280f, 26f);

            if (cls != null)
            {
                var clsLabel = STUi.Label(rt, $"{cls.name} · {cls.role}", 18, cls.Accent, true);
                STUi.SetRect(clsLabel.rectTransform, x, 106f, 300f, 26f);
            }

            var trait = STUi.Label(rt, op.trait, 16, STColors.Dim);
            STUi.SetRect(trait.rectTransform, x, 134f, 300f, 24f);

            // 签名词条胶囊
            float cx = x;
            float cy = 164f;
            foreach (var aid in op.affixes)
            {
                var a = STData.GetAffix(aid);
                if (a == null) continue;
                var chip = STUi.Chip(rt, a.name, cx, cy, new Color(0.59f, 0.5f, 0.94f, 0.55f), 15);
                var chipText = chip.GetComponentInChildren<Text>();
                float w = (chipText != null ? chipText.preferredWidth : 70f) + 24f;
                chip.rectTransform.sizeDelta = new Vector2(w, 34f);
                chip.rectTransform.anchoredPosition = new Vector2(cx, -cy);
                cx += w + 8f;
                if (cx > x + 290f) { cx = x; cy += 40f; }
            }

            // 选中角标
            var badgeColor = selected ? op.Accent : new Color(0.55f, 0.5f, 0.78f, 0.35f);
            var badge = STUi.Rect(rt, "Badge", 380f, 12f, 82f, 32f);
            var badgeImg = badge.gameObject.AddComponent<Image>();
            badgeImg.sprite = STUi.RoundSprite(999, Color.clear, 0);
            badgeImg.type = Image.Type.Sliced;
            badgeImg.color = badgeColor;
            badgeImg.raycastTarget = false;

            var badgeLabel = STUi.LabelCentered(badge, selected ? "已上阵" : "＋", 15,
                selected ? STColors.Hex("05131A") : STColors.Hex("CFC6F5"), true);
            STUi.SetRect(badgeLabel.rectTransform, 0f, 4f, 82f, 24f);

            return btn;
        }

        // ---------------------------------------------------------------- 关卡
        public static GameObject BuildLevels(Transform parent, IList<bool> cleared, IList<string> formation,
            Dictionary<string, Sprite> portraits, Action<int> onSelect, Action onBack, Action onFormation)
        {
            var root = MakeScreenRoot(parent, "Levels");
            AddBackdrop(root);

            var back = STUi.SmallButton(root, "‹ 返回", 46f, 54f, 140f, 56f, STColors.Hex("7A6BC7"),
                STColors.Hex("B9AEF0"));
            back.onClick.AddListener(() => onBack?.Invoke());

            var title = STUi.LabelCentered(root, "关卡 · SECTORS", 34, STColors.White, true);
            STUi.SetRect(title.rectTransform, 0f, 60f, STUi.DesignWidth, 44f);

            int clearedCount = 0;
            foreach (var c in cleared) if (c) clearedCount++;
            var count = STUi.LabelRight(root, $"{clearedCount} / {STData.Levels.Count}", 24, STColors.Cyan, true);
            STUi.SetRect(count.rectTransform, 830f, 62f, 200f, 36f);

            float y = 140f;
            for (int i = 0; i < STData.Levels.Count; i++)
            {
                var lv = STData.Levels[i];
                bool unlocked = i == 0 || (i - 1 < cleared.Count && cleared[i - 1]);
                bool done = i < cleared.Count && cleared[i];
                var thumb = STAssets.LevelThumb(lv.id);
                var card = BuildLevelCard(root, lv, unlocked, done, thumb, 46f, y, 988f, 232f);
                int index = i;
                card.onClick.AddListener(() => onSelect?.Invoke(index));
                card.interactable = unlocked;
                y += 252f;
            }

            // 提示面板
            var tipsImg = STUi.FlatPanel(root, 46f, y, 988f, 150f,
                new Color(0.078f, 0.059f, 0.17f, 0.75f), new Color(0.38f, 0.32f, 0.66f, 0.45f), 16, 1);
            var tips = tipsImg.rectTransform;

            string[] lines =
            {
                "召唤 · 花金币从编队随机出 1 级塔，落在已解锁空塔台；每召唤 3 次解锁 1 个新塔台。",
                "合成 · 把同一种防御塔拖到一起，等级 +1、干员不变（最高 4 级）。",
                "升级 · 从编队词条池抽 3 选 1，全局生效；价格逐次提升。",
            };
            float ty = 16f;
            foreach (var line in lines)
            {
                var t = STUi.Label(tips, line, 18, STColors.Hex("B7ACDF"));
                STUi.SetRect(t.rectTransform, 20f, ty, 948f, 30f);
                ty += 42f;
            }

            // 编队条
            float sx = 46f;
            foreach (var id in formation)
            {
                var op = STData.GetOperative(id);
                if (op == null) continue;
                var cls = STData.GetClass(op.classId);

                var chip = STUi.Rect(root, "Squad-" + id, sx, 1500f, 240f, 56f);
                var chipImg = chip.gameObject.AddComponent<Image>();
                chipImg.sprite = STUi.RoundSprite(12, op.Accent, 2);
                chipImg.type = Image.Type.Sliced;
                chipImg.color = new Color(0.086f, 0.066f, 0.19f, 0.88f);
                chipImg.raycastTarget = false;

                if (portraits.TryGetValue(op.portrait, out var tex) && tex != null)
                {
                    var clip = STUi.Clip(chip, 8f, 5f, 44f, 46f, "FaceClip");
                    var img = STUi.PictureCovered(clip.transform, tex, 0f, 0f, 44f, 46f);
                    img.rectTransform.SetParent(clip.transform, true);
                }

                var nm = STUi.Label(chip, op.name, 20, STColors.White, true);
                STUi.SetRect(nm.rectTransform, 58f, 12f, 100f, 30f);

                if (cls != null)
                {
                    var cl = STUi.Label(chip, cls.name, 16, cls.Accent, true);
                    STUi.SetRect(cl.rectTransform, 130f, 16f, 80f, 26f);
                }

                sx += 252f;
            }

            var adj = STUi.BigButton(root, "调整编队", "", STColors.Hex("9E8CF2"), 46f, 1580f, 988f, 110f);
            adj.onClick.AddListener(() => onFormation?.Invoke());

            return root.gameObject;
        }

        private static Button BuildLevelCard(Transform parent, LevelData lv, bool unlocked, bool done,
            Sprite thumb, float x, float y, float w, float h)
        {
            var rt = STUi.Rect(parent, "Level-" + lv.id, x, y, w, h);
            var btn = rt.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.ColorTint;

            var bg = CardBackground(rt, unlocked ? new Color(0.38f, 0.32f, 0.66f) : new Color(0.3f, 0.27f, 0.45f));
            btn.targetGraphic = bg;

            if (thumb != null)
            {
                var clip = STUi.Clip(rt, 560f, 14f, 336f, 204f, "ThumbClip");
                var img = STUi.PictureCovered(clip.transform, thumb, 0f, 0f, 336f, 204f);
                img.rectTransform.SetParent(clip.transform, true);
                img.color = unlocked ? new Color(1f, 1f, 1f, 0.95f) : new Color(0.55f, 0.55f, 0.6f, 0.9f);
            }

            var idx = STUi.Label(rt, "0" + lv.index, 20, unlocked ? STColors.Cyan : new Color(0.55f, 0.52f, 0.7f), true);
            STUi.SetRect(idx.rectTransform, 34f, 20f, 120f, 28f);

            var nm = STUi.Label(rt, lv.name, 42, unlocked ? STColors.White : new Color(0.62f, 0.58f, 0.72f), true);
            STUi.SetRect(nm.rectTransform, 34f, 46f, 500f, 56f);

            var en = STUi.Label(rt, lv.en, 16, STColors.Dim2, true);
            STUi.SetRect(en.rectTransform, 34f, 104f, 500f, 24f);

            string[] diffs = { "标准", "困难", "极难" };
            var diff = STUi.Label(rt, "难度 · " + diffs[Mathf.Clamp(lv.index - 1, 0, 2)], 19, STColors.Hex("C5BAF5"));
            STUi.SetRect(diff.rectTransform, 34f, 136f, 400f, 26f);

            var meta = STUi.Label(rt, "10 波 · 精英 第5波 · BOSS 第10波", 17, STColors.Dim);
            STUi.SetRect(meta.rectTransform, 34f, 164f, 520f, 24f);

            string state = done ? "★ 已通关" : (unlocked ? "未通关" : "通关上一关解锁");
            var stateColor = done ? STColors.Gold : (unlocked ? STColors.White : new Color(0.62f, 0.58f, 0.72f));
            var stateLabel = STUi.Label(rt, state, 20, stateColor, true);
            STUi.SetRect(stateLabel.rectTransform, 34f, 194f, 520f, 28f);

            if (!unlocked) rt.gameObject.AddComponent<CanvasGroup>().alpha = 0.72f;
            return btn;
        }

        // ---------------------------------------------------------------- 词条弹窗
        public static GameObject BuildAffixModal(Transform parent, IList<string> choices, int gold,
            Action<string> onPick, Action onLater)
        {
            var root = MakeScreenRoot(parent, "AffixModal");
            AddMask(root, 0.86f);

            var box = STUi.Panel(root, 56f, 470f, 968f, 980f, STColors.Hex("0E0A20"),
                new Color(0.48f, 0.42f, 0.78f, 0.8f), 26, 3);

            var kicker = STUi.LabelCentered(box.rectTransform, "SYNERGY DRAW", 16, STColors.Cyan, true);
            STUi.SetRect(kicker.rectTransform, 0f, 50f, 968f, 24f);

            var title = STUi.LabelCentered(box.rectTransform, "抽取词条", 46, STColors.White, true);
            STUi.SetRect(title.rectTransform, 0f, 74f, 968f, 60f);

            var sub = STUi.LabelCentered(box.rectTransform, "从编队词条池中随机 3 选 1，永久生效", 18, STColors.Dim);
            STUi.SetRect(sub.rectTransform, 0f, 138f, 968f, 26f);

            float y = 190f;
            for (int i = 0; i < choices.Count; i++)
            {
                var id = choices[i];
                var a = STData.GetAffix(id);
                if (a == null) { y += 222f; continue; }

                var col = STData.AffinityColor(id);

                var card = STUi.Rect(box.rectTransform, "Affix-" + id, 22f, y, 924f, 208f);
                var btn = card.gameObject.AddComponent<Button>();
                btn.transition = Selectable.Transition.ColorTint;

                var bg = CardBackground(card, col);
                btn.targetGraphic = bg;
                btn.onClick.AddListener(() => onPick?.Invoke(id));

                var fam = STUi.Label(card, STData.FamilyName(a.family) + "词条", 15, col, true);
                STUi.SetRect(fam.rectTransform, 34f, 24f, 300f, 22f);

                var name = STUi.Label(card, a.name, 38, STColors.White, true);
                STUi.SetRect(name.rectTransform, 34f, 50f, 700f, 52f);

                var desc = STUi.Label(card, a.desc, 20, STColors.Hex("C5BAF5"));
                STUi.SetRect(desc.rectTransform, 34f, 110f, 800f, 60f);
                desc.horizontalOverflow = HorizontalWrapMode.Wrap;
                desc.verticalOverflow = VerticalWrapMode.Truncate;

                var idxLabel = STUi.LabelRight(card, "0" + (i + 1), 30, new Color(1f, 1f, 1f, 0.12f), true);
                STUi.SetRect(idxLabel.rectTransform, 840f, 16f, 60f, 40f);

                var pick = STUi.Solid(card, col, 742f, 132f, 140f, 48f);
                pick.sprite = STUi.RoundSprite(999, Color.clear, 0);
                pick.type = Image.Type.Sliced;
                var pickLabel = STUi.LabelCentered(pick.rectTransform, "选择", 18, STColors.Hex("05131A"), true);
                STUi.Stretch(pickLabel.rectTransform);

                y += 222f;
            }

            var goldLabel = STUi.Label(box.rectTransform, "金币 " + gold, 19, STColors.Gold, true);
            STUi.SetRect(goldLabel.rectTransform, 44f, 880f, 300f, 28f);

            var later = STUi.SmallButton(box.rectTransform, "稍后选择", 690f, 872f, 240f, 50f,
                STColors.Hex("7A6BC7"), STColors.Hex("B9AEF0"));
            later.onClick.AddListener(() => onLater?.Invoke());

            return root.gameObject;
        }

        // ---------------------------------------------------------------- 暂停
        public static GameObject BuildPause(Transform parent, string statsLine,
            Action onResume, Action onRestart, Action onQuit)
        {
            var root = MakeScreenRoot(parent, "Pause");
            AddMask(root, 0.86f);

            var box = STUi.Panel(root, 100f, 620f, 880f, 620f, STColors.Hex("0E0A20"),
                new Color(0.48f, 0.42f, 0.78f, 0.8f), 26, 3);

            var title = STUi.LabelCentered(box.rectTransform, "战斗暂停", 46, STColors.White, true);
            STUi.SetRect(title.rectTransform, 0f, 62f, 880f, 60f);

            var st = STUi.LabelCentered(box.rectTransform, statsLine, 19, STColors.Hex("B9AEF0"));
            STUi.SetRect(st.rectTransform, 30f, 132f, 820f, 60f);
            st.horizontalOverflow = HorizontalWrapMode.Wrap;

            var resume = STUi.BigButton(box.rectTransform, "继续", "", STColors.Cyan, 34f, 208f, 812f, 116f);
            resume.onClick.AddListener(() => onResume?.Invoke());

            var restart = STUi.BigButton(box.rectTransform, "重开本关", "", STColors.Hex("9E8CF2"), 34f, 340f, 812f, 100f);
            restart.onClick.AddListener(() => onRestart?.Invoke());

            var quit = STUi.BigButton(box.rectTransform, "退出到关卡", "", STColors.Hex("9E8CF2"), 34f, 456f, 812f, 100f);
            quit.onClick.AddListener(() => onQuit?.Invoke());

            return root.gameObject;
        }

        // ---------------------------------------------------------------- 玩法说明
        public static GameObject BuildGuide(Transform parent, Action onClose)
        {
            var root = MakeScreenRoot(parent, "Guide");
            AddMask(root, 0.9f);

            var box = STUi.Panel(root, 46f, 90f, 988f, 1740f, STColors.Hex("0E0A20"),
                new Color(0.48f, 0.42f, 0.78f, 0.8f), 26, 3);

            var title = STUi.LabelCentered(box.rectTransform, "玩法说明", 44, STColors.White, true);
            STUi.SetRect(title.rectTransform, 0f, 122f, 988f, 56f);

            var scroll = MakeScroll(box.rectTransform, 72f, 200f, 896f, 1420f, out var content);
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 14f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.padding = new RectOffset(0, 0, 0, 24);

            var contentFitter = content.gameObject.AddComponent<ContentSizeFitter>();
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            (string head, string body)[] sections =
            {
                ("核心目标", "敌人沿固定轨道进入，抵达核心会扣除耐久（共 20）。守住 10 波即通关，第 5 波出现精英、第 10 波出现 BOSS。"),
                ("召唤", "消耗金币从编队中随机召唤一座 1 级防御塔，只能落在已解锁的空塔台上；价格逐次提升，每召唤 3 次解锁 1 个新塔台。"),
                ("合成", "把同一种防御塔拖到另一个同等级的同种塔上才会合成：等级 +1，干员不变。不同干员或不同等级都不能合成，最高 4 级。"),
                ("移动", "拖动防御塔到空塔台即可换位置；拖到不可合成的塔上会提示原因。"),
                ("升级 = 抽词条", "消耗金币从编队词条池抽 3 选 1，选择后全局生效；价格逐次提升。"),
                ("拦截职业", "拦截塔本体是发射基座，会在附近轨道生成拦截单位：在锚点附近移动拦敌，阵亡后按冷却重建。"),
                ("英雄", "点击/拖动地面移动英雄，自动攻击范围内的敌人；英雄不会死亡，也无法阻挡敌人。"),
            };

            foreach (var s in sections)
            {
                var card = STUi.Rect(content, "Section", 0f, 0f, 880f, 0f);

                // 分节卡片用纯色圆角（与 Godot 的 StyleBoxFlat 一致），不用九宫格
                var cardImg = card.gameObject.AddComponent<Image>();
                cardImg.sprite = STUi.RoundSprite(14, new Color(0.38f, 0.32f, 0.66f, 0.45f), 1);
                cardImg.type = Image.Type.Sliced;
                cardImg.color = new Color(0.086f, 0.066f, 0.19f, 0.8f);
                cardImg.raycastTarget = false;

                var vlg = card.gameObject.AddComponent<VerticalLayoutGroup>();
                vlg.padding = new RectOffset(18, 18, 14, 14);
                vlg.spacing = 6f;
                vlg.childControlWidth = true;
                vlg.childControlHeight = true;
                vlg.childForceExpandWidth = true;
                vlg.childForceExpandHeight = false;

                var fitter = card.gameObject.AddComponent<ContentSizeFitter>();
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                var head = STUi.Label(card, s.head, 24, STColors.Cyan, true);
                head.horizontalOverflow = HorizontalWrapMode.Wrap;

                var body = STUi.Label(card, s.body, 19, STColors.Hex("C9C0ED"));
                body.horizontalOverflow = HorizontalWrapMode.Wrap;

                var bodyEl = body.gameObject.AddComponent<LayoutElement>();
                bodyEl.preferredWidth = 840f;
            }

            var close = STUi.BigButton(box.rectTransform, "知道了", "", STColors.Cyan, 72f, 1652f, 896f, 110f);
            close.onClick.AddListener(() => onClose?.Invoke());

            return root.gameObject;
        }

        // ---------------------------------------------------------------- 结算
        public static GameObject BuildResult(Transform parent, RunState run, Action onRetry, Action onLevels)
        {
            bool win = run.Status == "victory";
            var root = MakeScreenRoot(parent, "Result");
            AddMask(root, 0.88f);

            var accent = win ? STColors.Cyan : STColors.Red;
            var box = STUi.Panel(root, 70f, 520f, 940f, 880f, STColors.Hex("0E0A20"), accent, 26, 3);

            var kicker = STUi.LabelCentered(box.rectTransform,
                win ? "SECTOR SECURED" : "CORE BREACHED", 16, accent, true);
            STUi.SetRect(kicker.rectTransform, 0f, 48f, 940f, 24f);

            var title = STUi.LabelCentered(box.rectTransform, win ? "防线稳固" : "核心失守", 62,
                STColors.White, true);
            STUi.SetRect(title.rectTransform, 0f, 76f, 940f, 82f);

            var lv = STUi.LabelCentered(box.rectTransform,
                $"{run.Level.name} · 第 {Mathf.Min(run.WaveIndex, 10)} / 10 波", 21, STColors.Dim);
            STUi.SetRect(lv.rectTransform, 0f, 164f, 940f, 30f);

            if (win)
            {
                int stars = run.CoreHp >= run.CoreHpMax ? 3 : (run.CoreHp >= run.CoreHpMax * 0.5f ? 2 : 1);
                for (int i = 0; i < 3; i++)
                {
                    var starSprite = STAssets.Ui("icon-star");
                    float sx = 380f + i * 82f;
                    if (starSprite != null)
                    {
                        var img = STUi.Picture(box.rectTransform, starSprite, sx, 184f, 68f, 68f);
                        img.color = i < stars ? Color.white : new Color(1f, 1f, 1f, 0.22f);
                    }
                    else
                    {
                        var label = STUi.LabelCentered(box.rectTransform, i < stars ? "★" : "☆", 54, STColors.Gold, true);
                        STUi.SetRect(label.rectTransform, sx, 184f, 68f, 68f);
                    }
                }
            }

            (string, string)[] stats =
            {
                ("击杀", run.Kills.ToString()),
                ("漏怪", run.LeakedCount.ToString()),
                ("核心", $"{(int)run.CoreHp} / {(int)run.CoreHpMax}"),
                ("召唤", run.Summons.ToString()),
                ("词条", run.Upgrades.ToString()),
                ("用时", $"{Mathf.FloorToInt(run.Time) / 60}:{Mathf.FloorToInt(run.Time) % 60:00}"),
            };

            float gy = 280f;
            for (int i = 0; i < stats.Length; i++)
            {
                var col = i % 3;
                int row = i / 3;
                STUi.FlatPanel(box.rectTransform, 28f + col * 300f, gy + row * 122f, 284f, 108f,
                    new Color(0.086f, 0.066f, 0.19f, 0.8f), new Color(0.38f, 0.32f, 0.66f, 0.5f), 14, 1);
                var cell = (RectTransform)box.rectTransform.GetChild(box.rectTransform.childCount - 1);

                var k = STUi.LabelCentered(cell, stats[i].Item1, 16, STColors.Dim2);
                STUi.SetRect(k.rectTransform, 0f, 14f, 284f, 24f);

                var v = STUi.LabelCentered(cell, stats[i].Item2, 30, STColors.White, true);
                STUi.SetRect(v.rectTransform, 0f, 42f, 284f, 44f);
            }

            var retry = STUi.BigButton(box.rectTransform, "再战", "同一编队重开", STColors.Cyan, 28f, 556f, 884f, 120f);
            retry.onClick.AddListener(() => onRetry?.Invoke());

            var levels = STUi.BigButton(box.rectTransform, "关卡", "", STColors.Hex("9E8CF2"), 28f, 690f, 884f, 100f);
            levels.onClick.AddListener(() => onLevels?.Invoke());

            return root.gameObject;
        }

        // ---------------------------------------------------------------- 通用
        private static RectTransform MakeScreenRoot(Transform parent, string name)
        {
            var root = STUi.Rect(parent, name, 0f, 0f, STUi.DesignWidth, STUi.DesignHeight);

            // 全屏根节点用于接收点击（遮罩由 AddMask 负责）
            var rt = root;
            var canvasGroup = rt.gameObject.AddComponent<CanvasGroup>();
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;
            return rt;
        }

        private static void AddBackdrop(RectTransform root)
        {
            var bg = STUi.Solid(root, STColors.Hex("09061C"), 0f, 0f, STUi.DesignWidth, STUi.DesignHeight);
            bg.raycastTarget = false;

            // 用菜单 KV 做一层极淡的底衬，避免纯色背景过于空洞
            var kv = STAssets.Background("menu-kv");
            if (kv != null)
            {
                var img = STUi.Picture(root, kv, 0f, 0f, STUi.DesignWidth, STUi.DesignHeight);
                img.preserveAspect = false;
                img.color = new Color(1f, 1f, 1f, 0.16f);
            }
        }

        private static void AddMask(RectTransform root, float alpha)
        {
            var mask = STUi.Solid(root, new Color(0.016f, 0.008f, 0.047f, alpha), 0f, 0f,
                STUi.DesignWidth, STUi.DesignHeight);
            mask.raycastTarget = true;
        }

        /// <summary>创建 ScrollRect，返回 Viewport 的 RectTransform（内容挂在这里）。</summary>
        private static RectTransform MakeScroll(RectTransform parent, float x, float y, float w, float h,
            out RectTransform content)
        {
            var scrollRt = STUi.Rect(parent, "Scroll", x, y, w, h);
            var scroll = scrollRt.gameObject.AddComponent<ScrollRect>();

            var viewportRt = STUi.Rect(scrollRt, "Viewport", 0f, 0f, w, h);
            var viewportImg = viewportRt.gameObject.AddComponent<Image>();
            viewportImg.color = new Color(0f, 0f, 0f, 0.001f);
            viewportImg.raycastTarget = true;
            viewportRt.gameObject.AddComponent<RectMask2D>();

            // Content 左右锚点拉伸（跟随视口宽度），纵向由调用方给高度
            var contentRt = STUi.Rect(viewportRt, "Content", 0f, 0f, 0f, 0f);
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.anchoredPosition = Vector2.zero;
            contentRt.sizeDelta = new Vector2(0f, 0f);

            scroll.viewport = viewportRt;
            scroll.content = contentRt;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;

            content = contentRt;
            return viewportRt;
        }
    }
}
