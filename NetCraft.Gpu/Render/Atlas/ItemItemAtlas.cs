using System.Numerics;
using System.Runtime.InteropServices;
using NetCraft.Gpu.Pipeline;

namespace NetCraft.Gpu;

//ItemItemAtlas 物品图集生产子类对标原版 GuiItemAtlas 的具体渲染逻辑
//DrawToSlot 用 PoseStack 把物品 translate/scale 到槽位中心 item.submit 后 ItemFeatureRenderer.Execute 写顶点
//PoC 用程序化 CubeModel 作物品模型不加载真模型 itemIdentity 映射到 TrackingItemStackRenderState
//SupportsGpuRendering=true 走 Vulkan 渲染到 AtlasTexture 否则仅 CPU 顶点生成供测试
public sealed class ItemItemAtlas : GuiItemAtlas
{
    //MvpUniform MVP 矩阵 uniform 块 model+view+proj 三个 mat4 column-major 上传到 shader
    [StructLayout(LayoutKind.Sequential)]
    private struct MvpUniform
    {
        public Matrix4x4 Model;
        public Matrix4x4 View;
        public Matrix4x4 Proj;
    }

    private readonly PoseStack _poseStack = new();
    private readonly Projection _projection = new();
    private readonly ItemSubmitCollector _collector = new();
    //缓存 itemIdentity → ItemStackRenderState 模拟 ItemModelResolver.update
    private readonly Dictionary<object, TrackingItemStackRenderState> _items = new();
    //最近一次 DrawToSlot 的顶点数据供测试/后续 GPU 上传
    public VertexConsumer3D LastFrameVertices { get; } = new();

    //GPU 渲染资源仅 SupportsGpuRendering=true 时初始化
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

    public ItemItemAtlas(GpuDevice device, int textureSize, int slotTextureSize)
        : base(device, textureSize, slotTextureSize)
    {
        if (device.SupportsGpuRendering)
            InitGpuResources();
    }

    //InitGpuResources 创建 Lighting + LightTexture + ItemTextureAtlas + MVP UBO + pipeline + 固定索引 buffer
    private void InitGpuResources()
    {
        _lighting = new Lighting(Device);
        _lighting.SetupFor(Lighting.Entry.Items3D);
        _lightTexture = new LightTexture(Device);
        _itemAtlas = new ItemTextureAtlas(Device);

        var mvpLayoutDesc = new GpuDescriptorLayoutDescription();
        mvpLayoutDesc.Bindings.Add(new GpuDescriptorBinding
        {
            Binding = 0,
            DescriptorType = GpuDescriptorType.UniformBuffer,
            StageFlags = GpuShaderStageFlags.Vertex
        });
        _mvpLayout = Device.CreateDescriptorLayout(mvpLayoutDesc);
        //3 个 mat4 = 192 bytes
        _mvpUbo = Device.CreateBuffer(192, GpuBufferUsage.UniformBuffer);
        _mvpSet = Device.AllocateDescriptorSet(_mvpLayout);
        _mvpSet.WriteBuffer(0, _mvpUbo, 0, -1);

        //lightmap sampler 绑定 set 2 binding 0 CombinedImageSampler
        var lightmapLayoutDesc = new GpuDescriptorLayoutDescription();
        lightmapLayoutDesc.Bindings.Add(new GpuDescriptorBinding
        {
            Binding = 0,
            DescriptorType = GpuDescriptorType.CombinedImageSampler,
            StageFlags = GpuShaderStageFlags.Fragment
        });
        _lightmapLayout = Device.CreateDescriptorLayout(lightmapLayoutDesc);
        _lightmapSet = Device.AllocateDescriptorSet(_lightmapLayout);
        _lightmapSet.WriteImage(0, _lightTexture.Texture!, _lightTexture.Sampler!);

        //物品纹理图集 sampler 绑定 set 3 binding 0 CombinedImageSampler
        var atlasLayoutDesc = new GpuDescriptorLayoutDescription();
        atlasLayoutDesc.Bindings.Add(new GpuDescriptorBinding
        {
            Binding = 0,
            DescriptorType = GpuDescriptorType.CombinedImageSampler,
            StageFlags = GpuShaderStageFlags.Fragment
        });
        _atlasLayout = Device.CreateDescriptorLayout(atlasLayoutDesc);
        _atlasSet = Device.AllocateDescriptorSet(_atlasLayout);
        _atlasSet.WriteImage(0, _itemAtlas.Texture!, _itemAtlas.Sampler!);

        //缓存编译产物避免每次 DrawToSlot 重复编译
        //extent 必须匹配 AtlasTexture 尺寸 否则 viewport 把顶点映射到错误位置被 scissor 裁掉
        var item3dDesc = RenderPipelineDescription.FromDeclaration(RenderPipelines.ITEM_3D, Device.ShaderManager);
        item3dDesc.TargetWidth = TextureSize;
        item3dDesc.TargetHeight = TextureSize;
        foreach (var layoutDesc in item3dDesc.DescriptorLayoutDescriptions)
            item3dDesc.DescriptorLayouts.Add(Device.CreateDescriptorLayout(layoutDesc));
        item3dDesc.DescriptorLayoutDescriptions.Clear();
        _pipeline = Device.CreateRenderPipeline(item3dDesc);

        //CubeModel 6 面 * 6 索引 = 36 索引固定一次性上传
        var indices = GenerateQuadIndices(6);
        _indexBuffer = Device.CreateHostVisibleBuffer(indices.Length * sizeof(ushort), GpuBufferUsage.IndexBuffer);
        _indexBuffer.Upload<ushort>(indices);

        //AtlasDepth 初始 layout 转换 Undefined->DepthStencilAttachmentOptimal
        //GuiItemAtlas 构造函数只 CreateImage 不 Upload 深度图布局是 Undefined
        //dynamic rendering 期望 DepthStencilAttachmentOptimal 需显式转换
        AtlasDepth.Upload(ReadOnlySpan<byte>.Empty);

        //初始 clear 整个 AtlasTexture 到不透明黑 后续 DrawToSlot 用 LoadOp=Load 保留其他 slot 内容
        //不初始 clear 的话首次 DrawToSlot 用 Load 会读到 Undefined 内容 validation 报错
        using (var initEncoder = Device.CreateCommandEncoder())
        {
            using var initPass = initEncoder.CreateRenderPass(_pipeline!, AtlasTexture, new Vector4(0f, 0f, 0f, 1f), AtlasDepth, 1.0f);
            initPass.Close();
            initEncoder.Submit();
        }
    }

