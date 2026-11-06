# 防御塔设计图（按干员立绘派生）

每张图是一张**四阶段设计表**：同一座塔的 Lv1→Lv4 形态横向排成一排，
白底、无文字、无 UI，可以直接切分成 4 张单体素材。

风格与干员立绘一致：白色硬表面装甲 + 深炭黑机械关节 + 金色饰边，
能量色跟角色走（青 / 红 / 冰蓝 / 紫 / 橙 / 品红 / 薄荷绿 / 金）。

## 干员 → 塔 的对应关系

| 干员 | 职业 | 能量色 | 立绘要素 | 塔的攻击方式 |
| --- | --- | --- | --- | --- |
| 岚 RAN | 突击 assault | 青 `#4ff3ff` | 双持双管手枪、胸甲六边形能量核心 | 双流离子速射，单体高频 |
| 零式 ZERO | 突击 assault | 红 `#ff5b6e` | 红瞳、双枪、白色护甲 | 交替脉冲齐射，暴击爆发 |
| 维加 VEGA | 狙击 sniper | 冰蓝 `#8fa8ff` | 后掠翼状肩甲、长导轨枪 | 蓄能轨道枪，超远单体 |
| 赛拉 SERA | 狙击 sniper | 紫 `#c77dff` | 背后环状光环装置、贯穿光矛 | 贯穿光束，一枪穿多目标 |
| 格洛姆 GROM | 重炮 siege | 橙 `#ffb03a` | 肩载六管旋转炮、粗工业管线 | 重口径动能弹幕 + 抛射炮弹 |
| 卡俄斯 CHAOS | 重炮 siege | 品红 `#ff6bd6` | 棱面星形爆炸核心 | 抛射集束弹，落点范围爆炸 |
| 艾拉 ELLA | 拦截 intercept | 薄荷 `#5be39a` | 晶体护盾、棱面玻璃板 | 生成棱镜屏障挡路 |
| 诺娃 NOVA | 拦截 intercept | 金 `#ffd166` | 悬浮无人机、蜂巢单元 | 放出小型拦截无人机群 |

## 四阶段的设计逻辑

所有塔共用同一条升级曲线，保证一眼能读出"这是同一座塔的第几级"：

| 阶段 | 形态特征 |
| --- | --- |
| Lv1 | 低矮、单管或最简结构、底座最窄 |
| Lv2 | 加高、双管/双臂、出现角色标志性部件（六边形核心 / 光环 / 星核） |
| Lv3 | 明显变宽变重、三管以上或外露管线、出现警示色点缀 |
| Lv4 | 最高最复杂、角色标志性部件完全展开（悬浮环 / 翼展 / 蜂群 / 屏障墙） |

## 文件清单

### 设计图（每张含 4 阶段）

| 文件 | 干员 | 阶段数 |
| --- | --- | --- |
| `tower-ran-ion.png` | 岚 | 4 |
| `tower-zero-crimson.png` | 零式 | 4 |
| `tower-vega-falcon.png` | 维加 | 4 |
| `tower-sera-halo.png` | 赛拉 | 4 |
| `tower-grom-hammer.png` | 格洛姆 | 4 |
| `tower-chaos-starburst.png` | 卡俄斯 | 4 |
| `tower-ella-prism.png` | 艾拉 | 4 |
| `tower-nova-swarm.png` | 诺娃 | 4 |

### 单体素材

`Stages/` 下 32 张透明背景 PNG，命名 `<塔>-lv<1-4>.png`，高度统一 700px
（Lv1 较矮的按原比例，不拉伸）。

### 总览

`_overview.png`：8 行 × 4 列的完整矩阵，一次性核对全部阶段。

## 重新生成

提示词在 `tools/apimart-towers.json`（`tools/apimart-assets.json` 在 .gitignore 里，
所以单独放一份）。

```powershell
# 单张
python .cursor/skills/apimart-image/scripts/apimart_image.py `
  --manifest tools/apimart-towers.json --only tower-ran

# 全部
python .cursor/skills/apimart-image/scripts/apimart_image.py `
  --manifest tools/apimart-towers.json

# 裁掉生图偶尔带出的左右装饰条（贯穿全高的非白竖条）
python tools/tower_trim.py

# 用连通域切成 4 段，输出 32 张透明背景单体素材到 TowerDesigns/Stages/
python tools/tower_cut.py

# 生成 8×4 总览图，一次看完所有塔的所有阶段
python tools/tower_overview.py
```

`tower_trim.py` 是幂等的：没有边框条时原样输出。

`tower_cut.py` 不用等分切。四个塔大小差距很大（Lv4 常比 Lv1 宽一倍），
等分会切到塔身。它先对墨迹做**横向膨胀**让每个塔的内部连成一块，
再取面积最大的 4 个连通块，切点落在相邻块之间的中点。
透明背景用「从画面四边泛洪」——不能直接按"接近白=透明"，
否则塔身的白色装甲会被一起抠掉。
