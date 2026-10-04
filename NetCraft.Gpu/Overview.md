# NetCraft.Gpu

> **完成度**：~95% · **阶段**：即时模式 → submission/render phase 分离架构重构已完成（阶段 0-6+8），阶段 7 Tick/Render 解耦部分完成（Tick 已独立线程，Render 仍串行） · **依赖**：零 NetCraft.* 依赖，仅 8 个 NuGet（Silk.NET + Veldrid.SPIRV + StbTrueTypeSharp + StbImageSharp） · **计划链位置**：Layer 4 渲染原语层，对应原版 `com.mojang.blaze3d/` + `net/minecraft/client/gui/render/` + `net/minecraft/client/renderer/state/gui/`
> **准绳文档**：`minecraft/GPU模块重构技术规划.md`（重构动机/目标架构/8 阶段计划/验收标准）

Layer 4 子库，渲染原语层。基于 Vulkan/Silk.NET 提供跨平台 GPU 渲染后端，对标原版 26.2 Blaze3D 的 **submission/render phase 分离架构**：submission 阶段构造不可变 RenderState 值对象提交到 GuiRenderState，render phase 按 (pipeline, texture, scissor) 排序合批提交 GPU。包含 GPU 资源抽象接口与 Vulkan 具体实现、声明式 pipeline 系统、纹理图集拼接算法、BakedModel/BakedQuad 纯数据容器、PIP 离屏渲染、blur 后处理、九宫格 sprite、字体 providers 链等渲染原语，并附带 GUI 控件系统用于测试与调试可视化。

## 架构边界

- 跨平台硬约束，禁止使用 DirectX/Metal 等平台特定 API
- 渲染原语层定位：可按需依赖其他 NetCraft.* 模块（如 Registry 的 BlockState），但**不承载业务逻辑**（方块模型 JSON 解析、BlockState→BakedModel 映射等依赖 ResourceManager/BlockStateRegistry 的业务逻辑放 NetCraft.Game/Client/Render/）
- 零 NetCraft.* 依赖：日志走 `IGpuLogger` 抽象（`GpuLogger` 默认实现），不直接引用 NetCraft.Util/NetCraft.Interop
- 顶层定义抽象接口（IGpuDevice/ICommandEncoder/IRenderPass 等），Vulkan 子目录提供具体实现，EmptyGpuContext 提供空实现供无 GPU 环境或单元测试使用
- Gui 子目录是 retained mode 控件系统，配合 GuiRenderContext submission + GuiRenderer render phase 调试可视化

## 三层分离架构（对标原版 26.2 Blaze3D）

```
Submission 层（GuiRenderContext 实现 IGuiRenderContext）
  ├─ 调用方调 DrawQuad/DrawText/DrawImage/DrawSprite
  ├─ 构造不可变 RenderState 值对象（ColoredRectangleRenderState/BlitRenderState/...）
  ├─ 维护 Pose 栈（Matrix3x2）和 Scissor 栈（ScreenRectangle，仅记录到 RenderState 不强制分段）
  └─ Submit 提交到 GuiRenderState（支持嵌套录制 _recordingStack + ReplayRange cache replay）
        │
        ▼
RenderState 数据层（Render/RenderState/ + Render/GuiRenderState.cs）
  ├─ GuiRenderState：node tree + strata 容器（NextStratum/BlurBeforeThisStratum/Up/ForEachElement/SortElements/Reset）
  ├─ GuiElementRenderState 接口 + 子类（Blit/TiledBlit/ColoredRectangle/GlyphBlit/Pip）
  ├─ TextureSetup（最多 3 texture+sampler 绑定，noTexture/singleTexture/singleTextureWithLightmap 工厂）
  └─ 排序按 (scissorArea, pipeline.sortKey, textureSetup.sortKey) 字典序
        │
        ▼
Render Phase 执行层（GuiRenderer）
  ├─ PreparePip：PIP renderer.Prepare offscreen 渲染+blit（必须在主 cmd BeginRecording 之前调避免 vkQueueSubmit 嵌套）
  ├─ Prepare：preprocess → SortElements → AddElementsToMeshes(BEFORE_BLUR) → firstDrawIndexAfterBlur → AddElementsToMeshes(AFTER_BLUR)
  ├─ StagedVertexBuffer：AppendDraw(VertexFormat, PrimitiveTopology) 返回 Draw，Upload() 拼接 staging→vertex buffer
  ├─ GpuBufferPool：fence 异步回收跨帧复用 buffer
  ├─ AutoStorageIndexBuffer：QUADS 自动生成 0,1,2,2,3,0 索引
  └─ Draw：createRenderPass → ExecuteDrawRange(0, firstDrawIndexAfterBlur) → clearDepthTexture → processBlurEffect → ExecuteDrawRange(firstDrawIndexAfterBlur, end)
        │
        ▼
GPU 抽象层（根目录 IGpuDevice/ICommandEncoder/IRenderPass + PipelineCache）
  ├─ IGpuDevice：CreateCommandEncoder/CreateSampler/CreateTexture/CreateBuffer/PrecompilePipeline
  ├─ ICommandEncoder：CreateRenderPass/CopyToBuffer/WriteToTexture/CreateFence/Submit
  ├─ IRenderPass：SetPipeline/BindTexture/SetUniform/SetVertexBuffer/SetIndexBuffer/EnableScissor/DrawIndexed/PushDebugGroup
  └─ Vulkan 后端（Vulkan/ 16 文件）实现上述接口
```

