namespace NetCraft.Network.Chat;

//Hover event, maps to vanilla net.minecraft.network.chat.HoverEvent
//Extra content shown when the text is hovered, such as text/item/entity info
//The simplified form only keeps ShowText; ShowItem/ShowEntity depend on the ItemStackTemplate/EntityTooltipInfo business types and are completed later
public interface HoverEvent
{
    public Action EventAction { get; }

    //The action enum, maps to vanilla HoverEvent.Action
    public enum Action
    {
        ShowText,
        ShowItem,
        ShowEntity,
    }

    //Show text, maps to vanilla HoverEvent.ShowText
    public sealed record ShowText(Component Value) : HoverEvent
    {
        public Action EventAction => Action.ShowText;
    }

    //Show item, maps to vanilla HoverEvent.ShowItem, a placeholder depending on the ItemStackTemplate business type, to be completed
    public sealed record ShowItem(object Item) : HoverEvent
    {
        public Action EventAction => Action.ShowItem;
    }

    //Show entity, maps to vanilla HoverEvent.ShowEntity, a placeholder depending on the EntityTooltipInfo business type, to be completed
    public sealed record ShowEntity(object Entity) : HoverEvent
    {
        public Action EventAction => Action.ShowEntity;
    }
}
