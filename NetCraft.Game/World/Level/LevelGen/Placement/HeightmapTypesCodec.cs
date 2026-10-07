using NetCraft.Codec;
using NetCraft.Registry;

namespace NetCraft.Game.World.Level.LevelGen.Placement;

//HeightmapTypesCodec heightmap type codec, maps to vanilla Heightmap.Types.CODEC
//JSON form is an uppercase underscore key such as WORLD_SURFACE_WG
internal sealed class HeightmapTypesCodec : ScalarCodec<Heightmap.Types>
{
    public static readonly HeightmapTypesCodec Instance = new();

    public override DataResult<Heightmap.Types> Parse<U>(DynamicOps<U> ops, U input)
    {
        var text = ops.GetStringValue(input);
        if (!text.Result().IsPresent)
            return DataResult<Heightmap.Types>.Error(() => "heightmap type must be a string");
        var type = Heightmap.FromSerializationKey(text.GetOrThrow());
        return type is null
            ? DataResult<Heightmap.Types>.Error(() => $"unknown heightmap type: {text.GetOrThrow()}")
            : DataResult<Heightmap.Types>.Success(type.Value);
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, Heightmap.Types value)
        => DataResult<U>.Success(ops.CreateString(value.GetSerializationKey()));
}
