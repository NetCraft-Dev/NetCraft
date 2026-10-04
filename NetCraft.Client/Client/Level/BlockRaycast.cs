using NetCraft.Game.World.Level.Block;
using NetCraft.Primitives;

namespace NetCraft.Game.Client.Level;

//BlockRaycast 客户端视线拾取对应原版 Entity.pick 的体素遍历
//从眼睛位置沿 yaw/pitch 决定的视线方向按固定步长推进 命中第一个非空气方块即返回
public static class BlockRaycast
{
    //Step 采样步长 0.1 格 拾取距离内最多几十次方块查询
    private const double Step = 0.1;

    //Reach 最大拾取距离 对应原版方块交互距离
    public const double Reach = 4.5;

    //TryPick 找视线上的第一个非空气方块 命中返回方块坐标与进入面
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

    //EnterFace 由进入方向反推被击中的面 视线朝 +X 进入说明打的是方块的西面
    private static Direction EnterFace(int fromX, int fromY, int fromZ, int toX, int toY, int toZ)
    {
        if (toX != fromX) return toX > fromX ? Direction.West : Direction.East;
        if (toY != fromY) return toY > fromY ? Direction.Down : Direction.Up;
        return toZ > fromZ ? Direction.North : Direction.South;
    }
}
