using System.Diagnostics;

namespace NetCraft.Game.Server;

//TickStageProfiler tick 阶段计时 累计每拍各阶段耗时供 perf 命令输出占比表
//默认关闭 关闭时 Now 返回 0 Record 直接跳过 整条路径只多一次判断
//多维度时同一阶段记录多次 累计值按拍数取平均 报的是所有维度加起来的每拍开销
public static class TickStageProfiler
{
    //StageCount 阶段数 与 TickStage 枚举末位对齐
    public const int StageCount = (int)TickStage.Count;

    //秒表刻度到纳秒的换算系数
    private static readonly double NanosecondsPerTimestampTick = 1_000_000_000.0 / Stopwatch.Frequency;

    private static readonly long[] Totals = new long[StageCount];
    private static int _ticks;
    private static bool _enabled;

    public static bool Enabled => _enabled;

    //Start 清空累计并开启对应 perf start
    public static void Start()
    {
        Array.Clear(Totals);
        _ticks = 0;
        _enabled = true;
    }

    //Stop 停止累计 已有数据留着供取报告
    public static void Stop() => _enabled = false;

    //EndTick 一拍收尾 占比按拍数取平均
    public static void EndTick()
    {
        if (_enabled) _ticks++;
    }

    //Now 取计时起点 未启用返回 0 由 Record 认出来跳过
    public static long Now() => _enabled ? Stopwatch.GetTimestamp() : 0;

    //Record 记一段耗时 start 为 0 表示未启用
    public static void Record(TickStage stage, long start)
    {
        if (start != 0) Totals[(int)stage] += Stopwatch.GetTimestamp() - start;
    }

    //Report 输出各阶段每拍平均微秒与占比 跳过没走过的阶段
    //占比分母取 Tick 本体耗时 分项之和与它的差额就是没被分项盖住的部分
    public static IReadOnlyList<string> Report()
    {
        var lines = new List<string>(StageCount + 3);
        var ticks = Math.Max(1, _ticks);
        var tickTotal = Totals[(int)TickStage.TickTotal];
        var body = 0L;
        for (var i = 0; i < StageCount; i++)
        {
            if (i != (int)TickStage.TickTotal) body += Totals[i];
        }
        var basis = tickTotal > 0 ? tickTotal : body;

        lines.Add($"tick 阶段耗时 {_ticks} 拍平均 单位微秒");
        for (var i = 0; i < StageCount; i++)
        {
            if (Totals[i] == 0) continue;
            var micros = Totals[i] * NanosecondsPerTimestampTick / 1000.0 / ticks;
            var share = basis == 0 ? 0 : Totals[i] * 100.0 / basis;
            lines.Add($"  {(TickStage)i,-16}{micros,10:F1} {share,6:F1}%");
        }

        var bodyMicros = body * NanosecondsPerTimestampTick / 1000.0 / ticks;
        var bodyShare = basis == 0 ? 0 : body * 100.0 / basis;
        lines.Add($"  {"分项合计",-16}{bodyMicros,10:F1} {bodyShare,6:F1}%");
        return lines;
    }
}

//TickStage tick 内各计时段 与 DedicatedServer.Tick 的段落一一对应
//TickTotal 是 Tick 本体总耗时 用它和分项之和对照可知分项有没有漏掉大段工作
public enum TickStage
{
    TickTotal,
    Console,
    Connections,
    Clock,
    BlockTicks,
    LevelTick,
    FlushBlocks,
    RandomTick,
    BlockEvents,
    Light,
    EntityInside,
    BlockEntities,
    Players,
    DebugPlayers,
    EntityTracking,
    AutoSave,
    Count,
}
