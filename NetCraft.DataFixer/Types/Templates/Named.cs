namespace NetCraft.DataFixer.Types.Templates;

using System;
using NetCraft.Codec;
using NetCraft.DataFixer;
using NetCraft.DataFixer.Kinds;
using NetCraft.DataFixer.Optics;
using NetCraft.DataFixer.Optics.Profunctors;
using T = NetCraft.DataFixer.Types;
using NetCraft.DataFixer.Types.Families;
using NetCraft.DataFixer.Util;
using OpticsClass = NetCraft.DataFixer.Optics.Optics;

//Named命名包装模板对应原版com.mojang.datafixers.types.templates.Named
//给元素类型附加名称用于调试与编解码
public sealed record Named(string Name, TypeTemplate Element) : TypeTemplate
{
    public int Size() => Element.Size();

    //apply每个index用DSL.named包装
    public TypeFamily Apply(TypeFamily family)
        => new NamedFamily(this, family);

    //applyO直接复用元素的applyO
    public FamilyOptic<object, object> ApplyO<A, B>(FamilyOptic<A, B> input, T.Type<A> aType, T.Type<B> bType)
        => TypeFamily.FamilyOptic<object, object>(i => Element.ApplyO(input, aType, bType).Apply(i));

    //findFieldOrType直接委托给元素
    public Either<TypeTemplate, T.Type<object>.FieldNotFoundException> FindFieldOrType<A, B>(
        int index, string? name, T.Type<A> type, T.Type<B> resultType)
        => Element.FindFieldOrType(index, name, type, resultType);

    //hmap每个index对元素应用hmap后用cap包装为Named
    public Func<int, RewriteResult<object, object>> Hmap(TypeFamily family, Func<int, RewriteResult<object, object>> function)
        => index =>
        {
            var elementResult = Element.Hmap(family, function)(index);
            return Cap(family, index, elementResult);
        };

    //Cap把元素重写结果用NamedType.fix包装为Named层
    private RewriteResult<object, object> Cap<A>(TypeFamily family, int index, RewriteResult<A, object> elementResult)
    {
        var typeObj = Apply(family).Apply(index)!;
        //NamedFamily.Apply用TypeObjectWrapper包装NamedType对齐Java类型擦除
        //Cap需要NamedType<A>实例从Inner取
        var namedType = typeObj is T.TypeObjectWrapper w
            ? (NamedType<A>)w.Inner
            : (NamedType<A>)(object)typeObj;
        var fixResult = NamedType<A>.Fix(namedType, elementResult);
        //NamedType.Fix返回RewriteResult<Pair<string,A>,object>强转RewriteResult<object,object>失败
        //用Unsafe.As绕过运行时类型检查对齐Java类型擦除
        var fixObj = (object)fixResult;
        return System.Runtime.CompilerServices.Unsafe.As<object, RewriteResult<object, object>>(ref fixObj);
    }

    public override string ToString() => "NamedTypeTag[" + Name + ": " + Element + "]";

    //NamedFamily按index返回DSL.named包装的子类型
    private sealed class NamedFamily : TypeFamily
    {
        private readonly Named _template;
        private readonly TypeFamily _family;
        public NamedFamily(Named template, TypeFamily family)
        {
            _template = template;
            _family = family;
        }
        public T.Type<object> Apply(int index)
            => T.TypeObjectConverterFactory.AsObjectType(
                DSL.Named(_template.Name,
                    _template.Element.Apply(_family).Apply(index)!));
    }

    //NamedType带名称的类型持有Pair<String,A>作为值
    public sealed class NamedType<A> : T.Type<NetCraft.DataFixer.Util.Pair<string, A>>
    {
        private readonly string _name;
        private readonly T.Type<A> _element;

        public NamedType(string name, T.Type<A> element)
        {
            _name = name;
            _element = element;
        }

        public string Name() => _name;
        public T.Type<A> Element() => _element;

