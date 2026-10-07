using NetCraft.Codec;
using NetCraft.Nbt;
using NetCraft.Registry;
using NetCraft.Storage;

namespace NetCraft.Game.World.Scores;

//ScoreboardSaveData scoreboard save data, maps to vanilla net.minecraft.world.scores.ScoreboardSaveData
//Stored at data/minecraft/scoreboard.dat with four top-level keys Objectives/PlayerScores/DisplaySlots/Teams
public sealed class ScoreboardSaveData : SavedData
{
    //TypeId save identifier, maps to vanilla minecraft:scoreboard
    private const string TypeId = "minecraft:scoreboard";

    //Type SavedData factory, builds an empty payload when the slot is empty
    public static readonly SavedDataType<ScoreboardSaveData> Type = new ScoreboardSaveType();

    private Packed _data = Packed.Empty;

    public override string Id => TypeId;

    //Data reads the save payload
    public Packed Data => _data;

    //SetData writes the save payload and marks it dirty, maps to vanilla setData
    public void SetData(Packed data)
    {
        _data = data;
        SetDirty();
    }

    //Save encodes the payload into NBT with the codec, maps to vanilla save
    public override CompoundTag Save(CompoundTag tag)
    {
        var encoded = Packed.Codec.EncodeStart(NbtOps.Instance, _data);
        return encoded.Result().IsPresent && encoded.GetOrThrow() is CompoundTag compound ? compound : tag;
    }

    //Packed the four parts of the save payload, maps to vanilla ScoreboardSaveData.Packed
    public sealed record Packed(
        IReadOnlyList<Objective.Packed> Objectives,
        IReadOnlyList<Scoreboard.PackedScore> Scores,
        Dictionary<DisplaySlot, string> DisplaySlots,
        IReadOnlyList<PlayerTeam.Packed> Teams)
    {
        //Empty empty payload, maps to vanilla EMPTY
        public static readonly Packed Empty = new(Array.Empty<Objective.Packed>(),
            Array.Empty<Scoreboard.PackedScore>(), new Dictionary<DisplaySlot, string>(),
            Array.Empty<PlayerTeam.Packed>());

        //Codec persistence codec, top-level keys Objectives/PlayerScores/DisplaySlots/Teams, maps to vanilla CODEC
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

    //ScoreboardSaveType save factory, parses the payload from NBT with the codec
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
