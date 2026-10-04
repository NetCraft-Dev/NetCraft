namespace NetCraft.Commands;

//TranslatableMessage 可翻译消息
//对应原版把 Component.translatable 当命令异常消息用 取译文推迟到显示那一刻
//键取不到时 Loc 会原样返回键名 漏翻译的表现是报错里直接露出键 一眼能认出来
public sealed class TranslatableMessage : IMessage
{
    private readonly string _key;
    private readonly object?[] _arguments;

    public TranslatableMessage(string key, params object?[] arguments)
    {
        _key = key;
        _arguments = arguments;
    }

    //Key 语言表里的键
    public string Key => _key;

    public string GetString() => _arguments.Length == 0 ? Loc.Get(_key) : Loc.Format(_key, _arguments);

    public override string ToString() => GetString();
}
