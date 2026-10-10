namespace NetCraft.Client.Blaze3d;

//GpuFormat pixel format, maps to vanilla com.mojang.blaze3d.GpuFormat
public enum GpuFormat
{
    R8Unorm, R8Snorm,
    Rg8Unorm, Rg8Snorm,
    Rgb8Unorm, Rgb8Snorm,
    Rgba8Unorm, Rgba8Snorm,
    Bgra8Unorm, Rgba8UnormSrgb, Bgra8UnormSrgb,
    R16Unorm, R16Snorm,
    Rg16Unorm, Rg16Snorm,
    Rgb16Unorm, Rgb16Snorm,
    Rgba16Unorm, Rgba16Snorm,
    R8Uint, R8Sint,
    Rg8Uint, Rg8Sint,
    Rgb8Uint, Rgb8Sint,
    Rgba8Uint, Rgba8Sint,
    R16Uint, R16Sint,
    Rg16Uint, Rg16Sint,
    Rgb16Uint, Rgb16Sint,
    Rgba16Uint, Rgba16Sint,
    R32Uint, R32Sint,
    Rg32Uint, Rg32Sint,
    Rgb32Uint, Rgb32Sint,
    Rgba32Uint, Rgba32Sint,
    R16Float, Rg16Float, Rgb16Float, Rgba16Float,
    R32Float, Rg32Float, Rgb32Float, Rgba32Float,
    Rgb10A2Unorm, Rgb10A2Uint, Rg11B10Float,
    D32Float, D32FloatS8Uint, D24UnormS8Uint, D16Unorm, S8Uint
}

//GpuComponentType component type of a GpuFormat, maps to vanilla GpuFormat.ComponentType
public enum GpuComponentType
{
    Unorm8, Snorm8, Uint8, Sint8,
    Unorm16, Snorm16, Uint16, Sint16, Float16,
    Uint32, Sint32, Float32,
    Opaque8, Opaque16, Opaque32, Opaque64
}

//GpuFormatExtensions query helpers for GpuFormat, maps to the accessors of vanilla GpuFormat
public static class GpuFormatExtensions
{
    //ByteSize bytes of one component of this type, maps to vanilla ComponentType.byteSize
    public static int ByteSize(this GpuComponentType type) => type switch
    {
        GpuComponentType.Unorm8 or GpuComponentType.Snorm8 or GpuComponentType.Uint8 or GpuComponentType.Sint8 or GpuComponentType.Opaque8 => 1,
        GpuComponentType.Unorm16 or GpuComponentType.Snorm16 or GpuComponentType.Uint16 or GpuComponentType.Sint16 or GpuComponentType.Float16 or GpuComponentType.Opaque16 => 2,
        GpuComponentType.Uint32 or GpuComponentType.Sint32 or GpuComponentType.Float32 or GpuComponentType.Opaque32 => 4,
        GpuComponentType.Opaque64 => 8,
        _ => 1
    };

    //ComponentType component type of the format, maps to vanilla GpuFormat.componentType
    public static GpuComponentType ComponentType(this GpuFormat format) => format switch
    {
        GpuFormat.R8Unorm or GpuFormat.Rg8Unorm or GpuFormat.Rgb8Unorm or GpuFormat.Rgba8Unorm
            or GpuFormat.Bgra8Unorm or GpuFormat.Rgba8UnormSrgb or GpuFormat.Bgra8UnormSrgb => GpuComponentType.Unorm8,
        GpuFormat.R8Snorm or GpuFormat.Rg8Snorm or GpuFormat.Rgb8Snorm or GpuFormat.Rgba8Snorm => GpuComponentType.Snorm8,
        GpuFormat.R16Unorm or GpuFormat.Rg16Unorm or GpuFormat.Rgb16Unorm or GpuFormat.Rgba16Unorm => GpuComponentType.Unorm16,
        GpuFormat.R16Snorm or GpuFormat.Rg16Snorm or GpuFormat.Rgb16Snorm or GpuFormat.Rgba16Snorm => GpuComponentType.Snorm16,
        GpuFormat.R8Uint or GpuFormat.Rg8Uint or GpuFormat.Rgb8Uint or GpuFormat.Rgba8Uint => GpuComponentType.Uint8,
        GpuFormat.R8Sint or GpuFormat.Rg8Sint or GpuFormat.Rgb8Sint or GpuFormat.Rgba8Sint => GpuComponentType.Sint8,
        GpuFormat.R16Uint or GpuFormat.Rg16Uint or GpuFormat.Rgb16Uint or GpuFormat.Rgba16Uint => GpuComponentType.Uint16,
        GpuFormat.R16Sint or GpuFormat.Rg16Sint or GpuFormat.Rgb16Sint or GpuFormat.Rgba16Sint => GpuComponentType.Sint16,
        GpuFormat.R32Uint or GpuFormat.Rg32Uint or GpuFormat.Rgb32Uint or GpuFormat.Rgba32Uint => GpuComponentType.Uint32,
        GpuFormat.R32Sint or GpuFormat.Rg32Sint or GpuFormat.Rgb32Sint or GpuFormat.Rgba32Sint => GpuComponentType.Sint32,
        GpuFormat.R16Float or GpuFormat.Rg16Float or GpuFormat.Rgb16Float or GpuFormat.Rgba16Float => GpuComponentType.Float16,
        GpuFormat.R32Float or GpuFormat.Rg32Float or GpuFormat.Rgb32Float or GpuFormat.Rgba32Float => GpuComponentType.Float32,
        GpuFormat.Rgb10A2Unorm or GpuFormat.Rgb10A2Uint or GpuFormat.Rg11B10Float or GpuFormat.D32Float or GpuFormat.D24UnormS8Uint => GpuComponentType.Opaque32,
        GpuFormat.D32FloatS8Uint => GpuComponentType.Opaque64,
        GpuFormat.D16Unorm => GpuComponentType.Opaque16,
        GpuFormat.S8Uint => GpuComponentType.Opaque8,
        _ => GpuComponentType.Opaque8
    };

