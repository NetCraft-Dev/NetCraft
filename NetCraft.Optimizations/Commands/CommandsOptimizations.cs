using NetCraft.Config;

namespace NetCraft.Optimizations.Commands;

//Commands 优化模块对应核心优化点 2.9
//实际优化待 NetCraft.Commands 子系统重构后集成
//CommandNode 当前是 abstract class 形态保持兼容现有 LiteralCommandNode/ArgumentCommandNode 派生类
//开关启用表示采用 string.Intern + List 查询等价优化方案
public static class CommandsOptimizations
{
    public const string ModuleName = "Commands Optimization";
    public const string TargetSubsystem = "NetCraft.Commands";

    //对应优化点 2.9 brigadier CommandNode 用 readonly struct + ImmutableArray
    //开关启用表示 CommandNode 子节点查询走 List 线性查找等价 struct 索引语义
    //CommandNode 保持 class 形态避免破坏 LiteralCommandNode/ArgumentCommandNode 派生类
    public static bool IsCommandNodeStructEnabled => OptimizationFlags.CommandNodeStruct;

    //对应优化点 2.9 命令字符串 key 用 string.Intern 池化
    //开关启用表示 GetChild 按 name 查找将走 string.Intern 池化路径
    public static bool IsCommandStringInternEnabled => OptimizationFlags.CommandStringIntern;

    //IsOptimized 检查两开关是否全开判断 Commands 优化是否启用
    public static bool IsOptimized => IsCommandNodeStructEnabled && IsCommandStringInternEnabled;

    //GetStats 返回 Commands 优化统计信息用于诊断
    public static CommandsOptimizationStats GetStats() => new(
        CommandNodeStruct: IsCommandNodeStructEnabled,
        CommandStringIntern: IsCommandStringInternEnabled,
        IsOptimized: IsOptimized);
}

//Commands 优化统计快照
public readonly record struct CommandsOptimizationStats(
    bool CommandNodeStruct,
    bool CommandStringIntern,
    bool IsOptimized);
