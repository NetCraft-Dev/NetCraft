using NetCraft.Codec;
using NetCraft.Network.Chat;

namespace NetCraft.Game.World.Scores;

//TeamColor 队伍颜色 对应原版 net.minecraft.world.scores.TeamColor
//十六种颜色各带数字 id 文本颜色与对应的显示槽
public enum TeamColor
{
    BLACK = 0,
    DARK_BLUE = 1,
    DARK_GREEN = 2,
    DARK_AQUA = 3,
    DARK_RED = 4,
    DARK_PURPLE = 5,
    GOLD = 6,
    GRAY = 7,
    DARK_GRAY = 8,
    BLUE = 9,
    GREEN = 10,
    AQUA = 11,
    RED = 12,
    LIGHT_PURPLE = 13,
    YELLOW = 14,
    WHITE = 15
}

//TeamColorExtensions 队伍颜色的序列化名 文本颜色 显示槽与反查
public static class TeamColorExtensions
{
    //GetId 数字 id 对应原版 id
    public static int GetId(this TeamColor color) => (int)color;

    //GetSerializedName 序列化名 对应原版 getSerializedName
    public static string GetSerializedName(this TeamColor color) => color.ToString().ToLowerInvariant();

    //GetTextColor 对应的文本颜色 对应原版 textColor
    public static TextColor GetTextColor(this TeamColor color) => color switch
    {
        TeamColor.BLACK => TextColor.Black,
        TeamColor.DARK_BLUE => TextColor.DarkBlue,
        TeamColor.DARK_GREEN => TextColor.DarkGreen,
        TeamColor.DARK_AQUA => TextColor.DarkAqua,
        TeamColor.DARK_RED => TextColor.DarkRed,
        TeamColor.DARK_PURPLE => TextColor.DarkPurple,
        TeamColor.GOLD => TextColor.Gold,
        TeamColor.GRAY => TextColor.Gray,
        TeamColor.DARK_GRAY => TextColor.DarkGray,
        TeamColor.BLUE => TextColor.Blue,
        TeamColor.GREEN => TextColor.Green,
        TeamColor.AQUA => TextColor.Aqua,
        TeamColor.RED => TextColor.Red,
        TeamColor.LIGHT_PURPLE => TextColor.LightPurple,
        TeamColor.YELLOW => TextColor.Yellow,
        _ => TextColor.White
    };

    //GetDisplaySlot 对应的显示槽 十六色紧跟在三个通用槽之后 对应原版 displaySlot
    public static DisplaySlot GetDisplaySlot(this TeamColor color) => (DisplaySlot)((int)color + 3);

    //ById 按 id 反查 越界给 BLACK 对应原版 BY_ID
    public static TeamColor ById(int id)
        => id >= 0 && id < 16 ? (TeamColor)id : TeamColor.BLACK;

    //ByName 按序列化名反查 找不到给 null 对应原版 byName
    public static TeamColor? ByName(string name)
    {
        foreach (var color in Enum.GetValues<TeamColor>())
            if (color.GetSerializedName() == name) return color;
        return null;
    }

    //Codec 持久化编解码 按序列化名 对应原版 CODEC
    public static readonly Codec<TeamColor> Codec = Codecs.String.ComapFlatMap(
        name => ByName(name) is { } color
            ? DataResult<TeamColor>.Success(color)
            : DataResult<TeamColor>.Error(() => $"未知队伍颜色 {name}"),
        color => color.GetSerializedName());
}
