namespace NetCraft.DataFixer.Fixes;

using System;
using NetCraft.Codec;
using NetCraft.DataFixer.Schemas;
using T = NetCraft.DataFixer.Types;
using NetCraft.DataFixer.Types.Templates;

//ExampleCounterIncrementFix end-to-end example fixer
//increments example_counter's int value by 1, maps to the vanilla simple DataFix structure
public class ExampleCounterIncrementFix : DataFix
{
    public ExampleCounterIncrementFix(Schema outputSchema) : base(outputSchema, changesType: false) { }

    protected override TypeRewriteRule MakeRule()
    {
        //uses ExampleType as the IfSame target because ExampleSchema registers example_counter as ConstType(ExampleType)
        //sourceType chain is CheckType->NamedType->ExampleType, and the If match on ExampleType succeeds
        var type = ExampleSchema.ExampleType;
        //FixTypeEverywhere<object> maps to the function type with A=object
        //input is object but actually a boxed int; add 1 then re-box
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
