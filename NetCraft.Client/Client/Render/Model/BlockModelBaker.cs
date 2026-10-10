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

//BlockModelBaker model baker, maps to the baking stage of vanilla ModelBakery
//Bakes UnbakedModel + ITextureAtlas into BakedModel
//Iterates elements' faces, resolving texture variables→sprite→atlas UV to generate BakedQuad
//Vertex positions are computed from element from/to; UV is normalized from face.uv then mapped via sprite.MapU/MapV
//The first version puts all quads in the Solid layer; later versions split into Cutout/Translucent by renderType
//Depends on the ITextureAtlas interface, not coupled to GpuDevice; tests can use a stub
public sealed class BlockModelBaker
{
    private readonly ITextureAtlas _atlas;

    public BlockModelBaker(ITextureAtlas atlas)
    {
        _atlas = atlas;
    }

    //Bake bakes UnbakedModel into BakedModel
    //model must already be ResolveParent'd (elements/textures merged)
    public BakedModel Bake(UnbakedModel model) => Bake(model, 0, 0);

    //Bake baking with variant rotation
    //rotationX/rotationY are the blockstates variant's x/y rotation about the block center, corresponding to vanilla Variant's model transform
    //Without them, facing blocks would only render the model file's base orientation; levers/buttons/torches would all face the same direction
    public BakedModel Bake(UnbakedModel model, int rotationX, int rotationY)
    {
        var baked = new BakedModel();
        foreach (var element in model.Elements)
        {
            foreach (var face in element.Faces)
            {
                var quad = BakeFace(element, face, model, rotationX, rotationY);
                if (quad.HasValue)
                    baked.AddQuad(RenderLayer.Solid, quad.Value, RotateFace(face.Cullface, rotationX, rotationY));
            }
        }
        return baked;
    }

    //BakeFace bakes a single face into BakedQuad
    //Resolve the texture variable to a sprite name, look up the atlas sprite, and compute atlas UV
    //Vertex positions are computed from from/to by Direction, four corner vertices
    //Returns null when the texture is not found (sprite not in the atlas)
    private BakedQuad? BakeFace(ModelElement element, ModelFace face, UnbakedModel model,
        int rotationX, int rotationY)
    {
        var textureName = BlockModelLoader.ResolveTexture(model, face.Texture);
        var sprite = _atlas.GetSprite(textureName);
        if (sprite is null) return null;
        //face UV is [u0,v0,u1,v1] in pixel coordinates 0-16, normalized to [0,1] then mapped to the atlas
        var u0 = face.UV.X / 16f;
        var v0 = face.UV.Y / 16f;
        var u1 = face.UV.Z / 16f;
        var w1 = face.UV.W / 16f;
        //Atlas UV mapping
        var auv0 = new Vector2(sprite.MapU(u0), sprite.MapV(v0));
        var auv1 = new Vector2(sprite.MapU(u1), sprite.MapV(v0));
        var auv2 = new Vector2(sprite.MapU(u1), sprite.MapV(w1));
        var auv3 = new Vector2(sprite.MapU(u0), sprite.MapV(w1));
        //Vertex positions computed from from/to by Direction, then applying the variant rotation
        var (p0, p1, p2, p3) = ComputeFaceVertices(element.From, element.To, face.Direction);
        p0 = RotateVertex(p0, rotationX, rotationY);
        p1 = RotateVertex(p1, rotationX, rotationY);
        p2 = RotateVertex(p2, rotationX, rotationY);
        p3 = RotateVertex(p3, rotationX, rotationY);
        //UV follows the vertex indices, equivalent to vanilla's default uvlock=false where the texture rotates with the face
        var direction = RotateDirection(face.Direction, rotationX, rotationY);
        return new BakedQuad(p0, p1, p2, p3, auv0, auv1, auv2, auv3, direction, face.TintIndex);
    }

