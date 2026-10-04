using NetCraft.Codec;

namespace NetCraft.Registry.Environment;

//BackgroundMusic 背景音乐设置对应原版 BackgroundMusic
public sealed class BackgroundMusic
{
    public static readonly BackgroundMusic Empty = new(
        Optional<Music>.Empty(),
        Optional<Music>.Empty(),
        Optional<Music>.Empty());

    public static readonly Codec<BackgroundMusic> Codec = RecordCodecBuilder.Of3(
        Music.Codec.OptionalFieldOf("default").ForGetter((BackgroundMusic v) => v.DefaultMusic),
        Music.Codec.OptionalFieldOf("creative").ForGetter((BackgroundMusic v) => v.CreativeMusic),
        Music.Codec.OptionalFieldOf("underwater").ForGetter((BackgroundMusic v) => v.UnderwaterMusic),
        (defaultMusic, creativeMusic, underwaterMusic) => new BackgroundMusic(defaultMusic, creativeMusic, underwaterMusic));

    public Optional<Music> DefaultMusic { get; }

    public Optional<Music> CreativeMusic { get; }

    public Optional<Music> UnderwaterMusic { get; }

    public BackgroundMusic(Optional<Music> defaultMusic, Optional<Music> creativeMusic, Optional<Music> underwaterMusic)
    {
        DefaultMusic = defaultMusic;
        CreativeMusic = creativeMusic;
        UnderwaterMusic = underwaterMusic;
    }

    public BackgroundMusic(Music music) : this(Optional<Music>.Of(music), Optional<Music>.Empty(), Optional<Music>.Empty()) { }

    public BackgroundMusic WithUnderwater(Music underwaterMusic)
        => new(DefaultMusic, CreativeMusic, Optional<Music>.Of(underwaterMusic));
}

//Music 一段背景音乐对应原版 Music
public sealed class Music
{
    public static readonly Codec<Music> Codec = RecordCodecBuilder.Of4(
        SoundEventIdCodec.Instance.FieldOf("sound").ForGetter((Music v) => v.Sound),
        AttributeValueCodecs.NonNegativeInt.FieldOf("min_delay").ForGetter((Music v) => v.MinDelay),
        AttributeValueCodecs.NonNegativeInt.FieldOf("max_delay").ForGetter((Music v) => v.MaxDelay),
        Codecs.Bool.OptionalFieldOf("replace_current_music", false).ForGetter((Music v) => v.ReplaceCurrentMusic),
        (sound, minDelay, maxDelay, replaceCurrentMusic) => new Music(sound, minDelay, maxDelay, replaceCurrentMusic));

    public Identifier Sound { get; }

    public int MinDelay { get; }

    public int MaxDelay { get; }

    public bool ReplaceCurrentMusic { get; }

    public Music(Identifier sound, int minDelay, int maxDelay, bool replaceCurrentMusic)
    {
        Sound = sound;
        MinDelay = minDelay;
        MaxDelay = maxDelay;
        ReplaceCurrentMusic = replaceCurrentMusic;
    }
}
