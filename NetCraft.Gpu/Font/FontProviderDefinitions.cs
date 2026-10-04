namespace NetCraft.Gpu.Font;

//Shift TTF 字形渲染偏移对标原版 TrueTypeGlyphProviderDefinition.Shift
//x 横向偏移 y 纵向偏移（向上为正）原版用 record 这里用 readonly struct
public readonly struct Shift
{
    public readonly float X;
    public readonly float Y;
    public static readonly Shift None = new(0f, 0f);
    public Shift(float x, float y) { X = x; Y = y; }
}

//TtfDefinition 对标原版 TrueTypeGlyphProviderDefinition
//font/*.json 中 ttf 类型 provider 的定义含 file/size/oversample/shift/skip 字段
//默认值对齐原版 size=11.0f oversample=1.0f shift=NONE skip=""
public sealed class TtfDefinition : IGlyphProviderDefinition
{
    public string File { get; }
    public float Size { get; }
    public float Oversample { get; }
    public Shift Shift { get; }
    public string Skip { get; }

    public TtfDefinition(string file, float size, float oversample, Shift shift, string skip)
    {
        File = file;
        Size = size;
        Oversample = oversample;
        Shift = shift;
        Skip = skip;
    }

    public GlyphProviderType Type => GlyphProviderType.Ttf;
    public bool IsReference => false;
    public IGlyphProviderDefinition.ILoader? AsLoader => new TtfLoader(this);

    //TtfLoader 实现 ILoader.Load 从 IFontResourceAccessor 打开 TTF 流读字节构造 TtfGlyphProvider
    private sealed class TtfLoader : IGlyphProviderDefinition.ILoader
    {
        private readonly TtfDefinition _def;
        public TtfLoader(TtfDefinition def) => _def = def;

        public IGlyphProvider? Load(IFontResourceAccessor resources)
        {
            var stream = resources.OpenResource(_def.File);
            if (stream == null) return null;
            using (stream)
            {
                using var ms = new MemoryStream();
                stream.CopyTo(ms);
                var bytes = ms.ToArray();
                return new TtfGlyphProvider(bytes, _def.Size, _def.Oversample, _def.Shift.X, _def.Shift.Y, _def.Skip);
            }
        }
    }
}

//SpaceDefinition 对标原版 SpaceProvider.Definition
//font/*.json 中 space 类型 provider 的定义含 advances 字段
//advances 是 codepoint→advance 映射 JSON 中键是字符值是 float
public sealed class SpaceDefinition : IGlyphProviderDefinition
{
    public IReadOnlyDictionary<int, float> Advances { get; }

    public SpaceDefinition(IReadOnlyDictionary<int, float> advances) => Advances = advances;

    public GlyphProviderType Type => GlyphProviderType.Space;
    public bool IsReference => false;
    public IGlyphProviderDefinition.ILoader? AsLoader => new SpaceLoader(this);

    private sealed class SpaceLoader : IGlyphProviderDefinition.ILoader
    {
        private readonly SpaceDefinition _def;
        public SpaceLoader(SpaceDefinition def) => _def = def;

        public IGlyphProvider? Load(IFontResourceAccessor resources) => new SpaceGlyphProvider(_def.Advances);
    }
}

//ReferenceDefinition 对标原版 ProviderReferenceDefinition
//font/*.json 中 reference 类型 provider 的定义含 id 字段引用其他 font json
//递归加载由 FontProviderDefinitionLoader 处理 Definition 本身不 Load
public sealed class ReferenceDefinition : IGlyphProviderDefinition
{
    public string Id { get; }

    public ReferenceDefinition(string id) => Id = id;

    public GlyphProviderType Type => GlyphProviderType.Reference;
    public bool IsReference => true;
    public IGlyphProviderDefinition.Reference? AsReference => new IGlyphProviderDefinition.Reference(Id);
}

