using NetCraft.Game.World.Items;
using NetCraft.Network;
using NetCraft.Registry.State;

namespace NetCraft.Game.World.Particle;

//ParticleType 粒子类型对应原版 net.minecraft.core.particles.ParticleType
//实现 Registry 层的占位接口以便登记进 PARTICLE_TYPE 注册表 类型特定参数编解码由子类实现
public abstract class ParticleType : NetCraft.Registry.ParticleType<object>
{
    protected ParticleType(bool overrideLimiter) => OverrideLimiter = overrideLimiter;

    //OverrideLimiter 是否绕过粒子数量限制 对应原版 getOverrideLimiter
    public bool OverrideLimiter { get; }

    //WriteParameters 写类型特定参数 无参数类型不写对应原版 streamCodec 的编码段
    public abstract void WriteParameters(RegistryFriendlyByteBuf buf, ParticleOptions options);

    //ReadParameters 读回类型特定参数 对应原版 streamCodec 的解码段
    public abstract ParticleOptions ReadParameters(RegistryFriendlyByteBuf buf);
}

//SimpleParticleType 无参数粒子类型对应原版 SimpleParticleType
public sealed class SimpleParticleType : ParticleType
{
    private readonly SimpleParticleOption _options;

    public SimpleParticleType(bool overrideLimiter) : base(overrideLimiter)
        => _options = new SimpleParticleOption(this);

    //Options 该类型唯一的选项实例 命令与网络都用它
    public SimpleParticleOption Options => _options;

    public override void WriteParameters(RegistryFriendlyByteBuf buf, ParticleOptions options) { }

    public override ParticleOptions ReadParameters(RegistryFriendlyByteBuf buf) => _options;
}

//BlockParticleType 携方块状态参数覆盖 block/block_marker/falling_dust/dust_pillar/block_crumble
//对应原版 BlockParticleOption.streamCodec 写全局方块状态 id
public sealed class BlockParticleType(bool overrideLimiter) : ParticleType(overrideLimiter)
{
    public override void WriteParameters(RegistryFriendlyByteBuf buf, ParticleOptions options)
        => buf.WriteVarInt(((BlockParticleOption)options).State.Id);

    public override ParticleOptions ReadParameters(RegistryFriendlyByteBuf buf)
        => new BlockParticleOption(this, BlockStateRegistry.GetState(buf.ReadVarInt()));
}

//ItemParticleType 携物品栈参数对应原版 ItemParticleOption.streamCodec
public sealed class ItemParticleType(bool overrideLimiter) : ParticleType(overrideLimiter)
{
    public override void WriteParameters(RegistryFriendlyByteBuf buf, ParticleOptions options)
        => ItemStack.StreamCodec.Encode(buf, ((ItemParticleOption)options).Item);

    public override ParticleOptions ReadParameters(RegistryFriendlyByteBuf buf)
        => new ItemParticleOption(this, ItemStack.StreamCodec.Decode(buf));
}

//DustParticleType 颜色与缩放参数对应原版 DustParticleOptions.streamCodec
public sealed class DustParticleType(bool overrideLimiter) : ParticleType(overrideLimiter)
{
    public override void WriteParameters(RegistryFriendlyByteBuf buf, ParticleOptions options)
    {
        var dust = (DustParticleOptions)options;
        buf.WriteInt(dust.Color);
        buf.WriteFloat(dust.Scale);
    }

    public override ParticleOptions ReadParameters(RegistryFriendlyByteBuf buf)
        => new DustParticleOptions(this, buf.ReadInt(), buf.ReadFloat());
}

//DustColorTransitionParticleType 两端颜色与缩放参数对应原版 DustColorTransitionOptions.streamCodec
public sealed class DustColorTransitionParticleType(bool overrideLimiter) : ParticleType(overrideLimiter)
{
    public override void WriteParameters(RegistryFriendlyByteBuf buf, ParticleOptions options)
    {
        var transition = (DustColorTransitionOptions)options;
        buf.WriteInt(transition.FromColor);
        buf.WriteInt(transition.ToColor);
        buf.WriteFloat(transition.Scale);
    }

    public override ParticleOptions ReadParameters(RegistryFriendlyByteBuf buf)
        => new DustColorTransitionOptions(this, buf.ReadInt(), buf.ReadInt(), buf.ReadFloat());
}

//ColorParticleType 单颜色参数对应原版 ColorParticleOption.streamCodec
public sealed class ColorParticleType(bool overrideLimiter) : ParticleType(overrideLimiter)
{
    public override void WriteParameters(RegistryFriendlyByteBuf buf, ParticleOptions options)
        => buf.WriteInt(((ColorParticleOption)options).Color);

    public override ParticleOptions ReadParameters(RegistryFriendlyByteBuf buf)
        => new ColorParticleOption(this, buf.ReadInt());
}

//UnsupportedParticleType 占位粒子类型 只保证注册表 id 与客户端对齐 参数编解码依赖未移植的子系统
public sealed class UnsupportedParticleType(string name, bool overrideLimiter) : ParticleType(overrideLimiter)
{
    //Name 注册名 报错信息用
    public string Name { get; } = name;

    public override void WriteParameters(RegistryFriendlyByteBuf buf, ParticleOptions options)
        => throw new NotSupportedException($"粒子 {Name} 的参数编码未实现");

    public override ParticleOptions ReadParameters(RegistryFriendlyByteBuf buf)
        => throw new NotSupportedException($"粒子 {Name} 的参数解码未实现");
}
