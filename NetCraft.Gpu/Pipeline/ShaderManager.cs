using System.Reflection;
using System.Text;
using Veldrid;
using Veldrid.SPIRV;

namespace NetCraft.Gpu.Pipeline;

//ShaderManager loads embedded GLSL, compiles it to SPIR-V, injects defines and caches the compiled result
//maps to vanilla GlslCompiler; stage 8 finishes the stage 3 declarative pipeline shader compilation leftover
//Matches embedded resource names by the name+suffix suffix without relying on the RootNamespace prefix, avoiding differences in MSBuild resource name generation
public sealed class ShaderManager
{
    private readonly Assembly _assembly;
    //Cache key = shader name + stage + defines content
    private readonly Dictionary<string, byte[]> _cache = new();
    //Collects all embedded resource names once at startup to avoid per-frame enumeration
    private readonly HashSet<string> _resourceNames;

    public ShaderManager()
    {
        _assembly = typeof(ShaderManager).Assembly;
        _resourceNames = new HashSet<string>(_assembly.GetManifestResourceNames());
    }

    //LoadVertexShader loads and compiles the vertex shader, injects defines and returns SPIR-V bytecode
    public byte[] LoadVertexShader(string name, ShaderDefines? defines = null)
        => LoadOrCompile(name, ShaderStages.Vertex, defines);

    //LoadFragmentShader loads and compiles the fragment shader, injects defines and returns SPIR-V bytecode
    public byte[] LoadFragmentShader(string name, ShaderDefines? defines = null)
        => LoadOrCompile(name, ShaderStages.Fragment, defines);

    //LoadOrCompile checks the cache by name+stage+defines; on a miss it loads GLSL from the embedded resource and compiles it to SPIR-V
    private byte[] LoadOrCompile(string name, ShaderStages stage, ShaderDefines? defines)
    {
        var cacheKey = BuildCacheKey(name, stage, defines);
        if (_cache.TryGetValue(cacheKey, out var cached))
            return cached;

        var suffix = stage == ShaderStages.Vertex ? ".vert.glsl" : ".frag.glsl";
        var resourceName = FindResourceName(name.Replace('/', '.'), suffix);
        using var stream = _assembly.GetManifestResourceStream(resourceName)
            ?? throw new FileNotFoundException($"Embedded shader resource not found {name}{suffix}");
        using var reader = new StreamReader(stream);
        var source = reader.ReadToEnd();

        var macros = BuildMacros(defines);
        var options = new GlslCompileOptions(debug: false, macros);
        var fileName = stage == ShaderStages.Vertex ? $"{name}.vert" : $"{name}.frag";
        var result = SpirvCompilation.CompileGlslToSpirv(source, fileName, stage, options);
        var spirv = result.SpirvBytes;
        _cache[cacheKey] = spirv;
        return spirv;
    }

    //FindResourceName matches an embedded resource name by normalized name + suffix
    //Does not rely on the RootNamespace prefix, tolerating MSBuild turning NetCraft.Gpu into NetCraftGpu in resource names
    private string FindResourceName(string normalized, string suffix)
    {
        var target = normalized + suffix;
        foreach (var rn in _resourceNames)
            if (rn.EndsWith(target, StringComparison.Ordinal))
                return rn;
        throw new FileNotFoundException($"Embedded shader resource not found {target}");
    }

    //BuildMacros converts ShaderDefines into a Veldrid MacroDefinition array
    //Flags valueless macros Values value-carrying macros
    private static MacroDefinition[] BuildMacros(ShaderDefines? defines)
    {
        if (defines == null) return Array.Empty<MacroDefinition>();
        var list = new List<MacroDefinition>();
        foreach (var kv in defines.Values)
            list.Add(new MacroDefinition(kv.Key, kv.Value));
        foreach (var flag in defines.Flags)
            list.Add(new MacroDefinition(flag));
        return list.ToArray();
    }

    //BuildCacheKey generates a deterministic cache key from name + stage + defines content
    private static string BuildCacheKey(string name, ShaderStages stage, ShaderDefines? defines)
    {
        var sb = new StringBuilder(name);
        sb.Append(stage == ShaderStages.Vertex ? ":v:" : ":f:");
        if (defines != null)
        {
            foreach (var kv in defines.Values.OrderBy(x => x.Key))
                sb.Append(kv.Key).Append('=').Append(kv.Value).Append(';');
            foreach (var flag in defines.Flags.OrderBy(x => x))
                sb.Append(flag).Append(';');
        }
        return sb.ToString();
    }
}
