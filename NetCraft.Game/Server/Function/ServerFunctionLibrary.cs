using System.Text.Json;
using NetCraft.Commands;
using NetCraft.Commands.Functions;
using NetCraft.Logging;
using NetCraft.Registry;
using NetCraft.Resources;

namespace NetCraft.Game.Server;

//ServerFunctionLibrary 函数库对应原版 net.minecraft.server.ServerFunctionLibrary
//从数据包 function/<namespace>/**.mcfunction 编译函数 tags/function/*.json 解析函数标签
//编译上下文按 function-permission-level 造 与原版 reloadableServerResources 传权限集合一致
public sealed class ServerFunctionLibrary : PreparableReloadListener
{
    private static readonly string ElementsDir = "function";
    private static readonly string TagsDir = "tags/function";
    private static readonly string FunctionExtension = ".mcfunction";

    private readonly object _gate = new();
    private readonly PermissionSet _functionCompilationPermissions;
    private readonly CommandDispatcher<CommandSourceStack> _dispatcher;
    private Dictionary<Identifier, CommandFunction<CommandSourceStack>> _functions = [];

    public ServerFunctionLibrary(PermissionSet functionCompilationPermissions, CommandDispatcher<CommandSourceStack> dispatcher)
    {
        _functionCompilationPermissions = functionCompilationPermissions;
        _dispatcher = dispatcher;
    }

    //GetFunction 按标识取函数
    public CommandFunction<CommandSourceStack>? GetFunction(Identifier id)
    {
        lock (_gate) return _functions.GetValueOrDefault(id);
    }

    //Functions 全量函数快照
    public IReadOnlyDictionary<Identifier, CommandFunction<CommandSourceStack>> Functions
    {
        get { lock (_gate) return new Dictionary<Identifier, CommandFunction<CommandSourceStack>>(_functions); }
    }

    //GetTag 函数标签内容
    public IReadOnlyList<CommandFunction<CommandSourceStack>> GetTag(Identifier tag)
        => GetTags().GetValueOrDefault(tag) ?? [];

    //Reload 实现资源监听 读函数与标签并替换内存表
    public void Reload(ResourceManager resourceManager, ReloadContext context)
    {
        //编译上下文权限取满等级 匿名空名与原版 createCompilationContext 一致
        var compilationContext = new CommandSourceStack(string.Empty, 0, TextWriter.Null);
        if (_functionCompilationPermissions is LevelBasedPermissionSet levelBased)
        {
            compilationContext = new CommandSourceStack(string.Empty, (int)levelBased.Level, TextWriter.Null);
        }
        var functions = new Dictionary<Identifier, CommandFunction<CommandSourceStack>>();
        var errors = 0;
        foreach (var resource in ListFunctionResources(resourceManager))
        {
            if (FunctionIdOf(resource.Location) is not { } id) continue;
            try
            {
                var lines = ReadLines(resource);
                functions[id] = CommandFunction<CommandSourceStack>.FromLines(id, _dispatcher, compilationContext, lines);
            }
            catch (Exception e)
            {
                errors++;
                Log.Error($"Failed to load function {id} {e.Message}");
            }
        }
        var tags = LoadTags(resourceManager, functions);
        lock (_gate)
        {
            _functions = functions;
            _tags = tags;
        }
        Log.Info($"Function reload finished: {functions.Count} functions, {tags.Count} tags, {errors} errors");
    }

    private Dictionary<Identifier, IReadOnlyList<CommandFunction<CommandSourceStack>>> _tags = [];

    //Tags 快照
    public IReadOnlyDictionary<Identifier, IReadOnlyList<CommandFunction<CommandSourceStack>>> GetTags()
    {
        lock (_gate) return new Dictionary<Identifier, IReadOnlyList<CommandFunction<CommandSourceStack>>>(_tags);
    }

    //AvailableTags 标签名集合
    public IEnumerable<Identifier> AvailableTags => GetTags().Keys;

    //ListFunctionResources 枚举各命名空间下 function 目录的 .mcfunction
    private IEnumerable<Resource> ListFunctionResources(ResourceManager resourceManager)
    {
        foreach (var ns in resourceManager.GetNamespaces(PackType.ServerData))
        {
            foreach (var resource in resourceManager.ListResources(PackType.ServerData, ns, ElementsDir))
            {
                if (resource.Location.Path.EndsWith(FunctionExtension, StringComparison.Ordinal))
                {
                    yield return resource;
                }
            }
        }
    }

