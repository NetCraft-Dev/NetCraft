# NetCraft.Game.GUI 概览与原版 HUD/页面布局调研

## 1. 模块职责

NetCraft.Game.Gui 是 Game 业务层 GUI，承载 Minecraft 业务屏幕与业务控件。对应原版 `net.minecraft.client.gui` 包：
- `screens/` 业务屏幕（TitleScreen/PauseScreen/OptionsScreen/GameScreen 等）
- `components/` 业务控件（带 SoundManager 的 GameButton、物品槽 SlotWidget 等）
- `events/` 业务键处理（GameKeys.cs 已有）
- `screens/Screen.cs` 屏幕基类 + `ScreenManager.cs` 屏幕管理器

通用 UI 框架（控件基类、布局、渲染上下文、字体）在 NetCraft.Gpu 内核层，详见 `NetCraft.Gpu/Overview.md`。Game 层只做业务屏幕与业务控件，不重复造通用控件。

## 2. 当前实现状态

| 子项 | 文件 | 状态 |
|------|------|------|
| Screen 基类 | Screen.cs | 生命周期 Init/Removed/OnClose/Tick/RenderBackground 已就绪 |
| ScreenManager | ScreenManager.cs | SetScreen/PushScreen/PopScreen/Resized/HandleRawKeyDown 已就绪 |
| GameKeys | GameKeys.cs | Esc/F3/D1-D9/Keypad1-9 键码常量已就绪 |
| TitleScreen | Screens/TitleScreen.cs | 主菜单 + dirt 纹理平铺背景已就绪 |
| GameScreen(HUD) | Screens/GameScreen.cs | 十字准星 + Hotbar + 心/饥饿条 + F3 Debug 已就绪（色块占位） |
| OptionsScreen | Screens/OptionsScreen.cs | 选项屏幕骨架 |
| PauseScreen | Screens/PauseScreen.cs | 暂停菜单骨架 |

已完成的 GUI 体系阶段（M1 + P1-P5 + 中文修复 + GUI scale + 纹理背景）：
- P1 渲染上下文：CPU Pose 栈 + GPU 动态 Scissor
- P2 布局引擎：IGuiLayout + GuiLinearLayout
- P3 多 RenderPipeline：矩形/文本/图片/反色 4 pipeline 同 RenderPass
- P4 字体渲染：CJK U+4E00..U+9FFF + 文本对齐
- P5 事件系统：TabStop/TabIndex 焦点导航
- 中文修复：DrawText 用 Ascent 替代 LineHeight
- GUI scale：整数倍缩放 ScaledWidth/Height 控件等比放大不模糊
- 纹理背景：StbImageSharp + GetTextureId + GuiImage Tile + RenderBackgroundHook

## 3. 原版 HUD 子系统调研

原版 HUD 由 `net.minecraft.client.gui.Gui` 类驱动（1.20+ 重构为 `GuiRenderer`），渲染层级从下到上：

### 3.1 Hotbar 物品栏
- 9 槽位，纹理来自 `assets/minecraft/textures/gui/widgets.png`（512x256 图集）
- 槽位背景 24x24，选中框 28x28（比槽位大，露出白边）
- 选中槽位由 `player.getInventory().selected` 决定，数字键 1-9 / 滚轮切换
- 物品图标由 ItemRenderer 渲染 16x16 物品模型，数量文字右下角，耐久条左侧

### 3.2 状态条
- **生命**：10 心，每心 2 点生命，半心/满心/空心头像三种状态，满血时心跳闪烁
- **饥饿**：10 鸡腿，与生命镜像对称（从右向左），饱和度影响生成速度
- **护甲**：仅在护甲值 > 0 时显示，10 盾牌图标，位于生命条上方
- **氧气**：水下显示，10 气泡，位于饥饿条上方
- **经验条**：Hotbar 上方绿色条 + 等级数字居中，经验值 0-1 映射条宽

### 3.3 十字准星
- 16x16 像素，纹理 `widgets.png` 或纯色混合（原版用 blend）
- 中心对齐窗口，`ScaledWidth/2 - 8, ScaledHeight/2 - 8`

