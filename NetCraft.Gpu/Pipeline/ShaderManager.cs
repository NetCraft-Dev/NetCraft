using System.Reflection;
using System.Text;
using Veldrid;
using Veldrid.SPIRV;

namespace NetCraft.Gpu.Pipeline;

//ShaderManager 嵌入资源 GLSL 加载并编译为 SPIR-V 注入 defines 缓存编译结果
//对标原版 GlslCompiler 阶段 8 补完阶段 3 声明式 pipeline shader 编译遗留
//按 name+suffix 后缀匹配嵌入资源名不依赖 RootNamespace 前缀避免 MSBuild 资源名生成规则差异
public sealed class ShaderManager
{
    private readonly Assembly _assembly;
    //缓存 key = shader name + stage + defines 内容
    private readonly Dictionary<string, byte[]> _cache = new();
    //启动时一次性收集所有嵌入资源名避免每帧遍历
    private readonly HashSet<string> _resourceNames;

    public ShaderManager()
    {
        _assembly = typeof(ShaderManager).Assembly;
        _resourceNames = new HashSet<string>(_assembly.GetManifestResourceNames());
    }

    //LoadVertexShader 加载并编译 vertex shader 注入 defines 返回 SPIR-V 字节码
    public byte[] LoadVertexShader(string name, ShaderDefines? defines = null)
        => LoadOrCompile(name, ShaderStages.Vertex, defines);

    //LoadFragmentShader 加载并编译 fragment shader 注入 defines 返回 SPIR-V 字节码
    public byte[] LoadFragmentShader(string name, ShaderDefines? defines = null)
        => LoadOrCompile(name, ShaderStages.Fragment, defines);

    //LoadOrCompile 按 name+stage+defines 查缓存未命中则从嵌入资源加载 GLSL 编译为 SPIR-V
    private byte[] LoadOrCompile(string name, ShaderStages stage, ShaderDefines? defines)
    {
        var cacheKey = BuildCacheKey(name, stage, defines);
        if (_cache.TryGetValue(cacheKey, out var cached))
            return cached;

        var suffix = stage == ShaderStages.Vertex ? ".vert.glsl" : ".frag.glsl";
        var resourceName = FindResourceName(name.Replace('/', '.'), suffix);
        using var stream = _assembly.GetManifestResourceStream(resourceName)
            ?? throw new FileNotFoundException($"嵌入 shader 资源不存在 {name}{suffix}");
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

    //FindResourceName 按 normalized name + suffix 后缀匹配嵌入资源名
    //不依赖 RootNamespace 前缀容忍 MSBuild 把 NetCraft.Gpu 变成 NetCraftGpu 的资源名生成差异
    private string FindResourceName(string normalized, string suffix)
    {
        var target = normalized + suffix;
        foreach (var rn in _resourceNames)
            if (rn.EndsWith(target, StringComparison.Ordinal))
                return rn;
        throw new FileNotFoundException($"嵌入 shader 资源不存在 {target}");
    }

    //BuildMacros 把 ShaderDefines 转 Veldrid MacroDefinition 数组
    //Flags 无值宏 Values 带值宏
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

    //BuildCacheKey 由 name + stage + defines 内容生成确定性缓存键
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