//BitmapDefinition 对标原版 BitmapProvider.Definition
//font/*.json 中 bitmap 类型 provider 含 file/height/ascent/chars 字段
//chars 是 codepoint 二维网格字符串数组每行 16 字符每个字符对应纹理一格
public sealed class BitmapDefinition : IGlyphProviderDefinition
{
    public string File { get; }
    public int Height { get; }
    public int Ascent { get; }
    public string[] Chars { get; }

    public BitmapDefinition(string file, int height, int ascent, string[] chars)
    {
        File = file;
        Height = height;
        Ascent = ascent;
        Chars = chars;
    }

    public GlyphProviderType Type => GlyphProviderType.Bitmap;
    public bool IsReference => false;
    public IGlyphProviderDefinition.ILoader? AsLoader => new BitmapLoader(this);

    //BitmapLoader 从 assets 读 PNG 字节构造 BitmapGlyphProvider
    //chars 字符串数组用 EnumerateRunes 转 codepoint 二维数组对标原版 CODEPOINT_GRID_CODEC
    private sealed class BitmapLoader : IGlyphProviderDefinition.ILoader
    {
        private readonly BitmapDefinition _def;
        public BitmapLoader(BitmapDefinition def) => _def = def;

        public IGlyphProvider? Load(IFontResourceAccessor resources)
        {
            //对标原版 BitmapProvider.load 用 file.withPrefix("textures/")
            //把 minecraft:font/xxx.png 转为 minecraft:textures/font/xxx.png 资源路径
            var textureId = WithTexturePrefix(_def.File);
            var stream = resources.OpenResource(textureId);
            if (stream == null) return null;
            using (stream)
            {
                using var ms = new MemoryStream();
                stream.CopyTo(ms);
                var pngBytes = ms.ToArray();
                var grid = new int[_def.Chars.Length][];
                for (int i = 0; i < _def.Chars.Length; i++)
                {
                    var runes = _def.Chars[i].EnumerateRunes().ToArray();
                    grid[i] = new int[runes.Length];
                    for (int j = 0; j < runes.Length; j++)
                        grid[i][j] = runes[j].Value;
                }
                return new BitmapGlyphProvider(pngBytes, grid, _def.Height, _def.Ascent);
            }
        }

        //WithTexturePrefix 把 minecraft:font/xxx.png 转为 minecraft:textures/font/xxx.png
        //对标原版 Identifier.withPrefix("textures/")
        private static string WithTexturePrefix(string file)
        {
            int colon = file.IndexOf(':');
            string ns, path;
            if (colon >= 0)
            {
                ns = file.Substring(0, colon);
                path = file.Substring(colon + 1);
            }
            else
            {
                ns = "minecraft";
                path = file;
            }
            return $"{ns}:textures/{path}";
        }
    }
}

//UnihexDefinition 对标原版 UnihexProvider.Definition
//font/*.json 中 unihex 类型 provider 含 hex_file 字段引用 .hex 文件（zip 打包）
public sealed class UnihexDefinition : IGlyphProviderDefinition
{
    public string HexFile { get; }

    public UnihexDefinition(string hexFile) => HexFile = hexFile;

    public GlyphProviderType Type => GlyphProviderType.Unihex;
    public bool IsReference => false;
    public IGlyphProviderDefinition.ILoader? AsLoader => new UnihexLoader(this);

    //UnihexLoader 从 assets 读 hex zip 流交给 UnihexGlyphProvider.LoadFromStream
    private sealed class UnihexLoader : IGlyphProviderDefinition.ILoader
    {
        private readonly UnihexDefinition _def;
        public UnihexLoader(UnihexDefinition def) => _def = def;

        public IGlyphProvider? Load(IFontResourceAccessor resources)
        {
            var stream = resources.OpenResource(_def.HexFile);
            if (stream == null) return null;
            using (stream)
            {
                using var ms = new MemoryStream();
                stream.CopyTo(ms);
                return UnihexGlyphProvider.LoadFromStream(new MemoryStream(ms.ToArray()));
            }
        }
    }
}
