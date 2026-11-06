"""删除 UI 素材的 .meta，强制 Unity 按新规则（比例边距）重新导入。

为什么要删：AssetPostprocessor 的 OnPreprocessTexture 只在**首次导入**时生效，
已存在的 .meta 会让 Unity 复用旧设置（旧的像素边距），导致九宫格切错位置。

删除 .meta 是安全的：这些贴图没有被场景/预制体直接引用，
运行时是通过 Resources.Load<Sprite>("Art/UI/xxx") 按名字加载的，
重新导入会生成新 GUID，但按名字加载不受影响。
"""
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
UI = ROOT / "Assets" / "Resources" / "Art" / "UI"

TARGETS = [
    "panel", "panel-gold", "button-primary", "button-gold",
    "chip", "bar", "card", "frame-plate", "frame-round",
    "icon-star", "icon-coin", "icon-crystal", "icon-pause",
    "icon-speed", "icon-help", "icon-arrow", "icon-book",
]

removed = 0
for name in TARGETS:
    meta = UI / f"{name}.png.meta"
    if meta.exists():
        meta.unlink()
        removed += 1
        print("rm", meta.name)
print(f"removed {removed} meta files")