### 3.4 物品 tooltip
- 鼠标悬停 Hotbar 物品时显示，深色半透明背景 + 物品名 + 附魔/Lore 多行
- 宽度由最长行决定，九宫格背景切片渲染

### 3.5 F3 Debug HUD
- 左上角多行文本：版本/FPS/XYZ/区块/面向/生物群系/光照/难度
- 右上角：内存/线程/区块加载统计
- 右下角：性能图表（FPS 折线 + 内存条）

### 4. 原版 Screen 体系调研

### 4.1 TitleScreen 主菜单
- dirt 纹理平铺背景 + 深色 overlay（`PanoramaRenderer` 在新版本可选旋转全景）
- Logo（`minecraft.png` 或 splash 文本）居中偏上
- 按钮列：单人游戏 / 多人游戏 / 选项 / 退出（200x20 标准 GuiButton）
- 底部 splash 文本随机黄色斜体
- 版权 `Copyright Mojang AB` 底部小字

### 4.2 PauseScreen 暂停菜单
- dirt 背景与 TitleScreen 一致
- "游戏暂停" 标题
- 按钮：返回游戏 / 保存并退出 / 选项 / 玩家皮肤 / 报告 Bug

### 4.3 OptionsScreen 选项
- 多个子页面：视频/控制/语言/资源包/辅助功能
- 控件：CycleButton（循环选项）/ Slider（滑块）/ EditBox（输入框）
- 控件列表可滚动（`OptionsList` 基于 `AbstractContainerEventHandler`）

### 4.4 InventoryScreen 物品栏
- 容器网格（玩家背包 9x3 + 快捷栏 9x1）
- 纹理 `assets/minecraft/textures/gui/container/` 下对应图（`inventory.png` / `crafting_table.png` / `chest.png` 等）
- 拖拽物品逻辑：左键拾取/放置，右键分散，Shift 转移
- 槽位由 `Slot` 抽象 + `ContainerMenu` 管理物品同步

### 5. 原版 Widget 体系调研

`net.minecraft.client.gui.components.AbstractWidget` 基类职责：
- 持 X/Y/Width/Height + visible/active + renderTexture/renderWidget
- 鼠标事件 `onClick/onDrag/onRelease`，键盘事件 `keyPressed`
- 焦点系统 `isFocused/setFocused`，Tab 导航 `nextFocusPath`

| Widget | 原版类 | 用途 | NetCraft 对应 |
|--------|--------|------|---------------|
| 按钮基类 | AbstractButton | 渲染九宫格 + 处理点击 | GuiButton（已有色块占位） |
| 普通按钮 | Button | 标准按钮 + OnPress 回调 | GuiButton |
| 编辑框 | EditBox | 文本输入 + 光标 + 选区 | 待实现 |
| 滑块 | AbstractSlider | 拖拽 + 数值绑定 | GuiSlider（已有） |
| 复选框 | Checkbox | 切换布尔 | GuiCheckbox（已有） |
| 循环按钮 | CycleButton | 循环选项 + 当前值显示 | 待实现 |
| 列表 | AbstractSelectionList | 滚动列表（服务器/资源包） | 待实现 |
| 标签 | MultiLineLabel | 多行文本 | GuiLabel（单行） |

## 6. 原版渲染调研

### 6.1 GuiGraphics 统一渲染上下文
原版 `GuiGraphics` 是立即模式渲染器，对应 NetCraft 的 `IGuiRenderContext`：
- `blit(texture, x, y, ...)` 绘制纹理子区域 → NetCraft `DrawImage`
- `drawString(font, text, x, y, color)` 绘制文本 → NetCraft `DrawText`
- `fill(x1, y1, x2, y2, color)` 绘制矩形 → NetCraft `DrawQuad`
- `enableScissor/disableScissor` 裁剪栈 → NetCraft `PushScissor/PopScissor`
- `pose` 矩阵栈 → NetCraft `PushPose/PopPose`

### 6.2 九宫格切片渲染（NinePatch）
按钮/面板背景用九宫格切片：4 角固定 + 4 边拉伸 + 中心平铺。
- 纹理 `widgets.png` 切片坐标硬编码在 `RenderSystem` 或 `Gui` 类
- NetCraft 待实现：`DrawImageNinePatch(x, y, w, h, srcX, srcY, cornerSize)` 或用 9 次 DrawImage 拼接

