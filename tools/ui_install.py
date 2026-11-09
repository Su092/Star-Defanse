"""把 AI 生成的 C6 素材安装进工程，并用「四边独立」的 spriteBorder 写九宫格。

为什么必须是四边独立：
  AI 生成的边框天然不等厚（panel-a 上 61 / 下 53），Unity 的 spriteBorder
  正好是 (left, bottom, right, top) 四值语义，所以照实测写入即可。
  早期版本用「短边 × 固定比例」给四个相等值，结果四角要么大于真实白带
  （切进内腔）、要么小于白带（白带进了可拉伸区，拉伸时被抹开）。

安装映射（源 -> 工程内文件名）：
  panel-a       -> panel.png
  panel-gold-a  -> panel-gold.png
  card-a        -> card.png
  plate-a       -> frame-plate.png
  plate-b       -> plate-thin.png
  button-a      -> button-primary.png
  button-gold-a -> button-gold.png
  round-a       -> frame-round.png
  bar-a         -> bar.png
  chip-a        -> chip.png

原素材备份到 tools/_ui_backup/，重复运行不会覆盖备份。

用法：
  python tools/ui_install.py            # 安装（源齐全才装，缺的跳过并报告）
  python tools/ui_install.py --dry      # 只打印计划，不写文件
"""
import json
import os
import shutil
import sys

from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
GEN = os.path.join(ROOT, "UiStyleTests", "gen")
UI = os.path.join(ROOT, "Assets", "Resources", "Art", "UI")
BAK = os.path.join(ROOT, "tools", "_ui_backup")
BORDERS = os.path.join(ROOT, "tools", "_ui_borders.json")

MAP = [
    ("panel-a", "panel"),
    ("panel-gold-a", "panel-gold"),
    ("card-a", "card"),
    ("plate-a", "frame-plate"),
    ("plate-b", "plate-thin"),
    ("button-a", "button-primary"),
    ("button-gold-a", "button-gold"),
    ("round-a", "frame-round"),
    ("bar-a", "bar"),
    ("chip-a", "chip"),
]

MAX_SIDE = 1024          # 工程里 UI 贴图统一不超过 1024，控制包体

# 这些素材是「整图拉伸」，不写 spriteBorder。
#   frame-round  圆按钮底图，RoundButton 用 Image.Type.Simple，圆形无法切片
#   bar          内腔是透明的，填充另有一层 Solid 叠上去；按整图拉伸最稳
#   chip         STUi.Chip 走程序化 RoundSprite，这张素材实际未被使用
NO_BORDER = {"frame-round", "bar", "chip"}


def load_borders():
    if not os.path.exists(BORDERS):
        return {}
    with open(BORDERS, encoding="utf-8") as f:
        return json.load(f)


def shrink(im, max_side=MAX_SIDE):
    """等比缩到 max_side 以内，返回 (im, 缩放系数)。"""
    w, h = im.size
    m = max(w, h)
    if m <= max_side:
        return im, 1.0
    k = max_side / m
    return im.resize((max(1, round(w * k)), max(1, round(h * k))), Image.LANCZOS), k


def main():
    dry = "--dry" in sys.argv
    borders = load_borders()
    os.makedirs(BAK, exist_ok=True)

    installed, skipped = [], []
    for src_name, dst_name in MAP:
        src = os.path.join(GEN, src_name + ".png")
        if not os.path.exists(src):
            skipped.append(src_name)
            continue

        info = borders.get(src_name)
        if info is None:
            skipped.append(src_name + "(无边距数据)")
            continue

        im = Image.open(src).convert("RGBA")
        box = info["box"]
        # 先按实测外接框裁掉多余透明边，让边距值直接对应裁剪后的图
        im = im.crop((box[0], box[1], box[2] + 1, box[3] + 1))
        im, k = shrink(im)

        b = {key: max(1.0, info[key] * k) for key in ("left", "bottom", "right", "top")}
        W, H = im.size
        # 保险：任意两片之和必须小于尺寸，否则 Unity 会强行压缩
        if b["left"] + b["right"] >= W:
            f = W * 0.9 / (b["left"] + b["right"])
            b["left"] *= f; b["right"] *= f
        if b["top"] + b["bottom"] >= H:
            f = H * 0.9 / (b["top"] + b["bottom"])
            b["top"] *= f; b["bottom"] *= f

        if dst_name in NO_BORDER:
            b = {key: 0.0 for key in ("left", "bottom", "right", "top")}

        print("%-14s -> %-16s %4dx%-4d  L%5.1f B%5.1f R%5.1f T%5.1f%s"
              % (src_name, dst_name + ".png", W, H,
                 b["left"], b["bottom"], b["right"], b["top"],
                 "  [dry]" if dry else ""))
        installed.append((dst_name, b))
        if dry:
            continue

        bak = os.path.join(BAK, dst_name + ".png")
        dst = os.path.join(UI, dst_name + ".png")
        if os.path.exists(dst) and not os.path.exists(bak):
            shutil.copy2(dst, bak)
        im.save(dst)

    if skipped:
        print("\n缺源（跳过）：" + ", ".join(skipped))

    if dry or not installed:
        return 0

    # 把边距写成 JSON，供 Unity 侧的安装器读取（精确四值）。
    # 必须是数组格式：Unity 的 JsonUtility 不能直接反序列化 Dictionary。
    items = [{"name": n, "left": b["left"], "bottom": b["bottom"],
              "right": b["right"], "top": b["top"]}
             for n, b in installed]
    with open(os.path.join(ROOT, "tools", "ui-sprite-borders.json"), "w",
              encoding="utf-8") as f:
        json.dump({"items": items}, f, indent=2, ensure_ascii=False)
    print("\n边距已写入 tools/ui-sprite-borders.json（%d 项）" % len(installed))
    return 0


if __name__ == "__main__":
    sys.exit(main())