## 实现

### GPU 抽象接口（根目录 14 个 cs）

- `GpuBackend.cs` 后端枚举
- `GpuContext.cs` GPU 上下文抽象
- `GpuDevice.cs` 设备抽象（对标原版 GpuDevice SPI 模式）
- `ICommandEncoder.cs` 命令编码器接口（对标原版 CommandEncoder，分离自旧 GpuCommandBuffer）
- `IRenderPass.cs` render pass 接口（对标原版 RenderPass，暴露 SetPipeline/BindTexture/SetUniform/SetVertexBuffer/SetIndexBuffer/EnableScissor/DrawIndexed/PushDebugGroup）
- `GpuCommandBuffer.cs` 旧命令缓冲兼容层（[Obsolete] 保留供 VulkanAppBase 子类过渡）
- `GpuBuffer.cs`/`GpuImage.cs` GPU 资源抽象
- `GpuShader.cs` 着色器抽象
- `GpuDescriptorSet.cs` 描述符集抽象
- `RenderPipeline.cs` 旧 pipeline 描述（兼容层）
- `PipelineCache.cs` pipeline 缓存（`Dictionary<RenderPipeline, CompiledPipeline>`，PrecompilePipeline 用 GetOrAdd，运行时零 shader 编译）
- `DeviceLimits.cs` 设备限制查询
- `EmptyGpuContext.cs` 空实现供无 GPU 环境用
- `GpuLogger.cs` IGpuLogger 默认实现（解耦 NetCraft.Logging，构造注入 VulkanGuiApp）

### Vulkan 实现（Vulkan/ 16 个 cs）

