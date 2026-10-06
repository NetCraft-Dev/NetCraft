using NetCraft.Game.World.Items;
using NetCraft.Registry.State;

namespace NetCraft.Game.World.Particle;

//ParticleOptions particle option base class, maps to vanilla net.minecraft.core.particles.ParticleOptions
//Network and commands both pass particle options as "type id + type-specific parameters"
public abstract class ParticleOptions
{
    //Type the owning particle type
    public abstract ParticleType Type { get; }
}

//SimpleParticleOption parameterless particle option, maps to the vanilla style where SimpleParticleType itself is the option
public sealed class SimpleParticleOption(SimpleParticleType type) : ParticleOptions
{
    public override ParticleType Type { get; } = type;
}

//BlockParticleOption carries a block state, maps to vanilla BlockParticleOption
public sealed class BlockParticleOption(BlockParticleType type, BlockState state) : ParticleOptions
{
    public override ParticleType Type { get; } = type;

    public BlockState State { get; } = state;
}

//ItemParticleOption carries an item stack, maps to vanilla ItemParticleOption
public sealed class ItemParticleOption(ItemParticleType type, ItemStack item) : ParticleOptions
{
    public override ParticleType Type { get; } = type;

    public ItemStack Item { get; } = item;
}

//DustParticleOptions color and scale, maps to vanilla DustParticleOptions
public sealed class DustParticleOptions(DustParticleType type, int color, float scale) : ParticleOptions
{
    //RedstoneParticleColor redstone default color, maps to vanilla REDSTONE_PARTICLE_COLOR
    public const int RedstoneParticleColor = 0xFF0000;

    //MinScale/MaxScale scale range, maps to vanilla ScalableParticleOptionsBase
    public const float MinScale = 0.01f;
    public const float MaxScale = 4.0f;

    public override ParticleType Type { get; } = type;

    public int Color { get; } = color;

    public float Scale { get; } = Math.Clamp(scale, MinScale, MaxScale);
}

//DustColorTransitionOptions two-end colors and scale, maps to vanilla DustColorTransitionOptions
public sealed class DustColorTransitionOptions(DustColorTransitionParticleType type, int fromColor, int toColor, float scale) : ParticleOptions
{
    public override ParticleType Type { get; } = type;

    public int FromColor { get; } = fromColor;

    public int ToColor { get; } = toColor;

    public float Scale { get; } = Math.Clamp(scale, DustParticleOptions.MinScale, DustParticleOptions.MaxScale);
}

//ColorParticleOption single color, maps to vanilla ColorParticleOption, covering entity_effect/tinted_leaves/flash
public sealed class ColorParticleOption(ColorParticleType type, int color) : ParticleOptions
{
    public override ParticleType Type { get; } = type;

    public int Color { get; } = color;
}
