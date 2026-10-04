namespace NetCraft.Network.Chat;

//公共组件常量对应原版net.minecraft.network.chat.CommonComponents
//提供常用UI组件的预定义常量如确认/取消/换行等
//简化版仅含基础常量完整版依赖Component.translatable工厂方法
public static class CommonComponents
{
    //空组件对应原版EMPTY
    public static readonly Component Empty = Component.Empty();

    //换行符组件对应原版NEW_LINE
    public static readonly Component NewLine = Component.Literal("\n");

    //省略号对应原版ELLIPSIS
    public static readonly Component Ellipsis = Component.Literal("...");

    //空格对应原版SPACE
    public static readonly Component Space = Component.Literal(" ");

    //叙述分隔符对应原版NARRATION_SEPARATOR
    public static readonly Component NarrationSeparator = Component.Literal(". ");
}