- `VulkanGpuContext.cs`/`VulkanGpuDevice.cs` Vulkan 上下文与设备（对标原版 VulkanDevice，含 VMA 分配）
- `VulkanCommandBuffer.cs` 旧命令缓冲实现（兼容层）
- `VulkanCommandEncoder.cs` ICommandEncoder 实现（封装 VulkanCommandBuffer，对标原版 CommandEncoder）
- `VulkanRenderPass.cs` IRenderPass 实现（对标原版 RenderPass，dynamic rendering 用 VkPipelineRenderingCreateInfoKHR 无 VkRenderPass 对象，pipeline 双 handle withDepth/withoutDepth）
- `VulkanBuffer.cs` 缓冲实现，按 Usage 选内存属性：UniformBuffer 走 HostVisible 直接 map+memcpy，VertexBuffer/IndexBuffer 走 DeviceLocal 通过 staging 中转；hostVisible 重载供 GUI vertex buffer 每帧 map+memcpy 避免 staging 的 QueueSubmit 同步开销
- `VulkanImage.cs` 图像实现，CreateView 按 Usage 选 aspectMask，Upload 支持深度图布局转换
- `VulkanRenderPipeline.cs` pipeline 编译 + 缓存（对标原版 VulkanRenderPipeline，PrecompilePipeline 用 computeIfAbsent 编译并缓存 VkPipeline）
- `VulkanDescriptorSet.cs` 描述符集实现
- `VulkanShader.cs` 着色器实现（GLSL → SPIR-V 通过 Veldrid.SPIRV，注入 shaderDefines）
- `SpirvShaders.cs` 内嵌 SPIR-V 字节码
- `VulkanAppBase.cs` Vulkan 应用公共抽象基类，封装 swapchain/imageview/sync/DrawFrame/Cleanup 全部通用逻辑。MaxFramesInFlight=2 双帧并行，监听窗口 Resize 触发 RecreateSwapchain。**阶段 7 Tick/Render 解耦**：移除 FrameUpdate 事件订阅，`Run()` 只 `_window.Render += DrawFrame`，新增 `OnBeforeRun`/`OnAfterRun` 钩子供子类启动/Join Tick 线程。Render 仍在 `_window.Run` 内串行（阶段 7 部分完成）
- `VulkanTriangleApp.cs` 三角形渲染 PoC
- `VulkanCubeApp.cs` 3D 立方体示例，36 顶点+36 索引+MVP uniform buffer，验证深度测试+MVP uniform+索引绘制
- `VulkanGuiApp.cs` GUI 渲染主程序，**阶段 5 切换到 GuiRenderContext+GuiRenderer+GuiResourceManager 新路径**。submission 阶段调 Window.Render(GuiRenderContext) 提交 RenderState，render 阶段调 GuiRenderer.Draw。持有 blur 后处理资源（_blurOffscreen/_blurTemp/_blurPipeline 等，BeforeBlur 段渲染到 offscreen 水平+垂直 blur，AfterBlur 段 LoadOp=Load 保留模糊背景叠加 GUI）。**阶段 7** 持有 `_latestSnapshot`（volatile）双缓冲 Tick 写 Render 读 + `FrameTick` 事件由 Tick 线程 20tps 触发替代旧 FrameUpdate。P15 持有 `ItemItemAtlas` 3D 物品图集 + `ItemPipRenderer` 超大物品 PIP 离屏渲染器
- `VulkanItemAtlasApp.cs` ItemAtlas 独立测试程序，渲染所有注册物品到各自 slot 验证多 slot 共享 atlas 互不干扰

### Render 核心（Render/ 16 个 cs）

- `GuiRenderer.cs` render phase 主入口对标原版 GuiRenderer。Prepare 按 pipeline+texture+scissor 分组合批，同组元素写入同一 Draw 顶点 drawIndexed 一次提交。blur 分段先合批 BeforeBlur 再合批 AfterBlur，`FirstMeshIndexAfterBlur` 标记分段点。`DrawCallCount`/`MeshCount`/`VertexCount` 性能指标暴露。`PreparePip` 在 Render 线程调 PIP renderer.Prepare offscreen 渲染+blit
- `GuiRenderContext.cs` submission 层 IGuiRenderContext 实现，把控件 DrawQuad/DrawText/DrawImage 调用转换为 RenderState 提交到 GuiRenderState。坐标系统一用 actual 像素（scaled 坐标 * guiScale 转 actual）。`_recordingStack` 支持嵌套录制子控件 cache 和 window cache 同时活跃。F7 DrawText 优先用 GlyphFont 动态烘焙路径 FontAtlas 为 fallback。P0 DrawSprite 由 GuiSpriteManager 解析.mcmeta 后按 scaling 分派
- `GuiRenderState.cs` RenderState 容器，node tree + strata（NextStratum/BlurBeforeThisStratum/Up/ForEachElement/SortElements/Reset）。`ForEachPictureInPicture` 供 PreparePip 遍历
- `GuiResourceManager.cs` 资源管理器，textureId 注册 + descriptor set 分配 + swapchain 重建时 ClearCache
- `StagedVertexBuffer.cs` 顶点暂存，AppendDraw 返回 Draw，GetVertexBuilder 返回 IVertexConsumer，Upload() 拼接 staging→vertex buffer，GetExecuteInfo 返回 ExecuteInfo
- `GpuBufferPool.cs` fence 异步回收跨帧复用 buffer
- `AutoStorageIndexBuffer.cs` QUADS 自动生成 0,1,2,2,3,0 索引
- `IVertexConsumer.cs` 顶点消费接口（AddVertexWith2DPose 等）
- `TextureSetup.cs` 最多 3 texture+sampler 绑定，noTexture/singleTexture/singleTextureWithLightmap 工厂
- `ScreenRectangle.cs` bounds 相交判断
- `TraverseRange.cs` 遍历范围枚举（BeforeBlur/AfterBlur/All）
- `PoseStack.cs` Matrix3x2 pose 栈
- `Projection.cs` 投影矩阵工具
- `Lighting.cs`/`LightTexture.cs` 光照 + 光照贴图（对标原版 LightTexture，PIP 3D 物品渲染用）
- `ItemTextureAtlas.cs` 物品纹理图集入口
- `BakedModel.cs` BakedModel 纯数据容器（对标原版 BakedModel，chunk mesh 生成用）

