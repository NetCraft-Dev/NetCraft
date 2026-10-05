using NetCraft.Codec;
using NetCraft.Network.Component;
using NetCraft.Registry;

namespace NetCraft.Game.World.Items.Component.Predicates;

//JukeboxPlayablePredicate 唱片机谓词 判定曲目引用是否落在给定集合
//对应原版 net.minecraft.core.component.predicates.JukeboxPlayablePredicate
public sealed record JukeboxPlayablePredicate(Optional<HolderSet<JukeboxSong>> Song)
    : SingleComponentItemPredicate<JukeboxPlayable>
{
    //Codec 持久化编解码 只有 song 一个可选字段 对应原版 CODEC
    public static readonly Codec<JukeboxPlayablePredicate> Codec = RecordCodecBuilder.Of1(
        HolderSetCodecs.JukeboxSongSet.OptionalFieldOf("song")
            .ForGetter((JukeboxPlayablePredicate predicate) => predicate.Song),
        song => new JukeboxPlayablePredicate(song));

    public DataComponentType<object> ComponentType => DataComponents.JUKEBOX_PLAYABLE;

    //MatchesValue 按注册名逐项比对 对应原版遍历 unwrapKey 的写法
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

    //Any 无约束谓词 对应原版 any
    public static JukeboxPlayablePredicate Any() => new(Optional<HolderSet<JukeboxSong>>.Empty());
}
