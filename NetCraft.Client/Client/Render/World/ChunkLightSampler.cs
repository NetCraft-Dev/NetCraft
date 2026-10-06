using NetCraft.Game.Client.Level;
using NetCraft.Gpu;
using NetCraft.Primitives;

namespace NetCraft.Game.Client.Render.World;

//ChunkLightSampler chunk light sampler, maps to vanilla SectionRenderBuilder's light computation
//Fetches the block/sky light of a block face's outer neighbor from ClientLevel, merges emission, and packs it
//LightCoords encoding (blockLight<<4)|(skyLight<<20), aligning with LightTexture.PackLightCoords
//Emission merge takes max(unpackedBlockLight, emission) without touching the sky segment
public sealed class ChunkLightSampler
{
    private readonly ClientLevel _level;

    public ChunkLightSampler(ClientLevel level) => _level = level;

    //GetLightCoords gets the packed light coords at the face's outer neighbor position
    //neighborX/Y/Z are the world coordinates of the face's outer neighbor; lightEmission is the block's own emission level 0-15
    //For out-of-bounds neighbors, ClientLevel returns block=0/sky=15, aligning with vanilla default behavior
    public int GetLightCoords(int neighborX, int neighborY, int neighborZ, int lightEmission)
    {
        var neighborPos = new BlockPos(neighborX, neighborY, neighborZ);
        var blockLight = _level.GetBlockLight(neighborPos);
        var skyLight = _level.GetSkyLight(neighborPos);
        //A glowing block's own light raises the blockLight segment without touching the sky segment
        if (lightEmission > blockLight) blockLight = lightEmission;
        return LightTexture.PackLightCoords(blockLight, skyLight);
    }
}
