namespace NetCraft.Gpu.Pipeline;

//VertexElement 顶点属性元素
public readonly record struct VertexElement(string Name, VertexElementFormat Format, int Offset);

//VertexElementFormat 顶点元素数据格式
public enum VertexElementFormat
{
    Float,
    Vec2,
    Vec3,
    Vec4,
    UByte4Norm,
    UShort2Norm,
    Int,
    IVec2
}

//VertexFormat 顶点格式对标原版 VertexFormat
//描述一个 binding 槽的顶点布局 elements 总 stride 由 elements 自动累加
public sealed class VertexFormat
{
    private readonly List<VertexElement> _elements;

    public IReadOnlyList<VertexElement> Elements => _elements;
    public int Stride { get; }

    private VertexFormat(List<VertexElement> elements, int stride)
    {
        _elements = elements;
        Stride = stride;
    }

    public static Builder Create() => new();

    public sealed class Builder
    {
        private readonly List<VertexElement> _elements = new();
        private int _offset;

        public Builder Add(string name, VertexElementFormat format)
        {
            _elements.Add(new VertexElement(name, format, _offset));
            _offset += SizeOf(format);
            return this;
        }

        public VertexFormat Build() => new(_elements, _offset);

        private static int SizeOf(VertexElementFormat f) => f switch
        {
            VertexElementFormat.Float => 4,
            VertexElementFormat.Vec2 => 8,
            VertexElementFormat.Vec3 => 12,
            VertexElementFormat.Vec4 => 16,
            VertexElementFormat.UByte4Norm => 4,
            VertexElementFormat.UShort2Norm => 4,
            VertexElementFormat.Int => 4,
            VertexElementFormat.IVec2 => 8,
            _ => 4
        };
    }
}

//DefaultVertexFormat 顶点格式预设对标原版 DefaultVertexFormat
//POSITION_COLOR 用于纯色 GUI POSITION_TEX_COLOR 用于带纹理 GUI
public static class DefaultVertexFormat
{
    public static readonly VertexFormat POSITION_COLOR = VertexFormat.Create()
        .Add("Position", VertexElementFormat.Vec3)
        .Add("Color", VertexElementFormat.UByte4Norm)
        .Build();

    public static readonly VertexFormat POSITION_TEX_COLOR = VertexFormat.Create()
        .Add("Position", VertexElementFormat.Vec3)
        .Add("UV0", VertexElementFormat.Vec2)
        .Add("Color", VertexElementFormat.UByte4Norm)
        .Build();

    public static readonly VertexFormat POSITION_TEX = VertexFormat.Create()
        .Add("Position", VertexElementFormat.Vec3)
        .Add("UV0", VertexElementFormat.Vec2)
        .Build();

    //POSITION_COLOR_UV_LIGHT_NORMAL 3D 物品顶点格式对标原版简化版去 overlay
    //VertexConsumer3D 输出 10 float 顶点 color/light 暂未使用 shader 跳过 location 1/3
    public static readonly VertexFormat POSITION_COLOR_UV_LIGHT_NORMAL = VertexFormat.Create()
        .Add("Position", VertexElementFormat.Vec3)
        .Add("Color", VertexElementFormat.Float)
        .Add("UV0", VertexElementFormat.Vec2)
        .Add("Light", VertexElementFormat.Float)
        .Add("Normal", VertexElementFormat.Vec3)
        .Build();

    //POSITION_COLOR_TEX_OVERLAY_LIGHT_NORMAL 实体顶点格式对标原版 entity 格式
    //比方块多 Overlay 属性伤害红闪用 stride 44 字节 color/light/overlay 用 Float 存 packed int
    public static readonly VertexFormat POSITION_COLOR_TEX_OVERLAY_LIGHT_NORMAL = VertexFormat.Create()
        .Add("Position", VertexElementFormat.Vec3)
        .Add("Color", VertexElementFormat.Float)
        .Add("UV0", VertexElementFormat.Vec2)
        .Add("Overlay", VertexElementFormat.Float)
        .Add("Light", VertexElementFormat.Float)
        .Add("Normal", VertexElementFormat.Vec3)
        .Build();
}
