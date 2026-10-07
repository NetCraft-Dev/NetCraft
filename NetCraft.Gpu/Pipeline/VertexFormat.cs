namespace NetCraft.Gpu.Pipeline;

//VertexElement vertex attribute element
public readonly record struct VertexElement(string Name, VertexElementFormat Format, int Offset);

//VertexElementFormat vertex element data format
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

//VertexFormat vertex format, maps to vanilla VertexFormat
//Describes the vertex layout of one binding slot; the total stride accumulates from the elements
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

//DefaultVertexFormat vertex format presets, maps to vanilla DefaultVertexFormat
//POSITION_COLOR for GUI solid color POSITION_TEX_COLOR for textured GUI
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

    //POSITION_COLOR_UV_LIGHT_NORMAL 3D item vertex format, a simplified vanilla version without overlay
    //VertexConsumer3D outputs a 10-float vertex; color/light are unused for now and the shader skips locations 1/3
    public static readonly VertexFormat POSITION_COLOR_UV_LIGHT_NORMAL = VertexFormat.Create()
        .Add("Position", VertexElementFormat.Vec3)
        .Add("Color", VertexElementFormat.Float)
        .Add("UV0", VertexElementFormat.Vec2)
        .Add("Light", VertexElementFormat.Float)
        .Add("Normal", VertexElementFormat.Vec3)
        .Build();

    //POSITION_COLOR_TEX_OVERLAY_LIGHT_NORMAL entity vertex format, maps to the vanilla entity format
    //Adds an Overlay attribute over blocks, used for the red damage flash; stride 44 bytes, color/light/overlay store packed ints in Float
    public static readonly VertexFormat POSITION_COLOR_TEX_OVERLAY_LIGHT_NORMAL = VertexFormat.Create()
        .Add("Position", VertexElementFormat.Vec3)
        .Add("Color", VertexElementFormat.Float)
        .Add("UV0", VertexElementFormat.Vec2)
        .Add("Overlay", VertexElementFormat.Float)
        .Add("Light", VertexElementFormat.Float)
        .Add("Normal", VertexElementFormat.Vec3)
        .Build();
}