### RenderState 值对象（Render/RenderState/ 6 个 cs）

- `GuiElementRenderState.cs` 接口（BuildVertices/Pipeline/TextureSetup/ScissorArea）
- `BlitRenderState.cs` 纹理 blit（对标原版 BlitRenderState）
- `TiledBlitRenderState.cs` 平铺 blit（对标原版 TiledBlitRenderState）
- `ColoredRectangleRenderState.cs` 纯色矩形（对标原版 ColoredRectangleRenderState，GUI/GUI_INVERT pipeline）
- `GlyphBlitRenderState.cs` 字形 blit（对标原版 GlyphRenderState）
- `Pip/PictureInPictureRenderState.cs` PIP 离屏渲染状态（对标原版 PictureInPictureRenderState，GUI 嵌 3D 内容如玩家皮肤预览）

### Atlas 图集（Render/Atlas/ 7 个 cs）

- `DynamicAtlasAllocator.cs` 动态图集分配器（对标原版 DynamicAtlasAllocator，按 K 分配 slot）
- `BitSet.cs` 图集占用位集
- `GuiItemAtlas.cs` GUI 物品图集（对标原版 GuiItemAtlas，物品图标动态拼合）
- `ItemItemAtlas.cs` 3D 物品图集（P15 实现，渲染所有注册物品到各自 slot 共享 atlas，LoadOp=Load 保留累积内容，depth 每次 Clear）
- `TextureAtlasSprite.cs` 纹理图集 sprite
- `TextureStitcher.cs` 纹理拼接算法（扫描 JSON 收集 sprite 路径列表拼接）
- `BlockTextureAtlas.cs` 方块纹理图集

### Item 3D 物品渲染（Render/Item/ 5 个 cs）

- `BakedQuad.cs` BakedQuad 纯数据容器（对标原版 BakedQuad）
- `CubeModel.cs` 立方体模型（6 面 24 顶点，PoC 用）
- `VertexConsumer3D.cs` 3D 顶点消费器，PutBakedQuad CPU 端 Vector3.Transform 把顶点变换到 world 空间（Blaze3d 设计：CPU 端 PoseStack transform 顶点，shader 端 Model=Identity）
- `ItemStackRenderState.cs` 物品栈渲染状态
- `ItemFeatureRenderer.cs` 物品特性渲染器

### PIP 离屏渲染（Render/Pip/ 2 个 cs）

- `PictureInPictureRenderer.cs` PIP 渲染器抽象基类（对标原版 PictureInPictureRenderer，泛型 `<T> where T : PictureInPictureRenderState`，needsResize 语义，Prepare 在 needsResize=false 时跳过避免每帧重建 GpuImage 泄漏 descriptor pool）
- `ItemPipRenderer.cs` 超大物品 PIP 离屏渲染器（P15 实现，EnsureTexturesAndProjection 复用 texture 修复 descriptor pool 耗尽，60/300 帧稳定）

### Pipeline 声明式系统（Pipeline/ 12 个 cs）

- `RenderPipeline.cs` 声明式 pipeline record（location/vertexShader/fragmentShader/shaderDefines/bindGroupLayouts/depthStencilState/polygonMode/cull/colorTargetStates/vertexFormatPerBuffer/primitiveTopology/sortKey）
- `Snippet.cs` pipeline 片段（全 Optional 字段）可叠加组合，后者的非空字段覆盖前者
- `PipelineBuilder.cs` builder + From(snippet1, snippet2) 合并 + WithXxx 链式 + BuildSnippet/Build
- `BindGroupLayout.cs`/`DepthStencilState.cs`/`ColorTargetState.cs`/`BlendFunction.cs`/`BlendEquation.cs` pipeline 子结构
- `PipelineEnums.cs` 枚举（PrimitiveTopology/CullMode/PolygonMode/CompareOp 等）
- `ShaderDefines.cs` shader 宏定义
- `ShaderManager.cs` GLSL 加载 + Veldrid.SPIRV 编译为 SPIR-V + 缓存
- `VertexFormat.cs` 顶点格式（PositionColor/PositionColorTexture/PositionTexColor 等）

### GUI 控件系统（Gui/ 6 个 cs + Gui/Layouts/ 11 个 cs）

