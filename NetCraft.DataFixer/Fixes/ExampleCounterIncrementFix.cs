namespace NetCraft.DataFixer.Fixes;

using System;
using NetCraft.Codec;
using NetCraft.DataFixer.Schemas;
using T = NetCraft.DataFixer.Types;
using NetCraft.DataFixer.Types.Templates;

//ExampleCounterIncrementFix端到端示例修复器
//把example_counter的int值+1对应原版简单DataFix结构
public class ExampleCounterIncrementFix : DataFix
{
    public ExampleCounterIncrementFix(Schema outputSchema) : base(outputSchema, changesType: false) { }

    protected override TypeRewriteRule MakeRule()
    {
        //用ExampleType作为IfSame目标因ExampleSchema注册example_counter为ConstType(ExampleType)
        //sourceType链为CheckType->NamedType->ExampleType If匹配ExampleType成功
        var type = ExampleSchema.ExampleType;
        //FixTypeEverywhere<object>对应A=object的函数类型
        //input是object实际是int装箱+1后重新装箱
        return FixTypeEverywhere("example_counter_increment", type, ops => input =>
        {
            if (input is int i)
            {
                return (object)(i + 1);
            }
            return input;
        });
    }
}
