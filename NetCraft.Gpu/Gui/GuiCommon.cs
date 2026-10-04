using System.Numerics;
using RenderPipeline = NetCraft.Gpu.Pipeline.RenderPipeline;

namespace NetCraft.Gpu;

//GuiColor RGBA 浮点颜色 0-1
public readonly record struct GuiColor(float R, float G, float B, float A)
{
    public static GuiColor White => new(1f, 1f, 1f, 1f);
    public static GuiColor Black => new(0f, 0f, 0f, 1f);
    public static GuiColor Red => new(1f, 0f, 0f, 1f);
    public static GuiColor Green => new(0f, 1f, 0f, 1f);
    public static GuiColor Blue => new(0f, 0f, 1f, 1f);
    public static GuiColor Transparent => new(0f, 0f, 0f, 0f);

    public static GuiColor FromRgb(int r, int g, int b) => new(r / 255f, g / 255f, b / 255f, 1f);
    public static GuiColor FromRgba(int r, int g, int b, int a) => new(r / 255f, g / 255f, b / 255f, a / 255f);
}

//GuiMouseButton 鼠标按键
[Flags]
public enum GuiMouseButton
{
    None = 0,
    Left = 1,
    Right = 2,
    Middle = 4
}

//MouseEventArgs 鼠标事件参数
public sealed class MouseEventArgs : EventArgs
{
    public GuiMouseButton Button { get; }
    public int X { get; }
    public int Y { get; }
    public int Clicks { get; set; } = 1;
    //Modifiers 派发时由 GuiWindow 填充供 TextBox 检测 Shift+点击扩展选区
    public KeyModifiers Modifiers { get; }

    public MouseEventArgs(GuiMouseButton button, int x, int y, KeyModifiers modifiers = KeyModifiers.None)
    {
        Button = button;
        X = x;
        Y = y;
        Modifiers = modifiers;
    }
}

//KeyEventArgs 键盘事件参数
public sealed class KeyEventArgs : EventArgs
{
    public int Key { get; }
    public char Char { get; }
    //Modifiers 派发时由 GuiWindow 根据修饰键状态填充供 TextBox 检测 Shift/Ctrl 组合
    public KeyModifiers Modifiers { get; }

    public KeyEventArgs(int key, char ch = '\0', KeyModifiers modifiers = KeyModifiers.None)
    {
        Key = key;
        Char = ch;
        Modifiers = modifiers;
    }
}

//KeyModifiers 修饰键标志位 TextBox 用 Shift 扩展选区 Ctrl 复制粘贴
[Flags]
public enum KeyModifiers
{
    None = 0,
    Shift = 1,
    Control = 2,
    Alt = 4
}

//GuiRectangle 整数矩形
public readonly record struct GuiRectangle(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;
    public int Bottom => Y + Height;

    public bool Contains(int x, int y) => x >= X && x < Right && y >= Y && y < Bottom;

    public GuiRectangle Offset(int dx, int dy) => new(X + dx, Y + dy, Width, Height);
}

//GuiTextAlign 文本水平对齐方式
public enum GuiTextAlign
{
    Left,
    Center,
    Right
}

//IGuiRenderContext GUI 渲染上下文
//控件通过此接口绘制自身 DrawText 基于 FontAtlas 字形图集渲染
public interface IGuiRenderContext
{
    //DrawQuad 绘制实心矩形
    void DrawQuad(int x, int y, int width, int height, GuiColor color);

    //DrawQuadInverted 绘制反色矩形 fragment shader 对 RGB 取反用于按钮按下等特效
    //对应原版 RenderPipelines.GUI_INVERT
    void DrawQuadInverted(int x, int y, int width, int height, GuiColor color);

    //DrawText 绘制文本字符尺寸由实现决定
    void DrawText(int x, int y, string text, GuiColor color);

    //MeasureText 测量文本像素宽度供控件计算对齐偏移
    float MeasureText(string text);

    //LineHeight 单行文本像素高度供控件计算多行换行间距
    int LineHeight { get; }

    //DrawImage 绘制已注册纹理的子区域到目标矩形 tint 调制颜色
    //textureId 由 RegisterTexture 返回 src 像素坐标源纹理内偏移
    void DrawImage(int textureId, int x, int y, int width, int height,
        int srcX, int srcY, int srcW, int srcH, GuiColor tint);

    //DrawImageNinePatch 九宫格切片绘制纹理 border 为源纹理四周固定不拉伸的边角像素宽度
    //旧 API 单一 border 中心固定拉伸目标/源尺寸不足 2*border 退化为 DrawImage
    //保留兼容现有 button 调用新 per-side border API 走 Math.min 钳制策略
    void DrawImageNinePatch(int textureId, int x, int y, int width, int height,
        int srcX, int srcY, int srcW, int srcH, int border, GuiColor tint);

