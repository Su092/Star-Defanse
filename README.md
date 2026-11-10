# 星轨防线 · STARLANE BULWARK

竖屏（逻辑 1080×1920，窗口 540×960 等比缩放）单屏星域塔防。开局编队，局内消耗金币**随机召唤**编队里的防御塔，**同级拖拽合成**升阶，用**抽 3 选 1 的编队词条**滚雪球，同时操作一名可自由移动的英雄补火力。每关固定 10 波：第 5 波精英，第 10 波 BOSS。

**Unity 6000.6.4f1**（C# + 内置渲染管线 + 自定义赛璐璐着色器 + Legacy 动画）。

## 运行

用 Unity 6000.6.4f1 打开本目录，或命令行：

```powershell
& "D:\Unity\6000.6.4f1\Editor\Unity.exe" -projectPath "<本目录>"
```

场景为 `Assets/Scenes/Main.unity`。全部 UI 与战场节点由代码在运行时构建，场景里只有一个入口物体。

## 操作

| 操作 | 说明 |
| --- | --- |
| 点击「召唤」 | 花金币从编队随机出 1 座 1 级塔，落在已解锁的空塔台；价格逐次提升 |
| 点击「强化」 | 花金币从编队词条池抽 3 选 1，全局生效；价格逐次提升 |
| 拖动防御塔 | 拖到空塔台＝换位；拖到**同种同等级**塔＝合成，等级 +1、干员不变（最高 4 级） |
| 点击/拖动地面 | 移动英雄，自动攻击范围内敌人（不会死亡、不阻挡） |
| 空格 / Esc | 暂停菜单（继续 / 重开 / 退出） |
| 1 / 2 / 3 | 1×、2×、3× 速度 |
| S / U / M | 召唤 / 强化 / 静音 |

每召唤 3 次解锁 1 个新塔台；开局解锁 6 个，每关共 13–14 个。只有**同一种**防御塔才能合成升级。

## 目录

| 路径 | 说明 |
| --- | --- |
| `Assets/Scenes/Main.unity` | 唯一场景 |
| `Assets/Scripts/Core/STData.cs` | 静态数据：职业、干员、词条、敌人、波次、关卡、调参（无状态） |
| `Assets/Scripts/Core/RunState.cs` | 局内模拟：路径、召唤、合成、词条、塔/敌/守卫/弹道、胜负（无 Unity 依赖，可 headless 测） |
| `Assets/Scripts/View/STBattleView.cs` | 3D 战场：模型实例化、炮塔瞄准、弹道、特效、相机与灯光 |
| `Assets/Scripts/View/STUi.cs` / `STScreens.cs` / `HudView.cs` | UI 构建：HUD、菜单、编队、关卡、弹窗、结算 |
| `Assets/Scripts/View/STFxSystem.cs` | 战场特效与飘字（飘字走 Overlay Canvas，永远在 3D 之上） |
| `Assets/Scripts/View/STUnitFSM.cs` | 单位动画状态机（`FSM` 表在 `STBattleView` 顶部） |
| `Assets/Scripts/App/STMain.cs` | 流程与输入：屏幕切换、固定步长循环、指针拖拽、音效、存档 |
| `Assets/Scripts/App/STAutoShot.cs` | 自动截图与实机自检（输入 / 塔姿态 / 朝向 / 存档 / 飘字） |
| `Assets/Scripts/Editor/` | 导入规则、资源校验、打包、逻辑自检、场景构建 |
| `Assets/Resources/Art/` | 运行时素材：UI 九宫格、战斗精灵、背景立绘、关卡地图、FBX 模型 |
| `Assets/Resources/Data/sldata.json` | 静态数据（由 `STData.cs` 读取） |
| `Assets/Resources/Fonts/` | 等线 Regular / Bold |
| `scripts/check.ps1` | 常用命令入口（tests / verify / build / shots） |
| `tools/apimart-assets.json` | 生图素材清单（APIMart gpt-image-2） |
| `.cursor/skills/apimart-image/` | 生图 skill：调用脚本与使用说明 |

## 验证

