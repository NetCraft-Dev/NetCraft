using System;

namespace NetCraft.Gpu.Sprite;

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
