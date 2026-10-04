namespace NetCraft.Game.World.Level;

//Difficulty 游戏难度对标原版 net.minecraft.world.level.Difficulty
//静态实例模式代替 enum id 与协议及 level.dat 一致 0=和平 1=简单 2=普通 3=困难
public sealed class Difficulty
{
    public static readonly Difficulty Peaceful = new(0, "peaceful");
    public static readonly Difficulty Easy = new(1, "easy");
    public static readonly Difficulty Normal = new(2, "normal");
    public static readonly Difficulty Hard = new(3, "hard");

    private static readonly Difficulty[] s_all = { Peaceful, Easy, Normal, Hard };

    //Id 数字 id 与协议一致
    public int Id { get; }
    //Name 名称 与 level.dat 里 difficulty 字段一致
    public string Name { get; }

    private Difficulty(int id, string name)
    {
        Id = id;
        Name = name;
    }

    //ById 按数字 id 查难度 未找到返回 null
    public static Difficulty? ById(int id)
    {
        foreach (var difficulty in s_all)
            if (difficulty.Id == id) return difficulty;
        return null;
    }

    //ByName 按名称查难度忽略大小写 未找到返回 null
    public static Difficulty? ByName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        foreach (var difficulty in s_all)
            if (difficulty.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) return difficulty;
        return null;
    }

    public override string ToString() => Name;
}
