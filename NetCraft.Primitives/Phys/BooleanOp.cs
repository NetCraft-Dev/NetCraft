namespace NetCraft.Primitives.Phys;

//BooleanOp 布尔运算谓词 对应原版 BooleanOp 函数式接口
//形状做并集交集时逐格子调用它 决定结果格是否实心
public delegate bool BooleanOp(bool first, bool second);

//BooleanOps 原版 BooleanOp 里的 16 个静态实例 语义逐一对齐
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
