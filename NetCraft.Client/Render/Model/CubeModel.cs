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

//CubeModel procedural cube model, maps to vanilla ModelPart.Cube.compile
//Generates 6 face BakedQuads for ItemRenderer to render; the PoC uses procedural geometry instead of loading a JSON model
//Each face has 4 vertices + UV + face normal (Direction); vertices are counter-clockwise with the front facing out
public static class CubeModel
{
    //Create generates a BakedQuad list for a cube of side size centered at the origin
    //UVs map to 0..1 per face; the PoC does no texture atlas chunking
    public static List<BakedQuad> Create(float size)
    {
        var s = size / 2f;
        var quads = new List<BakedQuad>(6);
        //Down -Y
        quads.Add(new BakedQuad(
            new Vector3(-s, -s, s), new Vector3(s, -s, s),
            new Vector3(s, -s, -s), new Vector3(-s, -s, -s),
            new Vector2(0, 0), new Vector2(1, 0),
            new Vector2(1, 1), new Vector2(0, 1), Direction.Down));
        //Up +Y
        quads.Add(new BakedQuad(
            new Vector3(-s, s, -s), new Vector3(s, s, -s),
            new Vector3(s, s, s), new Vector3(-s, s, s),
            new Vector2(0, 0), new Vector2(1, 0),
            new Vector2(1, 1), new Vector2(0, 1), Direction.Up));
        //North -Z
        quads.Add(new BakedQuad(
            new Vector3(-s, -s, -s), new Vector3(s, -s, -s),
            new Vector3(s, s, -s), new Vector3(-s, s, -s),
            new Vector2(0, 0), new Vector2(1, 0),
            new Vector2(1, 1), new Vector2(0, 1), Direction.North));
        //South +Z
        quads.Add(new BakedQuad(
            new Vector3(s, -s, s), new Vector3(-s, -s, s),
            new Vector3(-s, s, s), new Vector3(s, s, s),
            new Vector2(0, 0), new Vector2(1, 0),
            new Vector2(1, 1), new Vector2(0, 1), Direction.South));
        //West -X
        quads.Add(new BakedQuad(
            new Vector3(-s, -s, -s), new Vector3(-s, -s, s),
            new Vector3(-s, s, s), new Vector3(-s, s, -s),
            new Vector2(0, 0), new Vector2(1, 0),
            new Vector2(1, 1), new Vector2(0, 1), Direction.West));
        //East +X
        quads.Add(new BakedQuad(
            new Vector3(s, -s, s), new Vector3(s, -s, -s),
            new Vector3(s, s, -s), new Vector3(s, s, s),
            new Vector2(0, 0), new Vector2(1, 0),
            new Vector2(1, 1), new Vector2(0, 1), Direction.East));
        return quads;
    }
}
