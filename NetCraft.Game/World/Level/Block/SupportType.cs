using NetCraft.Game.World.Phys.Collision;
using NetCraft.Primitives;
using NetCraft.Primitives.Phys;
using NetCraft.Registry.State;
using NetCraft.Storage;

namespace NetCraft.Game.World.Level.Block;

//SupportType support-face strength, maps to vanilla net.minecraft.world.level.block.SupportType
//FULL the whole face must be supported; CENTER only the center column must be; RIGID the surrounding border must be
//The namespace segment Block clashes with the Registry.Block type, so block classes are always referenced by their fully qualified name here
public enum SupportType
{
    Full,
    Center,
    Rigid,
}

//SupportTypeExtensions checks for the three strengths, maps to isSupporting on each vanilla enum constant
public static class SupportTypeExtensions
{
    //CENTER_SUPPORT_SHAPE center column 2/16 square, up to 10/16 tall
    private static readonly VoxelShape CenterSupportShape = NetCraft.Registry.Block.Column(2.0, 0.0, 10.0);

    //RIGID_SUPPORT_SHAPE a full block with the center 12/16 column carved out, leaving the surrounding border
    private static readonly VoxelShape RigidSupportShape = Shapes.Join(
        Shapes.Block(), NetCraft.Registry.Block.Column(12.0, 0.0, 16.0), BooleanOps.OnlyFirst);

    //IsSupporting whether this face counts as supported for the given strength, maps to vanilla isSupporting
    public static bool IsSupporting(this SupportType supportType, BlockState state, BlockGetter level, BlockPos pos,
        Direction direction)
    {
        var face = state.GetBlockSupportShape(level, pos).GetFaceShape(direction);
        return supportType switch
        {
            SupportType.Full => NetCraft.Registry.Block.IsShapeFullBlock(face),
            SupportType.Center => !Shapes.JoinIsNotEmpty(face, CenterSupportShape, BooleanOps.OnlySecond),
            _ => !Shapes.JoinIsNotEmpty(face, RigidSupportShape, BooleanOps.OnlySecond),
        };
    }
}
