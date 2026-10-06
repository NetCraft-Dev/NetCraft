using NetCraft.Codec;

namespace NetCraft.Registry.Environment;

//AmbientSounds ambient sound settings, maps to vanilla AmbientSounds
public sealed class AmbientSounds
{
    public static readonly AmbientSounds Empty = new(
        Optional<Identifier>.Empty(),
        Optional<AmbientMoodSettings>.Empty(),
        Array.Empty<AmbientAdditionsSettings>());

    public static readonly AmbientSounds LegacyCaveSettings = new(
        Optional<Identifier>.Empty(),
        Optional<AmbientMoodSettings>.Of(AmbientMoodSettings.LegacyCaveSettings),
        Array.Empty<AmbientAdditionsSettings>());

    public static readonly Codec<AmbientSounds> Codec = RecordCodecBuilder.Of3(
        SoundEventIdCodec.Instance.OptionalFieldOf("loop").ForGetter((AmbientSounds v) => v.Loop),
        AmbientMoodSettings.Codec.OptionalFieldOf("mood").ForGetter((AmbientSounds v) => v.Mood),
        new CompactListCodec<AmbientAdditionsSettings>(AmbientAdditionsSettings.Codec)
            .OptionalFieldOf("additions", (IReadOnlyList<AmbientAdditionsSettings>)Array.Empty<AmbientAdditionsSettings>())
            .ForGetter((AmbientSounds v) => v.Additions),
        (loop, mood, additions) => new AmbientSounds(loop, mood, additions));

    public Optional<Identifier> Loop { get; }

    public Optional<AmbientMoodSettings> Mood { get; }

    public IReadOnlyList<AmbientAdditionsSettings> Additions { get; }

    public AmbientSounds(Optional<Identifier> loop, Optional<AmbientMoodSettings> mood,
        IReadOnlyList<AmbientAdditionsSettings> additions)
    {
        Loop = loop;
        Mood = mood;
        Additions = additions;
    }
}

//AmbientMoodSettings ambient mood sound settings, maps to vanilla AmbientMoodSettings
public sealed class AmbientMoodSettings
{
    public static readonly Codec<AmbientMoodSettings> Codec = RecordCodecBuilder.Of4(
        SoundEventIdCodec.Instance.FieldOf("sound").ForGetter((AmbientMoodSettings v) => v.SoundEvent),
        Codecs.Int.FieldOf("tick_delay").ForGetter((AmbientMoodSettings v) => v.TickDelay),
        Codecs.Int.FieldOf("block_search_extent").ForGetter((AmbientMoodSettings v) => v.BlockSearchExtent),
        Codecs.Double.FieldOf("offset").ForGetter((AmbientMoodSettings v) => v.SoundPositionOffset),
        (sound, tickDelay, blockSearchExtent, soundPositionOffset) =>
            new AmbientMoodSettings(sound, tickDelay, blockSearchExtent, soundPositionOffset));

    public static readonly AmbientMoodSettings LegacyCaveSettings =
        new(Identifier.WithDefaultNamespace("ambient.cave"), 6000, 8, 2.0);

    public Identifier SoundEvent { get; }

    public int TickDelay { get; }

    public int BlockSearchExtent { get; }

    public double SoundPositionOffset { get; }

    public AmbientMoodSettings(Identifier soundEvent, int tickDelay, int blockSearchExtent, double soundPositionOffset)
    {
        SoundEvent = soundEvent;
        TickDelay = tickDelay;
        BlockSearchExtent = blockSearchExtent;
        SoundPositionOffset = soundPositionOffset;
    }
}

//AmbientAdditionsSettings ambient additions sound settings, maps to vanilla AmbientAdditionsSettings
public sealed class AmbientAdditionsSettings
{
    public static readonly Codec<AmbientAdditionsSettings> Codec = RecordCodecBuilder.Of2(
        SoundEventIdCodec.Instance.FieldOf("sound").ForGetter((AmbientAdditionsSettings v) => v.SoundEvent),
        Codecs.Double.FieldOf("tick_chance").ForGetter((AmbientAdditionsSettings v) => v.TickChance),
        (sound, tickChance) => new AmbientAdditionsSettings(sound, tickChance));

    public Identifier SoundEvent { get; }

    public double TickChance { get; }

    public AmbientAdditionsSettings(Identifier soundEvent, double tickChance)
    {
        SoundEvent = soundEvent;
        TickChance = tickChance;
    }
}
