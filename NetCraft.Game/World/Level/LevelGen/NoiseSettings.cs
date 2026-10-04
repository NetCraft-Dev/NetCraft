using NetCraft.Codec;
using NetCraft.Primitives;
using NetCraft.Storage.Chunk;

namespace NetCraft.Game.World.Level.LevelGen;

//NoiseSettings 噪声设置对应原版 net.minecraft.world.level.levelgen.NoiseSettings
//描述维度噪声采样网格minY/height 定 y 范围noiseSize 决定 cell 宽高
//主世界 create(-64,384,1,2) → cellWidth=4 cellHeight=8
public sealed class NoiseSettings
{
    //对应原版 DimensionType.MIN_Y/MAX_Y 校验边界
    private const int MinAllowedY = -2032;
    private const int MaxAllowedY = 2032;

    //Codec 噪声设置 JSON 编解码对应原版 NoiseSettings.CODEC
    //字段 min_y/height/size_horizontal/size_verticalguardY 校验失败返回 Error
    public static readonly Codec<NoiseSettings> Codec = BuildCodec();

    public int MinY { get; }
    public int Height { get; }
    public int NoiseSizeHorizontal { get; }
    public int NoiseSizeVertical { get; }

    private NoiseSettings(int minY, int height, int noiseSizeHorizontal, int noiseSizeVertical)
    {
        MinY = minY;
        Height = height;
        NoiseSizeHorizontal = noiseSizeHorizontal;
        NoiseSizeVertical = noiseSizeVertical;
    }

    //BuildCodec 先按四字段构造再包一层 guardY 校验
    private static Codec<NoiseSettings> BuildCodec()
    {
        var fields = RecordCodecBuilder.Of4(
            Codecs.Int.FieldOf("min_y").ForGetter<NoiseSettings, int>(s => s.MinY),
            Codecs.Int.FieldOf("height").ForGetter<NoiseSettings, int>(s => s.Height),
            Codecs.Int.FieldOf("size_horizontal").ForGetter<NoiseSettings, int>(s => s.NoiseSizeHorizontal),
            Codecs.Int.FieldOf("size_vertical").ForGetter<NoiseSettings, int>(s => s.NoiseSizeVertical),
            (minY, height, sizeHorizontal, sizeVertical)
                => new NoiseSettings(minY, height, sizeHorizontal, sizeVertical));
        return fields.ComapFlatMap(GuardYResult, s => s);
    }

    //GuardYResult 校验失败返回 Error 对应原版 guardY 的 DataResult 形式
    private static DataResult<NoiseSettings> GuardYResult(NoiseSettings settings)
    {
        if (settings.MinY + settings.Height > MaxAllowedY + 1)
            return DataResult<NoiseSettings>.Error(() => $"min_y + height cannot be higher than: {MaxAllowedY + 1}");
        if (settings.Height % 16 != 0)
            return DataResult<NoiseSettings>.Error(() => "height has to be a multiple of 16");
        if (settings.MinY % 16 != 0)
            return DataResult<NoiseSettings>.Error(() => "min_y has to be a multiple of 16");
        return DataResult<NoiseSettings>.Success(settings);
    }

    //Create 带校验工厂对应原版 create
    public static NoiseSettings Create(int minY, int height, int noiseSizeHorizontal, int noiseSizeVertical)
    {
        var settings = new NoiseSettings(minY, height, noiseSizeHorizontal, noiseSizeVertical);
        GuardY(settings);
        return settings;
    }

    //GuardY 校验 y 范围与 16 倍数对应原版 guardY
    private static void GuardY(NoiseSettings settings)
    {
        if (settings.MinY + settings.Height > MaxAllowedY + 1)
            throw new InvalidOperationException($"min_y + height cannot be higher than: {MaxAllowedY + 1}");
        if (settings.Height % 16 != 0)
            throw new InvalidOperationException("height has to be a multiple of 16");
        if (settings.MinY % 16 != 0)
            throw new InvalidOperationException("min_y has to be a multiple of 16");
    }

    //GetCellHeight cell 高度=NoiseSizeVertical*4 对应原版 getCellHeight
    public int GetCellHeight() => QuartPos.ToBlock(NoiseSizeVertical);

    //GetCellWidth cell 宽度=NoiseSizeHorizontal*4 对应原版 getCellWidth
    public int GetCellWidth() => QuartPos.ToBlock(NoiseSizeHorizontal);

    //ClampToHeightAccessor 按 LevelHeightAccessor 裁剪 y 范围对应原版 clampToHeightAccessor
    public NoiseSettings ClampToHeightAccessor(LevelHeightAccessor accessor)
    {
        var newMinY = Math.Max(MinY, accessor.MinBuildHeight);
        var newHeight = Math.Min(MinY + Height, accessor.MaxBuildHeight) - newMinY;
        return new NoiseSettings(newMinY, newHeight, NoiseSizeHorizontal, NoiseSizeVertical);
    }

    //5 内置常量对应原版 OVERWORLD/NETHER/END/CAVES/FLOATING_ISLANDS_NOISE_SETTINGS
    public static readonly NoiseSettings Overworld = Create(-64, 384, 1, 2);
    public static readonly NoiseSettings Nether = Create(0, 128, 1, 2);
    public static readonly NoiseSettings End = Create(0, 128, 2, 1);
    public static readonly NoiseSettings Caves = Create(-64, 192, 1, 2);
    public static readonly NoiseSettings FloatingIslands = Create(0, 256, 2, 1);
}
