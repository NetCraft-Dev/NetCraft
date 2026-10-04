namespace NetCraft.DataFixer.Fixes;

using System;
using NetCraft.Codec;
using NetCraft.DataFixer.Schemas;
using NetCraft.DataFixer.Types.Templates;

//新增选项修复对应原版net.minecraft.util.datafix.fixes.AddNewChoices
//给TaggedChoice类型注入新选项原版本号升级新增实体方块时使用
public class AddNewChoices : DataFix
{
    private readonly string _name;
    private readonly DSL.ITypeReference _type;

    public AddNewChoices(Schema outputSchema, string name, DSL.ITypeReference type) : base(outputSchema, changesType: true)
    {
        _name = name;
        _type = type;
    }

    //makeRule按输入输出Schema的TaggedChoice类型构造cap
    protected override TypeRewriteRule MakeRule()
    {
        var inputType = GetInputSchema().FindChoiceType(_type);
        var outputType = GetOutputSchema().FindChoiceType(_type);
        return Cap(inputType, outputType);
    }

    //cap校验keyType一致后用fixTypeEverywhere按name构造透传规则
    //原版泛型方法K是key类型C#用object对齐类型擦除
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
