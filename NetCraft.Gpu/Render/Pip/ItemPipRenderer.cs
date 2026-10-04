using System.Numerics;
using System.Runtime.InteropServices;
using NetCraft.Gpu.Pipeline;

namespace NetCraft.Gpu;

//ItemPipState PIP 物品渲染状态对标原版 OversizedItemRenderState
//承载超出图集槽位的超大物品 3D 渲染到 offscreen 后 blit 到 GUI
//PoC 用 ItemIdentity 标识物品 renderer 查 RegisterItem 映射到 ItemStackRenderState
public sealed record ItemPipState(
    int X0, int Y0, int X1, int Y1,
    float Scale,
    ScreenRectangle ScissorArea,
    Matrix3x2 Pose,
    object ItemIdentity,
    float RotationY)
    : PictureInPictureRenderState
{
    public ScreenRectangle Bounds => PictureInPictureRenderState.GetBounds(X0, Y0, X1, Y1, ScissorArea);
}

//ItemPipRenderer PIP 物品渲染器子类对标原版 OversizedItemRenderer
//RenderToTexture 用 PoseStack 把物品 translate/scale/rotate 到 offscreen 中心
//item.submit 后 ItemFeatureRenderer.Execute 写顶点 然后上传 GPU+录制 Vulkan 命令渲染到 OffscreenTexture
//EnsureTexturesAndProjection 创建 offscreen texture+depth+清屏+透视投影+编译 pipeline
//BlitTexture 把 offscreen texture 作为 BlitRenderState 加到 guiRenderState
public sealed class ItemPipRenderer : PictureInPictureRenderer<ItemPipState>
{
    [StructLayout(LayoutKind.Sequential)]
    private struct MvpUniform
    {
        public Matrix4x4 Model;
        public Matrix4x4 View;
        public Matrix4x4 Proj;
    }

    private readonly GpuDevice _device;
    private readonly PoseStack _poseStack = new();
    private readonly Projection _projection = new();
    private readonly ItemSubmitCollector _collector = new();
    private readonly Dictionary<object, TrackingItemStackRenderState> _items = new();
    //最近一次 RenderToTexture 的顶点数据供测试诊断
    public VertexConsumer3D LastFrameVertices { get; } = new();

    //GPU 渲染资源 EnsureTexturesAndProjection 时懒初始化 尺寸变化时重建
    private Lighting? _lighting;
    private LightTexture? _lightTexture;
    private ItemTextureAtlas? _itemAtlas;
    private GpuBuffer? _mvpUbo;
    private GpuDescriptorLayout? _mvpLayout;
    private GpuDescriptorSet? _mvpSet;
    private GpuDescriptorLayout? _lightmapLayout;
    private GpuDescriptorSet? _lightmapSet;
    private GpuDescriptorLayout? _atlasLayout;
    private GpuDescriptorSet? _atlasSet;
    private CompiledRenderPipeline? _pipeline;
    private GpuBuffer? _vertexBuffer;
    private GpuBuffer? _indexBuffer;
    //_blitSampler offscreen→GUI blit 采样器线性过滤跨帧复用避免每帧 CreateSampler 泄漏
    private GpuSampler? _blitSampler;
    private int _pipelineWidth;
    private int _pipelineHeight;
    //双缓冲 offscreen texture+depth+encoder 乒乓本帧写 _writeIndex blit 读 _readIndex
    //_readIndex=-1 首帧无历史 blit 当前写的同 queue submission order 保证 GPU 端依赖
    //异步 SubmitAsync 不等 GPU CPU 继续录主 cmd 双 encoder 轮转避免 command buffer 复用竞争
    private readonly GpuImage?[] _offscreenTextures = new GpuImage[2];
    private readonly GpuImage?[] _offscreenDepths = new GpuImage[2];
    private readonly ICommandEncoder?[] _encoders = new ICommandEncoder[2];
    private int _writeIndex;
    private int _readIndex = -1;

    public override Type RenderStateClass => typeof(ItemPipState);

    public ItemPipRenderer(GpuDevice device) => _device = device;