        //fix元素重写结果为nop时返回nop否则用Proj2+Adapter投射对齐原版NamedType.fix+wrapOptic
        //原版wrapOptic用Optics.proj2()外层提取Pair.Second再compose内层optic
        public static RewriteResult<NetCraft.DataFixer.Util.Pair<string, A>, object> Fix<A2>(
            NamedType<A2> type, RewriteResult<A2, object> instance)
        {
            if (instance.View().IsNop())
            {
                return (RewriteResult<NetCraft.DataFixer.Util.Pair<string, A>, object>)(object)RewriteResult<NetCraft.DataFixer.Util.Pair<string, A2>, object>.Nop(type);
            }
            var newType = T.TypeObjectConverterFactory.AsObjectType(
                DSL.Named(type.Name(), instance.View().NewType()!))!;
            //newType是TypeObjectWrapper继承Type<object>无法直接强转为Type<Pair<string,object>>
            //用Unsafe.As绕过运行时类型检查对齐Java类型擦除语义
            var newTypeObj = (object)newType!;
            var newTypeCast = System.Runtime.CompilerServices.Unsafe.As<object, Type<NetCraft.DataFixer.Util.Pair<string, object>>>(ref newTypeObj);
            //外层Proj2提取Pair.Second对齐原版Optics.proj2()
            var proj2Optic = new TypedOptic<NetCraft.DataFixer.Util.Pair<string, A2>, NetCraft.DataFixer.Util.Pair<string, object>, A2, object>(
                typeof(ICartesianMu),
                type!,
                newTypeCast,
                instance.View().Type()!,
                instance.View().NewType()!,
                OpticsClass.Proj2<string, A2, object>());
            //内层Adapter从A2到object对齐原版TypedOptic.adapter(view.type, view.newType)
            var innerAdapter = TypedOptics.Adapter<A2, object>(
                instance.View().Type()!,
                instance.View().NewType()!);
            //外层Proj2 Compose 内层Adapter对齐原版.compose(optic)
            var wrappedOptic = proj2Optic.Compose(innerAdapter);
            var wrappedObj = (object)wrappedOptic;
            var wrappedCast = System.Runtime.CompilerServices.Unsafe.As<object, TypedOptic<NetCraft.DataFixer.Util.Pair<string, A2>, NetCraft.DataFixer.Util.Pair<string, object>, object, object>>(ref wrappedObj);
            var opticViewResult = T.Type<NetCraft.DataFixer.Util.Pair<string, A2>>.OpticView(type!,
                (RewriteResult<object, object>)(object)instance,
                wrappedCast);
            //OpticView返回RewriteResult<Pair<string,A2>,Pair<string,object>>强转为RewriteResult<Pair<string,A>,object>失败
            //用Unsafe.As绕过T泛型不变量对齐Java类型擦除
            var opticViewObj = (object)opticViewResult;
            return System.Runtime.CompilerServices.Unsafe.As<object, RewriteResult<NetCraft.DataFixer.Util.Pair<string, A>, object>>(ref opticViewObj);
        }

        public override RewriteResult<NetCraft.DataFixer.Util.Pair<string, A>, object> All(object rule, bool recurse, bool checkIndex)
        {
            var elementView = _element.RewriteOrNop(rule);
            return Fix(this, elementView);
        }

        public override Optional<RewriteResult<NetCraft.DataFixer.Util.Pair<string, A>, object>> One(object rule)
        {
            var view = ((TypeRewriteRule)rule).Rewrite(_element);
            if (!view.IsPresent) return Optional<RewriteResult<NetCraft.DataFixer.Util.Pair<string, A>, object>>.Empty();
            return Optional<RewriteResult<NetCraft.DataFixer.Util.Pair<string, A>, object>>.Of(Fix(this, (RewriteResult<A, object>)view.Get()));
        }

        //findFieldTypeOpt委托给被命名元素
        public override Optional<T.Type<object>> FindFieldTypeOpt(string name)
            => _element.FindFieldTypeOpt(name);

        //findChoiceType委托给被命名元素对应原版NamedType.findChoiceType
        public override Optional<object> FindChoiceType(string name, int index)
            => _element.FindChoiceType(name, index);

        //findTypeInChildren委托element.findType并wrapOptic包装外层为NamedType对齐原版NamedType.findTypeInChildren
        //NamedChoiceFinder.Match在NamedType上不命中返回Continue后走FindTypeInChildren委托到element让Match在TaggedChoiceType上重试命中
        //wrapOptic用Optics.proj2把NamedType投射到element再compose optic应用
        public override Either<TypedOptic<object, object, FT, FR>, FieldNotFoundException> FindTypeInChildren<FT, FR>(
            T.Type<FT> type, T.Type<FR> resultType, TypeMatcher<FT, FR> matcher, bool recurse)
        {
            //NamedType<A>继承Type<Pair<string,A>> _element是Type<A>
            //TypeMatcher/FieldNotFoundException是Type<A>的嵌套类型在不同Type<X>实例化下编译期不同
            //用Unsafe.As把外层matcher和Either转成Type<A>的对应类型
            var elemMatcher = System.Runtime.CompilerServices.Unsafe.As<TypeMatcher<FT, FR>, T.Type<A>.TypeMatcher<FT, FR>>(ref matcher!);
            var elemResultObj = (object)_element.FindType(type, resultType, elemMatcher, recurse);
            var elementResult = System.Runtime.CompilerServices.Unsafe.As<object, Either<TypedOptic<object, object, FT, FR>, T.Type<A>.FieldNotFoundException>>(ref elemResultObj);
            if (elementResult.IsRight)
            {
                //Continue/FieldNotFoundException是Type<X>嵌套类型在不同Type实例化下是不同CLR类型
                //跨泛型is判断失败按类型名识别Continue语义对齐原版跨Type委托行为
                var rightObj = (object)elementResult.GetRight().Get();
                if (rightObj.GetType().Name == "Continue")
                {
                    return Either<TypedOptic<object, object, FT, FR>, FieldNotFoundException>.Right(new Continue());
                }
                return Either<TypedOptic<object, object, FT, FR>, FieldNotFoundException>.Right(new FieldNotFoundException(rightObj.ToString()!));
            }
            var optic = elementResult.GetLeft().Get();
            //外层proj2把Pair<String,A>投射到A再compose optic应用A->B
            //sType/tType用DSL.named包装保持NamedType外层类型链
            var namedSType = (T.Type<NetCraft.DataFixer.Util.Pair<string, object>>)(object)DSL.Named(_name, optic.SType()!)!;
            var namedTType = (T.Type<NetCraft.DataFixer.Util.Pair<string, object>>)(object)DSL.Named(_name, optic.TType()!)!;
            var proj2Optic = new TypedOptic<NetCraft.DataFixer.Util.Pair<string, object>, NetCraft.DataFixer.Util.Pair<string, object>, object, object>(
                typeof(ICartesianMu),
                namedSType!,
                namedTType!,
                optic.SType()!,
                optic.TType()!,
                Optics.Proj2<string, object, object>());
            //proj2Optic内层焦点Pair<string,object>=optic.S/T Compose把焦点换成optic.A/B
            //对齐原版wrap方法的wrapOptic.compose(optic)
            var composed = proj2Optic.Compose(optic);
            //composed外层Pair<string,object>需castOuter为object/object对齐返回类型
            var casted = composed.CastOuterUncheckedObject((object)namedSType!, (object)namedTType!);
            var composedObj = (object)casted;
            var composedCast = System.Runtime.CompilerServices.Unsafe.As<object, TypedOptic<object, object, FT, FR>>(ref composedObj);
            return Either<TypedOptic<object, object, FT, FR>, FieldNotFoundException>.Left(composedCast);
        }

