using NetCraft.Codec;
using NetCraft.Registry.Codec;

namespace NetCraft.Registry.State;

//BlockStateCodec block state encoding/decoding, maps to vanilla BlockState.CODEC
//JSON form {"Name":"minecraft:stone","Properties":{"snowy":"false"}} with the properties section optional
//Name looks up the block in the BLOCK registry and Properties applies each entry by property name and value name
//Placed in the Registry layer because the chunk save Palette also uses it: saves must persist properties too
//Otherwise directional blocks (stairs/doors/slabs) would be restored to their default state after unloading and reloading
public sealed class BlockStateCodec : ScalarCodec<BlockState>
{
    //Instance default instance, looking up blocks in the built-in BLOCK registry
    public static readonly BlockStateCodec Instance = new(null);

    //_lookup custom block lookup, falling back to the built-in registry when null
    //The chunk save Palette uses the table registered by the container factory; MockBlock in tests only exists there
    private readonly Func<Identifier, Block?>? _lookup;

    public BlockStateCodec(Func<Identifier, Block?>? lookup) => _lookup = lookup;

    public override DataResult<BlockState> Parse<U>(DynamicOps<U> ops, U input)
        => ops.GetMap(input).FlatMap(map => DecodeState(ops, map));

    private DataResult<BlockState> DecodeState<U>(DynamicOps<U> ops, MapLike<U> input)
    {
        var nameTag = input.Get("Name");
        if (!nameTag.IsPresent)
            return DataResult<BlockState>.Error(() => "Missing key Name");
        var idResult = IdentifierCodec.Instance.Parse(ops, nameTag.Get());
        if (!idResult.Result().IsPresent)
            return DataResult<BlockState>.Error(() => "BlockState Name is not a valid identifier");
        var id = idResult.GetOrThrow();
        var block = LookupBlock(ops, id);
        if (block is null)
            return DataResult<BlockState>.Error(() => $"Unknown block: {id}");
        var state = block.DefaultBlockState;

        var propertiesTag = input.Get("Properties");
        if (!propertiesTag.IsPresent) return DataResult<BlockState>.Success(state);
        var propertiesResult = ops.GetMap(propertiesTag.Get());
        if (!propertiesResult.Result().IsPresent)
            return DataResult<BlockState>.Error(() => "BlockState Properties must be a map");
        foreach (var (keyTag, valueTag) in propertiesResult.GetOrThrow().Entries())
        {
            var keyResult = ops.GetStringValue(keyTag);
            var valueResult = ops.GetStringValue(valueTag);
            if (!keyResult.Result().IsPresent || !valueResult.Result().IsPresent)
                return DataResult<BlockState>.Error(() => "BlockState Properties entries must be strings");
            var name = keyResult.GetOrThrow();
            var property = FindProperty(state, name);
            if (property is null)
                return DataResult<BlockState>.Error(() => $"Unknown property {name} for block {id}");
            var parsed = property.GetValueForName(valueResult.GetOrThrow());
            if (parsed is null)
                return DataResult<BlockState>.Error(() => $"Unknown value {valueResult.GetOrThrow()} for property {name}");
            state = state.SetValue(property, parsed);
        }
        return DataResult<BlockState>.Success(state);
    }

    //LookupBlock first consults the BLOCK registry carried by RegistryOps, then the custom table, and finally the built-in registry
    //The default registry falls back to air for unknown ids, so ContainsKey must gate it first
    private Block? LookupBlock<U>(DynamicOps<U> ops, Identifier id)
    {
        if (ops is RegistryOps<U> registryOps)
        {
            var registry = registryOps.GetRegistry(Registries.BLOCK);
            if (registry is not null)
                return registry.ContainsKey(id) ? registry.GetValue(id) : null;
        }
        if (_lookup?.Invoke(id) is { } custom) return custom;
        return BuiltInRegistries.BLOCK.ContainsKey(id) ? BuiltInRegistries.BLOCK.GetValue(id) : null;
    }

    //FindProperty looks up a property by name in the state's property set
    private static PropertyBase? FindProperty(BlockState state, string name)
    {
        foreach (var property in state.GetProperties())
            if (property.Name == name) return property;
        return null;
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, BlockState value)
    {
        var builder = ops.MapBuilder();
        builder.Add("Name", ops.CreateString(value.Owner.Id.ToString()));
        var properties = value.GetValues().ToList();
        if (properties.Count > 0)
        {
            var propertyBuilder = ops.MapBuilder();
            foreach (var property in properties)
                propertyBuilder.Add(property.Property.Name, ops.CreateString(property.ValueName));
            var built = propertyBuilder.Build(ops.Empty());
            if (!built.Result().IsPresent)
                return DataResult<U>.Error(() => "Failed to encode BlockState properties");
            builder.Add("Properties", built.GetOrThrow());
        }
        return builder.Build(ops.Empty());
    }
}
