using NetCraft.Codec;
using NetCraft.Registry.Codec;

namespace NetCraft.Registry;

//Permission permission definition, maps to vanilla net.minecraft.server.permissions.Permission
//Two implementations: Atom for named atomic permissions (string id) and HasCommandLevel for command level thresholds
//FULL_CODEC dispatches through the PERMISSION_TYPE registry while CODEC additionally accepts a bare string id degraded to Atom
//The base is an abstract record so Atom/HasCommandLevel value semantics match vanilla records
public abstract record Permission
{
    //FullCodec dispatches through the PERMISSION_TYPE registry by the type field, maps to vanilla byNameCodec().dispatch
    public static readonly Codec<Permission> FullCodec = new PermissionDispatchCodec();

    //Codec parses with the full dispatch first and falls back to a bare string id wrapped as Atom, maps to vanilla Codec.either(...).xmap
    public static readonly Codec<Permission> Codec = Codecs.Either(FullCodec, IdentifierCodec.Instance)
        .ComapFlatMap(
            (Alt<Permission, Identifier> alt) => alt.Map(
                (Permission permission) => DataResult<Permission>.Success(permission),
                (Identifier id) => DataResult<Permission>.Success(Atom.Create(id))),
            (Permission permission) => permission is Atom atom
                ? Alt<Permission, Identifier>.Right(atom.Id)
                : Alt<Permission, Identifier>.Left(permission));

    //GetCodec gets this instance's registry dispatch codec, needed to reverse-look up the registry key when encoding
    public abstract MapCodec<Permission> GetCodec();

    //Atom named atomic permission, maps to vanilla Permission.Atom record
    //Equality compares the id value, which is how reference constants in LevelBasedPermissionSet match
    public sealed record Atom : Permission
    {
        //MapCodec single id field, maps to vanilla RecordCodecBuilder.mapCodec
        public static readonly MapCodec<Permission> MapCodec =
            RecordCodecBuilder.Of1<Permission, Identifier>(
                IdentifierCodec.Instance.FieldOf("id").ForGetter<Permission, Identifier>(p => ((Atom)p).Id),
                id => new Atom(id));

        public Identifier Id { get; }

        public Atom(Identifier id) => Id = id;

        public override MapCodec<Permission> GetCodec() => MapCodec;

        //Create builds an atomic permission under the default namespace from a string, maps to vanilla Atom.create
        public static Atom Create(string name) => Create(Identifier.WithDefaultNamespace(name));

        public static Atom Create(Identifier id) => new(id);

        public override string ToString() => $"Atom[{Id}]";
    }

    //HasCommandLevel command level threshold, maps to vanilla Permission.HasCommandLevel record
    //Holds a level rather than a concrete command; the PermissionSet side performs the check
    public sealed record HasCommandLevel : Permission
    {
        //MapCodec single level field encoded/decoded by serialized name, maps to vanilla PermissionLevel.CODEC
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

//PermissionDispatchCodec dispatches to the MapCodec in the registry by the type field, maps to vanilla dispatch
//type and arguments are flattened on the same level and the child codec ignores extra keys, the same convention as PlacementModifierCodec
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
