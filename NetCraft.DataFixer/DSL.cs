namespace NetCraft.DataFixer;

using System;
using System.Collections.Generic;
using NetCraft.Codec;
using NetCraft.DataFixer.Schemas;
using T = NetCraft.DataFixer.Types;
using NetCraft.DataFixer.Types.Constant;
using NetCraft.DataFixer.Types.Templates;
using NetCraft.DataFixer.Util;

//DSL domain-specific language factory maps to vanilla com.mojang.datafixers.DSL
//provides static construction entry points for all Types and TypeTemplates
public interface DSL
{
    //TypeReference type reference maps to vanilla DSL.TypeReference
    //subclasses provide typeName to take a template from the Schema
    public interface ITypeReference
    {
        string TypeName();

        //in returns the template for this reference from the Schema
        TypeTemplate In(Schema schema) => schema.Id(TypeName());
    }

    //bool type
    static T.Type<bool> Bool() => Instances.BOOL_TYPE;

    //intType integer type
    static T.Type<int> IntType() => Instances.INT_TYPE;

    //longType long integer
    static T.Type<long> LongType() => Instances.LONG_TYPE;

    //byteType byte type
    static T.Type<byte> ByteType() => Instances.BYTE_TYPE;

    //shortType short integer
    static T.Type<short> ShortType() => Instances.SHORT_TYPE;

    //floatType single-precision float
    static T.Type<float> FloatType() => Instances.FLOAT_TYPE;

    //doubleType double-precision float
    static T.Type<double> DoubleType() => Instances.DOUBLE_TYPE;

    //string string type
    static T.Type<string> String() => Instances.STRING_TYPE;

    //emptyPart empty unit template
    static TypeTemplate EmptyPart() => ConstType(Instances.EMPTY_PART);

    //emptyPartType empty unit Type
    static T.Type<Unit> EmptyPartType() => Instances.EMPTY_PART;

    //remainder passthrough template
    static TypeTemplate Remainder() => ConstType(Instances.EMPTY_PASSTHROUGH);

    //remainderType passthrough Type
    static T.Type<Dynamic<object>> RemainderType() => Instances.EMPTY_PASSTHROUGH;

    //check builds a check template
    static TypeTemplate Check(string name, int index, TypeTemplate element)
        => new Check(name, index, element);

    //compoundList builds a compound list template from a value template
    static TypeTemplate CompoundList(TypeTemplate element)
        => CompoundList(ConstType(String()), element);

    //compoundList builds a compound list Type from a value Type
    static CompoundList.CompoundListType<string, V> CompoundList<V>(T.Type<V> value)
        => new(String(), value);

    //compoundList builds a compound list template from key and value templates
    static TypeTemplate CompoundList(TypeTemplate key, TypeTemplate element)
        => And(new CompoundList(key, element), Remainder());

    //compoundList builds a compound list Type from key and value Types
    static CompoundList.CompoundListType<K, V> CompoundList<K, V>(T.Type<K> key, T.Type<V> value)
        => new(key, value);

    //constType builds a constant template
    static TypeTemplate ConstType<A>(T.Type<A> type) => new Const(T.TypeObjectConverterFactory.AsObjectType(type!));

    //hook builds a hook template from a template
    static TypeTemplate Hook(TypeTemplate template, Hook.IHookFunction preRead, Hook.IHookFunction postWrite)
        => new Hook(template, preRead, postWrite);

    //hook builds a hook Type from a Type
    static T.Type<A> Hook<A>(T.Type<A> type, Hook.IHookFunction preRead, Hook.IHookFunction postWrite)
        => new Hook.HookType<A>(type, preRead, postWrite);

    //list builds a list template from a template
    static TypeTemplate List(TypeTemplate element) => new List(element);

    //list builds a list Type from a Type
    static List.ListType<A> List<A>(T.Type<A> first) => new(first);

    //named builds a named template from a name and template
    static TypeTemplate Named(string name, TypeTemplate element) => new Named(name, element);

    //named builds a named Type from a name and Type
    //uses DFU's Pair, aligning with vanilla com.mojang.datafixers.util.Pair
    static T.Type<NetCraft.DataFixer.Util.Pair<string, A>> Named<A>(string name, T.Type<A> element)
        => new Named.NamedType<A>(name, element);

    //and builds a product from two templates
    static TypeTemplate And(TypeTemplate first, TypeTemplate second) => new Product(first, second);