    //Center variant rotation is about the block center, corresponding to vanilla Transformation pivoting on the block's geometric center
    private static readonly Vector3 Center = new(8f, 8f, 8f);

    //RotateVertex rotates a vertex about the block center by the variant x/y
    private static Vector3 RotateVertex(Vector3 position, int rotationX, int rotationY)
        => Center + RotateVector(position - Center, rotationX, rotationY);

    //RotateVector rotates a direction vector by the variant x/y
    //Vanilla x 90 turns UP into NORTH; y 90 turns NORTH into EAST; both rotate -90 degrees about the axis
    //Order is x then y, corresponding to vanilla OctahedralGroup's matrix composition
    private static Vector3 RotateVector(Vector3 vector, int rotationX, int rotationY)
    {
        if (rotationX != 0)
            vector = Vector3.Transform(vector, Quaternion.CreateFromAxisAngle(Vector3.UnitX,
                -MathF.PI / 2f * (rotationX / 90f)));
        if (rotationY != 0)
            vector = Vector3.Transform(vector, Quaternion.CreateFromAxisAngle(Vector3.UnitY,
                -MathF.PI / 2f * (rotationY / 90f)));
        return vector;
    }

    //RotateFace rotates the face orientation for cullface culling; null means no culling, kept as is
    private static Direction? RotateFace(Direction? face, int rotationX, int rotationY)
        => face is null ? null : RotateDirection(face.Value, rotationX, rotationY);

    //RotateDirection rotates an axis; the rotation is a multiple of 90 degrees so the result always lands on one of the six axes
    private static Direction RotateDirection(Direction direction, int rotationX, int rotationY)
        => rotationX == 0 && rotationY == 0
            ? direction
            : FromUnitVector(RotateVector(direction.UnitVector(), rotationX, rotationY));

    //FromUnitVector folds a unit axis vector back into the direction enum
    private static Direction FromUnitVector(Vector3 vector)
    {
        if (vector.Y > 0.5f) return Direction.Up;
        if (vector.Y < -0.5f) return Direction.Down;
        if (vector.X > 0.5f) return Direction.East;
        if (vector.X < -0.5f) return Direction.West;
        return vector.Z > 0.5f ? Direction.South : Direction.North;
    }

    //ComputeFaceVertices computes the four vertex positions by Direction
    //Returns vertex coordinates in CCW order (viewed from the face's outer side), 0-16 range
    //Chunk mesh generation normalizes them to 0-1 world coordinates
    private static (Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3) ComputeFaceVertices(
        Vector3 from, Vector3 to, Direction dir)
    {
        var x0 = from.X;
        var y0 = from.Y;
        var z0 = from.Z;
        var x1 = to.X;
        var y1 = to.Y;
        var z1 = to.Z;
        return dir switch
        {
            //Down facing down, viewed from the outer side (looking up from -Y), CCW order
            Direction.Down => (new(x0, y0, z1), new(x0, y0, z0), new(x1, y0, z0), new(x1, y0, z1)),
            //Up facing up, viewed from the outer side (looking down from +Y), CCW order
            Direction.Up => (new(x0, y1, z0), new(x0, y1, z1), new(x1, y1, z1), new(x1, y1, z0)),
            //North facing -Z, CCW order
            Direction.North => (new(x0, y1, z0), new(x0, y0, z0), new(x1, y0, z0), new(x1, y1, z0)),
            //South facing +Z, CCW order
            Direction.South => (new(x1, y1, z1), new(x1, y0, z1), new(x0, y0, z1), new(x0, y1, z1)),
            //West facing -X, CCW order
            Direction.West => (new(x0, y1, z1), new(x0, y0, z1), new(x0, y0, z0), new(x0, y1, z0)),
            //East facing +X, CCW order
            Direction.East => (new(x1, y1, z0), new(x1, y0, z0), new(x1, y0, z1), new(x1, y1, z1)),
            _ => throw new ArgumentOutOfRangeException(nameof(dir))
        };
    }
}
