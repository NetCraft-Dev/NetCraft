using NetCraft.Codec;

namespace NetCraft.Network.Chat.Contents;

//计分板内容对应原版net.minecraft.network.chat.contents.ScoreContents
//Name 持有者 Objective 计分项名运行时从 Scoreboard 取值
public sealed class ScoreContents : ComponentContents
{
    public string Name { get; }
    public string Objective { get; }

    public ScoreContents(string name, string objective)
    {
        Name = name;
        Objective = objective;
    }

    public MapCodec<ComponentContents> Codec() => throw new NotImplementedException();

    public override string ToString() => $"score{{{Name}:{Objective}}}";
}
