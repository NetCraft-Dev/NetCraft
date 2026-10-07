namespace NetCraft.Gpu.Sprite;

//GuiSprite a single sprite corresponding to one PNG + .mcmeta scaling config
//A simplified TextureAtlasSprite; NetCraft does no atlas packing, each sprite gets its own GpuImage
//TextureId returned by GuiResourceManager.RegisterTexture for GuiRenderContext.DrawImage
//Texture holds the GpuImage+Sampler registered by GuiResourceManager
//Width/Height are the source PNG's actual pixel size Scaling is the parsed .mcmeta result
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
