namespace NetCraft.Gpu.Pipeline;

//BlendEquation 混合方程对标原版 BlendEquation record
//描述颜色或 alpha 通道的 src*srcFactor OP dst*dstFactor 公式
public readonly record struct BlendEquation(BlendFactor SourceFactor, BlendFactor DestFactor, BlendOp Op);
