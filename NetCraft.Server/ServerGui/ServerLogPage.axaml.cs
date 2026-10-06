using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NetCraft.Game;
using NetCraft.Logging;

namespace NetCraft.Server.Gui;

//ServerLogPage, the log page, level filters on the left, regex search on top, filtered logs below
//Shares the LogStore buffer with the main page log area and uses the same rendering, the only difference is the missing command input area
//Filters and search affect only this page, the main page stays full
public sealed partial class ServerLogPage : UserControl
{
    //Display cap, aligned with the LogStore cache cap, overflow drops from the head
    private const int MaxLines = 20000;
    //Release a small batch each frame, the same pace as the main page log area
    private static readonly TimeSpan FlushInterval = TimeSpan.FromMilliseconds(16);
    //Search debounce, reordering two thousand lines on every keystroke would make typing feel choppy
    private static readonly TimeSpan SearchDelay = TimeSpan.FromMilliseconds(160);
    private const int MaxPerFlush = 200;
    //The entrance animation runs only when the released line count is at or below this, animating during a backlog only hurts
    private const int AnimatedBatchLimit = 6;
    private const int MaxPending = 4000;
    //Max matches marked per row, a common letter can hit thousands of places in one row and there is no need to mark them all
    private const int MaxMatchesPerRow = 200;
    //Bottom tolerance, anything below it counts as at the bottom
    private const double BottomTolerance = 4;

    private readonly LogStore _store;
    //_pending logs are written by the output thread and read by the UI thread
    private readonly Queue<LogStore.Entry> _pending = new();
    private readonly Lock _pendingLock = new();
    private readonly DispatcherTimer _flush;
    private readonly DispatcherTimer _searchDelay;
    //_all every row received, filtering and search pick from it, the view is just a subset
    private readonly List<LogRow> _all = new();
    //_hits rows matching the search, ordered by view order, used for jumping up and down
    private readonly List<LogRow> _hits = new();
    private readonly RangeObservableCollection<LogRow> _view = new();
    //_active selected levels, an empty set means no filtering
    private readonly HashSet<LogLevel> _active = new();
    private Regex? _search;
    private int _hitIndex;
    //_searchDirty the input changed but has not been searched, Enter uses it to decide whether to jump to the first or next match
    private bool _searchDirty;
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

    public ServerLogPage(LogStore store)
    {
        _store = store;
        InitializeComponent();
        LogLines.ItemsSource = _view;
        //Attach to the ListBox to receive the bubbled event from the internal scrollbar, avoiding a tree walk in Loaded
        //handledEventsToo: receive it even if the scrollbar marked the event handled, otherwise it degrades to always following
        LogLines.AddHandler(ScrollViewer.ScrollChangedEvent, OnScrollChanged,
            RoutingStrategies.Bubble, handledEventsToo: true);

        BuildFilters();
        SearchBox.TextChanged += (_, _) => RestartSearch();
        SearchBox.KeyDown += OnSearchKeyDown;
        PrevButton.Click += (_, _) => JumpMatch(-1);
        NextButton.Click += (_, _) => JumpMatch(1);

        //Take all rows from the buffer during construction, the filters are empty right now which means everything is shown
        foreach (var entry in _store.Snapshot())
        {
            var row = MakeRow(entry, animate: false);
            row.InView = true;
            _all.Add(row);
        }
        _view.ReplaceAll(_all);
        UpdateMatchLabel();
        _store.LineAdded += OnLine;
        //History is filled during construction when nothing is measured yet, scrolling to the end is a no-op then
        //Post it again after layout completes, otherwise the window opens stuck on the oldest line
        LogLines.Loaded += (_, _) =>
            Dispatcher.UIThread.Post(ScrollToEnd, DispatcherPriority.Loaded);

        _flush = new DispatcherTimer { Interval = FlushInterval };
        _flush.Tick += (_, _) => FlushPending();
        _flush.Start();
        _searchDelay = new DispatcherTimer { Interval = SearchDelay };
        _searchDelay.Tick += (_, _) =>
        {
            _searchDelay.Stop();
            ApplySearch();
        };
    }

    //Detach unsubscribes, called when the window closes, otherwise this page is held forever by the logging system
    public void Detach()
    {
        _store.LineAdded -= OnLine;
        _flush.Stop();
        _searchDelay.Stop();
    }

