namespace NetCraft.Storage;

//BorderStatus 边界变化状态对应原版 net.minecraft.world.level.border.BorderStatus
//客户端按状态取边界墙颜色 颜色值与原版一致
public enum BorderStatus
{
    Growing,
    Shrinking,
    Stationary,
}

//BorderStatus 扩展提供状态色 对应原版 getColor
public static class BorderStatusExtensions
{
    //GetColor 边界墙颜色 0x40FF00 生长 0xFF3000 收缩 0x20A0FF 静止
    public static int GetColor(this BorderStatus status) => status switch
    {
        BorderStatus.Growing => 4259712,
        BorderStatus.Shrinking => 16724016,
        _ => 2138367,
    };
}
