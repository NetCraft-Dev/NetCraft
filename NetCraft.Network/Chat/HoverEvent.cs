namespace NetCraft.Network.Chat;

//悬停事件对应原版net.minecraft.network.chat.HoverEvent
//文本被悬停时显示的额外内容如文本/物品/实体信息
//简化版仅保留ShowText ShowItem/ShowEntity依赖ItemStackTemplate/EntityTooltipInfo业务类型待后续补全
public interface HoverEvent
{
    public Action EventAction { get; }

    //动作枚举对应原版HoverEvent.Action
    public enum Action
    {
        ShowText,
        ShowItem,
        ShowEntity,
    }

    //显示文本对应原版HoverEvent.ShowText
    public sealed record ShowText(Component Value) : HoverEvent
    {
        public Action EventAction => Action.ShowText;
    }

    //显示物品对应原版HoverEvent.ShowItem依赖ItemStackTemplate业务类型占位待补全
    public sealed record ShowItem(object Item) : HoverEvent
    {
        public Action EventAction => Action.ShowItem;
    }

    //显示实体对应原版HoverEvent.ShowEntity依赖EntityTooltipInfo业务类型占位待补全
    public sealed record ShowEntity(object Entity) : HoverEvent
    {
        public Action EventAction => Action.ShowEntity;
    }
}
