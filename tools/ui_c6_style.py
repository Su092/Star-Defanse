"""C6 风格 UI 素材生成 —— 按功能区分材质。

为什么程序化绘制而不是生图：
  九宫格素材要求四边严格对称、边距精确落在「不可拉伸区」边界上。
  AI 生成的边框左右厚度必然不一致，切片后拉伸会变形（第一轮已踩过）。
  C6 的语言（刻线、点阵、短斜线、指示条）本身就是几何图形，
  代码画得比 AI 准，而且改参数就能重出。

功能分工（这是本脚本的核心设计）：
  容器   panel / card / panel-gold   白甲厚框 + 深色内腔。内腔要放浅色文字，
                                      所以不能做全白；白框负责承载塔的材质语言。
  信息牌 plate-thin / frame-plate    白甲中框 + 左侧青色指示条。比容器薄一档，
                                      区分「容器」与「读数」的层级。
  按钮   button-primary / button-gold 白甲框 + 底部青色能量槽。能量槽是「可按」的
                                      视觉承诺，容器没有这一条。
  圆形   frame-round                 深色圆心 + 白甲外环。圆按钮里叠的是白色图标
                                      （RoundButton 用 Color.white 画图标），
                                      圆心做白会让图标直接消失。
  血条   bar                         深槽 + 细白边，不用金。槽上叠彩色填充，
                                      边框越安静越好。

运行：python tools/ui_c6_style.py
产物直接覆盖 Assets/Resources/Art/UI/ 下的同名文件（原文件备份到 tools/_ui_backup/）。
"""
import os
import shutil

from PIL import Image, ImageDraw

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
UI = os.path.join(ROOT, "Assets", "Resources", "Art", "UI")
BAK = os.path.join(ROOT, "tools", "_ui_backup")

SS = 4          # 超采样倍数（先画大再缩小，保证边缘平滑）

# ---------------------------------------------------------------- 调色板
# 与防御塔一致：冷白硬表面 + 炭黑描边 + 金色倒角 + 青色能量
HULL_HI = (250, 252, 255, 255)
HULL = (232, 237, 244, 255)
HULL_DK = (192, 200, 214, 255)
INK = (7, 4, 15, 255)
CAVITY = (18, 24, 42, 255)
CAVITY_DK = (10, 14, 26, 255)
GOLD = (211, 165, 76, 255)
GOLD_HI = (243, 205, 128, 255)
CYAN = (79, 243, 255, 255)
CYAN_DIM = (38, 142, 168, 255)


