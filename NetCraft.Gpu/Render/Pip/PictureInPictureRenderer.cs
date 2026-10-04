using System.Numerics;
using NetCraft.Gpu.Pipeline;

namespace NetCraft.Gpu;

//IPictureInPictureRenderer PIP 渲染器非泛型接口供 GuiRenderer 按类型分派
//C# 泛型 class 不支持协变 GuiRenderer 持此接口而非 PictureInPictureRenderer<T> 避免类型转换
public interface IPictureInPictureRenderer
{
    //RenderStateClass 子类对应的 PIP state 类型 GuiRenderer 按此分派
    Type RenderStateClass { get; }

    //Prepare 非泛型入口内部转 T 调模板方法由 PictureInPictureRenderer<T> 显式实现
    void Prepare(PictureInPictureRenderState state, GuiRenderState guiRenderState, int guiScale);
}

//PictureInPictureRenderer<T> PIP 渲染器抽象基类对标原版 pip.PictureInPictureRenderer
//模板方法定义 prepare 流程：计算尺寸→确保 offscreen texture→清屏+投影→renderToTexture→blit
//GPU 操作（offscreen texture 管理/清屏/投影/3D 渲染）由子类实现注入 GpuDevice
//NetCraft 无 RenderSystem 全局状态所有 GPU 操作显式注入不依赖全局
//当前无 3D 渲染管线子类留待后续世界渲染补 先定义数据层和模板流程
public abstract class PictureInPictureRenderer<T> : IPictureInPictureRenderer, IDisposable
    where T : PictureInPictureRenderState
{
    //OffscreenTexture offscreen 颜色纹理 PIP 区域尺寸渲染 3D 内容后 blit 到 GUI
    //null 表示未创建由子类 EnsureTexturesAndProjection 懒创建
    protected GpuImage? OffscreenTexture { get; set; }
    //OffscreenDepth offscreen 深度纹理 3D 渲染深度测试用
    protected GpuImage? OffscreenDepth { get; set; }
    //_textureWidth/_textureHeight 当前 offscreen 尺寸 PIP 区域变化时重建
    private int _textureWidth;
    private int _textureHeight;

    public abstract Type RenderStateClass { get; }

    //IPictureInPictureRenderer.Prepare 非泛型入口转 T 调模板方法
    void IPictureInPictureRenderer.Prepare(PictureInPictureRenderState state, GuiRenderState guiRenderState, int guiScale)
        => Prepare((T)state, guiRenderState, guiScale);

    //Prepare 模板方法对标原版 prepare 流程
    //1 计算 PIP 区域尺寸（guiScale 缩放）
    //2 尺寸没变且 TextureIsReadyToBlit 直接 blit 跳过 offscreen 渲染
    //3 否则 EnsureTexturesAndProjection 重建/复用 texture+清屏+投影
    //4 RenderToTexture 子类渲染 3D 内容到 offscreen
    //5 BlitTexture 把 offscreen texture 作为 BlitRenderState 加到 guiRenderState
    public void Prepare(T renderState, GuiRenderState guiRenderState, int guiScale)
    {
        var width = (renderState.X1 - renderState.X0) * guiScale;
        var height = (renderState.Y1 - renderState.Y0) * guiScale;
        var needsResize = OffscreenTexture == null
            || _textureWidth != width
            || _textureHeight != height;

        if (!needsResize && TextureIsReadyToBlit(renderState))
        {
            BlitTexture(renderState, guiRenderState);
            return;
        }

        if (needsResize)
        {
            DisposeTextures();
            _textureWidth = width;
            _textureHeight = height;
        }
        EnsureTexturesAndProjection(width, height);
        RenderToTexture(renderState);
        BlitTexture(renderState, guiRenderState);
    }

    //TextureIsReadyToBlit 是否可直接 blit 跳过 offscreen 渲染
    //默认 false 每帧重新渲染子类可 override 缓存策略对标原版 textureIsReadyToBlit
    protected virtual bool TextureIsReadyToBlit(T renderState) => false;

    //EnsureTexturesAndProjection 确保 offscreen texture 存在且尺寸匹配+清屏+设置正交投影
    //子类创建 GpuImage（ColorAttachment|SampledImage）+ DepthAttachment + 清屏 + 投影矩阵
    //对标原版 prepareTexturesAndProjection
    protected abstract void EnsureTexturesAndProjection(int width, int height);

    //RenderToTexture 渲染 3D 内容到 offscreen texture 对标原版 renderToTexture
    //子类用 GpuDevice 录制命令渲染模型/实体/皮肤等到 OffscreenTexture
    protected abstract void RenderToTexture(T renderState);

    //BlitTexture 把 offscreen texture 作为 BlitRenderState 加到 guiRenderState
    //对标原版 blitTexture 用 GUI_TEXTURED_PREMULTIPLIED_ALPHA pipeline
    //Vulkan 纹理 V=0 顶部 offscreen 渲染 Y 朝下 V0=0 配 Y0 顶部 V1=1 配 Y1 底部不翻转
    //子类 EnsureTexturesAndProjection 后 OffscreenTexture 应就绪 GetBlitTextureSetup 提供纹理绑定
    protected virtual void BlitTexture(T renderState, GuiRenderState guiRenderState)
    {
        var textureSetup = GetBlitTextureSetup();
        var blit = new BlitRenderState(
            RenderPipelines.GUI_TEXTURED_PREMULTIPLIED_ALPHA,
            textureSetup,
            renderState.Pose,
            renderState.X0, renderState.Y0, renderState.X1, renderState.Y1,
            0f, 1f, 0f, 1f,
            -1,
            renderState.ScissorArea);
        guiRenderState.AddGuiElement(blit);
    }

    //GetBlitTextureSetup 构造 offscreen texture 的 TextureSetup 对标原版 singleTexture
    //子类提供 OffscreenTexture 的 TextureSetup（含 sampler）由 BlitTexture 调用
    protected abstract TextureSetup GetBlitTextureSetup();

    //DisposeTextures 释放 offscreen texture 尺寸变化或 Dispose 时调
    //virtual 供双缓冲子类 override 释放多个 texture/encoder 基类只释放单个
    protected virtual void DisposeTextures()
    {
        OffscreenTexture?.Dispose();
        OffscreenDepth?.Dispose();
        OffscreenTexture = null;
        OffscreenDepth = null;
    }

    public void Dispose()
    {
        DisposeTextures();
        OnDispose();
        GC.SuppressFinalize(this);
    }

    //OnDispose 子类额外资源释放钩子
    protected virtual void OnDispose() { }
}