    //ComponentCount number of components of the format, maps to vanilla GpuFormat.componentCount
    public static int ComponentCount(this GpuFormat format) => format switch
    {
        GpuFormat.R8Unorm or GpuFormat.R8Snorm or GpuFormat.R16Unorm or GpuFormat.R16Snorm or GpuFormat.R8Uint or GpuFormat.R8Sint
            or GpuFormat.R16Uint or GpuFormat.R16Sint or GpuFormat.R32Uint or GpuFormat.R32Sint or GpuFormat.R16Float or GpuFormat.R32Float => 1,
        GpuFormat.Rg8Unorm or GpuFormat.Rg8Snorm or GpuFormat.Rg16Unorm or GpuFormat.Rg16Snorm or GpuFormat.Rg8Uint or GpuFormat.Rg8Sint
            or GpuFormat.Rg16Uint or GpuFormat.Rg16Sint or GpuFormat.Rg32Uint or GpuFormat.Rg32Sint or GpuFormat.Rg16Float or GpuFormat.Rg32Float => 2,
        GpuFormat.Rgb8Unorm or GpuFormat.Rgb8Snorm or GpuFormat.Rgb16Unorm or GpuFormat.Rgb16Snorm or GpuFormat.Rgb8Uint or GpuFormat.Rgb8Sint
            or GpuFormat.Rgb16Uint or GpuFormat.Rgb16Sint or GpuFormat.Rgb32Uint or GpuFormat.Rgb32Sint or GpuFormat.Rgb16Float or GpuFormat.Rgb32Float => 3,
        GpuFormat.Rgba8Unorm or GpuFormat.Rgba8Snorm or GpuFormat.Rgba16Unorm or GpuFormat.Rgba16Snorm or GpuFormat.Rgba8Uint or GpuFormat.Rgba8Sint
            or GpuFormat.Rgba16Uint or GpuFormat.Rgba16Sint or GpuFormat.Rgba32Uint or GpuFormat.Rgba32Sint or GpuFormat.Rgba16Float or GpuFormat.Rgba32Float
            or GpuFormat.Bgra8Unorm or GpuFormat.Rgba8UnormSrgb or GpuFormat.Bgra8UnormSrgb => 4,
        _ => 1
    };

    //BlockSize bytes of one block of the format, maps to vanilla GpuFormat.blockSize
    public static int BlockSize(this GpuFormat format) => format.ComponentType().ByteSize() * format.ComponentCount();

    //ByteAlignment byte alignment of the format, maps to vanilla GpuFormat.byteAlignment
    public static int ByteAlignment(this GpuFormat format) => format.ComponentType().ByteSize();

    //HasDepthAspect whether the format carries a depth aspect, maps to vanilla GpuFormat.hasDepthAspect
    public static bool HasDepthAspect(this GpuFormat format) =>
        format is GpuFormat.D32Float or GpuFormat.D32FloatS8Uint or GpuFormat.D24UnormS8Uint or GpuFormat.D16Unorm;

    //HasStencilAspect whether the format carries a stencil aspect, maps to vanilla GpuFormat.hasStencilAspect
    public static bool HasStencilAspect(this GpuFormat format) =>
        format is GpuFormat.S8Uint or GpuFormat.D32FloatS8Uint or GpuFormat.D24UnormS8Uint;

    //HasColorAspect whether the format carries only a color aspect, maps to vanilla GpuFormat.hasColorAspect
    public static bool HasColorAspect(this GpuFormat format) => !format.HasDepthAspect() && !format.HasStencilAspect();
}
