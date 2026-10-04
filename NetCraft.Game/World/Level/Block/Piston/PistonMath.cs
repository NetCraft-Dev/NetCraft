using NetCraft.Primitives;

namespace NetCraft.Game.World.Level.Block.Piston;

//PistonMath 活塞推动的扫掠体积 对应原版 PistonMath
internal static class PistonMath
{
    //GetMovementArea 盒子沿 direction 走 amount 格时扫过的区域 用来找被推到的实体
    public static AABB GetMovementArea(AABB box, Direction direction, double amount)
    {
        var delta = amount * direction.GetStep(direction.GetAxis());
        var min = Math.Min(delta, 0.0);
        var max = Math.Max(delta, 0.0);
        if (direction == Direction.West)
            return new AABB(box.Min.X + min, box.Min.Y, box.Min.Z, box.Min.X + max, box.Max.Y, box.Max.Z);
        if (direction == Direction.East)
            return new AABB(box.Max.X + min, box.Min.Y, box.Min.Z, box.Max.X + max, box.Max.Y, box.Max.Z);
        if (direction == Direction.Down)
            return new AABB(box.Min.X, box.Min.Y + min, box.Min.Z, box.Max.X, box.Min.Y + max, box.Max.Z);
        if (direction == Direction.North)
            return new AABB(box.Min.X, box.Min.Y, box.Min.Z + min, box.Max.X, box.Max.Y, box.Min.Z + max);
        if (direction == Direction.South)
            return new AABB(box.Min.X, box.Min.Y, box.Max.Z + min, box.Max.X, box.Max.Y, box.Max.Z + max);
        //朝上
        return new AABB(box.Min.X, box.Max.Y + min, box.Min.Z, box.Max.X, box.Max.Y + max, box.Max.Z);
    }
}
