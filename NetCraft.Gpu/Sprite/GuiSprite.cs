namespace NetCraft.Gpu.Sprite;

//GuiSprite 单个 sprite 对应一张 PNG + .mcmeta scaling 配置
//简化版 TextureAtlasSprite NetCraft 不做图集打包每个 sprite 独立 GpuImage
//TextureId 由 GuiResourceManager.RegisterTexture 返回供 GuiRenderContext.DrawImage 用
//Texture 持有 GpuImage+Sampler 由 GuiResourceManager 注册
//Width/Height 是源 PNG 实际像素尺寸 Scaling 是.mcmeta 解析结果
public sealed class GuiSprite
{
    public int TextureId { get; }
    public TextureSetup Texture { get; }
    public int Width { get; }
    public int Height { get; }
    public GuiSpriteScaling Scaling { get; }

    public GuiSprite(int textureId, TextureSetup texture, int width, int height, GuiSpriteScaling scaling)
    {
        TextureId = textureId;
        Texture = texture;
        Width = width;
        Height = height;
        Scaling = scaling;
    }
}
