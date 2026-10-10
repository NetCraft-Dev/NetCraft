using System.Numerics;
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

namespace NetCraft.Client.Render.Model;

//ModelFace model face definition, maps to vanilla BlockElementFace
//Records the face's texture reference, cullface direction, UV, and tintindex
//UV defaults to [0,0,16,16], normalized to [0,1] by the Baker and then mapped to atlas UV
//Direction reuses the Gpu layer's Direction enum for consistency
public sealed class ModelFace
{
    //Direction face orientation, reuses Gpu.Direction
    public Direction Direction { get; set; }
    //Texture texture variable reference starting with #, such as #all, resolved to the actual texture path at bake time
    public string Texture { get; set; } = string.Empty;
    //Cullface cullface direction; null means no culling
    //A face can be culled when the neighbor block fully occludes that direction
    public Direction? Cullface { get; set; }
    //UV [u0,v0,u1,v1] in pixel coordinates, 0-16 range, default [0,0,16,16]
    //At bake time divide by 16 to normalize to [0,1], then map to atlas UV via TextureAtlasSprite.MapU/MapV
    public Vector4 UV { get; set; } = new(0, 0, 16, 16);
    //TintIndex tint index; -1 means no tinting, e.g. the side of grass_block uses tintindex=0 to tint green
    //The first version does not support tinting; field is preserved
    public int TintIndex { get; set; } = -1;

    public ModelFace(Direction direction)
    {
        Direction = direction;
    }
}