- `GuiWindow.cs` 顶层窗口，继承 GuiContainer。`FocusNext` 方法 Tab 键按 TabIndex 循环切换 TabStop 控件焦点
- `GuiContainer.cs` 容器控件，override Update 遍历可见子控件调用 Update。Render 自动 PushScissor/PopScissor 包裹子控件裁剪超出边界。Layout 属性 Update 前调 Measure 排列子控件
- `GuiControl.cs` 控件基类，`virtual void Update(double delta)` 默认空实现供子类重写推进动画状态。`TabStop/TabIndex` 属性参与 Tab 焦点导航
- `GuiControls.cs` 具体控件（Button/TextBox/Panel/Slider/Checkbox/Image 等）
- `GuiLayout.cs` 布局引擎旧接口（IGuiLayout + GuiLinearLayout 水平/垂直堆叠，对应原版 client.gui.layouts.LinearLayout 早期版本）
- `Gui/Layouts/LayoutElement.cs` 布局元素接口（对标原版 LayoutElement）
- `Gui/Layouts/Layout.cs`/`AbstractLayout.cs` 布局抽象基类
- `Gui/Layouts/GridLayout.cs`/`LinearLayout.cs`/`FrameLayout.cs`/`EqualSpacingLayout.cs`/`HeaderAndFooterLayout.cs` 5 种布局（对标原版 client.gui.layouts.* 全套）
- `Gui/Layouts/LayoutSettings.cs`/`Divisor.cs`/`SpacerElement.cs`/`CommonLayouts.cs` 布局设置/分隔器/占位/常用布局预设

### 字体系统（Font/ 19 个 cs + Gui/FontAtlas.cs）

- `Font.cs` 字体入口（对标原版 Font，F7 GlyphFont 动态烘焙路径）
- `FontSet.cs` 字体集（对标原版 FontSet，按 FontOption 查找 GlyphProvider）
- `FontOption.cs` 字体选项
- `GlyphProvider.cs`/`GlyphProviderType.cs` 字形提供者接口与枚举
- `GlyphProviderDefinition.cs`/`FontProviderDefinitionLoader.cs`/`FontProviderDefinitions.cs` 字体 providers 定义与加载（对标原版 client.gui.font.providers 链）
- `TtfGlyphProvider.cs`/`BitmapGlyphProvider.cs`/`UnihexGlyphProvider.cs`/`SpaceGlyphProvider.cs` 4 种字形提供者
- `AssetsFontResourceAccessor.cs` 字体资源访问器
- `SheetBakedGlyph.cs` 烘焙字形（对标原版 BakedSheetGlyph，renderChar 4 顶点偏移计算）
- `FontTexture.cs` 字体纹理
- `GlyphStitcher.cs` 字形拼接器（F7 动态烘焙到 atlas）
- `SpecialGlyphs.cs` 特殊字形
- `GlyphRenderOptions.cs`/`GlyphRenderTypes.cs` 字形渲染选项与类型
- `Gui/FontAtlas.cs` TTF 加载与 ASCII 32-126 + CJK U+4E00..U+9FFF 字符光栅化到 2048×2048 R8 atlas（fallback 路径，F7 GlyphFont 优先）

### 九宫格 Sprite（Sprite/ 4 个 cs）

- `GuiSprite.cs` GUI sprite（对标原版 GuiSprite）
- `GuiSpriteScaling.cs` 缩放模式（Stretch/Tile/NineSlice，对标原版 GuiSpriteScaling）
- `GuiSpriteManager.cs` sprite 管理器（按 identifier 缓存 GuiSprite 懒加载 PNG+.mcmeta，swapchain 重建时 ClearCache）
- `GuiMetadataSection.cs` .mcmeta 元数据段解析

### Shaders（Shaders/core/ 13 个 GLSL）

- `gui.vert/frag.glsl` GUI 矩形 pipeline
- `position_tex_color.vert/frag.glsl` 纹理+颜色 pipeline
- `text.vert/frag.glsl` 文本 pipeline
- `position_color.vert/frag.glsl` 纯色 pipeline
- `screenquad.vert.glsl` + `blit_screen.frag.glsl` 屏幕 blit pipeline
- `blur.frag.glsl` 高斯模糊后处理 pipeline
- `item_3d.vert/frag.glsl` 3D 物品 pipeline（atlas*tint*diffuse*lightmap 四重调制）

## 文件清单（~120 个 cs + 13 个 GLSL）

