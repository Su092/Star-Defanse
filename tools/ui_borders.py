"""测量 AI 生成 UI 素材的四条边框厚度，输出可直接写入 spriteBorder 的值。

关键点：Unity 的 spriteBorder 是 (left, bottom, right, top) 四个独立值，
**不要求对称**。所以 AI 生成的不等厚边框可以照用 —— 只要每一边都测准，
切片后四角尺寸就各按各的真实厚度，不会变形。

判据（沿每条边取多条扫描线，取稳健中位数）：
  外部背景 = alpha < 128
  边框带   = 不透明，且包含「黑描边 + 白甲」整段
  内腔起点 = 白甲之后第一次出现「连续较暗」的位置

边框厚度 = 从外接框边缘到内腔起点的距离，包含了黑描边和白甲，
因为这两部分都必须落在九宫格的角块里 —— 只要有一段进了可拉伸区，
拉伸时就会被抹开。
"""
import json
import os
import sys

import numpy as np
from PIL import Image

DARK = 110          # 亮度低于它视为「暗」（内腔 / 黑描边）
RUN = 4             # 连续这么多像素都是暗，才认为真的进入了内腔


def _first_inner(lum, from_start, skip_black=True):
    """在一条亮暗序列里找「内腔起点」的下标。

    skip_black=True：先跳过起始的黑描边（贴图外缘常有 1-3px 黑线）。
    返回 None 表示找不到（整条都很亮）。
    """
    n = len(lum)
    idx = range(n) if from_start else range(n - 1, -1, -1)
    order = list(idx)

    # 跳过前置黑描边
    k = 0
    if skip_black:
        while k < len(order) and lum[order[k]] < DARK:
            k += 1

    # 从白甲往里走，找第一个「连续 RUN 个暗像素」的起点
    for t in range(k, len(order) - RUN + 1):
        ok = True
        for u in range(RUN):
            if lum[order[t + u]] >= DARK:
                ok = False
                break
        if ok:
            return order[t]
    return None


def edge_profile(alpha, rgb, box, side, n_lines=9):
    """沿某条边测厚度，返回稳健中位数（px）。"""
    x0, y0, x1, y1 = box
    bw, bh = x1 - x0 + 1, y1 - y0 + 1
    vals = []

    # 采样区间：避开四角，取中段 20%~80%
    if side in ("left", "right"):
        lines = [int(y0 + bh * f) for f in np.linspace(0.22, 0.78, n_lines)]
    else:
        lines = [int(x0 + bw * f) for f in np.linspace(0.22, 0.78, n_lines)]

    for fx in lines:
        if side == "left":
            a = alpha[fx, x0:x1 + 1]; lum = rgb[fx, x0:x1 + 1].mean(axis=1)
        elif side == "right":
            a = alpha[fx, x0:x1 + 1][::-1]; lum = rgb[fx, x0:x1 + 1][::-1].mean(axis=1)
        elif side == "top":
            a = alpha[y0:y1 + 1, fx]; lum = rgb[y0:y1 + 1, fx].mean(axis=1)
        else:
            a = alpha[y0:y1 + 1, fx][::-1]; lum = rgb[y0:y1 + 1, fx][::-1].mean(axis=1)

        # 裁剪到「进入实体」之后
        i = 0
        while i < len(a) and a[i] < 128:
            i += 1
        if i >= len(a) - RUN * 2:
            continue
        seg = lum[i:]
        inner = _first_inner(seg, True)
        if inner is None:
            continue
        t = inner + i            # 厚度包含刚才跳过的透明段？不 —— 透明段在贴图外
        vals.append(inner)       # 从实体起点算的厚度

    if not vals:
        return None
    s = sorted(vals)
    if len(s) >= 5:
        s = s[1:-1]              # 去掉极值
    return float(np.median(s))


def measure(path):
    im = Image.open(path).convert("RGBA")
    a = np.asarray(im).astype(np.int16)
    alpha = a[:, :, 3]
    rgb = a[:, :, :3]

    ys, xs = np.where(alpha > 128)
    if len(ys) == 0:
        return None
    box = (int(xs.min()), int(ys.min()), int(xs.max()), int(ys.max()))
    bw, bh = box[2] - box[0] + 1, box[3] - box[1] + 1

    L = edge_profile(alpha, rgb, box, "left")
    R = edge_profile(alpha, rgb, box, "right")
    T = edge_profile(alpha, rgb, box, "top")
    B = edge_profile(alpha, rgb, box, "bottom")

    if None in (L, R, T, B):
        return None

    # 边距上限：不能吃掉内容区
    L = min(L, bw * 0.42); R = min(R, bw * 0.42)
    T = min(T, bh * 0.42); B = min(B, bh * 0.42)

    return dict(path=path, box=box, bw=bw, bh=bh,
                left=round(L, 1), bottom=round(B, 1),
                right=round(R, 1), top=round(T, 1),
                content=[round(bw - L - R, 1), round(bh - T - B, 1)])


def main():
    args = sys.argv[1:]
    if not args:
        d = os.path.join("UiStyleTests", "gen")
        args = [os.path.join(d, f) for f in sorted(os.listdir(d))
                if f.lower().endswith(".png")]

    out = {}
    for p in args:
        r = measure(p)
        name = os.path.splitext(os.path.basename(p))[0]
        if r is None:
            print("%-16s 测量失败" % name)
            continue
        out[name] = r
        print("%-16s %4dx%-4d  L%5.1f R%5.1f T%5.1f B%5.1f   内容 %.0fx%.0f"
              % (name, r["bw"], r["bh"], r["left"], r["right"],
                 r["top"], r["bottom"], r["content"][0], r["content"][1]))

    with open(os.path.join("tools", "_ui_borders.json"), "w", encoding="utf-8") as f:
        json.dump(out, f, indent=2, ensure_ascii=False)
    print("\nwrote tools/_ui_borders.json")


if __name__ == "__main__":
    main()
