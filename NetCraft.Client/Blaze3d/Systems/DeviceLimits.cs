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
namespace NetCraft.Client.Blaze3d.Systems;

//DeviceLimits GPU hardware limits, aligns with vanilla com.mojang.blaze3d.systems.DeviceLimits
public sealed record DeviceLimits(
    int MaxAnisotropy,
    int MinUniformOffsetAlignment,
    int MaxTextureSize,
    long MaxMemoryAllocationSize,
    int MaxMultiDrawDirectInterleavedDrawCount,
    int MaxColorAttachments)
{
    //MaxTextureSizeForFormat clamps the texture size by the allocation budget, maps to vanilla maxTextureSizeForFormat
    public int MaxTextureSizeForFormat(GpuFormat format)
        => HighestOneBit(Math.Min(MaxTextureSize, (int)Math.Sqrt((double)MaxMemoryAllocationSize / format.BlockSize())));

    //HighestOneBit the highest power of two not exceeding value, maps to vanilla Integer.highestOneBit
    private static int HighestOneBit(int value)
    {
        value |= value >> 1;
        value |= value >> 2;
        value |= value >> 4;
        value |= value >> 8;
        value |= value >> 16;
        return value - (value >> 1);
    }
}