| 区域 | 数量 | 说明 |
|------|------|------|
| 根目录 GPU 抽象 | 14 | GpuBackend/Context/Device/Buffer/Image/Shader/DescriptorSet/CommandBuffer/ICommandEncoder/IRenderPass/RenderPipeline/PipelineCache/DeviceLimits/EmptyGpuContext/GpuLogger |
| Vulkan 实现 | 16 | GpuContext/GpuDevice/CommandBuffer/CommandEncoder/RenderPass/Buffer/Image/RenderPipeline/DescriptorSet/Shader/SpirvShaders/AppBase/TriangleApp/CubeApp/GuiApp/ItemAtlasApp |
| Render 核心 | 16 | GuiRenderer/GuiRenderContext/GuiRenderState/GuiResourceManager/StagedVertexBuffer/GpuBufferPool/AutoStorageIndexBuffer/IVertexConsumer/TextureSetup/ScreenRectangle/TraverseRange/PoseStack/Projection/Lighting/LightTexture/ItemTextureAtlas/BakedModel |
| RenderState 值对象 | 6 | GuiElementRenderState/BlitRenderState/TiledBlitRenderState/ColoredRectangleRenderState/GlyphBlitRenderState/Pip/PictureInPictureRenderState |
| Atlas 图集 | 7 | DynamicAtlasAllocator/BitSet/GuiItemAtlas/ItemItemAtlas/TextureAtlasSprite/TextureStitcher/BlockTextureAtlas |
| Item 3D 物品渲染 | 5 | BakedQuad/CubeModel/VertexConsumer3D/ItemStackRenderState/ItemFeatureRenderer |
| PIP 离屏渲染 | 2 | PictureInPictureRenderer/ItemPipRenderer |
| Pipeline 声明式 | 12 | RenderPipeline/Snippet/PipelineBuilder/BindGroupLayout/DepthStencilState/ColorTargetState/BlendFunction/BlendEquation/PipelineEnums/ShaderDefines/ShaderManager/VertexFormat |
| GUI 控件 | 6 | GuiWindow/GuiContainer/GuiControl/GuiControls/GuiLayout/FontAtlas |
| GUI 布局 | 11 | LayoutElement/Layout/AbstractLayout/GridLayout/LinearLayout/FrameLayout/EqualSpacingLayout/HeaderAndFooterLayout/LayoutSettings/Divisor/SpacerElement/CommonLayouts |
| 字体系统 | 19 | Font/FontSet/FontOption/GlyphProvider/GlyphProviderType/GlyphProviderDefinition/FontProviderDefinitionLoader/FontProviderDefinitions/TtfGlyphProvider/BitmapGlyphProvider/UnihexGlyphProvider/SpaceGlyphProvider/AssetsFontResourceAccessor/SheetBakedGlyph/FontTexture/GlyphStitcher/SpecialGlyphs/GlyphRenderOptions/GlyphRenderTypes |
| 九宫格 Sprite | 4 | GuiSprite/GuiSpriteScaling/GuiSpriteManager/GuiMetadataSection |
| Shaders | 13 | gui/position_tex_color/text/position_color 各 vert+frag + screenquad.vert+blit_screen.frag + blur.frag + item_3d vert+frag |

## 关键设计决策

