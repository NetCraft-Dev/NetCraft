using NetCraft.Codec;

namespace NetCraft.Game.World.Scores;

//DisplaySlot 计分板显示槽 对应原版 net.minecraft.world.scores.DisplaySlot
//前三个是通用槽 其后十四个按队伍颜色各占一个
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

//DisplaySlotExtensions 显示槽的序列化名与按名按 id 反查
public static class DisplaySlotExtensions
{
    //GetName 序列化名 对应原版 getSerializedName
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

    //GetId 网络与存档用的数字 id 对应原版 id
    public static int GetId(this DisplaySlot slot) => (int)slot;

    //ById 按 id 反查 越界给 LIST 对应原版 BY_ID
    public static DisplaySlot ById(int id)
        => id >= 0 && id < 19 ? (DisplaySlot)id : DisplaySlot.LIST;

    //ByName 按序列化名反查 找不到给 null 对应原版 byName
    public static DisplaySlot? ByName(string name)
    {
        foreach (var slot in Enum.GetValues<DisplaySlot>())
            if (slot.GetName() == name) return slot;
        return null;
    }

    //Codec 持久化编解码 按序列化名 对应原版 CODEC
    public static readonly Codec<DisplaySlot> Codec = Codecs.String.ComapFlatMap(
        name => ByName(name) is { } slot
            ? DataResult<DisplaySlot>.Success(slot)
            : DataResult<DisplaySlot>.Error(() => $"未知显示槽 {name}"),
        slot => slot.GetName());
}
