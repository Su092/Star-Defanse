"""把游戏真实 path 画成发光车道，叠加到星球地形底图上。

为什么这样做：让 AI 在贴图里"顺手画一条路"必然和游戏逻辑路径对不上
（环形路口会被画成捷径、拐角位置有偏差）。改成——
  地形由 AI 生成（只出地表，不许画路）
  车道由本脚本按 sldata.json 里的 path 精确绘制
这样画面路线与逻辑路线**永远一致**，换地形也不会错位。

用法：
    python tools/paint_lane.py            # 生成全部 4 张
    python tools/paint_lane.py 1          # 只做第 1 关
"""
import json
import math
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

ROOT = Path(__file__).resolve().parent.parent
DATA = ROOT / "Assets" / "Resources" / "Data" / "sldata.json"
TERRAIN = ROOT / "tools" / "_terrain"
OUT_DIR = ROOT / "Assets" / "Resources" / "Art" / "Backgrounds"

W, H = 1080, 1920

# 车道视觉参数（设计像素）
LANE_W = 66          # 路面宽度
EDGE_W = 7           # 发光边线宽度

# 每关的强调色（与关卡缩略图一致）
ACCENT = {
    "starport": (79, 243, 255),
    "belt": (167, 139, 255),
    "nexus": (255, 107, 214),
    "crater": (120, 255, 190),
}

# 关卡 → 地形底图 / 输出文件名
MAPFILE = {
    "starport": ("terrain-1.png", "level-map-1.jpg"),
    "belt": ("terrain-2.png", "level-map-2.jpg"),
    "nexus": ("terrain-3.png", "map.jpg"),
    "crater": ("terrain-4.png", "level-map-3.jpg"),
}


def load_path(level):
    """从 sldata.json 取该关的路径点（已经是设计像素坐标）。

    把首尾各向外延长一小段，让车道在画面边缘干净地"进出画"，
    否则圆头端点会在边缘留下半圆缺口。
    """
    pts = [(float(p["x"]), float(p["y"])) for p in level["path"]]
    if len(pts) >= 2:
        ext = 22.0
        (x0, y0), (x1, y1) = pts[0], pts[1]
        d = math.hypot(x1 - x0, y1 - y0) or 1.0
        pts.insert(0, (x0 - (x1 - x0) / d * ext, y0 - (y1 - y0) / d * ext))

        (xa, ya), (xb, yb) = pts[-2], pts[-1]
        d2 = math.hypot(xb - xa, yb - ya) or 1.0
        pts.append((xb + (xb - xa) / d2 * ext, yb + (yb - ya) / d2 * ext))
    return pts


def smooth_polyline(pts, subdiv=6):
    """用 Catmull-Rom 细分，让折线拐角变成顺滑弧线（贴合车道观感）。"""
    if len(pts) < 3:
        return pts
    ext = [pts[0]] + list(pts) + [pts[-1]]
    out = []
    for i in range(1, len(ext) - 2):
        p0, p1, p2, p3 = ext[i - 1], ext[i], ext[i + 1], ext[i + 2]
        for s in range(subdiv):
            t = s / float(subdiv)
            t2, t3 = t * t, t * t * t
            x = 0.5 * ((2 * p1[0]) + (-p0[0] + p2[0]) * t +
                       (2 * p0[0] - 5 * p1[0] + 4 * p2[0] - p3[0]) * t2 +
                       (-p0[0] + 3 * p1[0] - 3 * p2[0] + p3[0]) * t3)
            y = 0.5 * ((2 * p1[1]) + (-p0[1] + p2[1]) * t +
                       (2 * p0[1] - 5 * p1[1] + 4 * p2[1] - p3[1]) * t2 +
                       (-p0[1] + 3 * p1[1] - 3 * p2[1] + p3[1]) * t3)
            out.append((x, y))
    out.append(pts[-1])
    return out