    //RegisterItem 注册物品 PoC 用 CubeModel 程序化生成模型
    public TrackingItemStackRenderState RegisterItem(object identity, float modelSize = 1f)
    {
        if (_items.TryGetValue(identity, out var existing)) return existing;
        var state = new TrackingItemStackRenderState();
        state.AppendModelIdentityElement(identity);
        state.SetQuads(CubeModel.Create(modelSize));
        _items[identity] = state;
        return state;
    }

    //EnsureTexturesAndProjection 创建双 offscreen texture+depth+清屏+透视投影+编译 pipeline
    //基类 Prepare 在 needsResize=false 时仍调本方法 此时双 texture 已存在只更新投影避免每帧重建
    //每帧重建会导致 offscreen texture 泄漏+descriptor set 泄漏 pool 耗尽
    //pipeline viewport 固定到 offscreen 尺寸 尺寸变化时重新编译
    //SupportsGpuRendering=false 时仅创建 texture+设置投影走 CPU 路径供单测 不调 EnsureGpuResources/clear
    //双 encoder 跨 resize 复用 不在 DisposeTextures 释放避免重建开销
    protected override void EnsureTexturesAndProjection(int width, int height)
    {
        //双 texture 都已创建复用只更新投影
        if (_offscreenTextures[0] is not null && _offscreenTextures[1] is not null)
        {
            _projection.SetupPerspective(0.05f, 1000f, MathF.PI / 4f, width, height);
            return;
        }
        for (var i = 0; i < 2; i++)
        {
            _offscreenTextures[i] = _device.CreateImage(new GpuImageDescription
            {
                Width = width,
                Height = height,
                Format = GpuImageFormat.R8G8B8A8Unorm,
                Usage = GpuImageUsage.ColorAttachment | GpuImageUsage.SampledImage
            });
            _offscreenDepths[i] = _device.CreateImage(new GpuImageDescription
            {
                Width = width,
                Height = height,
                Format = GpuImageFormat.D32Sfloat,
                Usage = GpuImageUsage.DepthAttachment
            });
        }
        //透视投影 offscreen 3D 渲染 MockDevice 也设置投影供测试
        _projection.SetupPerspective(0.05f, 1000f, MathF.PI / 4f, width, height);
        //基类 OffscreenTexture 设为首个 texture 供基类 needsResize 判断非 null
        OffscreenTexture = _offscreenTextures[0];
        OffscreenDepth = _offscreenDepths[0];
        //GPU 资源+初始 clear 仅 SupportsGpuRendering=true 时执行 MockDevice 走 CPU 路径仅生成顶点
        if (!_device.SupportsGpuRendering) return;
        //双 depth 初始 layout 转换 Undefined->DepthStencilAttachmentOptimal
        foreach (var depth in _offscreenDepths)
            depth!.Upload(ReadOnlySpan<byte>.Empty);
        //pipeline 尺寸变化时重新编译 viewport 固定到 offscreen 尺寸
        if (_pipeline == null || _pipelineWidth != width || _pipelineHeight != height)
        {
            _pipeline?.Dispose();
            _pipelineWidth = width;
            _pipelineHeight = height;
            EnsureGpuResources();
        }
        //双 encoder 跨帧复用异步 Submit 乒乓 首次创建后续 resize 复用
        _encoders[0] ??= _device.CreateCommandEncoder();
        _encoders[1] ??= _device.CreateCommandEncoder();
        //初始 clear 双 texture 到不透明黑避免首帧 blit 采样未定义内容
        for (var i = 0; i < 2; i++)
        {
            using var initEncoder = _device.CreateCommandEncoder();
            using var initPass = initEncoder.CreateRenderPass(_pipeline!, _offscreenTextures[i]!, new Vector4(0f, 0f, 0f, 1f), _offscreenDepths[i]!, 1.0f);
            initPass.Close();
            initEncoder.Submit();
        }
    }

