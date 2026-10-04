using System;

namespace NetCraft.Gpu.Sprite;

//GuiSpriteScaling GUI sprite 缩放策略对标原版 GuiSpriteScaling
//三种实现 Stretch 整图拉伸 Tile 平铺 NineSlice 九宫格
//由 GuiMetadataSection.Parse 根据.mcmeta 的 gui.scaling 段构造
//用 abstract record 让派生 record 可继承 C# record 只能继承 record 或 object
public abstract record GuiSpriteScaling
{
    //Default 无.mcmeta 时返回 Stretch 对标原版 GuiSpriteScaling.DEFAULT
    public static GuiSpriteScaling Default { get; } = new StretchScaling();

    public abstract ScalingType Type { get; }
}

//ScalingType 三种缩放模式枚举对标原版 GuiSpriteScaling.Type
public enum ScalingType
{
    Stretch,
    Tile,
    NineSlice
}

//StretchScaling 整图拉伸默认实现对标原版 GuiSpriteScaling.Stretch
//UV 全图取目标矩形整体拉伸
public sealed record StretchScaling : GuiSpriteScaling
{
    public override ScalingType Type => ScalingType.Stretch;
}

//TileScaling 平铺 tileWidth/tileHeight 必须为正对标原版 GuiSpriteScaling.Tile
//目标区域按 tile 尺寸重复平铺边缘按比例截取 UV
public sealed record TileScaling(int Width, int Height) : GuiSpriteScaling
{
    public override ScalingType Type => ScalingType.Tile;
}

//NineSliceScaling 九宫格 border 四边独立 stretchInner 控制中心拉伸或平铺
//对标原版 GuiSpriteScaling.NineSlice + NineSlice.validate
public sealed record NineSliceScaling(int Width, int Height, NineSliceBorder Border, bool StretchInner) : GuiSpriteScaling
{
    public override ScalingType Type => ScalingType.NineSlice;

    //IsValid 对标原版 NineSlice.validate
    //border.left+border.right<width 且 border.top+border.bottom<height 才有中心切片
    public bool IsValid => Border.Left + Border.Right < Width && Border.Top + Border.Bottom < Height;
}

//NineSliceBorder 四边 border 支持统一值或独立值
//对标原版 GuiSpriteScaling.NineSlice.Border 双格式 Codec int 或 {left,top,right,bottom}
public sealed record NineSliceBorder(int Left, int Top, int Right, int Bottom)
{
    //Uniform 四边相同值工厂方法对标原版 Border.VALUE_CODEC
    public static NineSliceBorder Uniform(int size) => new(size, size, size, size);

    //IsUniform 四边是否相同决定序列化为 int 还是 object
    public bool IsUniform => Left == Top && Top == Right && Right == Bottom;
}
