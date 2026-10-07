namespace NetCraft.Gpu.Pipeline;

//ColorTargetState color attachment state, maps to vanilla ColorTargetState record
//Describes the format/blend/write mask of the pipeline output attachment
//blendFunction=null means blending is disabled; writeMask defaults to WRITE_ALL
public readonly record struct ColorTargetState(BlendFunction? BlendFunction, GpuFormat Format, int WriteMask)
{
    public const int WriteRed = 1;
    public const int WriteGreen = 2;
    public const int WriteBlue = 4;
    public const int WriteAlpha = 8;
    public const int WriteColor = WriteRed | WriteGreen | WriteBlue;
    public const int WriteAll = WriteRed | WriteGreen | WriteBlue | WriteAlpha;
    public const int WriteNone = 0;
    public const int MaxColorTargets = 8;

    //DEFAULT default state RGBA8_UNORM no blending all-channel write
    public static readonly ColorTargetState DEFAULT = new(null, GpuFormat.R8G8B8A8Unorm, WriteAll);

    public ColorTargetState(BlendFunction blendFunction)
        : this(blendFunction, GpuFormat.R8G8B8A8Unorm, WriteAll) { }

    public bool RedChannel => (WriteMask & WriteRed) != 0;
    public bool GreenChannel => (WriteMask & WriteGreen) != 0;
    public bool BlueChannel => (WriteMask & WriteBlue) != 0;
    public bool AlphaChannel => (WriteMask & WriteAlpha) != 0;
}
