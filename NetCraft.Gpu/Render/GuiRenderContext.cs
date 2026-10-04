using System.Numerics;
using NetCraft.Gpu.Font;
using NetCraft.Gpu.Pipeline;
using NetCraft.Gpu.Sprite;

namespace NetCraft.Gpu;

//GuiRenderContext submission 层 IGuiRenderContext 实现
//把控件 DrawQuad/DrawText/DrawImage 调用转换为 RenderState 提交到 GuiRenderState
//坐标系统一用 actual 像素控件 scaled 坐标 *guiScale 转 actual
//阶段 5b 替代 VulkanGuiRenderer 的 submission 职责 render phase 交给 GuiRenderer
//F7 DrawText 优先用 GlyphFont 动态烘焙路径 FontAtlas 为 fallback
//P0 DrawSprite 由 GuiSpriteManager 解析.mcmeta 后按 scaling 分派
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
    //_recordingStack 录制栈支持嵌套录制子控件 cache 和 window cache 同时活跃
    //BeginRecording 压栈 EndRecording 弹栈 Submit 写入栈所有层 ReplayRange 写入栈顶
    //空栈表示当前未录制 Submit 只进 GuiRenderState
    private readonly Stack<List<GuiElementRenderState>> _recordingStack = new();

    //Pose 栈顶存 actual pose PushPose 的 scaled delta 转 actual 后压栈
    private readonly Stack<Matrix3x2> _poseStack = new();
    //Scissor 栈存 actual 像素 vkCmdSetScissor 直接用
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

    //BeginFrame 重置 Pose/Scissor 栈由调用方在每帧 GuiRenderState.Reset 之后调
    public void BeginFrame()
    {
        _poseStack.Clear();
        _poseStack.Push(Matrix3x2.Identity);
        _scissorStack.Clear();
        _scissorStack.Push(new ScreenRectangle(0, 0, _surfaceWidth, _surfaceHeight));
    }

    //DrawQuad 绘制纯色矩形提交 ColoredRectangleRenderState 到 GUI pipeline
    public void DrawQuad(int x, int y, int width, int height, GuiColor color)
    {
        var (ax, ay, ax1, ay1) = ToActual(x, y, width, height);
        var state = new ColoredRectangleRenderState(
            RenderPipelines.GUI, TextureSetup.NoTexture, _poseStack.Peek(),
            ax, ay, ax1, ay1,
            ToIntColor(color), ToIntColor(color), _scissorStack.Peek());
        Submit(state);
    }

    //DrawQuadInverted 绘制反色矩形提交 ColoredRectangleRenderState 到 GUI_INVERT pipeline
    public void DrawQuadInverted(int x, int y, int width, int height, GuiColor color)
    {
        var (ax, ay, ax1, ay1) = ToActual(x, y, width, height);
        var state = new ColoredRectangleRenderState(
            RenderPipelines.GUI_INVERT, TextureSetup.NoTexture, _poseStack.Peek(),
            ax, ay, ax1, ay1,
            ToIntColor(color), ToIntColor(color), _scissorStack.Peek());
        Submit(state);
    }

    //DrawText 渲染文本 F7 优先用 Font 动态烘焙路径 FontAtlas 为 fallback
    //Font 路径调 Font.Draw 遍历文本 Bake+Render 提交 GlyphBlitRenderState
    //FontAtlas 路径每个字形一个 BlitRenderState 提交到 GUI_TEXT pipeline
    public void DrawText(int x, int y, string text, GuiColor color)
    {
        if (string.IsNullOrEmpty(text)) return;
        var intColor = ToIntColor(color);

        //Font 动态烘焙路径 BakedGlyph.Render 调 DrawGlyphQuad 提交 GlyphBlitRenderState
        if (_font is not null)
        {
            //x/y 是 scaled 坐标转 actual penY = y*guiScale + Ascent*guiScale
            float penX = x * _guiScale;
            float penY = y * _guiScale;
            _font.Draw(this, text, penX, penY, intColor, shadow: false);
            return;
        }

        //FontAtlas fallback 路径
        if (_fontAtlas is null || _fontTexture is null) return;

        //y 是顶部转基线用 Ascent 避免 -descent 导致中文错位
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

    //DrawGlyphQuad 提交字形 quad 到渲染上下文 4 浮点顶点支持 italic/bold 偏移
    //由 SheetBakedGlyph.Render 调用提交 GlyphBlitRenderState 到 GuiRenderState
    //pose/scissor 从栈顶读取 color 是 ARGB int
    public void DrawGlyphQuad(RenderPipeline pipeline, TextureSetup textureSetup,
        float x0, float y0, float x1, float y1, float x2, float y2, float x3, float y3,
        float u0, float v0, float u1, float v1, int color)
    {
        var state = new GlyphBlitRenderState(pipeline, textureSetup, _poseStack.Peek(),
            x0, y0, x1, y1, x2, y2, x3, y3,
            u0, v0, u1, v1, color, _scissorStack.Peek());
        Submit(state);
    }

    //MeasureText 返回 scaled 像素宽度供控件布局用
    //Font 优先返回 Font.MeasureText *guiScale 否则用 FontAtlas
    public float MeasureText(string text)
    {
        if (_font is not null) return _font.MeasureText(text) * _guiScale;
        return _fontAtlas?.MeasureText(text) ?? 0;
    }

    //LineHeight 返回 scaled 像素高度供控件计算多行换行间距
    //Font 优先返回 Font.LineHeight *guiScale 否则用 FontAtlas
    public int LineHeight => (_font?.LineHeight ?? _fontAtlas?.LineHeight ?? 0) * _guiScale;

    //DrawImage 绘制纹理子区域提交 BlitRenderState 到 GUI_TEXTURED pipeline
    //UV 由 src 像素与 GpuImage 尺寸计算 textureId 通过 textureResolver 解析为 TextureSetup
    //srcW/srcH<=0 表示用纹理全尺寸调用方不必预知纹理尺寸
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

    //DrawImageNinePatch 旧 API 单一 border 保留原九宫格实现不委托新 API
    //四边统一 border 中心固定拉伸 stretchInner=true 等价行为
    //目标/源尺寸不足 2*border 退化为 DrawImage 避免负区域兼容现有 button 行为
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
        //4 角固定
        DrawImage(textureId, dxL, dyT, border, border, sxL, syT, border, border, tint);
        DrawImage(textureId, dxR, dyT, border, border, sxR, syT, border, border, tint);
        DrawImage(textureId, dxL, dyB, border, border, sxL, syB, border, border, tint);
        DrawImage(textureId, dxR, dyB, border, border, sxR, syB, border, border, tint);
        //上下边 dw×border 横向拉伸
        DrawImage(textureId, dxM, dyT, dw, border, sxM, syT, sw, border, tint);
        DrawImage(textureId, dxM, dyB, dw, border, sxM, syB, sw, border, tint);
        //左右边 border×dh 纵向拉伸
        DrawImage(textureId, dxL, dyM, border, dh, sxL, syM, border, sh, tint);
        DrawImage(textureId, dxR, dyM, border, dh, sxR, syM, border, sh, tint);
        //中心 dw×dh 双向拉伸
        DrawImage(textureId, dxM, dyM, dw, dh, sxM, syM, sw, sh, tint);
    }

    //DrawImageNinePatch per-side border + stretchInner 新 API 对标原版 blitNineSlicedSprite
    //四边独立 border 支持 slider_handle/tab 等 per-side border 控件
    //border 钳制到目标尺寸一半避免负区域对标原版 Math.min(border, width/2)
    //stretchInner=true 中心拉伸 false 中心按 sw×sh 平铺对应原版 stretch_inner 字段
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
        //border 钳制对标原版 Math.min(border, width/2) 避免负区域不退化为 DrawImage
        int bl = Math.Min(borderLeft, width / 2);
        int bt = Math.Min(borderTop, height / 2);
        int br = Math.Min(borderRight, width / 2);
        int bb = Math.Min(borderBottom, height / 2);
        //源尺寸不足 border 之和退化为 DrawImage
        if (srcW < bl + br || srcH < bt + bb)
        {
            DrawImage(textureId, x, y, width, height, srcX, srcY, srcW, srcH, tint);
            return;
        }
        var dw = width - bl - br;
        var dh = height - bt - bb;
        var sw = srcW - bl - br;
        var sh = srcH - bt - bb;
        //4 角固定
        DrawImage(textureId, x, y, bl, bt, srcX, srcY, bl, bt, tint);
        DrawImage(textureId, x + width - br, y, br, bt,
            srcX + srcW - br, srcY, br, bt, tint);
        DrawImage(textureId, x, y + height - bb, bl, bb,
            srcX, srcY + srcH - bb, bl, bb, tint);
        DrawImage(textureId, x + width - br, y + height - bb, br, bb,
            srcX + srcW - br, srcY + srcH - bb, br, bb, tint);
        //上下边固定拉伸 stretchInner 只影响中心
        DrawImage(textureId, x + bl, y, dw, bt, srcX + bl, srcY, sw, bt, tint);
        DrawImage(textureId, x + bl, y + height - bb, dw, bb,
            srcX + bl, srcY + srcH - bb, sw, bb, tint);
        //左右边固定拉伸 stretchInner 只影响中心
        DrawImage(textureId, x, y + bt, bl, dh, srcX, srcY + bt, bl, sh, tint);
        DrawImage(textureId, x + width - br, y + bt, br, dh,
            srcX + srcW - br, srcY + bt, br, sh, tint);
        //中心 stretchInner=true 拉伸 false 平铺对标原版 stretch_inner
        BlitInnerSegment(textureId, x + bl, y + bt, dw, dh,
            srcX + bl, srcY + bt, sw, sh, stretchInner, tint);
    }

    //DrawTiledSprite 按 tileWidth/tileHeight 平铺纹理提交 TiledBlitRenderState
    //对标原版 blitTiledSprite 双层循环平铺边缘按比例截取 UV
    //NineSlice stretchInner=false 中心平铺路径也走此方法
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

    //BlitInnerSegment 中心段拉伸或平铺对标原版 blitNineSliceInnerSegment
    //stretchInner=true 走 DrawImage 拉伸 false 走 DrawTiledSprite 平铺
    //dw/dh<=0 跳过避免提交空区域
    private void BlitInnerSegment(int textureId, int dx, int dy, int dw, int dh,
        int sx, int sy, int sw, int sh, bool stretchInner, GuiColor tint)
    {
        if (dw <= 0 || dh <= 0) return;
        if (stretchInner)
            DrawImage(textureId, dx, dy, dw, dh, sx, sy, sw, sh, tint);
        else
            DrawTiledSprite(textureId, sw, sh, dx, dy, dw, dh, tint);
    }

    //DrawSprite 按 identifier 取 sprite 按 scaling 分派对标原版 blitSprite
    //Stretch 走 DrawImage Tile 走 DrawTiledSprite NineSlice 走 DrawImageNinePatch
    //sprite 未加载或 GuiSpriteManager 为 null 静默返回避免崩溃
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

    //PushPose 压入 scaled delta 转 actual 后与栈顶相乘子控件相对父容器定位
    //translation *guiScale rotation/scale 不变保持旋转缩放比例
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

    //PushScissor scaled 像素 *guiScale 转 actual 与栈顶求交子容器裁剪不超出父容器
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

    //BeginRecording 压入录制层 Submit 期间写入此 cache 子控件级 cache 嵌套压栈
    public void BeginRecording(List<GuiElementRenderState> cache)
        => _recordingStack.Push(cache);

    //EndRecording 弹出栈顶录制层后续 Submit 不再写入该 cache
    public void EndRecording()
        => _recordingStack.Pop();

    //ReplayRange 把缓存的 RenderState 列表重新提交到当前帧 GuiRenderState
    //未 dirty 控件跳过 Render 直接重放上一帧录制的结果
    //录制活跃时同时写入栈顶 cache 让父级 cache 收集子控件 replay 的 RenderState
    public void ReplayRange(IReadOnlyList<GuiElementRenderState> cached)
    {
        var top = _recordingStack.Count > 0 ? _recordingStack.Peek() : null;
        foreach (var s in cached)
        {
            _renderState.AddGuiElement(s);
            top?.Add(s);
        }
    }

    //Submit 提交 RenderState 到 GuiRenderState 录制期间写入栈所有活跃层
    //子控件 dirty 时栈顶是 child cache 栈底是 window cache 两层都写
    private void Submit(GuiElementRenderState state)
    {
        _renderState.AddGuiElement(state);
        foreach (var cache in _recordingStack) cache.Add(state);
    }

    //BlurBeforeThisStratum 开新 stratum 并标记之前 strata 为 blur 前段
    //不调 Submit 不录制到 cache blur 帧由 GuiWindow 强制不走 cache 确保每帧重新调
    public void BlurBeforeThisStratum()
    {
        _renderState.NextStratum();
        _renderState.BlurBeforeThisStratum();
    }

    //AddPictureInPicture 提交 PIP 状态到 GuiRenderState 供 GuiRenderer.Prepare 调 renderer.Prepare
    //对标原版 addPicturesInPictureState 不录制到 cache PIP 每帧重新提交
    public void AddPictureInPicture(PictureInPictureRenderState pip)
        => _renderState.AddPictureInPicture(pip);

    //ToActual scaled 像素转 actual 返回左上和右下坐标对
    private (int Ax, int Ay, int Ax1, int Ay1) ToActual(int x, int y, int width, int height)
    {
        var ax = x * _guiScale;
        var ay = y * _guiScale;
        var ax1 = ax + width * _guiScale;
        var ay1 = ay + height * _guiScale;
        return (ax, ay, ax1, ay1);
    }

    //ToIntColor GuiColor 浮点 0-1 转 ARGB int 0xAARRGGBB
    //StagedVertexBuffer 提取为 RGBA 字节顺序匹配 UByte4Norm 顶点格式
    private static int ToIntColor(GuiColor c)
    {
        var r = (byte)Math.Clamp((int)(c.R * 255), 0, 255);
        var g = (byte)Math.Clamp((int)(c.G * 255), 0, 255);
        var b = (byte)Math.Clamp((int)(c.B * 255), 0, 255);
        var a = (byte)Math.Clamp((int)(c.A * 255), 0, 255);
        return (a << 24) | (r << 16) | (g << 8) | b;
    }
}
