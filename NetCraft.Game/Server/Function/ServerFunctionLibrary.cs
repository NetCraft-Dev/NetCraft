using System.Text.Json;
using NetCraft.Commands;
using NetCraft.Commands.Functions;
using NetCraft.Logging;
using NetCraft.Registry;
using NetCraft.Resources;

namespace NetCraft.Game.Server;

//ServerFunctionLibrary function library, maps to vanilla net.minecraft.server.ServerFunctionLibrary
//Compiles functions from datapack function/<namespace>/**.mcfunction and parses function tags from tags/function/*.json
//The compilation context is built by function-permission-level, consistent with the permission set passed by vanilla reloadableServerResources
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

    //GetFunction gets a function by identifier
    public CommandFunction<CommandSourceStack>? GetFunction(Identifier id)
    {
        lock (_gate) return _functions.GetValueOrDefault(id);
    }

    //Functions a full function snapshot
    public IReadOnlyDictionary<Identifier, CommandFunction<CommandSourceStack>> Functions
    {
        get { lock (_gate) return new Dictionary<Identifier, CommandFunction<CommandSourceStack>>(_functions); }
    }

    //GetTag function tag content
    public IReadOnlyList<CommandFunction<CommandSourceStack>> GetTag(Identifier tag)
        => GetTags().GetValueOrDefault(tag) ?? [];

    //Reload implements the resource listener, reading functions and tags and replacing the in-memory tables
    public void Reload(ResourceManager resourceManager, ReloadContext context)
    {
        //The compilation context uses full permission; an anonymous empty name matches vanilla createCompilationContext
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

    //Tags a snapshot of tags
    public IReadOnlyDictionary<Identifier, IReadOnlyList<CommandFunction<CommandSourceStack>>> GetTags()
    {
        lock (_gate) return new Dictionary<Identifier, IReadOnlyList<CommandFunction<CommandSourceStack>>>(_tags);
    }

    //AvailableTags the set of tag names
    public IEnumerable<Identifier> AvailableTags => GetTags().Keys;

    //ListFunctionResources enumerates the .mcfunction files in the function directory under each namespace
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

    //FunctionIdOf converts a resource path to a function identifier, stripping the function/ prefix and extension, maps to vanilla FileToIdConverter
    private static Identifier? FunctionIdOf(Identifier location)
    {
        var path = location.Path;
        if (!path.StartsWith(ElementsDir + "/", StringComparison.Ordinal)) return null;
        var functionPath = path[(ElementsDir.Length + 1)..^FunctionExtension.Length];
        return Identifier.TryParse(location.Namespace + Identifier.NamespaceSeparator + functionPath);
    }

    //ReadLines reads the function's text lines
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

    //LoadTags parses function tag values, supporting # nested tag references, maps to vanilla TagLoader's graph resolution
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
                        //Tag reference #ns:path is recorded as the raw string and expanded later
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
        //Parsed into a list; reference depth is bounded to prevent cycles
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

    //TagIdOf converts a resource path to a tag identifier
    private static Identifier? TagIdOf(Identifier location)
    {
        var path = location.Path;
        if (!path.StartsWith(TagsDir + "/", StringComparison.Ordinal)) return null;
        var tagPath = path[(TagsDir.Length + 1)..^".json".Length];
        return Identifier.TryParse(location.Namespace + Identifier.NamespaceSeparator + tagPath);
    }
}
