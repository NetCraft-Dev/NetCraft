namespace NetCraft.Network.Chat;

//Click event, maps to vanilla net.minecraft.network.chat.ClickEvent
//The action triggered when the text is clicked, such as opening a URL/running a command/changing page
public interface ClickEvent
{
    //The action enum, maps to vanilla ClickEvent.Action
    //Identifies the click event type and carries its corresponding MapCodec
    public enum Action
    {
        OpenUrl,
        OpenFile,
        RunCommand,
        SuggestCommand,
        ChangePage,
        CopyToClipboard,
    }

    public Action EventAction { get; }

    //Open URL, maps to vanilla ClickEvent.OpenUrl
    public sealed record OpenUrl(Uri Uri) : ClickEvent
    {
        public Action EventAction => Action.OpenUrl;
    }

    //Open file, maps to vanilla ClickEvent.OpenFile
    public sealed record OpenFile(string Path) : ClickEvent
    {
        public Action EventAction => Action.OpenFile;
    }

    //Run command, maps to vanilla ClickEvent.RunCommand
    public sealed record RunCommand(string Command) : ClickEvent
    {
        public Action EventAction => Action.RunCommand;
    }

    //Suggest command, maps to vanilla ClickEvent.SuggestCommand
    public sealed record SuggestCommand(string Command) : ClickEvent
    {
        public Action EventAction => Action.SuggestCommand;
    }

    //Change page, maps to vanilla ClickEvent.ChangePage
    public sealed record ChangePage(int Page) : ClickEvent
    {
        public Action EventAction => Action.ChangePage;
    }

    //Copy to clipboard, maps to vanilla ClickEvent.CopyToClipboard
    public sealed record CopyToClipboard(string Value) : ClickEvent
    {
        public Action EventAction => Action.CopyToClipboard;
    }
}
