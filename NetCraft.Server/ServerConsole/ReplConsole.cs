using System.IO;
using System.Threading;
using NetCraft.Game.Commands;
using NetCraft.Game.Server;
using NetCraft.Logging;

namespace NetCraft.Server.ServerConsole;

//ReplConsole, the server console
//Interactively it reads key by key, supports inline editing, history, completion, and clears the prompt line before writing logs
//When input or output is redirected it degrades to a read loop, output is written straight through and no control sequence is emitted
//Submitted commands go through the server console command source, the same entry as the GUI console
public sealed class ReplConsole : IDisposable
{
    private const string Prompt = "> ";

    //Clears the whole line the cursor is on and returns the cursor to the line start
    private const string ClearLine = "\r\u001B[2K";

    //HistoryCapacity history retention cap, aligned with the InputLine in-memory cap
    private const int HistoryCapacity = 1000;

    //Reverse search prompt, both the hit and miss forms, same as readline
    private const string SearchPrompt = "(reverse-i-search)`";
    private const string FailedSearchPrompt = "(failed reverse-i-search)`";
    //The segment between the query string and the matched text
    private const string SearchTail = "': ";

    private readonly object _lock = new();
    private readonly InputLine _input = new();
    private readonly DedicatedServer _server;
    private readonly TextWriter _output;
    private readonly bool _interactive;
    private readonly ICompletionSource? _completion;
    private readonly ICommandHighlighter? _highlighter;

    private Thread? _thread;
    private volatile bool _running;

    //_candidates the completion candidates currently kept above the prompt, null means none
    //The state is kept so it can be redrawn, when a log line arrives the candidates are drawn back with it, otherwise a fresh list is immediately wiped
    private IReadOnlyList<string>? _candidates;
    //_candidateRows how many rows the candidate area occupies, erasing moves up by it, the row count comes from this class's own rendering so the terminal width need not be guessed
    private int _candidateRows;
    //_drawn whether the prompt line is already drawn on screen, it makes the empty redraw of empty input skip, pressing Enter any number of times adds nothing to the screen
    private bool _drawn;

    //_searching whether it is parked in Ctrl+R reverse search, keys use a different dispatch during search
    private bool _searching;
    private string _searchQuery = string.Empty;
    //_searchIndex the search cursor, moves backward from the end of history and points to the next position to compare
    private int _searchIndex;
    //_searchSavedText the input line from before entering search, restored on Esc
    private string _searchSavedText = string.Empty;
    //_searchMissed the last search missed, marked on the prompt, same as readline
    private bool _searchMissed;

    private ReplConsole(DedicatedServer server, TextWriter output, bool interactive, ICompletionSource? completion,
        ICommandHighlighter? highlighter)
    {
        _server = server;
        _output = output;
        _interactive = interactive;
        _completion = completion;
        _highlighter = highlighter;
    }

    //Start takes over the server console
    //In interactive mode the output is attached before the input, the reverse would let startup logs wipe the prompt
    //When both are redirected the output is not attached, piped content should stay as is
    public static ReplConsole Start(DedicatedServer server)
    {
        var interactive = !Console.IsInputRedirected && !Console.IsOutputRedirected;
        var repl = new ReplConsole(
            server,
            Console.Out,
            interactive,
            interactive ? new CommandCompletionSource(server) : null,
            interactive ? new CommandHighlighter(server) : null);

        if (interactive)
        {
            //Only interactive mode loads history and attaches the output, the redirected branch passes output through as is and has no use for history
            repl.LoadHistory();
            Console.SetOut(new ConsoleLineWriter(repl));
        }

        repl._running = true;
        repl._thread = new Thread(interactive ? repl.RunInteractive : repl.RunRedirected)
        {
            Name = "NetCraft-Console",
            //Background thread, no need to wait for it once the server main loop exits
            IsBackground = true,
        };
        repl._thread.Start();
        return repl;
    }

    //HistoryPath where history is written, in the program root, maps to Paper's console console_history
    private static string HistoryPath => Path.Combine(AppPaths.BaseDirectory, "console_history");

