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

//AbstractLayout layout base class, maps to vanilla AbstractLayout implements Layout
//Holds x/y/width/height; the X/Y setters offset all children keeping their relative positions
//Subclasses implement VisitChildren/RemoveChildren and override ArrangeElements to add their own layout logic
//ChildWrapper wraps a child + LayoutSettings; SetX/SetY use lerp to compute the align offset
public abstract class AbstractLayout : ILayout
{
    private int _x;
    private int _y;
    protected int _width;
    protected int _height;

    protected AbstractLayout(int x, int y, int width, int height)
    {
        _x = x;
        _y = y;
        _width = width;
        _height = height;
    }

    //The X setter offsets all children by dx keeping relative positions, then sets its own x
    //Children follow when the layout engine moves the whole Layout, without detaching
    public int X
    {
        get => _x;
        set
        {
            int dx = value - _x;
            if (dx != 0) VisitChildren(child => child.X += dx);
            _x = value;
        }
    }

    public int Y
    {
        get => _y;
        set
        {
            int dy = value - _y;
            if (dy != 0) VisitChildren(child => child.Y += dy);
            _y = value;
        }
    }

    public int Width => _width;
    public int Height => _height;

    //SetPosition convenience method setting both X/Y, maps to vanilla LayoutElement.setPosition
    //C# interface default methods cannot be called directly on an instance, so an instance implementation is provided here
    public void SetPosition(int x, int y)
    {
        X = x;
        Y = y;
    }

    //ArrangeElements recursively arranges child Layouts by default; subclasses override, add their logic and call base
    //C# interface default methods cannot be called directly on an instance, so AbstractLayout provides an instance implementation
    public virtual void ArrangeElements()
    {
        VisitChildren(child =>
        {
            if (child is ILayout layout)
                layout.ArrangeElements();
        });
    }

    public abstract void VisitChildren(Action<ILayoutElement> visitor);
    public abstract void RemoveChildren();

    //ChildWrapper wraps a child + LayoutSettings and provides GetWidth/GetHeight including padding
    //SetX/SetY lerp the offset by align within availableSpace
    //paddingLeft fixes the start, paddingRight the end; align=0 left, 0.5 center, 1 right
    protected abstract class ChildWrapper
    {
        public readonly ILayoutElement Child;
        public readonly LayoutSettings.Impl Settings;

        protected ChildWrapper(ILayoutElement child, LayoutSettings settings)
        {
            Child = child;
            Settings = settings.GetExposed();
        }

        //GetHeight includes top/bottom padding, used by the layout engine for row height
        public int GetHeight() => Child.Height + Settings.PaddingTopValue + Settings.PaddingBottomValue;

        //GetWidth includes left/right padding, used by the layout engine for column width
        public int GetWidth() => Child.Width + Settings.PaddingLeftValue + Settings.PaddingRightValue;

        //SetX computes the child x offset by align within availableSpace starting at x
        //least=paddingLeft start most=availableSpace-childWidth-paddingRight end
        //align=0 left at paddingLeft align=0.5 center align=1 right at paddingRight
        public void SetX(int x, int availableSpace)
        {
            float least = Settings.PaddingLeftValue;
            float most = availableSpace - Child.Width - Settings.PaddingRightValue;
            int offset = (int)Math.Round(Lerp(Settings.XAlignment, least, most));
            Child.X = offset + x;
        }

        public void SetY(int y, int availableSpace)
        {
            float least = Settings.PaddingTopValue;
            float most = availableSpace - Child.Height - Settings.PaddingBottomValue;
            int offset = (int)Math.Round(Lerp(Settings.YAlignment, least, most));
            Child.Y = offset + y;
        }

        private static float Lerp(float t, float a, float b) => a + (b - a) * t;
    }
}
