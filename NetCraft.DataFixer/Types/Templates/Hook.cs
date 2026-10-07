namespace NetCraft.DataFixer.Types.Templates;

using System;
using NetCraft.Codec;
using NetCraft.DataFixer;
using T = NetCraft.DataFixer.Types;
using NetCraft.DataFixer.Types.Families;
using NetCraft.DataFixer.Util;

//Hook hook template maps to vanilla com.mojang.datafixers.types.templates.Hook
//inserts hook functions before reading and after writing the element type
public sealed record Hook(TypeTemplate Element, Hook.IHookFunction PreRead, Hook.IHookFunction PostWrite) : TypeTemplate
{
    //HookFunction hook function interface; transforms a Dynamic value before read or after write
    public interface IHookFunction
    {
        //Identity identity hook default instance
        public static readonly IHookFunction Identity = new IdentityHookFunction();

        T Apply<T>(DynamicOps<T> ops, T value);
    }

    //IdentityHookFunction identity hook implementation
    private sealed class IdentityHookFunction : IHookFunction
    {
        public T Apply<T>(DynamicOps<T> ops, T value) => value;
    }

    public int Size() => Element.Size();

    //apply wraps the element type with DSL.hook at each index
    public TypeFamily Apply(TypeFamily family)
        => new HookFamily(this, family);

    //applyO directly reuses the element's applyO
    public FamilyOptic<object, object> ApplyO<A, B>(FamilyOptic<A, B> input, T.Type<A> aType, T.Type<B> bType)
        => TypeFamily.FamilyOptic<object, object>(i => Element.ApplyO(input, aType, bType).Apply(i));

    //findFieldOrType delegates directly to the element
    public Either<TypeTemplate, T.Type<object>.FieldNotFoundException> FindFieldOrType<A, B>(
        int index, string? name, T.Type<A> type, T.Type<B> resultType)
        => Element.FindFieldOrType(index, name, type, resultType);

    //hmap applies hmap to the element at each index, then wraps it as a Hook via cap
    public Func<int, RewriteResult<object, object>> Hmap(TypeFamily family, Func<int, RewriteResult<object, object>> function)
        => index =>
        {
            var elementResult = Element.Hmap(family, function)(index);
            return Cap(family, index, elementResult);
        };

    //Cap wraps the element rewrite result into a Hook layer via HookType.fix
    private RewriteResult<object, object> Cap<A>(TypeFamily family, int index, RewriteResult<A, object> elementResult)
        => (RewriteResult<object, object>)(object)HookType<A>.Fix((HookType<A>)(object)Apply(family).Apply(index)!, elementResult);

    public override string ToString() => "Hook[" + Element + ", " + PreRead + ", " + PostWrite + "]";

    //HookFamily returns the child type wrapped with DSL.hook at each index
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

    //HookType hook-wrapped type; invokes the hook during encode/decode
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

        //buildCodec aligns with vanilla: decode transforms via preRead then delegates, encode transforms via postWrite
        protected override Codec<A> BuildCodec()
            => new HookCodec(this);

        //HookCodec hook codec; decode uses preRead, encode uses postWrite
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

        //all applies the rule to the element, then wraps with fix
        public override RewriteResult<A, object> All(object rule, bool recurse, bool checkIndex)
            => Fix(this, _delegate.RewriteOrNop(rule));

        //one applies the rule to the element, then wraps with fix
        public override Optional<RewriteResult<A, object>> One(object rule)
        {
            var view = ((TypeRewriteRule)rule).Rewrite(_delegate);
            if (!view.IsPresent) return Optional<RewriteResult<A, object>>.Empty();
            return Optional<RewriteResult<A, object>>.Of(Fix(this, (RewriteResult<A, object>)view.Get()));
        }

        //findFieldTypeOpt delegates to the wrapped element
        public override Optional<T.Type<object>> FindFieldTypeOpt(string name)
            => _delegate.FindFieldTypeOpt(name);

        public override T.Type<object> UpdateMu(RecursiveTypeFamily newFamily)
            => (T.Type<object>)(object)DSL.Hook(_delegate.UpdateMu(newFamily), _preRead, _postWrite);

        public override TypeTemplate BuildTemplate()
            => DSL.Hook(_delegate.Template(), _preRead, _postWrite);

        //fix returns nop when the element rewrite result is nop; otherwise projects via adapter and castOuter to a Hook layer
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
