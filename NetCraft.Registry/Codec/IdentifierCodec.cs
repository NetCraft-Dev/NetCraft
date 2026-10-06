using NetCraft.Codec;

namespace NetCraft.Registry.Codec;

//Identifier codec, maps to vanilla Identifier.CODEC
//Serialized as a StringTag in namespace:path format
public sealed class IdentifierCodec : ScalarCodec<Identifier>
{
    public static readonly IdentifierCodec Instance = new();

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, Identifier value)
        => DataResult<U>.Success(ops.CreateString(value.ToString()));

    public override DataResult<Identifier> Parse<U>(DynamicOps<U> ops, U input)
    {
        var strResult = ops.GetStringValue(input);
        if (!strResult.Result().IsPresent)
            return DataResult<Identifier>.Error(() => $"Not a string: {input}");
        var str = strResult.GetOrThrow();
        var id = Identifier.TryParse(str);
        return id is null
            ? DataResult<Identifier>.Error(() => $"Invalid identifier: {str}")
            : DataResult<Identifier>.Success(id.Value);
    }
}