    //and builds a product from the first and a params array
    static TypeTemplate And(TypeTemplate first, params TypeTemplate[] rest)
    {
        TypeTemplate template = first;
        foreach (var r in rest)
        {
            template = And(template, r);
        }
        return template;
    }

    //and builds a product Type from Types, using Util.Pair to align with vanilla Pair
    static T.Type<NetCraft.DataFixer.Util.Pair<F, G>> And<F, G>(T.Type<F> first, T.Type<G> second)
        => new Product.ProductType<F, G>(first, second);

    //AndObject is a non-generic version using Unsafe.As to bypass compile-time type checking
    //used by reflective calls such as PointFreeRule.SortProj, aligning with Java type erasure semantics
    static T.Type<NetCraft.DataFixer.Util.Pair<object, object>> AndObject(object first, object second)
    {
        var fObj = first;
        var sObj = second;
        var fCast = System.Runtime.CompilerServices.Unsafe.As<object, T.Type<object>>(ref fObj);
        var sCast = System.Runtime.CompilerServices.Unsafe.As<object, T.Type<object>>(ref sObj);
        return (T.Type<NetCraft.DataFixer.Util.Pair<object, object>>)(object)And(fCast, sCast);
    }

    //id builds a recursive point template from an index
    static TypeTemplate Id(int index) => new RecursivePoint(index);

    //or builds a sum from two templates
    static TypeTemplate Or(TypeTemplate left, TypeTemplate right) => new Sum(left, right);

    //or builds a sum Type from Types
    static T.Type<Either<F, G>> Or<F, G>(T.Type<F> first, T.Type<G> second)
        => new Sum.SumType<F, G>(first, second);

    //OrObject is a non-generic version using Unsafe.As to bypass compile-time type checking
    //used by reflective calls such as PointFreeRule.SortInj, aligning with Java type erasure semantics
    static T.Type<Either<object, object>> OrObject(object first, object second)
    {
        var fObj = first;
        var sObj = second;
        var fCast = System.Runtime.CompilerServices.Unsafe.As<object, T.Type<object>>(ref fObj);
        var sCast = System.Runtime.CompilerServices.Unsafe.As<object, T.Type<object>>(ref sObj);
        return (T.Type<Either<object, object>>)(object)Or(fCast, sCast);
    }

    //field builds a field template from a name and template
    static TypeTemplate Field(string name, TypeTemplate element) => new Tag(name, element);

    //field builds a field Type from a name and Type
    static Tag.TagType<A> Field<A>(string name, T.Type<A> element) => new(name, element);

    //taggedChoice builds a TaggedChoice from a name, key, and template Map
    static TaggedChoice<K> TaggedChoice<K>(string name, T.Type<K> keyType, Dictionary<K, TypeTemplate> templates)
        => new(name, keyType, templates);

    //taggedChoiceType builds a TaggedChoiceType from a name, key, and Type Map, using Util.Pair to align with vanilla Pair
    static T.Type<NetCraft.DataFixer.Util.Pair<K, object>> TaggedChoiceType<K>(string name, T.Type<K> keyType, Dictionary<K, T.Type<object>> types)
        => new TaggedChoice<K>.TaggedChoiceType<K>(name, keyType, types);

    //func builds a function type, maps to vanilla DSL.func returning Type<Function<A,B>>
    static T.Type<Func<A, B>> Func<A, B>(T.Type<A> input, T.Type<B> output)
        => new T.Func<A, B>(input, output);

    //optional wraps a Type as an optional Either<A,Unit>
    static T.Type<Either<A, Unit>> Optional<A>(T.Type<A> type) => Or(type, EmptyPartType());

    //optional wraps a template as an optional template
    static TypeTemplate Optional(TypeTemplate value) => Or(value, EmptyPart());

    //allWithRemainder appends a remainder after the first template and rest, maps to vanilla DSL.allWithRemainder
    static TypeTemplate AllWithRemainder(TypeTemplate first, params TypeTemplate[] rest)
    {
        var templates = new List<TypeTemplate> { first };
        templates.AddRange(rest);
        templates.Add(Remainder());
        return And(templates);
    }