- **submission/render phase 分离**：对标原版 26.2 Blaze3D，submission 阶段构造不可变 RenderState 提交到 GuiRenderState，render phase 按 (pipeline, texture, scissor) 排序合批。同组元素合并为 1 个 DrawCall，DrawCall 数从 N（控件数）降到 1~5
- **pipeline cache**：`PipelineCache` (`Dictionary<RenderPipeline, CompiledPipeline>`)，`PrecompilePipeline` 用 GetOrAdd 编译并缓存 VkPipeline，运行时零 shader 编译
- **dynamic rendering**：Vulkan 后端用 `VkPipelineRenderingCreateInfoKHR` 无 VkRenderPass 对象，pipeline 双 handle (withDepth/withoutDepth)
- **VulkanBuffer 内存分类**：UniformBuffer 高频 CPU 写走 HostVisible，VertexBuffer/IndexBuffer 一次创建只读走 DeviceLocal+staging；GUI 每帧更新的 vertex buffer 用 HostVisible 重载避免 staging 中转的 QueueSubmit+QueueWaitIdle 同步开销
- **Blaze3d 顶点变换设计**：CPU 端 PoseStack transform 顶点（`VertexConsumer3D.PutBakedQuad` 已在 CPU 端 `Vector3.Transform(quad.Position(i), pose)` 把顶点变换到 world 空间），shader 端 Model=Identity。MVP UBO 的 Model 改 Identity，**System.Numerics row-major 与 GLSL column-major 内存兼容**：直接 memcpy 时 GLSL `m*v`（列向量乘）等价 CPU `v*M`（行向量乘），**不要 transpose**（transpose 反而变成 CPU `M*v` 导致 w 分量符号错误，顶点全被视体裁剪）
- **多 slot 共享 atlas 必须 LoadOp=Load**：每个 slot 的 DrawToSlot 只应 clear/渲染当前 slot 区域，用 LoadOp=Clear 会清空整个 atlas 覆盖其他 slot。初始 clear 一次后所有 DrawToSlot 用 Load 保留累积内容。depth 可每次 Clear 不影响 color 内容
- **PIP offscreen vkQueueSubmit 不能嵌套**：PIP offscreen 渲染的 Submit 必须在主 cmd BeginRecording 之前完成（`PreparePip` 在 `OnRecordCommandBuffer` 调用），否则驱动上下文冲突崩溃
- **Silk.NET Dispose 容错**：长时间运行后 Run 返回 IsInsideRenderLoop 可能残留 true，Dispose 抛 InvalidOperationException。Vulkan 资源已在 Cleanup 释放，窗口 GLFW 句柄由进程退出回收，try-catch 忽略此第三方库错误
- **Vulkan 纹理 V=0 顶部**：所有 UV 计算和测试断言必须用 Vulkan 方向（V0=0 顶部）不能用 OpenGL 方向（V0=1 顶部）
- **VulkanAppBase 阶段 7 Tick/Render 解耦**：移除 FrameUpdate 事件订阅，`Run()` 只 `_window.Render += DrawFrame`，新增 `OnBeforeRun`/`OnAfterRun` 钩子供子类启动/Join Tick 线程。Tick 线程 20tps 独立，通过 `FrameTick` 事件驱动业务更新，通过 `_latestSnapshot`（volatile）双缓冲发布给 Render 线程。**Render 仍在 `_window.Run` 内串行**（阶段 7 部分完成，剩余优化是 Render 也独立线程）

## 验证

- 0 编译错误
- `dotnet run --project NetCraft.Test -- gpugui`：24/24 通过
  - Vulkan 三角形/立方体各 60+300 帧稳定性
  - GUI 控件交互 4 个（Button click/TextBox input/Panel children/Button hover）
  - 嵌套 scissor 300 帧
  - 反色管线 60 帧
  - PIP offscreen+blit 60/300 帧 PASS（descriptor 泄漏修复后稳定）
  - PIP 性能基准 PASS（RenderCpu=6.4ms < 10ms DrawCall=2）
  - ItemAtlas 60/300 帧 + DrawToSlot 像素验证 + 光照验证全 PASS
  - 多 slot 共享 atlas 所有注册物品渲染到各自 slot
  - WriteToTexture 4x4 全红像素写入
- `dotnet run --project NetCraft.Test -- guilogic`：34/34 通过
  - 控件 Update 派发 + ScreenManager 生命周期 + Slider/Checkbox 交互
  - Scissor/Pose 栈 + LinearLayout 布局
  - DrawQuadInverted 计数 + 文本对齐 + Tab 焦点导航
- `dotnet run --project NetCraft.Test`：1163/1163 全过无回归

### 性能指标（P15 验收）

| 场景 | RenderCpu | DrawCall | Mesh | Vertex | TickRate | PipelineHit/Miss |
|---|---|---|---|---|---|---|
| 非 PIP GUI | 0.236ms | 4 | 4 | 272 | 18tps | 480/10 |
| PIP 场景 | 6.4ms | 2 | 2 | 8 | — | 239/11 |

PIP 6ms 开销主要是 offscreen 每帧独立 vkQueueSubmit + fence wait 同步开销，非合批开销。阶段 8 PIP offscreen 双缓冲优化可消除此同步开销。

## 规划

- **世界渲染**（GPU 剩余 5%）：chunk mesh 生成 + 实体模型渲染，对应原版 `net.minecraft.client.renderer` 层，归属 NetCraft.Game/Client/Render/
- **PIP offscreen 双缓冲优化**：消除每帧 Submit+fence wait 同步开销（GPU 剩余 5%）
- **阶段 7 Render 也独立线程**：当前 Tick 已独立 20tps 线程，Render 仍在窗口线程串行。完整解耦需 Render 线程独立拉取 `_latestSnapshot` 快照提交，vsync 节流，与 Tick 互不阻塞
- **多后端抽象**：未来 DX/Metal 后端复用 ICommandEncoder/IRenderPass 等接口
- 关联文档：`minecraft/GPU模块重构技术规划.md`（重构动机/目标架构/8 阶段计划/验收标准/原版 26.2 架构参考）

