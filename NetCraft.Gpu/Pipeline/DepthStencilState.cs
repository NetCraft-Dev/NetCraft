namespace NetCraft.Gpu.Pipeline;

//DepthStencilState 深度模板状态对标原版 DepthStencilState record
//depthTest=null 表示不启用深度测试 writeDepth 控制是否写入深度缓冲
//depthBiasScaleFactor/depthBiasConstant 用于消除 z-fighting
public readonly record struct DepthStencilState(
    CompareOp? DepthTest,
    bool WriteDepth,
    float DepthBiasScaleFactor,
    float DepthBiasConstant)
{
    //DEFAULT 默认深度状态 Less 写入无偏移
    //当前 Camera 投影用标准 Vulkan [0,1] 深度 near=0 far=1 Less 匹配 clearDepth=1
    //原版用 GreaterOrEqual 因 reversed-Z NetCraft 未用 reversed-Z 改 Less 保持一致
    public static readonly DepthStencilState DEFAULT = new(CompareOp.Less, true, 0f, 0f);

    public DepthStencilState(CompareOp depthTest, bool depthWrite)
        : this(depthTest, depthWrite, 0f, 0f) { }
}
