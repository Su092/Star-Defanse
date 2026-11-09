"""C 系（军规克制）变体对比图：6 个方向横向排列 + 量化配色。"""
import os

import numpy as np
from PIL import Image, ImageDraw

SRC = "UiStyleTests"
OUT = os.path.join(SRC, "_compare-c.png")

VERSIONS = [
    ("ui-c-milspec", "C0", "原始（暖米白）", "色相偏暖，与塔不搭"),
    ("ui-c1-coldwhite", "C1", "冷白平板", "修正色相，最纯的克制"),
    ("ui-c2-graphite", "C2", "石墨深色", "深底高字对比"),
    ("ui-c3-hairline-gold", "C3", "金发丝边", "只留一道金线做质感"),
    ("ui-c4-twotone", "C4", "硬双色分界", "沿用赛璐璐两段式"),
    ("ui-c5-frameless", "C5", "无边框", "只靠形状与间距分层"),
    ("ui-c6-etched", "C6", "刻线细节", "雕刻纹理，不空也不吵"),
]


def stats(p):
    a = np.asarray(Image.open(p).convert("RGB")).astype(np.int16)
    r, g, b = a[:, :, 0], a[:, :, 1], a[:, :, 2]
    mx, mn = a.max(axis=2), a.min(axis=2)
    total = a.shape[0] * a.shape[1]
    white = ((mn > 175) & (abs(r - b) < 22)).sum() / total
    warm = ((mn > 150) & (r > b + 18)).sum() / total      # 暖白（米白）
    gold = ((r > 120) & (r > b + 40) & (g > b + 10) & (mx > 140)).sum() / total
    cyan = ((b > 130) & (g > 110) & (g > r + 45) & (b > r + 45)).sum() / total
    return dict(white=white, warm=warm, gold=gold, cyan=cyan)


CELL_W = 330
LABEL_H = 96

tiles = []
for key, tag, name, desc in VERSIONS:
    p = os.path.join(SRC, key + ".png")
    im = Image.open(p).convert("RGB")
    st = stats(p)
    im.thumbnail((CELL_W - 12, 660), Image.LANCZOS)
    tiles.append((im, tag, name, desc, st))
    print("%-22s 冷白%4.1f%% 暖白%4.1f%% 金%4.1f%% 青%4.1f%%"
          % (tag, st["white"] * 100, st["warm"] * 100,
             st["gold"] * 100, st["cyan"] * 100))

cols = 4
rows = (len(tiles) + cols - 1) // cols
cell_h = max(t[0].height for t in tiles) + LABEL_H
W = CELL_W * cols
H = cell_h * rows + 16
sheet = Image.new("RGB", (W, H), (15, 13, 24))
d = ImageDraw.Draw(sheet)

for i, (im, tag, name, desc, st) in enumerate(tiles):
    cx = (i % cols) * CELL_W
    cy = (i // cols) * cell_h
    sheet.paste(im, (cx + (CELL_W - im.width) // 2, cy + 10))
    y = cy + 10 + im.height + 8
    d.text((cx + 10, y), "%s  %s" % (tag, name), fill=(240, 246, 255))
    d.text((cx + 10, y + 17), desc, fill=(150, 165, 195))
    d.text((cx + 10, y + 34),
           "冷白%.0f%%  暖白%.0f%%  金%.1f%%  青%.1f%%"
           % (st["white"] * 100, st["warm"] * 100,
              st["gold"] * 100, st["cyan"] * 100),
           fill=(110, 125, 155))

sheet.save(OUT)
print("\nwrote", OUT, sheet.size)
