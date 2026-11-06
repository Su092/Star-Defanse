# 星轨防线 · 视觉设计规范（深空赛璐璐）

> 本文是 UI 素材的**唯一设计依据**。改素材前先读这里，改完同步更新。

## 风格定义

**深空赛璐璐（Deep Space Cel）**：日本动画赛璐璐上色 + 星际舰队仪表盘的结合。

与旧版的区别 —— 旧版是「细霓虹发光线」的赛博朋克（靠 glow 出效果），
新版靠**厚黑描边 + 平涂双色 + 硬边高光**出效果：轮廓先立住，颜色再分块，
不用柔光。这正是 3D 模型（`ModelLit` 赛璐璐着色器）的语言，UI 与模型因此统一。

### 四条铁律

1. **厚黑描边**：所有可交互元素都有一圈近黑外描边，宽度约为素材短边的 2–3%。
   描边颜色 `#07040F`，不是纯黑，带一点冷紫。
2. **平涂双色**：每个面只用两个色阶，交界是**硬边直线**，不做渐变。
   上半部亮（`#1E1A46`），下半部暗（`#12102B`）。
3. **硬边高光**：一条锐利的亮色带（`#D8F4FF` 或主色调亮），斜切 45°，
   不透明度 0.35–0.5。**不要**径向柔光、不要外发光。
4. **切角造型**：矩形面板切掉四个角（倒角 6–14px），不用大圆角。
   圆形按钮保留正圆，但同样加厚描边 + 内圈硬边高光弧。

### 调色板

| 用途 | 色值 | 说明 |
| --- | --- | --- |
| 星空底 | `#0A0718` | 场景最暗处，略暖于旧版 |
| 面板亮 | `#1E1A46` | 面板上半 |
| 面板暗 | `#12102B` | 面板下半 |
| 描边 | `#07040F` | 所有轮廓 |
| 主强调 | `#4FF3FF` | 青，品牌色，保持不变 |
| 副强调 | `#FF3DAE` | 品红，警示/精英 |
| 金色 | `#FFD166` | 货币、稀有 |
| 高光 | `#D8F4FF` | 硬边高光、文字 |
| 次文字 | `#9B93C8` | 说明文字 |

## 布局（保持不变）

用户要求布局基本沿用现有结构，只换皮肤：

- **主菜单**：标题左上，底部一宽两窄三个按钮
- **战斗 HUD**：左上三块信息牌（关卡/波次/金币）竖排；
  左侧三个圆形按钮（暂停/倍速/帮助）竖排；右上编队头像竖排；
  左下强化卡；右下召唤大圆钮；底部中央核心耐久条
- **关卡选择**：竖排关卡卡（缩略图 + 名称 + 难度 + 状态）
- **编队**：两列干员卡

## 素材清单与规格

### 九宫格（`Art/UI/`）

> **边距规则**：`spriteBorder = 贴图短边 × 0.22`，由 `STBuild.FixUiBorders`
> 在每次资源校验时自动写入。运行时 `STUi.FitSlice` 再把四角按
> 「素材短边 ↔ 控件短边」等比缩放，所以同一张素材在 968px 弹窗和
> 72px 信息牌上都不会被压扁，切角永远是 45°。
>
> **为什么不用逐边实测**：这批素材有硬边阴影分界，自动判据会把整块内胆
> 误判成装饰（实测四个边都顶到 45% 上限）。固定比例 + 运行时等比缩放
> 是唯一稳定的组合。

| id | 尺寸 | 边距 | 用途 |
| --- | --- | --- | --- |
| panel | 542×302 | 66 | 通用面板（弹窗、卡片底） |
| panel-gold | 608×428 | 94 | 金色强调面板（结算胜利） |
| button-primary | 609×141 | 31 | 主按钮（青色） |
| button-gold | 722×198 | 44 | 金按钮（召唤/强化） |
| chip | 530×138 | 30 | 词条胶囊 |
| bar | 540×77 | 17 | 耐久条底槽 |
| card | 417×579 | 92 | 干员卡竖幅 |
| frame-plate | 554×161 | 35 | 大幅信息牌 |
| **plate-thin** | 512×160 | 32 | **窄条信息牌专用**（HUD 左上三块、核心耐久） |
| frame-round | 512×503 | — | 圆形按钮外框 |

