using NetCraft.Codec;
using NetCraft.DataFixer.Schemas;

namespace NetCraft.DataFixer.Fixes;

//memory expiry data fix, maps to vanilla MemoryExpiryDataFix
//1.20.2 wraps each memory value under Brain.memories into the {value:memory} structure
public class MemoryExpiryDataFix : NamedEntityFix
{
    public MemoryExpiryDataFix(Schema schema, string entityType)
        : base(schema, false, "Memory expiry data fix (" + entityType + ")", References.Entity, entityType) { }

    protected override Typed<object> Fix(Typed<object> entity)
        => entity.Update(DSL.RemainderFinder(), FixTag);

    public Dynamic<object> FixTag(Dynamic<object> input)
        => input.Update(FixConstants.LivingEntityBrain, UpdateBrain);

    private Dynamic<object> UpdateBrain(Dynamic<object> input)
        => input.Update("memories", UpdateMemories);

    private Dynamic<object> UpdateMemories(Dynamic<object> memories)
        => memories.UpdateMapValues(UpdateMemoryEntry);

    //updateMemoryEntry applies WrapMemoryValue to the value, maps to vanilla memoryEntry.mapSecond
    private Pair<Dynamic<object>, Dynamic<object>> UpdateMemoryEntry(Pair<Dynamic<object>, Dynamic<object>> memoryEntry)
        => new(memoryEntry.First, WrapMemoryValue(memoryEntry.Second));

    //wrapMemoryValue wraps the memory value into the {value:original} structure
    private Dynamic<object> WrapMemoryValue(Dynamic<object> dynamic)
        => dynamic.CreateMap(new[] { new Pair<Dynamic<object>, Dynamic<object>>(dynamic.CreateString("value"), dynamic) });
}
