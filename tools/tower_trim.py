"""裁掉塔设计图左右两侧的非白竖条，得到干净的白底设计图。

生图偶尔会在画面左右各加一条贯穿全高的装饰条（像卷轴边框）。
这里自动检测：从边缘向内逐列检查，若整列都是非白就判为边框并裁掉。
"""
import os
import sys

import numpy as np
from PIL import Image

WHITE_MIN = 235   # 平均亮度高于它视为白


def trim_bars(path, margin=2):
    im = Image.open(path).convert("RGB")
    a = np.asarray(im).astype(np.int16)
    lum = a.mean(axis=2)
    h, w = lum.shape

    col_ink = (lum < WHITE_MIN).sum(axis=0)
    row_ink = (lum < WHITE_MIN).sum(axis=1)

    # 整列/整行几乎全是墨迹 => 边框条
    col_bar = col_ink > h * 0.9
    row_bar = row_ink > w * 0.9

    left = 0
    while left < w and col_bar[left]:
        left += 1
    right = w
    while right > left and col_bar[right - 1]:
        right -= 1
    top = 0
    while top < h and row_bar[top]:
        top += 1
    bottom = h
    while bottom > top and row_bar[bottom - 1]:
        bottom -= 1

    if (left, top, right, bottom) == (0, 0, w, h):
        return im, (0, 0, 0, 0)

    left = min(left + margin, w - 1)
    right = max(right - margin, left + 1)
    top = min(top + margin, h - 1)
    bottom = max(bottom - margin, top + 1)
    return im.crop((left, top, right, bottom)), (left, top, w - right, h - bottom)


def main():
    paths = sys.argv[1:]
    if not paths:
        d = "TowerDesigns"
        paths = [os.path.join(d, f) for f in sorted(os.listdir(d))
                 if f.lower().endswith((".png", ".jpg"))]
    for p in paths:
        fixed, cut = trim_bars(p)
        if cut == (0, 0, 0, 0):
            print("%-46s 无需裁剪 %s" % (os.path.basename(p), fixed.size))
            continue
        fixed.save(p)
        print("%-46s 裁掉 L%d T%d R%d B%d -> %s"
              % (os.path.basename(p), cut[0], cut[1], cut[2], cut[3], fixed.size))


if __name__ == "__main__":
    main()