    //BuildFilters builds the filter toggles for the available levels
    //DBG is exposed only with debug mode on, the console never emits Debug logs in normal mode and an always-dim button is pointless
    private void BuildFilters()
    {
        var levels = new List<LogLevel> { LogLevel.Info, LogLevel.Warning, LogLevel.Error, LogLevel.Critical };
        if (Log.DebugEnabled) levels.Insert(0, LogLevel.Debug);
        foreach (var level in levels)
        {
            var toggle = new ToggleButton
            {
                Content = LevelName(level),
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                Classes = { "levelToggle", LevelClass(level) },
            };
            //The closure needs this round's level, capturing the loop variable directly would make them all the last one
            //Watch the property change rather than Click: that avoids guessing whether Click fires before or after toggling IsChecked
            //So the value read is always the post-toggle one
            var captured = level;
            toggle.PropertyChanged += (_, e) =>
            {
                if (e.Property == ToggleButton.IsCheckedProperty)
                    OnFilterChanged(captured, e.GetNewValue<bool?>() == true);
            };
            Filters.Children.Add(toggle);
        }
    }

    private void OnFilterChanged(LogLevel level, bool on)
    {
        if (on) _active.Add(level);
        else _active.Remove(level);
        Rebuild();
    }

    //Rebuild rearranges the view by the current filters and search
    //A full rearrange rather than patching, both are low-frequency actions and the simpler logic is worth the cost
    private void Rebuild()
    {
        _hits.Clear();
        _hitIndex = 0;
        var shown = new List<LogRow>(_all.Count);
        foreach (var row in _all)
        {
            //Reset everything first, this row may leave the view due to a filter or search change
            row.Matches = Array.Empty<(int, int)>();
            row.InView = false;
            if (_active.Count > 0 && !_active.Contains(row.Level)) continue;
            row.InView = true;
            if (_search is not null)
            {
                var matches = FindMatches(row.PlainText);
                if (matches.Count > 0)
                {
                    row.Matches = matches;
                    _hits.Add(row);
                }
            }
            shown.Add(row);
        }
        _view.ReplaceAll(shown);
        UpdateMatchLabel();
        //After a rebuild the whole view is swapped and heights change entirely, if stuck to the bottom it must be re-pinned otherwise it stops back on the oldest line
        if (_follow) ScrollToEnd();
    }

    //RestartSearch restarts the debounce after an input change, typing continuously searches once after you stop
    private void RestartSearch()
    {
        _searchDirty = true;
        _searchDelay.Stop();
        _searchDelay.Start();
    }

    //ApplySearch compiles the regex and rearranges, an invalid regex falls back to no filtering and only marks the box red
    private void ApplySearch()
    {
        var pattern = SearchBox.Text?.Trim() ?? string.Empty;
        if (pattern.Length == 0)
        {
            _search = null;
            SearchBox.Classes.Remove("invalid");
        }
        else
        {
            try
            {
                _search = new Regex(pattern);
                SearchBox.Classes.Remove("invalid");
            }
            catch (ArgumentException)
            {
                _search = null;
                SearchBox.Classes.Add("invalid");
            }
        }
        _searchDirty = false;
        Rebuild();
    }

