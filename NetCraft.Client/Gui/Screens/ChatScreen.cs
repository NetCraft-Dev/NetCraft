using System.Text;
using NetCraft.Game.Gui;
using NetCraft.Gpu;

namespace NetCraft.Game.Gui.Screens;

//ChatScreen 聊天屏幕对应原版 ChatScreen
//输入框 + 消息历史 Enter 发送暂不接入网络仅本地累积
public sealed class ChatScreen : Screen
{
    private GuiTextBox? _inputBox;
    private GuiLabel? _historyLabel;
    private readonly List<string> _history = new();

    public override string Title => "聊天";

    public override void Init()
    {
        var inputY = GuiHeight - 30;
        _inputBox = AddWidget(new GuiTextBox { X = 4, Y = inputY, Width = GuiWidth - 8, Height = 20 });
        _historyLabel = AddWidget(new GuiLabel { X = 4, Y = inputY - 100, Width = GuiWidth - 8, Height = 90, ForegroundColor = GuiColor.White });
        Window.FocusedControl = _inputBox;
    }

    public override void Tick()
    {
        if (_historyLabel is null || _history.Count == 0) return;
        var sb = new StringBuilder();
        foreach (var msg in _history.Skip(Math.Max(0, _history.Count - 5)))
            sb.AppendLine(msg);
        _historyLabel.Text = sb.ToString();
    }

    //Send 发送消息加入历史并清空输入框暂不接入网络
    public void Send(string message)
    {
        _history.Add(message);
        if (_inputBox is not null) _inputBox.Text = string.Empty;
    }

    public override void OnClose() => Manager.SetScreen(new GameScreen());
    public override bool IsPauseScreen() => false;
}
