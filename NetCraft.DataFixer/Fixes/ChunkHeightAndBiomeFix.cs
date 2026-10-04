namespace NetCraft.DataFixer.Fixes;

using System;
using NetCraft.DataFixer.Schemas;

//区块高度与生物群系修复对应原版net.minecraft.util.datafix.fixes.ChunkHeightAndBiomeFix
//将旧版16段chunk升级到24段增加-4..19范围对齐1.18新增高度
//MakeRule完整实现依赖SerializableChunkData.copyOf/read + NoiseRouterData + JigsawBlockEntity
//三者均属NetCraft.Game游戏业务范畴待游戏业务模块开发后接通
public class ChunkHeightAndBiomeFix : DataFix
{
    //DATAFIXER_CONTEXT_TAG数据修复上下文tag名SimpleRegionStorage持有同名常量
    public const string DatafixerContextTag = "__context";

    public ChunkHeightAndBiomeFix(Schema outputSchema) : base(outputSchema, changesType: true) { }

    //makeRule完整实现依赖NetCraft.Game的SerializableChunkData等游戏内容
    //NetCraft.Game作为可选业务模块最后开发完成后再实现此处
    protected override TypeRewriteRule MakeRule()
        => throw new NotSupportedException("ChunkHeightAndBiomeFix.MakeRule 待 NetCraft.Game 游戏业务模块接通");
}
