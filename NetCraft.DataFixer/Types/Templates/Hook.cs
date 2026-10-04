namespace NetCraft.DataFixer.Types.Templates;

using System;
using NetCraft.Codec;
using NetCraft.DataFixer;
using T = NetCraft.DataFixer.Types;
using NetCraft.DataFixer.Types.Families;
using NetCraft.DataFixer.Util;

//Hook钩子模板对应原版com.mojang.datafixers.types.templates.Hook
//在元素类型的读前与写后插入hook函数
public sealed record Hook(TypeTemplate Element, Hook.IHookFunction PreRead, Hook.IHookFunction PostWrite) : TypeTemplate
{
    //HookFunction钩子函数接口对Dynamic值做读前或写后变换
    public interface IHookFunction
    {
        //Identity恒等钩子默认实例
        public static readonly IHookFunction Identity = new IdentityHookFunction();

        T Apply<T>(DynamicOps<T> ops, T value);
    }

    //IdentityHookFunction恒等钩子实现
    private sealed class IdentityHookFunction : IHookFunction
    {
        public T Apply<T>(DynamicOps<T> ops, T value) => value;
    }

    public int Size() => Element.Size();

    //apply每个index用DSL.hook包装元素类型
    public TypeFamily Apply(TypeFamily family)
        => new HookFamily(this, family);

    //applyO直接复用元素的applyO
    public FamilyOptic<object, object> ApplyO<A, B>(FamilyOptic<A, B> input, T.Type<A> aType, T.Type<B> bType)
        => TypeFamily.FamilyOptic<object, object>(i => Element.ApplyO(input, aType, bType).Apply(i));

    //findFieldOrType直接委托给元素
    public Either<TypeTemplate, T.Type<object>.FieldNotFoundException> FindFieldOrType<A, B>(
        int index, string? name, T.Type<A> type, T.Type<B> resultType)
        => Element.FindFieldOrType(index, name, type, resultType);

    //hmap每个index对元素应用hmap后用cap包装为Hook
    public Func<int, RewriteResult<object, object>> Hmap(TypeFamily family, Func<int, RewriteResult<object, object>> function)
        => index =>
        {
            var elementResult = Element.Hmap(family, function)(index);
            return Cap(family, index, elementResult);
        };

    //Cap把元素重写结果用HookType.fix包装为Hook层
    private RewriteResult<object, object> Cap<A>(TypeFamily family, int index, RewriteResult<A, object> elementResult)
        => (RewriteResult<object, object>)(object)HookType<A>.Fix((HookType<A>)(object)Apply(family).Apply(index)!, elementResult);

    public override string ToString() => "Hook[" + Element + ", " + PreRead + ", " + PostWrite + "]";

    //HookFamily按index返回DSL.hook包装的子类型
    private sealed class HookFamily : TypeFamily
    {
        private readonly Hook _template;
        private readonly TypeFamily _family;
        public HookFamily(Hook template, TypeFamily family)
        {
            _template = template;
            _family = family;
        }
        public T.Type<object> Apply(int index)
            => (T.Type<object>)(object)DSL.Hook(
                (T.Type<object>)(object)_template.Element.Apply(_family).Apply(index)!,
                _template.PreRead, _template.PostWrite);
    }

    //HookType带钩子的包装类型编解码时调用钩子
    public sealed class HookType<A> : T.Type<A>
    {
        private readonly T.Type<A> _delegate;
        private readonly IHookFunction _preRead;
        private readonly IHookFunction _postWrite;

        public HookType(T.Type<A> @delegate, IHookFunction preRead, IHookFunction postWrite)
        {
            _delegate = @delegate;
            _preRead = preRead;
            _postWrite = postWrite;
        }

        //buildCodec按原版decode用preRead变换后委托encode用postWrite变换
        protected override Codec<A> BuildCodec()
            => new HookCodec(this);

        //HookCodec钩子codec decode用preRead encode用postWrite
        private sealed class HookCodec : ScalarCodec<A>
        {
            private readonly HookType<A> _type;
            public HookCodec(HookType<A> type) => _type = type;

            public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, A value)
                => _type._delegate.Codec().EncodeStart(ops, value)
                    .Map(v => _type._postWrite.Apply(ops, v));

            public override DataResult<A> Parse<U>(DynamicOps<U> ops, U input)
                => _type._delegate.Codec().Parse(ops, _type._preRead.Apply(ops, input));
        }

        //all对元素应用规则后用fix包装
        public override RewriteResult<A, object> All(object rule, bool recurse, bool checkIndex)
            => Fix(this, _delegate.RewriteOrNop(rule));

        //one对元素应用规则后用fix包装
        public override Optional<RewriteResult<A, object>> One(object rule)
        {
            var view = ((TypeRewriteRule)rule).Rewrite(_delegate);
            if (!view.IsPresent) return Optional<RewriteResult<A, object>>.Empty();
            return Optional<RewriteResult<A, object>>.Of(Fix(this, (RewriteResult<A, object>)view.Get()));
        }

        //findFieldTypeOpt委托给被包装元素
        public override Optional<T.Type<object>> FindFieldTypeOpt(string name)
            => _delegate.FindFieldTypeOpt(name);

        public override T.Type<object> UpdateMu(RecursiveTypeFamily newFamily)
            => (T.Type<object>)(object)DSL.Hook(_delegate.UpdateMu(newFamily), _preRead, _postWrite);

        public override TypeTemplate BuildTemplate()
            => DSL.Hook(_delegate.Template(), _preRead, _postWrite);

        //fix元素重写结果为nop时返回nop否则用adapter投射并castOuter为Hook层
        public static RewriteResult<A, object> Fix<A2>(HookType<A2> type, RewriteResult<A2, object> instance)
        {
            if (instance.View().IsNop())
            {
                return (RewriteResult<A, object>)(object)RewriteResult<A2, object>.Nop(type);
            }
            return (RewriteResult<A, object>)(object)T.Type<A2>.OpticView(type, (RewriteResult<object, object>)(object)instance,
                (TypedOptic<A2, object, object, object>)(object)TypedOptics.Adapter<A2, object>(instance.View().Type()!, instance.View().NewType()!)!);
        }

        public override bool Equals(object? o, bool ignoreRecursionPoints, bool checkIndex)
        {
            if (o is not HookType<A> type) return false;
            return _delegate.Equals(type._delegate, ignoreRecursionPoints, checkIndex)
                && Equals(_preRead, type._preRead)
                && Equals(_postWrite, type._postWrite);
        }

        public override int GetHashCode()
            => unchecked((_delegate?.GetHashCode() ?? 0) * 31 * 31
                + (_preRead?.GetHashCode() ?? 0) * 31
                + (_postWrite?.GetHashCode() ?? 0));
    }
}
