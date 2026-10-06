using NetCraft.Primitives;
using NetCraft.Registry.State;
using NetCraft.Storage;

namespace NetCraft.Game.World.Level.Block.Dispenser;

//BlockSource dispenser context, maps to vanilla net.minecraft.core.dispenser.BlockSource
//Everything a dispense behavior needs is here: level, position, state, block entity
public readonly record struct BlockSource(
    ServerLevel Level,
    BlockPos Pos,
    BlockState State,
    DispenserBlockEntity BlockEntity)
{
    //Center block center, maps to vanilla Vec3.atCenterOf
    public Vec3 Center => new(Pos.X + 0.5, Pos.Y + 0.5, Pos.Z + 0.5);
}
