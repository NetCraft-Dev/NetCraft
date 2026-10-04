using System.Numerics;
using NetCraft.Gpu.Pipeline;
using StbImageSharp;

namespace NetCraft.Gpu;

//GuiResourceManager GUI 资源管理器阶段 5c
//管理 Projection uniform buffer + 字体图集纹理 + 图像纹理的 DescriptorSet
//descriptorResolver 把 TextureSetup 解析为 GpuDescriptorSet 供 GuiRenderer.Draw 绑定
//全局 DescriptorSet(GLOBALS+MATRICES)由调用方在 render pass 开始时绑定 set 0/1
public sealed class GuiResourceManager : IDisposable
{
    private readonly GpuDevice _device;
    private readonly int _surfaceWidth;
    private readonly int _surfaceHeight;
    private bool _disposed;

    //GLOBALS dummy uniform buffer + DescriptorSet set 0
    //gui.vert 声明 ScreenSize 但未使用传 dummy Mat4 避免布局不匹配
    private readonly GpuBuffer _globalsBuffer;
    private readonly GpuDescriptorSet _globalsDescriptorSet;

    //MATRICES_PROJECTION uniform buffer + DescriptorSet set 1
    private readonly GpuBuffer _projectionBuffer;
    private readonly GpuDescriptorSet _projectionDescriptorSet;

    //SAMPLER0 layout 供字体和图像纹理 DescriptorSet 共用
    private readonly GpuDescriptorLayout _samplerLayout;

    //字体图集纹理
    private readonly FontAtlas? _fontAtlas;
    private readonly GpuImage? _fontImage;
    private readonly GpuSampler? _fontSampler;
    private readonly TextureSetup? _fontTexture;
    private readonly GpuDescriptorSet? _fontDescriptorSet;

    //图像纹理 textureId → TextureSetup + DescriptorSet
    private readonly Dictionary<int, TextureSetup> _textures = new();
    private readonly Dictionary<string, int> _pathCache = new();
    private int _nextTextureId = 1;
    private readonly Dictionary<TextureSetup, GpuDescriptorSet> _descriptorSetCache = new();
    //_externalTextureIds 外部托管的 textureId 集合 Dispose 时不释放 GpuImage
    //ItemAtlas 的 AtlasTexture 由 ItemItemAtlas 自己管理生命周期这里只注册拿 textureId
    //_internalSamplers RegisterImage 内部创建的 sampler Dispose 时释放 GpuImage 不释放
    private readonly HashSet<int> _externalTextureIds = new();
    private readonly List<GpuSampler> _internalSamplers = new();
    private readonly IGpuLogger _logger;

    public GuiResourceManager(GpuDevice device, int surfaceWidth, int surfaceHeight,
        FontAtlas? fontAtlas, IGpuLogger? logger = null)
    {
        _device = device;
        _surfaceWidth = surfaceWidth;
        _surfaceHeight = surfaceHeight;
        _fontAtlas = fontAtlas;
        _logger = logger ?? new ConsoleGpuLogger();

        //GLOBALS layout 1 uniform Mat4
        var globalsLayoutDesc = new GpuDescriptorLayoutDescription();
        globalsLayoutDesc.Bindings.Add(new GpuDescriptorBinding
        {
            Binding = 0,
            DescriptorType = GpuDescriptorType.UniformBuffer,
            StageFlags = GpuShaderStageFlags.Vertex
        });
        var globalsLayout = _device.CreateDescriptorLayout(globalsLayoutDesc);
        _globalsBuffer = _device.CreateBuffer(64, GpuBufferUsage.UniformBuffer);
        _globalsDescriptorSet = _device.AllocateDescriptorSet(globalsLayout);
        _globalsDescriptorSet.WriteBuffer(0, _globalsBuffer, 0, -1);

        //MATRICES_PROJECTION layout 1 uniform Mat4
        var projLayoutDesc = new GpuDescriptorLayoutDescription();
        projLayoutDesc.Bindings.Add(new GpuDescriptorBinding
        {
            Binding = 0,
            DescriptorType = GpuDescriptorType.UniformBuffer,
            StageFlags = GpuShaderStageFlags.Vertex
        });
        var projLayout = _device.CreateDescriptorLayout(projLayoutDesc);
        _projectionBuffer = _device.CreateBuffer(64, GpuBufferUsage.UniformBuffer);
        _projectionDescriptorSet = _device.AllocateDescriptorSet(projLayout);
        _projectionDescriptorSet.WriteBuffer(0, _projectionBuffer, 0, -1);

        //SAMPLER0 layout 1 combined image sampler
        var samplerLayoutDesc = new GpuDescriptorLayoutDescription();
        samplerLayoutDesc.Bindings.Add(new GpuDescriptorBinding
        {
            Binding = 0,
            DescriptorType = GpuDescriptorType.CombinedImageSampler,
            StageFlags = GpuShaderStageFlags.Fragment
        });
        _samplerLayout = _device.CreateDescriptorLayout(samplerLayoutDesc);

        //字体图集纹理
        if (_fontAtlas is not null)
        {
            _fontImage = CreateFontImage(_fontAtlas);
            _fontSampler = _device.CreateSampler(new GpuSamplerDescription
            {
                LinearFilter = true,
                RepeatAddress = false
            });
            _fontTexture = TextureSetup.SingleTexture(_fontImage, _fontSampler);
            _fontDescriptorSet = _device.AllocateDescriptorSet(_samplerLayout);
            _fontDescriptorSet.WriteImage(0, _fontImage, _fontSampler);
        }

        UpdateProjection();
    }

