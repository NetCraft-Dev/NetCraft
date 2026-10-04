namespace NetCraft.Network.Chat;

//点击事件对应原版net.minecraft.network.chat.ClickEvent
//文本被点击时触发的动作如打开URL/运行命令/翻页等
public interface ClickEvent
{
    //动作枚举对应原版ClickEvent.Action
    //标识点击事件类型并承载该类型对应的MapCodec
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

    //打开URL对应原版ClickEvent.OpenUrl
    public sealed record OpenUrl(Uri Uri) : ClickEvent
    {
        public Action EventAction => Action.OpenUrl;
    }

    //打开文件对应原版ClickEvent.OpenFile
    public sealed record OpenFile(string Path) : ClickEvent
    {
        public Action EventAction => Action.OpenFile;
    }

    //运行命令对应原版ClickEvent.RunCommand
    public sealed record RunCommand(string Command) : ClickEvent
    {
        public Action EventAction => Action.RunCommand;
    }

    //建议命令对应原版ClickEvent.SuggestCommand
    public sealed record SuggestCommand(string Command) : ClickEvent
    {
        public Action EventAction => Action.SuggestCommand;
    }

    //翻页对应原版ClickEvent.ChangePage
    public sealed record ChangePage(int Page) : ClickEvent
    {
        public Action EventAction => Action.ChangePage;
    }

    //复制到剪贴板对应原版ClickEvent.CopyToClipboard
    public sealed record CopyToClipboard(string Value) : ClickEvent
    {
        public Action EventAction => Action.CopyToClipboard;
    }
}
