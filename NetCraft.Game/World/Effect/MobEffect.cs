namespace NetCraft.Game.World.Effect;

//MobEffect mob effect base class, maps to vanilla net.minecraft.world.effect.MobEffect
//Implements the Registry-layer placeholder interface NetCraft.Registry.MobEffect so effects can enter the MOB_EFFECT registry
//The same namespace already has a MobEffect class, so registry element types must be referenced with the fully qualified name
public class MobEffect : NetCraft.Registry.MobEffect
{
    //Category effect category; decides beneficial/harmful/neutral
    public MobEffectCategory Category { get; }

    //Color effect color; the client tints the icon and particles with it
    public int Color { get; }

    public MobEffect(MobEffectCategory category, int color)
    {
        Category = category;
        Color = color;
    }

    //IsInstantaneous whether it is an instantaneous effect; instantaneous effects have no duration and do not tick
    public virtual bool IsInstantaneous => false;

    //IsBeneficial whether it is a positive effect
    public bool IsBeneficial => Category == MobEffectCategory.Beneficial;

    //IsHarmful whether it is a negative effect
    public bool IsHarmful => Category == MobEffectCategory.Harmful;

    //NeedsBlend whether it needs to fade in/out on the client; set by SetBlendDuration
    public bool NeedsBlend { get; private set; }

    //SetBlendDuration marks the effect as needing blend rendering, maps to the three duration arguments of vanilla setBlendDuration
    public MobEffect SetBlendDuration(int duration)
    {
        NeedsBlend = true;
        return this;
    }
}

//InstantaneousMobEffect instantaneous effect base class, maps to vanilla InstantaneousMobEffect
public class InstantaneousMobEffect : MobEffect
{
    public InstantaneousMobEffect(MobEffectCategory category, int color) : base(category, color) { }

    public override bool IsInstantaneous => true;
}

//HealOrHarmMobEffect instant heal/harm effect, maps to vanilla HealOrHarmMobEffect
public sealed class HealOrHarmMobEffect : InstantaneousMobEffect
{
    public HealOrHarmMobEffect(MobEffectCategory category, int color) : base(category, color) { }
}
