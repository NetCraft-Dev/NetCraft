using NetCraft.Game.World.Phys.Collision;
using NetCraft.Primitives;
using NetCraft.Storage;
using NetCraft.Util;

namespace NetCraft.Game.Server;

//ServerBlockRaycast 服务端方块射线 对应原版 Level.clip 的 COLLIDER 那一路
//按体素逐格推进 每格取碰撞形状求交 取最近的命中并给出精确命中点与进入面
//客户端 BlockRaycast 是固定步长采样只判断非空气 精度不够 标靶要按命中点算强度
//放在 Game 层是因为求交要 CollisionContext 与 EmptyBlockGetter 这些 Game 侧类型
public static class ServerBlockRaycast
{
    //MaxSteps 单次射线上限格数 挡住极端参数下的长跑
    private const int MaxSteps = 512;

    //LengthEpsilon 射线长度平方下限 太短直接判为没有射线
    private const double LengthEpsilon = 1e-12;

    //Clip 求射线命中的方块 没有命中返回 null
    public static BlockHitResult? Clip(PersistentServerLevel level, Vec3 from, Vec3 to)
    {
        var dx = to.X - from.X;
        var dy = to.Y - from.Y;
        var dz = to.Z - from.Z;
        if (dx * dx + dy * dy + dz * dz < LengthEpsilon) return null;

        var x = Mth.Floor(from.X);
        var y = Mth.Floor(from.Y);
        var z = Mth.Floor(from.Z);
        var stepX = Math.Sign(dx);
        var stepY = Math.Sign(dy);
        var stepZ = Math.Sign(dz);

        //tMax 到下一个格边界还差多少参数距离 tDelta 每跨一格要加多少 参数以 1 为射线全长
        var tDeltaX = stepX == 0 ? double.PositiveInfinity : 1.0 / Math.Abs(dx);
        var tDeltaY = stepY == 0 ? double.PositiveInfinity : 1.0 / Math.Abs(dy);
        var tDeltaZ = stepZ == 0 ? double.PositiveInfinity : 1.0 / Math.Abs(dz);
        var tMaxX = stepX == 0
            ? double.PositiveInfinity
            : (stepX > 0 ? x + 1 - from.X : from.X - x) / Math.Abs(dx);
        var tMaxY = stepY == 0
            ? double.PositiveInfinity
            : (stepY > 0 ? y + 1 - from.Y : from.Y - y) / Math.Abs(dy);
        var tMaxZ = stepZ == 0
            ? double.PositiveInfinity
            : (stepZ > 0 ? z + 1 - from.Z : from.Z - z) / Math.Abs(dz);

        for (var i = 0; i < MaxSteps; i++)
        {
            var pos = new BlockPos(x, y, z);
            if (level.GetBlockState(pos) is { } state)
            {
                var shape = state.GetCollisionShape(EmptyBlockGetter.Instance, pos, CollisionContext.Empty);
                if (!shape.IsEmpty && shape.Clip(from, to, pos) is { } hit) return hit;
            }
            //沿参数推进到最近的下一格 越过 1 说明已经走出射线终点
            if (tMaxX < tMaxY)
            {
                if (tMaxX < tMaxZ)
                {
                    if (tMaxX > 1.0) break;
                    x += stepX;
                    tMaxX += tDeltaX;
                }
                else
                {
                    if (tMaxZ > 1.0) break;
                    z += stepZ;
                    tMaxZ += tDeltaZ;
                }
            }
            else if (tMaxY < tMaxZ)
            {
                if (tMaxY > 1.0) break;
                y += stepY;
                tMaxY += tDeltaY;
            }
            else
            {
                if (tMaxZ > 1.0) break;
                z += stepZ;
                tMaxZ += tDeltaZ;
            }
        }
        return null;
    }
}
