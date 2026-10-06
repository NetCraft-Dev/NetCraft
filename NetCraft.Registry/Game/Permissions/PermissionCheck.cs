using NetCraft.Codec;

namespace NetCraft.Registry;

//PermissionCheck permission check, maps to vanilla net.minecraft.server.permissions.PermissionCheck
//Makes a yes/no decision on a PermissionSet; implementations are Require, which only passes on a permission hit
//AlwaysPass is always true and its singleton codec encodes/decodes an empty map
//The base is an abstract record so Require value semantics match vanilla records
public abstract record PermissionCheck
{
    //FullCodec dispatches through the PERMISSION_CHECK_TYPE registry by the type field
    public static readonly Codec<PermissionCheck> FullCodec = new PermissionCheckDispatchCodec();

    //Check makes the decision against a permission set
    public abstract bool Check(PermissionSet source);

    //GetCodec gets this instance's registry dispatch codec
    public abstract MapCodec<PermissionCheck> GetCodec();

    //Require requires holding a permission, maps to vanilla PermissionCheck.Require record
    public sealed record Require : PermissionCheck
    {
        //MapCodec single permission field using Permission.Codec, supporting a bare string id
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

    //AlwaysPass always-true check, maps to vanilla PermissionCheck.AlwaysPass
    //The constructor is private to keep it a singleton, matching vanilla INSTANCE
    public sealed record AlwaysPass : PermissionCheck
    {
        public static readonly AlwaysPass Instance = new();

        //MapCodec no-argument codec producing an empty map, maps to vanilla MapCodec.unit
        public static readonly MapCodec<PermissionCheck> MapCodec = new UnitMapCodec<PermissionCheck>(Instance);

        private AlwaysPass()
        {
        }

        public override bool Check(PermissionSet source) => true;

        public override MapCodec<PermissionCheck> GetCodec() => MapCodec;

        public override string ToString() => "AlwaysPass";
    }
}

//UnitMapCodec constant-value codec; decode always yields the same instance and encode produces only an empty map, maps to vanilla MapCodec.unit
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

//PermissionCheckDispatchCodec dispatches to the PERMISSION_CHECK_TYPE registry by the type field
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
