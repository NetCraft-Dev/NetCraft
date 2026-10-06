namespace NetCraft.Primitives.Phys;

//BooleanOp boolean operation predicate, maps to the vanilla BooleanOp functional interface
//Shape union and intersection call it per cell to decide whether a result cell is solid
public delegate bool BooleanOp(bool first, bool second);

//BooleanOps the 16 static instances of vanilla BooleanOp, semantics aligned one by one
public static class BooleanOps
{
    public static readonly BooleanOp False = (_, _) => false;
    public static readonly BooleanOp NotOr = (a, b) => !a && !b;
    public static readonly BooleanOp OnlySecond = (a, b) => b && !a;
    public static readonly BooleanOp NotFirst = (a, _) => !a;
    public static readonly BooleanOp OnlyFirst = (a, b) => a && !b;
    public static readonly BooleanOp NotSecond = (_, b) => !b;
    public static readonly BooleanOp NotSame = (a, b) => a != b;
    public static readonly BooleanOp NotAnd = (a, b) => !a || !b;
    public static readonly BooleanOp And = (a, b) => a && b;
    public static readonly BooleanOp Same = (a, b) => a == b;
    public static readonly BooleanOp Second = (_, b) => b;
    public static readonly BooleanOp Causes = (a, b) => !a || b;
    public static readonly BooleanOp First = (a, _) => a;
    public static readonly BooleanOp CausedBy = (a, b) => a || !b;
    public static readonly BooleanOp Or = (a, b) => a || b;
    public static readonly BooleanOp True = (_, _) => true;
}
