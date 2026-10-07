namespace NetCraft.ModLoader;

//ModStatus: a mod's status within the loading flow
public enum ModStatus
{
    //NotFound: not found
    NotFound,
    //Scanned: passed scanning, waiting to initialize
    Scanned,
    //Initializing: initializing
    Initializing,
    //Running: loaded
    Running,
    //Error: failed scanning, dependency check, or initialization
    Error,
    //Skipped: skipped by the load control
    Skipped,
    //Interrupted: intercepted by the load control and not taken over
    Interrupted,
}

//ModLoadAction: the return value of the load control callback, deciding the current mod's fate
public enum ModLoadAction
{
    //Continue: load by the default flow
    Continue,
    //Interrupt: hand loading over to the host
    Interrupt,
    //Skip: skip this mod
    Skip,
}

//ModInfo: mod information exposed to the outside
//Used by both the loading flow and the UI; all display-related fields come from the embedded manifest
public sealed class ModInfo
{
    //Name: the mod identifier taken from the manifest id
    public string Name { get; init; } = string.Empty;
    //Id: same value as Name, reads more directly
    public string Id => Name;
    //DisplayName: the display name, falls back to the id when the manifest has no name
    public string DisplayName { get; init; } = string.Empty;
    //Version
    public string Version { get; init; } = string.Empty;
    //Description
    public string Description { get; init; } = string.Empty;
    //Authors
    public IReadOnlyList<string> Authors { get; init; } = Array.Empty<string>();
    //Contributors
    public IReadOnlyList<string> Contributors { get; init; } = Array.Empty<string>();
    //License
    public string License { get; init; } = string.Empty;
    //Contact
    public ModContact Contact { get; init; } = new();
    //Icon: the icon's embedded resource name, empty means look it up by convention
    public string Icon { get; init; } = string.Empty;
    //Environment: the declared environment side
    public ModEnvironment Environment { get; init; }
    //Hooks: the injection rule list
    public IReadOnlyList<ModHookRule> Hooks { get; init; } = Array.Empty<ModHookRule>();
    //LoadMilliseconds: initialization time in milliseconds, present even under a millisecond
    //null if initialization was not reached, a negative value means this column does not apply, used by the easter-egg entry
    public double? LoadMilliseconds { get; init; }
    //EntryType: the entry class type
    public Type? EntryType { get; init; }
    //Instance: the entry class instance
    public object? Instance { get; init; }
    //AssemblyPath: the mod dll path
    public string AssemblyPath { get; init; } = string.Empty;
    //Status: the current status
    public ModStatus Status { get; set; }
    //Dependencies: names of the mods it depends on
    public IReadOnlyList<string> Dependencies { get; init; } = Array.Empty<string>();

    public override string ToString() => $"{Name} [{Status}]";
}

//ModLoadContext: the context of the load control callback, from which the host decides to allow, skip, or take over
public sealed class ModLoadContext
{
    //Name: the mod name
    public string Name { get; init; } = string.Empty;
    //EntryType: the entry class type
    public Type? EntryType { get; init; }
    //Instance: the entry class instance
    public object? Instance { get; init; }
    //AssemblyPath: the mod dll path
    public string AssemblyPath { get; init; } = string.Empty;
    //Dependencies: names of the mods it depends on
    public IReadOnlyList<string> Dependencies { get; init; } = Array.Empty<string>();
    //Completed: number processed
    public int Completed { get; init; }
    //Total: total to process
    public int Total { get; init; }
    //CustomData: extra data for the host's own use
    public Dictionary<string, object> CustomData { get; init; } = new();
}

//InterruptResult: the result of the intercept callback
public sealed class InterruptResult
{
    //ContinueOriginalLogic: still initialize by the default flow after taking over
    public bool ContinueOriginalLogic { get; init; }
    //MarkAsLoaded: mark as loaded directly without running initialization
    public bool MarkAsLoaded { get; init; }
    //Error: non-empty means loading failed
    public string Error { get; init; } = string.Empty;
    //CustomData: extra data for the host's own use
    public Dictionary<string, object> CustomData { get; init; } = new();
}

//LoadResult: a summary of one loading flow
public sealed class LoadResult
{
    public bool Success { get; set; }
    public List<string> Errors { get; } = new();
    public List<string> Skipped { get; } = new();
    public List<string> Interrupted { get; } = new();
    public List<string> Loaded { get; } = new();
}

//ProgressInfo: a load progress snapshot
public sealed class ProgressInfo
{
    public string Status { get; set; } = string.Empty;
    public int Completed { get; set; }
    public int Total { get; set; }
    public string CurrentMod { get; set; } = string.Empty;
    public int Percentage { get; set; }
}
