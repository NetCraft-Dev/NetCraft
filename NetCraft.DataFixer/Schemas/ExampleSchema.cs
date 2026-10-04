namespace NetCraft.DataFixer.Schemas;

using System.Collections.Generic;
using NetCraft.Codec;
using NetCraft.DataFixer.Fixes;
using NetCraft.DataFixer.Types.Templates;
using T = NetCraft.DataFixer.Types;

//ExampleSchema端到端示例Schema
//注册minecraft:example_counter类型用PrimitiveType<object>包装ObjectIntCodec
//避开值类型A=int的泛型不变量问题用object作为载体
public class ExampleSchema : Schema
{
    //ObjectIntCodec把int装箱为object编解码到NbtOps的IntTag
    public static readonly Codec<object> ObjectIntCodec = new ObjectIntCodecImpl();

    //ExampleType注册表用PrimitiveType<object>避免Type<int>强转Type<object>失败
    public static readonly T.Type<object> ExampleType = new Const.PrimitiveType<object>(ObjectIntCodec);

    public ExampleSchema(int versionKey, Schema? parent) : base(versionKey, parent) { }

    public override void RegisterTypes(Schema schema, Dictionary<string, Func<TypeTemplate>> entityTypes, Dictionary<string, Func<TypeTemplate>> blockEntityTypes)
    {
        base.RegisterTypes(schema, entityTypes, blockEntityTypes);
        //递归注册example_counter让Schema.BuildTypes有templates能构造RecursiveTypeFamily
        schema.RegisterType(true, References.ExampleCounter, () => DSL.ConstType(ExampleType));
    }

    //ObjectIntCodecImpl内部ScalarCodec实现
    //Parse读IntTag返回object装箱int
    //EncodeStart把object强转int写IntTag
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
