namespace NetCraft.DataFixer;

using System;
using NetCraft.Codec;
using NetCraft.DataFixer.Kinds;
using NetCraft.DataFixer.Types;
using NetCraft.DataFixer.Types.Templates;
using NetCraft.DataFixer.Util;
using OpticsClass = NetCraft.DataFixer.Optics.Optics;

//FieldFinder字段查找器对应原版com.mojang.datafixers.FieldFinder
//按字段名与类型在TagType或TaggedChoiceType中查找
//对齐原版Java FieldFinder.Matcher.match仅处理Tag.TagType与TaggedChoice.TaggedChoiceType
//其他容器类型返回Continue走FindTypeInChildren递归
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

    //findType委托容器类型查找用Matcher按名匹配
    public Either<TypedOptic<object, object, FT, FR>, Type<object>.FieldNotFoundException> FindType<FR>(
        Type<object> containerType, Type<FR> resultType, bool recurse)
        => containerType.FindType(_type, resultType, new Matcher<FT, FR>(_name, _type, resultType), recurse);

    //Matcher字段匹配器按名与类型构造optic对齐原版FieldFinder.Matcher.match
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

        //match按名查找字段名空时按类型匹配adapter
            //TagType分支按名+元素类型匹配返回Adapter(Optics.Id Profunctor.Mu)
            //TaggedChoiceType分支按名+keyType类型匹配+type==resultType检查返回Proj1(Cartesian.Mu)
            //其他类型返回Continue
            public Either<TypedOptic<object, object, FT2, FR>, Type<object>.FieldNotFoundException> Match<S>(Type<S> targetType)
            {
                if (_name == null)
                {
                    if (targetType.Equals(_type, true, true))
                    {
                        //targetType可能是EmptyPartPassthrough等Type<具体T>无法cast为Type<object>
                        //用Unsafe.As绕过运行时类型检查对齐Java类型擦除
                        var targetObj = (object)targetType;
                        var targetCast = System.Runtime.CompilerServices.Unsafe.As<object, Type<object>>(ref targetObj);
                        var resultObj = (object)_resultType!;
                        var resultCast = System.Runtime.CompilerServices.Unsafe.As<object, Type<object>>(ref resultObj);
                        //Adapter返回TypedOptic<object,object,object,object>需cast为TypedOptic<object,object,FT2,FR>
                        //用Unsafe.As绕过运行时类型检查对齐Java类型擦除
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
                //tType用DSL.field构造对齐原版DSL.field(tagType.name(), resultType)
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
                    //对齐原版capChoice用Proj1作optic bounds=Cartesian.Mu
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

    //TryGetTagInfo反射判断targetType是否Tag.TagType<A>并取Name/Element
    //C#无Java类型擦除需反射对齐instanceof Tag.TagType<?>语义
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

    //TryGetTaggedChoiceInfo反射判断targetType是否TaggedChoice<K>.TaggedChoiceType<K>并取Name/KeyType
    private static bool TryGetTaggedChoiceInfo(object type, out string? name, out object? keyType)
    {
        name = null;
        keyType = null;
        var t = type.GetType();
        if (!t.IsGenericType) return false;
        var genericDef = t.GetGenericTypeDefinition();
        //TaggedChoice<K>.TaggedChoiceType<K2>开放泛型声明类型是TaggedChoice<K>
        if (genericDef.DeclaringType != typeof(TaggedChoice<>)) return false;
        if (genericDef.Name != "TaggedChoiceType`1") return false;
        name = (string)t.GetMethod("GetName")!.Invoke(type, null)!;
        keyType = t.GetMethod("GetKeyType")!.Invoke(type, null);
        return true;
    }
}
