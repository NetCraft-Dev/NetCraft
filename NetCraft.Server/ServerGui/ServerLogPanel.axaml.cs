using System;
using System.Linq;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NetCraft.Game;
using NetCraft.Game.Commands;
using NetCraft.Game.Server;
using NetCraft.Logging;

namespace NetCraft.Server.Gui;

//ServerLogPanel, the log and command panel, maps to "Log and chat" in vanilla MinecraftServerGui
//A readonly log area above and a command input below, Enter executes through the console source
public sealed partial class ServerLogPanel : UserControl
{
    //Max lines kept in the log area, aligned with the history cache so the startup window is retained
    //Overflow drops from the head, letting it grow would drag down memory and text layout
    private const int MaxLines = 20000;
    //Release a small batch of logs each frame, trickling out line by line looks continuous instead of hundreds slamming down at once
    private static readonly TimeSpan FlushInterval = TimeSpan.FromMilliseconds(16);
    //Max lines released per flush, any backlog waits for the next frame so a single frame does not insert thousands of controls and freeze the UI
    private const int MaxPerFlush = 120;
    //The entrance animation runs only when the released line count is at or below this, animating during a backlog only hurts
    private const int AnimatedBatchLimit = 6;
    //Max backlog of pending logs, the UI keeps only a thousand lines and any more just wastes memory
    private const int MaxPending = 4000;
    //Bottom tolerance, anything below it counts as at the bottom
    private const double BottomTolerance = 4;

    private readonly MinecraftServer _server;
    private readonly LogStore _store;
    //_pending logs are written by the output thread and read by the UI thread, a Queue makes dequeuing O(1)
    private readonly Queue<LogStore.Entry> _pending = new();
    private readonly Lock _pendingLock = new();
    private readonly DispatcherTimer _flush;
    //_view the list binding source, attached once at construction, swapping ItemsSource wholesale rebuilds all containers
    private readonly RangeObservableCollection<LogRow> _view = new();
    //_follow whether to stick to the newest line, changed only when the user actually moves the viewport
    //Computing Extent/Offset per frame does not work: inserting a new log grows Extent while Offset stays put
    //One frame judged as "not at the bottom" is unrecoverable, the user does not move Offset while Extent keeps growing, so it is flung off the whole way
    private bool _follow = true;
    //_scroll the internal scrollbar, obtained lazily by ScrollChanged rather than walking the visual tree every frame
    private ScrollViewer? _scroll;
    private double _lastOffsetY;

    //Scroll gets the internal scrollbar, only walking the visual tree and caching it when unavailable
    //It cannot be found before being attached to the visual tree, so it stays null then and is searched on next use
    private ScrollViewer? Scroll
        => _scroll ??= LogLines.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();

    public ServerLogPanel(MinecraftServer server, LogStore store)
    {
        _server = server;
        _store = store;
        InitializeComponent();

        CommandInput.KeyDown += OnInputKeyDown;
        LogLines.ItemsSource = _view;
        //Attach to the ListBox to receive the bubbled event from the internal scrollbar, avoiding a tree walk in Loaded
        //handledEventsToo: receive it even if the scrollbar marked the event handled, otherwise it degrades to always following
        LogLines.AddHandler(ScrollViewer.ScrollChangedEvent, OnScrollChanged,
            RoutingStrategies.Bubble, handledEventsToo: true);

        //Logs fire on the output thread, the callback only enqueues and all control actions are left to the timer below
        //Catch up on logs from before subscribing first, kernel init and resource loading logs exist only in the buffer
        LoadHistory();
        _store.LineAdded += OnLogLine;
        //History is filled during construction when nothing is measured yet, scrolling to the end is a no-op then
        //Post it again after layout completes, otherwise the window opens stuck on the oldest line
        LogLines.Loaded += (_, _) =>
            Dispatcher.UIThread.Post(ScrollToEnd, DispatcherPriority.Loaded);
        _flush = new DispatcherTimer { Interval = FlushInterval };
        _flush.Tick += (_, _) => FlushPending();
        _flush.Start();
    }

