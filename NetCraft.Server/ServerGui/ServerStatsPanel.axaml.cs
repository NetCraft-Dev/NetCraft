using System.Globalization;
using Avalonia.Controls;
using NetCraft.Game.Server;
using NetCraft.Server.Diagnostics;

namespace NetCraft.Server.Gui;

//ServerStatsPanel, stats panel, maps to vanilla net.minecraft.server.gui.StatsComponent
//Vanilla draws only two lines of text plus a memory usage bar chart, kept consistent here
public sealed partial class ServerStatsPanel : UserControl
{
    //Vanilla DECIMAL_FORMAT is ########0.000, three decimal places
    private const string TickFormat = "0.000";
    //Step for the bar chart cap, 1GB
    //Physical memory is not used as the denominator, on a 16GB machine usage stays around 1% year-round and the bars would all sit flat on the floor with no visible change
    private const long MemoryGraphStep = 1024L * 1024 * 1024;

    private readonly MinecraftServer _server;
    //Current bar chart cap, rises and falls between 1/2/3... GB with actual usage, at least 1GB
    private long _memoryGraphCap = MemoryGraphStep;

    public ServerStatsPanel(MinecraftServer server)
    {
        _server = server;
        InitializeComponent();
    }

    //Refresh samples once, driven by the window's 500ms timer, same frequency as vanilla Timer(500)
    public void Refresh()
    {
        var workingSet = Environment.WorkingSet;
        //Raise one step when hitting the cap, lower it only after falling one step below the cap
        //The 1GB in between is the hysteresis band, so jitter near the threshold does not shift back and forth
        var rescale = false;
        if (workingSet >= _memoryGraphCap)
        {
            _memoryGraphCap += MemoryGraphStep;
            rescale = true;
        }
        else if (workingSet < _memoryGraphCap - MemoryGraphStep)
        {
            _memoryGraphCap -= MemoryGraphStep;
            rescale = true;
        }
        MemoryLine.Text = $"{workingSet / 1024 / 1024} mb / {_memoryGraphCap / 1024 / 1024} mb";
        var (tier1, tier2) = JitEventMonitor.TakePending();
        Memory.Push((double)workingSet / _memoryGraphCap, rescale,
            GcEventMonitor.TakePending(), tier1, tier2);

        var tickMillis = _server.AverageTickTimeNanos / 1_000_000.0;
        TickLine.Text = tickMillis.ToString(TickFormat, CultureInfo.InvariantCulture) + " ms";
    }
}
