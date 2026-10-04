namespace NetCraft.Gpu.Pipeline;

//BlendFunction 混合函数对标原版 BlendFunction record
//分别描述颜色通道和 alpha 通道的混合方程
//提供 LIGHTNING/TRANSLUCENT/INVERT 等静态预设供 RenderPipelines 直接引用
public readonly record struct BlendFunction(BlendEquation Color, BlendEquation Alpha)
{
    //LIGHTNING 闪电效果 src_alpha + dst_alpha=1 加色叠加
    public static readonly BlendFunction LIGHTNING = new(
        new BlendEquation(BlendFactor.SrcAlpha, BlendFactor.One, BlendOp.Add),
        new BlendEquation(BlendFactor.SrcAlpha, BlendFactor.One, BlendOp.Add));

    //GLINT 闪烁效果 src_color + dst_color=1 alpha 不混合
    public static readonly BlendFunction GLINT = new(
        new BlendEquation(BlendFactor.SrcColor, BlendFactor.One, BlendOp.Add),
        new BlendEquation(BlendFactor.Zero, BlendFactor.One, BlendOp.Add));

    //OVERLAY 覆盖效果颜色按 src_alpha 混合 alpha 取 src
    public static readonly BlendFunction OVERLAY = new(
        new BlendEquation(BlendFactor.SrcAlpha, BlendFactor.One, BlendOp.Add),
        new BlendEquation(BlendFactor.One, BlendFactor.Zero, BlendOp.Add));

    //TRANSLUCENT 半透明标准 alpha 混合颜色和 alpha 都用 src_alpha*(1-dst_alpha)
    public static readonly BlendFunction TRANSLUCENT = new(
        new BlendEquation(BlendFactor.SrcAlpha, BlendFactor.OneMinusSrcAlpha, BlendOp.Add),
        new BlendEquation(BlendFactor.One, BlendFactor.OneMinusSrcAlpha, BlendOp.Add));

    //TRANSLUCENT_PREMULTIPLIED_ALPHA 预乘 alpha 半透明 src 已预乘 alpha
    public static readonly BlendFunction TRANSLUCENT_PREMULTIPLIED_ALPHA = new(
        new BlendEquation(BlendFactor.One, BlendFactor.OneMinusSrcAlpha, BlendOp.Add),
        new BlendEquation(BlendFactor.One, BlendFactor.OneMinusSrcAlpha, BlendOp.Add));

    //ADDITIVE 加色混合用于发光/高亮元素
    public static readonly BlendFunction ADDITIVE = new(
        new BlendEquation(BlendFactor.One, BlendFactor.One, BlendOp.Add),
        new BlendEquation(BlendFactor.One, BlendFactor.One, BlendOp.Add));

    //ENTITY_OUTLINE_BLIT 实体轮廓 blit 颜色按 src_alpha 混合 alpha 取 0
    public static readonly BlendFunction ENTITY_OUTLINE_BLIT = new(
        new BlendEquation(BlendFactor.SrcAlpha, BlendFactor.OneMinusSrcAlpha, BlendOp.Add),
        new BlendEquation(BlendFactor.Zero, BlendFactor.One, BlendOp.Add));

    //INVERT 反色混合用于十字准星等反相元素 1-dst_color / 1-src_color
    public static readonly BlendFunction INVERT = new(
        new BlendEquation(BlendFactor.OneMinusDstColor, BlendFactor.OneMinusSrcColor, BlendOp.Add),
        new BlendEquation(BlendFactor.One, BlendFactor.Zero, BlendOp.Add));

    //FromFactors 共享 src/dst 因子+共享 op 的便捷工厂
    public static BlendFunction FromFactors(BlendFactor src, BlendFactor dst, BlendOp op) =>
        new(new BlendEquation(src, dst, op), new BlendEquation(src, dst, op));

    //FromFactors 双通道独立因子颜色 alpha 共享 ADD op
    public static BlendFunction FromFactors(
        BlendFactor srcColor, BlendFactor dstColor,
        BlendFactor srcAlpha, BlendFactor dstAlpha) =>
        new(
            new BlendEquation(srcColor, dstColor, BlendOp.Add),
            new BlendEquation(srcAlpha, dstAlpha, BlendOp.Add));
}
