using NetCraft.Codec;
using NetCraft.Registry.Codec;

namespace NetCraft.Registry.State;

//BlockStateCodec 方块状态编解码对应原版 BlockState.CODEC
//JSON 形式 {"Name":"minecraft:stone","Properties":{"snowy":"false"}} 属性段可缺省
//Name 走 BLOCK 注册表查方块 Properties 逐项按属性名与取值名套用
//放 Registry 层是因为区块存档的 Palette 也要用它: 存档必须连同属性一起落盘
//否则带朝向的方块(楼梯/门/半砖)卸载再加载会被还原成默认状态
public sealed class BlockStateCodec : ScalarCodec<BlockState>
{
    //Instance 默认实例 按内置 BLOCK 注册表查方块
    public static readonly BlockStateCodec Instance = new(null);

    //_lookup 自定义方块查找 为空时回落内置注册表
    //区块存档的 Palette 用容器工厂自己登记过的那张表 测试里的 MockBlock 只在那里
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

    //LookupBlock 先问 RegistryOps 携带的 BLOCK 注册表 再看自定义表 最后回落内置注册表
    //默认注册表未知 id 会回落 air 必须先用 ContainsKey 拦住
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

    //FindProperty 按属性名在状态属性表里查找
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
