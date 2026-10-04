using System.Numerics;

namespace NetCraft.Game.Client.Render.Model;

//ModelElement 模型元素对标原版 BlockElement
//一个 from-to 定义的立方体含 6 个面（可能部分面缺失）
//from/to 像素坐标 0-16 范围烘焙时归一化到 [0,1]
public sealed class ModelElement
{
    //From 立方体最小角坐标
    public Vector3 From { get; set; }
    //To 立方体最大角坐标
    public Vector3 To { get; set; }
    //Faces 面字典按 FaceDirection 索引
    public List<ModelFace> Faces { get; set; } = new();

    public ModelElement(Vector3 from, Vector3 to)
    {
        From = from;
        To = to;
    }

    //IsFullCube 是否为完整立方体 from=[0,0,0] to=[16,16,16]
    //用于判断方块 BlockRenderShape 但实际 BlockRenderShape 由 BlockBehaviour 决定
    //这里仅用于烘焙时判断是否标准立方体面
    public bool IsFullCube
        => From == Vector3.Zero && To == new Vector3(16, 16, 16);
}