    //LoadHistory loads the persisted session history and wires up subsequent writes
    //An unreadable history file must not block the console, any exception is swallowed and treated as no history
    private void LoadHistory()
    {
        _input.HistoryAppended = SaveHistoryLine;
        try
        {
            if (!File.Exists(HistoryPath)) return;

            var lines = File.ReadAllLines(HistoryPath);
            _input.LoadHistory(lines);
            //If the file is longer than the in-memory cap, trim once to keep it from growing without bound
            if (lines.Length > HistoryCapacity) File.WriteAllLines(HistoryPath, _input.History);
        }
        catch (Exception e)
        {
            Log.Warning($"Console history load failed: {e.Message}");
        }
    }

    //SaveHistoryLine appends to disk on every commit so it can be recalled on the next startup
    //A failed write only logs a warning, a command already dispatched should not be derailed by it
    private void SaveHistoryLine(string line)
    {
        try
        {
            File.AppendAllText(HistoryPath, line + Environment.NewLine);
        }
        catch (Exception e)
        {
            Log.Warning($"Console history save failed: {e.Message}");
        }
    }

    //WriteLine the output point for kernel logs
    //Interactively the prompt line is cleared first, then the prompt is drawn back after writing, log wrapping is left to the terminal scroll
    public void WriteLine(string text)
    {
        if (!_interactive)
        {
            _output.Write(text);
            _output.Write('\n');
            _output.Flush();
            return;
        }

        lock (_lock)
        {
            //A log line lands between the candidate area and the prompt, erase the candidate area first and draw it back with the log after writing
            EraseCandidates();
            _output.Write(ClearLine);
            _output.Write(text);
            _output.Write('\n');
            //The log pushed the prompt away, this time a redraw is mandatory
            Render(force: true);
        }
    }

    public void Dispose()
    {
        //The key-reading thread may be blocked in ReadKey, it is a background thread so its exit is not awaited
        _running = false;
    }

    //RunInteractive the main path when input is a terminal
    private void RunInteractive()
    {
        lock (_lock) Render();

        while (_running)
        {
            if (!Console.KeyAvailable)
            {
                //15ms per step, key latency is imperceptible and it does not spin and burn a core
                Thread.Sleep(15);
                continue;
            }

            var key = Console.ReadKey(intercept: true);
            lock (_lock) HandleKey(key);
        }
    }

    //RunRedirected the degraded path when input is not a terminal
    //One command per line, reading null means the pipe end closed and the thread finishes on its own
    private void RunRedirected()
    {
        while (_running)
        {
            string? line;
            try
            {
                line = Console.ReadLine();
            }
            catch (IOException)
            {
                return;
            }

            if (line is null) return;
            Execute(line);
        }
    }

    //HandleKey key dispatch
    private void HandleKey(ConsoleKeyInfo key)
    {
        //Search mode has its own key map and switches from literal input to the search string, intercepted entirely here
        if (_searching)
        {
            HandleSearchKey(key);
            return;
        }

        switch (key.Key)
        {
            case ConsoleKey.Enter:
                Submit();
                return;
            case ConsoleKey.Tab:
                Complete();
                return;
            case ConsoleKey.Backspace:
                _input.Backspace();
                break;
            case ConsoleKey.Delete:
                _input.Delete();
                break;
            case ConsoleKey.LeftArrow:
                _input.MoveLeft();
                break;
            case ConsoleKey.RightArrow:
                _input.MoveRight();
                break;
            case ConsoleKey.Home:
                _input.MoveHome();
                break;
            case ConsoleKey.End:
                _input.MoveEnd();
                break;
            case ConsoleKey.UpArrow:
                _input.HistoryUp();
                break;
            case ConsoleKey.DownArrow:
                _input.HistoryDown();
                break;
            case ConsoleKey.Escape:
                //Same as readline, discard the current line
                _input.Clear();
                break;
            case ConsoleKey.L when key.Modifiers.HasFlag(ConsoleModifiers.Control):
                //Redraw the input and prompt after clearing the screen, the candidate rows are gone with the clear so the state is cleared too
                _output.Write("\u001B[2J\u001B[H");
                _candidates = null;
                _candidateRows = 0;
                _drawn = false;
                Render();
                return;
            case ConsoleKey.R when key.Modifiers.HasFlag(ConsoleModifiers.Control):
                //Ctrl+R enters reverse search, same route as readline
                BeginSearch();
                Render(force: true);
                return;
            default:
                //Control characters and function keys do not enter the input
                if (key.KeyChar < ' ' || key.KeyChar == '\u007F') return;
                _input.Insert(key.KeyChar);
                break;
        }

        //The input changed, the previous completion result is stale
        _candidates = null;
        Render();
    }