        //findCheckedType委托给被命名元素对应原版NamedType.findCheckedType
        public override Optional<T.Type<object>> FindCheckedType(int index)
            => _element.FindCheckedType(index);

        public override T.Type<object> UpdateMu(RecursiveTypeFamily newFamily)
            => (T.Type<object>)(object)DSL.Named(_name, _element.UpdateMu(newFamily));

        public override TypeTemplate BuildTemplate()
            => DSL.Named(_name, _element.Template());

        //buildCodec按原版逻辑decode附加name encode校验name后委托元素codec
        protected override Codec<NetCraft.DataFixer.Util.Pair<string, A>> BuildCodec()
            => new NamedCodec(this);

        //NamedCodec命名codec decode给值附加name encode校验name匹配后委托元素
        private sealed class NamedCodec : ScalarCodec<NetCraft.DataFixer.Util.Pair<string, A>>
        {
            private readonly NamedType<A> _type;
            public NamedCodec(NamedType<A> type) => _type = type;

            public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, NetCraft.DataFixer.Util.Pair<string, A> input)
            {
                if (!Equals(input.First, _type._name))
                {
                    return DataResult<U>.Error(() => "Named type name doesn't match: expected: " + _type._name + ", got: " + input.First);
                }
                //_element编译时Type<A>运行时可能是TaggedChoiceType<string>继承Type<Pair<string,object>>
                //虚方法Type<A>.Codec()在运行时类型vtable不存在抛EntryPointNotFoundException
                //用AsObjectType包装为TypeObjectWrapper通过反射调用对齐Java类型擦除
                var wrapped = T.TypeObjectConverterFactory.AsObjectType(_type._element);
                return wrapped.Codec().EncodeStart(ops, input.Second);
            }

            public override DataResult<NetCraft.DataFixer.Util.Pair<string, A>> Parse<U>(DynamicOps<U> ops, U input)
            {
                var wrapped = T.TypeObjectConverterFactory.AsObjectType(_type._element);
                var result = wrapped.Codec().Parse(ops, input);
                //result是DataResult<object>实际值是A类型Unsafe.As强转对齐Java类型擦除
                var casted = System.Runtime.CompilerServices.Unsafe.As<DataResult<object>, DataResult<A>>(ref result);
                return casted.Map(v => NetCraft.DataFixer.Util.Pair<string, A>.Of(_type._name, v));
            }
        }

        public override Optional<NetCraft.DataFixer.Util.Pair<string, A>> Point<T>(DynamicOps<T> ops)
        {
            var elementPoint = _element.Point(ops);
            if (!elementPoint.IsPresent) return Optional<NetCraft.DataFixer.Util.Pair<string, A>>.Empty();
            return Optional<NetCraft.DataFixer.Util.Pair<string, A>>.Of(NetCraft.DataFixer.Util.Pair<string, A>.Of(_name, elementPoint.Get()));
        }

        public override bool Equals(object? o, bool ignoreRecursionPoints, bool checkIndex)
        {
            if (ReferenceEquals(this, o)) return true;
            if (o is not NamedType<A> other)
            {
                return false;
            }
            var nameEqual = Equals(_name, other._name);
            var elemEqual = _element.Equals(other._element, ignoreRecursionPoints, checkIndex);
            return nameEqual && elemEqual;
        }

        public override int GetHashCode()
            => unchecked((_name?.GetHashCode() ?? 0) * 31 + (_element?.GetHashCode() ?? 0));
    }
}