    //UpdateProjection 每帧更新正交投影矩阵 actual 像素转 clip space
    //与 VulkanGuiRenderer.ToClipX/ToClipY 一致 clipX = px*2/W - 1 clipY = py*2/H - 1
    //Matrix4x4 row-major 上传前 Transpose 转 column-major 对齐 GLSL mat4
    public void UpdateProjection()
    {
        var w = (float)_surfaceWidth;
        var h = (float)_surfaceHeight;
        if (w <= 0) w = 1;
        if (h <= 0) h = 1;
        var proj = new Matrix4x4(
            2f / w, 0, 0, 0,
            0, 2f / h, 0, 0,
            0, 0, 1, 0,
            -1f, -1f, 0, 1);
        proj = Matrix4x4.Transpose(proj);
        _projectionBuffer.Upload<Matrix4x4>(new[] { proj });
    }

    //GlobalsDescriptorSet 全局 uniform DescriptorSet 绑定 set 0
    public GpuDescriptorSet GlobalsDescriptorSet => _globalsDescriptorSet;

    //ProjectionDescriptorSet 投影矩阵 DescriptorSet 绑定 set 1
    public GpuDescriptorSet ProjectionDescriptorSet => _projectionDescriptorSet;

    //FontTexture 字体图集 TextureSetup 供 GuiRenderContext 注入
    public TextureSetup? FontTexture => _fontTexture;

    //FontAtlas 字体图集供 GuiRenderContext 计算字形 UV
    public FontAtlas? FontAtlas => _fontAtlas;

    //RegisterTexture 按 path 加载 PNG 注册为纹理返回 textureId
    //同 path 第二次调直接返回缓存 id 文件不存在或解码失败返回 0
    public int RegisterTexture(string path)
    {
        if (_pathCache.TryGetValue(path, out var cached)) return cached;
        if (!File.Exists(path))
        {
            _logger.Warning($"纹理文件不存在 {path}");
            return 0;
        }
        byte[] bytes;
        try { bytes = File.ReadAllBytes(path); }
        catch (Exception ex) { _logger.Warning($"纹理读取失败 {path} {ex.Message}"); return 0; }
        ImageResult? result;
        try { result = ImageResult.FromMemory(bytes, ColorComponents.RedGreenBlueAlpha); }
        catch (Exception ex) { _logger.Warning($"纹理解码失败 {path} {ex.Message}"); return 0; }
        if (result is null || result.Width <= 0 || result.Height <= 0)
        {
            _logger.Warning($"纹理解码返回空 {path}");
            return 0;
        }
        var img = _device.CreateImage(new GpuImageDescription
        {
            Width = result.Width,
            Height = result.Height,
            Format = GpuImageFormat.R8G8B8A8Unorm,
            Usage = GpuImageUsage.SampledImage
        });
        img.Upload(result.Data);
        var sampler = _device.CreateSampler(new GpuSamplerDescription
        {
            LinearFilter = true,
            RepeatAddress = false
        });
        var texture = TextureSetup.SingleTexture(img, sampler);
        var id = _nextTextureId++;
        _textures[id] = texture;
        _pathCache[path] = id;
        return id;
    }

