namespace NetCraft.DataFixer.Schemas;

using System;
using NetCraft.Codec;
using NetCraft.DataFixer.Types.Templates;
using T = NetCraft.DataFixer.Types;

//Namespaced Schema maps to vanilla net.minecraft.util.datafix.schemas.NamespacedSchema
//inherits Schema and overrides GetChoiceType, wrapping choiceName with EnsureNamespaced to normalize the namespace
//vanilla EnsureNamespaced normalizes with Identifier.tryParse; here it is a placeholder returning input, to be completed when the MC integration layer is fully ported
public class NamespacedSchema : Schema
{
    //NAMESPACED_STRING_CODEC namespaced string codec; calls EnsureNamespaced to normalize on read
    public static readonly Codec<string> NamespacedStringCodec = new NamespacedStringCodecImpl();

    private static readonly T.Type<string> NamespacedStringType = new Const.PrimitiveType<string>(NamespacedStringCodec);

    public NamespacedSchema(int versionKey, Schema? parent) : base(versionKey, parent) { }

    //ensureNamespaced normalizes a namespaced string; a placeholder returning input, to be completed with tryParse logic once Identifier is ready
    public static string EnsureNamespaced(string input) => input;

    //namespacedString takes the namespaced string Type
    public static T.Type<string> NamespacedString() => NamespacedStringType;

    //getChoiceType override wraps choiceName with EnsureNamespaced, aligning with vanilla
    public override T.Type<object> GetChoiceType(DSL.ITypeReference type, string choiceName)
        => base.GetChoiceType(type, EnsureNamespaced(choiceName));

    //NamespacedStringCodecImpl internal ScalarCodec; Parse calls GetStringValue then Maps EnsureNamespaced
    private sealed class NamespacedStringCodecImpl : ScalarCodec<string>
    {
        public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, string value)
            => DataResult<U>.Success(ops.CreateString(value));

        public override DataResult<string> Parse<U>(DynamicOps<U> ops, U input)
            => ops.GetStringValue(input).Map(EnsureNamespaced);

        public override string ToString() => "NamespacedString";
    }
}