**`plate-thin` 为什么单独做一张**：`frame-plate` 的装饰带（切角 + 45° 玻璃反光
+ 端头凹槽）实测占短边一半以上。九宫格四角是 1:1 绘制的，塞进 72px 高的
HUD 信息牌时只有两种结局 —— 要么四角之和超过高度被 Unity 强行压缩（整块压扁），
要么边距吃掉一半内容区（文字压在花纹上）。`plate-thin` 把装饰带控制在 20%，
内容区 448×96 足够放 30px 字。生成脚本：`python tools/make_plate_thin.py`。

### 图标（`Art/UI/`，透明背景）

`icon-star` `icon-coin` `icon-crystal` `icon-pause` `icon-speed`
`icon-help` `icon-arrow` `icon-book`，统一 128px 高、正方形画布、居中。

### 背景（`Art/Backgrounds/`）

| id | 尺寸 | 说明 |
| --- | --- | --- |
| menu-kv | 1024×1536 | 主菜单主视觉 |
| battle-backdrop | 1024×1536 | 战场天空幕（星云 + 行星） |
| elite-threat | 660×990 | 精英威胁插画 |
| boss-threat | 660×990 | BOSS 威胁插画 |
| op-* | 660×990 | 8 名干员立绘 |
| level-* | 960×640 | 4 张关卡缩略图（3:2） |
| level-map-* | 1080×1920 | 4 张战场地图（**全部星球表面**） |

### 不重新生成

`Art/Sprites/` 下 27 张塔/单位图片**已不被运行时使用**（战斗走 3D 模型，
只有 `STBuild.CheckSprites` 还在校验它们）。本次保留文件不动，
待后续确认后连同校验一起移除。

## 素材生产管线

**关键原则：地形由 AI 生成，车道由代码绘制。**

让 AI 在贴图里"顺手画一条路"必然和游戏逻辑路径对不上（环形路口会被画成捷径、
拐角有偏差），所以拆成两步：

```
1. 生成纯地形（不许画路）
   python .cursor/skills/apimart-image/scripts/apimart_image.py \
     --manifest tools/maps-terrain.json        # 战场地形 4 张
     --manifest tools/thumbs-redesign.json     # 关卡缩略图地形 4 张

2. 按真实 path 绘制车道（与 sldata.json 100% 对齐）
   python tools/paint_lane.py        # -> level-map-1/2/3.jpg + map.jpg
   python tools/paint_thumb.py       # -> level-starport/belt/nexus/crater.jpg
```

换地貌时只重跑第 1 步，路线永远不用管。

### UI 素材

```
python .cursor/skills/apimart-image/scripts/apimart_image.py \
  --manifest tools/ui-redesign.json      # 生成（1254² 原始）
python tools/ui_reset_meta.py            # 删 .meta 强制重导入
python tools/ui_shrink.py                # 压到 512/256
python tools/make_plate_thin.py          # 生成窄条信息牌 plate-thin
```

`ui_reset_meta.py` 必须跑：`AssetPostprocessor` 只在**首次导入**生效，
已存在的 `.meta` 会让 Unity 沿用旧的像素边距，导致九宫格切错。
删除 `.meta` 安全——这些贴图都靠 `Resources.Load<Sprite>("Art/UI/xxx")`
按名字加载，不依赖 GUID。

生成后跑一次资源校验，会自动把九宫格边距写成「短边 × 0.22」：

```
powershell -ExecutionPolicy Bypass -File scripts/check.ps1 -Task verify
```

### 九宫格自检

`ST.exe -autoshot` 会遍历所有激活界面的 Sliced Image，输出两行结果：

```
UI SLICE n/n    四边之和 < 控件尺寸（否则 Unity 会强行压缩 → 整块压扁）
UI TEXT  n/n    文字字形框落在内容区内（否则文字压在切角/高光上）
```

改布局或换素材后这两行必须全绿，否则就是用户反馈的「UI 被压缩」。

## 提示词模板

UI 素材共用风格前缀（清单 `style` 字段）：

```
Japanese cel-shaded anime game UI asset, VERY THICK near-black ink outline
(heavy bold contour, at least 3 percent of the asset width, color #07040F),
flat two-tone hard-edged shading with absolutely NO gradients and NO outer glow,
one crisp 45-degree specular highlight band, chamfered 45-degree corners,
deep space sci-fi, clean vector-like edges, high contrast, bold graphic look,
no text, no letters, no numbers, no watermark, no logo, no soft bloom
```

地图与缩略图**不用**这个前缀（会带出 UI 风格），改为逐条写清地貌与"不要路"。

