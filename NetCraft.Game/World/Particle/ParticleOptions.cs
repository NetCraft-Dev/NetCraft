using NetCraft.Game.World.Items;
using NetCraft.Registry.State;

namespace NetCraft.Game.World.Particle;

//ParticleOptions 粒子选项基类对应原版 net.minecraft.core.particles.ParticleOptions
//网络与命令都按「类型 id + 类型特定参数」传递粒子选项
public abstract class ParticleOptions
{
    //Type 所属粒子类型
    public abstract ParticleType Type { get; }
}

//SimpleParticleOption 无参数粒子选项 对应原版以 SimpleParticleType 自身充当选项的写法
public sealed class SimpleParticleOption(SimpleParticleType type) : ParticleOptions
{
    public override ParticleType Type { get; } = type;
}

//BlockParticleOption 携方块状态对应原版 BlockParticleOption
public sealed class BlockParticleOption(BlockParticleType type, BlockState state) : ParticleOptions
{
    public override ParticleType Type { get; } = type;

    public BlockState State { get; } = state;
}

//ItemParticleOption 携物品栈对应原版 ItemParticleOption
public sealed class ItemParticleOption(ItemParticleType type, ItemStack item) : ParticleOptions
{
    public override ParticleType Type { get; } = type;

    public ItemStack Item { get; } = item;
}

//DustParticleOptions 颜色与缩放对应原版 DustParticleOptions
public sealed class DustParticleOptions(DustParticleType type, int color, float scale) : ParticleOptions
{
    //RedstoneParticleColor 红石默认颜色 对应原版 REDSTONE_PARTICLE_COLOR
    public const int RedstoneParticleColor = 0xFF0000;

    //MinScale/MaxScale 缩放取值范围 对应原版 ScalableParticleOptionsBase
    public const float MinScale = 0.01f;
    public const float MaxScale = 4.0f;

    public override ParticleType Type { get; } = type;

    public int Color { get; } = color;

    public float Scale { get; } = Math.Clamp(scale, MinScale, MaxScale);
}

//DustColorTransitionOptions 两端颜色与缩放对应原版 DustColorTransitionOptions
public sealed class DustColorTransitionOptions(DustColorTransitionParticleType type, int fromColor, int toColor, float scale) : ParticleOptions
{
    public override ParticleType Type { get; } = type;

    public int FromColor { get; } = fromColor;

    public int ToColor { get; } = toColor;

    public float Scale { get; } = Math.Clamp(scale, DustParticleOptions.MinScale, DustParticleOptions.MaxScale);
}

//ColorParticleOption 单颜色对应原版 ColorParticleOption 覆盖 entity_effect/tinted_leaves/flash
public sealed class ColorParticleOption(ColorParticleType type, int color) : ParticleOptions
{
    public override ParticleType Type { get; } = type;

    public int Color { get; } = color;
}
