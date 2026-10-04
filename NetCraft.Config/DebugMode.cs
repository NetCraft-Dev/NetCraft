namespace NetCraft.Config;

//DebugMode 全局调试模式开关对应原版 SharedConstants.IS_DEBUG
//运行时可变由 Loader --debug flag 设置或测试代码设置
//各子系统检查 IsEnabled 开启额外调试行为如启动信息/详细日志/断言
public static class DebugMode
{
    public static bool IsEnabled { get; set; }
}