    //BeginSearch enters Ctrl+R reverse search, saves the current line to restore on Esc
    private void BeginSearch()
    {
        _searching = true;
        _searchQuery = string.Empty;
        //The cursor sits past the end of history so one Ctrl+R starts from the newest entry
        _searchIndex = _input.History.Count;
        _searchSavedText = _input.Text;
        _searchMissed = false;
        //Candidates left from the previous round are unrelated to search, let Render clear them
        _candidates = null;
    }

    //HandleSearchKey search-mode keys, the query string goes through _searchQuery and the input line only carries the match
    private void HandleSearchKey(ConsoleKeyInfo key)
    {
        if (key.Key == ConsoleKey.R && key.Modifiers.HasFlag(ConsoleModifiers.Control))
        {
            //Another Ctrl+R continues searching backward from the current match
            SearchBackward();
            Render(force: true);
            return;
        }

        switch (key.Key)
        {
            case ConsoleKey.Enter:
                //Accept the match and submit directly, same as readline's accept-line
                _searching = false;
                ResetSearch();
                Submit();
                return;
            case ConsoleKey.Escape:
                //Abandon the search, restore the line from before entering search
                _searching = false;
                _input.SetText(_searchSavedText);
                ResetSearch();
                Render(force: true);
                return;
            case ConsoleKey.Backspace:
                if (_searchQuery.Length > 0) _searchQuery = _searchQuery[..^1];
                //The query string changed, search again from the end
                SearchBackward(reset: true);
                Render(force: true);
                return;
            default:
                if (key.KeyChar < ' ' || key.KeyChar == '\u007F')
                {
                    //Other control keys end the search in place, the match stays on the input for further editing
                    _searching = false;
                    ResetSearch();
                    Render(force: true);
                    return;
                }
                _searchQuery += key.KeyChar;
                SearchBackward(reset: true);
                Render(force: true);
                return;
        }
    }

    //SearchBackward finds the first entry in history containing the query string
    //When reset is true it searches from the newest entry, otherwise it steps one further back from the current cursor, matching another Ctrl+R
    private void SearchBackward(bool reset = false)
    {
        var history = _input.History;
        //An empty query string stays put and does not count as a miss
        if (_searchQuery.Length == 0 || history.Count == 0)
        {
            _searchMissed = false;
            return;
        }

        var index = reset ? history.Count - 1 : _searchIndex - 1;
        while (index >= 0)
        {
            if (history[index].Contains(_searchQuery, StringComparison.Ordinal))
            {
                _searchIndex = index;
                _input.SetText(history[index]);
                _searchMissed = false;
                return;
            }
            index--;
        }

        //Reached the start without a match, the last match stays on the input and only the prompt is marked as missed
        _searchMissed = true;
    }

    //ResetSearch clears all intermediate search state
    private void ResetSearch()
    {
        _searchQuery = string.Empty;
        _searchIndex = 0;
        _searchSavedText = string.Empty;
        _searchMissed = false;
    }

    //Submit submits the current line
    private void Submit()
    {
        var line = _input.Commit();
        //Clear the candidate area on submit, the reply must follow this line directly and must not have the previous candidates in between
        _candidates = null;

        //An empty line is just an Enter press and should not leave an extra line on screen, drawing the prompt back as is suffices
        if (line is null)
        {
            Render();
            return;
        }

        EraseCandidates();
        //Leave this line on screen at submit time so the following reply connects
        _output.Write(ClearLine);
        _output.Write(Prompt);
        _output.Write(line);
        _output.Write('\n');

        Execute(line);
        //The submitted line and the reply are both written on new lines, this time a redraw is mandatory
        Render(force: true);
    }

