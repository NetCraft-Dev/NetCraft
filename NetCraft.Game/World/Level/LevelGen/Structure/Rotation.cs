using NetCraft.Primitives;
using NetCraft.Util.Collection;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen.Structure;

//Rotation rotation around the Y axis, maps to vanilla net.minecraft.world.level.block.Rotation
//The four values correspond to 0/90/180/270 degrees; the whole structure template rotates by it
public enum Rotation
{
    None,
    Clockwise90,
    Clockwise180,
    Counterclockwise90,
}

//Mirror mirroring along a horizontal axis, maps to vanilla net.minecraft.world.level.block.Mirror
//LeftRight flips left/right along the X axis swapping east/west; FrontBack flips front/back along the Z axis swapping north/south
public enum Mirror
{
    None,
    LeftRight,
    FrontBack,
}

//StructureTransforms name mapping and direction conversion for rotation and mirror
//The transform is always "mirror then rotate"; reversing the order would make structure orientations diverge from vanilla
public static class StructureTransforms
{
    //Name returns the JSON name; 180 degrees serializes as "180", not clockwise_180
    public static string Name(this Rotation rotation) => rotation switch
    {
        Rotation.Clockwise90 => "clockwise_90",
        Rotation.Clockwise180 => "180",
        Rotation.Counterclockwise90 => "counterclockwise_90",
        _ => "none",
    };

    //Name returns the JSON name
    public static string Name(this Mirror mirror) => mirror switch
    {
        Mirror.LeftRight => "left_right",
        Mirror.FrontBack => "front_back",
        _ => "none",
    };

    //TryParse parses rotation by JSON name, returns null when invalid
    //Also accepts the enum-name form for legacy data and structure block NBT
    public static Rotation? TryParseRotation(string name) => name switch
    {
        "none" => Rotation.None,
        "clockwise_90" => Rotation.Clockwise90,
        "180" or "clockwise_180" or "CLOCKWISE_180" => Rotation.Clockwise180,
        "counterclockwise_90" => Rotation.Counterclockwise90,
        _ => null,
    };

    //TryParse parses mirror by JSON name, returns null when invalid
    public static Mirror? TryParseMirror(string name) => name switch
    {
        "none" => Mirror.None,
        "left_right" => Mirror.LeftRight,
        "front_back" => Mirror.FrontBack,
        _ => null,
    };

    //GetRandomRotation picks one of the four rotations at random, maps to vanilla Rotation.getRandom
    public static Rotation GetRandomRotation(RandomSource random) => (Rotation)random.NextInt(4);

    //GetShuffledRotations shuffled copy of the four rotations, maps to vanilla Rotation.getShuffled
    //Jigsaw assembly tries rotations on each candidate element in order, and the order is also decided by the random source
    public static Rotation[] GetShuffledRotations(RandomSource random)
    {
        var rotations = new[]
        {
            Rotation.None,
            Rotation.Clockwise90,
            Rotation.Clockwise180,
            Rotation.Counterclockwise90,
        };
        RandomCollections.Shuffle(rotations, random);
        return rotations;
    }

    //Rotate rotates a world direction, maps to vanilla Rotation.rotate
    //Vertical directions do not rotate
    public static Direction Rotate(this Rotation rotation, Direction direction)
    {
        if (direction.GetAxis() == Direction.Axis.Y) return direction;
        return rotation switch
        {
            Rotation.Clockwise90 => direction.ClockWise,
            Rotation.Clockwise180 => direction.Opposite,
            Rotation.Counterclockwise90 => direction.CounterClockWise,
            _ => direction,
        };
    }

    //MirrorDirection mirrors a world direction, maps to vanilla Mirror.mirror
    //FrontBack is INVERT_X flipping only east/west; LeftRight is INVERT_Z flipping only north/south
    public static Direction MirrorDirection(this Mirror mirror, Direction direction)
    {
        if (mirror == Mirror.FrontBack && direction.GetAxis() == Direction.Axis.X) return direction.Opposite;
        if (mirror == Mirror.LeftRight && direction.GetAxis() == Direction.Axis.Z) return direction.Opposite;
        return direction;
    }

    //GetRotation which rotation a mirror is equivalent to, maps to vanilla Mirror.getRotation
    //Mirroring along Z with a north/south direction equals a 180-degree turn; likewise mirroring along X with an east/west direction
    public static Rotation GetRotation(this Mirror mirror, Direction direction)
    {
        var axis = direction.GetAxis();
        return (mirror == Mirror.LeftRight && axis == Direction.Axis.Z)
            || (mirror == Mirror.FrontBack && axis == Direction.Axis.X)
            ? Rotation.Clockwise180
            : Rotation.None;
    }

    //RotateSteps converts a 0..steps step by rotation, maps to vanilla Rotation.rotate(int,int)
    //steps is the full-circle step count: 4 for facing properties, 16 for sixteenth-circle properties
    public static int RotateSteps(this Rotation rotation, int current, int steps)
    {
        var total = rotation switch
        {
            Rotation.Clockwise90 => current + steps / 4,
            Rotation.Clockwise180 => current + steps / 2,
            Rotation.Counterclockwise90 => current + steps * 3 / 4,
            _ => current,
        };
        return Mod(total, steps);
    }

    //MirrorSteps converts a 0..steps step by mirror, maps to vanilla Mirror.mirror(int,int)
    //First folds back steps past half circle, then complements by the mirror direction
    public static int MirrorSteps(this Mirror mirror, int current, int steps)
    {
        var halfSteps = steps / 2;
        var corrected = current > halfSteps ? current - steps : current;
        var result = mirror switch
        {
            Mirror.FrontBack => steps - corrected,
            Mirror.LeftRight => halfSteps - corrected + steps,
            _ => current,
        };
        return Mod(result, steps);
    }

    //Mod returns a non-negative remainder; Java's % can go negative, so this normalizes into 0..m-1
    public static int Mod(int value, int m)
    {
        var r = value % m;
        return r < 0 ? r + m : r;
    }
}
