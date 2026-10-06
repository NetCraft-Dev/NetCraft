namespace NetCraft.Network.Chat;

//Common component constants, maps to vanilla net.minecraft.network.chat.CommonComponents
//Provides predefined constants for common UI components such as confirm/cancel/newline
//The simplified form only contains basic constants; the full form depends on the Component.translatable factory method
public static class CommonComponents
{
    //Empty component, maps to vanilla EMPTY
    public static readonly Component Empty = Component.Empty();

    //Newline component, maps to vanilla NEW_LINE
    public static readonly Component NewLine = Component.Literal("\n");

    //Ellipsis, maps to vanilla ELLIPSIS
    public static readonly Component Ellipsis = Component.Literal("...");

    //Space, maps to vanilla SPACE
    public static readonly Component Space = Component.Literal(" ");

    //Narration separator, maps to vanilla NARRATION_SEPARATOR
    public static readonly Component NarrationSeparator = Component.Literal(". ");
}
