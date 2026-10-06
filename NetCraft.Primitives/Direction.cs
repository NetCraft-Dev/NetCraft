namespace NetCraft.Primitives;

//Direction enum, maps to vanilla net.minecraft.core.Direction
//Vanilla has 16 directions, here only the 6 base directions plus enum values such as DOWN/UP
//Holds the StepX/StepY/StepZ offsets and an Axis marker
public readonly struct Direction : IEquatable<Direction>
{
    public enum Axis
    {
        X,
        Y,
        Z
    }

    public enum AxisDirection
    {
        Positive,
        Negative
    }

    public const int DownId = 0;
    public const int UpId = 1;
    public const int NorthId = 2;
    public const int SouthId = 3;
    public const int WestId = 4;
    public const int EastId = 5;

    public static readonly Direction Down = new(DownId, UpId, AxisDirection.Negative, Axis.Y, 0, -1, 0);
    public static readonly Direction Up = new(UpId, DownId, AxisDirection.Positive, Axis.Y, 0, 1, 0);
    public static readonly Direction North = new(NorthId, SouthId, AxisDirection.Negative, Axis.Z, 0, 0, -1);
    public static readonly Direction South = new(SouthId, NorthId, AxisDirection.Positive, Axis.Z, 0, 0, 1);
    public static readonly Direction West = new(WestId, EastId, AxisDirection.Negative, Axis.X, -1, 0, 0);
    public static readonly Direction East = new(EastId, WestId, AxisDirection.Positive, Axis.X, 1, 0, 0);

    public static readonly Direction[] Values = { Down, Up, North, South, West, East };
    public static readonly Direction[] AllShuffledOrder = { West, East, North, South, Down, Up };

    //Two per-axis step orders, the order is fixed and must not change, vanilla also reuses two constants
    private static readonly Axis[] YxzAxisOrder = { Axis.Y, Axis.X, Axis.Z };
    private static readonly Axis[] YzxAxisOrder = { Axis.Y, Axis.Z, Axis.X };

    public int Id3D { get; }
    public int OppositeId { get; }
    public AxisDirection AxisDir { get; }
    public Axis AxisValue { get; }
    public int StepX { get; }
    public int StepY { get; }
    public int StepZ { get; }

    private Direction(int id3d, int oppositeId, AxisDirection axisDir, Axis axis, int stepX, int stepY, int stepZ)
    {
        Id3D = id3d;
        OppositeId = oppositeId;
        AxisDir = axisDir;
        AxisValue = axis;
        StepX = stepX;
        StepY = stepY;
        StepZ = stepZ;
    }

    //ById returns the direction by id
    public static Direction ById(int id)
    {
        return Values[((id % Values.Length) + Values.Length) % Values.Length];
    }

    //fromYRot returns the direction for a horizontal yaw angle, maps to vanilla Direction.fromYRot
    //0 is SOUTH, 90 is WEST, 180 is NORTH, 270 is EAST, same as vanilla
    public static Direction FromYRot(float yRot)
    {
        var index = (int)MathF.Floor(yRot / 90f + 0.5f) & 3;
        return index switch
        {
            0 => South,
            1 => West,
            2 => North,
            _ => East,
        };
    }

    //FromViewVector returns the closest direction for a view vector, all six directions participate, maps to vanilla Direction.getNearest
    //Blocks such as observers that need up/down use this to compute their facing, the horizontal FromYRot does not apply to them
    //Initial value is the smallest positive number, when the view vector is zero all six dots are 0 and nothing replaces it, falling back to NORTH, same as vanilla
    public static Direction FromViewVector(double x, double y, double z)
    {
        var result = North;
        var best = double.Epsilon;
        foreach (var direction in Values)
        {
            var dot = x * direction.StepX + y * direction.StepY + z * direction.StepZ;
            if (dot <= best) continue;
            best = dot;
            result = direction;
        }
        return result;
    }

    //byAxisDirection returns the positive/negative direction of the axis
    public static Direction ByAxisDirection(Axis axis, AxisDirection dir)
    {
        foreach (var d in Values)
        {
            if (d.AxisValue == axis && d.AxisDir == dir) return d;
        }
        return Down;
    }

    //AxisStepOrder step order when clipping axis by axis, maps to vanilla Direction.axisStepOrder
    //Vertical first, the two horizontal axes ordered with the larger displacement first, solving the larger-displacement axis first aligns the result with vanilla step by step
    public static Axis[] AxisStepOrder(Vec3 movement)
        => Math.Abs(movement.X) < Math.Abs(movement.Z) ? YzxAxisOrder : YxzAxisOrder;

    //opposite reverses the direction
    public Direction Opposite => ById(OppositeId);

    //counterClockWise rotates counter-clockwise once, maps to vanilla getCounterClockWise
    //Only defined for horizontal directions, vanilla throws for vertical ones, here it falls back to itself
    public Direction CounterClockWise => Id3D switch
    {
        NorthId => West,
        WestId => South,
        SouthId => East,
        EastId => North,
        _ => this,
    };

    //clockWise rotates clockwise once, maps to vanilla getClockWise, only defined for horizontal directions
    public Direction ClockWise => Id3D switch
    {
        NorthId => East,
        EastId => South,
        SouthId => West,
        WestId => North,
        _ => this,
    };

    public bool IsHorizontal => AxisValue == Axis.X || AxisValue == Axis.Z;

    //getAxis returns the Axis
    public Axis GetAxis() => AxisValue;

    //getStep returns the step along the axis
    public int GetStep(Axis axis)
    {
        if (axis == Axis.X) return StepX;
        if (axis == Axis.Y) return StepY;
        return StepZ;
    }

    public override int GetHashCode() => Id3D;

    public bool Equals(Direction other) => Id3D == other.Id3D;

    public override bool Equals(object? obj) => obj is Direction d && Equals(d);

    public static bool operator ==(Direction left, Direction right) => left.Equals(right);
    public static bool operator !=(Direction left, Direction right) => !left.Equals(right);

    public override string ToString()
    {
        return AxisValue switch
        {
            Axis.X => AxisDir == AxisDirection.Positive ? "+X" : "-X",
            Axis.Y => AxisDir == AxisDirection.Positive ? "+Y" : "-Y",
            _ => AxisDir == AxisDirection.Positive ? "+Z" : "-Z"
        };
    }
}

//DirectionAxisExtensions axis value helper, maps to vanilla Direction.Axis.choose
//Used when iterating a discrete grid by axis so the three-axis loops share one piece of code
public static class DirectionAxisExtensions
{
    public static int Choose(this Direction.Axis axis, int x, int y, int z) => axis switch
    {
        Direction.Axis.X => x,
        Direction.Axis.Y => y,
        _ => z,
    };

    public static double Choose(this Direction.Axis axis, double x, double y, double z) => axis switch
    {
        Direction.Axis.X => x,
        Direction.Axis.Y => y,
        _ => z,
    };
}
