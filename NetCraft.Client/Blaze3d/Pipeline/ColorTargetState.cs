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
using NetCraft.Client.Blaze3d;
namespace NetCraft.Client.Blaze3d.Pipeline;

//ColorTargetState color attachment state, maps to vanilla ColorTargetState record
//Describes the format/blend/write mask of the pipeline output attachment
//blendFunction=null means blending is disabled; writeMask defaults to WRITE_ALL
public readonly record struct ColorTargetState(BlendFunction? BlendFunction, GpuFormat Format, int WriteMask)
{
    public const int WriteRed = 1;
    public const int WriteGreen = 2;
    public const int WriteBlue = 4;
    public const int WriteAlpha = 8;
    public const int WriteColor = WriteRed | WriteGreen | WriteBlue;
    public const int WriteAll = WriteRed | WriteGreen | WriteBlue | WriteAlpha;
    public const int WriteNone = 0;
    public const int MaxColorTargets = 8;

    //DEFAULT default state RGBA8_UNORM no blending all-channel write
    public static readonly ColorTargetState DEFAULT = new(null, GpuFormat.Rgba8Unorm, WriteAll);

    public ColorTargetState(BlendFunction blendFunction)
        : this(blendFunction, GpuFormat.Rgba8Unorm, WriteAll) { }

    public bool RedChannel => (WriteMask & WriteRed) != 0;
    public bool GreenChannel => (WriteMask & WriteGreen) != 0;
    public bool BlueChannel => (WriteMask & WriteBlue) != 0;
    public bool AlphaChannel => (WriteMask & WriteAlpha) != 0;
}
