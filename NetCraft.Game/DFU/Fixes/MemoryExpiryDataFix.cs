using NetCraft.Codec;
using NetCraft.DataFixer.Schemas;

namespace NetCraft.Game.DFU.Fixes;

using NetCraft.DataFixer.Fixes;

using NetCraft.DataFixer;

//记忆过期数据修复对应原版MemoryExpiryDataFix
//1.20.2把Brain.memories下每个memory值包装为{value:memory}结构
public class MemoryExpiryDataFix : NamedEntityFix
{
    public MemoryExpiryDataFix(Schema schema, string entityType)
        : base(schema, false, "Memory expiry data fix (" + entityType + ")", References.Entity, entityType) { }

    protected override Typed<object> Fix(Typed<object> entity)
        => entity.Update(DSL.RemainderFinder(), FixTag);

    public Dynamic<object> FixTag(Dynamic<object> input)
    {
        if (input.Value is Dynamic<object> nested)
            input = nested;
        return input.Update(FixConstants.LivingEntityBrain, UpdateBrain);
    }

    private Dynamic<object> UpdateBrain(Dynamic<object> input)
        => input.Update("memories", UpdateMemories);

    private Dynamic<object> UpdateMemories(Dynamic<object> memories)
        => memories.UpdateMapValues(UpdateMemoryEntry);

    //updateMemoryEntry对value应用WrapMemoryValue对应原版memoryEntry.mapSecond
    private Pair<Dynamic<object>, Dynamic<object>> UpdateMemoryEntry(Pair<Dynamic<object>, Dynamic<object>> memoryEntry)
        => new(memoryEntry.First, WrapMemoryValue(memoryEntry.Second));

    //wrapMemoryValue把memory值包装为{value:原值}结构
    private Dynamic<object> WrapMemoryValue(Dynamic<object> dynamic)
        => dynamic.CreateMap(new[] { new Pair<Dynamic<object>, Dynamic<object>>(dynamic.CreateString("value"), dynamic) });
}
