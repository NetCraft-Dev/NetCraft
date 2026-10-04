using NetCraft.Codec;
using NetCraft.Nbt;

namespace NetCraft.Game.World.Level.LevelGen;

//NoiseRouterCodec 15 字段 NoiseRouter 编解码对应原版 NoiseRouter.CODEC
//手写 decode 逐字段读取 encode 逐字段写入不依赖 RecordCodecBuilder 扩展
//所有字段通过 DensityFunctionCodec dispatch codec 编解码
public sealed class NoiseRouterCodec : AbstractMapCodec<NoiseRouter>
{
    public static readonly NoiseRouterCodec Instance = new();

    //FieldNames 15 字段名对应原版 NoiseRouter 字段命名
    //preliminary_surface_level 对应类内 PreliminarySurfaceLevel 原版 26.2 起的字段名
    private static readonly string[] FieldNames =
    {
        "final_density",
        "barrier",
        "fluid_level_floodedness",
        "fluid_level_spread",
        "lava",
        "temperature",
        "vegetation",
        "continents",
        "erosion",
        "depth",
        "ridges",
        "preliminary_surface_level",
        "vein_toggle",
        "vein_ridged",
        "vein_gap"
    };

    public override DataResult<NoiseRouter> Decode<U>(DynamicOps<U> ops, MapLike<U> input)
    {
        var finalDensity = DecodeDensity(ops, input, FieldNames[0]);
        if (!finalDensity.IsPresent) return DataResult<NoiseRouter>.Error(() => $"missing {FieldNames[0]}");
        var barrier = DecodeDensity(ops, input, FieldNames[1]);
        if (!barrier.IsPresent) return DataResult<NoiseRouter>.Error(() => $"missing {FieldNames[1]}");
        var floodedness = DecodeDensity(ops, input, FieldNames[2]);
        if (!floodedness.IsPresent) return DataResult<NoiseRouter>.Error(() => $"missing {FieldNames[2]}");
        var spread = DecodeDensity(ops, input, FieldNames[3]);
        if (!spread.IsPresent) return DataResult<NoiseRouter>.Error(() => $"missing {FieldNames[3]}");
        var lava = DecodeDensity(ops, input, FieldNames[4]);
        if (!lava.IsPresent) return DataResult<NoiseRouter>.Error(() => $"missing {FieldNames[4]}");
        var temperature = DecodeDensity(ops, input, FieldNames[5]);
        if (!temperature.IsPresent) return DataResult<NoiseRouter>.Error(() => $"missing {FieldNames[5]}");
        var vegetation = DecodeDensity(ops, input, FieldNames[6]);
        if (!vegetation.IsPresent) return DataResult<NoiseRouter>.Error(() => $"missing {FieldNames[6]}");
        var continents = DecodeDensity(ops, input, FieldNames[7]);
        if (!continents.IsPresent) return DataResult<NoiseRouter>.Error(() => $"missing {FieldNames[7]}");
        var erosion = DecodeDensity(ops, input, FieldNames[8]);
        if (!erosion.IsPresent) return DataResult<NoiseRouter>.Error(() => $"missing {FieldNames[8]}");
        var depth = DecodeDensity(ops, input, FieldNames[9]);
        if (!depth.IsPresent) return DataResult<NoiseRouter>.Error(() => $"missing {FieldNames[9]}");
        var ridges = DecodeDensity(ops, input, FieldNames[10]);
        if (!ridges.IsPresent) return DataResult<NoiseRouter>.Error(() => $"missing {FieldNames[10]}");
        var preliminary = DecodeDensity(ops, input, FieldNames[11]);
        if (!preliminary.IsPresent) return DataResult<NoiseRouter>.Error(() => $"missing {FieldNames[11]}");
        var veinToggle = DecodeDensity(ops, input, FieldNames[12]);
        if (!veinToggle.IsPresent) return DataResult<NoiseRouter>.Error(() => $"missing {FieldNames[12]}");
        var veinRidged = DecodeDensity(ops, input, FieldNames[13]);
        if (!veinRidged.IsPresent) return DataResult<NoiseRouter>.Error(() => $"missing {FieldNames[13]}");
        var veinGap = DecodeDensity(ops, input, FieldNames[14]);
        if (!veinGap.IsPresent) return DataResult<NoiseRouter>.Error(() => $"missing {FieldNames[14]}");

        return DataResult<NoiseRouter>.Success(new NoiseRouter(
            barrier.Get(), floodedness.Get(), spread.Get(), lava.Get(),
            temperature.Get(), vegetation.Get(), continents.Get(), erosion.Get(),
            depth.Get(), ridges.Get(), preliminary.Get(), finalDensity.Get(),
            veinToggle.Get(), veinRidged.Get(), veinGap.Get()));
    }

    public override RecordBuilder<U> EncodeTo<U>(DynamicOps<U> ops, NoiseRouter value, RecordBuilder<U> builder)
    {
        EncodeDensity(ops, builder, FieldNames[0], value.FinalDensity);
        EncodeDensity(ops, builder, FieldNames[1], value.Barrier);
        EncodeDensity(ops, builder, FieldNames[2], value.FluidLevelFloodedness);
        EncodeDensity(ops, builder, FieldNames[3], value.FluidLevelSpread);
        EncodeDensity(ops, builder, FieldNames[4], value.Lava);
        EncodeDensity(ops, builder, FieldNames[5], value.Temperature);
        EncodeDensity(ops, builder, FieldNames[6], value.Vegetation);
        EncodeDensity(ops, builder, FieldNames[7], value.Continents);
        EncodeDensity(ops, builder, FieldNames[8], value.Erosion);
        EncodeDensity(ops, builder, FieldNames[9], value.Depth);
        EncodeDensity(ops, builder, FieldNames[10], value.Ridges);
        EncodeDensity(ops, builder, FieldNames[11], value.PreliminarySurfaceLevel);
        EncodeDensity(ops, builder, FieldNames[12], value.VeinToggle);
        EncodeDensity(ops, builder, FieldNames[13], value.VeinRidged);
        EncodeDensity(ops, builder, FieldNames[14], value.VeinGap);
        return builder;
    }

    //DecodeDensity 读取单个 DensityFunction 字段
    private static Optional<DensityFunction> DecodeDensity<U>(DynamicOps<U> ops, MapLike<U> input, string name)
    {
        var value = input.Get(name);
        if (!value.IsPresent) return Optional<DensityFunction>.Empty();
        return DensityFunctionCodec.Instance.Parse(ops, value.Get()).Result();
    }

    //EncodeDensity 写入单个 DensityFunction 字段
    private static void EncodeDensity<U>(DynamicOps<U> ops, RecordBuilder<U> builder, string name, DensityFunction value)
        => builder.Add(name, DensityFunctionCodec.Instance.EncodeStart(ops, value).GetOrThrow());
}
