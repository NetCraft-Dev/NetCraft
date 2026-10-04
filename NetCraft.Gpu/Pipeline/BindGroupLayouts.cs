namespace NetCraft.Gpu.Pipeline;

//BindGroupLayouts 绑定组布局预设对标原版 BindGroupLayouts
//声明 GLOBALS/MATRICES_PROJECTION/SAMPLER0 等共享 BindGroupLayout 供 RenderPipelines 复用
public static class BindGroupLayouts
{
    //GLOBALS 全局 uniform 块含 GameTime/ScreenSize 等
    public static readonly BindGroupLayout GLOBALS = BindGroupLayout.Create()
        .AddUniform("Globals", UniformType.Mat4)
        .Build();

    //MATRICES_PROJECTION 投影矩阵 uniform 块
    public static readonly BindGroupLayout MATRICES_PROJECTION = BindGroupLayout.Create()
        .AddUniform("Matrices", UniformType.Mat4)
        .Build();

    //PROJECTION 仅含投影矩阵用于 chunk 渲染等独立矩阵场景
    public static readonly BindGroupLayout PROJECTION = BindGroupLayout.Create()
        .AddUniform("Proj", UniformType.Mat4)
        .Build();

    //FOG 雾效参数 uniform 块
    public static readonly BindGroupLayout FOG = BindGroupLayout.Create()
        .AddUniform("Fog", UniformType.Vec4)
        .Build();

    //LIGHTING 光照参数 uniform 块
    public static readonly BindGroupLayout LIGHTING = BindGroupLayout.Create()
        .AddUniform("Lighting", UniformType.Vec4)
        .Build();

    //ITEM_MATRICES 3D 物品 MVP 矩阵 uniform 块 model+view+proj 三个 mat4
    public static readonly BindGroupLayout ITEM_MATRICES = BindGroupLayout.Create()
        .AddUniform("Mvp", UniformType.Mat4)
        .Build();

    //ITEM_LIGHTING 3D 物品方向光照 uniform 块 2 个 vec4 含光方向 xyz w 未用
    public static readonly BindGroupLayout ITEM_LIGHTING = BindGroupLayout.Create()
        .AddUniform("Lighting", UniformType.Vec4)
        .Build();

    //ITEM_LIGHTMAP 3D 物品 lightmap 采样器 16x16 光照贴图按顶点 light 坐标采样
    public static readonly BindGroupLayout ITEM_LIGHTMAP = BindGroupLayout.Create()
        .AddSampler("LightmapSampler")
        .Build();

    //ITEM_ATLAS 3D 物品纹理图集采样器按顶点 UV 采样物品纹理
    public static readonly BindGroupLayout ITEM_ATLAS = BindGroupLayout.Create()
        .AddSampler("AtlasSampler")
        .Build();

    //SAMPLER0 单纹理采样器
    public static readonly BindGroupLayout SAMPLER0 = BindGroupLayout.Create()
        .AddSampler("Sampler0")
        .Build();

    //SAMPLER0_SAMPLER1 双纹理采样器
    public static readonly BindGroupLayout SAMPLER0_SAMPLER1 = BindGroupLayout.Create()
        .AddSampler("Sampler0")
        .AddSampler("Sampler1")
        .Build();

    //SAMPLER0_SAMPLER2 双纹理采样器第二槽常用于 lightmap
    public static readonly BindGroupLayout SAMPLER0_SAMPLER2 = BindGroupLayout.Create()
        .AddSampler("Sampler0")
        .AddSampler("Sampler2")
        .Build();

    //SAMPLER0_SAMPLER1_SAMPLER2 三纹理采样器
    public static readonly BindGroupLayout SAMPLER0_SAMPLER1_SAMPLER2 = BindGroupLayout.Create()
        .AddSampler("Sampler0")
        .AddSampler("Sampler1")
        .AddSampler("Sampler2")
        .Build();

    //IN_SAMPLER 输入纹理 blit 用
    public static readonly BindGroupLayout IN_SAMPLER = BindGroupLayout.Create()
        .AddSampler("InSampler")
        .Build();

    //BLUR_CONFIG blur 参数 uniform 块 vec4 Data xy=BlurDir z=Radius w=unused
    public static readonly BindGroupLayout BLUR_CONFIG = BindGroupLayout.Create()
        .AddUniform("BlurConfig", UniformType.Vec4)
        .Build();
}
