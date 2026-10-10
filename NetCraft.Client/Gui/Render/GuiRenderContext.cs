using System.Numerics;
using NetCraft.Client.Gui;
using NetCraft.Client.Gui.Font;
using NetCraft.Client.Gui.Font.Providers;
using NetCraft.Client.Gui.Font.Glyphs;
using NetCraft.Client.Blaze3d.Pipeline;
using NetCraft.Client.Render;
using NetCraft.Client.Gui;
using NetCraft.Client.Resources.Metadata.Gui;
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
using NetCraft.Client.Gui.Render;
using NetCraft.Client.Gui.Render.State;
using NetCraft.Client.Gui.Render.Pip;
using NetCraft.Client.Gui.Navigation;
using NetCraft.Client.Gui.Layouts;
using NetCraft.Client.Model;
using NetCraft.Client.Model.Geom;

namespace NetCraft.Client.Gui.Render;

//GuiRenderContext submission-layer IGuiRenderContext implementation
//Converts widget DrawQuad/DrawText/DrawImage calls into RenderStates submitted to GuiRenderState
//The coordinate system is uniformly actual pixels; widget scaled coordinates are converted by *guiScale
//Stage 5b replaces VulkanGuiRenderer's submission duty; the render phase is handed to GuiRenderer
//F7 DrawText prefers the GlyphFont dynamic bake path with FontAtlas as fallback
//P0 DrawSprite dispatched by scaling after GuiSpriteManager parses .mcmeta
public sealed class GuiRenderContext : IGuiRenderContext
{
    private readonly GuiRenderState _renderState;
    private readonly int _guiScale;
    private readonly int _surfaceWidth;
    private readonly int _surfaceHeight;
    private readonly GlyphFont? _font;
    private readonly FontAtlas? _fontAtlas;
    private readonly TextureSetup? _fontTexture;
    private readonly Func<int, TextureSetup?> _textureResolver;
    private readonly GuiSpriteManager? _spriteManager;
    //_recordingStack recording stack supporting nested child cache and window cache active at once
    //BeginRecording pushes EndRecording pops Submit writes to all stack levels ReplayRange writes to the top
    //An empty stack means not recording, and Submit only goes to GuiRenderState
    private readonly Stack<List<GuiElementRenderState>> _recordingStack = new();

    //Pose the top stores the actual pose; PushPose's scaled delta is converted to actual then pushed
    private readonly Stack<Matrix3x2> _poseStack = new();
    //Scissor the stack stores actual pixels used directly by vkCmdSetScissor
    private readonly Stack<ScreenRectangle> _scissorStack = new();

    public GuiRenderContext(GuiRenderState renderState, int surfaceWidth, int surfaceHeight, int guiScale,
        GlyphFont? font, FontAtlas? fontAtlas, TextureSetup? fontTexture, Func<int, TextureSetup?> textureResolver,
        GuiSpriteManager? spriteManager = null)
    {
        _renderState = renderState;
        _surfaceWidth = surfaceWidth;
        _surfaceHeight = surfaceHeight;
        _guiScale = guiScale <= 0 ? 1 : guiScale;
        _font = font;
        _fontAtlas = fontAtlas;
        _fontTexture = fontTexture;
        _textureResolver = textureResolver;
        _spriteManager = spriteManager;
    }

    //BeginFrame resets the Pose/Scissor stacks, called by the caller after GuiRenderState.Reset each frame
    public void BeginFrame()
    {
        _poseStack.Clear();
        _poseStack.Push(Matrix3x2.Identity);
        _scissorStack.Clear();
        _scissorStack.Push(new ScreenRectangle(0, 0, _surfaceWidth, _surfaceHeight));
    }

    //DrawQuad draws a solid rectangle, submitting a ColoredRectangleRenderState to the GUI pipeline
    public void DrawQuad(int x, int y, int width, int height, GuiColor color)
    {
        var (ax, ay, ax1, ay1) = ToActual(x, y, width, height);
        var state = new ColoredRectangleRenderState(
            RenderPipelines.GUI, TextureSetup.NoTexture, _poseStack.Peek(),
            ax, ay, ax1, ay1,
            ToIntColor(color), ToIntColor(color), _scissorStack.Peek());
        Submit(state);
    }

    //DrawQuadInverted draws an inverted rectangle, submitting a ColoredRectangleRenderState to the GUI_INVERT pipeline
    public void DrawQuadInverted(int x, int y, int width, int height, GuiColor color)
    {
        var (ax, ay, ax1, ay1) = ToActual(x, y, width, height);
        var state = new ColoredRectangleRenderState(
            RenderPipelines.GUI_INVERT, TextureSetup.NoTexture, _poseStack.Peek(),
            ax, ay, ax1, ay1,
            ToIntColor(color), ToIntColor(color), _scissorStack.Peek());
        Submit(state);
    }

