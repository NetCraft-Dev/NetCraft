namespace NetCraft.Registry;

//SoundSource sound category, maps to vanilla net.minecraft.sounds.SoundSource
//Member order is the network transmission index; do not change
public enum SoundSource
{
    Master,
    Music,
    Records,
    Weather,
    Blocks,
    Hostile,
    Neutral,
    Players,
    Ambient,
    Voice,
    Ui,
}
