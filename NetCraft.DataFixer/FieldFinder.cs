namespace NetCraft.DataFixer;

using System;
using NetCraft.Codec;
using NetCraft.DataFixer.Kinds;
using NetCraft.DataFixer.Types;
using NetCraft.DataFixer.Types.Templates;
using NetCraft.DataFixer.Util;
using OpticsClass = NetCraft.DataFixer.Optics.Optics;

//FieldFinder field finder maps to vanilla com.mojang.datafixers.FieldFinder
//looks up in TagType or TaggedChoiceType by field name and type
//aligns with vanilla Java FieldFinder.Matcher.match, handling only Tag.TagType and TaggedChoice.TaggedChoiceType
//other container types return Continue and go through the recursive FindTypeInChildren
public sealed class FieldFinder<FT> : OpticFinder<FT>
{
    private readonly string? _name;
    private readonly Type<FT> _type;

    public FieldFinder(string? name, Type<FT> type)
    {
        _name = name;
        _type = type;
    }

    public Type<FT> Type() => _type;

    //findType delegates to the container type lookup, matching by name with Matcher
    public Either<TypedOptic<object, object, FT, FR>, Type<object>.FieldNotFoundException> FindType<FR>(
        Type<object> containerType, Type<FR> resultType, bool recurse)
        => containerType.FindType(_type, resultType, new Matcher<FT, FR>(_name, _type, resultType), recurse);

    //Matcher field matcher builds an optic by name and type, aligning with vanilla FieldFinder.Matcher.match
    private sealed class Matcher<FT2, FR> : Type<object>.TypeMatcher<FT2, FR>
    {
        private readonly Type<FR> _resultType;
        private readonly string? _name;
        private readonly Type<FT2> _type;

        public Matcher(string? name, Type<FT2> type, Type<FR> resultType)
        {
            _resultType = resultType;
            _name = name;
            _type = type;
        }

