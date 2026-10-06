namespace NetCraft.Util;

//ReportType report type, maps to vanilla net.minecraft.ReportType
//A type carries a header title and a set of random comments; it is the first two lines of a crash report
public sealed class ReportType
{
    private const string FallbackComment = "Witty comment unavailable :(";

    //Crash crash report, shared by client and server
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

    //NetworkProtocolError used when protocol parsing fails, corresponds to the network subsystem in the server list
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

    //ChunkIoError used when chunk IO fails, classifies the world save side separately
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

    //GetErrorComment picks a random comment, maps to vanilla getErrorComment
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

    //AppendHeader writes the report header, maps to vanilla appendHeader
    //Two lines: a dashed title plus a random comment, followed by the extra comments from the caller
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
