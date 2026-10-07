namespace NetCraft.Gpu.Pipeline;

//DepthStencilState depth-stencil state, maps to vanilla DepthStencilState record
//depthTest=null means depth testing is disabled; writeDepth controls whether the depth buffer is written
//depthBiasScaleFactor/depthBiasConstant used to eliminate z-fighting
public readonly record struct DepthStencilState(
    CompareOp? DepthTest,
    bool WriteDepth,
    float DepthBiasScaleFactor,
    float DepthBiasConstant)
{
    //DEFAULT default depth state Less write no bias
    //The current Camera projection uses the standard Vulkan [0,1] depth near=0 far=1; Less matches clearDepth=1
    //Vanilla uses GreaterOrEqual because of reversed-Z; NetCraft does not use reversed-Z and switches to Less for consistency
    public static readonly DepthStencilState DEFAULT = new(CompareOp.Less, true, 0f, 0f);

    public DepthStencilState(CompareOp depthTest, bool depthWrite)
        : this(depthTest, depthWrite, 0f, 0f) { }
}
