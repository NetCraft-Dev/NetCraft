using NetCraft.Codec;
using NetCraft.Registry.Codec;

namespace NetCraft.Registry;

//Permission 权限定义对应原版 net.minecraft.server.permissions.Permission
//两类实现: Atom 具名原子权限(字符串 id)与 HasCommandLevel 命令等级门槛
//FULL_CODEC 按 PERMISSION_TYPE 注册表分派 CODEC 额外接受裸字符串 id 退化为 Atom
//基类做成 abstract record 让 Atom/HasCommandLevel 的值语义与原版 record 一致
public abstract record Permission
{
    //FullCodec 按 type 字段查 PERMISSION_TYPE 注册表分派对应原版 byNameCodec().dispatch
    public static readonly Codec<Permission> FullCodec = new PermissionDispatchCodec();

    //Codec 先按完整分派解析 失败退回裸字符串 id 包成 Atom 对应原版 Codec.either(...).xmap
    public static readonly Codec<Permission> Codec = Codecs.Either(FullCodec, IdentifierCodec.Instance)
        .ComapFlatMap(
            (Alt<Permission, Identifier> alt) => alt.Map(
                (Permission permission) => DataResult<Permission>.Success(permission),
                (Identifier id) => DataResult<Permission>.Success(Atom.Create(id))),
            (Permission permission) => permission is Atom atom
                ? Alt<Permission, Identifier>.Right(atom.Id)
                : Alt<Permission, Identifier>.Left(permission));

    //GetCodec 取本实例的注册表分派 codec 编码时反查注册表键要用
    public abstract MapCodec<Permission> GetCodec();

    //Atom 具名原子权限对应原版 Permission.Atom record
    //相等性按 id 值比较 LevelBasedPermissionSet 里的引用常量靠它命中
    public sealed record Atom : Permission
    {
        //MapCodec 单字段 id 对应原版 RecordCodecBuilder.mapCodec
        public static readonly MapCodec<Permission> MapCodec =
            RecordCodecBuilder.Of1<Permission, Identifier>(
                IdentifierCodec.Instance.FieldOf("id").ForGetter<Permission, Identifier>(p => ((Atom)p).Id),
                id => new Atom(id));

        public Identifier Id { get; }

        public Atom(Identifier id) => Id = id;

        public override MapCodec<Permission> GetCodec() => MapCodec;

        //Create 按字符串建带默认命名空间的原子权限对应原版 Atom.create
        public static Atom Create(string name) => Create(Identifier.WithDefaultNamespace(name));

        public static Atom Create(Identifier id) => new(id);

        public override string ToString() => $"Atom[{Id}]";
    }

    //HasCommandLevel 命令等级门槛对应原版 Permission.HasCommandLevel record
    //持有等级而非具体命令 判定交给 PermissionSet 侧
    public sealed record HasCommandLevel : Permission
    {
        //MapCodec 单字段 level 按序列化名编解码对应原版 PermissionLevel.CODEC
        public static readonly MapCodec<Permission> MapCodec =
            RecordCodecBuilder.Of1<Permission, PermissionLevel>(
                PermissionLevels.Codec.FieldOf("level").ForGetter<Permission, PermissionLevel>(p => ((HasCommandLevel)p).Level),
                level => new HasCommandLevel(level));

        public PermissionLevel Level { get; }

        public HasCommandLevel(PermissionLevel level) => Level = level;

        public override MapCodec<Permission> GetCodec() => MapCodec;

        public override string ToString() => $"HasCommandLevel[{Level.SerializedName()}]";
    }
}

//PermissionDispatchCodec 按 type 字段分派到注册表里的 MapCodec 对应原版 dispatch
//type 与参数平铺在同一层 子 codec 忽略多余键 与 PlacementModifierCodec 同一套口径
internal sealed class PermissionDispatchCodec : ScalarCodec<Permission>
{
    public override DataResult<Permission> Parse<U>(DynamicOps<U> ops, U input)
        => ops.GetMap(input).FlatMap(map => DecodePermission(ops, map));

    private static DataResult<Permission> DecodePermission<U>(DynamicOps<U> ops, MapLike<U> input)
    {
        var typeTag = input.Get("type");
        if (!typeTag.IsPresent) return DataResult<Permission>.Error(() => "Permission is missing the type field");
        var typeText = ops.GetStringValue(typeTag.Get());
        if (!typeText.Result().IsPresent)
            return DataResult<Permission>.Error(() => "Permission type must be a string");
        var typeId = Identifier.TryParse(typeText.GetOrThrow());
        if (typeId is null)
            return DataResult<Permission>.Error(() => $"Invalid permission type: {typeText.GetOrThrow()}");
        if (BuiltInRegistries.PERMISSION_TYPE.GetValue(typeId.Value) is not { } codec)
            return DataResult<Permission>.Error(() => $"Unknown permission type: {typeId}");
        return codec.Decode(ops, input);
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, Permission value)
    {
        var id = BuiltInRegistries.PERMISSION_TYPE.GetKey(value.GetCodec());
        if (id is null)
            return DataResult<U>.Error(() => $"Permission codec is not registered: {value.GetCodec()}");
        var builder = ops.MapBuilder();
        builder.Add("type", ops.CreateString(id.Value.ToString()));
        return value.GetCodec().EncodeTo(ops, value, builder).Build(ops.Empty());
    }
}
