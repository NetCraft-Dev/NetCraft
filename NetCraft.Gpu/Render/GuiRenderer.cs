using NetCraft.Gpu.Pipeline;

namespace NetCraft.Gpu;

//GuiRenderer render phase 主入口对标原版 GuiRenderer
//submission phase 通过 Prepare 按 pipeline+texture+scissor 分组合批
//render phase 通过 Draw 提交 GPU 同组元素合并为 1 个 DrawCall
//阶段 4.4a 实现 Prepare 合批逻辑可单测 4.4b 实现 Upload/Draw 提交
public sealed class GuiRenderer : IDisposable
{
    private readonly StagedVertexBuffer _vertexBuffer;
    private readonly List<Mesh> _meshes = new();
    //_pipRenderers PIP 渲染器注册表按 RenderStateClass 分派对标原版 pictureInPictureRenderers
    //外部 RegisterPipRenderer 填充 Prepare 时遍历 pipStates 调对应 renderer.Prepare
    private readonly Dictionary<Type, IPictureInPictureRenderer> _pipRenderers = new();
    private bool _disposed;
    //_firstMeshIndexAfterBlur BeforeBlur 段 mesh 数 AfterBlur 段从这里开始
    //-1 表示无 blur 分段 Prepare 未调或 snapshot 无 BlurBeforeThisStratum
    private int _firstMeshIndexAfterBlur = -1;
    //DrawCallCount 最近一帧 DrawRange 提交的 drawIndexed/draw 次数供性能验收
    //MeshCount 合批后的 mesh 数量 VertexCount 合批后总顶点数
    public int DrawCallCount { get; private set; }
    public int MeshCount => _meshes.Count;
    public int VertexCount { get; private set; }

    public GuiRenderer()
    {
        _vertexBuffer = new StagedVertexBuffer();
    }

    //Meshes 已合批的 mesh 列表供 Draw 遍历提交
    public IReadOnlyList<Mesh> Meshes => _meshes;

    //FirstMeshIndexAfterBlur BeforeBlur/AfterBlur 分段点 Prepare 后读
    //-1 无 blur 分段 0 表示 BeforeBlur 空 >0 表示 BeforeBlur 有 mesh AfterBlur 从此索引开始
    public int FirstMeshIndexAfterBlur => _firstMeshIndexAfterBlur;

    //RegisterPipRenderer 注册 PIP 渲染器按 RenderStateClass 分派对标原版 pictureInPictureRenderers.put
    //Prepare 遍历 pipStates 时按 state 类型查表调 renderer.Prepare 做 offscreen 渲染+blit
    public void RegisterPipRenderer<T>(PictureInPictureRenderer<T> renderer) where T : PictureInPictureRenderState
    {
        _pipRenderers[renderer.RenderStateClass] = renderer;
    }

    //PreparePip 在 Render 线程调 PIP renderer.Prepare offscreen 渲染+blit 把 BlitRenderState 加到 snapshot
    //必须在 Prepare 之前调用让 BlitRenderState 进入 snapshot 供合批
    //P15 移到 OnRecordCommandBuffer 调用 vkQueueSubmit 必须在 Render 线程串行避免 queue 竞争
    public void PreparePip(GuiRenderState snapshot, int guiScale)
    {
        if (guiScale <= 0 || _pipRenderers.Count == 0) return;
        snapshot.ForEachPictureInPicture(pip =>
        {
            if (_pipRenderers.TryGetValue(pip.GetType(), out var renderer))
                renderer.Prepare(pip, snapshot, guiScale);
        });
    }