    //and builds a product from a List of templates, throws on an empty list and returns a single element directly, aligning with vanilla DSL.and(List)
    static TypeTemplate And(List<TypeTemplate> templates)
    {
        if (templates.Count == 0) throw new ArgumentException("Must have at least one type");
        if (templates.Count == 1) return templates[0];
        var result = templates[templates.Count - 1];
        for (int i = templates.Count - 2; i >= 0; i--)
        {
            result = And(templates[i], result);
        }
        return result;
    }

    //optionalFields makes a single field optional plus a remainder, aligns with vanilla DSL.optionalFields(name,element)
    static TypeTemplate OptionalFields(string name, TypeTemplate element)
        => AllWithRemainder(Optional(Field(name, element)));

    //optionalFields makes two fields optional plus a remainder
    static TypeTemplate OptionalFields(string name1, TypeTemplate element1, string name2, TypeTemplate element2)
        => AllWithRemainder(Optional(Field(name1, element1)), Optional(Field(name2, element2)));

    //optionalFields makes all fields Optional from a Pair array plus a remainder, aligns with vanilla DSL.optionalFields(Pair...)
    static TypeTemplate OptionalFields(params NetCraft.DataFixer.Util.Pair<string, TypeTemplate>[] fields)
    {
        var templates = new List<TypeTemplate>();
        foreach (var p in fields) templates.Add(Optional(Field(p.First, p.Second)));
        templates.Add(Remainder());
        return And(templates);
    }

    //optionalFieldsLazy lazily evaluates fields from a Map, all Optional plus a remainder, maps to vanilla DSL.optionalFieldsLazy
    static TypeTemplate OptionalFieldsLazy(Dictionary<string, Func<TypeTemplate>> fields)
    {
        var templates = new List<TypeTemplate>();
        foreach (var kv in fields) templates.Add(Optional(Field(kv.Key, kv.Value())));
        templates.Add(Remainder());
        return And(templates);
    }

    //remainderFinder gets the passthrough type finder
    static OpticFinder<Dynamic<object>> RemainderFinder() => Instances.REMAINDER_FINDER;

    //typeFinder builds a type finder from a type
    static OpticFinder<FT> TypeFinder<FT>(T.Type<FT> type) => new FieldFinder<FT>(null, type);

    //fieldFinder builds a field finder from a name and type
    static OpticFinder<FT> FieldFinder<FT>(string? name, T.Type<FT> type) => new global::NetCraft.DataFixer.FieldFinder<FT>(name, type);

    //namedChoice builds a named choice finder from a name and type
    static OpticFinder<FT> NamedChoice<FT>(string name, T.Type<FT> type) => new NamedChoiceFinder<FT>(name, type);

    //unit returns the Unit singleton
    static Unit Unit() => Util.Unit.Instance;

    //Instances caches basic Type instances and the TaggedChoiceType cache
    public static class Instances
    {
        public static readonly T.Type<bool> BOOL_TYPE = new Const.PrimitiveType<bool>(null!);
        public static readonly T.Type<int> INT_TYPE = new Const.PrimitiveType<int>(null!);
        public static readonly T.Type<long> LONG_TYPE = new Const.PrimitiveType<long>(null!);
        public static readonly T.Type<byte> BYTE_TYPE = new Const.PrimitiveType<byte>(null!);
        public static readonly T.Type<short> SHORT_TYPE = new Const.PrimitiveType<short>(null!);
        public static readonly T.Type<float> FLOAT_TYPE = new Const.PrimitiveType<float>(null!);
        public static readonly T.Type<double> DOUBLE_TYPE = new Const.PrimitiveType<double>(null!);
        public static readonly T.Type<string> STRING_TYPE = new Const.PrimitiveType<string>(null!);
        public static readonly T.Type<Unit> EMPTY_PART = new EmptyPart();
        public static readonly T.Type<Dynamic<object>> EMPTY_PASSTHROUGH = new EmptyPartPassthrough();

        public static readonly OpticFinder<Dynamic<object>> REMAINDER_FINDER
            = new FieldFinder<Dynamic<object>>(null, EMPTY_PASSTHROUGH);

        //TaggedChoiceType cache key
        public sealed record TaggedChoiceCacheKey<K>(string Name, T.Type<K> KeyType, Dictionary<K, T.Type<object>> Types)
        {
            public TaggedChoice<K>.TaggedChoiceType<K> Build()
                => new(Name, KeyType, new Dictionary<K, T.Type<object>>(Types));
        }
    }
}
