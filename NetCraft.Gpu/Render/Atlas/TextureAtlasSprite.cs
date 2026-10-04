namespace NetCraft.Gpu;

//TextureAtlasSprite 纹理图集 sprite 元数据对标原版 TextureAtlasSprite
//记录 sprite 在图集中的像素位置和 UV 范围供 BakedQuad 烘焙时把模型 UV 映射到图集 UV
//sprite name 用 identifier 格式 minecraft:block/stone 对应资源路径 textures/block/stone
public sealed class TextureAtlasSprite
{
    //Name sprite 标识符 minecraft:block/stone
    public string Name { get; }
    //AtlasX/AtlasY sprite 左上角在图集中的像素坐标
    public int AtlasX { get; }
    //AtlasY 顶部对齐 Vulkan V=0 在顶部
    public int AtlasY { get; }
    //Width/Height sprite 像素尺寸
    public int Width { get; }
    public int Height { get; }
    //AtlasWidth/AtlasHeight 整个图集尺寸用于算 UV
    public int AtlasWidth { get; }
    public int AtlasHeight { get; }
    //Pixels sprite 原始像素数据 RGBA 用于上传到图集区域
    //null 表示无像素数据（占位 sprite）Bake 时跳过上传
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

    //U0/V0 左上角 UV Vulkan 纹理 V=0 顶部
    public float U0 => (float)AtlasX / AtlasWidth;
    public float V0 => (float)AtlasY / AtlasHeight;
    //U1/V1 右下角 UV
    public float U1 => (float)(AtlasX + Width) / AtlasWidth;
    public float V1 => (float)(AtlasY + Height) / AtlasHeight;

    //MapU 把模型局部 UV [0,1] 映射到图集 UV
    //模型 face 的 uv 是 [0,1] 范围烘焙时调此方法转图集坐标
    public float MapU(float u) => U0 + (U1 - U0) * u;
    public float MapV(float v) => V0 + (V1 - V0) * v;
}
