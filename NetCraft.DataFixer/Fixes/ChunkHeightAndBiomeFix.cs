namespace NetCraft.DataFixer.Fixes;

using System;
using NetCraft.DataFixer.Schemas;

//chunk height and biome fix, maps to vanilla net.minecraft.util.datafix.fixes.ChunkHeightAndBiomeFix
//upgrades old 16-section chunks to 24 sections, adding the -4..19 range to align with 1.18's new height
//the full MakeRule implementation depends on SerializableChunkData.copyOf/read + NoiseRouterData + JigsawBlockEntity
//all three belong to the NetCraft.Game business domain, to be wired up once the game module is developed
public class ChunkHeightAndBiomeFix : DataFix
{
    //DATAFIXER_CONTEXT_TAG data fix context tag name; SimpleRegionStorage holds a constant with the same name
    public const string DatafixerContextTag = "__context";

    public ChunkHeightAndBiomeFix(Schema outputSchema) : base(outputSchema, changesType: true) { }

    //the full makeRule implementation depends on NetCraft.Game's SerializableChunkData and other game content
    //NetCraft.Game is an optional business module; implement this once it is finished
    protected override TypeRewriteRule MakeRule()
        => throw new NotSupportedException("ChunkHeightAndBiomeFix.MakeRule pending NetCraft.Game business module integration");
}
