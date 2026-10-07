using NetCraft.Codec;
using NetCraft.Network.Chat;

namespace NetCraft.Game.World.Scores;

//TeamColor team color, maps to vanilla net.minecraft.world.scores.TeamColor
//Sixteen colors, each with a numeric id, text color and matching display slot
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

//TeamColorExtensions team color serialized names, text colors, display slots and reverse lookup
public static class TeamColorExtensions
{
    //GetId numeric id, maps to vanilla id
    public static int GetId(this TeamColor color) => (int)color;

    //GetSerializedName serialized name, maps to vanilla getSerializedName
    public static string GetSerializedName(this TeamColor color) => color.ToString().ToLowerInvariant();

    //GetTextColor matching text color, maps to vanilla textColor
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

    //GetDisplaySlot matching display slot, the sixteen colors follow the three generic slots, maps to vanilla displaySlot
    public static DisplaySlot GetDisplaySlot(this TeamColor color) => (DisplaySlot)((int)color + 3);

    //ById reverse lookup by id, out of range gives BLACK, maps to vanilla BY_ID
    public static TeamColor ById(int id)
        => id >= 0 && id < 16 ? (TeamColor)id : TeamColor.BLACK;

    //ByName reverse lookup by serialized name, null when not found, maps to vanilla byName
    public static TeamColor? ByName(string name)
    {
        foreach (var color in Enum.GetValues<TeamColor>())
            if (color.GetSerializedName() == name) return color;
        return null;
    }

    //Codec persistence codec, keyed by serialized name, maps to vanilla CODEC
    public static readonly Codec<TeamColor> Codec = Codecs.String.ComapFlatMap(
        name => ByName(name) is { } color
            ? DataResult<TeamColor>.Success(color)
            : DataResult<TeamColor>.Error(() => $"unknown team color {name}"),
        color => color.GetSerializedName());
}