    //SetInputEnabled disables the command box before the server is ready
    //The GUI shows before Done, typing then hits a world still initializing and results in a half-initialized state are unpredictable
    public void SetInputEnabled(bool enabled) => CommandInput.IsEnabled = enabled;

    //LoadHistory backfills logs written before subscribing into the log area, afterwards the live subscription path takes over
    private void LoadHistory()
    {
        var rows = new List<LogRow>();
        foreach (var entry in _store.Snapshot()) rows.Add(MakeRow(entry, animate: false));
        //The history count may exceed the panel cap, trim the head by the same rule and lay it all in at once
        //This is a whole-content swap, emitting a single Reset to rebuild the list is appropriate, adding one by one would waste thousands of container runs
        if (rows.Count > MaxLines) rows.RemoveRange(0, rows.Count - MaxLines);
        _view.ReplaceAll(rows);
    }

    //Detach unsubscribes from logs, called when the window closes, otherwise this panel is held forever by the logging system
    public void Detach()
    {
        _store.LineAdded -= OnLogLine;
        _flush.Stop();
    }

    private void OnLogLine(LogStore.Entry entry)
    {
        lock (_pendingLock)
        {
            //Drop the oldest when the backlog peaks, keeping the newest batch matters when logs surge
            if (_pending.Count >= MaxPending) _pending.Dequeue();
            _pending.Enqueue(entry);
        }
    }

    //FlushPending releases a small batch of the backlog each frame, building controls per line rather than resetting text wholesale
    //The release amount scales with the backlog, keeping up with a surge while making output at normal rates look continuous
    private void FlushPending()
    {
        List<LogStore.Entry> batch;
        lock (_pendingLock)
        {
            if (_pending.Count == 0) return;
            var take = Math.Clamp(_pending.Count / 8, 1, MaxPerFlush);
            batch = new List<LogStore.Entry>(take);
            for (var i = 0; i < take; i++) batch.Add(_pending.Dequeue());
        }
        //Whether to follow is decided by _follow, which is changed only by actual user scrolling, see OnScrollChanged
        var animate = batch.Count <= AnimatedBatchLimit;
        //One notification carries the whole batch, adding one by one would run container logic per line
        var rows = new List<LogRow>(batch.Count);
        foreach (var entry in batch) rows.Add(MakeRow(entry, animate));
        _view.AddRange(rows);
        TrimToLimit();
        //Following continues only when stuck to the bottom, equivalent to vanilla's shouldScroll check
        if (_follow) ScrollToEnd();
    }

    //TrimToLimit trims from the head in one batch when over the line cap
    //RemoveAt(0) per line shifts the array and emits a notification each time, a batch trim emits once
    private void TrimToLimit()
    {
        var over = _view.Count - MaxLines;
        if (over > 0) _view.RemoveRange(0, over);
    }

    //ScrollToEnd scrolls to the newest line
    //It does nothing when already at the bottom: forcing a scroll every frame is itself a jitter source and under virtualization scrolling also triggers a layout pass
    //When the scrollbar is unavailable it falls back to ScrollIntoView, taken when the list is not yet attached to the visual tree
    private void ScrollToEnd()
    {
        if (Scroll is not { } scroll)
        {
            if (_view.Count > 0) LogLines.ScrollIntoView(_view[^1]);
            return;
        }
        var target = Math.Max(0, scroll.Extent.Height - scroll.Viewport.Height);
        if (target - scroll.Offset.Y < 0.5) return;
        scroll.Offset = new Vector(scroll.Offset.X, target);
    }

