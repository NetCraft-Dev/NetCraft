namespace NetCraft.Storage;

//BorderStatus, border change state, maps to vanilla net.minecraft.world.level.border.BorderStatus
//The client picks the border wall color by state; the color values match vanilla
public enum BorderStatus
{
    Growing,
    Shrinking,
    Stationary,
}

//BorderStatus extensions providing the state color, maps to vanilla getColor
public static class BorderStatusExtensions
{
    //GetColor, border wall color: 0x40FF00 growing, 0xFF3000 shrinking, 0x20A0FF stationary
    public static int GetColor(this BorderStatus status) => status switch
    {
        BorderStatus.Growing => 4259712,
        BorderStatus.Shrinking => 16724016,
        _ => 2138367,
    };
}
