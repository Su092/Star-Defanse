"""生成关卡卡片缩略图（960x640，3:2）：星球地形 + 路线示意图。

路线同样由代码按 sldata.json 的真实 path 绘制，保证与战场一致。
卡片上路线用细线 + 端点圆点，做成"任务简报"式的俯视标记，
不用战场那种粗发光车道（缩略图尺寸下会糊成一团）。
"""
import json
import math
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter

ROOT = Path(__file__).resolve().parent.parent
DATA = ROOT / "Assets" / "Resources" / "Data" / "sldata.json"
TERRAIN = ROOT / "tools" / "_terrain"
OUT = ROOT / "Assets" / "Resources" / "Art" / "Backgrounds"

TW, TH = 960, 640

ACCENT = {
    "starport": (79, 243, 255),
    "belt": (167, 139, 255),
    "nexus": (255, 107, 214),
    "crater": (120, 255, 190),
}

SRC = {
    "starport": ("thumb-1.png", "level-starport.jpg"),
    "belt": ("thumb-2.png", "level-belt.jpg"),
    "nexus": ("thumb-3.png", "level-nexus.jpg"),
    "crater": ("thumb-4.png", "level-crater.jpg"),
}


def cover(img, w, h):
    sw, sh = img.size
    scale = max(w / sw, h / sh)
    nw, nh = int(sw * scale + 0.5), int(sh * scale + 0.5)
    img = img.resize((nw, nh), Image.LANCZOS)
    x = (nw - w) // 2
    y = (nh - h) // 2
    return img.crop((x, y, x + w, y + h))


def paint_route(img: Image.Image, pts, accent) -> Image.Image:
    """把 1080x1920 的设计坐标路线缩放进 960x640 卡片。"""
    # 取路线包围盒，等比缩进卡片内留边
    xs = [p[0] for p in pts]
    ys = [p[1] for p in pts]
    minx, maxx = min(xs), max(xs)
    miny, maxy = min(ys), max(ys)
    spanx = max(1.0, maxx - minx)
    spany = max(1.0, maxy - miny)

    pad = 56
    scale = min((TW - pad * 2) / spanx, (TH - pad * 2) / spany)
    # 居中放置
    ox = (TW - spanx * scale) / 2 - minx * scale
    oy = (TH - spany * scale) / 2 - miny * scale

    line = [(x * scale + ox, y * scale + oy) for x, y in pts]

    out = img.convert("RGB")

    # 柔和投影，让路线从地形上"浮"起来
    sh = Image.new("L", (TW, TH), 0)
    ImageDraw.Draw(sh).line(line, fill=160, width=16, joint="curve")
    sh = sh.filter(ImageFilter.GaussianBlur(6))
    shadow_rgb = Image.new("RGB", (TW, TH), (6, 4, 14))
    out = Image.composite(Image.blend(out, shadow_rgb, 0.75), out, sh)

    ov = Image.new("RGBA", (TW, TH), (0, 0, 0, 0))
    d = ImageDraw.Draw(ov)
    d.line(line, fill=(10, 8, 22, 235), width=15, joint="curve")
    d.line(line, fill=accent + (255,), width=9, joint="curve")
    d.line(line, fill=(236, 250, 255, 235), width=3, joint="curve")

    # 起点 / 终点标记
    r = 13
    sx, sy = line[0]
    ex, ey = line[-1]
    d.ellipse([sx - r, sy - r, sx + r, sy + r], fill=(10, 8, 22, 255),
              outline=accent + (255,), width=4)
    r2 = 15
    d.ellipse([ex - r2, ey - r2, ex + r2, ey + r2], fill=(10, 8, 22, 255),
              outline=(255, 209, 102, 255), width=5)

    out = Image.alpha_composite(out.convert("RGBA"), ov)
    return out.convert("RGB")


def build(only=None):
    data = json.loads(DATA.read_text(encoding="utf-8"))
    for lv in data["levels"]:
        lid = lv["id"]
        if only and lid != only:
            continue
        if lid not in SRC:
            print("  skip", lid)
            continue
        src_name, out_name = SRC[lid]
        p = TERRAIN / src_name
        if not p.exists():
            print("  missing", p)
            continue

        base = cover(Image.open(p).convert("RGB"), TW, TH)
        pts = [(float(q["x"]), float(q["y"])) for q in lv["path"]]
        img = paint_route(base, pts, ACCENT[lid])

        out = OUT / out_name
        img.save(out, "JPEG", quality=88, optimize=True, progressive=True)
        print(f"  {lid:9s} -> {out.name} {out.stat().st_size/1024:.0f}KB")


if __name__ == "__main__":
    build(sys.argv[1] if len(sys.argv) > 1 else None)
    print("done")