    //Complete inline completion, same scheme as readline
    //A single candidate is filled in directly, multiple candidates fill to the common prefix first and only list when no further fill is possible
    private void Complete()
    {
        if (_completion is null) return;

        var text = _input.Text;
        IReadOnlyList<string> candidates;
        try
        {
            candidates = _completion.GetCompletions(text, _input.Caret);
        }
        catch (Exception ex)
        {
            //The parser used for completion parses whatever it sees, it must not take down the console thread
            WriteLine($"Completion failed: {ex.GetType().Name} {ex.Message}");
            return;
        }

        if (candidates.Count == 0) return;

        if (candidates.Count == 1)
        {
            _input.SetText(candidates[0]);
            _candidates = null;
            Render();
            return;
        }

        var prefix = CommonPrefix(candidates);
        if (prefix.Length > text.Length)
        {
            _input.SetText(prefix);
            _candidates = null;
            Render();
            return;
        }

        //Multiple candidates that cannot fill to the common prefix stay above the prompt until the next edit
        _candidates = candidates;
        Render();
    }

    //Execute enqueues one command line for the main loop to pick up
    //A leading slash is accepted too, the vanilla terminal takes both spellings
    //Does not run directly on the console thread, crossing world state with the main loop causes problems, exception fallback also belongs to the main loop
    private void Execute(string line)
    {
        var text = line.Trim();
        if (text.StartsWith('/')) text = text[1..];
        if (text.Length == 0) return;
        _server.EnqueueConsoleCommand(ServerCommandSource.Console(_server), text);
    }

    //Render redraws the candidate area and the prompt line, the cursor ends up at the input position
    //Candidates stay above the prompt and are erased and redrawn when a log arrives, so new logs do not wash the candidates away
    //When force is false and the screen already shows just the prompt with no other state, it skips without writing a single character
    //Pressing Enter on empty input takes this path and shows no visible change
    private void Render(bool force = false)
    {
        if (!force && _drawn && _candidateRows == 0 && _candidates is null && _input.Text.Length == 0) return;

        EraseCandidates();
        _output.Write(ClearLine);
        if (_candidates is { Count: > 0 } candidates)
        {
            //One candidate per line, same layout as the completion list shown by pressing Tab in-game
            //One per line also keeps erasing exact: line length is far below the terminal width so the terminal does not wrap, the counted rows are the actual rows
            foreach (var candidate in candidates)
            {
                _output.Write(candidate);
                _output.Write('\n');
                _candidateRows++;
            }
        }

        if (_searching)
        {
            //In search mode the prompt becomes the readline line, the cursor sits at the end of the query string rather than the matched line
            _output.Write(_searchMissed ? FailedSearchPrompt : SearchPrompt);
            _output.Write(_searchQuery);
            _output.Write(SearchTail);
            _output.Write(_input.Text);
            var tail = _input.Text.Length + SearchTail.Length;
            if (tail > 0) _output.Write($"\u001B[{tail}D");
        }
        else
        {
            _output.Write(Prompt);
            //The parts the command tree recognizes are colored by node, highlighting only inserts escapes between characters and the visible length is unchanged
            _output.Write(_highlighter?.Highlight(_input.Text) ?? _input.Text);
            //Move the cursor left to the input position after drawing, ANSI D is move left n columns
            var back = _input.Text.Length - _input.Caret;
            if (back > 0) _output.Write($"\u001B[{back}D");
        }

        _drawn = true;
        _output.Flush();
    }

    //EraseCandidates wipes the candidate area together with the prompt line off the screen, the cursor lands on the first candidate row
    //Uses "clear from the cursor to the end of screen" rather than clearing line by line: line-by-line clearing only erases the text while those rows still occupy the screen and leave a block of blank lines when collapsed
    //The prompt line is always at the very bottom of the screen and nothing else is below the cursor, so clearing to the end of screen is safe
    //The row count was recorded during this class's own rendering, moving up by it lands exactly on the first candidate row
    private void EraseCandidates()
    {
        if (_candidateRows == 0) return;
        _output.Write($"\u001B[{_candidateRows}A");
        _output.Write("\r\u001B[J");
        _candidateRows = 0;
    }

    //CommonPrefix finds the common prefix of all candidates, for multiple candidates it fills to the divergence point
    private static string CommonPrefix(IReadOnlyList<string> values)
    {
        var prefix = values[0];
        for (var i = 1; i < values.Count; i++)
        {
            var other = values[i];
            var length = 0;
            while (length < prefix.Length && length < other.Length &&
                   char.ToLowerInvariant(prefix[length]) == char.ToLowerInvariant(other[length]))
            {
                length++;
            }

            prefix = prefix[..length];
            if (prefix.Length == 0) break;
        }

        return prefix;
    }
}