    //Prepare render phase 按 pipeline+texture+scissor 分组合批
    //同组元素写入同一 Draw 顶点 drawIndexed 一次提交
    //重复调用安全先 EndFrame 重置上一帧再重新合批
    //snapshot 由调用方传入 Tick 构造并深拷贝的快照 Render 线程只读
    //blur 分段先合批 BeforeBlur 元素再合批 AfterBlur 元素 FirstMeshIndexAfterBlur 标记分段点
    //无 BlurBeforeThisStratum 时 BeforeBlur 遍历全部 AfterBlur 遍历空 等价不分段
    //PIP prepare 已由 OnRecordCommandBuffer 调 PreparePip 提前完成 Prepare 只做合批不再调 PIP Submit
    public void Prepare(GuiRenderState snapshot, int guiScale = 0)
    {
        _meshes.Clear();
        _firstMeshIndexAfterBlur = -1;
        _vertexBuffer.EndFrame();

        Action<GuiElementRenderState> append = element =>
        {
            var mesh = FindMesh(element.Pipeline, element.TextureSetup, element.ScissorArea);
            if (mesh == null)
            {
                var format = element.Pipeline.VertexFormatPerBuffer.Count > 0
                    ? element.Pipeline.VertexFormatPerBuffer[0]
                    : null;
                if (format == null)
                    throw new InvalidOperationException($"pipeline {element.Pipeline.Location} 缺 VertexFormat 无法 AppendDraw");
                var draw = _vertexBuffer.AppendDraw(format, element.Pipeline.PrimitiveTopology);
                var builder = _vertexBuffer.GetVertexBuilder(draw);
                mesh = new Mesh(element.Pipeline, element.TextureSetup, element.ScissorArea, draw, builder);
                _meshes.Add(mesh);
            }
            element.BuildVertices(mesh.VertexBuilder);
        };
        snapshot.ForEachElement(append, TraverseRange.BeforeBlur);
        if (snapshot.HasBlurSplit)
            _firstMeshIndexAfterBlur = _meshes.Count;
        snapshot.ForEachElement(append, TraverseRange.AfterBlur);

        foreach (var mesh in _meshes)
            _vertexBuffer.EndDraw(mesh.Draw);
        DrawCallCount = 0;
        VertexCount = 0;
        foreach (var mesh in _meshes)
            VertexCount += mesh.Draw.VertexCount;
    }

    //Upload 拼接顶点到 vertex buffer 索引到 index buffer 上传 GPU
    //必须在 Prepare 之后 Draw 之前调用 buffer 跨帧复用 HostVisible 每帧重写
    public void Upload(GpuDevice device) => _vertexBuffer.Upload(device);

    //Draw render phase 全量提交等价 DrawRange(0, _meshes.Count)
    //blur 分段时调用方改用 DrawRange 分别提交 BeforeBlur/AfterBlur 段中间插 blur
    public void Draw(IRenderPass pass,
        Func<RenderPipeline, CompiledRenderPipeline> pipelineResolver,
        Func<TextureSetup, GpuDescriptorSet?> descriptorResolver)
        => DrawRange(pass, pipelineResolver, descriptorResolver, 0, _meshes.Count);

