using System;
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

namespace NetCraft.Client.Resources.Metadata.Gui;

//GuiSpriteScaling GUI sprite scaling strategy, maps to vanilla GuiSpriteScaling
//Three implementations Stretch whole-image stretch Tile tiling NineSlice nine-slice
//Built by GuiMetadataSection.Parse from the .mcmeta gui.scaling section
//Uses an abstract record so derived records can inherit; C# records can only inherit a record or object
public abstract record GuiSpriteScaling
{
    //Default returns Stretch when there is no .mcmeta, maps to vanilla GuiSpriteScaling.DEFAULT
    public static GuiSpriteScaling Default { get; } = new StretchScaling();

    public abstract ScalingType Type { get; }
}

//ScalingType three scaling mode enum, maps to vanilla GuiSpriteScaling.Type
public enum ScalingType
{
    Stretch,
    Tile,
    NineSlice
}

//StretchScaling whole-image stretch default implementation, maps to vanilla GuiSpriteScaling.Stretch
//The whole-image UV is stretched across the target rectangle
public sealed record StretchScaling : GuiSpriteScaling
{
    public override ScalingType Type => ScalingType.Stretch;
}

//TileScaling tiling; tileWidth/tileHeight must be positive, maps to vanilla GuiSpriteScaling.Tile
//The target area is repeatedly tiled at the tile size with UVs cropped proportionally at the edges
public sealed record TileScaling(int Width, int Height) : GuiSpriteScaling
{
    public override ScalingType Type => ScalingType.Tile;
}

//NineSliceScaling nine-slice with independent borders per side; stretchInner controls whether the center stretches or tiles
//maps to vanilla GuiSpriteScaling.NineSlice + NineSlice.validate
public sealed record NineSliceScaling(int Width, int Height, NineSliceBorder Border, bool StretchInner) : GuiSpriteScaling
{
    public override ScalingType Type => ScalingType.NineSlice;

    //IsValid maps to vanilla NineSlice.validate
    //A center slice exists only when border.left+border.right<width and border.top+border.bottom<height
    public bool IsValid => Border.Left + Border.Right < Width && Border.Top + Border.Bottom < Height;
}

//NineSliceBorder borders per side, supporting a uniform value or independent values
//maps to vanilla GuiSpriteScaling.NineSlice.Border dual-format codec int or {left,top,right,bottom}
public sealed record NineSliceBorder(int Left, int Top, int Right, int Bottom)
{
    //Uniform factory method for the same value on all sides, maps to vanilla Border.VALUE_CODEC
    public static NineSliceBorder Uniform(int size) => new(size, size, size, size);

    //IsUniform whether all four sides are equal, deciding serialization as int or object
    public bool IsUniform => Left == Top && Top == Right && Right == Bottom;
}