    //DrawText renders text; F7 prefers the Font dynamic bake path with FontAtlas as fallback
    //The Font path calls Font.Draw, iterating text Bake+Render and submitting GlyphBlitRenderState
    //The FontAtlas path submits one BlitRenderState per glyph to the GUI_TEXT pipeline
    public void DrawText(int x, int y, string text, GuiColor color)
    {
        if (string.IsNullOrEmpty(text)) return;
        var intColor = ToIntColor(color);

        //Font dynamic bake path: BakedGlyph.Render calls DrawGlyphQuad, submitting GlyphBlitRenderState
        if (_font is not null)
        {
            //x/y are scaled coordinates converted to actual penY = y*guiScale + Ascent*guiScale
            float penX = x * _guiScale;
            float penY = y * _guiScale;
            _font.Draw(this, text, penX, penY, intColor, shadow: false);
            return;
        }

        //FontAtlas fallback path
        if (_fontAtlas is null || _fontTexture is null) return;

        //y is the top; convert to baseline with Ascent to avoid the CJK misalignment caused by -descent
        float penX2 = x * _guiScale;
        float penY2 = y * _guiScale + _fontAtlas.Ascent * _guiScale;
        var pose = _poseStack.Peek();
        var scissor = _scissorStack.Peek();

        foreach (var ch in text)
        {
            var g = _fontAtlas.GetGlyph(ch);
            if (g is null)
            {
                penX2 += _fontAtlas.MeasureText("?") * _guiScale;
                continue;
            }
            var glyph = g.Value;
            float px = penX2 + glyph.OffsetX * _guiScale;
            float py = penY2 + glyph.OffsetY * _guiScale;
            float pw = glyph.Width * _guiScale;
            float ph = glyph.Height * _guiScale;
            if (pw > 0 && ph > 0)
            {
                var state = new BlitRenderState(
                    RenderPipelines.GUI_TEXT, _fontTexture, pose,
                    (int)px, (int)py, (int)(px + pw), (int)(py + ph),
                    glyph.U0, glyph.U1, glyph.V0, glyph.V1,
                    intColor, scissor);
                Submit(state);
            }
            penX2 += glyph.Advance * _guiScale;
        }
    }

    //DrawGlyphQuad submits a glyph quad to the render context; 4 float vertices support italic/bold offsets
    //Called by SheetBakedGlyph.Render to submit a GlyphBlitRenderState to GuiRenderState
    //pose/scissor are read from the stack top; color is an ARGB int
    public void DrawGlyphQuad(RenderPipeline pipeline, TextureSetup textureSetup,
        float x0, float y0, float x1, float y1, float x2, float y2, float x3, float y3,
        float u0, float v0, float u1, float v1, int color)
    {
        var state = new GlyphBlitRenderState(pipeline, textureSetup, _poseStack.Peek(),
            x0, y0, x1, y1, x2, y2, x3, y3,
            u0, v0, u1, v1, color, _scissorStack.Peek());
        Submit(state);
    }

    //MeasureText returns the scaled pixel width for widget layout
    //Font takes priority, returning Font.MeasureText *guiScale, otherwise uses FontAtlas
    public float MeasureText(string text)
    {
        if (_font is not null) return _font.MeasureText(text) * _guiScale;
        return _fontAtlas?.MeasureText(text) ?? 0;
    }

    //LineHeight returns the scaled pixel height for widgets to compute multi-line spacing
    //Font takes priority, returning Font.LineHeight *guiScale, otherwise uses FontAtlas
    public int LineHeight => (_font?.LineHeight ?? _fontAtlas?.LineHeight ?? 0) * _guiScale;

