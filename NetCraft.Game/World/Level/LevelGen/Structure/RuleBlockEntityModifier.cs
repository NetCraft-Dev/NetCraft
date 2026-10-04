using NetCraft.Codec;
using NetCraft.Nbt;
using NetCraft.Registry;
using NetCraft.Registry.Codec;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen.Structure;

//RuleBlockEntityModifier 处理器规则的方块实体数据修改器 对应原版 RuleBlockEntityModifier
//规则命中后决定这一格带什么方块实体数据 传 null 表示不带
public abstract class RuleBlockEntityModifier
{
    //Codec 多态入口 按 type 派发到 RULE_BLOCK_ENTITY_MODIFIER 注册表里的具体类型
    public static readonly Codec<RuleBlockEntityModifier> Codec = new RuleBlockEntityModifierDispatchCodec();

    //Apply 产出修改后的方块实体数据 对应原版 apply
    public abstract CompoundTag? Apply(RandomSource random, CompoundTag? existingTag);

    //Type 所属类型单例
    public abstract RuleBlockEntityModifierType Type { get; }
}

//RuleBlockEntityModifierType 修改器类型 对应原版 RuleBlockEntityModifierType
public abstract class RuleBlockEntityModifierType : NetCraft.Registry.RuleBlockEntityModifierType<object>
{
    public Identifier Id { get; }

    protected RuleBlockEntityModifierType(Identifier id) => Id = id;

    //DecodeModifier 从 map 解出一个修改器
    public abstract DataResult<RuleBlockEntityModifier> DecodeModifier<U>(DynamicOps<U> ops, MapLike<U> input);

    public override string ToString() => $"RuleBlockEntityModifierType[{Id}]";
}

//RuleBlockEntityModifierType<T> 强类型修改器类型
public sealed class RuleBlockEntityModifierType<T> : RuleBlockEntityModifierType where T : RuleBlockEntityModifier
{
    private readonly MapCodec<T> _codec;

    public RuleBlockEntityModifierType(Identifier id, MapCodec<T> codec) : base(id) => _codec = codec;

    public override DataResult<RuleBlockEntityModifier> DecodeModifier<U>(DynamicOps<U> ops, MapLike<U> input)
        => _codec.Decode(ops, input).Map(v => (RuleBlockEntityModifier)v);
}

//RuleBlockEntityModifierTypes 修改器类型登记 对应原版 RuleBlockEntityModifierType 的静态字段
public static class RuleBlockEntityModifierTypes
{
    public static readonly RuleBlockEntityModifierType<PassthroughModifier> Passthrough =
        Register("passthrough", PassthroughModifier.MapCodec);

    public static readonly RuleBlockEntityModifierType<ClearModifier> Clear =
        Register("clear", ClearModifier.MapCodec);

    public static readonly RuleBlockEntityModifierType<AppendStaticModifier> AppendStatic =
        Register("append_static", AppendStaticModifier.MapCodec);

    public static readonly RuleBlockEntityModifierType<AppendLootModifier> AppendLoot =
        Register("append_loot", AppendLootModifier.MapCodec);

    //Register 登记进 RULE_BLOCK_ENTITY_MODIFIER 并返回类型实例
    private static RuleBlockEntityModifierType<T> Register<T>(string path, MapCodec<T> codec)
        where T : RuleBlockEntityModifier
    {
        var type = new RuleBlockEntityModifierType<T>(Identifier.WithDefaultNamespace(path), codec);
        Registry<NetCraft.Registry.RuleBlockEntityModifierType<object>>.Register(
            BuiltInRegistries.RULE_BLOCK_ENTITY_MODIFIER, path, type);
        return type;
    }
}

//PassthroughModifier 原样透传 对应原版 Passthrough
public sealed class PassthroughModifier : RuleBlockEntityModifier
{
    public static readonly PassthroughModifier Instance = new();

    public static readonly MapCodec<PassthroughModifier> MapCodec =
        new StructureUnitMapCodec<PassthroughModifier>(() => Instance);

    private PassthroughModifier() { }

    public override CompoundTag? Apply(RandomSource random, CompoundTag? existingTag) => existingTag;

    public override RuleBlockEntityModifierType Type => RuleBlockEntityModifierTypes.Passthrough;
}

//ClearModifier 清空方块实体数据 对应原版 Clear
public sealed class ClearModifier : RuleBlockEntityModifier
{
    public static readonly ClearModifier Instance = new();

