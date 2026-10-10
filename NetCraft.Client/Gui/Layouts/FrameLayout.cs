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
namespace NetCraft.Client.Gui.Layouts;

//FrameLayout frame layout, maps to vanilla FrameLayout extends AbstractLayout
//All children align within the same rectangle by their own align, centered by default
//Provides static centerInRectangle/alignInRectangle for manual positioning
public sealed class FrameLayout : AbstractLayout
{
    private readonly List<ChildContainer> _children = new();
    private int _minWidth;
    private int _minHeight;
    private LayoutSettings _defaultChildSettings;

    public FrameLayout() : this(0, 0, 0, 0) { }

    public FrameLayout(int minWidth, int minHeight) : this(0, 0, minWidth, minHeight) { }

    public FrameLayout(int x, int y, int minWidth, int minHeight) : base(x, y, minWidth, minHeight)
    {
        //Children are centered by default, maps to vanilla defaults().align(0.5,0.5)
        _defaultChildSettings = LayoutSettings.Defaults().Align(0.5f, 0.5f);
        SetMinDimensions(minWidth, minHeight);
    }

    //SetMinWidth/SetMinHeight/SetMinDimensions chained minimum-size setters
    //ArrangeElements result size is never smaller than this
    public FrameLayout SetMinWidth(int minWidth) { _minWidth = minWidth; return this; }
    public FrameLayout SetMinHeight(int minHeight) { _minHeight = minHeight; return this; }
    public FrameLayout SetMinDimensions(int minWidth, int minHeight)
        => SetMinWidth(minWidth).SetMinHeight(minHeight);

    public LayoutSettings NewChildLayoutSettings() => _defaultChildSettings.Copy();
    public LayoutSettings DefaultChildLayoutSetting() => _defaultChildSettings;

    //ArrangeElements takes the children's max width/height, not below minDim, then positions by align within the result rectangle
    public override void ArrangeElements()
    {
        base.ArrangeElements();
        int resultWidth = _minWidth;
        int resultHeight = _minHeight;
        foreach (var c in _children)
        {
            resultWidth = Math.Max(resultWidth, c.GetWidth());
            resultHeight = Math.Max(resultHeight, c.GetHeight());
        }
        foreach (var c in _children)
        {
            c.SetX(X, resultWidth);
            c.SetY(Y, resultHeight);
        }
        _width = resultWidth;
        _height = resultHeight;
    }

    public T AddChild<T>(T child) where T : ILayoutElement
        => AddChild(child, NewChildLayoutSettings());

    public T AddChild<T>(T child, LayoutSettings settings) where T : ILayoutElement
    {
        _children.Add(new ChildContainer(child, settings));
        return child;
    }

    public override void VisitChildren(Action<ILayoutElement> visitor)
    {
        foreach (var c in _children) visitor(c.Child);
    }

    public override void RemoveChildren() => _children.Clear();

    //CenterInRectangle centers the element in the given rectangle
    public static void CenterInRectangle(ILayoutElement widget, int x, int y, int width, int height)
        => AlignInRectangle(widget, x, y, width, height, 0.5f, 0.5f);

    public static void CenterInRectangle(ILayoutElement widget, GuiRectangle rectangle)
        => CenterInRectangle(widget, rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);

    //AlignInRectangle aligns the element within the rectangle by alignX/alignY
    public static void AlignInRectangle(ILayoutElement widget, GuiRectangle rectangle, float alignX, float alignY)
        => AlignInRectangle(widget, rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height, alignX, alignY);

    public static void AlignInRectangle(ILayoutElement widget, int x, int y, int width, int height, float alignX, float alignY)
    {
        AlignInDimension(x, width, widget.Width, v => widget.X = v, alignX);
        AlignInDimension(y, height, widget.Height, v => widget.Y = v, alignY);
    }

    //AlignInDimension places a widgetLength element by align within pos start and length
    //align=0 left/top align=0.5 center align=1 right/bottom
    public static void AlignInDimension(int pos, int length, int widgetLength, Action<int> setWidgetPos, float align)
    {
        int offset = (int)Math.Round(Lerp(align, 0.0f, length - widgetLength));
        setWidgetPos(pos + offset);
    }

    private static float Lerp(float t, float a, float b) => a + (b - a) * t;

    //ChildContainer frame-layout child container wrapping only child+settings without extra row/column state
    private sealed class ChildContainer : ChildWrapper
    {
        public ChildContainer(ILayoutElement child, LayoutSettings settings) : base(child, settings) { }
    }
}