    //EnsureGpuResources 创建 Lighting+LightTexture+ItemTextureAtlas+MVP UBO+descriptor sets+pipeline+index buffer
    private void EnsureGpuResources()
    {
        _lighting ??= new Lighting(_device);
        _lighting.SetupFor(Lighting.Entry.Items3D);
        _lightTexture ??= new LightTexture(_device);
        _itemAtlas ??= new ItemTextureAtlas(_device);

        if (_mvpUbo == null)
        {
            var mvpLayoutDesc = new GpuDescriptorLayoutDescription();
            mvpLayoutDesc.Bindings.Add(new GpuDescriptorBinding
            {
                Binding = 0,
                DescriptorType = GpuDescriptorType.UniformBuffer,
                StageFlags = GpuShaderStageFlags.Vertex
            });
            _mvpLayout = _device.CreateDescriptorLayout(mvpLayoutDesc);
            _mvpUbo = _device.CreateBuffer(192, GpuBufferUsage.UniformBuffer);
            _mvpSet = _device.AllocateDescriptorSet(_mvpLayout);
            _mvpSet.WriteBuffer(0, _mvpUbo, 0, -1);
        }
        if (_lightmapSet == null)
        {
            var lightmapLayoutDesc = new GpuDescriptorLayoutDescription();
            lightmapLayoutDesc.Bindings.Add(new GpuDescriptorBinding
            {
                Binding = 0,
                DescriptorType = GpuDescriptorType.CombinedImageSampler,
                StageFlags = GpuShaderStageFlags.Fragment
            });
            _lightmapLayout = _device.CreateDescriptorLayout(lightmapLayoutDesc);
            _lightmapSet = _device.AllocateDescriptorSet(_lightmapLayout);
            _lightmapSet.WriteImage(0, _lightTexture.Texture!, _lightTexture.Sampler!);
        }
        if (_atlasSet == null)
        {
            var atlasLayoutDesc = new GpuDescriptorLayoutDescription();
            atlasLayoutDesc.Bindings.Add(new GpuDescriptorBinding
            {
                Binding = 0,
                DescriptorType = GpuDescriptorType.CombinedImageSampler,
                StageFlags = GpuShaderStageFlags.Fragment
            });
            _atlasLayout = _device.CreateDescriptorLayout(atlasLayoutDesc);
            _atlasSet = _device.AllocateDescriptorSet(_atlasLayout);
            _atlasSet.WriteImage(0, _itemAtlas.Texture!, _itemAtlas.Sampler!);
        }
        if (_indexBuffer == null)
        {
            var indices = GenerateQuadIndices(6);
            _indexBuffer = _device.CreateHostVisibleBuffer(indices.Length * sizeof(ushort), GpuBufferUsage.IndexBuffer);
            _indexBuffer.Upload<ushort>(indices);
        }
        //_blitSampler 线性过滤 offscreen 3D 渲染结果 blit 到 GUI 平滑跨帧复用
        _blitSampler ??= _device.CreateSampler(new GpuSamplerDescription
        {
            LinearFilter = true,
            RepeatAddress = false
        });

        var item3dDesc = RenderPipelineDescription.FromDeclaration(RenderPipelines.ITEM_3D, _device.ShaderManager);
        item3dDesc.TargetWidth = _pipelineWidth;
        item3dDesc.TargetHeight = _pipelineHeight;
        foreach (var layoutDesc in item3dDesc.DescriptorLayoutDescriptions)
            item3dDesc.DescriptorLayouts.Add(_device.CreateDescriptorLayout(layoutDesc));
        item3dDesc.DescriptorLayoutDescriptions.Clear();
        _pipeline = _device.CreateRenderPipeline(item3dDesc);
    }

    //RenderToTexture 渲染 3D 物品到 offscreen
    //poseStack translate 到 offscreen 中心 + 后退 + scale + rotate
    //item.submit 后 ItemFeatureRenderer.Execute 写顶点 然后上传 GPU + 录制 Vulkan 命令渲染
    protected override void RenderToTexture(ItemPipState renderState)
    {
        var width = _pipelineWidth;
        var height = _pipelineHeight;
        _poseStack.SetIdentity();
        //translate 到 offscreen 中心 + 后退让物品在视口内
        _poseStack.Translate(width / 2f, height / 2f, -3f);
        //按 Scale 缩放
        _poseStack.Scale(renderState.Scale, renderState.Scale, renderState.Scale);
        //按 RotationY 旋转 Y 轴
        if (renderState.RotationY != 0f)
            _poseStack.Rotate(Quaternion.CreateFromAxisAngle(Vector3.UnitY, renderState.RotationY));

        if (_items.TryGetValue(renderState.ItemIdentity, out var item))
        {
            _collector.Nodes.Clear();
            item.Submit(_poseStack, _collector,
                ItemFeatureRenderer.FullBright, ItemFeatureRenderer.NoOverlay, 0);
            LastFrameVertices.Clear();
            ItemFeatureRenderer.Execute(_collector, LastFrameVertices);
        }

        if (LastFrameVertices.VertexCount == 0) return;
        //SupportsGpuRendering=false 时仅生成顶点供单测不录制 GPU 命令 MockDevice.CreateCommandEncoder 抛 NotSupportedException
        if (_device.SupportsGpuRendering)
            RenderToGpu();
    }

