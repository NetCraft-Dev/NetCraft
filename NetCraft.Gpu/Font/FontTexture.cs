namespace NetCraft.Gpu.Font;

//FontTexture 动态字形图集对标原版 FontTexture
//256×256 像素图集 Node 二叉树分配器按需分配空间上传字形像素
//装不下时返回 null 由 GlyphStitcher 新建下一张图集
//colored=true 用 RGBA8 图集 false 用 R8 图集与原版 GpuFormat.RGBA8_UNORM/R8_UNORM 对应
//GpuImage 由 GlyphStitcher 创建后注入 Dispose 时释放 TextureSetup+GlyphRenderTypes 同样注入
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

    //Add 把字形缝到图集返回 BakedGlyph null 表示装不下需新建图集
    //对标原版 FontTexture.add 检查 isColored 匹配后调 root.insert 找空位上传像素
    public BakedGlyph? Add(IGlyphInfo info, IGlyphBitmap glyph)
    {
        if (glyph.IsColored != _colored) return null;
        var node = _root.Insert(glyph);
        if (node == null) return null;
        var pixels = glyph.GetPixels();
        _texture.UploadRegion(node.X, node.Y, glyph.PixelWidth, glyph.PixelHeight, pixels);
        //UV 内缩 0.01 像素避免采样越界对标原版 (x+0.01)/256.0f
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

    //Node 二叉树分配器对标原版 FontTexture$Node
    //occupied 标记叶子是否已用 left/right 子节点按宽高差决定横向/纵向切分
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

        //Insert 递归找空位装不下返回 null
        //对标原版 Node.insert 已分裂走子树未分裂检查尺寸能否容纳
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
            //宽差大于高差横向切分否则纵向切分对标原版 deltaWidth>deltaHeight 判断
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
