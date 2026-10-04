using System.Numerics;
using NetCraft.Gpu;

namespace NetCraft.Game.Client.Render.Model;

//ModelFace 模型面定义对标原版 BlockElementFace
//记录面的纹理引用、cullface 方向、UV、tintindex
//UV 默认 [0,0,16,16] 由 Baker 归一化到 [0,1] 再映射到图集 UV
//Direction 复用 Gpu 层 Direction 枚举保持一致性
public sealed class ModelFace
{
    //Direction 面朝向复用 Gpu.Direction
    public Direction Direction { get; set; }
    //Texture 纹理变量引用以 # 开头如 #all 烘焙时解析为实际纹理路径
    public string Texture { get; set; } = string.Empty;
    //Cullface cullface 方向 null 表示不 cull
    //面在邻居方块完整遮挡该方向时可剔除
    public Direction? Cullface { get; set; }
    //UV [u0,v0,u1,v1] 像素坐标 0-16 范围默认 [0,0,16,16]
    //烘焙时除以 16 归一化到 [0,1] 再用 TextureAtlasSprite.MapU/MapV 映射到图集 UV
    public Vector4 UV { get; set; } = new(0, 0, 16, 16);
    //TintIndex 染色索引 -1 表示不染色如 grass_block 侧面用 tintindex=0 染绿色
    //首版不支持染色保留字段
    public int TintIndex { get; set; } = -1;

    public ModelFace(Direction direction)
    {
        Direction = direction;
    }
}
