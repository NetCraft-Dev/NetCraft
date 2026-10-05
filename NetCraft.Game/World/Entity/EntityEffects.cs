using NetCraft.Game.World.Effect;
using NetCraft.Registry;

namespace NetCraft.Game.World.Entity;

//IEffectHolder 持活跃药水效果的实体 对应原版 LivingEntity.getActiveEffectsMap 那一层能力
//本作没有 LivingEntity 由 Mob 与 Player 各自实现
public interface IEffectHolder
{
    //Effects 活跃药水效果容器
    EntityEffects Effects { get; }
}

//EntityEffects 活跃药水效果容器 按效果句柄存实例
public sealed class EntityEffects
{
    private readonly Dictionary<Holder<NetCraft.Registry.MobEffect>, MobEffectInstance> _effects = new();

    //Map 只读视图 供实体谓词读取
    public IReadOnlyDictionary<Holder<NetCraft.Registry.MobEffect>, MobEffectInstance> Map => _effects;

    //Add 新增或覆盖一条效果
    public void Add(Holder<NetCraft.Registry.MobEffect> effect, MobEffectInstance instance)
        => _effects[effect] = instance;

    //Remove 移除一条效果 不存在返回 false
    public bool Remove(Holder<NetCraft.Registry.MobEffect> effect) => _effects.Remove(effect);

    //Get 取一条效果实例 没有给 null
    public MobEffectInstance? Get(Holder<NetCraft.Registry.MobEffect> effect)
        => _effects.TryGetValue(effect, out var instance) ? instance : null;

    //Clear 清空全部效果
    public void Clear() => _effects.Clear();
}
