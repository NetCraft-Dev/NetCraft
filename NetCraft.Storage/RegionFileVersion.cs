using System.IO.Compression;
using K4os.Compression.LZ4.Streams;
using NetCraft.Logging;

namespace NetCraft.Storage;

//MCA compression version, maps to vanilla RegionFileVersion
//GZIP, DEFLATE, NONE and LZ4 are built in; CUSTOM is stubbed to throw NotSupported for now
public sealed class RegionFileVersion
{
    private static readonly Dictionary<int, RegionFileVersion> _versionsById = new();
    private static readonly Dictionary<string, RegionFileVersion> _versionsByName = new();

    //WrapOutput uses leaveOpen false so closing the BinaryWriter propagates to ChunkDataBuffer and triggers the write-back
    public static readonly RegionFileVersion VersionGzip = Register(new RegionFileVersion(
        1, null,
        s => new GZipStream(s, CompressionMode.Decompress, leaveOpen: true),
        s => new GZipStream(s, CompressionLevel.Optimal, leaveOpen: false)));

    public static readonly RegionFileVersion VersionDeflate = Register(new RegionFileVersion(
        2, "deflate",
        s => new DeflateStream(s, CompressionMode.Decompress, leaveOpen: true),
        s => new DeflateStream(s, CompressionLevel.Optimal, leaveOpen: false)));

    public static readonly RegionFileVersion VersionNone = Register(new RegionFileVersion(
        3, "none",
        s => s,
        s => s));

    //LZ4 uses the standard frame format from K4os.Compression.LZ4.Streams, maps to vanilla LZ4BlockInputStream/OutputStream
    public static readonly RegionFileVersion VersionLz4 = Register(new RegionFileVersion(
        4, "lz4",
        s => LZ4Stream.Decode(s, new LZ4DecoderSettings(), leaveOpen: true, interactive: false),
        s => LZ4Stream.Encode(s, new LZ4EncoderSettings(), leaveOpen: false)));

    public static readonly RegionFileVersion VersionCustom = Register(new RegionFileVersion(
        127, null,
        _ => throw new NotSupportedException(),
        _ => throw new NotSupportedException()));

    public static readonly RegionFileVersion Default = VersionDeflate;

    private static RegionFileVersion _selected = Default;

    public int Id { get; }
    private readonly string? _optionName;
    private readonly Func<Stream, Stream> _wrapInput;
    private readonly Func<Stream, Stream> _wrapOutput;

    private RegionFileVersion(int id, string? optionName, Func<Stream, Stream> wrapInput, Func<Stream, Stream> wrapOutput)
    {
        Id = id;
        _optionName = optionName;
        _wrapInput = wrapInput;
        _wrapOutput = wrapOutput;
    }

    private static RegionFileVersion Register(RegionFileVersion version)
    {
        _versionsById[version.Id] = version;
        if (version._optionName != null)
            _versionsByName[version._optionName] = version;
        return version;
    }

    public static RegionFileVersion? FromId(int id) => _versionsById.TryGetValue(id, out var v) ? v : null;

    public static RegionFileVersion GetSelected() => _selected;

    //Switch the default version by the region-file-compression value in server.properties
    public static void Configure(string optionName)
    {
        if (_versionsByName.TryGetValue(optionName, out var v))
            _selected = v;
        else
            Log.Error($"Invalid region file compression {optionName}, available: {string.Join(", ", _versionsByName.Keys)}");
    }

    public static bool IsValidVersion(int version) => _versionsById.ContainsKey(version);

    public Stream WrapInput(Stream input) => _wrapInput(input);
    public Stream WrapOutput(Stream output) => _wrapOutput(output);
}