    //DrawImageNinePatch per-side border + stretchInner 重载对标原版 blitNineSlicedSprite
    //borderLeft/Top/Right/Bottom 四边独立支持 slider_handle/tab 等 per-side border 控件
    //stretchInner=true 中心拉伸 false 中心按 sw×sh 平铺对应原版 stretch_inner 字段
    void DrawImageNinePatch(int textureId, int x, int y, int width, int height,
        int srcX, int srcY, int srcW, int srcH,
        int borderLeft, int borderTop, int borderRight, int borderBottom,
        bool stretchInner, GuiColor tint);

    //DrawTiledSprite 按 tileWidth/tileHeight 平铺纹理到目标区域
    //对标原版 blitTiledSprite 提交 TiledBlitRenderState 边缘按比例截取 UV
    //NineSlice stretchInner=false 中心平铺路径也走此方法
    void DrawTiledSprite(int textureId, int srcW, int srcH,
        int x, int y, int width, int height, GuiColor tint);

    //DrawSprite 按 identifier 取 sprite 并按 scaling 分派 Stretch/Tile/NineSlice
    //对标原版 GuiGraphicsExtractor.blitSprite 内部调 GuiSpriteManager.GetSprite
    //identifier 格式 "minecraft:textures/gui/sprites/widget/button"
    //textureId 与 scaling 由 GuiSpriteManager 解析.mcmeta 后填充到 GuiSprite
    void DrawSprite(string identifier, int x, int y, int width, int height, GuiColor tint);

    //DrawGlyphQuad 提交字形 quad 到渲染上下文 4 浮点顶点支持 italic/bold 偏移
    //pipeline 由 GlyphRenderTypes.Select 选取 textureSetup 绑定字形图集
    //x0/y0 左上 x1/y1 左下 x2/y2 右下 x3/y3 右上 UV 对应 (u0,v0)/(u0,v1)/(u1,v1)/(u1,v0)
    //对标原版 BakedSheetGlyph.renderChar 直接提交 4 顶点到 VertexConsumer
    void DrawGlyphQuad(RenderPipeline pipeline, TextureSetup textureSetup,
        float x0, float y0, float x1, float y1, float x2, float y2, float x3, float y3,
        float u0, float v0, float u1, float v1, int color);

    //PushPose 压入增量变换与当前栈顶相乘子控件相对父容器定位
    void PushPose(Matrix3x2 delta);

    //PopPose 弹出栈顶变换恢复到上一级
    void PopPose();

    //PushScissor 压入裁剪矩形与当前栈顶求交开新绘制段
    void PushScissor(int x, int y, int width, int height);

    //PopScissor 弹出裁剪矩形开新绘制段用新栈顶
    void PopScissor();

    //BeginRecording 开始录制提交的 RenderState 到 cache 供未 dirty 帧 replay
    //cache 由调用方拥有录制期间所有 DrawQuad/DrawText/DrawImage 提交同时写入 cache
    //阶段 6 retained mode 缓存机制控件未变化时跳过 Render 直接 ReplayRange
    void BeginRecording(List<GuiElementRenderState> cache);

    //EndRecording 结束录制后续提交只进 GuiRenderState 不写 cache
    void EndRecording();

    //ReplayRange 把缓存的 RenderState 列表重新提交到当前帧 GuiRenderState
    //用于未 dirty 控件跳过 Render 直接重放上一帧的 RenderState
    void ReplayRange(IReadOnlyList<GuiElementRenderState> cached);

    //BlurBeforeThisStratum 开新 stratum 并标记之前所有 strata 为 blur 前段
    //Screen 在 RenderBackground 末尾调用让背景进 BeforeBlur 段控件进 AfterBlur 段
    //blur 帧不走 retained mode cache 每帧重新 Render 确保 blur 标记不丢失
    void BlurBeforeThisStratum();

    //AddPictureInPicture 提交 PIP 状态到当前帧 GuiRenderState 供 GuiRenderer.Prepare 调 renderer.Prepare
    //Screen 在 Render 阶段构造 ItemPipState 等提交对标原版 addPicturesInPictureState
    void AddPictureInPicture(PictureInPictureRenderState pip);
}

//GuiKeys GLFW 键码常量与 Silk.NET.Input.Key 数值一致
//避免 Gpu 层依赖 Silk.NET.Input 命名空间 TextBox 用这些常量处理编辑键
public static class GuiKeys
{
    public const int Left = 263;
    public const int Right = 262;
    public const int Home = 268;
    public const int End = 269;
    public const int BackSpace = 259;
    public const int Delete = 261;
    public const int LeftShift = 340;
    public const int RightShift = 344;
    public const int LeftControl = 341;
    public const int RightControl = 345;
    public const int A = 65;
    public const int C = 67;
    public const int V = 86;
    public const int X = 88;
}
