namespace NetCraft.Gpu.Pipeline;

//BlendEquation blend equation, maps to vanilla BlendEquation record
//Describes the src*srcFactor OP dst*dstFactor formula for the color or alpha channel
public readonly record struct BlendEquation(BlendFactor SourceFactor, BlendFactor DestFactor, BlendOp Op);