    //RegisterItem 注册物品 PoC 用 CubeModel 程序化生成模型
    //完整版走 ItemModelResolver.update 从 ItemModel 填充 quads
    public TrackingItemStackRenderState RegisterItem(object identity, float modelSize = 1f)
    {
        if (_items.TryGetValue(identity, out var existing)) return existing;
        var state = new TrackingItemStackRenderState();
        state.AppendModelIdentityElement(identity);
        state.SetQuads(CubeModel.Create(modelSize));
        _items[identity] = state;
        return state;
    }

    //ReadbackSlot 把指定槽位的 AtlasTexture 像素读回 CPU 供集成测试验证渲染结果
    //返回 SlotTextureSize x SlotTextureSize x 4(RGBA) 字节数组
    //slotX/slotY 网格坐标对应 GetOrUpdate 分配的槽位
    //内部调 AtlasTexture.Readback 读整张图集再裁剪槽位区域
    public byte[] ReadbackSlot(int slotX, int slotY)
    {
        var fullPixels = AtlasTexture.Readback();
        var slotPixels = new byte[SlotTextureSize * SlotTextureSize * 4];
        var srcStride = TextureSize * 4;
        var dstStride = SlotTextureSize * 4;
        var srcOffsetY = slotY * SlotTextureSize;
        var srcOffsetX = slotX * SlotTextureSize;
        for (int y = 0; y < SlotTextureSize; y++)
        {
            var srcRow = (srcOffsetY + y) * srcStride + srcOffsetX * 4;
            var dstRow = y * dstStride;
            Array.Copy(fullPixels, srcRow, slotPixels, dstRow, dstStride);
        }
        return slotPixels;
    }

    //ReadbackFullAtlas 把整张 AtlasTexture 像素读回 CPU 供诊断测试
    //返回 TextureSize x TextureSize x 4(RGBA) 字节数组
    public byte[] ReadbackFullAtlas() => AtlasTexture.Readback();

    //DrawToSlot 渲染物品到槽位 对标原版 GuiItemAtlas.drawToSlot
    //poseStack translate 到槽位中心 scale(slotSize,-slotSize,slotSize) 翻转 Y
    //item.submit 后 ItemFeatureRenderer.Execute 写顶点到 LastFrameVertices
    //SupportsGpuRendering=true 时上传顶点 + 录制 Vulkan 命令渲染到 AtlasTexture 槽位区域
    protected override void DrawToSlot(int slotX, int slotY, bool clear, object itemIdentity)
    {
        var left = slotX * SlotTextureSize;
        var top = slotY * SlotTextureSize;
        //STALE 状态需清槽位 PoC 清 LastFrameVertices 完整版清 AtlasTexture 区域
        if (clear) LastFrameVertices.Clear();

        //设正交投影到图集尺寸 invertY=true 翻转 Y
        _projection.SetupOrtho(-1000f, 1000f, TextureSize, TextureSize, true);

        _poseStack.PushPose();
        //row-major 下 v*(S*T)=(v*S)*T 先缩放再平移 平移不被缩放放大
        //原版 column-major T*S*v 先 scale 再 translate NetCraft row-major 顺序需对调
        _poseStack.Scale(SlotTextureSize, -SlotTextureSize, SlotTextureSize);
        _poseStack.Translate(left + SlotTextureSize / 2f, top + SlotTextureSize / 2f, 0f);

        if (_items.TryGetValue(itemIdentity, out var item))
        {
            _collector.Nodes.Clear();
            item.Submit(_poseStack, _collector,
                ItemFeatureRenderer.FullBright, ItemFeatureRenderer.NoOverlay, 0);
            //清当前帧顶点后写入新顶点
            LastFrameVertices.Clear();
            ItemFeatureRenderer.Execute(_collector, LastFrameVertices);
        }
        _poseStack.PopPose();

        if (Device.SupportsGpuRendering && LastFrameVertices.VertexCount > 0)
            RenderToGpu(slotX, slotY);
    }