    //RenderToGpu 上传顶点 + 更新 MVP UBO + 录制 Vulkan 命令渲染到 _offscreenTextures[_writeIndex]
    //双 encoder 乒乓本帧用 _encoders[_writeIndex] 上轮该 encoder 的命令已 SubmitAsync
    //WaitForCompletion 等上轮完成才能 Reset 复用首次未 SubmitAsync 跳过
    //SubmitAsync 异步提交不等 GPU CPU 继续录主 cmd 同 queue submission order 保证 GPU 端依赖
    //model=Identity 因 VertexConsumer3D.PutBakedQuad 已在 CPU 端 apply pose
    //透视投影需翻转 Y 和转换 Z 范围匹配 Vulkan
    private void RenderToGpu()
    {
        var vertices = LastFrameVertices.Vertices;
        var vertexBytes = VerticesToBytes(vertices);
        if (_vertexBuffer == null || _vertexBuffer.Size < vertexBytes.Length)
        {
            _vertexBuffer?.Dispose();
            _vertexBuffer = _device.CreateHostVisibleBuffer(vertexBytes.Length, GpuBufferUsage.VertexBuffer);
        }
        _vertexBuffer.Upload<byte>(vertexBytes);

        var proj = _projection.GetMatrix();
        proj.M22 *= -1;
        proj.M42 *= -1;
        proj.M33 *= 0.5f;
        proj.M43 = proj.M43 * 0.5f + 0.5f;
        var mvp = new MvpUniform
        {
            Model = Matrix4x4.Identity,
            View = Matrix4x4.Identity,
            Proj = proj
        };
        _mvpUbo!.Upload<MvpUniform>(new[] { mvp });

        var idx = _writeIndex;
        var encoder = _encoders[idx]!;
        //等上一轮该 encoder 完成才能 Reset command buffer 复用首次未 SubmitAsync 跳过
        encoder.WaitForCompletion();
        encoder.BeginRecording();
        //本帧写 texture 上帧末尾转 ShaderReadOnly 供 blit 本帧渲染前转回 ColorAttachment
        //首次渲染初始 clear 后已 ColorAttachmentOptimal TransitionImageLayout 内部跳过 no-op
        encoder.TransitionImageLayout(_offscreenTextures[idx]!, GpuImageLayout.ColorAttachment);
        var clearColor = new Vector4(0f, 0f, 0f, 1f);
        using var pass = encoder.CreateRenderPass(_pipeline!, _offscreenTextures[idx]!, clearColor, _offscreenDepths[idx]!, 1.0f, GpuLoadOp.Clear);
        pass.SetVertexBuffer(0, _vertexBuffer!);
        pass.SetIndexBuffer(_indexBuffer!, GpuIndexType.UInt16);
        pass.BindDescriptorSet(_mvpSet!, 0);
        pass.BindDescriptorSet(_lighting!.CurrentDescriptorSet!, 1);
        pass.BindDescriptorSet(_lightmapSet!, 2);
        pass.BindDescriptorSet(_atlasSet!, 3);
        //DynamicScissorEnabled=true 必须调 DisableScissor 设 scissor 到 pipeline extent 否则 DrawIndexed 崩溃
        pass.DisableScissor();
        //CubeModel 6 面 * 6 索引 = 36
        pass.DrawIndexed(36);
        pass.Close();
        //渲染后转 ShaderReadOnly 供 BlitTexture 采样 不转采样 ColorAttachmentOptimal 纹理驱动崩溃
        encoder.TransitionImageLayout(_offscreenTextures[idx]!, GpuImageLayout.ShaderReadOnly);
        //异步 Submit 不等 GPU 完成 CPU 继续录主 cmd blit 读上一帧 texture 无本帧依赖
        encoder.SubmitAsync();
    }