        //match looks up by name; when the field name is empty it matches the type with an adapter
            //TagType branch matches by name + element type and returns Adapter(Optics.Id, Profunctor.Mu)
            //TaggedChoiceType branch matches by name + keyType type and checks type==resultType, returning Proj1(Cartesian.Mu)
            //other types return Continue
            public Either<TypedOptic<object, object, FT2, FR>, Type<object>.FieldNotFoundException> Match<S>(Type<S> targetType)
            {
                if (_name == null)
                {
                    if (targetType.Equals(_type, true, true))
                    {
                        //targetType may be a Type<concrete T> such as EmptyPartPassthrough and cannot be cast to Type<object>
                        //use Unsafe.As to bypass the runtime type check and align with Java type erasure
                        var targetObj = (object)targetType;
                        var targetCast = System.Runtime.CompilerServices.Unsafe.As<object, Type<object>>(ref targetObj);
                        var resultObj = (object)_resultType!;
                        var resultCast = System.Runtime.CompilerServices.Unsafe.As<object, Type<object>>(ref resultObj);
                        //Adapter returns TypedOptic<object,object,object,object>, which needs casting to TypedOptic<object,object,FT2,FR>
                        //use Unsafe.As to bypass the runtime type check and align with Java type erasure
                        var adapterObj = (object)TypedOptics.Adapter<object, object>(targetCast, resultCast);
                        var adapter = System.Runtime.CompilerServices.Unsafe.As<object, TypedOptic<object, object, FT2, FR>>(ref adapterObj);
                        return Either<TypedOptic<object, object, FT2, FR>, Type<object>.FieldNotFoundException>
                            .Left(adapter);
                    }
                    return Either<TypedOptic<object, object, FT2, FR>, Type<object>.FieldNotFoundException>
                        .Right(new Type<object>.Continue());
                }

            if (TryGetTagInfo(targetType, out var tagName, out var tagElement))
            {
                if (!Equals(_name, tagName))
                {
                    return Either<TypedOptic<object, object, FT2, FR>, Type<object>.FieldNotFoundException>
                        .Right(new Type<object>.FieldNotFoundException($"Not found: \"{_name}\" (in type: {targetType})"));
                }
                var elementObj = (Type<object>)tagElement!;
                if (!elementObj.Equals(_type, true, true))
                {
                    return Either<TypedOptic<object, object, FT2, FR>, Type<object>.FieldNotFoundException>
                        .Right(new Type<object>.FieldNotFoundException($"Type error for field \"{_name}\": expected type: {_type}, actual type: {elementObj})"));
                }
                //tType is built with DSL.field, aligning with vanilla DSL.field(tagType.name(), resultType)
                var tType = (Type<object>)(object)DSL.Field(tagName!, (Type<FR>)(object)_resultType!);
                return Either<TypedOptic<object, object, FT2, FR>, Type<object>.FieldNotFoundException>
                    .Left((TypedOptic<object, object, FT2, FR>)(object)new TypedOptic<object, object, FT2, FR>(
                        typeof(IProfunctorMu),
                        (Type<object>)(object)targetType,
                        tType,
                        (Type<FT2>)(object)_type,
                        (Type<FR>)(object)_resultType!,
                        OpticsClass.Id<object, object>()));
            }

            if (TryGetTaggedChoiceInfo(targetType, out var choiceName, out var choiceKeyType))
            {
                if (Equals(_name, choiceName))
                {
                    var keyTypeObj = (Type<object>)choiceKeyType!;
                    if (!keyTypeObj.Equals(_type, true, true))
                    {
                        return Either<TypedOptic<object, object, FT2, FR>, Type<object>.FieldNotFoundException>
                            .Right(new Type<object>.FieldNotFoundException($"Type error for field \"{_name}\": expected type: {_type}, actual type: {choiceKeyType})"));
                    }
                    if (!_type.Equals(_resultType, true, true))
                    {
                        return Either<TypedOptic<object, object, FT2, FR>, Type<object>.FieldNotFoundException>
                            .Right(new Type<object>.FieldNotFoundException("TaggedChoiceType key type change is unsupported."));
                    }
                    //aligns with vanilla capChoice using Proj1 as the optic, bounds=Cartesian.Mu
                    return Either<TypedOptic<object, object, FT2, FR>, Type<object>.FieldNotFoundException>
                        .Left((TypedOptic<object, object, FT2, FR>)(object)new TypedOptic<object, object, FT2, FR>(
                            typeof(ICartesianMu),
                            (Type<object>)(object)targetType,
                            (Type<object>)(object)targetType,
                            (Type<FT2>)(object)_type,
                            (Type<FR>)(object)_resultType!,
                            OpticsClass.Proj1<object, object, object>()));
                }
            }

            return Either<TypedOptic<object, object, FT2, FR>, Type<object>.FieldNotFoundException>
                .Right(new Type<object>.Continue());
        }
    }

    //TryGetTagInfo reflectively checks whether targetType is Tag.TagType<A> and takes Name/Element
    //C# has no Java type erasure, so reflection is needed to align with the instanceof Tag.TagType<?> semantics
    private static bool TryGetTagInfo(object type, out string? name, out object? element)
    {
        name = null;
        element = null;
        var t = type.GetType();
        if (!t.IsGenericType) return false;
        if (t.GetGenericTypeDefinition() != typeof(Tag.TagType<>)) return false;
        name = (string)t.GetMethod("Name")!.Invoke(type, null)!;
        element = t.GetMethod("Element")!.Invoke(type, null);
        return true;
    }

    //TryGetTaggedChoiceInfo reflectively checks whether targetType is TaggedChoice<K>.TaggedChoiceType<K> and takes Name/KeyType
    private static bool TryGetTaggedChoiceInfo(object type, out string? name, out object? keyType)
    {
        name = null;
        keyType = null;
        var t = type.GetType();
        if (!t.IsGenericType) return false;
        var genericDef = t.GetGenericTypeDefinition();
        //TaggedChoice<K>.TaggedChoiceType<K2>'s open generic declaring type is TaggedChoice<K>
        if (genericDef.DeclaringType != typeof(TaggedChoice<>)) return false;
        if (genericDef.Name != "TaggedChoiceType`1") return false;
        name = (string)t.GetMethod("GetName")!.Invoke(type, null)!;
        keyType = t.GetMethod("GetKeyType")!.Invoke(type, null);
        return true;
    }
}
