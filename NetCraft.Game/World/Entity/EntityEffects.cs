using NetCraft.Game.World.Effect;
using NetCraft.Registry;

namespace NetCraft.Game.World.Entity;

//IEffectHolder entity holding active potion effects, maps to the capability of vanilla LivingEntity.getActiveEffectsMap
//Implemented by LivingEntity; Mob and Player inherit it
public interface IEffectHolder
{
    //Effects active potion effect container
    EntityEffects Effects { get; }
}

//EntityEffects active potion effect container, stores instances by effect handle
public sealed class EntityEffects
{
    private readonly Dictionary<Holder<NetCraft.Registry.MobEffect>, MobEffectInstance> _effects = new();

    //Map read-only view for entity predicates to read
    public IReadOnlyDictionary<Holder<NetCraft.Registry.MobEffect>, MobEffectInstance> Map => _effects;

    //Add adds or overwrites an effect
    public void Add(Holder<NetCraft.Registry.MobEffect> effect, MobEffectInstance instance)
        => _effects[effect] = instance;

    //Remove removes an effect, false when it is absent
    public bool Remove(Holder<NetCraft.Registry.MobEffect> effect) => _effects.Remove(effect);

    //Get returns an effect instance, null when absent
    public MobEffectInstance? Get(Holder<NetCraft.Registry.MobEffect> effect)
        => _effects.TryGetValue(effect, out var instance) ? instance : null;

    //Clear clears all effects
    public void Clear() => _effects.Clear();
}