    //DrawRange 提交 _meshes[start..end) 范围内的 mesh 供 blur 分段执行
    //pipelineResolver 把声明式 RenderPipeline 解析为已编译 CompiledRenderPipeline 由调用方注入 PipelineCache.Precompile
    //descriptorResolver 把 TextureSetup 解析为纹理 GpuDescriptorSet 由 Game 层管理纹理资源 NoTexture 返回 null
    //全局 DescriptorSet(GLOBALS+MATRICES_PROJECTION)由调用方在 render pass 开始时绑定 set 0/1 纹理绑定到最后一个 set
    //必须在 Upload 之后 IRenderPass 已开时调用 QUADS 走 DrawIndexed 索引含 baseVertex 其他走 Draw 非索引
    public void DrawRange(IRenderPass pass,
        Func<RenderPipeline, CompiledRenderPipeline> pipelineResolver,
        Func<TextureSetup, GpuDescriptorSet?> descriptorResolver,
        int start, int end)
    {
        for (int i = start; i < end; i++)
        {
            var mesh = _meshes[i];
            var compiled = pipelineResolver(mesh.Pipeline);
            pass.SetPipeline(compiled);
            DrawCallCount++;

            //纹理绑定到最后一个 set pipeline 的 BindGroupLayouts 末尾是 SAMPLER0
            //NoTexture 单例返回 null 跳过 BindDescriptorSet 全局 set 0/1 由调用方已绑定
            var descSet = descriptorResolver(mesh.TextureSetup);
            if (descSet != null)
            {
                var textureSetIndex = (uint)(mesh.Pipeline.BindGroupLayouts.Count - 1);
                pass.BindDescriptorSet(descSet, textureSetIndex);
            }

            //scissor 动态切换空矩形禁用裁剪全屏渲染
            if (mesh.ScissorArea.IsEmpty)
                pass.DisableScissor();
            else
                pass.EnableScissor(mesh.ScissorArea.X, mesh.ScissorArea.Y, mesh.ScissorArea.Width, mesh.ScissorArea.Height);

            var info = _vertexBuffer.GetExecuteInfo(mesh.Draw);
            pass.SetVertexBuffer(0, info.VertexBuffer, 0);
            if (info.IndexBuffer != null && info.IndexCount > 0)
            {
                //QUADS 索引已编码 baseVertex DrawIndexed 的 vertexOffset 传 0 靠 firstIndex 偏移
                pass.SetIndexBuffer(info.IndexBuffer, GpuIndexType.UInt32, 0);
                pass.DrawIndexed(info.IndexCount, 1, info.FirstIndex, 0, 0);
            }
            else
            {
                //非 QUADS 走非索引绘制 firstVertex = BaseVertex 偏移到本 Draw 顶点段
                pass.Draw(mesh.Draw.VertexCount, 1, mesh.Draw.BaseVertex, 0);
            }
        }
    }

    //GetExecuteInfo 返回 mesh 的 Draw 执行信息提交阶段传给 IRenderPass
    public ExecuteInfo GetExecuteInfo(Mesh mesh) => _vertexBuffer.GetExecuteInfo(mesh.Draw);

    //EndFrame 重置暂存区保留 GPU buffer 跨帧复用
    public void EndFrame() => _vertexBuffer.EndFrame();

    //FindMesh 线性查找匹配 pipeline+texture+scissor 的 mesh
    //mesh 数量通常小(几到几十)线性查找足够
    private Mesh? FindMesh(RenderPipeline pipeline, TextureSetup texture, ScreenRectangle scissor)
    {
        foreach (var mesh in _meshes)
            if (mesh.Matches(pipeline, texture, scissor))
                return mesh;
        return null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _vertexBuffer.Dispose();
        _disposed = true;
    }
}

//Mesh 一次合批的渲染状态组同 pipeline+texture+scissor 的元素合并
//Draw 记录顶点索引元数据 ExecuteDraw 时用 pipeline/texture/scissor 提交
public sealed class Mesh
{
    public RenderPipeline Pipeline { get; }
    public TextureSetup TextureSetup { get; }
    public ScreenRectangle ScissorArea { get; }
    public Draw Draw { get; }
    public IVertexConsumer VertexBuilder { get; }

    internal Mesh(RenderPipeline pipeline, TextureSetup textureSetup, ScreenRectangle scissorArea, Draw draw, IVertexConsumer vertexBuilder)
    {
        Pipeline = pipeline;
        TextureSetup = textureSetup;
        ScissorArea = scissorArea;
        Draw = draw;
        VertexBuilder = vertexBuilder;
    }

    //Matches 判断是否可合批同 pipeline(引用)+texture(纹理引用)+scissor(值)
    public bool Matches(RenderPipeline pipeline, TextureSetup texture, ScreenRectangle scissor)
        => ReferenceEquals(Pipeline, pipeline)
            && Equals(TextureSetup, texture)
            && ScissorArea == scissor;
}
