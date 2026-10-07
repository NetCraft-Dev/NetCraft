namespace NetCraft.DataFixer;

using System;
using NetCraft.DataFixer.Types;
using NetCraft.DataFixer.Types.Templates;
using NetCraft.DataFixer.Util;

//NamedChoiceFinder named choice finder maps to vanilla NamedChoiceFinder
//looks up a choice branch by name in a TaggedChoiceType
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

    //findType delegates to the container type lookup, matching by name with Matcher
    public Either<TypedOptic<object, object, FT, FR>, Type<object>.FieldNotFoundException> FindType<FR>(
        Type<object> containerType, Type<FR> resultType, bool recurse)
        => containerType.FindType(_type, resultType, new Matcher<FT, FR>(_name, _type, resultType), recurse);

    //Matcher named choice matcher builds an optic by name and type
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

        //match finds choiceType, takes the child type by name and builds a Tagged optic on match; returns an error on mismatch and Continue when not found
        //maps to vanilla NamedChoiceFinder.Matcher.match, directly checking whether targetType is a TaggedChoiceType
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
            //Tagged returns TypedOptic<Pair<string,object>,Pair<string,object>,FT2,FR>; the outer Pair<string,object> and object are different CLR types
            //the (object) cast followed by a (TypedOptic<object,object,FT2,FR>) cast throws InvalidCastException at runtime
            //use Unsafe.As to bypass the runtime type check, aligning with Java type erasure semantics
            var taggedRaw = TypedOptics.Tagged<string, FT2, FR>(choiceType, _name, _type, _resultType);
            var taggedObj = (object)taggedRaw!;
            var tagged = System.Runtime.CompilerServices.Unsafe.As<object, TypedOptic<object, object, FT2, FR>>(ref taggedObj);
            return Either<TypedOptic<object, object, FT2, FR>, Type<object>.FieldNotFoundException>.Left(tagged);
        }
    }
}
