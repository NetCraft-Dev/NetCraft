namespace NetCraft.Registry;

//WorldClock 世界时钟标记类型对应原版 net.minecraft.world.clock.WorldClock
//空 record 仅作为注册表 key 的载体 每个维度可关联一个默认时钟
//运行时状态由 Game 层 ServerClockManager 按 Holder 索引维护
public sealed class WorldClock
{
}