### 6.3 GUI scale
原版 `Minecraft.getEffectiveGuiScale()` 返回 `(guiScale, scaledWidth, scaledHeight)`，与 NetCraft `GuiWindow.GuiScale/ScaledWidth/ScaledHeight` 对齐。原版阈值 320x240，NetCraft 沿用。

## 7. 资源依赖清单

| 资源 | 路径 | 用途 |
|------|------|------|
| widgets.png | `assets/minecraft/textures/gui/widgets.png` | Hotbar/心/饥饿/十字准星/按钮九宫格 |
| options_background.png | `assets/minecraft/textures/gui/options_background.png` | 选项屏幕背景平铺 |
| dirt.png | `assets/minecraft/textures/block/dirt.png` | TitleScreen/PauseScreen 背景平铺 |
| minecraft.png | `assets/minecraft/textures/gui/title/minecraft.png` | TitleScreen logo |
| container/*.png | `assets/minecraft/textures/gui/container/*.png` | 容器屏幕背景（背包/工作台/箱子等） |
| font/*.json | `assets/minecraft/font/*.json` | 字体提供器配置（位图/旧版/TTF） |
| *.png | `assets/minecraft/textures/font/*.png` | 位图字体图集（原版默认 ascii.png） |

## 8. 当前 NetCraft 差距与移植建议

### 8.1 已就绪能力
- 通用控件基类（GuiControl/GuiContainer/GuiButton/GuiLabel/GuiSlider/GuiCheckbox/GuiImage）
- 屏幕生命周期与切换（Screen/ScreenManager）
- 键事件桥接（VulkanGuiApp.RawKeyDown → ScreenManager.HandleRawKeyDown）
- 渲染上下文（4 pipeline + Pose/Scissor 栈）
- 纹理加载与平铺（GetTextureId + GuiImage Tile）
- GUI scale 整数缩放（窗口 resize 自适应）

### 8.2 待补能力（按优先级）

**P0 必做：业务纹理 HUD 替换色块**
- GameScreen 的 Hotbar/心/饥饿/十字准星 改用 `widgets.png` 切片渲染
- 需先确认 assets 已提取（AssetsExtractor 已支持），再调 `RegisterTexture(widgetsPath)` 得 id
- 切片坐标硬编码常量（参考原版 `Gui` 类）

**P1 高优：EditBox 文本输入**
- 单行输入 + 光标闪烁 + 选区 + 字符过滤
- 当前 GuiWindow.ProcessKeyChar 已派发到焦点控件，GuiEditBox 需消费

**P2 中优：九宫格按钮背景**
- GuiButton 改用九宫格切片（widgets.png 普通态/悬停/禁用 3 态）
- 提供 `DrawImageNinePatch` 扩展或 9 次 DrawImage 拼接

**P3 中优：PauseScreen 完整化**
- 对齐 TitleScreen 加 dirt 背景 + 按钮列
- 返回游戏 / 保存并退出 / 选项

**P4 低优：OptionsScreen 控件列表**
- 滚动列表容器（AbstractSelectionList 对应）
- CycleButton 循环选项

**P5 低优：InventoryScreen 容器**
- 需要 ContainerMenu + Slot 抽象 + 拖拽逻辑（依赖物品系统先就绪）
- 这是大任务，建议物品系统稳定后单独推进

### 8.3 不建议移植
- 原版 `PanoramaRenderer` 全景旋转（需独立渲染管线，收益低）
- 原版 Realms 按钮（无对应业务）
- 原版辅助功能高对比度模式（无业务需求）

## 9. 后续工作建议

1. **HUD 纹理化**（独立任务）：GameScreen 改用 widgets.png 切片，预计 1-2 天
2. **EditBox**（独立任务）：文本输入控件，预计 1 天
3. **九宫格切片**（基础设施）：DrawImageNinePatch，预计半天，配合按钮改造
4. **OptionsScreen 完整化**：滚动列表 + CycleButton，预计 2-3 天
5. **InventoryScreen**：依赖物品系统，建议作为更大任务的一部分

本调研仅记录方向，不触发实现。后续按需开独立任务推进。
