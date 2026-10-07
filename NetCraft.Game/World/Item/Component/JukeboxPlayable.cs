using NetCraft.Codec;
using NetCraft.Network;
using NetCraft.Registry;

namespace NetCraft.Game.World.Items.Component;

//JukeboxPlayable jukebox playable track, holds a single song reference
//Maps to vanilla net.minecraft.world.item.JukeboxPlayable
public sealed class JukeboxPlayable : IEquatable<JukeboxPlayable>
{
    //Codec persistence codec, the payload is just the song reference, maps to vanilla CODEC
    public static readonly Codec<JukeboxPlayable> Codec = HolderSetCodecs.JukeboxSongRef.ComapFlatMap(
        song => DataResult<JukeboxPlayable>.Success(new JukeboxPlayable(song)),
        playable => playable.Song);

    //StreamCodec network codec, the song reference goes in and out by registry id, maps to vanilla STREAM_CODEC
    public static readonly StreamCodec<RegistryFriendlyByteBuf, JukeboxPlayable> StreamCodec = new JukeboxPlayableStreamCodec();

    public JukeboxPlayable(Holder<JukeboxSong> song) => Song = song;

    public Holder<JukeboxSong> Song { get; }

    public bool Equals(JukeboxPlayable? other) => other is not null && Equals(Song, other.Song);

    public override bool Equals(object? obj) => Equals(obj as JukeboxPlayable);

    public override int GetHashCode() => Song.GetHashCode();

    public override string ToString() => $"JukeboxPlayable[{Song}]";
}

//JukeboxPlayableStreamCodec the song reference goes in and out, maps to vanilla STREAM_CODEC
internal sealed class JukeboxPlayableStreamCodec : StreamCodec<RegistryFriendlyByteBuf, JukeboxPlayable>
{
    private static readonly StreamCodec<RegistryFriendlyByteBuf, Holder<JukeboxSong>> SongCodec =
        ByteBufCodecs.Holder(Registries.JUKEBOX_SONG);

    public JukeboxPlayable Decode(RegistryFriendlyByteBuf buf) => new(SongCodec.Decode(buf));

    public void Encode(RegistryFriendlyByteBuf buf, JukeboxPlayable value) => SongCodec.Encode(buf, value.Song);
}
