using NetCraft.Client.Level;
using NetCraft.Client.Blaze3d.Systems;
using NetCraft.Client.Blaze3d.Buffers;
using NetCraft.Client.Blaze3d.Textures;
using NetCraft.Client.Blaze3d.Shaders;
using NetCraft.Client.Blaze3d.Pipeline;
using NetCraft.Client.Blaze3d.Vertex;
using NetCraft.Client.Blaze3d.Vulkan;
using NetCraft.Client.Blaze3d.Platform;
using NetCraft.Client.Blaze3d.Font;
using NetCraft.Primitives;
using NetCraft.Client.Blaze3d.Resource;
using NetCraft.Client.Blaze3d.Audio;
using NetCraft.Client.Blaze3d.Framegraph;
using NetCraft.Client.Blaze3d.Preprocessor;
using NetCraft.Client.Blaze3d.Util;
using NetCraft.Client.Render;
using NetCraft.Client.Render.Model;
using NetCraft.Client.Render.Texture;
using NetCraft.Client.Render.Texture.Atlas;
using NetCraft.Client.Render.Item;
using NetCraft.Client.Render.Entity;
using NetCraft.Client.Render.Entity.State;
using NetCraft.Client.Render.State.Gui;
using NetCraft.Client.Gui;
using NetCraft.Client.Gui.Render;
using NetCraft.Client.Gui.Render.State;
using NetCraft.Client.Gui.Render.Pip;
using NetCraft.Client.Gui.Navigation;
using NetCraft.Client.Gui.Layouts;
using NetCraft.Client.Gui.Font;
using NetCraft.Client.Gui.Font.Providers;
using NetCraft.Client.Gui.Font.Glyphs;
using NetCraft.Client.Model;
using NetCraft.Client.Model.Geom;
using NetCraft.Client.Resources.Metadata.Gui;

namespace NetCraft.Client.Render.World;

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