    //FunctionIdOf 资源路径转函数标识 去掉 function/ 前缀与扩展名对应原版 FileToIdConverter
    private static Identifier? FunctionIdOf(Identifier location)
    {
        var path = location.Path;
        if (!path.StartsWith(ElementsDir + "/", StringComparison.Ordinal)) return null;
        var functionPath = path[(ElementsDir.Length + 1)..^FunctionExtension.Length];
        return Identifier.TryParse(location.Namespace + Identifier.NamespaceSeparator + functionPath);
    }

    //ReadLines 读函数文本行
    private static List<string> ReadLines(Resource resource)
    {
        using var stream = resource.Open();
        using var reader = new StreamReader(stream);
        var lines = new List<string>();
        while (reader.ReadLine() is { } line)
        {
            lines.Add(line);
        }
        return lines;
    }

    //LoadTags 解析函数标签 values 支持 #嵌套标签引用 对应原版 TagLoader 的图解析
    private Dictionary<Identifier, IReadOnlyList<CommandFunction<CommandSourceStack>>> LoadTags(
        ResourceManager resourceManager, Dictionary<Identifier, CommandFunction<CommandSourceStack>> functions)
    {
        var rawTags = new Dictionary<Identifier, List<Identifier>>();
        foreach (var ns in resourceManager.GetNamespaces(PackType.ServerData))
        {
            foreach (var resource in resourceManager.ListResources(PackType.ServerData, ns, TagsDir))
            {
                if (!resource.Location.Path.EndsWith(".json", StringComparison.Ordinal)) continue;
                if (TagIdOf(resource.Location) is not { } id) continue;
                try
                {
                    using var stream = resource.Open();
                    using var document = JsonDocument.Parse(stream);
                    if (document.RootElement.ValueKind != JsonValueKind.Object
                        || !document.RootElement.TryGetProperty("values", out var valuesNode)
                        || valuesNode.ValueKind != JsonValueKind.Array) continue;
                    var entries = new List<Identifier>();
                    foreach (var value in valuesNode.EnumerateArray())
                    {
                        if (value.ValueKind != JsonValueKind.String) continue;
                        var text = value.GetString();
                        if (string.IsNullOrEmpty(text)) continue;
                        //标签引用 #ns:path 记成原始串后续展开
                        if (text.StartsWith("#", StringComparison.Ordinal))
                        {
                            if (Identifier.TryParse(text[1..]) is { } referenceId) entries.Add(referenceId);
                        }
                        else if (Identifier.TryParse(text) is { } functionId)
                        {
                            entries.Add(functionId);
                        }
                    }
                    rawTags[id] = entries;
                }
                catch (Exception e)
                {
                    Log.Warning($"Failed to load function tag {id} {e.Message}");
                }
            }
        }
        //解析进列表 引用深度受限防环
        var resolved = new Dictionary<Identifier, IReadOnlyList<CommandFunction<CommandSourceStack>>>();
        foreach (var (tagId, entries) in rawTags)
        {
            var output = new List<CommandFunction<CommandSourceStack>>();
            ResolveTag(tagId, rawTags, functions, output, resolved, 0);
            if (output.Count > 0) resolved[tagId] = output;
        }
        return resolved;
    }

    private void ResolveTag(Identifier tagId,
        Dictionary<Identifier, List<Identifier>> rawTags,
        Dictionary<Identifier, CommandFunction<CommandSourceStack>> functions,
        List<CommandFunction<CommandSourceStack>> output,
        Dictionary<Identifier, IReadOnlyList<CommandFunction<CommandSourceStack>>> resolved,
        int depth)
    {
        if (depth > 16) return;
        if (resolved.TryGetValue(tagId, out var cached))
        {
            output.AddRange(cached);
            return;
        }
        if (!rawTags.TryGetValue(tagId, out var entries)) return;
        foreach (var entry in entries)
        {
            if (rawTags.ContainsKey(entry))
            {
                ResolveTag(entry, rawTags, functions, output, resolved, depth + 1);
            }
            else if (functions.TryGetValue(entry, out var function))
            {
                output.Add(function);
            }
        }
        if (depth == 0) resolved[tagId] = output;
    }

    //TagIdOf 资源路径转标签标识
    private static Identifier? TagIdOf(Identifier location)
    {
        var path = location.Path;
        if (!path.StartsWith(TagsDir + "/", StringComparison.Ordinal)) return null;
        var tagPath = path[(TagsDir.Length + 1)..^".json".Length];
        return Identifier.TryParse(location.Namespace + Identifier.NamespaceSeparator + tagPath);
    }
}
