using NetCraft.Game.World.Phys.Collision;
using NetCraft.Primitives;
using NetCraft.Storage;
using NetCraft.Util;

namespace NetCraft.Game.Server;

//ServerBlockRaycast server-side block raycast, maps to the COLLIDER path of vanilla Level.clip
//Advances cell by cell along the voxel grid, intersecting the collision shape per cell, taking the nearest hit and giving the exact hit point and entry face
//The client BlockRaycast uses fixed-step sampling that only tests non-air; too imprecise, and targets need strength from the hit point
//Placed in the Game layer because intersection needs Game-side types such as CollisionContext and EmptyBlockGetter
public static class ServerBlockRaycast
{
    //MaxSteps maximum cells on a single ray, capping long runs under extreme parameters
    private const int MaxSteps = 512;

    //LengthEpsilon squared lower bound of the ray length; too short means no ray
    private const double LengthEpsilon = 1e-12;

    //Clip computes the block hit by the ray; returns null when nothing is hit
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

        //tMax how much parametric distance is left to the next cell boundary; tDelta how much each cell adds; the parameter is 1 over the whole ray
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
            //Advance parametrically to the nearest next cell; passing 1 means the ray end is passed
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