    private void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            //Clearing runs TextChanged once, do not trigger it again here
            SearchBox.Text = string.Empty;
            return;
        }
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        //Enter does not wait for the debounce, it searches immediately then jumps, to the first match if just changed or the next one if not
        var fresh = _searchDirty;
        _searchDelay.Stop();
        ApplySearch();
        JumpMatch(fresh ? 0 : 1);
    }

    //JumpMatch cycles through matches, the list only scrolls to visible without centering
    private void JumpMatch(int delta)
    {
        if (_hits.Count == 0) return;
        _hitIndex = (_hitIndex + delta + _hits.Count) % _hits.Count;
        LogLines.ScrollIntoView(_hits[_hitIndex]);
        UpdateMatchLabel();
    }

    private void UpdateMatchLabel()
    {
        //Hidden without a search, and a search with no match must say so, leaving it blank looks like it is still computing
        if (_search is null) MatchLabel.Text = string.Empty;
        else MatchLabel.Text = _hits.Count == 0 ? Loc.Get("netcraft.gui.log.no_match") : $"{_hitIndex + 1}/{_hits.Count}";
        //How many rows are shown versus the total, this line is the most direct way to see whether filtering works
        FilterStats.Text = Loc.Format("netcraft.gui.log.filter_stats", _view.Count, _all.Count);
    }

    //FindMatches runs the regex on the visible text and returns match ranges
    //Zero-width matches (like ^ or a*) give a position without a length, they are dropped since inserting them yields an empty highlight
    //Too many matches in one row are truncated, a common letter can hit thousands of places in one row
    private List<(int Start, int Length)> FindMatches(string text)
    {
        var result = new List<(int, int)>();
        if (_search is null || text.Length == 0) return result;
        foreach (Match match in _search.Matches(text))
        {
            if (match.Length == 0) continue;
            result.Add((match.Index, match.Length));
            if (result.Count >= MaxMatchesPerRow) break;
        }
        return result;
    }

    private void OnLine(LogStore.Entry entry)
    {
        lock (_pendingLock)
        {
            //Drop the oldest when the backlog peaks, keeping the newest batch matters when logs surge
            if (_pending.Count >= MaxPending) _pending.Dequeue();
            _pending.Enqueue(entry);
        }
    }

    //FlushPending releases a small batch of the backlog each frame
    //Rows blocked by a filter still enter the full buffer, they just do not enter the view so removing the filter later shows them again
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
        var added = new List<LogRow>(batch.Count);
        foreach (var entry in batch)
        {
            var row = MakeRow(entry, animate);
            //Only the full buffer is filled here, trimming is deferred to once after the whole batch is inserted
            //In the loop it would trim per inserted row, and at the cap every row would touch the view once, jittering badly at high rates
            _all.Add(row);
            if (_active.Count > 0 && !_active.Contains(row.Level)) continue;
            row.InView = true;
            if (_search is not null)
            {
                var matches = FindMatches(row.PlainText);
                if (matches.Count > 0)
                {
                    row.Matches = matches;
                    _hits.Add(row);
                }
            }
            added.Add(row);
        }
        TrimToLimit();
        if (added.Count > 0) _view.AddRange(added);
        //Do not touch the scrollbar when no new row enters the view, most frames are like that under filtering and an empty scroll wastes a layout pass
        if (_follow && added.Count > 0) ScrollToEnd();
        UpdateMatchLabel();
    }

    //TrimToLimit trims the oldest rows, the view and the hit table must be cleared along with it
    //_view is a subset of _all in the same order, counting how many of the first over entries are in the view allows a batch removal from the head
    private void TrimToLimit()
    {
        var over = _all.Count - MaxLines;
        if (over <= 0) return;
        var visible = 0;
        for (var i = 0; i < over; i++)
        {
            var row = _all[i];
            if (row.InView) visible++;
            if (row.Matches.Count > 0) _hits.Remove(row);
        }
        _all.RemoveRange(0, over);
        if (visible > 0) _view.RemoveRange(0, visible);
        if (_hits.Count > 0) _hitIndex = Math.Min(_hitIndex, _hits.Count - 1);
        else _hitIndex = 0;
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

    private static LogRow MakeRow(LogStore.Entry entry, bool animate)
        => new(entry.Text, entry.Level, animate);

    //OnRowPrepared plays the entrance animation the first time a new row enters the visible area
    //Virtualization recycles containers that scroll off screen and reattachment is common during scrolling, IsNew gates it to truly new rows
    private void OnRowPrepared(object? sender, ContainerPreparedEventArgs e)
    {
        if (e.Container is not ListBoxItem item || item.DataContext is not LogRow row || !row.IsNew) return;
        row.IsNew = false;
        //Write the start value locally first so the frame before the animation does not flash a fully drawn row
        item.Opacity = 0;
        item.RenderTransform = new TranslateTransform(0, 6);
        LogRowAnimation.Play(item);
    }

    private static string LevelName(LogLevel level) => level switch
    {
        LogLevel.Debug => "DBG",
        LogLevel.Warning => "WARN",
        LogLevel.Error => "ERROR",
        LogLevel.Critical => "CRIT",
        _ => "INFO",
    };

    //LevelClass the class name used for styles, matching the level color in log rows
    private static string LevelClass(LogLevel level) => level switch
    {
        LogLevel.Debug => "dbg",
        LogLevel.Warning => "warn",
        LogLevel.Error => "error",
        LogLevel.Critical => "crit",
        _ => "info",
    };
}
