using NetCraft.Nbt;

namespace NetCraft.Game.World.Clock;

//ClockState 单时钟持久化状态对应原版 net.minecraft.world.clock.ClockState
//NBT 字段 total_ticks/partial_tick/rate/paused 与原版 Codec 一致
public sealed record ClockState(long TotalTicks, float PartialTick, float Rate, bool Paused)
{
    public const float DefaultRate = 1f;

    //Save 写入 NBT 缺省字段按原版 optionalFieldOf 默认值补齐
    public CompoundTag Save(CompoundTag tag)
    {
        tag.PutLong("total_ticks", TotalTicks);
        tag.PutFloat("partial_tick", PartialTick);
        tag.PutFloat("rate", Rate);
        tag.PutBoolean("paused", Paused);
        return tag;
    }

    //Load 从 NBT 恢复 缺省字段对应原版默认 0/0/1/false
    public static ClockState Load(CompoundTag tag)
        => new(
            tag.GetLongValue("total_ticks"),
            tag.GetFloat("partial_tick")?.Value ?? 0f,
            tag.GetFloat("rate")?.Value ?? DefaultRate,
            tag.GetBooleanOr("paused", false));
}
