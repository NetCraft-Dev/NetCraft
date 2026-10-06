using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Threading;
using NetCraft.Game;
using NetCraft.Game.Server;
using NetCraft.Primitives;
using NetCraft.Storage;

namespace NetCraft.Server.Gui;

//ServerChunkPage, the chunk page, the left side is generation tasks and the player list, the right side is a grid centered on some chunk
//The grid only covers chunks with a built holder, there is no holder beyond the view distance and not drawing means unloaded
//The center has three sources: the default spawn, following a player, and a manual jump or drag, only following moves on its own at any moment
public sealed partial class ServerChunkPage : UserControl
{
    //Pixels of cell size changed per zoom step
    private const double ZoomStep = 2;
    //Fade duration for list items
    private const int FadeMilliseconds = 200;

    private readonly MinecraftServer _server;
    //_followName the player being followed, empty means the center is decided by _manualX/_manualZ
    //Clicking a player starts following, clicking again, dragging the map, or jumping to a chunk all exit following
    private string? _followName;
    //_manualX/_manualZ the manual center, during following it also records the target player's chunk so the center stays in place when they go offline
    private double _manualX;
    private double _manualZ;
    private bool _centerReady;

    //_shownPlayers/_shownSelected the player list and follow target drawn last time
    //Rebuilds buttons only when both changed, otherwise rebuilding every 500ms shakes the hover state
    private List<string> _shownPlayers = new();
    private string? _shownSelected;
    //_taskItems generation task items, reused by coordinate string, only the ones actually added or removed are touched
    private readonly Dictionary<string, TextBlock> _taskItems = new();

    //_cells/_players reuse the same buffer, cleared and refilled each frame without reallocating
    private readonly List<(ChunkPos Pos, bool Strong)> _cells = new();
    private readonly List<(ChunkPos Pos, string Name)> _players = new();

    public ServerChunkPage(MinecraftServer server)
    {
        _server = server;
        InitializeComponent();

        Focusable = true;
        //The wheel handles zoom, but only with Ctrl held
        ChunkGrid.PointerWheelChanged += OnWheel;
        //Keyboard zoom needs a click first to take focus
        ChunkGrid.PointerPressed += (_, _) => Focus();
        //Dragging the map exits following and syncs the center back to the page
        ChunkGrid.CenterPanned += OnCenterPanned;
        ChunkGrid.HoverChanged += OnHoverChanged;
        KeyDown += OnKeyDown;

        GotoButton.Click += OnGoto;
        GotoX.KeyDown += OnGotoKey;
        GotoZ.KeyDown += OnGotoKey;

        UpdateScaleLabel();
    }

    //Refresh is driven by the window's 500ms poll, same frequency as the vanilla stats panel
    public void Refresh()
    {
        //The level does not exist yet when the main loop is not up, leave this page blank
        if (!_server.Running) return;

        //The initial center is set the first time the spawn is available, afterwards only following and manual operations change the center
        if (!_centerReady)
        {
            var spawn = _server.SpawnPos;
            _manualX = ToChunk(spawn.X);
            _manualZ = ToChunk(spawn.Z);
            _centerReady = true;
        }

        DropOfflineFollow();
        RebuildPlayerList();
        RebuildGenerationList();

        var source = _server.Overworld.ChunkSource;
        var (centerX, centerZ) = ResolveCenter();
        FillCells(source);
        ChunkGrid.SetSnapshot(centerX, centerZ, _cells, _players);

        //Put the holder and drawn counts out in the open, when the grid is blank one can tell at a glance whether there is no data or nothing drawn
        CenterLabel.Text = Loc.Format("netcraft.gui.chunk.center",
            Math.Round(centerX), Math.Round(centerZ), source.HoldersCount, _cells.Count);
    }

    //OnWheel Ctrl plus wheel zoom
    private void OnWheel(object? sender, PointerWheelEventArgs e)
    {
        if (!e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.Delta.Y == 0) return;
        Zoom(e.Delta.Y > 0 ? ZoomStep : -ZoomStep);
        e.Handled = true;
    }

