using NetCraft.Game.World.Level.Block;
using NetCraft.Primitives;
using NetCraft.Primitives.Phys;
using NetCraft.Registry.State;
using NetCraft.Storage;

namespace NetCraft.Game.World.Phys.Collision;

//LevelCollisionGetter a view reading the server level with the collision query interface
//Vanilla Level itself implements CollisionGetter; here the level lives in the Storage layer and cannot do shapes, so a separate adapter layer is added
//ServerLevel only reads blocks by BlockPos here, so the BlockGetter three-coordinate version and the height range are added
public sealed class LevelCollisionGetter : CollisionGetter
{
    private readonly ServerLevel _level;
    private readonly int _minSectionY;
    private readonly int _sectionsCount;

    public LevelCollisionGetter(ServerLevel level, int minSectionY, int sectionsCount)
    {
        _level = level;
        _minSectionY = minSectionY;
        _sectionsCount = sectionsCount;
    }

    public int MinSectionY => _minSectionY;

    public int MaxSectionY => _minSectionY + _sectionsCount - 1;

    public int SectionsCount => _sectionsCount;

    //Unloaded chunks and sections are treated as air, consistent with vanilla BlockGetter; otherwise collisions would gain blocks out of nowhere
    public BlockState GetBlockState(int x, int y, int z)
        => _level.GetBlockState(new BlockPos(x, y, z)) ?? Blocks.AIR.DefaultBlockState;

    //IsUnobstructed whether placing a shape into the level does not crush entities, maps to vanilla EntityGetter.isUnobstructed
    //Only entities that block building count; drops and the like do not, and removed ones do not
    //Players are not in the entity manager, so the player bounding box injected by the Game layer is counted separately
    public bool IsUnobstructed(NetCraft.Registry.Entity? source, VoxelShape shape)
    {
        if (shape.IsEmpty) return true;
        foreach (var entity in _level.EntitiesInBox(shape.Bounds()))
        {
            if (entity.IsRemoved || !entity.BlocksBuilding || ReferenceEquals(entity, source)) continue;
            if (Shapes.JoinIsNotEmpty(shape, Shapes.Create(entity.BoundingBox), BooleanOps.And)) return false;
        }

        if (_level.ExtraEntityBoxes is null) return true;
        foreach (var playerBox in _level.ExtraEntityBoxes())
            if (Shapes.JoinIsNotEmpty(shape, Shapes.Create(playerBox), BooleanOps.And)) return false;
        return true;
    }
}
