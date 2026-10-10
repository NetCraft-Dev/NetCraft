using System.Numerics;
using RenderPipeline = NetCraft.Client.Blaze3d.Pipeline.RenderPipeline;
using NetCraft.Client.Blaze3d.Systems;
using NetCraft.Client.Blaze3d.Buffers;
using NetCraft.Client.Blaze3d.Textures;
using NetCraft.Client.Blaze3d.Shaders;
using NetCraft.Client.Blaze3d.Pipeline;
using NetCraft.Client.Blaze3d.Vertex;
using NetCraft.Client.Blaze3d.Vulkan;
using NetCraft.Client.Blaze3d.Platform;
using NetCraft.Client.Blaze3d.Font;
using NetCraft.Client.Blaze3d.Resource;
using NetCraft.Client.Blaze3d.Audio;
using NetCraft.Client.Blaze3d.Framegraph;
using NetCraft.Client.Blaze3d.Preprocessor;
using NetCraft.Client.Blaze3d.Util;
using NetCraft.Client.Render;
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

namespace NetCraft.Client.Gui;

//GuiColor RGBA float color 0-1
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

//GuiMouseButton mouse button
[Flags]
public enum GuiMouseButton
{
    None = 0,
    Left = 1,
    Right = 2,
    Middle = 4
}

//MouseEventArgs mouse event args
public sealed class MouseEventArgs : EventArgs
{
    public GuiMouseButton Button { get; }
    public int X { get; }
    public int Y { get; }
    public int Clicks { get; set; } = 1;
    //Modifiers filled in by GuiWindow on dispatch so TextBox can detect Shift+click to extend the selection
    public KeyModifiers Modifiers { get; }

    public MouseEventArgs(GuiMouseButton button, int x, int y, KeyModifiers modifiers = KeyModifiers.None)
    {
        Button = button;
        X = x;
        Y = y;
        Modifiers = modifiers;
    }
}

//KeyEventArgs keyboard event args
public sealed class KeyEventArgs : EventArgs
{
    public int Key { get; }
    public char Char { get; }
    //Modifiers filled in by GuiWindow from the modifier state on dispatch so TextBox can detect Shift/Ctrl combos
    public KeyModifiers Modifiers { get; }

    public KeyEventArgs(int key, char ch = '\0', KeyModifiers modifiers = KeyModifiers.None)
    {
        Key = key;
        Char = ch;
        Modifiers = modifiers;
    }
}

//KeyModifiers modifier flags; TextBox uses Shift to extend the selection and Ctrl for copy/paste
[Flags]
public enum KeyModifiers
{
    None = 0,
    Shift = 1,
    Control = 2,
    Alt = 4
}

//GuiRectangle integer rectangle
public readonly record struct GuiRectangle(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;
    public int Bottom => Y + Height;

    public bool Contains(int x, int y) => x >= X && x < Right && y >= Y && y < Bottom;

    public GuiRectangle Offset(int dx, int dy) => new(X + dx, Y + dy, Width, Height);
}

//GuiTextAlign horizontal text alignment
public enum GuiTextAlign
{
    Left,
    Center,
    Right
}

//IGuiRenderContext GUI render context
//Widgets draw themselves through this interface; DrawText renders via the FontAtlas glyph atlas
public interface IGuiRenderContext
{
    //DrawQuad draws a solid rectangle
    void DrawQuad(int x, int y, int width, int height, GuiColor color);

    //DrawQuadInverted draws an inverted rectangle; the fragment shader inverts RGB, used for button-press effects
    //corresponds to vanilla RenderPipelines.GUI_INVERT
    void DrawQuadInverted(int x, int y, int width, int height, GuiColor color);

    //DrawText draws text; the character size is decided by the implementation
    void DrawText(int x, int y, string text, GuiColor color);

    //MeasureText measures the text pixel width for widgets to compute alignment offsets
    float MeasureText(string text);

    //LineHeight single-line text pixel height for widgets to compute multi-line spacing
    int LineHeight { get; }

    //DrawImage draws a sub-region of a registered texture into the target rectangle; tint modulates the color
    //textureId comes from RegisterTexture; src is the pixel-space offset inside the source texture
    void DrawImage(int textureId, int x, int y, int width, int height,
        int srcX, int srcY, int srcW, int srcH, GuiColor tint);

