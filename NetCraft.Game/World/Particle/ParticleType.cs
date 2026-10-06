using NetCraft.Game.World.Items;
using NetCraft.Network;
using NetCraft.Registry.State;

namespace NetCraft.Game.World.Particle;

//ParticleType particle type, maps to vanilla net.minecraft.core.particles.ParticleType
//Implements the Registry-layer placeholder interface so it can be registered into the PARTICLE_TYPE registry; type-specific parameter codecs are implemented by subclasses
public abstract class ParticleType : NetCraft.Registry.ParticleType<object>
{
    protected ParticleType(bool overrideLimiter) => OverrideLimiter = overrideLimiter;

    //OverrideLimiter whether it bypasses the particle count limit, maps to vanilla getOverrideLimiter
    public bool OverrideLimiter { get; }

    //WriteParameters writes type-specific parameters; parameterless types write nothing, maps to the encoding part of the vanilla streamCodec
    public abstract void WriteParameters(RegistryFriendlyByteBuf buf, ParticleOptions options);

    //ReadParameters reads type-specific parameters back, maps to the decoding part of the vanilla streamCodec
    public abstract ParticleOptions ReadParameters(RegistryFriendlyByteBuf buf);
}

//SimpleParticleType parameterless particle type, maps to vanilla SimpleParticleType
public sealed class SimpleParticleType : ParticleType
{
    private readonly SimpleParticleOption _options;

    public SimpleParticleType(bool overrideLimiter) : base(overrideLimiter)
        => _options = new SimpleParticleOption(this);

    //Options the type's single option instance; used by both commands and the network
    public SimpleParticleOption Options => _options;

    public override void WriteParameters(RegistryFriendlyByteBuf buf, ParticleOptions options) { }

    public override ParticleOptions ReadParameters(RegistryFriendlyByteBuf buf) => _options;
}

//BlockParticleType carries a block state parameter, covering block/block_marker/falling_dust/dust_pillar/block_crumble
//maps to vanilla BlockParticleOption.streamCodec writing the global block state id
public sealed class BlockParticleType(bool overrideLimiter) : ParticleType(overrideLimiter)
{
    public override void WriteParameters(RegistryFriendlyByteBuf buf, ParticleOptions options)
        => buf.WriteVarInt(((BlockParticleOption)options).State.Id);

    public override ParticleOptions ReadParameters(RegistryFriendlyByteBuf buf)
        => new BlockParticleOption(this, BlockStateRegistry.GetState(buf.ReadVarInt()));
}

//ItemParticleType carries an item stack parameter, maps to vanilla ItemParticleOption.streamCodec
public sealed class ItemParticleType(bool overrideLimiter) : ParticleType(overrideLimiter)
{
    public override void WriteParameters(RegistryFriendlyByteBuf buf, ParticleOptions options)
        => ItemStack.StreamCodec.Encode(buf, ((ItemParticleOption)options).Item);

    public override ParticleOptions ReadParameters(RegistryFriendlyByteBuf buf)
        => new ItemParticleOption(this, ItemStack.StreamCodec.Decode(buf));
}

//DustParticleType color and scale parameters, maps to vanilla DustParticleOptions.streamCodec
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

//DustColorTransitionParticleType two-end colors and scale parameters, maps to vanilla DustColorTransitionOptions.streamCodec
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

//ColorParticleType single color parameter, maps to vanilla ColorParticleOption.streamCodec
public sealed class ColorParticleType(bool overrideLimiter) : ParticleType(overrideLimiter)
{
    public override void WriteParameters(RegistryFriendlyByteBuf buf, ParticleOptions options)
        => buf.WriteInt(((ColorParticleOption)options).Color);

    public override ParticleOptions ReadParameters(RegistryFriendlyByteBuf buf)
        => new ColorParticleOption(this, buf.ReadInt());
}

//UnsupportedParticleType placeholder particle type; only keeps the registry id aligned with the client, parameter encode/decode depends on unported subsystems
public sealed class UnsupportedParticleType(string name, bool overrideLimiter) : ParticleType(overrideLimiter)
{
    //Name registry name, used in error messages
    public string Name { get; } = name;

    public override void WriteParameters(RegistryFriendlyByteBuf buf, ParticleOptions options)
        => throw new NotSupportedException($"parameter encoding for particle {Name} is not implemented");

    public override ParticleOptions ReadParameters(RegistryFriendlyByteBuf buf)
        => throw new NotSupportedException($"parameter decoding for particle {Name} is not implemented");
}
