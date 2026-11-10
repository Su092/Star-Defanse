"""生成「薄边框信息牌」plate-thin.png。

为什么需要新素材：
  现有 frame-plate 的装饰带（切角 + 45°玻璃反光 + 端头凹槽）实测占短边
  一半以上。九宫格四角是 1:1 绘制的，塞进 72px 高的 HUD 信息牌时，
  要么四角之和超过高度被 Unity 强行压缩（整块压扁），
  要么边距吃掉一半内容区（文字压在花纹上）。两者都不可接受。

  这张素材按「窄条」重新构图：画布 512×160，装饰带控制在 32px（20%），
  内容区 448×96 足够放 30px 字。风格与其余素材一致：
  45° 切角、厚黑描边、青色内线、左上 45° 玻璃反光、右端小凹槽。

用法：由 tools/ui-redesign.json 之外单独生成，导入规则见 STAssetPostprocessor。
"""
import json
import os

from PIL import Image, ImageDraw

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "Resources", "Art", "UI", "plate-thin.png")

W, H = 512, 160

# 调色板（与枢纽文档 docs/art-style.md 一致）
INK = (7, 4, 15, 255)          # 厚黑描边
BODY = (20, 16, 51, 255)       # 深紫底色
BODY_HI = (28, 23, 66, 255)    # 上半略亮（赛璐璐平涂分界）
CYAN = (79, 243, 255, 255)
CYAN_DIM = (27, 124, 154, 255)
GLASS = (255, 255, 255, 210)

CHAMFER = 18     # 45° 切角
STROKE = 5       # 黑描边
INNER = 3        # 青色内线


def chamfered_polygon(x0, y0, x1, y1, c):
    return [
        (x0 + c, y0), (x1 - c, y0), (x1, y0 + c),
        (x1, y1 - c), (x1 - c, y1), (x0 + c, y1),
        (x0, y1 - c), (x0, y0 + c),
    ]


img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
d = ImageDraw.Draw(img)

# 1) 外圈厚黑描边（最外层多边形）
d.polygon(chamfered_polygon(0, 0, W - 1, H - 1, CHAMFER), fill=INK)

# 2) 底色（内缩 STROKE）
b0, b1 = STROKE, (W - 1 - STROKE, H - 1 - STROKE)
d.polygon(chamfered_polygon(b0, b0, b1[0], b1[1], CHAMFER - STROKE), fill=BODY)

# 3) 上半略亮：赛璐璐平涂的硬边分界（放在内容区中部偏上，拉伸时不会被抹开）
d.polygon(chamfered_polygon(b0, b0, b1[0], b0 + int((H - 2 * STROKE) * 0.42),
                            CHAMFER - STROKE), fill=BODY_HI)

# 4) 青色内线
i0 = STROKE + 3
i1 = (W - 1 - i0, H - 1 - i0)
d.line(chamfered_polygon(i0, i0, i1[0], i1[1], CHAMFER - STROKE - 3) +
       [chamfered_polygon(i0, i0, i1[0], i1[1], CHAMFER - STROKE - 3)[0]],
       fill=CYAN, width=INNER, joint="curve")

# 5) 左上 45° 玻璃反光：一条斜带，宽度控制在装饰带内
gl_x, gl_y = STROKE + 4, STROKE + 4
band = []
for k in range(0, 30):
    band.append((gl_x + k, gl_y + 26 - k))
for k in range(29, -1, -1):
    band.append((gl_x + k + 9, gl_y + 26 - k))
# 用多边形裁出斜向高光（限制在左侧装饰带内）
mask = Image.new("L", (W, H), 0)
md = ImageDraw.Draw(mask)
md.polygon(band, fill=255)
# 只保留左侧 34px、顶部 34px 以内的部分
clip = Image.new("L", (W, H), 0)
ImageDraw.Draw(clip).rectangle([0, 0, 34, 34], fill=255)
from PIL import ImageChops
mask = ImageChops.multiply(mask, clip)
gl = Image.new("RGBA", (W, H), GLASS)
gl.putalpha(mask.point(lambda v: int(v * 210 / 255)))
img.alpha_composite(gl)

# 6) 右端机械凹槽（小 V，位于右侧装饰带内）
notch_cx = W - 1 - 22
d.polygon([(notch_cx - 7, H // 2), (notch_cx + 5, H // 2 - 13), (notch_cx + 5, H // 2 + 13)],
          fill=INK)
d.line([(notch_cx - 7, H // 2), (notch_cx + 4, H // 2 - 12)], fill=CYAN_DIM, width=2)
d.line([(notch_cx - 7, H // 2), (notch_cx + 4, H // 2 + 12)], fill=CYAN_DIM, width=2)

# 7) 上下边缘高光线（细，增强立体）
d.line([(CHAMFER + 6, STROKE + 2), (W - CHAMFER - 6, STROKE + 2)],
       fill=(79, 243, 255, 150), width=1)
d.line([(CHAMFER + 6, H - STROKE - 3), (W - CHAMFER - 6, H - STROKE - 3)],
       fill=(11, 40, 70, 200), width=1)

os.makedirs(os.path.dirname(OUT), exist_ok=True)
img.save(OUT)
print(f"wrote {OUT}  {W}x{H}")

# 同时给出一张对位图，确认 32px 边距能包住装饰
border = 32
vis = img.copy()
vd = ImageDraw.Draw(vis)
vd.rectangle([border, border, W - 1 - border, H - 1 - border],
             outline=(80, 255, 120, 255), width=2)
vd.rectangle([0, 0, border, border], outline=(255, 60, 220, 255), width=2)
vd.rectangle([W - border, 0, W - 1, border], outline=(255, 60, 220, 255), width=2)
vd.rectangle([0, H - border, border, H - 1], outline=(255, 60, 220, 255), width=2)
vd.rectangle([W - border, H - border, W - 1, H - 1], outline=(255, 60, 220, 255), width=2)
bg = Image.new("RGBA", (W, H), (24, 24, 30, 255))
bg.alpha_composite(vis)
bg.convert("RGB").resize((W * 2, H * 2), Image.LANCZOS).save(
    os.path.join(ROOT, "Shots", "_plate_thin.png"))
print("wrote Shots/_plate_thin.png  border=32, content="
      f"{W - 2 * border}x{H - 2 * border}")
