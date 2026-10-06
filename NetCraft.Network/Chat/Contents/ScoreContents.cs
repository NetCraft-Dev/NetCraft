using NetCraft.Codec;

namespace NetCraft.Network.Chat.Contents;

//Scoreboard contents, maps to vanilla net.minecraft.network.chat.contents.ScoreContents
//Name is the holder, Objective the score name, with values read from the Scoreboard at runtime
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
