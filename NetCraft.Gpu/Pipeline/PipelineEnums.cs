namespace NetCraft.Gpu.Pipeline;

//BlendFactor 混合因子对标原版 BlendFactor
//控制 pipeline 颜色混合时源/目标颜色的权重来源
public enum BlendFactor
{
    Zero,
    One,
    SrcColor,
    OneMinusSrcColor,
    DstColor,
    OneMinusDstColor,
    SrcAlpha,
    OneMinusSrcAlpha,
    DstAlpha,
    OneMinusDstAlpha,
    ConstantColor,
    OneMinusConstantColor,
    ConstantAlpha,
    OneMinusConstantAlpha,
    SrcAlphaSaturate,
    Src1Color,
    OneMinusSrc1Color,
    Src1Alpha,
    OneMinusSrc1Alpha
}

//BlendOp 混合操作对标原版 BlendOp
//控制源/目标颜色按因子加权后如何组合
public enum BlendOp
{
    Add,
    Subtract,
    ReverseSubtract,
    Min,
    Max
}

//CompareOp 深度/模板比较运算对标原版 CompareOp
public enum CompareOp
{
    Never,
    Less,
    Equal,
    LessOrEqual,
    Greater,
    NotEqual,
    GreaterOrEqual,
    Always
}

//PolygonMode 多边形绘制模式对标原版 PolygonMode
public enum PolygonMode
{
    Fill,
    Line,
    Point
}

//PrimitiveTopology 图元拓扑对标原版 PrimitiveTopology
public enum PrimitiveTopology
{
    Points,
    Lines,
    LineStrip,
    TriangleList,
    TriangleStrip,
    TriangleFan,
    Quads
}

//GpuFormat GPU 像素格式对标原版 GpuFormat
//ColorTargetState 用其声明附件格式
public enum GpuFormat
{
    R8Unorm,
    R8G8Unorm,
    R8G8B8A8Unorm,
    B8G8R8A8Unorm,
    R8G8B8A8Srgb,
    B8G8R8A8Srgb,
    R16Float,
    R16G16Float,
    R16G16B16A16Float,
    R32Float,
    R32G32Float,
    R32G32B32A32Float,
    D32Sfloat,
    D24UnormS8Uint,
    D16Unorm
}

//UniformType shader uniform 类型对标原版 UniformType
//用于 BindGroupLayout.UniformDescription 描述 uniform buffer/纹理绑定的类型
public enum UniformType
{
    Mat4,
    Vec4,
    Vec3,
    Vec2,
    Float,
    Int,
    TexelBuffer,
    Sampler
}