```powershell
# 逻辑回归（123 项断言，headless）
powershell -ExecutionPolicy Bypass -File scripts/check.ps1 -Task tests

# 资源完整性（位图可加载、模型/动画/字体齐全、数据一致）
powershell -ExecutionPolicy Bypass -File scripts/check.ps1 -Task verify

# 打包
powershell -ExecutionPolicy Bypass -File scripts/check.ps1 -Task build

# 实机自检 + 截图（输出到 Shots/）
& ".\Build\ST.exe" -autoshot -screen-width 540 -screen-height 960 -screen-fullscreen 0
```

`-autoshot` 会依次验证并输出：

- `INPUT RESULT` — 点击召唤、点地移动英雄、点塔显射程、拖塔显射程、同级合成、等级不同拒绝
- `MODEL RESULT` — 4 种塔落地 minY≈0、攻击动画不扭根节点、只转 `Yaw` 时底座零位移
- `FACING RESULT` — 单位机头指向移动方向
- `SAVE RESULT` — 通关存档长度跟随关卡数、最后一关可落盘
- `FX RESULT` — 金币飘字贴图有内容、绘制在 Overlay 层

## 生图（APIMart / gpt-image-2）

所有 AI 素材走 APIMart，详见 **`.cursor/skills/apimart-image/SKILL.md`**。

```powershell
# 先放密钥（该文件已 gitignore，不会入库）
Set-Content tools/.apimart.key "sk-你的密钥"

# 单张
python .cursor/skills/apimart-image/scripts/apimart_image.py `
  --prompt "..." --out Assets/Resources/Art/Generated/x.png --size 9:16 --resolution 2k

# 按清单批量
python .cursor/skills/apimart-image/scripts/apimart_image.py `
  --manifest tools/apimart-assets.json --only level-map-3
```

> 注意：本机 DNS 会把 `api.apimart.ai` 污染成不可达地址（表现为「提交成功但轮询永远超时」）。
> 脚本已用 DoH + 固定 IP 绕过，手写调用必须照抄这套逻辑。

## 关键实现约定

- **炮塔只转 `Yaw` 骨骼**，底座永不旋转；瞄准角自校准（生成时记录静止炮口朝向），不依赖手工常数。
- **单位朝向**：`Body` 同时应用 `Pitch` 与 `Yaw`（`Quaternion.Euler(Pitch, Yaw, 0)`）。漏掉 `Yaw` 会让敌人整体倒着走。
- **模型重导**：Blender 用 `Scale 1.00 / FBX All / Forward -Z / Up Y / Apply Unit / Use Space Transform`，`Apply Transform` 不勾。
  FBX 会把轴转换烘焙成根节点 `RotX(-90°)` 轨道 —— 敌人靠它立起来，塔的姿态已由 `Pitch` 校正，
  所以 `Anim_Tower_*.fbx` 的根轨道必须在导入阶段剔除（见 `STAssetPostprocessor`）。
- **动画轨道剔除**只删「路径末段恰好是 `Yaw`」的轨道，不能用子串匹配（否则会删空后坐/炮口闪光等后代轨道）。
- **飘字**走屏幕空间 Overlay Canvas，不能放 3D 层（会被血条等 Always+Transparent 材质挡住）。
- **落地**由 `STGroundSnap` 在生成后一帧用真实 `Renderer.bounds` 压到 y=0。
- 逻辑必须保持确定性（mulberry32 + 种子），否则 `tests` 会失败。
- 工程内不使用 SVG，图标与所有素材均为 PNG/JPEG 位图。

## 视觉风格

**深空赛璐璐**：日本动画赛璐璐上色 + 星际舰队仪表盘。厚黑描边、平涂双色、
硬边 45° 高光、切角面板。完整规范见枢纽文档
`E:\Unity Development\Project\Star Defanse\docs\art-style.md`（设计与数值文档一律不放在本工程内）。

- UI 素材由 APIMart 生成，清单在 `tools/ui-redesign.json`
- 战场地图是**星球表面**（月面 / 冰原高地 / 晶簇盆地 / 火星红壤）
- **地形由 AI 生成，车道由代码按真实路径绘制**（`tools/paint_lane.py`），
  保证画面路线与逻辑路线永远一致

## 素材来源

全部美术由 APIMart `gpt-image-2` 生成，清单见 `tools/` 下的 `*-redesign.json` 与 `maps-terrain.json`。
3D 模型为外部制作后导入的 FBX（`Assets/Resources/Art/Models/`，按模型分目录，含 `Anim_*.fbx`）。

