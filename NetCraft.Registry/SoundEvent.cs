namespace NetCraft.Registry;

//SoundEvent sound event, maps to vanilla net.minecraft.sounds.SoundEvent
//Location points to the audio resource; a set FixedRange marks a fixed audible range
public sealed record SoundEvent(Identifier Location, float? FixedRange = null);
