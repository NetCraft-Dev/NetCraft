namespace NetCraft.Registry;

//ClockManager 时钟查询接口对应原版 net.minecraft.world.clock.ClockManager
//Timeline 按此接口取时钟总刻数 服务端实现为 Game 层 ServerClockManager
public interface ClockManager
{
    //GetTotalTicks 取指定时钟的累计总刻数
    long GetTotalTicks(Holder<WorldClock> definition);
}
