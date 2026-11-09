---
name: apimart-image
description: Generate images via APIMart (gpt-image-2) and save them into this project. Use when the user asks to create or regenerate art assets — level maps, character portraits, enemy/threat illustrations, UI pieces, icons, textures — or mentions APIMart, gpt-image-2, 生图, 生成素材, 生成贴图.
---

# APIMart 生图（gpt-image-2）

本项目的 AI 素材统一走 APIMart。**不要**用浏览器或其它生图通道替代。

## 凭据与端点

| 项 | 值 |
| --- | --- |
| Base URL | `https://api.apimart.ai/v1` |
| 提交 | `POST /v1/images/generations` |
| 轮询 | `GET /v1/tasks/{task_id}` |
| 模型 | `gpt-image-2`（固定） |
| 密钥 | 环境变量 `APIMART_API_KEY` → `tools/.apimart.key` |

**首次使用**：把自己的密钥写进 `tools/.apimart.key`（单行纯文本），或设环境变量。
该文件已在 `.gitignore` 中，不会被提交。

```powershell
# 写入密钥（二选一）
Set-Content tools/.apimart.key "sk-你的新密钥"
$env:APIMART_API_KEY = "sk-你的新密钥"
```

## 一键调用

仓库根目录执行。脚本只用 Python 标准库，无需 pip 安装。

```bash
# 单张
python .cursor/skills/apimart-image/scripts/apimart_image.py \
  --prompt "提示词" --out Assets/Resources/Art/Generated/my-asset.png \
  --size 9:16 --resolution 2k

# 批量：按清单里的 id
python .cursor/skills/apimart-image/scripts/apimart_image.py \
  --manifest tools/apimart-assets.json --only level-map-3,op-ran

# 只查已有任务（不重新提交）
python .cursor/skills/apimart-image/scripts/apimart_image.py \
  --task-id task_xxx --out Assets/Resources/Art/Generated/foo.png
```

清单格式见 `tools/apimart-assets.json`：顶层 `style` 是全局风格后缀，
`assets[]` 每项 `{id, out, size, resolution, prompt, no_global_style?}`。
**新素材一律往这个清单里加条目**，不要散落成一堆临时命令。

## 关键坑：本机 DNS 被污染

2026-11 实测：本地 DNS 把 `api.apimart.ai` 解析到 Facebook 的地址
（`128.242.250.155` / `2a03:2880:...:face:b00c`）。症状是 **POST 偶尔成功、GET 轮询全部超时**：
`curl` 报 `connect timed out`，Python 报 `SSL: UNEXPECTED_EOF_WHILE_READING`，
看起来像"提交成功但永远查不到结果"。

真实地址是 Cloudflare 的 A 记录。脚本已内置处理，无需手动干预：

1. 用 DoH（`dns.google` / `cloudflare-dns.com`，走 HTTPS 不受投毒影响）取真实 IP；
2. 缓存到 `tools/.apimart-ip.json`；
3. 用「固定 IP + 真实域名做 SNI/证书校验」建连；
4. 全失败时再退回系统 DNS。

**手写调用必须照抄这套逻辑**，否则会踩同样的坑。

## 参数速查

- `size`：比例字符串（`1:1` / `3:2` / `2:3` / `4:3` / `3:4` / `16:9` / `9:16` / `2:1` / `1:2` / `21:9` / `3:1` / `1:3` …）
  或直接给像素（`1080x1920`）。**合法比例白名单见下**，`8:1`、`5:2` 这类会直接
  返回 `invalid size`，任务失败并自动退费。
- `resolution`：`1k` / `2k` / `4k`，决定实际像素档位，**按档位计费**；调试用 `1k`。
- `background`：`transparent` 让服务端**直接返回带 alpha 的 PNG**，
  比事后抠图干净得多（描边、内发光这类半透明边缘抠不干净）。
  UI 素材、图标、道具一律用它。
- `n`：必须传纯数字（本脚本固定 1）。
- 结果只返回 `url`（有效期约 24h），需自行下载：`data.result.images[0].url[0]`。
  脚本会自动下载并校验体积（<10KB 视为失败，防止拿到错误页）。

合法比例白名单（实测）：
`1:1 16:9 9:16 4:3 3:4 3:2 2:3 5:4 4:5 2:1 1:2 21:9 9:21 3:1 1:3`

常用尺寸：

| 用途 | size | resolution | background |
| --- | --- | --- | --- |
| 竖屏整屏地图 / 立绘 | `9:16` | `2k` | opaque |
| 竖幅卡片 | `2:3` | `2k` | opaque |
| 方形图标 | `1:1` | `1k` | transparent |
| 横屏横幅 | `16:9` | `2k` | opaque |
| 九宫格 UI 素材 | `4:3` / `3:1` | `2k` | **transparent** |

### UI 素材的九宫格注意

生图产出的边框**左右/上下必然不等厚**。不要因此认为它不能做九宫格 ——
Unity 的 `spriteBorder` 是 `(left, bottom, right, top)` 四个独立值，
照实测写入即可（测量工具见 `tools/ui_borders.py`）。
反而是「用短边 × 固定比例给四个相等值」这种做法会把某一侧白带推进可拉伸区，
拉伸后就是肉眼看到的「UI 被压扁」。

## 提示词写法

- 全局风格后缀（赛璐璐、深紫星空、霓虹青/品红、无文字无 UI）在清单 `style` 里，**不要重复**；
  需要脱离该风格时给该条加 `"no_global_style": true`。
- 地图类：写清 "top-down orthographic"、"portrait framing"、"the whole frame is the ground plane"，
  并逐项列出**不要**的东西（NO buildings / towers / gates / characters / text / UI）。
  负面清单要写具体物件名，泛泛的 "no extraneous elements" 不管用。
- 地面要压暗、低对比，否则小单位在俯视下看不清。
- 立绘类：写明 "single character, centered, plain flat background"，避免生成多人群像。

## 生成后的接入

1. 产物放到 `Assets/Resources/Art/` 下对应子目录（`UI/`、`Sprites/`、`Backgrounds/`）。
2. Unity 会自动导入；贴图导入规则在 `Assets/Scripts/Editor/STAssetPostprocessor.cs`
   （Sprite 类型、九宫格边距、压缩设置）。改了导入规则需重新导入资源。
3. 若是换掉已有同名贴图，直接覆盖文件即可，`.meta` 会保留原 GUID，引用不会断。
4. 验证：`powershell -ExecutionPolicy Bypass -File scripts/check.ps1 -Task verify`

## 交付前必查

```powershell
powershell -ExecutionPolicy Bypass -File scripts/check.ps1 -Task verify
powershell -ExecutionPolicy Bypass -File scripts/check.ps1 -Task build
& ".\Build\ST.exe" -autoshot -screen-width 540 -screen-height 960 -screen-fullscreen 0
```

截图会输出到 `Shots/`，确认新素材在实机里显示正常。
