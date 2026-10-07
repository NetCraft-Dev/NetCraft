using System.Text.Json;
using System.Text.Json.Serialization;

namespace NetCraft.ModLoader;

//ModEnvironment: the environment side a mod applies to
public enum ModEnvironment
{
    //Both: loaded on both client and server
    Both,
    //Client: loaded on the client only
    Client,
    //Server: loaded on the server only
    Server,
}

//ModEnvironmentExtensions: shared logic for environment side matching
internal static class ModEnvironmentExtensions
{
    //Matches: whether the declared environment side includes the current side
    public static bool Matches(this ModEnvironment declared, ModEnvironment current)
        => declared == ModEnvironment.Both || declared == current;

    //ParseEnvironment: parses the environment field, falling back to Both when unrecognized
    public static ModEnvironment ParseEnvironment(string value)
        => Enum.TryParse<ModEnvironment>(value, ignoreCase: true, out var parsed) ? parsed : ModEnvironment.Both;
}

//ModManifest: the mod declaration, maps to the dll's embedded resource ncmod.json
//Kept as an embedded resource rather than an attribute so the scan phase can read it statically with MetadataReader without loading the assembly
public sealed class ModManifest
{
    //Id: the mod identifier, used by dependency relationships and lookups
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    //Version: the mod version
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    //DisplayName: the display name, the human-facing name, falls back to Id when absent
    [JsonPropertyName("name")]
    public string DisplayName { get; set; } = string.Empty;

    //Description: a one-line description
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    //Authors
    [JsonPropertyName("authors")]
    public List<string> Authors { get; set; } = new();

    //Contributors
    [JsonPropertyName("contributors")]
    public List<string> Contributors { get; set; } = new();

    //License: the license identifier
    [JsonPropertyName("license")]
    public string License { get; set; } = string.Empty;

    //Contact: contact info, maps to the contact section of vanilla fabric.mod.json
    [JsonPropertyName("contact")]
    public ModContact Contact { get; set; } = new();

    //Icon: the icon's embedded resource name, empty means look for the conventional icon.png
    [JsonPropertyName("icon")]
    public string Icon { get; set; } = string.Empty;

    //EnvironmentValue: the environment field, takes both/client/server
    [JsonPropertyName("environment")]
    public string EnvironmentValue { get; set; } = "both";

    //Entry: the entry class full name, the class needs a public Task Init()
    [JsonPropertyName("entry")]
    public string Entry { get; set; } = string.Empty;

    //Depends: depended mods and required versions, the key is the mod id and the value is the version constraint
    //Dependencies not listed are not version-constrained and load as usual as long as the mod is present
    //When a listed dependency version does not match, this mod is skipped; for syntax see VersionConstraint
    [JsonPropertyName("depends")]
    public Dictionary<string, string> Depends { get; set; } = new();

    //Hooks: the injection rule list
    [JsonPropertyName("hooks")]
    public List<ModHookRule> Hooks { get; set; } = new();

    //Mixins: mixin rules that move a class's fields and methods from this mod into a target type
    //Follows its own path separate from hooks and only takes effect for load-time rewriting
    [JsonPropertyName("mixins")]
    public List<ModMixinRule> Mixins { get; set; } = new();

    //EffectiveName: the display name, falls back to Id when name is absent
    [JsonIgnore]
    public string EffectiveName => DisplayName.Length > 0 ? DisplayName : Id;

    //Environment: the parsed environment side, falling back to Both when unrecognized
    [JsonIgnore]
    public ModEnvironment Environment
        => ModEnvironmentExtensions.ParseEnvironment(EnvironmentValue);
}

//ModMixinRule: a mixin rule that moves members of the source type into the target type
//The source type is in this mod's own assembly, so no assembly name is needed
public sealed class ModMixinRule
{
    //Target: the target type full name, members are moved here
    [JsonPropertyName("target")]
    public string Target { get; set; } = string.Empty;

    //Source: the source type full name, its fields and methods are moved away leaving only an empty shell
    [JsonPropertyName("source")]
    public string Source { get; set; } = string.Empty;

    //Interfaces: full names of interfaces for the target type to implement as well
    //No assembly name is written; looked up in the type index at assembly time, and if not found it is left empty for the engine to locate within the current module
    [JsonPropertyName("interfaces")]
    public List<string> Interfaces { get; set; } = new();
}

//ModContact: the mod's external links
public sealed class ModContact
{
    //Homepage
    [JsonPropertyName("homepage")]
    public string Homepage { get; set; } = string.Empty;

    //Sources: the source repository
    [JsonPropertyName("sources")]
    public string Sources { get; set; } = string.Empty;

    //Issues: the issue tracker
    [JsonPropertyName("issues")]
    public string Issues { get; set; } = string.Empty;
}

//ModHookRule: an injection rule whose fields map one to one to Lead.Hook's HookRule
public sealed class ModHookRule
{
    //Target: the target type full name, usually a type in the kernel
    [JsonPropertyName("target")]
    public string Target { get; set; } = string.Empty;