    //DrawImage draws a texture sub-region, submitting a BlitRenderState to the GUI_TEXTURED pipeline
    //UVs are computed from src pixels and the GpuTexture size; textureId is resolved to a TextureSetup via textureResolver
    //srcW/srcH<=0 means the full texture size, so the caller need not know the texture size
    public void DrawImage(int textureId, int x, int y, int width, int height,
        int srcX, int srcY, int srcW, int srcH, GuiColor tint)
    {
        var texture = _textureResolver(textureId);
        if (texture?.Texture0 is not { } img) return;
        var imgWidth = img.Width;
        var imgHeight = img.Height;
        if (imgWidth <= 0 || imgHeight <= 0) return;
        if (srcW <= 0) srcW = imgWidth;
        if (srcH <= 0) srcH = imgHeight;

        float u0 = srcX / (float)imgWidth;
        float v0 = srcY / (float)imgHeight;
        float u1 = (srcX + srcW) / (float)imgWidth;
        float v1 = (srcY + srcH) / (float)imgHeight;

        var (ax, ay, ax1, ay1) = ToActual(x, y, width, height);
        var state = new BlitRenderState(
            RenderPipelines.GUI_TEXTURED, texture, _poseStack.Peek(),
            ax, ay, ax1, ay1,
            u0, u1, v0, v1,
            ToIntColor(tint), _scissorStack.Peek());
        Submit(state);
    }

    //DrawImageNinePatch legacy API with a single border keeps the original nine-slice implementation and does not delegate to the new API
    //A uniform border on all sides with a fixed stretched center, equivalent to stretchInner=true
    //When the target/source size is under 2*border it falls back to DrawImage to avoid negative regions, matching existing button behavior
    public void DrawImageNinePatch(int textureId, int x, int y, int width, int height,
        int srcX, int srcY, int srcW, int srcH, int border, GuiColor tint)
    {
        if (srcW <= 0 || srcH <= 0)
        {
            var tex = _textureResolver(textureId);
            if (tex?.Texture0 is not { } full) return;
            if (srcW <= 0) srcW = full.Width;
            if (srcH <= 0) srcH = full.Height;
        }
        if (border <= 0 || width < border * 2 || height < border * 2 || srcW < border * 2 || srcH < border * 2)
        {
            DrawImage(textureId, x, y, width, height, srcX, srcY, srcW, srcH, tint);
            return;
        }
        var dw = width - border * 2;
        var dh = height - border * 2;
        var sw = srcW - border * 2;
        var sh = srcH - border * 2;
        var dxL = x;
        var dxM = x + border;
        var dxR = x + width - border;
        var dyT = y;
        var dyM = y + border;
        var dyB = y + height - border;
        var sxL = srcX;
        var sxM = srcX + border;
        var sxR = srcX + srcW - border;
        var syT = srcY;
        var syM = srcY + border;
        var syB = srcY + srcH - border;
        //4 corners fixed
        DrawImage(textureId, dxL, dyT, border, border, sxL, syT, border, border, tint);
        DrawImage(textureId, dxR, dyT, border, border, sxR, syT, border, border, tint);
        DrawImage(textureId, dxL, dyB, border, border, sxL, syB, border, border, tint);
        DrawImage(textureId, dxR, dyB, border, border, sxR, syB, border, border, tint);
        //Top/bottom edges dw×border stretched horizontally
        DrawImage(textureId, dxM, dyT, dw, border, sxM, syT, sw, border, tint);
        DrawImage(textureId, dxM, dyB, dw, border, sxM, syB, sw, border, tint);
        //Left/right edges border×dh stretched vertically
        DrawImage(textureId, dxL, dyM, border, dh, sxL, syM, border, sh, tint);
        DrawImage(textureId, dxR, dyM, border, dh, sxR, syM, border, sh, tint);
        //Center dw×dh stretched both ways
        DrawImage(textureId, dxM, dyM, dw, dh, sxM, syM, sw, sh, tint);
    }

