using NetCraft.Codec;

namespace NetCraft.Registry;

//PermissionCheck 权限判定对应原版 net.minecraft.server.permissions.PermissionCheck
//对 PermissionSet 做是/否判定 实现 Only 只在权限命中时放行 Require
//AlwaysPass 恒真 单例 codec 编解码为空表
//基类做成 abstract record 让 Require 的值语义与原版 record 一致
public abstract record PermissionCheck
{
    //FullCodec 按 type 字段查 PERMISSION_CHECK_TYPE 注册表分派
    public static readonly Codec<PermissionCheck> FullCodec = new PermissionCheckDispatchCodec();

    //Check 对权限集合做判定
    public abstract bool Check(PermissionSet source);

    //GetCodec 取本实例的注册表分派 codec
    public abstract MapCodec<PermissionCheck> GetCodec();

    //Require 要求持有一项权限对应原版 PermissionCheck.Require record
    public sealed record Require : PermissionCheck
    {
        //MapCodec 单字段 permission 走 Permission.Codec 支持裸字符串 id
        public static readonly MapCodec<PermissionCheck> MapCodec =
            RecordCodecBuilder.Of1<PermissionCheck, Permission>(
                Permission.Codec.FieldOf("permission").ForGetter<PermissionCheck, Permission>(p => ((Require)p).Permission),
                permission => new Require(permission));

        public Permission Permission { get; }

        public Require(Permission permission) => Permission = permission;

        public override bool Check(PermissionSet source) => source.HasPermission(Permission);

        public override MapCodec<PermissionCheck> GetCodec() => MapCodec;

        public override string ToString() => $"Require[{Permission}]";
    }

    //AlwaysPass 恒真判定对应原版 PermissionCheck.AlwaysPass
    //构造私有保持单例对应原版 INSTANCE
    public sealed record AlwaysPass : PermissionCheck
    {
        public static readonly AlwaysPass Instance = new();

        //MapCodec 无参编解码编出空表对应原版 MapCodec.unit
        public static readonly MapCodec<PermissionCheck> MapCodec = new UnitMapCodec<PermissionCheck>(Instance);

        private AlwaysPass()
        {
        }

        public override bool Check(PermissionSet source) => true;

        public override MapCodec<PermissionCheck> GetCodec() => MapCodec;

        public override string ToString() => "AlwaysPass";
    }
}

//UnitMapCodec 恒定值 codec 解码永远给同一实例 编码只产出空表对应原版 MapCodec.unit
internal sealed class UnitMapCodec<T> : MapCodec<T> where T : class
{
    private readonly T _instance;

    public UnitMapCodec(T instance) => _instance = instance;

    public DataResult<T> Decode<U>(DynamicOps<U> ops, MapLike<U> input)
        => DataResult<T>.Success(_instance);

    public DataResult<U> EncodeStart<U>(DynamicOps<U> ops, T value)
    {
        if (!ReferenceEquals(value, _instance))
            return DataResult<U>.Error(() => $"Expected the {typeof(T).Name} unit instance");
        return ops.MapBuilder().Build(ops.Empty());
    }

    public RecordBuilder<U> EncodeTo<U>(DynamicOps<U> ops, T value, RecordBuilder<U> builder) => builder;

    public RecordBuilder<U> Encoder<U>(DynamicOps<U> ops) => ops.MapBuilder();
}

//PermissionCheckDispatchCodec 按 type 字段分派到 PERMISSION_CHECK_TYPE 注册表
internal sealed class PermissionCheckDispatchCodec : ScalarCodec<PermissionCheck>
{
    public override DataResult<PermissionCheck> Parse<U>(DynamicOps<U> ops, U input)
        => ops.GetMap(input).FlatMap(map => DecodeCheck(ops, map));

    private static DataResult<PermissionCheck> DecodeCheck<U>(DynamicOps<U> ops, MapLike<U> input)
    {
        var typeTag = input.Get("type");
        if (!typeTag.IsPresent) return DataResult<PermissionCheck>.Error(() => "Permission check is missing the type field");
        var typeText = ops.GetStringValue(typeTag.Get());
        if (!typeText.Result().IsPresent)
            return DataResult<PermissionCheck>.Error(() => "Permission check type must be a string");
        var typeId = Identifier.TryParse(typeText.GetOrThrow());
        if (typeId is null)
            return DataResult<PermissionCheck>.Error(() => $"Invalid permission check type: {typeText.GetOrThrow()}");
        if (BuiltInRegistries.PERMISSION_CHECK_TYPE.GetValue(typeId.Value) is not { } codec)
            return DataResult<PermissionCheck>.Error(() => $"Unknown permission check type: {typeId}");
        return codec.Decode(ops, input);
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, PermissionCheck value)
    {
        var id = BuiltInRegistries.PERMISSION_CHECK_TYPE.GetKey(value.GetCodec());
        if (id is null)
            return DataResult<U>.Error(() => $"Permission check codec is not registered: {value.GetCodec()}");
        var builder = ops.MapBuilder();
        builder.Add("type", ops.CreateString(id.Value.ToString()));
        return value.GetCodec().EncodeTo(ops, value, builder).Build(ops.Empty());
    }
}
