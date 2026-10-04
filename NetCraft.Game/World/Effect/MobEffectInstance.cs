namespace NetCraft.Game.World.Effect;

//MobEffectInstance 药水效果实例 对应原版 net.minecraft.world.effect.MobEffectInstance
//一个实例描述某条效果在实体身上的剩余时长与等级 玩家侧按效果值存进效果表
public sealed class MobEffectInstance
{
    public MobEffectInstance(Holder<NetCraft.Registry.MobEffect> effect, int duration, int amplifier, bool ambient, bool visible)
    {
        Effect = effect;
        Duration = duration;
        Amplifier = amplifier;
        IsAmbient = ambient;
        IsVisible = visible;
        //showIcon 对齐原版取 visible
        ShowIcon = visible;
    }

    //Effect 效果注册表引用 网络编解码按它取注册表 id
    public Holder<NetCraft.Registry.MobEffect> Effect { get; }

    //Duration 剩余刻数 -1 表示无限
    public int Duration { get; private set; }

    //Amplifier 效果等级 0 对应 I 级
    public int Amplifier { get; }

    //IsAmbient 是否环境效果 信标/潮涌核心施加的效果
    public bool IsAmbient { get; }

    //IsVisible 是否显示粒子
    public bool IsVisible { get; }

    //ShowIcon 是否显示图标
    public bool ShowIcon { get; }

    //Tick 每刻递减持续时间 -1 表示无限不减
    public void Tick()
    {
        if (Duration > 0) Duration--;
    }

    //Expired 持续时间归零 对应原版效果到期
    public bool Expired => Duration == 0;
}
