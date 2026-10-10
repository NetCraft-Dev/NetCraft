using NetCraft.Client.Blaze3d.Systems;
using NetCraft.Client.Blaze3d.Buffers;
using NetCraft.Client.Blaze3d.Textures;
using NetCraft.Client.Blaze3d.Shaders;
using NetCraft.Client.Blaze3d.Pipeline;
using NetCraft.Client.Blaze3d.Vertex;
using NetCraft.Client.Blaze3d.Vulkan;
using NetCraft.Client.Blaze3d.Platform;
using NetCraft.Client.Blaze3d.Font;
using NetCraft.Client.Blaze3d.Resource;
using NetCraft.Client.Blaze3d.Audio;
using NetCraft.Client.Blaze3d.Framegraph;
using NetCraft.Client.Blaze3d.Preprocessor;
using NetCraft.Client.Blaze3d.Util;
using NetCraft.Client.Render;
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
namespace NetCraft.Client.Gui.Font;

//FontTexture dynamic glyph atlas, maps to vanilla FontTexture
//256×256 pixel atlas; a Node binary-tree allocator assigns space on demand to upload glyph pixels
//Returns null when it does not fit, and GlyphStitcher creates the next atlas
//colored=true uses an RGBA8 atlas, false uses an R8 atlas, corresponding to vanilla GpuFormat.RGBA8_UNORM/R8_UNORM
//The GpuImage is created and injected by GlyphStitcher and released on Dispose; TextureSetup+GlyphRenderTypes are injected the same way
internal sealed class FontTexture : IDisposable
{
    public const int Size = 256;

    private readonly GpuImage _texture;
    private readonly Node _root;
    private readonly bool _colored;
    private readonly TextureSetup _textureSetup;
    private readonly GlyphRenderTypes _renderTypes;

    public GpuImage Texture => _texture;
    public bool Colored => _colored;
    public TextureSetup TextureSetup => _textureSetup;
    public GlyphRenderTypes RenderTypes => _renderTypes;

    public FontTexture(GpuImage texture, bool colored, TextureSetup textureSetup, GlyphRenderTypes renderTypes)
    {
        _texture = texture;
        _colored = colored;
        _textureSetup = textureSetup;
        _renderTypes = renderTypes;
        _root = new Node(0, 0, Size, Size);
    }

    //Add stitches the glyph into the atlas and returns a BakedGlyph; null means it does not fit and a new atlas is needed
    //maps to vanilla FontTexture.add: checks isColored, then calls root.insert to find a slot and upload pixels
    public BakedGlyph? Add(IGlyphInfo info, IGlyphBitmap glyph)
    {
        if (glyph.IsColored != _colored) return null;
        var node = _root.Insert(glyph);
        if (node == null) return null;
        var pixels = glyph.GetPixels();
        _texture.UploadRegion(node.X, node.Y, glyph.PixelWidth, glyph.PixelHeight, pixels);
        //UVs are inset by 0.01 pixels to avoid sampling out of bounds, maps to vanilla (x+0.01)/256.0f
        float u0 = (node.X + 0.01f) / Size;
        float u1 = ((node.X - 0.01f) + glyph.PixelWidth) / Size;
        float v0 = (node.Y + 0.01f) / Size;
        float v1 = ((node.Y - 0.01f) + glyph.PixelHeight) / Size;
        return new SheetBakedGlyph(info, u0, v0, u1, v1,
            glyph.Left, glyph.Right, glyph.Top, glyph.Bottom,
            _textureSetup, _renderTypes);
    }

    public void Dispose()
    {
        _texture.Dispose();
    }

    //Node binary-tree allocator, maps to vanilla FontTexture$Node
    //occupied marks whether a leaf is used; left/right children split horizontally or vertically based on the width/height delta
    private sealed class Node
    {
        internal readonly int X;
        internal readonly int Y;
        private readonly int _width;
        private readonly int _height;
        private Node? _left;
        private Node? _right;
        private bool _occupied;

        public Node(int x, int y, int width, int height)
        {
            X = x;
            Y = y;
            _width = width;
            _height = height;
        }

        //Insert recursively finds a slot and returns null when it does not fit
        //maps to vanilla Node.insert: recurse into children if split, otherwise check whether the size fits
        public Node? Insert(IGlyphBitmap glyph)
        {
            if (_left != null && _right != null)
            {
                var newNode = _left.Insert(glyph);
                return newNode ?? _right.Insert(glyph);
            }
            if (_occupied) return null;
            int glyphWidth = glyph.PixelWidth;
            int glyphHeight = glyph.PixelHeight;
            if (glyphWidth > _width || glyphHeight > _height) return null;
            if (glyphWidth == _width && glyphHeight == _height)
            {
                _occupied = true;
                return this;
            }
            int deltaWidth = _width - glyphWidth;
            int deltaHeight = _height - glyphHeight;
            //Split horizontally when the width delta exceeds the height delta, otherwise vertically, maps to the vanilla deltaWidth>deltaHeight check
            if (deltaWidth > deltaHeight)
            {
                _left = new Node(X, Y, glyphWidth, _height);
                _right = new Node(X + glyphWidth + 1, Y, _width - glyphWidth - 1, _height);
            }
            else
            {
                _left = new Node(X, Y, _width, glyphHeight);
                _right = new Node(X, Y + glyphHeight + 1, _width, _height - glyphHeight - 1);
            }
            return _left.Insert(glyph);
        }
    }
}