## 原版 Minecraft 26.2 GUI 架构分析

### 三层分层

| 层 | 包路径 | 职责 | NetCraft 对应 |
|----|--------|------|---------------|
| 渲染内核层 | `com.mojang.blaze3d.*` | GPU 资源抽象+渲染状态管理+窗口+字形数据结构 | NetCraft.Gpu 根 + Vulkan/ + Pipeline/ + Font/ |
| 业务 GUI 框架层 | `net.minecraft.client.gui.*` | 控件框架+事件+布局+字体渲染+屏幕+Tooltip/Narration | NetCraft.Gpu/Gui/ + NetCraft.Game/Gui/（当前混层） |
| 世界渲染层 | `net.minecraft.client.renderer.*` | 实体/方块/世界渲染，与 GUI 关联度低 | NetCraft.Game/Client/Render/（待补） |

### blaze3d 渲染内核层职责

仅提供 GUI 渲染所需的底层基础，**不含任何控件管理、事件系统、布局引擎**：

- `RenderSystem`：scissor/projection/blend 等全局渲染状态控制
- `Window`：窗口与表面尺寸（guiScaledWidth/guiScaledHeight）
- `GpuTexture`/`GpuTextureView`/`GpuSampler`：GPU 资源抽象
- `RenderPipeline`：渲染管线描述
- `GlyphInfo`：字形数据结构（仅数据，不含渲染逻辑）

### client/gui 业务 GUI 框架层职责

完整 GUI 控件框架，依赖 `Minecraft.getInstance()` 业务实例和注册表 API：

- **控件框架**：`AbstractWidget`（implements LayoutElement/Renderable/GuiEventListener/NarratableEntry）+ `Button`/`EditBox`/`AbstractScrollWidget` 等具体控件
- **渲染上下文**：`GuiGraphicsExtractor`（原 GuiGraphics）桥接控件与底层 RenderSystem，提供 fill/text/blit/scissor/pose/itemStack/tooltip 等渲染接口，持有 `Minecraft` 与 `GuiRenderState`
- **事件系统**：`GuiEventListener`/`ContainerEventHandler`/`FocusNavigationEvent`/`ScreenRectangle`
- **布局引擎**：`LayoutElement`/`GridLayout`/`LinearLayout`/`LayoutSettings`
- **字体渲染**：`Font`/`FontSet`（实际渲染逻辑，blaze3d 仅提供字形数据）
- **屏幕系统**：`Screen` + `TitleScreen`/`OptionsScreen` 等业务屏幕
- **辅助系统**：`Tooltip`/`WidgetTooltipHolder` + `NarratableEntry`/`NarrationElementOutput` 无障碍

### 控件管理归属结论

**原版控件管理位于 client/gui 业务框架层，不属于 blaze3d 内核渲染层**。证据：

1. `AbstractWidget` 构造与 `mouseClicked` 直接 import 并调用 `Minecraft.getInstance()` 和 `SoundManager`
2. `GuiGraphicsExtractor` 构造时持 `Minecraft` 实例与 `AtlasManager`，访问 `AtlasIds.GUI` 业务注册表
3. 布局引擎 `GridLayout` 位于 `client.gui.layouts`，非 blaze3d
4. 控件 4 接口（LayoutElement/Renderable/GuiEventListener/NarratableEntry）全是 client/gui 内部接口

### 关键架构特征

- **渲染状态收集与提交分离**：控件 `extractRenderState` 只收集渲染状态塞入 `GuiRenderState`，由渲染层后续批量提交，非 immediate 模式
- **多 RenderPipeline 切换**：`RenderPipelines.GUI`/`GUI_INVERT`/`GUI_TEXT_HIGHLIGHT` 等多种管线在同一 RenderPass 内切换
- **矩阵栈与裁剪栈**：`Matrix3x2fStack pose()` 做变换嵌套，`ScissorStack` 做裁剪嵌套
- **Minecraft 业务语义紧密耦合**：控件层直接访问 Minecraft/SoundManager/AtlasManager/ItemStack/Component
