using System.Globalization;
using Avalonia.Controls;
using NetCraft.Game.Server;
using NetCraft.Server.Diagnostics;

namespace NetCraft.Server.Gui;

//ServerStatsPanel 统计面板 对应原版 net.minecraft.server.gui.StatsComponent
//原版只画两行文本加一张内存占用柱状图 这里保持一致
public sealed partial class ServerStatsPanel : UserControl
{
    //原版 DECIMAL_FORMAT 是 ########0.000 三位小数
    private const string TickFormat = "0.000";
    //柱图上限的步进 1GB
    //不拿物理内存当分母 16GB 机器上占用常年 1% 上下 柱子全贴地看不出变化
    private const long MemoryGraphStep = 1024L * 1024 * 1024;

    private readonly MinecraftServer _server;
    //柱图当前上限 随实际占用在 1/2/3... GB 之间升降 至少 1GB
    private long _memoryGraphCap = MemoryGraphStep;

    public ServerStatsPanel(MinecraftServer server)
    {
        _server = server;
        InitializeComponent();
    }

    //Refresh 重新采样一次 由窗口的 500ms 定时器驱动 与原版 Timer(500) 同频
    public void Refresh()
    {
        var workingSet = Environment.WorkingSet;
        //到顶抬一档 跌破上限一档再降回来
        //中间这 1GB 是迟滞区间 在阈值附近抖动时不会来回换挡
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