    //OnScrollChanged updates the follow state when the viewport is genuinely moved
    //Filter out passive movement by the Extent/Viewport delta first: inserting a new line or trimming an old one changes Extent
    //That change also drags Offset along and is different from a user dragging the viewport, judging them together would turn off following by itself
    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (e.Source is ScrollViewer scroll) _scroll = scroll;
        if (_scroll is null) return;
        if (Math.Abs(e.ExtentDelta.Y) > 0.5 || Math.Abs(e.ViewportDelta.Y) > 0.5) return;
        var y = _scroll.Offset.Y;
        if (Math.Abs(y - _lastOffsetY) < 0.5) return;
        //At the bottom (or near it) following is always restored, scrolling up stops it, and any other case (programmatic scroll down) keeps the status quo
        //It must not be written as "set false when not at the bottom": the scroll position may not land exactly on the last pixel
        //One frame judged false is unrecoverable, the user does not move Offset while Extent keeps growing, so it is flung off the whole way
        var up = y < _lastOffsetY;
        _lastOffsetY = y;
        if (y + _scroll.Viewport.Height >= _scroll.Extent.Height - BottomTolerance) _follow = true;
        else if (up) _follow = false;
    }

    //MakeRow parses one log line, the actual text control is created on demand by virtualization
    //The color spans stay on the row object and are reused when a container is recycled after scrolling off screen, no reparse needed
    //When animate is true the row plays an entrance animation on entering the visible area, a few hundred history lines at once skip it since that would slow window opening
    private static LogRow MakeRow(LogStore.Entry entry, bool animate)
        => new(entry.Text, entry.Level, animate);

    //OnRowPrepared plays the entrance animation the first time a new row enters the visible area
    //Virtualization recycles containers that scroll off screen and reattachment is common during scrolling, IsNew gates it to truly new rows
    private void OnRowPrepared(object? sender, ContainerPreparedEventArgs e)
    {
        if (e.Container is not ListBoxItem item || item.DataContext is not LogRow row || !row.IsNew) return;
        row.IsNew = false;
        //Write the start value locally first so the frame before the animation does not flash a fully drawn row
        //PlayAppear writes the end value back locally when done, otherwise the property falls back to 0 and the whole row disappears
        item.Opacity = 0;
        item.RenderTransform = new TranslateTransform(0, 6);
        LogRowAnimation.Play(item);
    }

    private void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        var command = CommandInput.Text?.Trim();
        CommandInput.Text = string.Empty;
        if (string.IsNullOrEmpty(command)) return;
        Execute(command);
    }

    //Execute dispatches the command through the console source for the main loop tick to pick up
    //Runs on neither the UI thread nor a connection thread, only the main loop thread has consistent world state, and the reply goes back to logs per the command source
    private void Execute(string command)
        => _server.EnqueueConsoleCommand(ServerCommandSource.Console(_server), command);
}

//LogRow, one line in the log panel, the raw text and color spans are stored together
//Virtualized containers are recycled when scrolled off screen and reused, the spans stay on the row and are reused on reattachment without reparsing
public sealed class LogRow
{
    private readonly string _line;
    private IReadOnlyList<AnsiLogParser.LogSpan>? _spans;
    private string? _plainText;

    public LogRow(string line, LogLevel level, bool animate)
    {
        _line = line;
        Level = level;
        IsNew = animate;
    }

    //Line the raw text, useful for finding rows by content during investigation, it still carries ANSI color codes
    public string Line => _line;

    //Level the level of this line, the log page filters by it, the level is parsed once by LogStore and carried directly on the row object
    public LogLevel Level { get; }

    //Spans the color spans, parsed once and kept
    //After the cache grows to twenty thousand lines, parsing at construction would slow window opening, and the list is virtualized so only the few dozen visible rows actually request it
    //So parsing is deferred to first use
    public IReadOnlyList<AnsiLogParser.LogSpan> Spans => _spans ??= AnsiLogParser.Parse(_line);

    //PlainText the visible text with ANSI removed, search runs on it
    public string PlainText
    {
        get
        {
            if (_plainText is not null) return _plainText;
            var text = new StringBuilder();
            foreach (var span in Spans) text.Append(span.Text);
            return _plainText = text.ToString();
        }
    }

    //IsNew has not played the entrance animation yet, a recycled and reused container must not play it again
    public bool IsNew { get; set; }

