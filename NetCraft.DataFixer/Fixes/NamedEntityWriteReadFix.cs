using NetCraft.Codec;
using NetCraft.DataFixer.Schemas;
using T = NetCraft.DataFixer.Types;

namespace NetCraft.DataFixer.Fixes;

//命名实体写读修复父类对应原版net.minecraft.util.datafix.fixes.NamedEntityWriteReadFix
//按entityName匹配特定实体后write+fix+read应用子类的fix方法
public abstract class NamedEntityWriteReadFix : DataFix
{
    private readonly string _name;
    private readonly string _entityName;
    private readonly DSL.ITypeReference _type;

    protected abstract Dynamic<object> Fix(Dynamic<object> input);

    public NamedEntityWriteReadFix(Schema outputSchema, bool changesType, string name, DSL.ITypeReference type, string entityName)
        : base(outputSchema, changesType)
    {
        _name = name;
        _type = type;
        _entityName = entityName;
    }

    protected override TypeRewriteRule MakeRule()
    {
        var inputEntityType = GetInputSchema().GetType(_type);
        var inputEntityChoiceType = GetInputSchema().GetChoiceType(_type, _entityName);
        var outputEntityType = GetOutputSchema().GetType(_type);
        var entityF = DSL.NamedChoice(_entityName, inputEntityChoiceType);
        var patchedEntityType = ExtraDataFixUtils.PatchSubType(inputEntityType, inputEntityType, outputEntityType);
        return FixInternal(inputEntityType, outputEntityType, patchedEntityType, entityF);
    }

    //fixInternal按inputType与outputType与patchedType与choiceFinder构造规则
    private TypeRewriteRule FixInternal(
        T.Type<object> inputEntityType, T.Type<object> outputEntityType, T.Type<object> patchedEntityType,
        OpticFinder<object> choiceFinder)
        => FixTypeEverywhereTyped(_name, inputEntityType, outputEntityType, typed =>
        {
            if (!typed.GetOptional(choiceFinder).IsPresent)
                return ExtraDataFixUtils.Cast<object, object>(outputEntityType, typed);
            var fakeTyped = ExtraDataFixUtils.Cast<object, object>(patchedEntityType, typed);
            return DataFixUtils.WriteAndReadTypedOrThrow<object, object>(fakeTyped, outputEntityType, d => Fix(d));
        });
}
