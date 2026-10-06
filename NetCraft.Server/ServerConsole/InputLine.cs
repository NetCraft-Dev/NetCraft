namespace NetCraft.Server.ServerConsole;

//InputLine, a line of input, text plus cursor position, history navigation also belongs to it
//Model only, it does not touch the console, rendering and key dispatch are in ReplConsole
public sealed class InputLine
{
    //HistoryCapacity number of history entries kept in memory, drops from the head when over, same idea as the readline history cap
    private const int HistoryCapacity = 1000;

    private readonly List<string> _history = new();
    private int _navIndex = -1;
    private string _draft = string.Empty;

    //HistoryAppended callback when a history entry is added, the console picks it up and writes it to disk
    //The model does not touch files itself, the timing and format of writing to disk are left to the caller
    public Action<string>? HistoryAppended { get; set; }

    //Text the current input text
    public string Text { get; private set; } = string.Empty;

    //Caret cursor position, ranging from 0 to Text.Length
    public int Caret { get; private set; }

    //History committed history, the newest entry at the end
    public IReadOnlyList<string> History => _history;

    //IsNavigating whether it is currently parked in history
    public bool IsNavigating => _navIndex >= 0;

    //SetText replaces the whole string and puts the cursor at the end
    public void SetText(string text)
    {
        Text = text;
        Caret = text.Length;
    }

    //LoadHistory loads history from disk, keeping only the last HistoryCapacity entries
    //Empty lines do not enter history, consistent with the check at commit time
    public void LoadHistory(IReadOnlyList<string> lines)
    {
        var start = Math.Max(0, lines.Count - HistoryCapacity);
        for (var i = start; i < lines.Count; i++)
        {
            if (lines[i].Length > 0) _history.Add(lines[i]);
        }

        _navIndex = -1;
    }

    //Insert inserts one character at the cursor
    public void Insert(char value)
    {
        LeaveHistory();
        Text = Text.Insert(Caret, value.ToString());
        Caret++;
    }

    //Insert inserts a whole string at the cursor, paste goes through this
    public void Insert(string value)
    {
        if (value.Length == 0) return;
        LeaveHistory();
        Text = Text.Insert(Caret, value);
        Caret += value.Length;
    }

    //Backspace deletes the character to the left of the cursor
    public void Backspace()
    {
        if (Caret == 0) return;
        LeaveHistory();
        Text = Text.Remove(Caret - 1, 1);
        Caret--;
    }

    //Delete deletes the character to the right of the cursor
    public void Delete()
    {
        if (Caret >= Text.Length) return;
        LeaveHistory();
        Text = Text.Remove(Caret, 1);
    }

    //MoveLeft moves the cursor left, stays put at the line start
    public void MoveLeft()
    {
        if (Caret > 0) Caret--;
    }

    //MoveRight moves the cursor right, stays put at the line end
    public void MoveRight()
    {
        if (Caret < Text.Length) Caret++;
    }

    //MoveHome moves the cursor to the line start
    public void MoveHome() => Caret = 0;

    //MoveEnd moves the cursor to the line end
    public void MoveEnd() => Caret = Text.Length;

    //Clear clears the current input, history is untouched
    public void Clear()
    {
        Text = string.Empty;
        Caret = 0;
        _navIndex = -1;
        _draft = string.Empty;
    }

    //Commit takes the current input and adds it to history, an empty input returns null so the caller decides not to execute
    public string? Commit()
    {
        var text = Text;
        //A consecutively repeated entry does not enter history, consistent with readline dedup behavior
        if (text.Length > 0 && (_history.Count == 0 || _history[^1] != text))
        {
            _history.Add(text);
            if (_history.Count > HistoryCapacity) _history.RemoveAt(0);
            HistoryAppended?.Invoke(text);
        }

        Clear();
        return text.Length > 0 ? text : null;
    }

    //HistoryUp navigates up through history
    public bool HistoryUp() => Navigate(-1);

    //HistoryDown navigates down through history
    public bool HistoryDown() => Navigate(1);

    //Navigate walks up and down through history, going past the newest entry returns to the draft from before entering history
    private bool Navigate(int direction)
    {
        if (_history.Count == 0) return false;

        if (_navIndex < 0)
        {
            //Entering history for the first time, keep the current input as a draft
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

    //LeaveHistory editing while in history leaves navigation, later up-navigation restarts from the current position
    private void LeaveHistory()
    {
        if (_navIndex < 0) return;
        _navIndex = -1;
        _draft = string.Empty;
    }
}
