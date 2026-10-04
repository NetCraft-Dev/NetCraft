namespace NetCraft.DataFixer;

using System;
using System.Collections.Generic;
using NetCraft.Codec;
using NetCraft.DataFixer.Schemas;
using T = NetCraft.DataFixer.Types;
using NetCraft.DataFixer.Types.Constant;
using NetCraft.DataFixer.Types.Templates;
using NetCraft.DataFixer.Util;

//DSL领域特定语言工厂对应原版com.mojang.datafixers.DSL
//提供所有Type与TypeTemplate的静态构造入口
public interface DSL
{
    //TypeReference类型引用对应原版DSL.TypeReference
    //子类提供typeName从Schema取模板
    public interface ITypeReference
    {
        string TypeName();

        //in按Schema返回此引用对应的模板
        TypeTemplate In(Schema schema) => schema.Id(TypeName());
    }

    //bool类型
    static T.Type<bool> Bool() => Instances.BOOL_TYPE;

    //intType整数类型
    static T.Type<int> IntType() => Instances.INT_TYPE;

    //longType长整型
    static T.Type<long> LongType() => Instances.LONG_TYPE;

    //byteType字节型
    static T.Type<byte> ByteType() => Instances.BYTE_TYPE;

    //shortType短整型
    static T.Type<short> ShortType() => Instances.SHORT_TYPE;

    //floatType单精度浮点
    static T.Type<float> FloatType() => Instances.FLOAT_TYPE;

    //doubleType双精度浮点
    static T.Type<double> DoubleType() => Instances.DOUBLE_TYPE;

    //string字符串类型
    static T.Type<string> String() => Instances.STRING_TYPE;

    //emptyPart空单元模板
    static TypeTemplate EmptyPart() => ConstType(Instances.EMPTY_PART);

    //emptyPartType空单元Type
    static T.Type<Unit> EmptyPartType() => Instances.EMPTY_PART;

    //remainder透传模板
    static TypeTemplate Remainder() => ConstType(Instances.EMPTY_PASSTHROUGH);

    //remainderType透传Type
    static T.Type<Dynamic<object>> RemainderType() => Instances.EMPTY_PASSTHROUGH;

    //check构造检查模板
    static TypeTemplate Check(string name, int index, TypeTemplate element)
        => new Check(name, index, element);

    //compoundList按值模板构造复合列表模板
    static TypeTemplate CompoundList(TypeTemplate element)
        => CompoundList(ConstType(String()), element);

    //compoundList按值Type构造复合列表Type
    static CompoundList.CompoundListType<string, V> CompoundList<V>(T.Type<V> value)
        => new(String(), value);

    //compoundList按键值模板构造复合列表模板
    static TypeTemplate CompoundList(TypeTemplate key, TypeTemplate element)
        => And(new CompoundList(key, element), Remainder());

    //compoundList按键值Type构造复合列表Type
    static CompoundList.CompoundListType<K, V> CompoundList<K, V>(T.Type<K> key, T.Type<V> value)
        => new(key, value);

    //constType构造常量模板
    static TypeTemplate ConstType<A>(T.Type<A> type) => new Const(T.TypeObjectConverterFactory.AsObjectType(type!));

    //hook按模板构造钩子模板
    static TypeTemplate Hook(TypeTemplate template, Hook.IHookFunction preRead, Hook.IHookFunction postWrite)
        => new Hook(template, preRead, postWrite);

    //hook按Type构造钩子Type
    static T.Type<A> Hook<A>(T.Type<A> type, Hook.IHookFunction preRead, Hook.IHookFunction postWrite)
        => new Hook.HookType<A>(type, preRead, postWrite);

    //list按模板构造列表模板
    static TypeTemplate List(TypeTemplate element) => new List(element);

    //list按Type构造列表Type
    static List.ListType<A> List<A>(T.Type<A> first) => new(first);

    //named按名字与模板构造命名模板
    static TypeTemplate Named(string name, TypeTemplate element) => new Named(name, element);

    //named按名字与Type构造命名Type
    //用DFU的Pair对齐原版com.mojang.datafixers.util.Pair
    static T.Type<NetCraft.DataFixer.Util.Pair<string, A>> Named<A>(string name, T.Type<A> element)
        => new Named.NamedType<A>(name, element);

    //and按两个模板构造积
    static TypeTemplate And(TypeTemplate first, TypeTemplate second) => new Product(first, second);

    //and按首个与可变参构造积
    static TypeTemplate And(TypeTemplate first, params TypeTemplate[] rest)
    {
        TypeTemplate template = first;
        foreach (var r in rest)
        {
            template = And(template, r);
        }
        return template;
    }

    //and按Type构造积Type用Util.Pair对齐原版Pair
    static T.Type<NetCraft.DataFixer.Util.Pair<F, G>> And<F, G>(T.Type<F> first, T.Type<G> second)
        => new Product.ProductType<F, G>(first, second);

    //AndObject非泛型版用Unsafe.As绕过编译期类型检查
    //供PointFreeRule.SortProj等反射调用对齐Java类型擦除语义
    static T.Type<NetCraft.DataFixer.Util.Pair<object, object>> AndObject(object first, object second)
    {
        var fObj = first;
        var sObj = second;
        var fCast = System.Runtime.CompilerServices.Unsafe.As<object, T.Type<object>>(ref fObj);
        var sCast = System.Runtime.CompilerServices.Unsafe.As<object, T.Type<object>>(ref sObj);
        return (T.Type<NetCraft.DataFixer.Util.Pair<object, object>>)(object)And(fCast, sCast);
    }

