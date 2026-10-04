using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Context;
using NetCraft.Commands.Exceptions;
using StringReader = NetCraft.Commands.StringReader;

namespace NetCraft.Game.Commands.Arguments;

//MessageArgument 聊天消息参数对应原版 net.minecraft.commands.arguments.MessageArgument
//取到命令结尾的全部文本 长度上限 256 对应原版 TOO_LONG
//注册在网络 id 20(message) 客户端按同 id 用原版解析器切词
public sealed class MessageArgument : ArgumentType<string>
{
    //MaxLength 消息长度上限 对应原版 256
    private const int MaxLength = 256;

    private static readonly IReadOnlyList<string> ExamplesList =
        new[] { "Hello world!", "foo", "@e", "Hello @p :)" };

    private static readonly MessageArgument Instance = new();

    public static MessageArgument Message() => Instance;

    public string Parse(StringReader reader)
    {
        var remaining = reader.Remaining;
        if (remaining.Length > MaxLength) throw ErrorTooLong.Create(remaining.Length, MaxLength);
        reader.SetCursor(reader.TotalLength);
        return remaining;
    }

    //ErrorTooLong 消息超长 对应原版 TOO_LONG
    private static readonly Dynamic2CommandExceptionType ErrorTooLong =
        new((length, max) => new LiteralMessage($"消息长度 {length} 超过上限 {max}"));

    //GetMessage 取解析出的消息文本 对应原版 getMessage
    //原版会把 @选择器 展开成实体名组件 NC 暂无该解析 直接按纯文本使用
    public static string GetMessage(CommandContext<CommandSourceStack> context, string name)
        => context.GetArgument<string>(name);

    public IReadOnlyList<string> Examples => ExamplesList;
}