def chamfer(x0, y0, x1, y1, c):
    """45° 切角矩形的顶点序列。"""
    c = max(0, min(c, (x1 - x0) // 2, (y1 - y0) // 2))
    return [
        (x0 + c, y0), (x1 - c, y0), (x1, y0 + c),
        (x1, y1 - c), (x1 - c, y1), (x0 + c, y1),
        (x0, y1 - c), (x0, y0 + c),
    ]


def new(w, h):
    return Image.new("RGBA", (w * SS, h * SS), (0, 0, 0, 0))


def save(im, name):
    os.makedirs(BAK, exist_ok=True)
    src = os.path.join(UI, name + ".png")
    bak = os.path.join(BAK, name + ".png")
    if os.path.exists(src) and not os.path.exists(bak):
        shutil.copy2(src, bak)
    im.resize((im.width // SS, im.height // SS), Image.LANCZOS).save(src)
    return src


# ---------------------------------------------------------------- 通用底板
def plate(w, h, cut, stroke, band, cavity, *, cavity_split=False):
    """画「黑描边 + 白甲带 + 深腔」三层，返回 (img, draw, 内腔矩形)。"""
    im = new(w, h)
    d = ImageDraw.Draw(im)
    W, H = w * SS, h * SS
    s, b, c = stroke * SS, band * SS, cut * SS

    d.polygon(chamfer(0, 0, W - 1, H - 1, c), fill=INK)
    d.polygon(chamfer(s, s, W - 1 - s, H - 1 - s, c - s), fill=HULL)

    # 白甲带内缘压一条暗线，形成赛璐璐两段感
    d.line(chamfer(s, s, W - 1 - s, H - 1 - s, c - s) +
           [chamfer(s, s, W - 1 - s, H - 1 - s, c - s)[0]],
           fill=HULL_DK, width=max(1, s // 3), joint="curve")

    i0 = s + b
    i1x, i1y = W - 1 - s - b, H - 1 - s - b
    ic = max(0, c - s - b)
    d.polygon(chamfer(i0, i0, i1x, i1y, ic), fill=cavity)

    if cavity_split:
        mid = i0 + (i1y - i0) * 42 // 100
        d.rectangle([i0, mid, i1x, i1y], fill=CAVITY_DK)

    return im, d, (i0, i0, i1x, i1y)


def gold_corner(d, x, y, size, flip_x=False, flip_y=False):
    """一角金标：L 形金条，画在角块内所以拉伸不变形。"""
    sx = -1 if flip_x else 1
    sy = -1 if flip_y else 1
    lw = max(2, size // 4)
    x2 = x + sx * size
    y2 = y + sy * size
    d.line([(x, y), (x2, y)], fill=GOLD, width=lw)
    d.line([(x, y), (x, y2)], fill=GOLD, width=lw)
    d.line([(x + sx * lw, y + sy * lw), (x2, y + sy * lw)], fill=GOLD_HI, width=1)


def tick_dots(d, x, y, cols, rows, step, color=CYAN_DIM):
    """点阵。只能出现在角块内，否则横向拉伸会被拉开。"""
    r = max(1, step // 6)
    for i in range(cols):
        for j in range(rows):
            cx, cy = x + i * step, y + j * step
            d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=color)


def hatches(d, x, y, n, step, length, color=HULL_DK):
    """短斜线（铭牌刻痕），只用于角块。"""
    for i in range(n):
        ox = x + i * step
        d.line([(ox, y), (ox + length, y - length)], fill=color, width=2)


# ---------------------------------------------------------------- 各功能素材
def make_panel(w, h, gold=False):
    """容器：白甲厚框 + 深腔。金版把白带的暗线换成金线并加金角标。"""
    cut = max(6, int(min(w, h) * 0.055))
    stroke = max(3, int(min(w, h) * 0.022))
    band = max(8, int(min(w, h) * 0.22))
    im, d, box = plate(w, h, cut, stroke, band, CAVITY, cavity_split=True)
    W, H = w * SS, h * SS

    edge = GOLD if gold else HULL_DK
    d.line(chamfer(0, 0, W - 1, H - 1, cut * SS) +
           [chamfer(0, 0, W - 1, H - 1, cut * SS)[0]],
           fill=edge, width=max(2, int(stroke * SS * 0.7)), joint="curve")

    if gold:
        cs = int(band * SS * 0.8)
        m = int(stroke * SS) + int(band * SS) // 3
        gold_corner(d, m, m, cs)
        gold_corner(d, W - 1 - m, m, cs, flip_x=True)
        gold_corner(d, m, H - 1 - m, cs, flip_y=True)
        gold_corner(d, W - 1 - m, H - 1 - m, cs, flip_x=True, flip_y=True)
    else:
        # 四角点阵（位于角块内）
        m = int(stroke * SS) + int(band * SS) // 3
        st = max(3, int(band * SS) // 6)
        tick_dots(d, m, m, 3, 2, st)
        tick_dots(d, W - 1 - m - 2 * st, m, 3, 2, st)
        tick_dots(d, m, H - 1 - m - st, 3, 2, st)

    # 内腔上缘一条青色细线（沿 x 恒定，拉伸安全）
    cx0, cy0, cx1, cy1 = box
    d.line([(cx0 + 2, cy0 + 2), (cx1 - 2, cy0 + 2)], fill=CYAN, width=max(1, SS))

    return save(im, "panel-gold" if gold else "panel")


def make_plate(w, h, gold=False):
    """信息牌：白甲中框 + 左侧青色指示条。比容器薄一档。"""
    short = min(w, h)
    cut = max(4, int(short * 0.06))
    stroke = max(3, int(short * 0.024))
    band = max(6, int(short * 0.22))
    im, d, box = plate(w, h, cut, stroke, band, CAVITY, cavity_split=False)
    W, H = w * SS, h * SS

    edge = GOLD if gold else HULL_DK
    d.line(chamfer(0, 0, W - 1, H - 1, cut * SS) +
           [chamfer(0, 0, W - 1, H - 1, cut * SS)[0]],
           fill=edge, width=max(2, int(stroke * SS * 0.7)), joint="curve")

    cx0, cy0, cx1, cy1 = box
    # 左侧竖向青色指示条（沿 y 恒定，纵向拉伸安全）
    bar_w = max(2, int(band * SS * 0.22))
    d.rectangle([cx0 + 2, cy0 + int(band * SS * 0.25),
                 cx0 + 2 + bar_w, cy1 - int(band * SS * 0.25)],
                fill=CYAN)

    if gold:
        cs = int(band * SS * 0.7)
        m = int(stroke * SS) + int(band * SS) // 3
        gold_corner(d, W - 1 - m, m, cs, flip_x=True)
        gold_corner(d, W - 1 - m, H - 1 - m, cs, flip_x=True, flip_y=True)
    else:
        st = max(3, int(band * SS) // 7)
        m = int(stroke * SS) + int(band * SS) // 3
        tick_dots(d, W - 1 - m - 2 * st, m, 3, 1, st)
        hatches(d, m, H - 1 - m - st, 3, st * 2, st)

    return save(im, "frame-plate" if not gold else "frame-plate-gold")


def make_plate_thin(w, h):
    """窄条信息牌：白甲薄框 + 左侧青条。装饰更少，因为显示尺寸小。"""
    short = min(w, h)
    cut = max(3, int(short * 0.055))
    stroke = max(2, int(short * 0.022))
    band = max(5, int(short * 0.20))
    im, d, box = plate(w, h, cut, stroke, band, CAVITY)
    W, H = w * SS, h * SS

    cx0, cy0, cx1, cy1 = box
    bar_w = max(2, int(band * SS * 0.28))
    d.rectangle([cx0 + 2, cy0 + int(band * SS * 0.2),
                 cx0 + 2 + bar_w, cy1 - int(band * SS * 0.2)], fill=CYAN)

    st = max(3, int(band * SS) // 6)
    m = int(stroke * SS) + int(band * SS) // 3
    tick_dots(d, W - 1 - m - 2 * st, m, 3, 1, st)
    return save(im, "plate-thin")


def make_button(w, h, gold=False):
    """按钮：白甲框 + 底部青色能量槽。能量槽是「可按」的视觉承诺。"""
    short = min(w, h)
    cut = max(4, int(short * 0.07))
    stroke = max(3, int(short * 0.026))
    band = max(6, int(short * 0.22))
    im, d, box = plate(w, h, cut, stroke, band, CAVITY, cavity_split=True)
    W, H = w * SS, h * SS

    edge = GOLD if gold else HULL_DK
    d.line(chamfer(0, 0, W - 1, H - 1, cut * SS) +
           [chamfer(0, 0, W - 1, H - 1, cut * SS)[0]],
           fill=edge, width=max(2, int(stroke * SS * 0.7)), joint="curve")

    cx0, cy0, cx1, cy1 = box
    # 底部能量槽：一条通长青条 + 两端收口（沿 x 恒定，横向拉伸安全）
    groove_h = max(2, int(band * SS * 0.20))
    gy = cy1 - int(band * SS * 0.28)
    d.rectangle([cx0 + 2, gy, cx1 - 2, gy + groove_h], fill=CYAN_DIM)
    d.rectangle([cx0 + 2, gy, cx1 - 2, gy + max(1, groove_h // 2)], fill=CYAN)

    if gold:
        cs = int(band * SS * 0.65)
        m = int(stroke * SS) + int(band * SS) // 3
        gold_corner(d, m, m, cs)
        gold_corner(d, W - 1 - m, m, cs, flip_x=True)
        gold_corner(d, m, H - 1 - m, cs, flip_y=True)
        gold_corner(d, W - 1 - m, H - 1 - m, cs, flip_x=True, flip_y=True)
    else:
        st = max(3, int(band * SS) // 6)
        m = int(stroke * SS) + int(band * SS) // 3
        hatches(d, m, m + st, 3, st * 2, st)

    return save(im, "button-gold" if gold else "button-primary")


def make_card(w, h):
    """竖幅卡片：容器语言，但金角标放底部（与干员卡的角标位置呼应）。"""
    cut = max(6, int(min(w, h) * 0.055))
    stroke = max(3, int(min(w, h) * 0.022))
    band = max(8, int(min(w, h) * 0.20))
    im, d, box = plate(w, h, cut, stroke, band, CAVITY, cavity_split=True)
    W, H = w * SS, h * SS

    d.line(chamfer(0, 0, W - 1, H - 1, cut * SS) +
           [chamfer(0, 0, W - 1, H - 1, cut * SS)[0]],
           fill=HULL_DK, width=max(2, int(stroke * SS * 0.7)), joint="curve")

    cx0, cy0, cx1, cy1 = box
    d.line([(cx0 + 2, cy0 + 2), (cx1 - 2, cy0 + 2)], fill=CYAN, width=max(1, SS))

    st = max(3, int(band * SS) // 6)
    m = int(stroke * SS) + int(band * SS) // 3
    tick_dots(d, m, m, 3, 2, st)
    tick_dots(d, W - 1 - m - 2 * st, m, 3, 2, st)
    return save(im, "card")


def make_round(dia=512):
    """圆形按钮：深色圆心 + 白甲外环 + 青色弧。

    圆心必须深色：RoundButton 里图标用 Color.white 绘制，
    白圆心会让白色图标完全看不见（这是硬约束，不是审美选择）。
    """
    im = new(dia, dia)
    d = ImageDraw.Draw(im)
    D = dia * SS
    c = D // 2
    ring = int(D * 0.11)
    stroke = max(3, int(D * 0.016))

    d.ellipse([0, 0, D - 1, D - 1], fill=INK)
    d.ellipse([stroke, stroke, D - 1 - stroke, D - 1 - stroke], fill=HULL)
    r_in = c - stroke - ring
    d.ellipse([c - r_in, c - r_in, c + r_in, c + r_in], fill=CAVITY)

    # 外环内侧暗线（两段感）
    d.arc([stroke + ring // 2, stroke + ring // 2,
           D - 1 - stroke - ring // 2, D - 1 - stroke - ring // 2],
          start=0, end=360, fill=HULL_DK, width=max(2, ring // 5))

    # 青色能量弧（左上 90°）
    rr = c - stroke - ring // 2
    d.arc([c - rr, c - rr, c + rr, c + rr], start=190, end=280,
          fill=CYAN, width=max(3, ring // 3))

    # 环上的金色铆点（等分，圆周对称所以旋转/缩放都不变形）
    for k in range(8):
        import math
        ang = math.radians(k * 45 + 22.5)
        pr = c - stroke - ring // 2
        px = c + pr * math.cos(ang)
        py = c + pr * math.sin(ang)
        rr2 = max(2, D // 90)
        d.ellipse([px - rr2, py - rr2, px + rr2, py + rr2], fill=GOLD)

    return save(im, "frame-round")


def make_bar(w, h):
    """血条底槽：深槽 + 细白边，无金。

    槽上会叠彩色填充（青/橙/红），边框越安静，填充越清楚。
    """
    short = h
    cut = max(2, int(short * 0.18))
    stroke = max(2, int(short * 0.14))
    im = new(w, h)
    d = ImageDraw.Draw(im)
    W, H = w * SS, h * SS
    s, c = stroke * SS, cut * SS

    d.polygon(chamfer(0, 0, W - 1, H - 1, c), fill=INK)
    d.polygon(chamfer(s, s, W - 1 - s, H - 1 - s, max(0, c - s)), fill=HULL)
    i = s + max(2, short * SS // 10)
    d.polygon(chamfer(i, i, W - 1 - i, H - 1 - i, max(0, c - i)), fill=CAVITY)

    return save(im, "bar")


def main():
    os.makedirs(BAK, exist_ok=True)
    made = []
    # 容器
    made.append(make_panel(560, 340))
    made.append(make_panel(620, 430, gold=True))
    made.append(make_card(430, 585))
    # 信息牌
    made.append(make_plate(560, 175))
    made.append(make_plate_thin(520, 160))
    # 按钮
    made.append(make_button(600, 160))
    made.append(make_button(600, 200, gold=True))
    # 圆形 / 血条 / 胶囊
    made.append(make_round(512))
    made.append(make_bar(540, 84))

    # chip 是代码画的（STUi.Chip 用 RoundSprite），但仍生成一张备用
    im, d, box = plate(320, 96, 10, 3, 18, CAVITY)
    made.append(save(im, "chip"))

    for p in made:
        print(os.path.basename(p))


if __name__ == "__main__":
    main()