    //DrawImageNinePatch per-side border + stretchInner new API, maps to vanilla blitNineSlicedSprite
    //Independent borders per side supporting per-side border widgets like slider_handle/tab
    //border is clamped to half the target size to avoid negative regions, maps to vanilla Math.min(border, width/2)
    //stretchInner=true stretches the center, false tiles it at sw×sh, corresponding to the vanilla stretch_inner field
    public void DrawImageNinePatch(int textureId, int x, int y, int width, int height,
        int srcX, int srcY, int srcW, int srcH,
        int borderLeft, int borderTop, int borderRight, int borderBottom,
        bool stretchInner, GuiColor tint)
    {
        if (srcW <= 0 || srcH <= 0)
        {
            var tex = _textureResolver(textureId);
            if (tex?.Texture0 is not { } full) return;
            if (srcW <= 0) srcW = full.Width;
            if (srcH <= 0) srcH = full.Height;
        }
        //Border clamping, maps to vanilla Math.min(border, width/2) to avoid negative regions without falling back to DrawImage
        int bl = Math.Min(borderLeft, width / 2);
        int bt = Math.Min(borderTop, height / 2);
        int br = Math.Min(borderRight, width / 2);
        int bb = Math.Min(borderBottom, height / 2);
        //When the source size is under the sum of borders it falls back to DrawImage
        if (srcW < bl + br || srcH < bt + bb)
        {
            DrawImage(textureId, x, y, width, height, srcX, srcY, srcW, srcH, tint);
            return;
        }
        var dw = width - bl - br;
        var dh = height - bt - bb;
        var sw = srcW - bl - br;
        var sh = srcH - bt - bb;
        //4 corners fixed
        DrawImage(textureId, x, y, bl, bt, srcX, srcY, bl, bt, tint);
        DrawImage(textureId, x + width - br, y, br, bt,
            srcX + srcW - br, srcY, br, bt, tint);
        DrawImage(textureId, x, y + height - bb, bl, bb,
            srcX, srcY + srcH - bb, bl, bb, tint);
        DrawImage(textureId, x + width - br, y + height - bb, br, bb,
            srcX + srcW - br, srcY + srcH - bb, br, bb, tint);
        //Top/bottom edges fixed-stretched; stretchInner only affects the center
        DrawImage(textureId, x + bl, y, dw, bt, srcX + bl, srcY, sw, bt, tint);
        DrawImage(textureId, x + bl, y + height - bb, dw, bb,
            srcX + bl, srcY + srcH - bb, sw, bb, tint);
        //Left/right edges fixed-stretched; stretchInner only affects the center
        DrawImage(textureId, x, y + bt, bl, dh, srcX, srcY + bt, bl, sh, tint);
        DrawImage(textureId, x + width - br, y + bt, br, dh,
            srcX + srcW - br, srcY + bt, br, sh, tint);
        //Center stretchInner=true stretches, false tiles, maps to vanilla stretch_inner
        BlitInnerSegment(textureId, x + bl, y + bt, dw, dh,
            srcX + bl, srcY + bt, sw, sh, stretchInner, tint);
    }

    //DrawTiledSprite tiles the texture by tileWidth/tileHeight, submitting a TiledBlitRenderState
    //maps to vanilla blitTiledSprite, a double loop tiling and cropping UVs proportionally at the edges
    //The NineSlice stretchInner=false center-tile path also goes through this method
    public void DrawTiledSprite(int textureId, int srcW, int srcH,
        int x, int y, int width, int height, GuiColor tint)
    {
        var texture = _textureResolver(textureId);
        if (texture is null || srcW <= 0 || srcH <= 0) return;
        var (ax, ay, ax1, ay1) = ToActual(x, y, width, height);
        var state = new TiledBlitRenderState(
            RenderPipelines.GUI_TEXTURED, texture, _poseStack.Peek(),
            srcW, srcH, ax, ay, ax1, ay1,
            0f, 1f, 0f, 1f, ToIntColor(tint), _scissorStack.Peek());
        Submit(state);
    }

    //BlitInnerSegment center segment stretch or tile, maps to vanilla blitNineSliceInnerSegment
    //stretchInner=true goes to DrawImage stretch, false to DrawTiledSprite tile
    //dw/dh<=0 is skipped to avoid submitting an empty region
    private void BlitInnerSegment(int textureId, int dx, int dy, int dw, int dh,
        int sx, int sy, int sw, int sh, bool stretchInner, GuiColor tint)
    {
        if (dw <= 0 || dh <= 0) return;
        if (stretchInner)
            DrawImage(textureId, dx, dy, dw, dh, sx, sy, sw, sh, tint);
        else
            DrawTiledSprite(textureId, sw, sh, dx, dy, dw, dh, tint);
    }

    //DrawSprite looks up a sprite by identifier and dispatches by scaling, maps to vanilla blitSprite
    //Stretch goes to DrawImage Tile to DrawTiledSprite NineSlice to DrawImageNinePatch
    //Returns silently when the sprite is not loaded or GuiSpriteManager is null, avoiding a crash
    public void DrawSprite(string identifier, int x, int y, int width, int height, GuiColor tint)
    {
        if (_spriteManager is null) return;
        var sprite = _spriteManager.GetSprite(identifier);
        if (sprite is null) return;
        switch (sprite.Scaling)
        {
            case StretchScaling:
                DrawImage(sprite.TextureId, x, y, width, height, 0, 0, sprite.Width, sprite.Height, tint);
                break;
            case TileScaling tile:
                DrawTiledSprite(sprite.TextureId, tile.Width, tile.Height, x, y, width, height, tint);
                break;
            case NineSliceScaling nineSlice:
                DrawImageNinePatch(sprite.TextureId, x, y, width, height, 0, 0, sprite.Width, sprite.Height,
                    nineSlice.Border.Left, nineSlice.Border.Top, nineSlice.Border.Right, nineSlice.Border.Bottom,
                    nineSlice.StretchInner, tint);
                break;
        }
    }

