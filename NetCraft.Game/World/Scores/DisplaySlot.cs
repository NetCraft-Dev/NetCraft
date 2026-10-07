using NetCraft.Codec;

namespace NetCraft.Game.World.Scores;

//DisplaySlot scoreboard display slot, maps to vanilla net.minecraft.world.scores.DisplaySlot
//The first three are the generic slots, followed by fourteen, one per team color
public enum DisplaySlot
{
    LIST = 0,
    SIDEBAR = 1,
    BELOW_NAME = 2,
    TEAM_BLACK = 3,
    TEAM_DARK_BLUE = 4,
    TEAM_DARK_GREEN = 5,
    TEAM_DARK_AQUA = 6,
    TEAM_DARK_RED = 7,
    TEAM_DARK_PURPLE = 8,
    TEAM_GOLD = 9,
    TEAM_GRAY = 10,
    TEAM_DARK_GRAY = 11,
    TEAM_BLUE = 12,
    TEAM_GREEN = 13,
    TEAM_AQUA = 14,
    TEAM_RED = 15,
    TEAM_LIGHT_PURPLE = 16,
    TEAM_YELLOW = 17,
    TEAM_WHITE = 18
}

//DisplaySlotExtensions serialized names for display slots and reverse lookup by name and by id
public static class DisplaySlotExtensions
{
    //GetName serialized name, maps to vanilla getSerializedName
    public static string GetName(this DisplaySlot slot) => slot switch
    {
        DisplaySlot.LIST => "list",
        DisplaySlot.SIDEBAR => "sidebar",
        DisplaySlot.BELOW_NAME => "below_name",
        DisplaySlot.TEAM_BLACK => "team_black",
        DisplaySlot.TEAM_DARK_BLUE => "team_dark_blue",
        DisplaySlot.TEAM_DARK_GREEN => "team_dark_green",
        DisplaySlot.TEAM_DARK_AQUA => "team_dark_aqua",
        DisplaySlot.TEAM_DARK_RED => "team_dark_red",
        DisplaySlot.TEAM_DARK_PURPLE => "team_dark_purple",
        DisplaySlot.TEAM_GOLD => "team_gold",
        DisplaySlot.TEAM_GRAY => "team_gray",
        DisplaySlot.TEAM_DARK_GRAY => "team_dark_gray",
        DisplaySlot.TEAM_BLUE => "team_blue",
        DisplaySlot.TEAM_GREEN => "team_green",
        DisplaySlot.TEAM_AQUA => "team_aqua",
        DisplaySlot.TEAM_RED => "team_red",
        DisplaySlot.TEAM_LIGHT_PURPLE => "team_light_purple",
        DisplaySlot.TEAM_YELLOW => "team_yellow",
        _ => "team_white"
    };

    //GetId numeric id used on the network and in saves, maps to vanilla id
    public static int GetId(this DisplaySlot slot) => (int)slot;

    //ById reverse lookup by id, out of range gives LIST, maps to vanilla BY_ID
    public static DisplaySlot ById(int id)
        => id >= 0 && id < 19 ? (DisplaySlot)id : DisplaySlot.LIST;

    //ByName reverse lookup by serialized name, null when not found, maps to vanilla byName
    public static DisplaySlot? ByName(string name)
    {
        foreach (var slot in Enum.GetValues<DisplaySlot>())
            if (slot.GetName() == name) return slot;
        return null;
    }

    //Codec persistence codec, keyed by serialized name, maps to vanilla CODEC
    public static readonly Codec<DisplaySlot> Codec = Codecs.String.ComapFlatMap(
        name => ByName(name) is { } slot
            ? DataResult<DisplaySlot>.Success(slot)
            : DataResult<DisplaySlot>.Error(() => $"unknown display slot {name}"),
        slot => slot.GetName());
}
