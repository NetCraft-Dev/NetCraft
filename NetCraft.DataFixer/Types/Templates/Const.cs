namespace NetCraft.DataFixer.Types.Templates;

using System;
using System.Collections.Generic;
using NetCraft.Codec;
using NetCraft.DataFixer;
using NetCraft.DataFixer.Kinds;
using T = NetCraft.DataFixer.Types;
using NetCraft.DataFixer.Types.Families;
using NetCraft.DataFixer.Util;

//Const常量模板对应原版com.mojang.datafixers.types.templates.Const
//所有index都返回同一类型size为0
public sealed record Const(T.Type<object> Type) : TypeTemplate
{
    public int Size() => 0;

    //apply忽略family所有index返回同一类型
    public TypeFamily Apply(TypeFamily family)
        => new ConstFamily(Type);

    //applyO类型匹配aType时返回id否则构造忽略optic
    public FamilyOptic<object, object> ApplyO<A, B>(FamilyOptic<A, B> input, T.Type<A> aType, T.Type<B> bType)
    {
        if (Equals(Type, (T.Type<object>)(object)aType!))
        {
            return TypeFamily.FamilyOptic<object, object>(i => (TypedOptic<object, object, object, object>)(object)MakeIdOptic(aType, bType));
        }
        var ignoreOptic = MakeIgnoreOptic(Type, aType, bType);
        return TypeFamily.FamilyOptic<object, object>(i => (TypedOptic<object, object, object, object>)(object)ignoreOptic);
    }

    //MakeIdOptic类型匹配时用Profunctor.id作为恒等optic
    private static TypedOptic<object, object, A, B> MakeIdOptic<A, B>(T.Type<A> aType, T.Type<B> bType)
        => new TypedOptic<object, object, A, B>(
            new HashSet<object> { TypeClassesMarker.ProfunctorToken },
            (T.Type<object>)(object)aType!,
            (T.Type<object>)(object)bType!,
            aType,
            bType,
            Optics.Optics.Id<object, object>()!);

    //MakeIgnoreOptic类型不匹配时用Affine构造忽略焦点返回原值
    private static TypedOptic<TT, TT, A, B> MakeIgnoreOptic<TT, A, B>(T.Type<TT> type, T.Type<A> aType, T.Type<B> bType)
        => new TypedOptic<TT, TT, A, B>(
            TypeClassesMarker.AffinePToken,
            type,
            type,
            aType,
            bType,
            Optics.Optics.Affine<TT, TT, A, B>(t => Either<TT, A>.Left(t), (b, t) => t));

    //findFieldOrType委托给DSL.fieldFinder在Const.type中查找
    public Either<TypeTemplate, T.Type<object>.FieldNotFoundException> FindFieldOrType<A, B>(
        int index, string? name, T.Type<A> type, T.Type<B> resultType)
    {
        var finder = DSL.FieldFinder<A>(name, type);
        var either = finder.FindType(Type, resultType, false);
        if (either.IsLeft)
            {
                return Either<TypeTemplate, T.Type<object>.FieldNotFoundException>
                    .Left(new Const((T.Type<object>)(object)((TypedOptic<object, object, object, object>)(object)either.GetLeft().Get()).TType()!));
            }
        return Either<TypeTemplate, T.Type<object>.FieldNotFoundException>.Right(either.GetRight().Get());
    }

    //hmap忽略function返回nop对齐原版Const.hmap
    //Const是叶节点无递归子结构原版直接i->RewriteResult.nop(type)
    public Func<int, RewriteResult<object, object>> Hmap(TypeFamily family, Func<int, RewriteResult<object, object>> function)
        => _ => RewriteResult<object, object>.Nop(Type);

    public override string ToString() => "Const[" + Type + "]";

    //ConstFamily固定类型家族所有index返回同一类型
    private sealed class ConstFamily : TypeFamily
    {
        private readonly T.Type<object> _type;
        public ConstFamily(T.Type<object> type) => _type = type;
        public T.Type<object> Apply(int index) => _type;
    }

    //PrimitiveType原始类型用Codec直接编解码不做模板展开
    public sealed class PrimitiveType<A> : T.Type<A>
    {
        private readonly Codec<A> _codec;

        public PrimitiveType(Codec<A> codec)
        {
            _codec = codec;
        }

        public override bool Equals(object? o, bool ignoreRecursionPoints, bool checkIndex)
            => ReferenceEquals(this, o);

        public override TypeTemplate BuildTemplate()
            => DSL.ConstType(this);

        protected override Codec<A> BuildCodec() => _codec;

        public override string ToString() => _codec?.ToString() ?? "PrimitiveType";
    }
}

//TypeClassesMarker集中存放 optic proof token避免 Templates 反向依赖 Kinds 子包
internal static class TypeClassesMarker
{
    //ProfunctorToken对应原版 Profunctor.Mu.TYPE_TOKEN用typeof(IProfunctorMu)反映接口继承
    public static readonly object ProfunctorToken = typeof(IProfunctorMu);
    //AffinePToken对应原版 AffineP.Mu.TYPE_TOKEN用typeof(IAffinePMu)反映接口继承
    public static readonly object AffinePToken = typeof(IAffinePMu);
    //CartesianToken对应原版 Cartesian.Mu.TYPE_TOKEN用typeof(ICartesianMu)反映接口继承
    public static readonly object CartesianToken = typeof(ICartesianMu);
    //TraversalPToken对应原版 TraversalP.Mu.TYPE_TOKEN用typeof(ITraversalPMu)反映接口继承
    public static readonly object TraversalPToken = typeof(ITraversalPMu);
}
