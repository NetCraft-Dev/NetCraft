namespace NetCraft.Commands;

//LiteralMessage 字面消息对应原版com.mojang.brigadier.LiteralMessage
//包装固定字符串作为IMessage的简单实现
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
