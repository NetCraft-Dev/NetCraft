namespace NetCraft.Commands;

//LiteralMessage maps to vanilla com.mojang.brigadier.LiteralMessage
//Wraps a fixed string as a simple IMessage implementation
public sealed class LiteralMessage : IMessage
{
    private readonly string _string;

    public LiteralMessage(string @string)
    {
        _string = @string;
    }

    public string GetString() => _string;

    public override string ToString() => _string;
}
