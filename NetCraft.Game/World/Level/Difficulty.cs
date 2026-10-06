namespace NetCraft.Game.World.Level;

//Difficulty game difficulty, maps to vanilla net.minecraft.world.level.Difficulty
//Static instance pattern instead of enum; ids match protocol and level.dat: 0=peaceful 1=easy 2=normal 3=hard
public sealed class Difficulty
{
    public static readonly Difficulty Peaceful = new(0, "peaceful");
    public static readonly Difficulty Easy = new(1, "easy");
    public static readonly Difficulty Normal = new(2, "normal");
    public static readonly Difficulty Hard = new(3, "hard");

    private static readonly Difficulty[] s_all = { Peaceful, Easy, Normal, Hard };

    //Id numeric id, matches protocol
    public int Id { get; }
    //Name name, matches the difficulty field in level.dat
    public string Name { get; }

    private Difficulty(int id, string name)
    {
        Id = id;
        Name = name;
    }

    //ById lookup difficulty by numeric id, returns null if not found
    public static Difficulty? ById(int id)
    {
        foreach (var difficulty in s_all)
            if (difficulty.Id == id) return difficulty;
        return null;
    }

    //ByName lookup difficulty by name, case-insensitive, returns null if not found
    public static Difficulty? ByName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        foreach (var difficulty in s_all)
            if (difficulty.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) return difficulty;
        return null;
    }

    public override string ToString() => Name;
}
