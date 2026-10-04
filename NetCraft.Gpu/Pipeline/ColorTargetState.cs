namespace NetCraft.Gpu.Pipeline;

//ColorTargetState 颜色附件状态对标原版 ColorTargetState record
//描述 pipeline 输出附件的格式/混合/写掩码
//blendFunction=null 表示不启用混合 writeMask 默认 WRITE_ALL
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

    //DEFAULT 默认状态 RGBA8_UNORM 不混合 全通道写入
    public static readonly ColorTargetState DEFAULT = new(null, GpuFormat.R8G8B8A8Unorm, WriteAll);

    public ColorTargetState(BlendFunction blendFunction)
        : this(blendFunction, GpuFormat.R8G8B8A8Unorm, WriteAll) { }

    public bool RedChannel => (WriteMask & WriteRed) != 0;
    public bool GreenChannel => (WriteMask & WriteGreen) != 0;
    public bool BlueChannel => (WriteMask & WriteBlue) != 0;
    public bool AlphaChannel => (WriteMask & WriteAlpha) != 0;
}