    //GetBlitTextureSetup 返回读 buffer 的 TextureSetup 供 BlitTexture blit 到 GUI
    //_readIndex=-1 首帧无历史 blit 当前写的(_writeIndex)同 queue submission order 保证主 cmd 在 PIP 后
    //_readIndex>=0 后续帧 blit 上一帧写的(_readIndex)已 SubmitAsync 完成无本帧依赖
    //_blitSampler 跨帧复用避免每帧 CreateSampler 泄漏 EnsureGpuResources 时懒创建
    protected override TextureSetup GetBlitTextureSetup()
    {
        var idx = _readIndex < 0 ? _writeIndex : _readIndex;
        var tex = _offscreenTextures[idx];
        return tex is not null && _blitSampler is not null
            ? TextureSetup.SingleTexture(tex, _blitSampler)
            : TextureSetup.NoTexture;
    }

    //BlitTexture override 基类加 BlitRenderState 后翻转读写索引实现乒乓
    //本帧写的变读下次写另一个双 encoder 轮转避免 command buffer 复用竞争
    //更新基类 OffscreenTexture 为新读 buffer 供基类 needsResize 判断
    protected override void BlitTexture(ItemPipState renderState, GuiRenderState guiRenderState)
    {
        base.BlitTexture(renderState, guiRenderState);
        _readIndex = _writeIndex;
        _writeIndex = 1 - _writeIndex;
        if (_readIndex >= 0 && _offscreenTextures[_readIndex] is not null)
        {
            OffscreenTexture = _offscreenTextures[_readIndex];
            OffscreenDepth = _offscreenDepths[_readIndex];
        }
    }

    //DisposeTextures override 释放双 texture+depth 尺寸变化或 Dispose 时调
    //先等双 encoder 完成才能释放 texture GPU 不再使用
    //encoder 跨 resize 复用不在此时释放避免重建开销
    protected override void DisposeTextures()
    {
        foreach (var enc in _encoders)
            enc?.WaitForCompletion();
        foreach (var tex in _offscreenTextures)
            tex?.Dispose();
        foreach (var depth in _offscreenDepths)
            depth?.Dispose();
        Array.Fill(_offscreenTextures, null);
        Array.Fill(_offscreenDepths, null);
        OffscreenTexture = null;
        OffscreenDepth = null;
        _readIndex = -1;
        _writeIndex = 0;
    }

    private static byte[] VerticesToBytes(List<float> vertices)
    {
        var span = CollectionsMarshal.AsSpan(vertices);
        return MemoryMarshal.AsBytes(span).ToArray();
    }

    private static ushort[] GenerateQuadIndices(int quadCount)
    {
        var indices = new ushort[quadCount * 6];
        for (int i = 0; i < quadCount; i++)
        {
            var baseVertex = i * 4;
            var offset = i * 6;
            indices[offset + 0] = (ushort)(baseVertex + 0);
            indices[offset + 1] = (ushort)(baseVertex + 1);
            indices[offset + 2] = (ushort)(baseVertex + 2);
            indices[offset + 3] = (ushort)(baseVertex + 2);
            indices[offset + 4] = (ushort)(baseVertex + 3);
            indices[offset + 5] = (ushort)(baseVertex + 0);
        }
        return indices;
    }

    protected override void OnDispose()
    {
        //双 encoder 释放 DisposeTextures 已 WaitForCompletion 此处 Dispose 不再等 GPU
        foreach (var enc in _encoders)
            enc?.Dispose();
        Array.Fill(_encoders, null);
        _vertexBuffer?.Dispose();
        _indexBuffer?.Dispose();
        _blitSampler?.Dispose();
        _atlasSet?.Dispose();
        _atlasLayout?.Dispose();
        _lightmapSet?.Dispose();
        _lightmapLayout?.Dispose();
        _mvpSet?.Dispose();
        _mvpLayout?.Dispose();
        _mvpUbo?.Dispose();
        _pipeline?.Dispose();
        _itemAtlas?.Dispose();
        _lightTexture?.Dispose();
        _lighting?.Dispose();
    }
}
