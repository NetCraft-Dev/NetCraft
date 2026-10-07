namespace NetCraft.DataFixer.Fixes;

using System;
using NetCraft.Codec;
using NetCraft.DataFixer.Schemas;
using NetCraft.DataFixer.Types.Templates;

//add new choices fix, maps to vanilla net.minecraft.util.datafix.fixes.AddNewChoices
//injects new choices into a TaggedChoice type; vanilla uses this when a version bump adds entities/blocks
public class AddNewChoices : DataFix
{
    private readonly string _name;
    private readonly DSL.ITypeReference _type;

    public AddNewChoices(Schema outputSchema, string name, DSL.ITypeReference type) : base(outputSchema, changesType: true)
    {
        _name = name;
        _type = type;
    }

    //makeRule builds cap from the input/output Schema's TaggedChoice types
    protected override TypeRewriteRule MakeRule()
    {
        var inputType = GetInputSchema().FindChoiceType(_type);
        var outputType = GetOutputSchema().FindChoiceType(_type);
        return Cap(inputType, outputType);
    }

    //cap verifies keyType equality, then uses fixTypeEverywhere to build a passthrough rule by name
    //vanilla's generic method has K as the key type; C# uses object to align with type erasure
    private TypeRewriteRule Cap(TaggedChoice<object>.TaggedChoiceType<object> inputType, TaggedChoice<object>.TaggedChoiceType<object> outputType)
    {
        if (inputType.GetKeyType() != outputType.GetKeyType())
        {
            throw new InvalidOperationException("Could not inject: key type is not the same");
        }
        return FixTypeEverywhere(_name, inputType, outputType, ops => input =>
        {
            if (!outputType.HasType(input.First))
            {
                throw new ArgumentException($"{_name}: Unknown type {input.First} in '{_type.TypeName()}'");
            }
            return input;
        });
    }
}