    //RenderToGpu 上传顶点 + 更新 MVP UBO + 录制 Vulkan 命令渲染到 AtlasTexture 槽位区域
    //model 不上传 因 VertexConsumer3D.PutBakedQuad 已在 CPU 端 apply pose shader 端 Model=Identity
    private void RenderToGpu(int slotX, int slotY)
    {
        //上传顶点 buffer 跨帧复用 size 不够才重建
        var vertices = LastFrameVertices.Vertices;
        var vertexBytes = VerticesToBytes(vertices);
        if (_vertexBuffer == null || _vertexBuffer.Size < vertexBytes.Length)
        {
            _vertexBuffer?.Dispose();
            _vertexBuffer = Device.CreateHostVisibleBuffer(vertexBytes.Length, GpuBufferUsage.VertexBuffer);
        }
        _vertexBuffer.Upload<byte>(vertexBytes);

        //更新 MVP UBO model=Identity 因 VertexConsumer3D.PutBakedQuad 已在 CPU 端 apply pose
        //Blaze3d 设计 CPU 端 transform 顶点 shader 端 Model=Identity 重复 apply 会导致顶点 NDC 超视体被裁剪
        //SetupOrtho invertY=true 假设 OpenGL Y 朝上 Vulkan framebuffer Y 朝下 需翻转 M22+M42 匹配 Vulkan
        //OpenGL Z [-1,1] 转 Vulkan Z [0,1] M33*=0.5 M43=M43*0.5+0.5
        var proj = _projection.GetMatrix();
        proj.M22 *= -1;
        proj.M42 *= -1;
        proj.M33 *= 0.5f;
        proj.M43 = proj.M43 * 0.5f + 0.5f;
        //System.Numerics row-major 内存直接 memcpy 到 GLSL column-major mat4
        //GLSL m*v(列向量乘)等价于 CPU v*M(行向量乘) 两者结果一致
        //若上传 transpose 反而变成 CPU M*v(列向量乘) 导致 w 分量符号错误顶点被裁剪
        var mvp = new MvpUniform
        {
            Model = Matrix4x4.Identity,
            View = Matrix4x4.Identity,
            Proj = proj
        };
        _mvpUbo!.Upload<MvpUniform>(new[] { mvp });

        //录制渲染命令 colorLoadOp=Load 保留其他 slot 内容 depth 每次 Clear 到 1.0
        //scissor 限制渲染到当前 slot 区域 不污染其他 slot
        using var encoder = Device.CreateCommandEncoder();
        var clearColor = new Vector4(0f, 0f, 0f, 1f);
        using var pass = encoder.CreateRenderPass(_pipeline!, AtlasTexture, clearColor, AtlasDepth, 1.0f, GpuLoadOp.Load);
        pass.SetVertexBuffer(0, _vertexBuffer!);
        pass.SetIndexBuffer(_indexBuffer!, GpuIndexType.UInt16);
        pass.BindDescriptorSet(_mvpSet!, 0);
        pass.BindDescriptorSet(_lighting!.CurrentDescriptorSet!, 1);
        pass.BindDescriptorSet(_lightmapSet!, 2);
        pass.BindDescriptorSet(_atlasSet!, 3);
        pass.EnableScissor(slotX * SlotTextureSize, slotY * SlotTextureSize, SlotTextureSize, SlotTextureSize);
        //CubeModel 6 面 * 6 索引 = 36
        pass.DrawIndexed(36);
        pass.Close();
        encoder.Submit();
    }

    //VerticesToBytes 把 List<float> 顶点转 byte[] 直接 memcpy
    private static byte[] VerticesToBytes(List<float> vertices)
    {
        var span = CollectionsMarshal.AsSpan(vertices);
        return MemoryMarshal.AsBytes(span).ToArray();
    }

    //GenerateQuadIndices 生成 quadCount 个四边形的索引数组每个 quad 6 索引 (0,1,2,2,3,0) 累加 baseVertex
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
        _vertexBuffer?.Dispose();
        _indexBuffer?.Dispose();
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
