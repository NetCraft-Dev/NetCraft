namespace NetCraft.Gpu.Pipeline;

//BindGroupLayouts bind group layout presets, maps to vanilla BindGroupLayouts
//Declares shared BindGroupLayouts like GLOBALS/MATRICES_PROJECTION/SAMPLER0 for RenderPipelines to reuse
public static class BindGroupLayouts
{
    //GLOBALS global uniform block holding GameTime/ScreenSize etc.
    public static readonly BindGroupLayout GLOBALS = BindGroupLayout.Create()
        .AddUniform("Globals", UniformType.Mat4)
        .Build();

    //MATRICES_PROJECTION projection matrix uniform block
    public static readonly BindGroupLayout MATRICES_PROJECTION = BindGroupLayout.Create()
        .AddUniform("Matrices", UniformType.Mat4)
        .Build();

    //PROJECTION holds only the projection matrix, for standalone-matrix cases like chunk rendering
    public static readonly BindGroupLayout PROJECTION = BindGroupLayout.Create()
        .AddUniform("Proj", UniformType.Mat4)
        .Build();

    //FOG fog parameters uniform block
    public static readonly BindGroupLayout FOG = BindGroupLayout.Create()
        .AddUniform("Fog", UniformType.Vec4)
        .Build();

    //LIGHTING lighting parameters uniform block
    public static readonly BindGroupLayout LIGHTING = BindGroupLayout.Create()
        .AddUniform("Lighting", UniformType.Vec4)
        .Build();

    //ITEM_MATRICES 3D item MVP matrix uniform block with three mat4: model+view+proj
    public static readonly BindGroupLayout ITEM_MATRICES = BindGroupLayout.Create()
        .AddUniform("Mvp", UniformType.Mat4)
        .Build();

    //ITEM_LIGHTING 3D item directional lighting uniform block with two vec4 carrying the light direction xyz; w is unused
    public static readonly BindGroupLayout ITEM_LIGHTING = BindGroupLayout.Create()
        .AddUniform("Lighting", UniformType.Vec4)
        .Build();

    //ITEM_LIGHTMAP 3D item lightmap sampler; a 16x16 lightmap sampled by the vertex light coordinates
    public static readonly BindGroupLayout ITEM_LIGHTMAP = BindGroupLayout.Create()
        .AddSampler("LightmapSampler")
        .Build();

    //ITEM_ATLAS 3D item texture atlas sampler sampling the item texture by vertex UV
    public static readonly BindGroupLayout ITEM_ATLAS = BindGroupLayout.Create()
        .AddSampler("AtlasSampler")
        .Build();

    //SAMPLER0 single texture sampler
    public static readonly BindGroupLayout SAMPLER0 = BindGroupLayout.Create()
        .AddSampler("Sampler0")
        .Build();

    //SAMPLER0_SAMPLER1 two texture samplers
    public static readonly BindGroupLayout SAMPLER0_SAMPLER1 = BindGroupLayout.Create()
        .AddSampler("Sampler0")
        .AddSampler("Sampler1")
        .Build();

    //SAMPLER0_SAMPLER2 two texture samplers; the second slot is often the lightmap
    public static readonly BindGroupLayout SAMPLER0_SAMPLER2 = BindGroupLayout.Create()
        .AddSampler("Sampler0")
        .AddSampler("Sampler2")
        .Build();

    //SAMPLER0_SAMPLER1_SAMPLER2 three texture samplers
    public static readonly BindGroupLayout SAMPLER0_SAMPLER1_SAMPLER2 = BindGroupLayout.Create()
        .AddSampler("Sampler0")
        .AddSampler("Sampler1")
        .AddSampler("Sampler2")
        .Build();

    //IN_SAMPLER input texture, used for blit
    public static readonly BindGroupLayout IN_SAMPLER = BindGroupLayout.Create()
        .AddSampler("InSampler")
        .Build();

    //BLUR_CONFIG blur parameters uniform block, vec4 Data xy=BlurDir z=Radius w=unused
    public static readonly BindGroupLayout BLUR_CONFIG = BindGroupLayout.Create()
        .AddUniform("BlurConfig", UniformType.Vec4)
        .Build();
}
