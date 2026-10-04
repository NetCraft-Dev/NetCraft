namespace NetCraft.DataFixer.Schemas;

using System;
using NetCraft.Codec;
using NetCraft.DataFixer.Types.Templates;
using T = NetCraft.DataFixer.Types;

//命名空间Schema对应原版net.minecraft.util.datafix.schemas.NamespacedSchema
//继承Schema重写GetChoiceType用EnsureNamespaced包装choiceName规范化命名空间
//EnsureNamespaced原版用Identifier.tryParse规范化此处占位直接返回input待MC集成层完整移植时补
public class NamespacedSchema : Schema
{
    //NAMESPACED_STRING_CODEC命名空间字符串codec读时调EnsureNamespaced规范化
    public static readonly Codec<string> NamespacedStringCodec = new NamespacedStringCodecImpl();

    private static readonly T.Type<string> NamespacedStringType = new Const.PrimitiveType<string>(NamespacedStringCodec);

    public NamespacedSchema(int versionKey, Schema? parent) : base(versionKey, parent) { }

    //ensureNamespaced规范化命名空间字符串占位直接返回input待Identifier就绪后补tryParse逻辑
    public static string EnsureNamespaced(string input) => input;

    //namespacedString取得命名空间字符串Type
    public static T.Type<string> NamespacedString() => NamespacedStringType;

    //getChoiceType重写用EnsureNamespaced包装choiceName对齐原版
    public override T.Type<object> GetChoiceType(DSL.ITypeReference type, string choiceName)
        => base.GetChoiceType(type, EnsureNamespaced(choiceName));

    //NamespacedStringCodecImpl内部ScalarCodec实现Parse调GetStringValue后Map EnsureNamespaced
    private sealed class NamespacedStringCodecImpl : ScalarCodec<string>
    {
        public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, string value)
            => DataResult<U>.Success(ops.CreateString(value));

        public override DataResult<string> Parse<U>(DynamicOps<U> ops, U input)
            => ops.GetStringValue(input).Map(EnsureNamespaced);

        public override string ToString() => "NamespacedString";
    }
}
