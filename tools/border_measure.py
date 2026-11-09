"""测量生图 UI 边框的四边厚度一致性。

九宫格要求「同轴两侧边框等厚」，否则 Sliced 拉伸后四角大小不同，整体是歪的。

方法：不沿单一中线（中线常穿过内腔里的装饰，判据不稳），
改为在内容区的中段取多条扫描线（避开四角），
每条线测「从外边界到内腔边界」的距离，取中位数。

判据用 alpha 而不是亮度：外框和深腔都是不透明的，
只有「外部背景」是透明的，所以先用 alpha 找外接框，
再用「亮度骤降」（白框 -> 深腔）找内腔边界。
"""
import sys

import numpy as np
from PIL import Image

DROP = 55.0     # 亮度下降超过它，判为「进入内腔」


def scan_line(alpha, rgb, fixed, horizontal, lo, hi):
    """测一条线上左右（或上下）边框厚度。返回 (前侧厚度, 后侧厚度) 或 None。

    边框的实际结构是「黑描边 -> 白甲 -> 黑描边 -> 深腔」，
    所以不能只找「亮度下降」，要跳过第一条黑线，落在白甲段上再找内腔。
    """
    if horizontal:
        a = alpha[fixed, lo:hi + 1]
        lum = rgb[fixed, lo:hi + 1].mean(axis=1)
    else:
        a = alpha[lo:hi + 1, fixed]
        lum = rgb[lo:hi + 1, fixed].mean(axis=1)

    n = len(a)
    i = 0
    while i < n and a[i] < 128:
        i += 1
    if i >= n:
        return None
    start = i
    j = n - 1
    while j >= 0 and a[j] < 128:
        j -= 1
    if j <= start:
        return None
    end = j
    if end - start < 12:
        return None

    seg = lum[start:end + 1]

    def first_bright_band(s):
        """返回第一条「白甲带」的 (起点, 终点)，跳过前置黑描边。"""
        k = 0
        # 跳过起始的黑描边
        while k < len(s) and s[k] < 120:
            k += 1
        if k >= len(s):
            return None
        b0 = k
        while k < len(s) and s[k] >= 120:
            k += 1
        b1 = k - 1
        return (b0, b1) if b1 > b0 else None

    fb = first_bright_band(seg)
    if fb is None:
        return None
    band_l0, band_l1 = fb

    # 白甲之后是第二条黑描边，再往里就是内腔：找「白甲带结束」的位置
    inner_l = band_l1 + 1
    while inner_l < len(seg) and seg[inner_l] < 120:
        inner_l += 1

    # 反向同理
    rs = seg[::-1]
    rb = first_bright_band(rs)
    if rb is None:
        return None
    band_r0, band_r1 = rb
    inner_r = len(seg) - 1 - (band_r1 + 1)
    while inner_r >= 0 and seg[inner_r] < 120:
        inner_r -= 1

    if inner_r <= inner_l:
        return None

    left_t = inner_l
    right_t = len(seg) - 1 - inner_r
    return left_t, right_t


def measure(p, verbose=True, drop_ratio=0.35):
    im = Image.open(p).convert("RGBA")
    a = np.asarray(im).astype(np.int16)
    alpha = a[:, :, 3]
    rgb = a[:, :, :3]
    h, w = alpha.shape

    ys, xs = np.where(alpha > 128)
    if len(ys) == 0:
        return None
    x0, x1 = int(xs.min()), int(xs.max())
    y0, y1 = int(ys.min()), int(ys.max())
    bw, bh = x1 - x0 + 1, y1 - y0 + 1

    # 多线采样，剔除离群值（中线可能压到装饰条/按钮）
    rows = [int(y0 + bh * f) for f in (0.34, 0.40, 0.44, 0.50, 0.56, 0.60, 0.66)]
    cols = [int(x0 + bw * f) for f in (0.34, 0.40, 0.44, 0.50, 0.56, 0.60, 0.66)]

    def collect(fixed_vals, horizontal):
        L, R = [], []
        for f in fixed_vals:
            res = scan_line(alpha, rgb, f, horizontal, x0, x1) if horizontal \
                else scan_line(alpha, rgb, f, False, y0, y1)
            if res:
                L.append(res[0]); R.append(res[1])
        return L, R

    lx, rx = collect(rows, True)
    ty, by = collect(cols, False)

    if len(lx) < 2 or len(ty) < 2:
        if verbose:
            print("%s  %dx%d  无法稳定识别边框" % (p.split("\\")[-1], w, h))
        return None

    def robust(v):
        """去掉最大最小值后取中位数，抵抗少数被装饰干扰的线。"""
        s = sorted(v)
        if len(s) >= 5:
            s = s[1:-1]
        return float(np.median(s))

    L, R = robust(lx), robust(rx)
    T, B = robust(ty), robust(by)

    def pct(x, y):
        return abs(x - y) / max(1.0, (x + y) / 2) * 100

    dx, dy = pct(L, R), pct(T, B)
    ok = dx <= 12 and dy <= 12 and min(L, R, T, B) >= 6
    if verbose:
        print("%s  %dx%d  (采样 %d/%d 条线)" % (p.split("\\")[-1], w, h, len(lx), len(ty)))
        print("  左右 %5.1f / %5.1f px  差异 %5.1f%%" % (L, R, dx))
        print("  上下 %5.1f / %5.1f px  差异 %5.1f%%" % (T, B, dy))
        print("  内容区 %.0f x %.0f" % (bw - L - R, bh - T - B))
        print("  可作九宫格：%s" % ("是" if ok else "否"))
    return dict(path=p, L=L, R=R, T=T, B=B, dx=dx, dy=dy, ok=ok,
                w=w, h=h, bw=bw, bh=bh)


if __name__ == "__main__":
    args = sys.argv[1:]
    if not args:
        import os
        d = os.path.join("UiStyleTests", "gen")
        args = [os.path.join(d, f) for f in sorted(os.listdir(d))
                if f.lower().endswith(".png")]
    for p in args:
        measure(p)
        print()
