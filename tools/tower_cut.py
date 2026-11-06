"""把四阶段设计图切成 4 张单体素材（透明背景，统一高度）。

切分用**连通域**而不是等分或阈值间隙：
四个塔本来就是互不相连的独立物体，先做横向膨胀让每个塔的内部连成一块，
再取面积最大的 4 个连通块，按 x 排序，切点落在相邻块之间的中点。
这比「找空白列」稳得多 —— Lv4 常常比 Lv1 宽一倍，等分会切到塔身。

透明背景用「从画面四边泛洪」：只有与边缘连通的白才判为背景。
不能直接按"接近白=透明"，否则塔身的白色装甲会被一起抠掉。
"""
import os
import sys
from collections import deque

import numpy as np
from PIL import Image

OUT_DIR = os.path.join("TowerDesigns", "Stages")
WHITE = 236          # 泛洪判白的阈值
MAX_SIDE = 700       # 单体素材统一缩到这个高度以内
DILATE = 0.010       # 膨胀核宽度占图宽的比例（把塔内部缝隙连起来）


def ink_mask(im, thr=235):
    a = np.asarray(im.convert("RGB")).astype(np.int16)
    return a.mean(axis=2) < thr


def dilate_cols(mask, k):
    """只在横向做膨胀：把每个塔内部的竖向缝隙连成一块，但不跨塔相连。"""
    if k <= 1:
        return mask
    h, w = mask.shape
    pad = k // 2
    padded = np.pad(mask, ((0, 0), (pad, pad)), mode="constant", constant_values=False)
    out = np.zeros_like(mask)
    for d in range(k):
        out |= padded[:, d:d + w]
    return out


def components(mask, min_area_ratio=0.002):
    """4 连通域标记，返回 [(x0, y0, x1, y1, area)]，按面积降序。"""
    h, w = mask.shape
    seen = np.zeros((h, w), dtype=bool)
    blocks = []
    min_area = max(64, int(h * w * min_area_ratio))
    for sy in range(h):
        row = mask[sy]
        for sx in range(w):
            if not row[sx] or seen[sy, sx]:
                continue
            q = deque([(sy, sx)])
            seen[sy, sx] = True
            x0 = x1 = sx
            y0 = y1 = sy
            area = 0
            while q:
                y, x = q.popleft()
                area += 1
                if x < x0: x0 = x
                if x > x1: x1 = x
                if y < y0: y0 = y
                if y > y1: y1 = y
                for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    ny, nx = y + dy, x + dx
                    if 0 <= ny < h and 0 <= nx < w and mask[ny, nx] and not seen[ny, nx]:
                        seen[ny, nx] = True
                        q.append((ny, nx))
            if area >= min_area:
                blocks.append((x0, y0, x1, y1, area))
    blocks.sort(key=lambda b: -b[4])
    return blocks


def pick_cuts(blocks, w, n=4):
    """取横向跨度最大的 n 块（按面积），按 x 排序后用中点切线。"""
    if len(blocks) < n:
        return None
    # 面积最大的 n 块
    top = sorted(blocks, key=lambda b: -b[4])[:n]
    top.sort(key=lambda b: b[0])
    cuts = [0]
    for i in range(n - 1):
        cuts.append((top[i][2] + top[i + 1][0]) // 2)
    cuts.append(w)
    return cuts, top


def edge_flood_transparent(im):
    rgb = np.asarray(im.convert("RGB")).astype(np.int16)
    h, w = rgb.shape[:2]
    is_white = rgb.min(axis=2) >= WHITE
    visited = np.zeros((h, w), dtype=bool)
    q = deque()
    for x in range(w):
        for y in (0, h - 1):
            if is_white[y, x] and not visited[y, x]:
                visited[y, x] = True
                q.append((y, x))
    for y in range(h):
        for x in (0, w - 1):
            if is_white[y, x] and not visited[y, x]:
                visited[y, x] = True
                q.append((y, x))
    while q:
        y, x = q.popleft()
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            ny, nx = y + dy, x + dx
            if 0 <= ny < h and 0 <= nx < w and not visited[ny, nx] and is_white[ny, nx]:
                visited[ny, nx] = True
                q.append((ny, nx))
    out = np.dstack([rgb.astype(np.uint8), np.full((h, w), 255, dtype=np.uint8)])
    out[visited, 3] = 0
    return Image.fromarray(out, "RGBA")


def crop_opaque(im, pad=6):
    a = np.asarray(im)[:, :, 3]
    ys, xs = np.where(a > 8)
    if len(ys) == 0:
        return im
    x0, y0 = max(0, xs.min() - pad), max(0, ys.min() - pad)
    x1, y1 = min(im.width, xs.max() + 1 + pad), min(im.height, ys.max() + 1 + pad)
    return im.crop((x0, y0, x1, y1))


def run(path, verbose=True):
    base = os.path.splitext(os.path.basename(path))[0]
    im = Image.open(path).convert("RGB")
    w, h = im.size

    mask = ink_mask(im)
    grown = dilate_cols(mask, max(3, int(w * DILATE)))
    blocks = components(grown)
    picked = pick_cuts(blocks, w)

    if picked is None:
        if verbose:
            print("%-30s 连通块不足 4 个（%d），无法切分" % (base, len(blocks)))
        return []
    cuts, top = picked
    if verbose:
        print("%-30s %d 块  切点 %s" % (base, len(blocks), cuts))

    os.makedirs(OUT_DIR, exist_ok=True)
    made = []
    for i in range(len(cuts) - 1):
        # 用原图（未膨胀）切，膨胀只用于定位
        tile = im.crop((cuts[i], 0, cuts[i + 1], h))
        tile = edge_flood_transparent(tile)
        tile = crop_opaque(tile)
        if tile.height > MAX_SIDE:
            sc = MAX_SIDE / tile.height
            tile = tile.resize((max(1, round(tile.width * sc)), MAX_SIDE), Image.LANCZOS)
        out = os.path.join(OUT_DIR, "%s-lv%d.png" % (base, i + 1))
        tile.save(out)
        made.append((out, tile.size))
    return made


if __name__ == "__main__":
    args = sys.argv[1:]
    if not args:
        d = "TowerDesigns"
        args = [os.path.join(d, f) for f in sorted(os.listdir(d))
                if f.lower().endswith(".png")]
    for p in args:
        for out, size in run(p):
            print("  %-52s %s" % (out, size))
