using NetCraft.Codec;
using NetCraft.Primitives;
using NetCraft.Storage.Chunk;

namespace NetCraft.Game.World.Level.LevelGen;

//NoiseSettings noise settings, maps to vanilla net.minecraft.world.level.levelgen.NoiseSettings
//Describes the dimension noise sampling grid; minY/height fix the y range and noiseSize decides the cell width and height
//Overworld create(-64,384,1,2) → cellWidth=4 cellHeight=8
public sealed class NoiseSettings
{
    //Matches vanilla DimensionType.MIN_Y/MAX_Y validation bounds
    private const int MinAllowedY = -2032;
    private const int MaxAllowedY = 2032;

    //Codec noise settings JSON codec, maps to vanilla NoiseSettings.CODEC
    //Fields min_y/height/size_horizontal/size_vertical; a failed guardY returns Error
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

    //BuildCodec builds from the four fields then wraps a guardY check
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

    //GuardYResult returns Error on a failed check, the DataResult form of vanilla guardY
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

    //Create checked factory, maps to vanilla create
    public static NoiseSettings Create(int minY, int height, int noiseSizeHorizontal, int noiseSizeVertical)
    {
        var settings = new NoiseSettings(minY, height, noiseSizeHorizontal, noiseSizeVertical);
        GuardY(settings);
        return settings;
    }

    //GuardY validates the y range and the multiple-of-16 rule, maps to vanilla guardY
    private static void GuardY(NoiseSettings settings)
    {
        if (settings.MinY + settings.Height > MaxAllowedY + 1)
            throw new InvalidOperationException($"min_y + height cannot be higher than: {MaxAllowedY + 1}");
        if (settings.Height % 16 != 0)
            throw new InvalidOperationException("height has to be a multiple of 16");
        if (settings.MinY % 16 != 0)
            throw new InvalidOperationException("min_y has to be a multiple of 16");
    }

    //GetCellHeight cell height = NoiseSizeVertical*4, maps to vanilla getCellHeight
    public int GetCellHeight() => QuartPos.ToBlock(NoiseSizeVertical);

    //GetCellWidth cell width = NoiseSizeHorizontal*4, maps to vanilla getCellWidth
    public int GetCellWidth() => QuartPos.ToBlock(NoiseSizeHorizontal);

    //ClampToHeightAccessor clamps the y range to a LevelHeightAccessor, maps to vanilla clampToHeightAccessor
    public NoiseSettings ClampToHeightAccessor(LevelHeightAccessor accessor)
    {
        var newMinY = Math.Max(MinY, accessor.MinBuildHeight);
        var newHeight = Math.Min(MinY + Height, accessor.MaxBuildHeight) - newMinY;
        return new NoiseSettings(newMinY, newHeight, NoiseSizeHorizontal, NoiseSizeVertical);
    }

    //The five built-in constants, maps to vanilla OVERWORLD/NETHER/END/CAVES/FLOATING_ISLANDS_NOISE_SETTINGS
    public static readonly NoiseSettings Overworld = Create(-64, 384, 1, 2);
    public static readonly NoiseSettings Nether = Create(0, 128, 1, 2);
    public static readonly NoiseSettings End = Create(0, 128, 2, 1);
    public static readonly NoiseSettings Caves = Create(-64, 192, 1, 2);
    public static readonly NoiseSettings FloatingIslands = Create(0, 256, 2, 1);
}
