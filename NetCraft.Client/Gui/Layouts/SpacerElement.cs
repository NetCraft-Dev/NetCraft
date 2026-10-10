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

//SpacerElement placeholder spacer element, maps to vanilla SpacerElement implements LayoutElement
//Occupies only width/height, renders nothing and ignores input; the layout engine uses it to open up space
//Width/Height are read-only after construction; X/Y can be moved by the layout engine
public sealed class SpacerElement : ILayoutElement
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; }
    public int Height { get; }

    public SpacerElement(int width, int height) : this(0, 0, width, height) { }

    public SpacerElement(int x, int y, int width, int height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    //OfWidth/OfHeight static factories creating a one-direction spacer with the other direction 0
    //C# disallows a static method and an instance property sharing a name, so an Of prefix is used, maps to vanilla width(int)/height(int)
    public static SpacerElement OfWidth(int width) => new(width, 0);
    public static SpacerElement OfHeight(int height) => new(0, height);
}
