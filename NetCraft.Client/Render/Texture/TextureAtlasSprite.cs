using NetCraft.Client.Blaze3d.Systems;
using NetCraft.Client.Blaze3d.Buffers;
using NetCraft.Client.Blaze3d.Textures;
using NetCraft.Client.Blaze3d.Shaders;
using NetCraft.Client.Blaze3d.Pipeline;
using NetCraft.Client.Blaze3d.Vertex;
using NetCraft.Client.Blaze3d.Vulkan;
using NetCraft.Client.Blaze3d.Platform;
using NetCraft.Client.Blaze3d.Font;
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
namespace NetCraft.Client.Render.Texture;

//TextureAtlasSprite texture atlas sprite metadata, maps to vanilla TextureAtlasSprite
//Records the sprite's pixel position and UV range in the atlas for BakedQuad to map model UVs to atlas UVs
//sprite name uses the identifier format minecraft:block/stone, corresponding to the resource path textures/block/stone
public sealed class TextureAtlasSprite
{
    //Name sprite identifier minecraft:block/stone
    public string Name { get; }
    //AtlasX/AtlasY pixel coordinates of the sprite top-left in the atlas
    public int AtlasX { get; }
    //AtlasY top-aligned; Vulkan V=0 is at the top
    public int AtlasY { get; }
    //Width/Height sprite pixel size
    public int Width { get; }
    public int Height { get; }
    //AtlasWidth/AtlasHeight whole atlas size used to compute UVs
    public int AtlasWidth { get; }
    public int AtlasHeight { get; }
    //Pixels sprite raw RGBA pixel data used to upload into the atlas region
    //null means no pixel data (placeholder sprite) and the upload is skipped on Bake
    public byte[]? Pixels { get; }

    public TextureAtlasSprite(string name, int atlasX, int atlasY, int width, int height,
        int atlasWidth, int atlasHeight, byte[]? pixels)
    {
        Name = name;
        AtlasX = atlasX;
        AtlasY = atlasY;
        Width = width;
        Height = height;
        AtlasWidth = atlasWidth;
        AtlasHeight = atlasHeight;
        Pixels = pixels;
    }

    //U0/V0 top-left UV; Vulkan texture V=0 is at the top
    public float U0 => (float)AtlasX / AtlasWidth;
    public float V0 => (float)AtlasY / AtlasHeight;
    //U1/V1 bottom-right UV
    public float U1 => (float)(AtlasX + Width) / AtlasWidth;
    public float V1 => (float)(AtlasY + Height) / AtlasHeight;

    //MapU maps model-local UV [0,1] to atlas UV
    //Model face UVs are in [0,1]; this method converts them to atlas coordinates during baking
    public float MapU(float u) => U0 + (U1 - U0) * u;
    public float MapV(float v) => V0 + (V1 - V0) * v;
}
