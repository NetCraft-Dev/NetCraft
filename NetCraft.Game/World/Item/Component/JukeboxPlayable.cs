using NetCraft.Codec;
using NetCraft.Network;
using NetCraft.Registry;

namespace NetCraft.Game.World.Items.Component;

//JukeboxPlayable 唱片机可播放曲目 只持一张唱片引用
//对应原版 net.minecraft.world.item.JukeboxPlayable
public sealed class JukeboxPlayable : IEquatable<JukeboxPlayable>
{
    //Codec 持久化编解码 本体就是唱片引用 对应原版 CODEC
    public static readonly Codec<JukeboxPlayable> Codec = HolderSetCodecs.JukeboxSongRef.ComapFlatMap(
        song => DataResult<JukeboxPlayable>.Success(new JukeboxPlayable(song)),
        playable => playable.Song);

    //StreamCodec 网络编解码 唱片引用按注册表 id 进出 对应原版 STREAM_CODEC
    public static readonly StreamCodec<RegistryFriendlyByteBuf, JukeboxPlayable> StreamCodec = new JukeboxPlayableStreamCodec();

    public JukeboxPlayable(Holder<JukeboxSong> song) => Song = song;

    public Holder<JukeboxSong> Song { get; }

    public bool Equals(JukeboxPlayable? other) => other is not null && Equals(Song, other.Song);

    public override bool Equals(object? obj) => Equals(obj as JukeboxPlayable);

    public override int GetHashCode() => Song.GetHashCode();

    public override string ToString() => $"JukeboxPlayable[{Song}]";
}

//JukeboxPlayableStreamCodec 唱片引用进出 对应原版 STREAM_CODEC
internal sealed class JukeboxPlayableStreamCodec : StreamCodec<RegistryFriendlyByteBuf, JukeboxPlayable>
{
    private static readonly StreamCodec<RegistryFriendlyByteBuf, Holder<JukeboxSong>> SongCodec =
        ByteBufCodecs.Holder(Registries.JUKEBOX_SONG);

    public JukeboxPlayable Decode(RegistryFriendlyByteBuf buf) => new(SongCodec.Decode(buf));

    public void Encode(RegistryFriendlyByteBuf buf, JukeboxPlayable value) => SongCodec.Encode(buf, value.Song);
}
