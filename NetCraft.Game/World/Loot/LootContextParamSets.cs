using NetCraft.Codec;
using NetCraft.Registry;
using NetCraft.Registry.Codec;
using NetCraft.Registry.Context;

namespace NetCraft.Game.World.Loot;

//LootContextParamSets the named context key sets loot tables declare, maps to vanilla LootContextParamSets
//Vanilla keeps a bimap keyed by identifier so the "type" field of a loot table resolves back to a set
public static class LootContextParamSets
{
    private static readonly Dictionary<Identifier, ContextKeySet> Registry = new();

    //CODEC resolves a parameter set by identifier, maps to vanilla LootContextParamSets.CODEC
    public static readonly Codec<ContextKeySet> CODEC = new ParamSetCodec();

    public static readonly ContextKeySet Empty = Register("empty", _ => { });

    public static readonly ContextKeySet Block = Register("block", b => b
        .Required(LootContextParams.BlockState)
        .Required(LootContextParams.Origin)
        .Required(LootContextParams.Tool)
        .Optional(LootContextParams.ThisEntity)
        .Optional(LootContextParams.BlockEntity)
        .Optional(LootContextParams.ExplosionRadius));

    public static readonly ContextKeySet Entity = Register("entity", b => b
        .Required(LootContextParams.ThisEntity)
        .Required(LootContextParams.Origin)
        .Required(LootContextParams.DamageSource)
        .Optional(LootContextParams.AttackingEntity)
        .Optional(LootContextParams.DirectAttackingEntity)
        .Optional(LootContextParams.LastDamagePlayer));

    public static readonly ContextKeySet AllParams = Register("generic", b => b
        .Required(LootContextParams.ThisEntity)
        .Required(LootContextParams.LastDamagePlayer)
        .Required(LootContextParams.DamageSource)
        .Required(LootContextParams.AttackingEntity)
        .Required(LootContextParams.DirectAttackingEntity)
        .Required(LootContextParams.Origin)
        .Required(LootContextParams.BlockState)
        .Required(LootContextParams.BlockEntity)
        .Required(LootContextParams.Tool)
        .Required(LootContextParams.ExplosionRadius));

    //Get resolves a parameter set by id, the reverse of the lookup the "type" field needs
    public static ContextKeySet? Get(Identifier id) => Registry.GetValueOrDefault(id);

    private static ContextKeySet Register(string name, Action<ContextKeySet.Builder> configure)
    {
        var builder = new ContextKeySet.Builder();
        configure(builder);
        var result = builder.Build();
        Registry[Identifier.WithDefaultNamespace(name)] = result;
        return result;
    }
}

//ParamSetCodec reads a parameter set identifier; vanilla reverses the bimap to encode, which NC does not need yet
internal sealed class ParamSetCodec : ScalarCodec<ContextKeySet>
{
    public override DataResult<ContextKeySet> Parse<U>(DynamicOps<U> ops, U input)
        => IdentifierCodec.Instance.Parse(ops, input).FlatMap(id =>
        {
            var set = LootContextParamSets.Get(id);
            return set is null
                ? DataResult<ContextKeySet>.Error(() => $"No parameter set exists with id: '{id}'")
                : DataResult<ContextKeySet>.Success(set);
        });

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, ContextKeySet value)
        => DataResult<U>.Error(() => "Parameter sets are not encodable");
}
