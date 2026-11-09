"""生成 UI 风格测试的对比图（带版本标签与量化配色）。

量化指标说明见下方 palette_stats：白甲占比最能说明"是不是白甲主导"。
"""
import os

import numpy as np
from PIL import Image, ImageDraw

SRC = "UiStyleTests"
OUT = os.path.join(SRC, "_compare.png")

VERSIONS = [
    ("ui-a-white-armor", "A", "白甲主导", "整块白色装甲板"),
    ("ui-b-armor-frame", "B", "白框 + 深色玻璃", "白甲做框，内嵌深色窗口"),
    ("ui-c-milspec", "C", "军规克制", "无发光，只有细指示线"),
    ("ui-d-heavy-armor", "D", "厚重装甲", "深腔 + 厚描边，最像塔"),
    ("ui-e-command-gold", "E", "指挥级金饰", "宽金边 + 金角标，高级感"),
]


def palette_stats(p):
    a = np.asarray(Image.open(p).convert("RGB")).astype(np.int16)
    r, g, b = a[:, :, 0], a[:, :, 1], a[:, :, 2]
    mx, mn = a.max(axis=2), a.min(axis=2)
    total = a.shape[0] * a.shape[1]
    return dict(
        white=(mn > 175).sum() / total,
        gold=((r > 120) & (r > b + 40) & (g > b + 10) & (mx > 140)).sum() / total,
        cyan=((b > 130) & (g > 110) & (g > r + 45) & (b > r + 45)).sum() / total,
        dark=(mx < 70).sum() / total,
    )


CELL_W = 380
LABEL_H = 84

tiles = []
for key, tag, name, desc in VERSIONS:
    p = os.path.join(SRC, key + ".png")
    im = Image.open(p).convert("RGB")
    st = palette_stats(p)
    im.thumbnail((CELL_W - 14, 700), Image.LANCZOS)
    tiles.append((im, tag, name, desc, st))
    print("%-20s 白%4.1f%% 金%4.1f%% 青%4.1f%% 深%4.1f%%"
          % (key, st["white"] * 100, st["gold"] * 100,
             st["cyan"] * 100, st["dark"] * 100))

cols = len(tiles)
H = max(t[0].height for t in tiles) + LABEL_H + 24
sheet = Image.new("RGB", (CELL_W * cols, H), (15, 13, 24))
d = ImageDraw.Draw(sheet)

for i, (im, tag, name, desc, st) in enumerate(tiles):
    x = i * CELL_W
    sheet.paste(im, (x + (CELL_W - im.width) // 2, 12))
    y = 12 + im.height + 10
    d.text((x + 12, y), "%s  %s" % (tag, name), fill=(240, 246, 255))
    d.text((x + 12, y + 18), desc, fill=(150, 165, 195))
    d.text((x + 12, y + 36),
           "白%.0f%%  金%.0f%%  青%.0f%%  深%.0f%%"
           % (st["white"] * 100, st["gold"] * 100, st["cyan"] * 100, st["dark"] * 100),
           fill=(110, 125, 155))

sheet.save(OUT)
print("\nwrote", OUT, sheet.size)
