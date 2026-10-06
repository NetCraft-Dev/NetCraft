using NetCraft.Game.World.Level.Block;
using NetCraft.Primitives;

namespace NetCraft.Game.Client.Level;

//BlockRaycast client look picking, maps to vanilla Entity.pick's voxel traversal
//Steps from the eye position along the view direction determined by yaw/pitch at a fixed step, returning on the first non-air block hit
public static class BlockRaycast
{
    //Step sampling step 0.1 blocks; within reach this is at most a few dozen block queries
    private const double Step = 0.1;

    //Reach max pick distance, maps to vanilla block interaction distance
    public const double Reach = 4.5;

    //TryPick finds the first non-air block along the view; on hit returns the block position and entry face
    public static bool TryPick(ClientLevel level, Vec3 origin, float yaw, float pitch,
        out BlockPos pos, out Direction face)
    {
        pos = BlockPos.Zero;
        face = Direction.Up;
        const double toRadians = Math.PI / 180.0;
        var yawRad = yaw * toRadians;
        var pitchRad = pitch * toRadians;
        var dirX = -Math.Sin(yawRad) * Math.Cos(pitchRad);
        var dirY = -Math.Sin(pitchRad);
        var dirZ = Math.Cos(yawRad) * Math.Cos(pitchRad);

        var fromX = (int)Math.Floor(origin.X);
        var fromY = (int)Math.Floor(origin.Y);
        var fromZ = (int)Math.Floor(origin.Z);
        level.EnterReadLock();
        try
        {
            for (var t = Step; t <= Reach; t += Step)
            {
                var x = (int)Math.Floor(origin.X + dirX * t);
                var y = (int)Math.Floor(origin.Y + dirY * t);
                var z = (int)Math.Floor(origin.Z + dirZ * t);
                if (x == fromX && y == fromY && z == fromZ) continue;
                var current = new BlockPos(x, y, z);
                var state = level.GetBlockState(current);
                if (ReferenceEquals(state.Owner, Blocks.AIR))
                {
                    fromX = x;
                    fromY = y;
                    fromZ = z;
                    continue;
                }
                pos = current;
                face = EnterFace(fromX, fromY, fromZ, x, y, z);
                return true;
            }
        }
        finally
        {
            level.ExitReadLock();
        }
        return false;
    }

    //EnterFace infers the hit face from the entry direction; entering toward +X means the block's west face was hit
    private static Direction EnterFace(int fromX, int fromY, int fromZ, int toX, int toY, int toZ)
    {
        if (toX != fromX) return toX > fromX ? Direction.West : Direction.East;
        if (toY != fromY) return toY > fromY ? Direction.Down : Direction.Up;
        return toZ > fromZ ? Direction.North : Direction.South;
    }
}
