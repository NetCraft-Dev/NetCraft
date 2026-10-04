using NetCraft.Gpu;

namespace NetCraft.Gpu.Font;

//IGlyphInfo 字形度量对标原版 GlyphInfo
//advance 字形前进宽度 bold/shadow 偏移加粗阴影时用
public interface IGlyphInfo
{
    float Advance { get; }
    float BoldOffset => 1.0f;
    float ShadowOffset => 1.0f;

    //GetAdvance bold 时加 BoldOffset 加粗字形更宽
    float GetAdvance(bool bold) => Advance + (bold ? BoldOffset : 0f);

    //Simple 创建仅 advance 的简单度量空格等无位图字形用
    static IGlyphInfo Simple(float advance) => new SimpleGlyphInfo(advance);
}

//SimpleGlyphInfo 仅 advance 的简单度量
public sealed class SimpleGlyphInfo : IGlyphInfo
{
    public float Advance { get; }
    public SimpleGlyphInfo(float advance) => Advance = advance;
}

//IGlyphBitmap 栅格化位图对标原版 GlyphBitmap
//原版用 upload(x,y,GpuTexture) 直接上传 GPU 我们用 GetPixels 返回像素数组由 Stitcher 上传
//IsColored=false 返回 R8 单通道长度=PixelWidth*PixelHeight
//IsColored=true  返回 RGBA8 长度=PixelWidth*PixelHeight*4
public interface IGlyphBitmap
{
    int PixelWidth { get; }
    int PixelHeight { get; }
    float Oversample { get; }
    bool IsColored { get; }

    //BearingLeft/BearingTop 默认值对标原版 GlyphBitmap 接口默认实现 0.0/7.0
    float BearingLeft => 0.0f;
    float BearingTop => IGlyphProvider.Baseline;

    //Left/Right/Top/Bottom 像素坐标换算 oversample 缩放后实际占据空间
    float Left => BearingLeft;
    float Right => Left + PixelWidth / Oversample;
    float Top => IGlyphProvider.Baseline - BearingTop;
    float Bottom => Top + PixelHeight / Oversample;

    //GetPixels 返回栅格化后的像素数据懒加载首次调用时栅格化并缓存
    byte[] GetPixels();
}

//BakedGlyph 烘焙后字形对标原版 BakedGlyph
//含 IGlyphInfo 度量 + Render 把字形提交到渲染上下文
//原版 createGlyph 依赖 TextRenderable/Style 业务对象 NetCraft 在 Gpu 层用 GlyphRenderOptions 替代
public abstract class BakedGlyph
{
    public abstract IGlyphInfo Info { get; }

    //Render 把字形提交到渲染上下文 options 含 x/y/color/shadowColor/bold/italic 等渲染参数
    //对标原版 BakedSheetGlyph.renderChar 含 italic/bold/shadow 完整逻辑
    public abstract void Render(IGuiRenderContext context, in GlyphRenderOptions options);
}

//IUnbakedGlyph 未烘焙字形对标原版 UnbakedGlyph
//bake 后得到 BakedGlyph info 返回度量信息
public interface IUnbakedGlyph
{
    IGlyphInfo Info { get; }
    BakedGlyph Bake(Stitcher stitcher);

    //Stitcher 缝合器把 IGlyphInfo+IGlyphBitmap 缝到图集返回 BakedGlyph
    public interface Stitcher
    {
        BakedGlyph Stitch(IGlyphInfo info, IGlyphBitmap bitmap);
        BakedGlyph GetMissing();
    }
}

//EmptyGlyph 空字形对标原版 EmptyGlyph
//SpaceProvider 用它表示空格只有 advance 不渲染位图
public sealed class EmptyGlyph : IUnbakedGlyph
{
    private readonly float _advance;
    public EmptyGlyph(float advance) => _advance = advance;

    public IGlyphInfo Info => IGlyphInfo.Simple(_advance);

    //Bake 返回空 BakedGlyph 不渲染位图只占 advance
    public BakedGlyph Bake(IUnbakedGlyph.Stitcher stitcher) => new EmptyBakedGlyph(Info);

    //EmptyBakedGlyph 空格字形 Render 不提交任何渲染指令
    private sealed class EmptyBakedGlyph : BakedGlyph
    {
        private readonly IGlyphInfo _info;
        public EmptyBakedGlyph(IGlyphInfo info) => _info = info;
        public override IGlyphInfo Info => _info;
        public override void Render(IGuiRenderContext context, in GlyphRenderOptions options) { }
    }
}

//IGlyphProvider 字形提供器对标原版 GlyphProvider extends AutoCloseable
//按 codepoint 返回 IUnbakedGlyph providers 链按顺序查找首个命中
public interface IGlyphProvider : IDisposable
{
    //Baseline 基线高度对标原版 GlyphProvider.BASELINE=7.0
    public const float Baseline = 7.0f;

    IReadOnlySet<int> GetSupportedGlyphs();

    //GetGlyph 返回 codepoint 对应字形无则 null
    IUnbakedGlyph? GetGlyph(int codepoint);

    //Conditional 条件 provider 持有 provider+filter FontSet 按选项激活
    public sealed class Conditional
    {
        public IGlyphProvider Provider { get; }
        public FontOptionFilter Filter { get; }

        public Conditional(IGlyphProvider provider, FontOptionFilter filter)
        {
            Provider = provider;
            Filter = filter;
        }
    }
}