    //ResolveTexture 按 textureId 返回 TextureSetup 供 GuiRenderContext DrawImage 用
    public TextureSetup? ResolveTexture(int textureId)
    {
        return _textures.TryGetValue(textureId, out var t) ? t : null;
    }

    //ResolveDescriptorSet 把 TextureSetup 解析为 GpuDescriptorSet 供 GuiRenderer.Draw 绑定
    //NoTexture 返回 null 字体纹理返回缓存 DescriptorSet 图像纹理懒创建并缓存
    public GpuDescriptorSet? ResolveDescriptorSet(TextureSetup texture)
    {
        if (texture == TextureSetup.NoTexture) return null;
        if (_fontTexture is not null && texture == _fontTexture) return _fontDescriptorSet;
        if (_descriptorSetCache.TryGetValue(texture, out var cached)) return cached;
        if (texture.Texture0 is null || texture.Sampler0 is null) return null;
        var set = _device.AllocateDescriptorSet(_samplerLayout);
        set.WriteImage(0, texture.Texture0, texture.Sampler0);
        _descriptorSetCache[texture] = set;
        return set;
    }

    //RegisterFontTexture 注册动态字形图集纹理返回 TextureSetup
    //用于 F7 GlyphStitcher 创建 FontTexture 时注册图集 GpuImage 获得 TextureSetup
    //sampler 复用 LinearFilter+RepeatAddress=false 与 FontAtlas 一致
    //descriptorSet 懒创建由 ResolveDescriptorSet 缓存
    public TextureSetup RegisterFontTexture(GpuImage image)
    {
        var sampler = _device.CreateSampler(new GpuSamplerDescription
        {
            LinearFilter = true,
            RepeatAddress = false
        });
        var texture = TextureSetup.SingleTexture(image, sampler);
        return texture;
    }

    //RegisterImage 注册外部 GpuImage 返回 textureId 供 GuiRenderContext.DrawImage 用
    //不接管 GpuImage 所有权调用方自行 Dispose 内部创建 nearest sampler 物品图集像素风格
    //Dispose 时释放内部 sampler 但不释放 GpuImage（由 ItemItemAtlas 管理生命周期）
    public int RegisterImage(GpuImage image)
    {
        var sampler = _device.CreateSampler(new GpuSamplerDescription
        {
            LinearFilter = false,
            RepeatAddress = false
        });
        _internalSamplers.Add(sampler);
        var texture = TextureSetup.SingleTexture(image, sampler);
        var id = _nextTextureId++;
        _textures[id] = texture;
        _externalTextureIds.Add(id);
        return id;
    }

    //CreateFontImage 创建字体图集 GpuImage 上传像素数据
    private GpuImage CreateFontImage(FontAtlas atlas)
    {
        var img = _device.CreateImage(new GpuImageDescription
        {
            Width = atlas.AtlasWidth,
            Height = atlas.AtlasHeight,
            Format = GpuImageFormat.R8Unorm,
            Usage = GpuImageUsage.SampledImage
        });
        img.Upload(atlas.AtlasPixels);
        return img;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _globalsBuffer.Dispose();
        _projectionBuffer.Dispose();
        _fontImage?.Dispose();
        _fontSampler?.Dispose();
        foreach (var pair in _textures)
        {
            //外部托管纹理（ItemAtlas AtlasTexture）不释放 GpuImage 由所有者 Dispose
            //RegisterImage 内部创建的 sampler 单独释放
            if (_externalTextureIds.Contains(pair.Key)) continue;
            pair.Value.Texture0?.Dispose();
            pair.Value.Sampler0?.Dispose();
        }
        foreach (var s in _internalSamplers) s.Dispose();
        _disposed = true;
    }
}
