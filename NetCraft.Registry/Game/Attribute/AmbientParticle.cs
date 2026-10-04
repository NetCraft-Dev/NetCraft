using NetCraft.Codec;

namespace NetCraft.Registry.Environment;

//AmbientParticle 环境粒子设置对应原版 AmbientParticle
public sealed class AmbientParticle
{
    public static readonly Codec<AmbientParticle> Codec = RecordCodecBuilder.Of2(
        ParticleIdCodec.Instance.FieldOf("particle").ForGetter((AmbientParticle v) => v.Particle),
        AttributeValueCodecs.UnitFloat.FieldOf("probability").ForGetter((AmbientParticle v) => v.Probability),
        (particle, probability) => new AmbientParticle(particle, probability));

    //本仓库无粒子类型注册表数据，粒子用 Identifier 弱引用
    public Identifier Particle { get; }

    public float Probability { get; }

    public AmbientParticle(Identifier particle, float probability)
    {
        Particle = particle;
        Probability = probability;
    }

    public static IReadOnlyList<AmbientParticle> Of(Identifier particle, float probability)
        => new[] { new AmbientParticle(particle, probability) };
}