    public static readonly MapCodec<ClearModifier> MapCodec =
        new StructureUnitMapCodec<ClearModifier>(() => Instance);

    private ClearModifier() { }

    public override CompoundTag Apply(RandomSource random, CompoundTag? existingTag) => new();

    public override RuleBlockEntityModifierType Type => RuleBlockEntityModifierTypes.Clear;
}

//AppendStaticModifier 合并固定方块实体数据 对应原版 AppendStatic
public sealed class AppendStaticModifier : RuleBlockEntityModifier
{
    public static readonly MapCodec<AppendStaticModifier> MapCodec =
        new StructureSingleFieldMapCodec<AppendStaticModifier, CompoundTag>(
            StructureCompoundTagCodec.Instance.FieldOf("data"), tag => new AppendStaticModifier(tag), m => m.Data);

    public CompoundTag Data { get; }

    public AppendStaticModifier(CompoundTag data) => Data = data;

    public override CompoundTag Apply(RandomSource random, CompoundTag? existingTag)
    {
        if (existingTag is null) return (CompoundTag)Data.Copy();
        existingTag.Merge(Data);
        return existingTag;
    }

    public override RuleBlockEntityModifierType Type => RuleBlockEntityModifierTypes.AppendStatic;
}

//AppendLootModifier 写入战利品表引用与随机种子 对应原版 AppendLoot
//只写 LootTable 与 LootTableSeed 两个键 战利品内容由掉落系统另行处理
public sealed class AppendLootModifier : RuleBlockEntityModifier
{
    public static readonly MapCodec<AppendLootModifier> MapCodec =
        new StructureSingleFieldMapCodec<AppendLootModifier, Identifier>(
            IdentifierCodec.Instance.FieldOf("loot_table"), id => new AppendLootModifier(id), m => m.LootTable);

    public Identifier LootTable { get; }

    public AppendLootModifier(Identifier lootTable) => LootTable = lootTable;

    public override CompoundTag Apply(RandomSource random, CompoundTag? existingTag)
    {
        var result = existingTag is null ? new CompoundTag() : (CompoundTag)existingTag.Copy();
        result.PutString("LootTable", LootTable.ToString());
        result.PutLong("LootTableSeed", random.NextLong());
        return result;
    }

    public override RuleBlockEntityModifierType Type => RuleBlockEntityModifierTypes.AppendLoot;
}

//RuleBlockEntityModifierDispatchCodec 修改器多态 codec 对应原版 RuleBlockEntityModifier.CODEC 的 dispatch
internal sealed class RuleBlockEntityModifierDispatchCodec : ScalarCodec<RuleBlockEntityModifier>
{
    public override DataResult<RuleBlockEntityModifier> Parse<U>(DynamicOps<U> ops, U input)
        => ops.GetMap(input).FlatMap(map => DecodeModifier(ops, map));

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, RuleBlockEntityModifier value)
        => DataResult<U>.Error(() => "方块实体修改器编码暂未实现");

    //DecodeModifier 读 type 字段查表再交给该类型的 codec
    internal static DataResult<RuleBlockEntityModifier> DecodeModifier<U>(DynamicOps<U> ops, MapLike<U> input)
    {
        var typeTag = input.Get("type");
        if (!typeTag.IsPresent) return DataResult<RuleBlockEntityModifier>.Error(() => "方块实体修改器缺少 type");
        var text = ops.GetStringValue(typeTag.Get());
        if (!text.Result().IsPresent) return DataResult<RuleBlockEntityModifier>.Error(() => "type 必须是字符串");
        var id = Identifier.TryParse(text.GetOrThrow());
        if (id is null) return DataResult<RuleBlockEntityModifier>.Error(() => $"非法的修改器类型: {text.GetOrThrow()}");
        if (!BuiltInRegistries.RULE_BLOCK_ENTITY_MODIFIER.ContainsKey(id.Value))
            return DataResult<RuleBlockEntityModifier>.Error(() => $"未注册的修改器类型: {id}");
        var type = BuiltInRegistries.RULE_BLOCK_ENTITY_MODIFIER.GetValue(id.Value) as RuleBlockEntityModifierType;
        return type is null
            ? DataResult<RuleBlockEntityModifier>.Error(() => $"修改器类型 {id} 无法解析")
            : type.DecodeModifier(ops, input);
    }
}
