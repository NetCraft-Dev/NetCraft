using NetCraft.Game.World.Level.LevelGen.Structure;
using NetCraft.Nbt;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Storage;

namespace NetCraft.Game.Server;

//ServerStructureDataBridge the Game-layer implementation of the structure data bridge
//Packs the structure table by chunk into a structures section and restores the same table on load
public sealed class ServerStructureDataBridge(
    StructureFeatureManager structures,
    StructurePieceSerializationContext context) : IStructureDataBridge
{
    //Pack writes the starts and References sections, maps to vanilla packStructureData
    //starts keys the assembly result by structure name; References keys referenced chunk coordinates by structure name
    public CompoundTag Pack(ChunkPos pos)
    {
        var tag = new CompoundTag();
        var startsTag = new CompoundTag();
        foreach (var start in structures.GetStructureStarts(pos))
        {
            if (!start.IsValid) continue;
            startsTag.Put(start.StructureId.ToString(), start.CreateTag());
        }
        tag.Put("starts", startsTag);

        var grouped = new Dictionary<Identifier, List<long>>();
        foreach (var reference in structures.GetReferences(pos))
        {
            if (!grouped.TryGetValue(reference.StructureId, out var list))
                grouped[reference.StructureId] = list = new List<long>();
            list.Add(reference.TargetChunk.Pack());
        }
        var referencesTag = new CompoundTag();
        foreach (var (id, chunks) in grouped)
            referencesTag.PutLongArray(id.ToString(), chunks.ToArray());
        tag.Put("References", referencesTag);
        return tag;
    }

    //Restore reads back starts and References, maps to vanilla unpackStructureStart/unpackStructureReferences
    //Entries whose structure name is not found are discarded like vanilla; unknown piece types are skipped by StructureStart itself
    public void Restore(ChunkPos pos, CompoundTag tag)
    {
        var startsTag = tag.GetCompound("starts");
        if (startsTag is not null)
        {
            foreach (var (_, value) in startsTag)
            {
                if (value is not CompoundTag startTag) continue;
                var start = StructureStart.LoadStaticStart(context, startTag);
                if (start is null || !start.IsValid) continue;
                structures.AddStructureStart(pos, start);
            }
        }

        var referencesTag = tag.GetCompound("References");
        if (referencesTag is null) return;
        foreach (var (name, value) in referencesTag)
        {
            var structureId = Identifier.TryParse(name);
            if (structureId is null || value is not LongArrayTag array) continue;
            foreach (var packed in array.Value)
                structures.AddStructureReference(pos, new StructureReference(structureId.Value, ChunkPos.Unpack(packed)));
        }
    }
}
