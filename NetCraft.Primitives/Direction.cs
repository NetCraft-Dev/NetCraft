namespace NetCraft.Primitives;

//方向枚举对应原版net.minecraft.core.Direction
//原版有16个方向这里只取6个基础方向加DOWN/UP等枚举值
//持StepX/StepY/StepZ偏移量与Axis轴标记
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

    //两个逐轴推进顺序 顺序固定不能改 原版也是两个常量复用
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

    //ById按id取方向
    public static Direction ById(int id)
    {
        return Values[((id % Values.Length) + Values.Length) % Values.Length];
    }

    //fromYRot按水平朝向角度取方向 对应原版 Direction.fromYRot
    //0 是 SOUTH 90 是 WEST 180 是 NORTH 270 是 EAST 与原版一致
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

    //FromViewVector 按视线向量取最贴近的方向 六个方向都参与 对应原版 Direction.getNearest
    //观察者这类要含上下的方块靠它算朝向 水平版 FromYRot 不适用于它们
    //初值取最小正数 视线为零向量时六个点积都是 0 谁也不替换 落回 NORTH 与原版一致
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

    //byAxisDirection按轴方向取该轴正负方向
    public static Direction ByAxisDirection(Axis axis, AxisDirection dir)
    {
        foreach (var d in Values)
        {
            if (d.AxisValue == axis && d.AxisDir == dir) return d;
        }
        return Down;
    }

    //AxisStepOrder 逐轴裁剪时的推进顺序 对应原版 Direction.axisStepOrder
    //竖直优先 水平两轴按位移大的排前面 先解位移大的轴结果才与原版逐个对齐
    public static Axis[] AxisStepOrder(Vec3 movement)
        => Math.Abs(movement.X) < Math.Abs(movement.Z) ? YzxAxisOrder : YxzAxisOrder;

    //opposite取反方向
    public Direction Opposite => ById(OppositeId);

    //counterClockWise逆时针旋转一次 对应原版 getCounterClockWise
    //只对水平方向有定义 竖直方向原版抛异常 这里退回自身
    public Direction CounterClockWise => Id3D switch
    {
        NorthId => West,
        WestId => South,
        SouthId => East,
        EastId => North,
        _ => this,
    };

    //clockWise顺时针旋转一次 对应原版 getClockWise 只对水平方向有定义
    public Direction ClockWise => Id3D switch
    {
        NorthId => East,
        EastId => South,
        SouthId => West,
        WestId => North,
        _ => this,
    };

    public bool IsHorizontal => AxisValue == Axis.X || AxisValue == Axis.Z;

    //getAxis返回Axis
    public Axis GetAxis() => AxisValue;

    //getStep按轴取步长
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

//DirectionAxisExtensions 轴取值辅助 对应原版 Direction.Axis.choose
//离散网格按轴遍历时要用它把三轴循环写成一套代码
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
