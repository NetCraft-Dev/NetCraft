namespace NetCraft.Registry;

//SoundEvent 音效事件对应原版 net.minecraft.sounds.SoundEvent
//Location 指向音频资源 FixedRange 有值时表示固定可听范围
public sealed record SoundEvent(Identifier Location, float? FixedRange = null);
