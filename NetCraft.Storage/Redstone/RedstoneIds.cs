using NetCraft.Registry;

namespace NetCraft.Storage.Redstone;

//RedstoneIds 红石元件里被判定引用到的注册名
//原版靠 Blocks.REDSTONE_BLOCK 与 Blocks.REDSTONE_WIRE 这类静态引用
//NC 的红石方块在 Game 层 Storage 只认识注册名 判定统一走这里
public static class RedstoneIds
{
    //Wire 红石线 取电与锁定判定都要认它
    public static readonly Identifier Wire = Identifier.WithDefaultNamespace("redstone_wire");

    //Block 红石块 控制输入里它恒给 15
    public static readonly Identifier Block = Identifier.WithDefaultNamespace("redstone_block");

    //Repeater 中继器 红石线跟它两个口都对接
    public static readonly Identifier Repeater = Identifier.WithDefaultNamespace("repeater");

    //Observer 观察者 红石线只跟它输出那一边对接 P2-6 才落地
    public static readonly Identifier Observer = Identifier.WithDefaultNamespace("observer");

    //Hopper 漏斗 红石线能架在它上面 与普通支撑方块不同要单独认
    public static readonly Identifier Hopper = Identifier.WithDefaultNamespace("hopper");

    //TrackedNames 排查红石链路时要跟踪的元件 按钮与压力板全族按后缀认
    private static readonly HashSet<string> TrackedNames = new()
    {
        "redstone_wire", "redstone_block", "redstone_torch", "redstone_wall_torch",
        "repeater", "comparator", "lever", "observer", "target", "lightning_rod",
        "tripwire_hook", "tripwire", "daylight_detector",
    };

    //IsRedstoneComponent 该方块是不是链路里的红石元件 只给更新日志做过滤用
    public static bool IsRedstoneComponent(Identifier id)
        => TrackedNames.Contains(id.Path)
           || id.Path.EndsWith("_button")
           || id.Path.EndsWith("_pressure_plate");
}
