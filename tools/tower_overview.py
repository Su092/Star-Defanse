"""生成总览图：8 座塔 × 4 阶段，排成网格便于一次性核对。"""
import os

from PIL import Image, ImageDraw

SRC = os.path.join("TowerDesigns", "Stages")
OUT = os.path.join("TowerDesigns", "_overview.png")

ROWS = ["ran-ion", "zero-crimson", "vega-falcon", "sera-halo",
        "grom-hammer", "chaos-starburst", "ella-prism", "nova-swarm"]
LABELS = ["岚 RAN", "零式 ZERO", "维加 VEGA", "赛拉 SERA",
          "格洛姆 GROM", "卡俄斯 CHAOS", "艾拉 ELLA", "诺娃 NOVA"]

CELL_W, CELL_H = 240, 300
LABEL_W = 150
PAD = 12

W = LABEL_W + CELL_W * 4 + PAD * 2
H = CELL_H * len(ROWS) + PAD * 2 + 30

sheet = Image.new("RGB", (W, H), (18, 16, 32))
d = ImageDraw.Draw(sheet)

d.text((PAD + LABEL_W, 8), "Lv1            Lv2            Lv3            Lv4",
       fill=(150, 200, 230))

for r, (key, label) in enumerate(zip(ROWS, LABELS)):
    y0 = PAD + 30 + r * CELL_H
    d.text((PAD, y0 + CELL_H // 2 - 6), label, fill=(220, 230, 250))
    for c in range(4):
        p = os.path.join(SRC, "tower-%s-lv%d.png" % (key, c + 1))
        if not os.path.exists(p):
            continue
        im = Image.open(p).convert("RGBA")
        im.thumbnail((CELL_W - 10, CELL_H - 10), Image.LANCZOS)
        x = PAD + LABEL_W + c * CELL_W + (CELL_W - im.width) // 2
        y = y0 + (CELL_H - im.height) // 2
        sheet.paste(im, (x, y), im)
    d.line([(0, y0), (W, y0)], fill=(45, 40, 70), width=1)

sheet.save(OUT)
print("wrote", OUT, sheet.size)