def paint(base: Image.Image, pts, accent, scale=1.0) -> Image.Image:
    """在底图上画发光车道（赛璐璐风格：厚墨线 + 平涂 + 硬边）。"""
    img = base.convert("RGB").resize((W, H), Image.LANCZOS)
    line = smooth_polyline(pts)
    line = [(x * scale, y * scale) for x, y in line]

    # 1) 柔和外发光晕（地面被照亮的感觉，保持克制，避免破坏赛璐璐硬边）
    glow = Image.new("L", (W, H), 0)
    gd = ImageDraw.Draw(glow)
    gd.line(line, fill=70, width=int(LANE_W * 2.0), joint="curve")
    glow = glow.filter(ImageFilter.GaussianBlur(26))
    glow_rgb = Image.new("RGB", (W, H), accent)
    img = Image.composite(Image.blend(img, glow_rgb, 0.40), img, glow)

    # 2) 路面：厚墨线外轮廓（赛璐璐的关键）
    ink = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    ImageDraw.Draw(ink).line(line, fill=(7, 4, 15, 255),
                             width=LANE_W + 12, joint="curve")
    img = Image.alpha_composite(img.convert("RGBA"), ink)

    # 3) 路面：两段平涂（上半亮、下半暗），硬边分割
    road = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    rd = ImageDraw.Draw(road)
    rd.line(line, fill=(24, 21, 50, 255), width=LANE_W, joint="curve")
    rd.line(line, fill=(34, 30, 68, 255), width=int(LANE_W * 0.44), joint="curve")
    img = Image.alpha_composite(img, road)

    # 4) 两侧发光边线（细而锐利，不是模糊光斑）
    edges = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    ed = ImageDraw.Draw(edges)
    ed.line(line, fill=accent + (255,), width=LANE_W, joint="curve")
    inner = Image.new("L", (W, H), 0)
    idraw = ImageDraw.Draw(inner)
    idraw.line(line, fill=255, width=LANE_W - EDGE_W * 2, joint="curve")
    edges.putalpha(Image.composite(Image.new("L", (W, H), 0),
                                   edges.getchannel("A"), inner))
    img = Image.alpha_composite(img, edges)

    # 5) 中心导向标记：稀疏的短横纹（能量管路节流环），避免过密产生"梯子"感
    ticks = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    td = ImageDraw.Draw(ticks)
    step = 118.0
    acc = 0.0
    for i in range(1, len(line)):
        x0, y0 = line[i - 1]
        x1, y1 = line[i]
        seg = math.hypot(x1 - x0, y1 - y0)
        if seg <= 1e-6:
            continue
        ux, uy = (x1 - x0) / seg, (y1 - y0) / seg
        nx, ny = -uy, ux
        pos = (step - (acc % step)) % step
        while pos < seg:
            cx, cy = x0 + ux * pos, y0 + uy * pos
            half = LANE_W * 0.22
            td.line([(cx - nx * half, cy - ny * half),
                     (cx + nx * half, cy + ny * half)],
                    fill=accent + (120,), width=3)
            pos += step
        acc += seg
    img = Image.alpha_composite(img, ticks)

    return img.convert("RGB")


def build(index: int | None = None):
    data = json.loads(DATA.read_text(encoding="utf-8"))
    levels = data["levels"]
    OUT_DIR.mkdir(parents=True, exist_ok=True)

    for i, lv in enumerate(levels):
        if index is not None and i != index:
            continue
        lid = lv["id"]
        if lid not in MAPFILE:
            print("  skip unknown level", lid)
            continue
        terrain_name, out_name = MAPFILE[lid]
        tpath = TERRAIN / terrain_name
        if not tpath.exists():
            print("  missing terrain", tpath)
            continue

        base = Image.open(tpath)
        pts = load_path(lv)
        img = paint(base, pts, ACCENT[lid])
        out = OUT_DIR / out_name
        img.save(out, "JPEG", quality=90, optimize=True, progressive=True)
        print(f"  {lid:9s} -> {out.name}  {out.stat().st_size/1024:.0f}KB  "
              f"pts={len(pts)}")


if __name__ == "__main__":
    idx = int(sys.argv[1]) if len(sys.argv) > 1 else None
    build(idx)
    print("done")
