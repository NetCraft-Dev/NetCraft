namespace NetCraft.Server.ServerConsole;

//InputLine 一行输入 文本加光标位置 历史导航也归它
//只做模型不碰控制台 渲染与按键分派都在 ReplConsole
public sealed class InputLine
{
    //HistoryCapacity 内存里保留的历史条数 超出从头部丢 与 readline 的历史上限一个意思
    private const int HistoryCapacity = 1000;

    private readonly List<string> _history = new();
    private int _navIndex = -1;
    private string _draft = string.Empty;

    //HistoryAppended 历史新增一条时回调 由控制台接过去落盘
    //模型自己不碰文件 落盘时机与格式留给调用方
    public Action<string>? HistoryAppended { get; set; }

    //Text 当前输入文本
    public string Text { get; private set; } = string.Empty;

    //Caret 光标位置 取值 0 到 Text.Length
    public int Caret { get; private set; }

    //History 已提交的历史 最新一条在末尾
    public IReadOnlyList<string> History => _history;

    //IsNavigating 是否正停在历史里
    public bool IsNavigating => _navIndex >= 0;

    //SetText 整串替换并把光标落到末尾
    public void SetText(string text)
    {
        Text = text;
        Caret = text.Length;
    }

    //LoadHistory 载入落盘的历史 只保留最后 HistoryCapacity 条
    //空行不进历史 与提交时那条判定保持一致
    public void LoadHistory(IReadOnlyList<string> lines)
    {
        var start = Math.Max(0, lines.Count - HistoryCapacity);
        for (var i = start; i < lines.Count; i++)
        {
            if (lines[i].Length > 0) _history.Add(lines[i]);
        }

        _navIndex = -1;
    }

    //Insert 在光标处插入一个字符
    public void Insert(char value)
    {
        LeaveHistory();
        Text = Text.Insert(Caret, value.ToString());
        Caret++;
    }

    //Insert 在光标处插入整串 粘贴走这条
    public void Insert(string value)
    {
        if (value.Length == 0) return;
        LeaveHistory();
        Text = Text.Insert(Caret, value);
        Caret += value.Length;
    }

    //Backspace 删光标左边那一个字符
    public void Backspace()
    {
        if (Caret == 0) return;
        LeaveHistory();
        Text = Text.Remove(Caret - 1, 1);
        Caret--;
    }

    //Delete 删光标右边那一个字符
    public void Delete()
    {
        if (Caret >= Text.Length) return;
        LeaveHistory();
        Text = Text.Remove(Caret, 1);
    }

    //MoveLeft 光标左移 到行首不动
    public void MoveLeft()
    {
        if (Caret > 0) Caret--;
    }

    //MoveRight 光标右移 到行尾不动
    public void MoveRight()
    {
        if (Caret < Text.Length) Caret++;
    }

    //MoveHome 光标到行首
    public void MoveHome() => Caret = 0;

    //MoveEnd 光标到行尾
    public void MoveEnd() => Caret = Text.Length;

    //Clear 清空当前输入 历史不动
    public void Clear()
    {
        Text = string.Empty;
        Caret = 0;
        _navIndex = -1;
        _draft = string.Empty;
    }

    //Commit 取走当前输入并入历史 空输入返回 null 由调用方决定不执行
    public string? Commit()
    {
        var text = Text;
        //连续重复的一条不入历史 与 readline 的去重行为一致
        if (text.Length > 0 && (_history.Count == 0 || _history[^1] != text))
        {
            _history.Add(text);
            if (_history.Count > HistoryCapacity) _history.RemoveAt(0);
            HistoryAppended?.Invoke(text);
        }

        Clear();
        return text.Length > 0 ? text : null;
    }

    //HistoryUp 上翻历史
    public bool HistoryUp() => Navigate(-1);

    //HistoryDown 下翻历史
    public bool HistoryDown() => Navigate(1);

    //Navigate 在历史里上下走 翻过最新一条就回到进历史前的草稿
    private bool Navigate(int direction)
    {
        if (_history.Count == 0) return false;

        if (_navIndex < 0)
        {
            //第一次进历史 把当前输入留作草稿
            if (direction > 0) return false;
            _draft = Text;
            _navIndex = _history.Count - 1;
        }
        else
        {
            var next = _navIndex + direction;
            if (next < 0) return false;
            if (next >= _history.Count)
            {
                _navIndex = -1;
                SetText(_draft);
                return true;
            }
            _navIndex = next;
        }

        SetText(_history[_navIndex]);
        return true;
    }

    //LeaveHistory 在历史里一编辑就脱离导航 之后上翻从当前位置重来
    private void LeaveHistory()
    {
        if (_navIndex < 0) return;
        _navIndex = -1;
        _draft = string.Empty;
    }
}