    //DrawImageNinePatch draws a nine-slice texture; border is the pixel width of the fixed, non-stretched corners of the source texture
    //Legacy API with a single border and a fixed stretched center; falls back to DrawImage when the target/source size is under 2*border
    //Kept for compatibility with existing button calls; the new per-side border API uses Math.min clamping
    void DrawImageNinePatch(int textureId, int x, int y, int width, int height,
        int srcX, int srcY, int srcW, int srcH, int border, GuiColor tint);

    //DrawImageNinePatch per-side border + stretchInner overload, maps to vanilla blitNineSlicedSprite
    //borderLeft/Top/Right/Bottom are independent per side, supporting per-side border widgets like slider_handle/tab
    //stretchInner=true stretches the center, false tiles it at sw×sh, corresponding to the vanilla stretch_inner field
    void DrawImageNinePatch(int textureId, int x, int y, int width, int height,
        int srcX, int srcY, int srcW, int srcH,
        int borderLeft, int borderTop, int borderRight, int borderBottom,
        bool stretchInner, GuiColor tint);

    //DrawTiledSprite tiles the texture into the target area by tileWidth/tileHeight
    //maps to vanilla blitTiledSprite, submitting a TiledBlitRenderState and cropping UVs proportionally at the edges
    //The NineSlice stretchInner=false center-tile path also goes through this method
    void DrawTiledSprite(int textureId, int srcW, int srcH,
        int x, int y, int width, int height, GuiColor tint);

    //DrawSprite looks up a sprite by identifier and dispatches Stretch/Tile/NineSlice by scaling
    //maps to vanilla GuiGraphicsExtractor.blitSprite, internally calling GuiSpriteManager.GetSprite
    //identifier format "minecraft:textures/gui/sprites/widget/button"
    //textureId and scaling are filled into GuiSprite after GuiSpriteManager parses .mcmeta
    void DrawSprite(string identifier, int x, int y, int width, int height, GuiColor tint);

    //DrawGlyphQuad submits a glyph quad to the render context; 4 float vertices support italic/bold offsets
    //pipeline is chosen by GlyphRenderTypes.Select and textureSetup binds the glyph atlas
    //x0/y0 top-left x1/y1 bottom-left x2/y2 bottom-right x3/y3 top-right, UVs corresponding to (u0,v0)/(u0,v1)/(u1,v1)/(u1,v0)
    //maps to vanilla BakedSheetGlyph.renderChar, submitting the 4 vertices directly to the VertexConsumer
    void DrawGlyphQuad(RenderPipeline pipeline, TextureSetup textureSetup,
        float x0, float y0, float x1, float y1, float x2, float y2, float x3, float y3,
        float u0, float v0, float u1, float v1, int color);

    //PushPose pushes an incremental transform multiplied with the current top; child widgets position relative to the parent
    void PushPose(Matrix3x2 delta);

    //PopPose pops the top transform, restoring the previous level
    void PopPose();

    //PushScissor pushes a scissor rectangle intersected with the current top and starts a new draw segment
    void PushScissor(int x, int y, int width, int height);

    //PopScissor pops the scissor rectangle and starts a new draw segment with the new top
    void PopScissor();

    //BeginRecording starts recording submitted RenderStates into cache for replay on non-dirty frames
    //The cache is owned by the caller; during recording all DrawQuad/DrawText/DrawImage submits also write to cache
    //Stage 6 retained-mode caching: widgets that did not change skip Render and directly ReplayRange
    void BeginRecording(List<GuiElementRenderState> cache);

    //EndRecording ends recording; later submits only go to GuiRenderState and not to cache
    void EndRecording();

    //ReplayRange re-submits the cached RenderState list into the current frame's GuiRenderState
    //Used for non-dirty widgets to skip Render and replay the previous frame's RenderStates
    void ReplayRange(IReadOnlyList<GuiElementRenderState> cached);

    //BlurBeforeThisStratum opens a new stratum and marks all previous strata as the pre-blur segment
    //Screen calls it at the end of RenderBackground so the background goes into BeforeBlur and widgets into AfterBlur
    //Blur frames skip the retained-mode cache and re-Render every frame so the blur marker is not lost
    void BlurBeforeThisStratum();

    //AddPictureInPicture submits PIP state into the current frame's GuiRenderState for GuiRenderer.Prepare to call renderer.Prepare
    //Screen builds ItemPipState etc. during Render and submits them, maps to vanilla addPicturesInPictureState
    void AddPictureInPicture(PictureInPictureRenderState pip);
}

//GuiKeys GLFW key code constants matching Silk.NET.Input.Key values
//Avoids the GPU layer depending on the Silk.NET.Input namespace; TextBox uses these constants for editing keys
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