    //Matches the ranges of the search hits on PlainText, rendering uses them to pick out the hit characters and highlight them
    public IReadOnlyList<(int Start, int Length)> Matches { get; set; } = Array.Empty<(int, int)>();

    //InView whether this row is currently in the log page's view, trimming old rows uses it to remove from the list in sync so the view and the full buffer stay aligned
    public bool InView { get; set; }
}

//LogRowSpans pours a row's color spans and match ranges into a TextBlock's Inlines
//TextBlock.Inlines itself cannot be bound and the control generated by ItemTemplate is not on ListBoxItem.Content
//As attached properties the change callback lands exactly when the template instance has data bound, and it re-enters when a container is reattached to another row
public static class LogRowSpans
{
    public static readonly AttachedProperty<IReadOnlyList<AnsiLogParser.LogSpan>?> SpansProperty =
        AvaloniaProperty.RegisterAttached<TextBlock, IReadOnlyList<AnsiLogParser.LogSpan>?>(
            "Spans", typeof(TextBlock));

    public static readonly AttachedProperty<IReadOnlyList<(int Start, int Length)>?> MatchesProperty =
        AvaloniaProperty.RegisterAttached<TextBlock, IReadOnlyList<(int Start, int Length)>?>(
            "Matches", typeof(TextBlock));

    //Background and foreground for hit text, bright yellow on a dark background with dark text pops most without covering the whole line
    private static readonly IBrush MatchBackground = new SolidColorBrush(Color.Parse("#FFD54A"));
    private static readonly IBrush MatchForeground = new SolidColorBrush(Color.Parse("#1E1F22"));

    static LogRowSpans()
    {
        //Whichever property arrives first redraws the whole line, binding update order is not guaranteed and this keeps the result consistent
        SpansProperty.Changed.AddClassHandler<TextBlock>((block, _) => Render(block));
        MatchesProperty.Changed.AddClassHandler<TextBlock>((block, _) => Render(block));
    }

    public static void SetSpans(TextBlock target, IReadOnlyList<AnsiLogParser.LogSpan>? value)
        => target.SetValue(SpansProperty, value);

    public static IReadOnlyList<AnsiLogParser.LogSpan>? GetSpans(TextBlock target)
        => target.GetValue(SpansProperty);

    public static void SetMatches(TextBlock target, IReadOnlyList<(int Start, int Length)>? value)
        => target.SetValue(MatchesProperty, value);

    public static IReadOnlyList<(int Start, int Length)>? GetMatches(TextBlock target)
        => target.GetValue(MatchesProperty);

    //Render writes Inlines per span, segments falling in a match range get the highlight style
    //Spans are colored while match ranges are given against the visible text, so the written character count is tracked along the way to align the two
    private static void Render(TextBlock block)
    {
        block.Inlines!.Clear();
        var spans = block.GetValue(SpansProperty);
        if (spans is null) return;
        var matches = block.GetValue(MatchesProperty);
        var offset = 0;
        foreach (var span in spans)
        {
            var text = span.Text;
            var length = text.Length;
            if (matches is null || matches.Count == 0 || length == 0)
            {
                if (length > 0) block.Inlines.Add(new Run(text) { Foreground = span.Brush });
                offset += length;
                continue;
            }
            //This text covers [offset, offset+length), the part of a match range falling inside is sliced out separately
            var cursor = 0;
            foreach (var (start, matchLength) in matches)
            {
                var from = Math.Max(start, offset) - offset;
                var to = Math.Min(start + matchLength, offset + length) - offset;
                if (to <= from) continue;
                if (from > cursor) block.Inlines.Add(new Run(text[cursor..from]) { Foreground = span.Brush });
                block.Inlines.Add(new Run(text[from..to])
                {
                    Foreground = MatchForeground,
                    Background = MatchBackground,
                });
                cursor = to;
            }
            if (cursor < length) block.Inlines.Add(new Run(text[cursor..]) { Foreground = span.Brush });
            offset += length;
        }
    }
}
