namespace NetCraft.Game.World;

//MobEffect mob effect enum, mirrors vanilla net.minecraft.world.effect.MobEffect
//P1 only defines the minimal set needed by forPlayer detection; extend as needed later
public enum MobEffect
{
    //Poison poison, the POISONED heart (vanilla misspells it POISIONED)
    Poison,
    //Wither wither, the WITHERED heart
    Wither,
    //Regeneration regeneration drives the heartbeat jump animation heartOffsetIndex
    Regeneration,
}
