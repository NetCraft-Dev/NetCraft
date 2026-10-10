using NetCraft.Primitives;
using NetCraft.Registry.Context;
using NetCraft.Registry.State;

namespace NetCraft.Game.World.Loot;

//LootContextParams the context keys loot data reads, maps to vanilla LootContextParams
//The entity type is fully qualified because this namespace's parent owns an Entity segment
public static class LootContextParams
{
    public static readonly ContextKey<NetCraft.Registry.Entity> ThisEntity =
        ContextKey<NetCraft.Registry.Entity>.Vanilla("this_entity");

    public static readonly ContextKey<NetCraft.Registry.Entity> InteractingEntity =
        ContextKey<NetCraft.Registry.Entity>.Vanilla("interacting_entity");

    public static readonly ContextKey<NetCraft.Registry.Entity> TargetEntity =
        ContextKey<NetCraft.Registry.Entity>.Vanilla("target_entity");

    public static readonly ContextKey<NetCraft.Registry.Entity> AttackingEntity =
        ContextKey<NetCraft.Registry.Entity>.Vanilla("attacking_entity");

    public static readonly ContextKey<NetCraft.Registry.Entity> DirectAttackingEntity =
        ContextKey<NetCraft.Registry.Entity>.Vanilla("direct_attacking_entity");

    public static readonly ContextKey<NetCraft.Game.World.Entity.Player> LastDamagePlayer =
        ContextKey<NetCraft.Game.World.Entity.Player>.Vanilla("last_damage_player");

    public static readonly ContextKey<NetCraft.Game.World.Damage.DamageSource> DamageSource =
        ContextKey<NetCraft.Game.World.Damage.DamageSource>.Vanilla("damage_source");

    public static readonly ContextKey<Vec3> Origin = ContextKey<Vec3>.Vanilla("origin");

    public static readonly ContextKey<BlockState> BlockState = ContextKey<BlockState>.Vanilla("block_state");

    public static readonly ContextKey<NetCraft.Game.World.Level.Block.BlockEntity> BlockEntity =
        ContextKey<NetCraft.Game.World.Level.Block.BlockEntity>.Vanilla("block_entity");

    public static readonly ContextKey<NetCraft.Game.World.Items.ItemStack> Tool =
        ContextKey<NetCraft.Game.World.Items.ItemStack>.Vanilla("tool");

    public static readonly ContextKey<float> ExplosionRadius = ContextKey<float>.Vanilla("explosion_radius");
}
