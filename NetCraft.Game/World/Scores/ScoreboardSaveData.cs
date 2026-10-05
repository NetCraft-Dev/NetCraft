using NetCraft.Codec;
using NetCraft.Nbt;
using NetCraft.Registry;
using NetCraft.Storage;

namespace NetCraft.Game.World.Scores;

//ScoreboardSaveData 计分板存档 对应原版 net.minecraft.world.scores.ScoreboardSaveData
//存 data/minecraft/scoreboard.dat 顶层四键 Objectives/PlayerScores/DisplaySlots/Teams
public sealed class ScoreboardSaveData : SavedData
{
    //TypeId 存档标识 对应原版 minecraft:scoreboard
    private const string TypeId = "minecraft:scoreboard";

    //Type SavedData 工厂空档建空载体
    public static readonly SavedDataType<ScoreboardSaveData> Type = new ScoreboardSaveType();

    private Packed _data = Packed.Empty;

    public override string Id => TypeId;

    //Data 读存档载体
    public Packed Data => _data;

    //SetData 写存档载体并标脏 对应原版 setData
    public void SetData(Packed data)
    {
        _data = data;
        SetDirty();
    }

    //Save 用 codec 把载体编成 NBT 对应原版 save
    public override CompoundTag Save(CompoundTag tag)
    {
        var encoded = Packed.Codec.EncodeStart(NbtOps.Instance, _data);
        return encoded.Result().IsPresent && encoded.GetOrThrow() is CompoundTag compound ? compound : tag;
    }

    //Packed 存档载体四部分 对应原版 ScoreboardSaveData.Packed
    public sealed record Packed(
        IReadOnlyList<Objective.Packed> Objectives,
        IReadOnlyList<Scoreboard.PackedScore> Scores,
        Dictionary<DisplaySlot, string> DisplaySlots,
        IReadOnlyList<PlayerTeam.Packed> Teams)
    {
        //Empty 空载体 对应原版 EMPTY
        public static readonly Packed Empty = new(Array.Empty<Objective.Packed>(),
            Array.Empty<Scoreboard.PackedScore>(), new Dictionary<DisplaySlot, string>(),
            Array.Empty<PlayerTeam.Packed>());

        //Codec 持久化编解码 顶层四键 Objectives/PlayerScores/DisplaySlots/Teams 对应原版 CODEC
        public static readonly Codec<Packed> Codec = RecordCodecBuilder.Of4(
            Objective.Packed.Codec.ListOf()
                .OptionalFieldOf("Objectives", Array.Empty<Objective.Packed>())
                .ForGetter((Packed packed) => packed.Objectives),
            Scoreboard.PackedScore.Codec.ListOf()
                .OptionalFieldOf("PlayerScores", Array.Empty<Scoreboard.PackedScore>())
                .ForGetter((Packed packed) => packed.Scores),
            Codecs.UnboundedMap(DisplaySlotExtensions.Codec, Codecs.String)
                .OptionalFieldOf("DisplaySlots", new Dictionary<DisplaySlot, string>())
                .ForGetter((Packed packed) => packed.DisplaySlots),
            PlayerTeam.Packed.Codec.ListOf()
                .OptionalFieldOf("Teams", Array.Empty<PlayerTeam.Packed>())
                .ForGetter((Packed packed) => packed.Teams),
            (objectives, scores, displaySlots, teams) => new Packed(objectives, scores, displaySlots, teams));
    }

    //ScoreboardSaveType 存档工厂 从 NBT 用 codec 解析出载体
    private sealed class ScoreboardSaveType : SavedDataType<ScoreboardSaveData>
    {
        public string Id => TypeId;

        public ScoreboardSaveData Create(CompoundTag tag, RegistryAccess registryAccess)
        {
            var data = new ScoreboardSaveData();
            var parsed = Packed.Codec.Parse(NbtOps.Instance, tag);
            if (parsed.Result().IsPresent) data._data = parsed.GetOrThrow();
            return data;
        }
    }
}