    //id按index构造递归点模板
    static TypeTemplate Id(int index) => new RecursivePoint(index);

    //or按两个模板构造和
    static TypeTemplate Or(TypeTemplate left, TypeTemplate right) => new Sum(left, right);

    //or按Type构造和Type
    static T.Type<Either<F, G>> Or<F, G>(T.Type<F> first, T.Type<G> second)
        => new Sum.SumType<F, G>(first, second);

    //OrObject非泛型版用Unsafe.As绕过编译期类型检查
    //供PointFreeRule.SortInj等反射调用对齐Java类型擦除语义
    static T.Type<Either<object, object>> OrObject(object first, object second)
    {
        var fObj = first;
        var sObj = second;
        var fCast = System.Runtime.CompilerServices.Unsafe.As<object, T.Type<object>>(ref fObj);
        var sCast = System.Runtime.CompilerServices.Unsafe.As<object, T.Type<object>>(ref sObj);
        return (T.Type<Either<object, object>>)(object)Or(fCast, sCast);
    }

    //field按名与模板构造字段模板
    static TypeTemplate Field(string name, TypeTemplate element) => new Tag(name, element);

    //field按名与Type构造字段Type
    static Tag.TagType<A> Field<A>(string name, T.Type<A> element) => new(name, element);

    //taggedChoice按名与key与模板Map构造TaggedChoice
    static TaggedChoice<K> TaggedChoice<K>(string name, T.Type<K> keyType, Dictionary<K, TypeTemplate> templates)
        => new(name, keyType, templates);

    //taggedChoiceType按名与key与Type Map构造TaggedChoiceType用Util.Pair对齐原版Pair
    static T.Type<NetCraft.DataFixer.Util.Pair<K, object>> TaggedChoiceType<K>(string name, T.Type<K> keyType, Dictionary<K, T.Type<object>> types)
        => new TaggedChoice<K>.TaggedChoiceType<K>(name, keyType, types);

    //func构造函数类型对应原版DSL.func返回Type<Function<A,B>>
    static T.Type<Func<A, B>> Func<A, B>(T.Type<A> input, T.Type<B> output)
        => new T.Func<A, B>(input, output);

    //optional把Type包装为可选Either<A,Unit>
    static T.Type<Either<A, Unit>> Optional<A>(T.Type<A> type) => Or(type, EmptyPartType());

    //optional把模板包装为可选模板
    static TypeTemplate Optional(TypeTemplate value) => Or(value, EmptyPart());

    //allWithRemainder首模板加rest末尾追加remainder对应原版DSL.allWithRemainder
    static TypeTemplate AllWithRemainder(TypeTemplate first, params TypeTemplate[] rest)
    {
        var templates = new List<TypeTemplate> { first };
        templates.AddRange(rest);
        templates.Add(Remainder());
        return And(templates);
    }

    //and按List模板构造积空列表抛异常单元素直返对齐原版DSL.and(List)
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

    //optionalFields单字段可选加余数对齐原版DSL.optionalFields(name,element)
    static TypeTemplate OptionalFields(string name, TypeTemplate element)
        => AllWithRemainder(Optional(Field(name, element)));

    //optionalFields双字段可选加余数
    static TypeTemplate OptionalFields(string name1, TypeTemplate element1, string name2, TypeTemplate element2)
        => AllWithRemainder(Optional(Field(name1, element1)), Optional(Field(name2, element2)));

    //optionalFields按Pair数组构造字段全部Optional加余数对齐原版DSL.optionalFields(Pair...)
    static TypeTemplate OptionalFields(params NetCraft.DataFixer.Util.Pair<string, TypeTemplate>[] fields)
    {
        var templates = new List<TypeTemplate>();
        foreach (var p in fields) templates.Add(Optional(Field(p.First, p.Second)));
        templates.Add(Remainder());
        return And(templates);
    }

    //optionalFieldsLazy按Map懒求值字段全部Optional加余数对应原版DSL.optionalFieldsLazy
    static TypeTemplate OptionalFieldsLazy(Dictionary<string, Func<TypeTemplate>> fields)
    {
        var templates = new List<TypeTemplate>();
        foreach (var kv in fields) templates.Add(Optional(Field(kv.Key, kv.Value())));
        templates.Add(Remainder());
        return And(templates);
    }

    //remainderFinder取得透传类型查找器
    static OpticFinder<Dynamic<object>> RemainderFinder() => Instances.REMAINDER_FINDER;

    //typeFinder按类型构造类型查找器
    static OpticFinder<FT> TypeFinder<FT>(T.Type<FT> type) => new FieldFinder<FT>(null, type);

    //fieldFinder按名与类型构造字段查找器
    static OpticFinder<FT> FieldFinder<FT>(string? name, T.Type<FT> type) => new global::NetCraft.DataFixer.FieldFinder<FT>(name, type);

    //namedChoice按名与类型构造命名选择查找器
    static OpticFinder<FT> NamedChoice<FT>(string name, T.Type<FT> type) => new NamedChoiceFinder<FT>(name, type);

    //unit返回Unit单例
    static Unit Unit() => Util.Unit.Instance;

    //Instances缓存基本Type实例与TaggedChoiceType缓存
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

        //TaggedChoiceType缓存key
        public sealed record TaggedChoiceCacheKey<K>(string Name, T.Type<K> KeyType, Dictionary<K, T.Type<object>> Types)
        {
            public TaggedChoice<K>.TaggedChoiceType<K> Build()
                => new(Name, KeyType, new Dictionary<K, T.Type<object>>(Types));
        }
    }
}