    //Method: the target method name
    [JsonPropertyName("method")]
    public string Method { get; set; } = string.Empty;

    //HookTypeName: the injection type name, maps to Lead.Hook's HookType, taking CallSite/MethodBody/NewObj etc.
    [JsonPropertyName("type")]
    public string HookTypeName { get; set; } = "CallSite";

    //PatchModeName: the patch mode name, maps to Lead.Hook's PatchMode, taking ILRewrite or RuntimeInject
    //ILRewrite rewrites bytes before the target assembly loads; RuntimeInject uses ReJIT to rewrite already loaded code
    //RuntimePatch is a retiring entry patch and should not be used by new rules
    [JsonPropertyName("patchMode")]
    public string PatchModeName { get; set; } = "ILRewrite";

    //ReplaceType: the full name of the class containing the replacement method
    //The class must not reference kernel types, otherwise resolving it pulls up the kernel and injection misses the kernel load
    [JsonPropertyName("replaceType")]
    public string ReplaceType { get; set; } = string.Empty;

    //ReplaceMethod: the replacement method name
    [JsonPropertyName("replaceMethod")]
    public string ReplaceMethod { get; set; } = string.Empty;

    //Label: the probe label, used only by Probe and Mark
    [JsonPropertyName("label")]
    public string? Label { get; set; }

    //Ordinal: when the same anchor matches multiple places in the host method, which one to use, zero-based
    //Absent means rewrite every occurrence; when the host has fewer occurrences this rule does not apply
    [JsonPropertyName("ordinal")]
    public int? Ordinal { get; set; }

    //ArgumentIndex: which argument to rewrite, zero-based, this counts as argument 0 for instance calls, used only with type=CallArg
    [JsonPropertyName("argumentIndex")]
    public int? ArgumentIndex { get; set; }

    //SliceFrom/SliceTo: intra-method range constraint written as "FullTypeName::MethodName"
    //Narrows matching to between the first call to SliceFrom and the first call to SliceTo in the host method, either end may be omitted
    [JsonPropertyName("sliceFrom")]
    public string? SliceFrom { get; set; }

    [JsonPropertyName("sliceTo")]
    public string? SliceTo { get; set; }

    //InType/InMethod: host constraint, matching anchors only within this method body, both may be omitted
    [JsonPropertyName("inType")]
    public string? InType { get; set; }

    [JsonPropertyName("inMethod")]
    public string? InMethod { get; set; }

    //PlacementName: how the anchor is placed, takes Replace/Before/After, default Replace
    //Before and After keep the original call and insert one callback before or after it; the callback receives the host method's arguments
    [JsonPropertyName("placement")]
    public string PlacementName { get; set; } = "Replace";

    //LocalIndex: the local variable slot, zero-based, used only when type is LocalRead/LocalWrite
    [JsonPropertyName("localIndex")]
    public int? LocalIndex { get; set; }

    //ConstantValue: the constant to match, used only when type is Constant
    //JSON carries no type tag so the integer width is guessed from the value range; to match long 5 on ldc.i8 you must write 5L in the annotation
    [JsonPropertyName("constantValue")]
    public JsonElement? ConstantValue { get; set; }

    //ScannedConstantValue: the constant written in the annotation, a CLR value is placed directly during the scan phase
    //The annotation path bypasses JSON so the boxed type is the one written on the property; it is stored separately from the manifest
    [JsonIgnore]
    public object? ScannedConstantValue { get; set; }

    //ConstantValueObject: the constant value the engine uses; the annotation takes priority, otherwise the manifest JSON is collapsed into a CLR value
    [JsonIgnore]
    public object? ConstantValueObject => ScannedConstantValue ?? (ConstantValue switch
    {
        null => null,
        { ValueKind: JsonValueKind.True } => true,
        { ValueKind: JsonValueKind.False } => false,
        { ValueKind: JsonValueKind.String } text => text.GetString(),
        { ValueKind: JsonValueKind.Number } number when number.TryGetInt32(out var int32) => int32,
        { ValueKind: JsonValueKind.Number } number when number.TryGetInt64(out var int64) => int64,
        { ValueKind: JsonValueKind.Number } number => number.GetDouble(),
        _ => null,
    });

    //EnvironmentValue: the environment side this rule applies to, takes both/client/server, default both
    //The same manifest can be shared by both sides, but a rule targeting a server type runs on the client with the target assembly entirely absent
    //Without filtering by side such rules error with "target is in no kernel assembly", which is expected rather than a fault
    [JsonPropertyName("environment")]
    public string EnvironmentValue { get; set; } = "both";

    //Environment: the parsed environment side, falling back to Both when unrecognized
    [JsonIgnore]
    public ModEnvironment Environment
        => ModEnvironmentExtensions.ParseEnvironment(EnvironmentValue);
}
