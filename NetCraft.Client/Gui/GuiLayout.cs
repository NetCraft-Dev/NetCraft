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

//GuiLayoutOrientation linear layout orientation
public enum GuiLayoutOrientation
{
    Horizontal,
    Vertical
}

//IGuiLayout layout engine interface measuring container children and arranging their positions
//GuiContainer.Update calls Measure every frame so children reflow with the container size
public interface IGuiLayout
{
    //Measure sets child X/Y per the layout strategy; child Width/Height stay unchanged
    void Measure(GuiContainer container);
}

//GuiLinearLayout linear layout stacking children horizontally or vertically
//Padding container padding Spacing child spacing; child Width/Height stay unchanged, only X/Y is arranged
//corresponds to vanilla client.gui.layouts.LinearLayout
public sealed class GuiLinearLayout : IGuiLayout
{
    public GuiLayoutOrientation Orientation { get; set; }
    public int Padding { get; set; } = 4;
    public int Spacing { get; set; } = 4;

    public GuiLinearLayout(GuiLayoutOrientation orientation = GuiLayoutOrientation.Vertical)
    {
        Orientation = orientation;
    }

    public void Measure(GuiContainer container)
    {
        var x = container.X + Padding;
        var y = container.Y + Padding;
        foreach (var child in container.Children)
        {
            if (!child.Visible) continue;
            child.X = x;
            child.Y = y;
            if (Orientation == GuiLayoutOrientation.Vertical)
                y += child.Height + Spacing;
            else
                x += child.Width + Spacing;
        }
    }
}
