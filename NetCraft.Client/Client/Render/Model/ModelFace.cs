using System.Numerics;
using NetCraft.Gpu;

namespace NetCraft.Game.Client.Render.Model;

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
