using NetCraft.Primitives;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen.Structure;

//PoolAliasLookup 池别名查找表 对应原版 net.minecraft.world.level.levelgen.structure.pools.alias.PoolAliasLookup
//一次生成解出一份固定的别名映射 之后每查一个池名都走它
public sealed class PoolAliasLookup
{
    //Empty 不做任何替换的查找表 对应原版 EMPTY
    public static readonly PoolAliasLookup Empty = new(new Dictionary<Identifier, Identifier>());

    private readonly IReadOnlyDictionary<Identifier, Identifier> _mappings;

    private PoolAliasLookup(IReadOnlyDictionary<Identifier, Identifier> mappings) => _mappings = mappings;

    //Create 按种子与生成点解出一份别名映射 对应原版 create
    //随机源按种子派生再定位到生成点 同一生成点重复生成解出的映射一致
    public static PoolAliasLookup Create(IReadOnlyList<PoolAliasBinding> bindings, BlockPos pos, long seed)
    {
        if (bindings.Count == 0) return Empty;
        var random = RandomSource.Create(seed).ForkPositional().At(pos.X, pos.Y, pos.Z);
        var mappings = new Dictionary<Identifier, Identifier>();
        foreach (var binding in bindings)
            binding.ForEachResolved(random, (alias, target) => mappings[alias] = target);
        return new PoolAliasLookup(mappings);
    }

    //Lookup 查别名对应的池名 没有别名就用原池名
    public Identifier Lookup(Identifier poolId)
        => _mappings.TryGetValue(poolId, out var target) ? target : poolId;
}
