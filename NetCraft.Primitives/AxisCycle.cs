namespace NetCraft.Primitives;

//AxisCycle axis rotation, maps to vanilla AxisCycle
//Discrete grids and boolean operations need all three axes written in a unified form for iteration, this rotates coordinates among X/Y/Z
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

    //Values order must be None, Forward, Backward to match the enum order, Between indexes by ordinal
    public static readonly AxisCycle[] Values = { None, Forward, Backward };

    //Cycle returns the integer component after rotation by axis, maps to vanilla cycle(int,int,int,Axis)
    public abstract int Cycle(int x, int y, int z, Direction.Axis axis);

    //Cycle floating point version, maps to vanilla cycle(double,double,double,Axis)
    public abstract double Cycle(double x, double y, double z, Direction.Axis axis);

    //Cycle rotation of the axis itself, maps to vanilla cycle(Axis)
    public abstract Direction.Axis Cycle(Direction.Axis axis);

    //Inverse the inverse rotation, maps to vanilla inverse
    public abstract AxisCycle Inverse { get; }

    //Between the rotation that maps the from axis onto the to axis, maps to vanilla AxisCycle.between
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
