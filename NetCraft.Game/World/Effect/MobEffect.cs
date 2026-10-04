namespace NetCraft.Game.World.Effect;

//MobEffect 药水效果基类 对应原版 net.minecraft.world.effect.MobEffect
//实现 Registry 层占位接口 NetCraft.Registry.MobEffect 让效果可进 MOB_EFFECT 注册表
//同命名空间已有 MobEffect 类 引用注册表元素类型时必须写全限定名
public class MobEffect : NetCraft.Registry.MobEffect
{
    //Category 效果分类 决定有益/有害/中性
    public MobEffectCategory Category { get; }

    //Color 效果颜色 客户端按它上色图标与粒子
    public int Color { get; }

    public MobEffect(MobEffectCategory category, int color)
    {
        Category = category;
        Color = color;
    }

    //IsInstantaneous 是否瞬时效果 瞬时效果没有持续时间不逐刻结算
    public virtual bool IsInstantaneous => false;

    //IsBeneficial 是否正面效果
    public bool IsBeneficial => Category == MobEffectCategory.Beneficial;

    //IsHarmful 是否负面效果
    public bool IsHarmful => Category == MobEffectCategory.Harmful;

    //NeedsBlend 是否需要在客户端淡入淡出 由 SetBlendDuration 置位
    public bool NeedsBlend { get; private set; }

    //SetBlendDuration 标记该效果需要混合渲染 对应原版 setBlendDuration 的三个时长参数
    public MobEffect SetBlendDuration(int duration)
    {
        NeedsBlend = true;
        return this;
    }
}

//InstantaneousMobEffect 瞬时效果基类 对应原版 InstantaneousMobEffect
public class InstantaneousMobEffect : MobEffect
{
    public InstantaneousMobEffect(MobEffectCategory category, int color) : base(category, color) { }

    public override bool IsInstantaneous => true;
}

//HealOrHarmMobEffect 瞬间治疗/伤害效果 对应原版 HealOrHarmMobEffect
public sealed class HealOrHarmMobEffect : InstantaneousMobEffect
{
    public HealOrHarmMobEffect(MobEffectCategory category, int color) : base(category, color) { }
}
