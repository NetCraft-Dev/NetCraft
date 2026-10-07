namespace NetCraft.DataFixer.Schemas;

using System.Collections.Generic;
using NetCraft.Codec;
using NetCraft.DataFixer.Fixes;
using NetCraft.DataFixer.Types.Templates;
using T = NetCraft.DataFixer.Types;

//ExampleSchema end-to-end example Schema
//registers the minecraft:example_counter type, wrapping ObjectIntCodec with PrimitiveType<object>
//avoids the value-type A=int generic invariance issue by using object as the carrier
public class ExampleSchema : Schema
{
    //ObjectIntCodec boxes int as object and encodes/decodes to NbtOps's IntTag
    public static readonly Codec<object> ObjectIntCodec = new ObjectIntCodecImpl();

    //the ExampleType registry uses PrimitiveType<object> to avoid the failed Type<int> to Type<object> cast
    public static readonly T.Type<object> ExampleType = new Const.PrimitiveType<object>(ObjectIntCodec);

    public ExampleSchema(int versionKey, Schema? parent) : base(versionKey, parent) { }

    public override void RegisterTypes(Schema schema, Dictionary<string, Func<TypeTemplate>> entityTypes, Dictionary<string, Func<TypeTemplate>> blockEntityTypes)
    {
        base.RegisterTypes(schema, entityTypes, blockEntityTypes);
        //registers example_counter recursively so Schema.BuildTypes has templates to construct the RecursiveTypeFamily
        schema.RegisterType(true, References.ExampleCounter, () => DSL.ConstType(ExampleType));
    }

    //ObjectIntCodecImpl internal ScalarCodec implementation
    //Parse reads IntTag and returns an object-boxed int
    //EncodeStart casts object to int and writes IntTag
    private sealed class ObjectIntCodecImpl : ScalarCodec<object>
    {
        public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, object value)
        {
            if (value is int i)
            {
                return DataResult<U>.Success(ops.CreateInt(i));
            }
            return DataResult<U>.Error(() => $"Expected int, got {value?.GetType().Name ?? "null"}");
        }

        public override DataResult<object> Parse<U>(DynamicOps<U> ops, U input)
        {
            return ops.GetNumberValue(input).Map(n => (object)(int)n);
        }

        public override string ToString() => "ObjectInt";
    }
}