    //OnKeyDown Ctrl plus arrow keys or plus/minus zoom
    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (!e.KeyModifiers.HasFlag(KeyModifiers.Control)) return;
        switch (e.Key)
        {
            case Key.Up:
            case Key.OemPlus:
            case Key.Add:
                Zoom(ZoomStep);
                e.Handled = true;
                break;
            case Key.Down:
            case Key.OemMinus:
            case Key.Subtract:
                Zoom(-ZoomStep);
                e.Handled = true;
                break;
        }
    }

    private void Zoom(double delta)
    {
        var next = Math.Clamp(ChunkGrid.CellSize + delta, ChunkGridView.MinCellSize, ChunkGridView.MaxCellSize);
        if (Math.Abs(next - ChunkGrid.CellSize) < 0.001) return;
        ChunkGrid.CellSize = next;
        UpdateScaleLabel();
    }

    private void UpdateScaleLabel()
        => ScaleLabel.Text = Loc.Format("netcraft.gui.chunk.scale",
            ChunkGrid.CellSize.ToString("0.#", CultureInfo.CurrentCulture));

    //OnGoto jumps to a given chunk, the input is chunk coordinates matching the numbers in brackets in the task list on the left
    private void OnGoto(object? sender, RoutedEventArgs e)
    {
        //Only acts when both fields are valid, otherwise the cursor is still on a half-typed number and jumping away would be baffling
        if (!int.TryParse(GotoX.Text?.Trim(), out var x)) return;
        if (!int.TryParse(GotoZ.Text?.Trim(), out var z)) return;
        _followName = null;
        _manualX = x;
        _manualZ = z;
        Refresh();
    }

    //OnGotoKey Enter in a coordinate field is the same as clicking go
    private void OnGotoKey(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        OnGoto(sender, e);
        e.Handled = true;
    }

    //OnHoverChanged refreshes the info bar when the hovered cell changes, players standing in the cell are named too
    private void OnHoverChanged(ChunkPos? pos, IReadOnlyList<string> names)
    {
        if (pos is not { } chunk)
        {
            HoverLabel.Text = string.Empty;
            return;
        }
        HoverLabel.Text = names.Count == 0
            ? Loc.Format("netcraft.gui.chunk.hover", chunk.X, chunk.Z)
            : Loc.Format("netcraft.gui.chunk.hover_players", chunk.X, chunk.Z, string.Join(", ", names));
    }

    //OnCenterPanned changes the center by dragging, wanting to look yourself means no longer following anyone, so it exits following
    private void OnCenterPanned(double x, double z)
    {
        _manualX = x;
        _manualZ = z;
        if (_followName is null) return;
        _followName = null;
        //The selected mark on the buttons must be cleared too, this line short-circuits by itself when the name is unchanged
        RebuildPlayerList();
    }

    //ResolveCenter takes the followed player's chunk if online and records it, so the center lands on that cell if they go offline
    private (double X, double Z) ResolveCenter()
    {
        if (_followName is not null)
        {
            var player = _server.PlayerList.Players.FirstOrDefault(p => p.Profile.Name == _followName);
            if (player is not null)
            {
                _manualX = ToChunk(player.Position.X);
                _manualZ = ToChunk(player.Position.Z);
            }
        }
        return (_manualX, _manualZ);
    }

    //DropOfflineFollow stops following when the followed player is gone, the center stays at _manualX/_manualZ and no longer moves
    private void DropOfflineFollow()
    {
        if (_followName is null) return;
        if (_server.PlayerList.Players.Any(p => p.Profile.Name == _followName)) return;
        _followName = null;
    }

    private void RebuildPlayerList()
    {
        var players = _server.PlayerList.Players;
        var names = players.Select(p => p.Profile.Name).ToList();
        if (names.SequenceEqual(_shownPlayers) && _followName == _shownSelected) return;
        _shownPlayers = names;
        _shownSelected = _followName;

        PlayerList.Children.Clear();
        foreach (var player in players)
        {
            var name = player.Profile.Name;
            var toggle = new ToggleButton
            {
                Content = name,
                IsChecked = name == _followName,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Classes = { "playerToggle" },
            };
            var captured = player;
            toggle.Click += (_, _) => SelectPlayer(captured);
            PlayerList.Children.Add(toggle);
        }
    }

    //SelectPlayer starts following on click, clicking the same one cancels, after cancelling the center stays on the current view and no longer chases
    private void SelectPlayer(ServerPlayer player)
    {
        var name = player.Profile.Name;
        if (_followName == name)
        {
            //Freezing in place takes the current rendered center rather than the player position, otherwise the view slides forward one more time at the moment of cancelling
            var (x, z) = ChunkGrid.CurrentCenter;
            _manualX = x;
            _manualZ = z;
            _followName = null;
        }
        else _followName = name;

        //The rebuild is posted after this click dispatch finishes, otherwise the button handling the click would clear itself
        Dispatcher.UIThread.Post(() =>
        {
            RebuildPlayerList();
            Refresh();
        }, DispatcherPriority.Background);
    }

    //RebuildGenerationList lists chunks submitted for load but not yet finished, these are the terrain generations in progress
    private void RebuildGenerationList()
    {
        var source = _server.Overworld.ChunkSource;
        var tasks = new List<string>();
        foreach (var holder in source.Holders)
        {
            if (!holder.WasScheduled || holder.IsDone) continue;
            tasks.Add($"[{holder.Pos.X}, {holder.Pos.Z}]");
        }
        tasks.Sort(StringComparer.Ordinal);

        GenerationSummary.Text = Loc.Format("netcraft.gui.chunk.generating",
            tasks.Count, source.LoadedCount, source.HoldersCount, source.ViewDistance);

        SyncTaskItems(tasks);
    }

    //SyncTaskItems maintains task items incrementally against the new list, only the ones actually added or removed are touched
    //Clearing and rebuilding the whole column makes unchanged items flicker and the animation becomes messier
    private void SyncTaskItems(List<string> tasks)
    {
        var wanted = new HashSet<string>(tasks);
        //Vanished items fade out first and are removed after the transition, a direct removal disappears instantly
        var stale = _taskItems.Keys.Where(key => !wanted.Contains(key)).ToList();
        foreach (var key in stale)
        {
            var item = _taskItems[key];
            _taskItems.Remove(key);
            FadeOut(item);
        }

        for (var i = 0; i < tasks.Count; i++)
        {
            var key = tasks[i];
            if (_taskItems.TryGetValue(key, out var existing))
            {
                //When the order changes it moves position, the moved item itself does not change and only the neighbors shift aside
                var at = GenerationList.Children.IndexOf(existing);
                if (at != i) GenerationList.Children.Move(at, i);
                continue;
            }

            var item = new TextBlock { Text = key, Classes = { "monoValue" }, Opacity = 0 };
            ApplyFade(item);
            _taskItems[key] = item;
            GenerationList.Children.Insert(Math.Min(i, GenerationList.Children.Count), item);
            //Enter the tree fully transparent and raise it on the next tick so the transition actually runs
            Dispatcher.UIThread.Post(() => item.Opacity = 1, DispatcherPriority.Background);
        }
    }

    //ApplyFade makes opacity changes go through a transition instead of snapping
    private static void ApplyFade(Control item)
        => item.Transitions = new Transitions
        {
            new DoubleTransition
            {
                Property = Visual.OpacityProperty,
                Duration = TimeSpan.FromMilliseconds(FadeMilliseconds),
            },
        };

    //FadeOut fades then removes, the wait is slightly longer than the transition so it is not removed before the animation finishes
    private static void FadeOut(Control item)
    {
        item.Opacity = 0;
        DispatcherTimer.RunOnce(() =>
        {
            if (item.Parent is Panel panel) panel.Children.Remove(item);
        }, TimeSpan.FromMilliseconds(FadeMilliseconds + 60));
    }

    //FillCells turns the holder table into grid cells, holders whose ticket level is outside the ticking tier are not drawn
    //Only block-ticking and closer are drawn: outer holders are the border of the loaded range and show as unloaded on the grid
    private void FillCells(ServerChunkCache source)
    {
        _cells.Clear();
        foreach (var holder in source.Holders)
        {
            if (!ChunkLevel.IsBlockTicking(holder.TicketLevel)) continue;
            //Strong or weak is decided by the simulation level: beyond the simulation distance chunks load without ticking, that ring is weak loading
            _cells.Add((holder.Pos, source.InEntityTickingRange(holder.Pos.Pack())));
        }

        _players.Clear();
        foreach (var player in _server.PlayerList.Players)
            _players.Add((new ChunkPos((int)ToChunk(player.Position.X), (int)ToChunk(player.Position.Z)),
                player.Profile.Name));
    }

    //ToChunk world coordinate to chunk coordinate, floor first then shift right so negatives land in the correct cell
    private static double ToChunk(double value) => (int)Math.Floor(value) >> 4;
}
