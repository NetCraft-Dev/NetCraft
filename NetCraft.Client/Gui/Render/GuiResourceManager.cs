using System.Numerics;
using NetCraft.Client.Blaze3d.Pipeline;
using NetCraft.Client.Render;
using StbImageSharp;
using NetCraft.Client.Blaze3d.Systems;
using NetCraft.Client.Blaze3d.Buffers;
using NetCraft.Client.Blaze3d.Textures;
using NetCraft.Client.Blaze3d.Shaders;
using NetCraft.Client.Blaze3d.Vertex;
using NetCraft.Client.Blaze3d.Vulkan;
using NetCraft.Client.Blaze3d.Platform;
using NetCraft.Client.Blaze3d.Font;
using NetCraft.Client.Blaze3d.Resource;
using NetCraft.Client.Blaze3d.Audio;
using NetCraft.Client.Blaze3d.Framegraph;
using NetCraft.Client.Blaze3d.Preprocessor;
using NetCraft.Client.Blaze3d.Util;
using NetCraft.Client.Render.Model;
using NetCraft.Client.Render.Texture;
using NetCraft.Client.Render.Texture.Atlas;
using NetCraft.Client.Render.Item;
using NetCraft.Client.Render.Entity;
using NetCraft.Client.Render.Entity.State;
using NetCraft.Client.Render.State.Gui;
using NetCraft.Client.Gui;
using NetCraft.Client.Gui.Render;
using NetCraft.Client.Gui.Render.State;
using NetCraft.Client.Gui.Render.Pip;
using NetCraft.Client.Gui.Navigation;
using NetCraft.Client.Gui.Layouts;
using NetCraft.Client.Gui.Font;
using NetCraft.Client.Gui.Font.Providers;
using NetCraft.Client.Gui.Font.Glyphs;
using NetCraft.Client.Model;
using NetCraft.Client.Model.Geom;
using NetCraft.Client.Resources.Metadata.Gui;
using NetCraft.Client.Blaze3d;

namespace NetCraft.Client.Gui.Render;

//GuiResourceManager GUI resource manager, stage 5c
//Manages the Projection uniform buffer + font atlas texture + image texture DescriptorSets
//descriptorResolver resolves a TextureSetup into a GpuDescriptorSet for GuiRenderer.Draw to bind
//The global DescriptorSet (GLOBALS+MATRICES) is bound by the caller at render pass start as set 0/1
public sealed class GuiResourceManager : IDisposable
{
    private readonly GpuDevice _device;
    private readonly int _surfaceWidth;
    private readonly int _surfaceHeight;
    private bool _disposed;

    //GLOBALS uniform buffer, bound to the render pass by the name "Globals"
    //gui.vert declares ScreenSize but never uses it; a dummy Mat4 is passed to avoid a layout mismatch
    private readonly GpuBuffer _globalsBuffer;

    //MATRICES_PROJECTION uniform buffer, bound by the name "Matrices"
    private readonly GpuBuffer _projectionBuffer;

    //Font atlas texture
    private readonly FontAtlas? _fontAtlas;
    private readonly GpuTexture? _fontImage;
    private readonly GpuSampler? _fontSampler;
    private readonly TextureSetup? _fontTexture;

    //Image textures textureId → TextureSetup
    private readonly Dictionary<int, TextureSetup> _textures = new();
    private readonly Dictionary<string, int> _pathCache = new();
    private int _nextTextureId = 1;
    //Texture views are created lazily because render passes bind GpuTextureView rather than GpuTexture
    private readonly Dictionary<GpuTexture, GpuTextureView> _textureViews = new();
    //_externalTextureIds set of externally managed textureIds; Dispose does not release their GpuTexture
    //The ItemAtlas's AtlasTexture is managed by ItemItemAtlas itself; here it is only registered to get a textureId
    //_internalSamplers samplers created internally by RegisterImage, released on Dispose while the GpuTexture is not
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

        //The globals and projection buffers are bound by name through the pipeline's BindGroupLayouts
        _globalsBuffer = _device.CreateBuffer(null, GpuBuffer.UsageUniform | GpuBuffer.UsageMapWrite, 64);
        _projectionBuffer = _device.CreateBuffer(null, GpuBuffer.UsageUniform | GpuBuffer.UsageMapWrite, 64);

        //Font atlas texture
        if (_fontAtlas is not null)
        {
            _fontImage = CreateFontImage(_fontAtlas);
            _fontSampler = _device.CreateSampler(AddressMode.ClampToEdge, AddressMode.ClampToEdge, FilterMode.Linear, FilterMode.Linear, 1, null);
            _fontTexture = TextureSetup.SingleTexture(_fontImage, _fontSampler);
        }

