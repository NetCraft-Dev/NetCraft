namespace NetCraft.Primitives;

//BlockHitResult block hit result, maps to vanilla net.minecraft.world.phys.BlockHitResult
//Records the hit block position, the hit face, the exact position of the hit point within the block, whether it hit from inside the block, and whether it hit the world border
//WorldBorderHit is the last boolean in the 26.2 network serialization, without it the field right after it shifts by one overall
public readonly record struct BlockHitResult(BlockPos BlockPos, Direction Direction, Vec3 Location, bool Inside,
    bool WorldBorderHit = false);
