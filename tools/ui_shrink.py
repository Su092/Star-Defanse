"""把新生成的 UI 素材缩到运行时尺寸并压紧体积。

美术按 1254x1254 生成（质量好、细节足），但运行时并不需要这么大：
UI 九宫格实际显示区域大多在 100-900 设计像素，512 已足够，
能显著降低内存与包体。图标类保留 256（HUD 上最大也就 80px 显示）。
"""
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
UI = ROOT / "Assets" / "Resources" / "Art" / "UI"

# 九宫格：短边 512
FRAMES = ["panel", "panel-gold", "button-primary", "button-gold",
          "chip", "bar", "card", "frame-plate", "frame-round"]

# 图标：短边 256
ICONS = ["icon-star", "icon-coin", "icon-crystal", "icon-pause",
         "icon-speed", "icon-help", "icon-arrow", "icon-book"]


def shrink(name: str, target: int) -> None:
    p = UI / f"{name}.png"
    if not p.exists():
        print("  missing", name)
        return
    before = p.stat().st_size
    im = Image.open(p).convert("RGBA")

    # 裁掉四周全透明的多余留白，让九宫格边距计算更准
    bbox = im.getbbox()
    if bbox:
        im = im.crop(bbox)

    w, h = im.size
    scale = target / min(w, h)
    if scale < 1.0:
        im = im.resize((max(1, round(w * scale)), max(1, round(h * scale))),
                       Image.LANCZOS)

    im.save(p, "PNG", optimize=True)
    print(f"  {name:16s} {w}x{h} -> {im.width}x{im.height}  "
          f"{before/1024:7.0f}KB -> {p.stat().st_size/1024:6.0f}KB")


print("frames:")
for n in FRAMES:
    shrink(n, 512)
print("icons:")
for n in ICONS:
    shrink(n, 256)
