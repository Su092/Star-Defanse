// 星轨防线 · 顶部横幅与 toast（Godot scripts/ui.gd build_banner / main.gd _toast）
//
// 横幅用于波次提示（WAVE 01 / ELITE INBOUND / BOSS APPROACHING），
// 精英与 BOSS 会带生成好的威胁插画。

using System;
using UnityEngine;
using UnityEngine.UI;

namespace StarLane
{
    public class BannerView
    {
        public GameObject Root;
        private RectTransform _rootRect;
        private Image _art;
        private Image _glass;
        private Image _topRule;
        private Image _botRule;
        private Text _kicker;
        private Text _title;
        private Text _sub;
        private float _life;
        private float _fade;

        public void Build(Transform parent)
        {
            _rootRect = STUi.Rect(parent, "Banner", 0f, 0f, STUi.DesignWidth, STUi.DesignHeight);
            Root = _rootRect.gameObject;
            var group = Root.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            group.alpha = 0f;

            _art = STUi.PictureCovered(_rootRect, null, 160f, 596f, 760f, 200f);
            _art.color = new Color(1f, 1f, 1f, 0.78f);
            _art.gameObject.SetActive(false);

            _glass = STUi.Solid(_rootRect, new Color(0.03f, 0.02f, 0.08f, 0.55f), 160f, 596f, 760f, 200f);

            _topRule = STUi.Solid(_rootRect, STColors.Cyan.WithAlpha(0.75f), 160f, 596f, 760f, 2f);
            _botRule = STUi.Solid(_rootRect, STColors.Cyan.WithAlpha(0.75f), 160f, 794f, 760f, 2f);

            _kicker = STUi.LabelCentered(_rootRect, "WAVE 01", 18, STColors.Cyan, true);
            STUi.SetRect(_kicker.rectTransform, 160f, 620f, 760f, 26f);

            _title = STUi.LabelCentered(_rootRect, "第 1 波 / 10", 50, STColors.White, true);
            STUi.SetRect(_title.rectTransform, 160f, 648f, 760f, 66f);

            _sub = STUi.LabelCentered(_rootRect, "", 19, STColors.Dim);
            STUi.SetRect(_sub.rectTransform, 160f, 722f, 760f, 28f);

            Root.SetActive(false);
        }

        public void Show(string kicker, string title, string sub, string kind, Sprite threatArt)
        {
            _kicker.text = kicker;
            _title.text = title;
            _sub.text = sub;
            _sub.gameObject.SetActive(!string.IsNullOrEmpty(sub));

            bool hasArt = (kind == "boss" || kind == "elite") && threatArt != null;
            _art.gameObject.SetActive(hasArt);
            if (hasArt)
            {
                _art.sprite = threatArt;
                STUi.CoverInto(_art.rectTransform, threatArt, 160f, 596f, 760f, 200f);
            }

            var accent = kind == "boss" ? STColors.Magenta
                : (kind == "elite" ? STColors.Red : STColors.Cyan);
            _kicker.color = accent;
            _topRule.color = accent.WithAlpha(0.8f);
            _botRule.color = accent.WithAlpha(0.8f);

            Root.SetActive(true);
            var group = Root.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            _fade = 0f;
            _life = 2.1f;
        }

        public void Tick(float delta)
        {
            if (Root == null || !Root.activeSelf) return;
            _life -= delta;

            var group = Root.GetComponent<CanvasGroup>();
            if (_life > 1.85f)
            {
                _fade = Mathf.Clamp01(_fade + delta / 0.2f);
            }
            else if (_life > 0.35f)
            {
                _fade = 1f;
            }
            else
            {
                _fade = Mathf.Clamp01(_life / 0.35f);
            }
            group.alpha = _fade;

            if (_life <= 0f) Root.SetActive(false);
        }
    }

    /// <summary>底部 toast 提示条。</summary>
    public class ToastView
    {
        public GameObject Root;
        private Text _label;
        private CanvasGroup _group;
        private float _life;

        public void Build(Transform parent)
        {
            var rt = STUi.Rect(parent, "Toast", 0f, 0f, STUi.DesignWidth, STUi.DesignHeight);
            Root = rt.gameObject;
            _group = Root.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = false;
            _group.interactable = false;
            _group.alpha = 0f;

            var panel = STUi.Rect(rt, "Panel", 240f, 96f, 600f, 56f);
            var img = panel.gameObject.AddComponent<Image>();
            var sprite = STAssets.Ui("frame-plate");
            if (sprite != null) { img.sprite = sprite; img.type = Image.Type.Sliced; img.color = Color.white; }
            else
            {
                img.sprite = STUi.RoundSprite(14, new Color(0.59f, 0.5f, 0.94f, 0.7f), 2);
                img.type = Image.Type.Sliced;
                img.color = new Color(0.07f, 0.05f, 0.16f, 0.95f);
            }
            img.raycastTarget = false;

            _label = STUi.LabelCentered(panel, "", 21, STColors.Hex("EAF6FF"), true);
            STUi.Stretch(_label.rectTransform);
            _label.horizontalOverflow = HorizontalWrapMode.Wrap;
        }

        public void Show(string message, string tone = "")
        {
            _label.text = message;
            var col = STColors.Hex("EAF6FF");
            switch (tone)
            {
                case "warn": col = STColors.Hex("FFF3CE"); break;
                case "bad": col = STColors.Hex("FFE1E6"); break;
                case "good": col = STColors.Hex("DFFFEE"); break;
            }
            _label.color = col;
            _group.alpha = 1f;
            _life = 1.85f;
        }

        public void Tick(float delta)
        {
            if (_life <= 0f) return;
            _life -= delta;
            _group.alpha = _life > 0.35f ? 1f : Mathf.Clamp01(_life / 0.35f);
            if (_life <= 0f) _group.alpha = 0f;
        }
    }
}
