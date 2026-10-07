using System.Numerics;

namespace NetCraft.Gpu;

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