        UpdateProjection();
    }

    //UpdateProjection updates the orthographic projection matrix every frame, converting actual pixels to clip space
    //Consistent with VulkanGuiRenderer.ToClipX/ToClipY clipX = px*2/W - 1 clipY = py*2/H - 1
    //Matrix4x4 is row-major; Transpose to column-major before upload to match GLSL mat4
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

    //GlobalsBuffer global uniform buffer, bound by the name "Globals"
    public GpuBuffer GlobalsBuffer => _globalsBuffer;

    //ProjectionBuffer projection matrix uniform buffer, bound by the name "Matrices"
    public GpuBuffer ProjectionBuffer => _projectionBuffer;

    //FontTexture font atlas TextureSetup for GuiRenderContext to inject
    public TextureSetup? FontTexture => _fontTexture;

    //FontAtlas font atlas for GuiRenderContext to compute glyph UVs
    public FontAtlas? FontAtlas => _fontAtlas;

    //RegisterTexture loads a PNG by path, registers it as a texture and returns a textureId
    //A second call with the same path returns the cached id directly; a missing file or decode failure returns 0
    public int RegisterTexture(string path)
    {
        if (_pathCache.TryGetValue(path, out var cached)) return cached;
        if (!File.Exists(path))
        {
            _logger.Warning($"Texture file does not exist {path}");
            return 0;
        }
        byte[] bytes;
        try { bytes = File.ReadAllBytes(path); }
        catch (Exception ex) { _logger.Warning($"Texture read failed {path} {ex.Message}"); return 0; }
        ImageResult? result;
        try { result = ImageResult.FromMemory(bytes, ColorComponents.RedGreenBlueAlpha); }
        catch (Exception ex) { _logger.Warning($"Texture decode failed {path} {ex.Message}"); return 0; }
        if (result is null || result.Width <= 0 || result.Height <= 0)
        {
            _logger.Warning($"Texture decode returned empty {path}");
            return 0;
        }
        var img = _device.CreateTexture(null, GpuTexture.UsageTextureBinding, GpuFormat.Rgba8Unorm, result.Width, result.Height, 1, 1);
        img.Upload(result.Data);
        var sampler = _device.CreateSampler(AddressMode.ClampToEdge, AddressMode.ClampToEdge, FilterMode.Linear, FilterMode.Linear, 1, null);
        var texture = TextureSetup.SingleTexture(img, sampler);
        var id = _nextTextureId++;
        _textures[id] = texture;
        _pathCache[path] = id;
        return id;
    }

    //ResolveTexture returns a TextureSetup by textureId for GuiRenderContext.DrawImage
    public TextureSetup? ResolveTexture(int textureId)
    {
        return _textures.TryGetValue(textureId, out var t) ? t : null;
    }

    //ResolveTextureBinding resolves a TextureSetup into the view and sampler a render pass binds by name
    //NoTexture returns nulls; texture views are created lazily and cached
    public (GpuTextureView? View, GpuSampler? Sampler) ResolveTextureBinding(TextureSetup texture)
    {
        if (texture == TextureSetup.NoTexture) return (null, null);
        if (texture.Texture0 is null || texture.Sampler0 is null) return (null, null);
        if (!_textureViews.TryGetValue(texture.Texture0, out var view))
        {
            view = _device.CreateTextureView(texture.Texture0);
            _textureViews[texture.Texture0] = view;
        }
        return (view, texture.Sampler0);
    }

    //RegisterFontTexture registers a dynamic glyph atlas texture and returns a TextureSetup
    //Used by F7 when GlyphStitcher creates a FontTexture to register the atlas GpuTexture and get a TextureSetup
    //The sampler reuses LinearFilter+RepeatAddress=false, consistent with FontAtlas
    //The descriptorSet is lazily created and cached by ResolveDescriptorSet
    public TextureSetup RegisterFontTexture(GpuTexture image)
    {
        var sampler = _device.CreateSampler(AddressMode.ClampToEdge, AddressMode.ClampToEdge, FilterMode.Linear, FilterMode.Linear, 1, null);
        var texture = TextureSetup.SingleTexture(image, sampler);
        return texture;
    }

    //RegisterImage registers an external GpuTexture and returns a textureId for GuiRenderContext.DrawImage
    //Does not take ownership of the GpuTexture, which the caller Disposes; internally creates a nearest sampler for the pixel-style item atlas
    //Dispose releases the internal sampler but not the GpuTexture (whose lifetime is managed by ItemItemAtlas)
    public int RegisterImage(GpuTexture image)
    {
        var sampler = _device.CreateSampler(AddressMode.ClampToEdge, AddressMode.ClampToEdge, FilterMode.Nearest, FilterMode.Nearest, 1, null);
        _internalSamplers.Add(sampler);
        var texture = TextureSetup.SingleTexture(image, sampler);
        var id = _nextTextureId++;
        _textures[id] = texture;
        _externalTextureIds.Add(id);
        return id;
    }

    //CreateFontImage creates the font atlas GpuTexture and uploads pixel data
    private GpuTexture CreateFontImage(FontAtlas atlas)
    {
        var img = _device.CreateTexture(null, GpuTexture.UsageTextureBinding, GpuFormat.R8Unorm, atlas.AtlasWidth, atlas.AtlasHeight, 1, 1);
        img.Upload(atlas.AtlasPixels);
        return img;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _globalsBuffer.Dispose();
        _projectionBuffer.Dispose();
        foreach (var view in _textureViews.Values) view.Dispose();
        _textureViews.Clear();
        _fontImage?.Dispose();
        _fontSampler?.Dispose();
        foreach (var pair in _textures)
        {
            //Externally managed textures (ItemAtlas AtlasTexture) do not release the GpuTexture; the owner Disposes it
            //Samplers created internally by RegisterImage are released separately
            if (_externalTextureIds.Contains(pair.Key)) continue;
            pair.Value.Texture0?.Dispose();
            pair.Value.Sampler0?.Dispose();
        }
        foreach (var s in _internalSamplers) s.Dispose();
        _disposed = true;
    }
}