    //PushPose converts the scaled delta to actual then multiplies with the top; child widgets position relative to the parent
    //translation *guiScale rotation/scale unchanged keeping the rotation and scale ratio
    public void PushPose(Matrix3x2 delta)
    {
        var actualDelta = new Matrix3x2(delta.M11, delta.M12, delta.M21, delta.M22,
            delta.M31 * _guiScale, delta.M32 * _guiScale);
        _poseStack.Push(_poseStack.Peek() * actualDelta);
    }

    public void PopPose()
    {
        if (_poseStack.Count > 1) _poseStack.Pop();
    }

    //PushScissor scaled pixels *guiScale to actual, intersected with the top so sub-containers clip within the parent
    public void PushScissor(int x, int y, int width, int height)
    {
        var ax = x * _guiScale;
        var ay = y * _guiScale;
        var aw = width * _guiScale;
        var ah = height * _guiScale;
        var current = _scissorStack.Peek();
        var ix = Math.Max(current.X, ax);
        var iy = Math.Max(current.Y, ay);
        var ir = Math.Min(current.Right, ax + aw);
        var ib = Math.Min(current.Bottom, ay + ah);
        var iw = Math.Max(0, ir - ix);
        var ih = Math.Max(0, ib - iy);
        _scissorStack.Push(new ScreenRectangle(ix, iy, iw, ih));
    }

    public void PopScissor()
    {
        if (_scissorStack.Count > 1) _scissorStack.Pop();
    }

    //BeginRecording pushes a recording level; during Submit it writes into this cache, nesting child-level caches
    public void BeginRecording(List<GuiElementRenderState> cache)
        => _recordingStack.Push(cache);

    //EndRecording pops the top recording level; later Submits no longer write into that cache
    public void EndRecording()
        => _recordingStack.Pop();

    //ReplayRange re-submits the cached RenderState list into the current frame's GuiRenderState
    //Non-dirty widgets skip Render and replay the previous frame's recorded result
    //While recording it also writes to the top cache so the parent cache collects the child's replayed RenderStates
    public void ReplayRange(IReadOnlyList<GuiElementRenderState> cached)
    {
        var top = _recordingStack.Count > 0 ? _recordingStack.Peek() : null;
        foreach (var s in cached)
        {
            _renderState.AddGuiElement(s);
            top?.Add(s);
        }
    }

    //Submit submits a RenderState to GuiRenderState; while recording it writes to all active stack levels
    //When a child is dirty the top is the child cache and the bottom the window cache; both are written
    private void Submit(GuiElementRenderState state)
    {
        _renderState.AddGuiElement(state);
        foreach (var cache in _recordingStack) cache.Add(state);
    }

    //BlurBeforeThisStratum opens a new stratum and marks prior strata as the pre-blur segment
    //It does not call Submit or record to cache; GuiWindow forces blur frames to skip the cache so it is called every frame
    public void BlurBeforeThisStratum()
    {
        _renderState.NextStratum();
        _renderState.BlurBeforeThisStratum();
    }

    //AddPictureInPicture submits PIP state to GuiRenderState for GuiRenderer.Prepare to call renderer.Prepare
    //maps to vanilla addPicturesInPictureState, not recorded to cache; PIP is re-submitted every frame
    public void AddPictureInPicture(PictureInPictureRenderState pip)
        => _renderState.AddPictureInPicture(pip);

    //ToActual converts scaled pixels to actual and returns the top-left and bottom-right coordinate pair
    private (int Ax, int Ay, int Ax1, int Ay1) ToActual(int x, int y, int width, int height)
    {
        var ax = x * _guiScale;
        var ay = y * _guiScale;
        var ax1 = ax + width * _guiScale;
        var ay1 = ay + height * _guiScale;
        return (ax, ay, ax1, ay1);
    }

    //ToIntColor converts GuiColor float 0-1 to an ARGB int 0xAARRGGBB
    //StagedVertexBuffer extracts the RGBA byte order matching the UByte4Norm vertex format
    private static int ToIntColor(GuiColor c)
    {
        var r = (byte)Math.Clamp((int)(c.R * 255), 0, 255);
        var g = (byte)Math.Clamp((int)(c.G * 255), 0, 255);
        var b = (byte)Math.Clamp((int)(c.B * 255), 0, 255);
        var a = (byte)Math.Clamp((int)(c.A * 255), 0, 255);
        return (a << 24) | (r << 16) | (g << 8) | b;
    }
}
