namespace NetCraft.Util;

//ReportType 报告类型 对应原版 net.minecraft.ReportType
//一个类型带一个头部标题与一组随机文案 崩溃报告开头那两行就是它
public sealed class ReportType
{
    private const string FallbackComment = "Witty comment unavailable :(";

    //Crash 崩溃报告 客户端与服务端共用
    public static readonly ReportType Crash = new("NetCraft Crash Report", new[]
    {
        "Who set us up the TNT?",
        "Everything's going to plan. No, really, that was supposed to happen.",
        "Uh... Did I do that?",
        "Oops.",
        "Why did you do that?",
        "I feel sad now :(",
        "My bad.",
        "I'm sorry, Dave.",
        "I let you down. Sorry :(",
        "On the bright side, I bought you a teddy bear!",
        "Daisy, daisy...",
        "Oh - I know what I did wrong!",
        "Hey, that tickles! Hehehe!",
        "I blame Dinnerbone.",
        "You should try our sister game, Minceraft!",
        "Don't be sad. I'll do better next time, I promise!",
        "Don't be sad, have a hug! <3",
        "I just don't know what went wrong :(",
        "Shall we play a game?",
        "Quite honestly, I wouldn't worry myself about that.",
        "I bet Cylons wouldn't have this problem.",
        "Sorry :(",
        "Surprise! Haha. Well, this is awkward.",
        "Would you like a cupcake?",
        "Hi. I'm Minecraft, and I'm a crashaholic.",
        "Ooh. Shiny.",
        "This doesn't make any sense!",
        "Why is it breaking :(",
        "Don't do that.",
        "Ouch. That hurt :(",
        "You're mean.",
        "This is a token for 1 free hug. Redeem at your nearest Mojangsta: [~~HUG~~]",
        "There are four lights!",
        "But it works on my machine.",
    });

    //NetworkProtocolError 协议解析出错时用 与服务端清单里的 network 子系统对应
    public static readonly ReportType NetworkProtocolError = new("NetCraft Network Protocol Error Report", new[]
    {
        "0xBADF00D",
        "+'${`%&NO CARRIER",
        "Please insert The Internet CD #4",
        "Sabotage!",
        "Are you sure you are not moving wrongly?",
        "This time is not my fault, I promise!",
        "All lines are down!",
        "Maybe a shark bit some cable",
        "404",
        "I'm sorry, I don't speak that language",
        "What we've got here is failure to communicate",
        "It's the tubes, they're clogged!",
        "Abort, Retry, Ignore?",
        "Could be worse, I guess",
        "Wait, was the last bit one or zero?",
        "Too many suspicious packets",
        "Don't worry, I'll be fine",
        "Maybe this time it will work!",
        "I heard pigeons are more reliable",
    });

    //ChunkIoError 区块读写出错时用 存档那一侧的问题单独归类
    public static readonly ReportType ChunkIoError = new("NetCraft Chunk IO Error Report", new[]
    {
        "I have failed you!",
        "Let's not do it again...",
        "Worst magic trick ever!",
        "Remember to backup your worlds regularly",
        "Pirates stole your chunk!",
        "Ker-chunk!",
        "Ideally, this shouldn't be here",
        "Let's hope it wasn't anything important",
        "Computers were a mistake",
        "Welp",
        "Not my proudest moment",
        "Who needs blocks in a block game, right?",
        "This chunk is no more...it has ceased to be...this is an EX-chunk",
        "loss.mca",
    });

    private readonly string[] _comments;

    public ReportType(string header, string[] comments)
    {
        Header = header;
        _comments = comments;
    }

    public string Header { get; }

    //GetErrorComment 随机取一条文案 对应原版 getErrorComment
    public string GetErrorComment()
    {
        try
        {
            return _comments.Length == 0
                ? FallbackComment
                : _comments[(int)(Environment.TickCount64 % _comments.Length)];
        }
        catch (Exception)
        {
            return FallbackComment;
        }
    }

    //AppendHeader 写报告头 对应原版 appendHeader
    //两行一个横线标题加一条随机文案 之后是调用方给的补充说明
    public void AppendHeader(System.Text.StringBuilder builder, IReadOnlyList<string> extraComments)
    {
        var newLine = Environment.NewLine;
        builder.Append("---- ").Append(Header).Append(" ----").Append(newLine);
        builder.Append("// ").Append(GetErrorComment()).Append(newLine);
        foreach (var comment in extraComments)
            builder.Append("// ").Append(comment).Append(newLine);
        builder.Append(newLine);
    }
}
