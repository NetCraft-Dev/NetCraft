using NetCraft.Codec;
using NetCraft.Network.Component;
using NetCraft.Registry;

namespace NetCraft.Game.World.Items.Component.Predicates;

//JukeboxPlayablePredicate jukebox predicate, checks whether the song reference falls in the given set
//Maps to vanilla net.minecraft.core.component.predicates.JukeboxPlayablePredicate
public sealed record JukeboxPlayablePredicate(Optional<HolderSet<JukeboxSong>> Song)
    : SingleComponentItemPredicate<JukeboxPlayable>
{
    //Codec persistence codec, a single optional song field, maps to vanilla CODEC
    public static readonly Codec<JukeboxPlayablePredicate> Codec = RecordCodecBuilder.Of1(
        HolderSetCodecs.JukeboxSongSet.OptionalFieldOf("song")
            .ForGetter((JukeboxPlayablePredicate predicate) => predicate.Song),
        song => new JukeboxPlayablePredicate(song));

    public DataComponentType<object> ComponentType => DataComponents.JUKEBOX_PLAYABLE;

    //MatchesValue compares entry by entry by registry name, maps to vanilla's loop over unwrapKey
    public bool MatchesValue(JukeboxPlayable value)
    {
        if (!Song.IsPresent) return true;
        var target = value.Song.UnwrapKey();
        if (target is null) return false;
        foreach (var holder in Song.Get())
        {
            var key = holder.UnwrapKey();
            if (key is not null && key.Equals(target)) return true;
        }
        return false;
    }

    //Any unconstrained predicate, maps to vanilla any
    public static JukeboxPlayablePredicate Any() => new(Optional<HolderSet<JukeboxSong>>.Empty());
}
