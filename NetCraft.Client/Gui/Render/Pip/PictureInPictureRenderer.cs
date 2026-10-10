using System.Numerics;
using NetCraft.Client.Blaze3d.Pipeline;
using NetCraft.Client.Render;
using NetCraft.Client.Blaze3d.Systems;
using NetCraft.Client.Blaze3d.Buffers;
using NetCraft.Client.Blaze3d.Textures;
using NetCraft.Client.Blaze3d.Shaders;
using NetCraft.Client.Blaze3d.Vertex;
using NetCraft.Client.Blaze3d.Vulkan;
using NetCraft.Client.Blaze3d.Platform;
using NetCraft.Client.Blaze3d.Font;
using NetCraft.Client.Blaze3d.Resource;
using NetCraft.Client.Blaze3d.Audio;
using NetCraft.Client.Blaze3d.Framegraph;
using NetCraft.Client.Blaze3d.Preprocessor;
using NetCraft.Client.Blaze3d.Util;
using NetCraft.Client.Render.Model;
using NetCraft.Client.Render.Texture;
using NetCraft.Client.Render.Texture.Atlas;
using NetCraft.Client.Render.Item;
using NetCraft.Client.Render.Entity;
using NetCraft.Client.Render.Entity.State;
using NetCraft.Client.Render.State.Gui;
using NetCraft.Client.Gui;
using NetCraft.Client.Gui.Render;
using NetCraft.Client.Gui.Render.State;
using NetCraft.Client.Gui.Render.Pip;
using NetCraft.Client.Gui.Navigation;
using NetCraft.Client.Gui.Layouts;
using NetCraft.Client.Gui.Font;
using NetCraft.Client.Gui.Font.Providers;
using NetCraft.Client.Gui.Font.Glyphs;
using NetCraft.Client.Model;
using NetCraft.Client.Model.Geom;
using NetCraft.Client.Resources.Metadata.Gui;

namespace NetCraft.Client.Gui.Render.Pip;

//IPictureInPictureRenderer PIP renderer non-generic interface for GuiRenderer to dispatch by type
//C# generic classes do not support covariance; GuiRenderer holds this interface rather than PictureInPictureRenderer<T> to avoid casts
public interface IPictureInPictureRenderer
{
    //RenderStateClass the PIP state type of the subclass; GuiRenderer dispatches by it
    Type RenderStateClass { get; }

    //Prepare non-generic entry that casts to T and calls the template method, explicitly implemented by PictureInPictureRenderer<T>
    void Prepare(PictureInPictureRenderState state, GuiRenderState guiRenderState, int guiScale);
}

//PictureInPictureRenderer<T> PIP renderer abstract base class, maps to vanilla pip.PictureInPictureRenderer
//The template method defines the prepare flow: compute size→ensure offscreen texture→clear+projection→renderToTexture→blit
//GPU operations (offscreen texture management/clear/projection/3D rendering) are implemented by subclasses with an injected GpuDevice
//NetCraft has no RenderSystem global state; all GPU operations are explicitly injected without global dependencies
//There is no 3D render pipeline yet; subclasses are deferred to later world rendering, defining the data layer and template flow first
public abstract class PictureInPictureRenderer<T> : IPictureInPictureRenderer, IDisposable
    where T : PictureInPictureRenderState
{
    //OffscreenTexture offscreen color texture, PIP-area sized, rendering 3D content then blitted to the GUI
    //null means not created; lazily created by the subclass's EnsureTexturesAndProjection
    protected GpuTexture? OffscreenTexture { get; set; }
    //OffscreenDepth offscreen depth texture for 3D render depth testing
    protected GpuTexture? OffscreenDepth { get; set; }
    //_textureWidth/_textureHeight current offscreen size, rebuilt when the PIP area changes
    private int _textureWidth;
    private int _textureHeight;

    public abstract Type RenderStateClass { get; }

    //IPictureInPictureRenderer.Prepare non-generic entry that casts to T and calls the template method
    void IPictureInPictureRenderer.Prepare(PictureInPictureRenderState state, GuiRenderState guiRenderState, int guiScale)
        => Prepare((T)state, guiRenderState, guiScale);

    //Prepare template method, maps to the vanilla prepare flow
    //1 compute the PIP area size (scaled by guiScale)
    //2 if the size is unchanged and TextureIsReadyToBlit, blit directly and skip offscreen rendering
    //3 otherwise EnsureTexturesAndProjection rebuilds/reuses the texture+clear+projection
    //4 RenderToTexture lets the subclass render 3D content to offscreen
    //5 BlitTexture adds the offscreen texture as a BlitRenderState to guiRenderState
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

    //TextureIsReadyToBlit whether it can blit directly and skip offscreen rendering
    //Defaults to false and re-renders every frame; subclasses may override the cache strategy, maps to vanilla textureIsReadyToBlit
    protected virtual bool TextureIsReadyToBlit(T renderState) => false;

    //EnsureTexturesAndProjection ensures the offscreen texture exists and matches the size+clear+sets the orthographic projection
    //The subclass creates the GpuTexture (ColorAttachment|SampledImage) + DepthAttachment + clear + projection matrix
    //maps to vanilla prepareTexturesAndProjection
    protected abstract void EnsureTexturesAndProjection(int width, int height);

    //RenderToTexture renders 3D content into the offscreen texture, maps to vanilla renderToTexture
    //Subclasses use GpuDevice to record commands rendering models/entities/skins etc. into OffscreenTexture
    protected abstract void RenderToTexture(T renderState);

    //BlitTexture adds the offscreen texture as a BlitRenderState to guiRenderState
    //maps to vanilla blitTexture using the GUI_TEXTURED_PREMULTIPLIED_ALPHA pipeline
    //Vulkan texture V=0 at the top; offscreen rendering has Y down, so V0=0 pairs with Y0 at the top and V1=1 with Y1 at the bottom, no flip
    //After the subclass's EnsureTexturesAndProjection the OffscreenTexture should be ready and GetBlitTextureSetup provides the texture binding
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

    //GetBlitTextureSetup builds the offscreen texture's TextureSetup, maps to vanilla singleTexture
    //The subclass provides the OffscreenTexture's TextureSetup (including the sampler) called by BlitTexture
    protected abstract TextureSetup GetBlitTextureSetup();

    //DisposeTextures releases the offscreen texture, called on a size change or Dispose
    //virtual so double-buffered subclasses can override to release multiple textures/encoders; the base only releases one
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

    //OnDispose extra resource-release hook for subclasses
    protected virtual void OnDispose() { }
}
