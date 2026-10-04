namespace NetCraft.DataFixer;

using System;
using NetCraft.DataFixer.Types;
using NetCraft.DataFixer.Types.Templates;
using NetCraft.DataFixer.Util;

//NamedChoiceFinder命名选择查找器对应原版NamedChoiceFinder
//在TaggedChoiceType中按名查找选择分支
internal sealed class NamedChoiceFinder<FT> : OpticFinder<FT>
{
    private readonly string _name;
    private readonly Type<FT> _type;

    public NamedChoiceFinder(string name, Type<FT> type)
    {
        _name = name;
        _type = type;
    }

    public Type<FT> Type() => _type;

    //findType委托容器类型查找用Matcher按名匹配
    public Either<TypedOptic<object, object, FT, FR>, Type<object>.FieldNotFoundException> FindType<FR>(
        Type<object> containerType, Type<FR> resultType, bool recurse)
        => containerType.FindType(_type, resultType, new Matcher<FT, FR>(_name, _type, resultType), recurse);

    //Matcher命名选择匹配器按名与类型构造optic
    private sealed class Matcher<FT2, FR> : Type<object>.TypeMatcher<FT2, FR>
    {
        private readonly Type<FR> _resultType;
        private readonly string _name;
        private readonly Type<FT2> _type;

        public Matcher(string name, Type<FT2> type, Type<FR> resultType)
        {
            _resultType = resultType;
            _name = name;
            _type = type;
        }

        //match查找choiceType按名取子类型匹配则构造Tagged optic不匹配返回错误没找到返回Continue
        //对应原版NamedChoiceFinder.Matcher.match直接检查targetType是否TaggedChoiceType
        public Either<TypedOptic<object, object, FT2, FR>, Type<object>.FieldNotFoundException> Match<S>(Type<S> targetType)
        {
            if (targetType is not TaggedChoice<string>.TaggedChoiceType<string> choiceType)
            {
                return Either<TypedOptic<object, object, FT2, FR>, Type<object>.FieldNotFoundException>.Right(new Type<object>.Continue());
            }
            if (!choiceType.Types().ContainsKey(_name))
            {
                return Either<TypedOptic<object, object, FT2, FR>, Type<object>.FieldNotFoundException>.Right(new Type<object>.FieldNotFoundException("Choice type doesn't contain key: " + _name));
            }
            var matchedType = choiceType.Types()[_name];
            if (!matchedType.Equals(_type, true, true))
            {
                return Either<TypedOptic<object, object, FT2, FR>, Type<object>.FieldNotFoundException>.Right(new Type<object>.FieldNotFoundException("Type " + matchedType + " is not equal to type " + _type));
            }
            //Tagged返回TypedOptic<Pair<string,object>,Pair<string,object>,FT2,FR>外层Pair<string,object>与object不同CLR类型
            //(object)cast再(TypedOptic<object,object,FT2,FR>)cast运行时castclass会抛InvalidCastException
            //用Unsafe.As绕过运行时类型检查对齐Java类型擦除语义
            var taggedRaw = TypedOptics.Tagged<string, FT2, FR>(choiceType, _name, _type, _resultType);
            var taggedObj = (object)taggedRaw!;
            var tagged = System.Runtime.CompilerServices.Unsafe.As<object, TypedOptic<object, object, FT2, FR>>(ref taggedObj);
            return Either<TypedOptic<object, object, FT2, FR>, Type<object>.FieldNotFoundException>.Left(tagged);
        }
    }
}
