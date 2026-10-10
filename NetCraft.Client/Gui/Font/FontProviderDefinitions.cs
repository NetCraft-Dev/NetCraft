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

//Shift TTF glyph render offset, maps to vanilla TrueTypeGlyphProviderDefinition.Shift
//x is horizontal offset, y is vertical offset (positive upwards); vanilla uses a record, here a readonly struct
public readonly struct Shift
{
    public readonly float X;
    public readonly float Y;
    public static readonly Shift None = new(0f, 0f);
    public Shift(float x, float y) { X = x; Y = y; }
}

//TtfDefinition maps to vanilla TrueTypeGlyphProviderDefinition
//Definition of a ttf provider in font/*.json with file/size/oversample/shift/skip fields
//Defaults match vanilla: size=11.0f oversample=1.0f shift=NONE skip=""
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

    //TtfLoader implements ILoader.Load, opening the TTF stream via IFontResourceAccessor and reading bytes to build a TtfGlyphProvider
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

//SpaceDefinition maps to vanilla SpaceProvider.Definition
//Definition of a space provider in font/*.json with the advances field
//advances is a codepoint→advance map; in JSON the keys are characters and the values are floats
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

//ReferenceDefinition maps to vanilla ProviderReferenceDefinition
//Definition of a reference provider in font/*.json with an id field referencing another font json
//Recursive loading is handled by FontProviderDefinitionLoader; the Definition itself does not Load
public sealed class ReferenceDefinition : IGlyphProviderDefinition
{
    public string Id { get; }

    public ReferenceDefinition(string id) => Id = id;

    public GlyphProviderType Type => GlyphProviderType.Reference;
    public bool IsReference => true;
    public IGlyphProviderDefinition.Reference? AsReference => new IGlyphProviderDefinition.Reference(Id);
}

//BitmapDefinition maps to vanilla BitmapProvider.Definition
//A bitmap provider in font/*.json has file/height/ascent/chars fields
//chars is a 2D grid of codepoint strings with 16 characters per row, each mapping to one texture cell
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

    //BitmapLoader reads PNG bytes from assets to build a BitmapGlyphProvider
    //The chars string array is converted to a 2D codepoint array with EnumerateRunes, maps to vanilla CODEPOINT_GRID_CODEC
    private sealed class BitmapLoader : IGlyphProviderDefinition.ILoader
    {
        private readonly BitmapDefinition _def;
        public BitmapLoader(BitmapDefinition def) => _def = def;

        public IGlyphProvider? Load(IFontResourceAccessor resources)
        {
            //maps to vanilla BitmapProvider.load using file.withPrefix("textures/")
            //Converts minecraft:font/xxx.png to the resource path minecraft:textures/font/xxx.png
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

        //WithTexturePrefix converts minecraft:font/xxx.png to minecraft:textures/font/xxx.png
        //maps to vanilla Identifier.withPrefix("textures/")
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

//UnihexDefinition maps to vanilla UnihexProvider.Definition
//A unihex provider in font/*.json has a hex_file field referencing a .hex file (zip-packed)
public sealed class UnihexDefinition : IGlyphProviderDefinition
{
    public string HexFile { get; }

    public UnihexDefinition(string hexFile) => HexFile = hexFile;

    public GlyphProviderType Type => GlyphProviderType.Unihex;
    public bool IsReference => false;
    public IGlyphProviderDefinition.ILoader? AsLoader => new UnihexLoader(this);

    //UnihexLoader reads the hex zip stream from assets and hands it to UnihexGlyphProvider.LoadFromStream
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
