using System.Numerics;

namespace NetCraft.Client.Render.Model;

//ModelElement model element, maps to vanilla BlockElement
//A cube defined by from-to, containing 6 faces (some may be missing)
//from/to in pixel coordinates, 0-16 range, normalized to [0,1] at bake time
public sealed class ModelElement
{
    //From the cube's minimum corner coordinate
    public Vector3 From { get; set; }
    //To the cube's maximum corner coordinate
    public Vector3 To { get; set; }
    //Faces faces indexed by FaceDirection
    public List<ModelFace> Faces { get; set; } = new();

    public ModelElement(Vector3 from, Vector3 to)
    {
        From = from;
        To = to;
    }

    //IsFullCube whether it is a full cube from=[0,0,0] to=[16,16,16]
    //Used to determine the block's BlockRenderShape, but the actual BlockRenderShape is decided by BlockBehaviour
    //Here it is only used at bake time to decide whether the faces are a standard cube
    public bool IsFullCube
        => From == Vector3.Zero && To == new Vector3(16, 16, 16);
}
