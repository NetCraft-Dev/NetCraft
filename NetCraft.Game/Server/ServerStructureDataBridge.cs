using NetCraft.Game.World.Level.LevelGen.Structure;
using NetCraft.Nbt;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Storage;

namespace NetCraft.Game.Server;

//ServerStructureDataBridge 结构数据桥的 Game 层实现
//把结构表按区块打包成 structures 段 读档时再还原回同一张结构表
public sealed class ServerStructureDataBridge(
    StructureFeatureManager structures,
    StructurePieceSerializationContext context) : IStructureDataBridge
{
    //Pack 写出 starts 与 References 两段 对应原版 packStructureData
    //starts 按结构名分键存装配结果 References 按结构名分键存被引用到的区块坐标
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

    //Restore 读回 starts 与 References 对应原版 unpackStructureStart/unpackStructureReferences
    //结构名查不到的条目按原版丢弃 片段类型不认识的由 StructureStart 自己跳过
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
