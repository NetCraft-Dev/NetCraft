using NetCraft.Game.Client.Level;
using NetCraft.Gpu;
using NetCraft.Primitives;

namespace NetCraft.Game.Client.Render.World;

//ChunkLightSampler 区块光照采样器对标原版 SectionRenderBuilder 的光照计算
//从 ClientLevel 取方块某面外侧邻居的 block/sky light 合并 emission 后打包
//LightCoords 编码 (blockLight<<4)|(skyLight<<20) 对齐 LightTexture.PackLightCoords
//emission 合并取 max(unpackedBlockLight, emission) 不动 sky 段
public sealed class ChunkLightSampler
{
    private readonly ClientLevel _level;

    public ChunkLightSampler(ClientLevel level) => _level = level;

    //GetLightCoords 取面外侧邻居位置的 packed light coords
    //neighborX/Y/Z 是面外侧邻居的世界坐标 lightEmission 是方块自身发光等级 0-15
    //越界邻居 ClientLevel 返回 block=0/sky=15 对齐原版默认行为
    public int GetLightCoords(int neighborX, int neighborY, int neighborZ, int lightEmission)
    {
        var neighborPos = new BlockPos(neighborX, neighborY, neighborZ);
        var blockLight = _level.GetBlockLight(neighborPos);
        var skyLight = _level.GetSkyLight(neighborPos);
        //发光方块自身光源提升 blockLight 段不动 sky 段
        if (lightEmission > blockLight) blockLight = lightEmission;
        return LightTexture.PackLightCoords(blockLight, skyLight);
    }
}
