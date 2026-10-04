namespace NetCraft.Primitives;

//AxisCycle 轴轮转 对应原版 AxisCycle
//离散网格与布尔运算要把三轴写成统一形式遍历 靠它把坐标在 X/Y/Z 之间轮转
public abstract class AxisCycle
{
    private static readonly Direction.Axis[] AxisValues =
    {
        Direction.Axis.X,
        Direction.Axis.Y,
        Direction.Axis.Z,
    };

    public static readonly AxisCycle None = new NoneCycle();
    public static readonly AxisCycle Forward = new ForwardCycle();
    public static readonly AxisCycle Backward = new BackwardCycle();

    //Values 顺序必须是 None Forward Backward 与枚举序一致 Between 按下标索引
    public static readonly AxisCycle[] Values = { None, Forward, Backward };

    //Cycle 按轴取轮转后的整数分量 对应原版 cycle(int,int,int,Axis)
    public abstract int Cycle(int x, int y, int z, Direction.Axis axis);

    //Cycle 浮点版本 对应原版 cycle(double,double,double,Axis)
    public abstract double Cycle(double x, double y, double z, Direction.Axis axis);

    //Cycle 轴自身的轮转 对应原版 cycle(Axis)
    public abstract Direction.Axis Cycle(Direction.Axis axis);

    //Inverse 逆轮转 对应原版 inverse
    public abstract AxisCycle Inverse { get; }

    //Between 取把 from 轴转到 to 轴的轮转 对应原版 AxisCycle.between
    public static AxisCycle Between(Direction.Axis from, Direction.Axis to)
        => Values[FloorMod((int)to - (int)from, 3)];

    private static int FloorMod(int value, int modulus) => (value % modulus + modulus) % modulus;

    private sealed class NoneCycle : AxisCycle
    {
        public override int Cycle(int x, int y, int z, Direction.Axis axis) => axis.Choose(x, y, z);

        public override double Cycle(double x, double y, double z, Direction.Axis axis) => axis.Choose(x, y, z);

        public override Direction.Axis Cycle(Direction.Axis axis) => axis;

        public override AxisCycle Inverse => this;
    }

    private sealed class ForwardCycle : AxisCycle
    {
        public override int Cycle(int x, int y, int z, Direction.Axis axis) => axis.Choose(z, x, y);

        public override double Cycle(double x, double y, double z, Direction.Axis axis) => axis.Choose(z, x, y);

        public override Direction.Axis Cycle(Direction.Axis axis) => AxisValues[FloorMod((int)axis + 1, 3)];

        public override AxisCycle Inverse => Backward;
    }

    private sealed class BackwardCycle : AxisCycle
    {
        public override int Cycle(int x, int y, int z, Direction.Axis axis) => axis.Choose(y, z, x);

        public override double Cycle(double x, double y, double z, Direction.Axis axis) => axis.Choose(y, z, x);

        public override Direction.Axis Cycle(Direction.Axis axis) => AxisValues[FloorMod((int)axis - 1, 3)];

        public override AxisCycle Inverse => Forward;
    }
}
