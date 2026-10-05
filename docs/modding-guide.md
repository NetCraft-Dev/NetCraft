# NetCraft Modding Guide


Details for individual APIs are not here; see [mod-api.md](mod-api.md).

---

## 1. Runtime structure first

### 1.1 Three process entry points

NC has three entry points, and the mod injection pipeline is the same for all three:

| Entry | Purpose |
| --- | --- |
| `NetCraft.Loader` | one exe for both sides: `--server` starts the server; `--client` or no mode flag starts the client |
| `NetCraft.ServerExe` | standalone server executable |
| `NetCraft.ClientExe` | standalone client executable |

`Main` itself is a thin shell that only registers callbacks and hands the work to the next method. Take `NetCraft.ServerExe`:

```csharp
public static int Main(string[] args)
{
    EmbeddedAssemblyLoader.Initialize();   // register the kernel assembly resolution callback
    BootMods(args);                        // run the mod bootstrap
    return Launch(args);                   // only now enter the business implementation
}
```

This separation is not a matter of style. When the JIT compiles a method it resolves **all types** appearing in that method body, and this happens before the method executes. If `Main` called `ServerMain.Run(args)` directly, `NetCraft.Server.dll` would be pulled up the instant `Main` is JIT-compiled, before the mod bootstrap has run, and the rewrite window would be gone. So both `BootMods` and `Launch` must be marked `MethodImplOptions.NoInlining` — without the marker the JIT inlines them back into `Main`, defeating the split.

`NetCraft.Loader` has the same structure, except its mode detection and mod bootstrap are both in `Launch`, and `Main` keeps only the two steps `Initialize` and `Launch`.

### 1.2 Kernel assemblies in the kernel/ subdirectory

The output directory after a build looks like this:

```
NetCraft.ServerExe.exe
NetCraft.dll              <- main library, embeds all lower-level sub-libraries
NetCraft.ModLoader.dll    <- the loader itself
NetCraft.ServerExe.dll    <- entry assembly
kernel/
  NetCraft.Game.dll
  NetCraft.Server.dll
  ... remaining kernel assemblies
mods/
  your-mod.dll
```

Why move them into `kernel/` instead of leaving them in the root?

The .NET host treats assemblies registered in `deps.json` as TPA (Trusted Platform Assemblies). For assemblies in the TPA, the runtime resolves **by path** — bytes passed to `AssemblyLoadContext.LoadFromStream` are simply ignored. That is, even if we feed in rewritten bytes ahead of time, the runtime still reads the unrewritten copy from disk. Only by removing the kernel assemblies from `deps.json` and moving the files away does the runtime call back into `AssemblyLoadContext.Resolving` on resolution failure, giving us the chance to hand over the rewritten bytes.

The three kinds left in the root cannot be moved: the main library (it is the embedding host and must start first), the loader itself (the bootstrap code lives in it), and the entry assembly (the apphost starts from it).

**Cost**: the entry assembly itself cannot be injected into. If your hook target happens to live in the `NetCraft.ServerExe.dll` assembly, it is ineffective. Kernel business code is all under `kernel/`, so normally this is not a problem.

### 1.3 Mod loading sequence

```
EmbeddedAssemblyLoader.Initialize()
  └─ install the Resolving callback
BootMods → ModBootstrap.Run(current side)
  ├─ statically scan mods/*.dll (MetadataReader reads embedded ncmod.json, no assembly is loaded)
  ├─ filter out mods whose environment does not match the current side
  ├─ assemble injection rules and hand the rewriter to the main library
  ├─ preload target assemblies: read bytes → run through rewriter → LoadFromStream
  └─ call each mod entry's Init()
Launch → ServerMain/ClientMain.Run(args)
  └─ kernel business starts running; the probes are already inside
```

Note the order: **declarations are scanned, rewritten bytes are loaded, and entry code runs last**. By the time a mod's `Init()` executes, the kernel assemblies have already been replaced.

---

## 2. Key differences from Fabric

| Dimension | Fabric | NetCraft |
| --- | --- | --- |
| Language / runtime | Java / JVM | C# / .NET 10 (CoreCLR) |
| Mod carrier | jar containing `fabric.mod.json` | dll embedding `ncmod.json` |
| Declaration reading | read a file inside the jar | `MetadataReader` statically reads embedded resources without loading assemblies |
| Code injection | Mixin (annotations in source; members are mixed into the target class at class load) | `Lead.Hook` (rules declared in a manifest or annotations; bytes are rewritten in place during assembly resolution) |
| Injection granularity | any line in a method body, including locals and intermediate expression values | thirteen forms (call site, field read/write, constructor, type check, boxing, local variable, constant, whole-method-body replacement, probes, etc.), with insert-before or insert-after |
| Loading model | Fabric Loader + Knot class loader | single default ALC + `AssemblyLoadContext.Resolving` |
| Official API scope | Fabric API has a great many modules | NetCraft-ModApi currently has only event and command extension points |

### 2.1 Injection styles: annotations or manifest, pick one

Fabric's Mixin is annotated **in source**:

```java
@Inject(method = "tick", at = @At("HEAD"))
private void onTick(CallbackInfo ci) { ... }
```

NC supports both styles, but their prerequisites differ: the annotation form relies on `InjectAttribute` in `NetCraft.ModApi.Extension`, so a mod that does not reference it cannot use annotations; the manifest form is pure data written in `ncmod.json` and requires no reference for injection rules.

**Annotation**, placed on your own replacement method:

```csharp
[Inject(typeof(DedicatedServer), nameof(DedicatedServer.Tick),
        HookType = "Mark", Label = "server_tick", Environment = "server")]
public static void OnTick(object self) { ... }
```

**Manifest**, written in `hooks` in `ncmod.json`:

```json
{
  "target": "NetCraft.Game.Server.DedicatedServer",
  "method": "Tick",
  "type": "Mark",
  "label": "server_tick",
  "environment": "server",
  "replaceType": "MyMod.Probe",
  "replaceMethod": "OnTick"
}
```

During assembly the two routes merge into one rule table, and **if the same injection point is written in both, the annotation wins**. After merging they are indistinguishable; the difference is in prerequisites and ergonomics:

| | Annotation | Manifest |
| --- | --- | --- |
| Where it is written | on the replacement method | in `hooks` in `ncmod.json` |
| Prerequisite | must reference `NetCraft.ModApi` | none, pure data |
| Type names | `typeof` / `nameof`, checked by the compiler | hand-written strings; typos are only found during assembly |
| What it can carry | injection rules only | mod identity (id, entry, environment, display info) and injection rules |

So `ncmod.json` must be written whether or not you use annotations; it is the sole source of mod identity. Annotations only make rules less error-prone to write. The manifest has no dependency field; dependency relationships are inferred from assembly references (see 6.1) and need no declaration.

Conversely, **a mod that does not reference `NetCraft.ModApi` can only use the manifest** — this affects more than injection rules: events like `ServerEvents` and the `Nc*` facades are also in ModApi (under `NetCraft.ModApi.Wrapper`, see [3.3](#33-a-side-by-side-example)), so a mod that cannot use annotations also cannot use them.

The differences from Fabric remain:

- **How and when changes happen**: Mixin has a transformer **mix members of the mixin class into** the target class at **class load**, so what gets loaded is a synthesized new class and the original no longer exists; NC rewrites the target method's instructions in place **before the assembly enters memory**, so the class is still the same class, only its method body changes. Both rewrite at load time, neither modifies bytecode at compile time — Mixin's annotation processor only generates a refmap (obfuscation mapping) and performs validation at build time, while NC is not obfuscated and has no such layer at all.
- Mixin can inject at **any position in the middle of a method body**; NC can target a specific call site, field access, construction, local variable read/write, or constant within a specified host method, and can insert before or after it (`InType`/`InMethod` narrow the scope, `Placement` decides insert or replace), but it **cannot reach an arbitrary line number** and cannot change a jump target or an intermediate expression value on the stack.
- Mixin targets use a string method name plus descriptor; NC uses "full type name + method name", so same-name overloads all match, and precision to a single one requires `InType`/`InMethod`.

**Which layer handles annotations**: the annotation type (`InjectAttribute`) is provided by `NetCraft.ModApi.Extension`, and it is resolved by `NetCraft.ModLoader` — when scanning mods it statically reads the `CustomAttribute` table with `MetadataReader`, without loading assemblies. **`Lead.Hook` does not recognize annotations**; it only sees the merged rule table, and the native injection layer recognizes only the description bytes compiled on the managed side, not even reading `ncmod.json`.

This determines what annotations can express: what you can write depends entirely on which fields `InjectAttribute` has. Currently there are seven — target type, method name, `HookType`, `Label`, `Environment`, `PatchMode`, `Ordinal` — and `InType`/`InMethod`/`Placement` from [2.4](#24-narrowing-to-one-site-host-scoping-and-placement) and `LocalIndex`/`ConstantValue` from [2.5](#25-in-method-body-anchors-local-variables-and-constants) **cannot be written in annotations**; use the C# API or wait for the manifest to catch up. The manifest side is missing these too — the only thing it accepts beyond annotations is `ordinal`.

For the thirteen injection forms see the [modding-guide appendix](#appendix-hooktype-overview) and [mod-api.md](mod-api.md).

### 2.2 An important constraint: probe classes must not carry kernel types in signatures

NC rewriting happens **before** the kernel assemblies are loaded. When assembling rules, `Lead.Hook` uses reflection to find your replacement method and build a method reference, and this process resolves every parameter type and return type in the signature.

Therefore: **a replacement method's signature may only use BCL types and `object`**. Once a `NetCraft.*` type appears in the signature, resolving it pulls up the kernel assemblies early and injection fails outright.

When you need a kernel object, declare the parameter as `object` and cast inside the method body:

```csharp
//assembly only sees object; the method body is JIT-compiled after the kernel starts
public static void OnCommandsReady(object dispatcher)
{
    var typed = (CommandDispatcher<CommandSourceStack>)dispatcher;
    ...
}
```

### 2.3 CallSite is replacement, not insertion

A `CallSite` rule's replacement method **replaces** the original call, so the original method is not executed. To preserve the original behavior you must restore it yourself in the replacement method:

```csharp
public static void OnCommandsReady(object dispatcher)
{
    var typed = (CommandDispatcher<CommandSourceStack>)dispatcher;
    EffectCommand.Register(typed);              // restore the replaced call
    ServerEvents.CommandRegister.Publish(...);  // then add the mod's own logic
}
```

Miss this step and the original functionality disappears entirely.

A few details about carrying this out:

- **`this` on an instance method counts as a parameter too**. Whether the target method is an instance method determines whether the replacement method needs an extra leading parameter. In IL both `call` and `callvirt` count as instance calls — for a non-virtual method on a `sealed` type the compiler emits `call`.
- **One rule key covers all overloads under that class name**; overloads with the same parameter count share one replacement method. `Disconnect(string)` and `Disconnect(Component)` share this way, and the replacement method dispatches by the real argument type.
- **A private method cannot be called from outside**, so the replacement method cannot restore the original call. Either give up this hook point or call it once via reflection (acceptable at low frequency).
- **Value-type parameters and return values cannot be declared as `object`**, `object` is a reference on the stack while `float`/`bool` are values, and a mismatch is invalid IL. Keep these two positions as their real types.
- **Unicast callbacks cannot be assigned directly**. Some kernel callback properties (e.g., the three chunk callbacks on `ServerChunkCache`) are `Action<T>` rather than `event`, and the kernel already occupies them. A mod assigning directly overrides the kernel's copy with no error at all. The correct approach is to hook the property's setter and, at the moment of assignment, chain your logic and the kernel callback into one wrapper delegate.

### 2.4 Narrowing to one site: host scoping and placement

An instruction-level rule's default scope is **the entire assembly** — every place that calls the target method or reads/writes the target field matches. To narrow to one site, use two optional parameters:

| Parameter | Effect |
| --- | --- |
| `InType` / `InMethod` | match anchors only inside the specified host method body; both empty means unrestricted |
| `Placement` | `Replace` replaces the anchor (default); `Before` / `After` keep the anchor and insert one call before or after it |
| `Ordinal` | when the same anchor matches multiple places in the host method, pick which one, 0-based. Omitted means every place is modified |

```csharp
//example: instrument only when LevelChunk reads block state; PalettedContainer::Get elsewhere is untouched
new HookRule("NetCraft.Storage.PalettedContainer", "Get", typeof(MyProbe), nameof(MyProbe.OnGet),
    HookType.CallSite, PatchMode.ILRewrite,
    inType: "NetCraft.Storage.LevelChunk", inMethod: "GetBlockState",
    placement: HookPlacement.Before)
```

The two modes impose different requirements on the callback signature:

- **Replace mode** aligns with the arguments of the replaced call (including `this` for instance calls); whether the callback restores the original call is up to you.
- **Insert mode** passes the **host method's parameters** (including `this`), consistent with the `MethodBody` convention. Insertion does not disturb the stack the anchor has already built up; the original call runs as usual, just with one extra callback before or after it.

A few boundaries:

- `InType` and `InMethod` are independent; you may specify just one. Both empty is equivalent to no scoping.
- Multiple rules may hook the same anchor, each scoped to a different host; **the first host match wins**.
- `Placement` only applies to instruction-level forms (`CallSite`, `NewObj`, field read/write, `TypeCheck`, `Box`, `FunctionPointer`, and the three kinds in [2.5](#25-in-method-body-anchors-local-variables-and-constants)); `MethodBody` always replaces the whole thing.
- `Ordinal` counts the **order of matches**, regardless of whether that site is ultimately modified; if the rule does not occur enough times in the host method, the rule does not land. Same idea as Mixin's `@At(ordinal)`.
- `InType` / `InMethod` / `Placement` / `LocalIndex` / `ConstantValue` are currently available only on the C# API; neither `ncmod.json` nor `[Inject]` supports them (the manifest accepts `ordinal`), so manifest-based mods cannot use the first few.

### 2.5 In-method-body anchors: local variables and constants

The previous kinds anchor to a **referenced entity** (a method, field, or constructor), whereas `LocalRead` / `LocalWrite` / `Constant` anchor to **a position inside the host method body**, corresponding to Mixin's `@ModifyVariable` and `@ModifyConstant`. For these three, `OriginalType` / `OriginalMethod` name the **host method**, not a referenced entity.

| Form | Extra parameter | Selected positions |
| --- | --- | --- |
| `LocalRead` | `LocalIndex` | every read of that slot (0-based) |
| `LocalWrite` | `LocalIndex` | every write to that slot |
| `Constant` | `ConstantValue` | every load of that constant, compared by boxed type |

```csharp
//example: insert one callback before the write to slot 0 in G
new HookRule("TargetLib.Host", "G", typeof(MyProbe), nameof(MyProbe.OnWrite),
    HookType.LocalWrite, PatchMode.ILRewrite,
    localIndex: 0, placement: HookPlacement.Before)

//example: replace the constant 5 in G with the return value of OnConst()
new HookRule("TargetLib.Host", "G", typeof(MyProbe), nameof(MyProbe.OnConst),
    HookType.Constant, PatchMode.ILRewrite, constantValue: 5)
```

In replace mode the callback signature aligns with the **instruction's stack effect**, not the host parameters:

| Anchor | Stack effect | Replacement method signature |
| --- | --- | --- |
| `LocalRead` | pushes one value | zero parameters, returns that value |
| `LocalWrite` | pops one value | one parameter |
| `Constant` | pushes one value | zero parameters, returns that value |

`ConstantValue` is compared by boxed type, so `5` (int) and `5L` (long) are two different anchors; to match `ldc.i8` you must pass `long`.

A slot is the compiled local variable index; the same source may change it under a different compiler version, so do not treat it as a stable identifier when porting across versions.

### 2.6 Runtime injection: modifying already-running code

The injection discussed so far all happens **before assembly load** — bytes are rewritten first, then handed to the runtime. The premise is that the target assembly has not been loaded yet.

`Lead.Hook` has another route: using the CLR's Profiler interface (ReJIT) to modify code that is **already loaded, or whose methods have already run**. Both share the same `HookRule`, and the parameters from [2.4](#24-narrowing-to-one-site-host-scoping-and-placement) and [2.5](#25-in-method-body-anchors-local-variables-and-constants) remain available:

```csharp
var engine = new HookEngine();
engine.AddRule(new HookRule("TargetLib.Host", "Callee", typeof(Hooks), nameof(Hooks.Double),
    hookType: HookType.CallSite, patchMode: PatchMode.RuntimeInject, inMethod: "A"));

//one call and injection is done; no restart and no file changes
RuntimeInjector.Inject(typeof(Host).Assembly, engine);
```

| | Load-time rewriting | Runtime injection |
| --- | --- | --- |
| Manifest `patchMode` | `ILRewrite` (default) | `RuntimeInject` |
| Timing | before the assembly enters memory | any time after the process has started |
| Foundation | Mono.Cecil byte rewriting | CLR Profiler ReJIT |
| Prerequisite | the target has not been loaded | the target is already in the process |
| Modify already-JIT-compiled code | not possible | possible |

**Why rules are shared**: Cecil still does the rewriting here, but the result is not written to disk; instead it is compiled into a description handed to the native layer, which submits the new method body to the CLR at runtime, with the remaining version management left to the CLR.

**How a mod does it**: add a `patchMode` entry to the rule item in `ncmod.json`; the `[Inject]` annotation has a parameter of the same name.

```json
{ "target": "NetCraft.Game.Server.DedicatedServer", "method": "Tick",
  "type": "CallSite", "patchMode": "RuntimeInject",
  "replaceType": "MyMod.Probe", "replaceMethod": "OnTick" }
```

```csharp
[Inject(typeof(DedicatedServer), nameof(DedicatedServer.Tick), PatchMode = "RuntimeInject")]
public static void OnTick(object self) { }
```

**What assembly does**: these rules do not go through the load-time rewriting path (`ModHooks.Rewrite` applies only `ILRewrite`); at assembly time a separate runtime target table is recorded. After the kernel assemblies are preloaded and before mods' `Init()`, the loader takes each of their **already loaded** instances and submits the rewritten method bodies to the CLR. If a target is not loaded at that moment it is skipped with a warning, and it will not be loaded early on its behalf — the constraint from [2.2](#22-an-important-constraint-probe-classes-must-not-carry-kernel-types-in-signatures) that "pulling up the kernel early misses the window" is reversed in direction here, with the same conclusion: if it is not present, it cannot be done.

**To hook the native injection layer**: the ReJIT switch can only be set via environment variables at process start (`CORECLR_ENABLE_PROFILING` / `CORECLR_PROFILER` / `CORECLR_PROFILER_PATH`); setting them after startup has no effect. When the loader detects `RuntimeInject` rules early during startup and the process is not yet hooked, it **restarts the process with the same command line** carrying these three variables (`NC_PROFILER_ATTACHED=1` guards against repeated restarts when "hooked but not effective"). The native library `lead_hook_native` must be placed in the program root, or pointed elsewhere via `NC_PROFILER_PATH`; if neither is present, the whole batch of rules is downgraded to a single warning and startup is not blocked.

**Do not mix it with load-time rewriting on the same target**: a method body submitted by runtime injection is derived from **original bytes** and does not include changes load-time rewriting made to the same method — if the same method is hit by both rule types, the load-time version is overwritten entirely. Assembly cannot tell whether two rules hit the same method, so it can only make a coarse judgment by target assembly and record a warning.

**Annotations do not take part in this path directly**: the native injection layer does not recognize `InjectAttribute` or read `ncmod.json` — it recognizes only description bytes. Annotations and the manifest are both **assembly-time** things (see [2.1](#21-injection-styles-annotations-or-manifest-pick-one)), parsed by `NetCraft.ModLoader` into `HookRule` and then handed to `RuntimeInjector`; the mod side does not need to handle them differently.

**Limitations** (narrower than load-time rewriting): methods with exception handling tables are not supported, the local variable table cannot be changed, generic types and methods are not supported, and operands recognize only method references (field references, string constants, and type tokens throw `NotSupportedException`).

**Performance**: injection happens only at registration; afterward the method is ordinary JIT code with the same call overhead as without injection. Attaching the profiler has a one-time cost — enabling ReJIT requires disabling ReadyToRun images at the same time, and measured process startup is about 80–110 ms slower; steady-state computation shows no difference. For a server like NC whose startup is already measured in seconds, this is negligible.

### 2.7 Do two mods modifying the same class conflict like Mixin?

First, why things conflict on the Mixin side. Mixin **mixes members into the target class** and applies them at class load: when multiple mixins mix into the same class, cases like injecting at the same place repeatedly or adding same-named members to the same class throw `MixinApplyError`, and the default fail-hard **kills the game outright**; moreover this detection happens at the instant of class load, when the game may already be half-running.

NC's model is different, and the surface where conflicts can happen is much smaller:

| | Mixin | NetCraft |
| --- | --- | --- |
| Landing method | mix members into the target class + rewrite bytecode | rewrite IL instructions only; no type synthesis, no members added |
| Structural conflicts (same-named members, inheritance conflicts) | yes | none |
| When rules are validated | at class load | at assembly time, statically reading metadata |
| Two rules hitting the same place | throws | first come, first served; the latter silently fails |
| One mod fails | may drag down the whole load | affects only itself |

**Static validation**: rules are not built by loading assemblies and reflecting over types, but by reading PE metadata tables. So problems like "the target type is not in any known assembly" or "the injection form is misspelled" are recorded and skipped **early at startup**, without waiting for a class to load before exploding.

**Failure isolation**: when a mod's rules fail to parse, the replacement class fails to load, or the entry `Init()` throws, only **that one mod** is marked as failed (status `Error`, shown as "load failed" on the MODS page) and the other mods load as usual. One wording needs correcting here: NC has **no runtime unloading** — mods are loaded once, and `ModManager` explicitly does not offer dynamic loading/unloading. So-called "auto-unload on failure" is actually **load-time isolation**: a failed mod is not initialized, but it is also not "unloaded".

**Dynamic injection**: on the ReJIT route from [2.6](#26-runtime-injection-modifying-already-running-code), the semantics of several mods competing for the same method match the static case — the first registered wins, and later requests are sent but cannot claim it (`GetReJITParameters` claims by "module + method", taking the first).

We hit a pitfall on this route once, worth recording: in an early implementation, `FindTypeRef`'s **resolution scope parameter was passed as `mdTokenNil`**, whose semantics are "match only TypeRefs with no resolution scope" — our references all hang off `AssemblyRef`, so the one we had built was never found. It manifested as: the first injection succeeded, but on the second injection reference resolution failed, no new method body could be built, the CLR fell back to the original IL, and **the first injection was lost along with it** (the target method reverted to its uninjected behavior). After the fix it no longer reproduced, but the limitation remains: **metadata injection of references must complete within the window right after the target module loads; the later it is, the more likely it fails**.

**The cost must be stated clearly**: NC's non-crashing behavior comes at the cost of conflicts being easily missed — Mixin at least interrupts loading, whereas NC lets the later one silently fail. To address this, assembly performs a **same-anchor conflict check**: when the same injection point is declared by multiple mods, the later-assembled one is recorded in `ModHooks.Warnings` and reported as a warning in the startup log (without blocking loading):

```
Mod injection conflict mod my-mod-b's injection NetCraft.Game.Server.DedicatedServer::Tick[CallSite/ILRewrite] is already taken by mod my-mod-a; this rule will not take effect
```

The detection key is "target type + method + injection form + patch mode". **Host scoping is not distinguished** — neither the manifest nor annotations can write `InType`/`InMethod`, so rules coming from mods are naturally whole-assembly in scope, and the same key means a collision. Rules added directly via the C# API bypass this check, since in that case two rules may each hit a different host and are inherently non-conflicting.

**Runtime injection goes through this check too**: it enters the same assembly entry point, and the patch mode in the detection key keeps it separate from load-time rewriting; when both types land on the same target assembly there is a separate overwrite notice (see [6.4](#64-two-mods-injecting-the-same-target)).

### 2.8 Mixins: adding members to a target type

The previous sections all modify instructions in existing code and cannot create anything new. To **add fields, methods, or interfaces** to a target type, use a mixin.

The relationship to Mixin's syntax is as follows:

| Mixin | NC |
| --- | --- |
| `@Mixin(X.class)` on the mixin class | `[Mixin(typeof(X))]` on the source class |
| members of the mixin class are mixed into the target class | fields and methods of the source class are moved into the target type |
| `@Unique` adds a private field | write an ordinary field in the source class; it is moved over the same way |
| `@Shadow` references an existing member of the target class | not needed; write `X`'s members directly and then hook them |
| `@Implements` / `implements` | `Interfaces` |

It can also be written in the manifest:

```json
{
  "target": "NetCraft.Game.World.Entity.SomeEntity",
  "source": "MyMod.SomeEntityMixin",
  "interfaces": ["MyMod.ITagged"]
}
```

```csharp
[Mixin(typeof(SomeEntity), Interfaces = new[] { typeof(ITagged) })]
public class SomeEntityMixin
{
    //after being moved over, this is an instance field on the target type
    public int MyCounter = 5;

    //a mixed-in method; it reads/writes the field moved along with it
    public int Bump() => MyCounter + 1;

    //the implementation the interface requires; after being moved over the target type implements ITagged
    public string Describe() => $"tagged:{MyCounter}";
}
```

**Move, not copy**. These members are removed from the source class, leaving only an empty shell — just like Mixin's mixin class, **mod code should no longer use that class** (`new SomeEntityMixin()` or calling its methods will fail to find the members).

A few landing rules:

- **Only applies to load-time rewriting**. The target type must be in a kernel assembly or mod assembly that has not entered memory yet. Runtime injection only submits method bodies and cannot change type layout, so this form cannot exist on it.
- **Constructor initializers come along**. Values written in the source class's field initializers are merged into every instance constructor of the target type; static field initializers are merged into the static constructor (created if the target has none). The base-class chaining call inside the source class's constructor is stripped, so the base constructor is not run twice.
- **Rules with interfaces mark the moved public instance methods as virtual**. Interface dispatch only recognizes the vtable, and without the marker the CLR would determine the interface is not implemented and fail to load outright. So do not expect those methods to stay non-virtual when mixing in an interface.
- **Same-named members are skipped**. When the target type already has a field or method with the same name, that one item is not moved and the rest proceed as usual. When two mods mix into the same target type, both land, and only the name-colliding part of the latter is skipped — gentler than the "latter fails entirely" behavior of hooks in [2.7](#27-do-two-mods-modifying-the-same-class-conflict-like-mixin).
- **Nested types are not moved**, and nested types or generic methods in the source class are also currently outside this path's coverage.

The source type must be in **the mod's own assembly**, so neither the manifest nor the annotation writes an assembly name.

### 2.9 Two routes: wrapper layer and extension points

`NetCraft.ModApi`'s public surface is split into two namespaces, corresponding to two usages:

| Namespace | Contents | What you get |
| --- | --- | --- |
| `NetCraft.ModApi.Wrapper` | `ServerEvents` / `ClientEvents` / `NetworkEvents`, `NcServer` / `NcWorld` / `NcPlayers` / `NcLists` / `NcRegistries` / `NcRecipes` / `NcStartup`, and object handles like `NcPlayer` / `NcLevel` | wrapper types; no kernel types on the public surface |
| `NetCraft.ModApi.Extension` | `[Inject]` / `[Mixin]` annotations | rules bind to kernel class and method names |

The two are **parallel** routes, not one layered on top of the other:

- **For stability, use `Wrapper`**. The facades handle the kernel's tedious call ordering for you (writing a single block while touching the level, the player list, and the sync chain is one example), and event args are all wrapper types. The cost is that capabilities the facades do not expose are unavailable to you.
- **For completeness, use `Extension`**. Injection rules modify kernel classes and methods directly, but the target names you write are the kernel's names, so when the kernel changes the rules must change with it.

You can reference both. The `Wrapper` line is being consolidated toward "no kernel types on the public surface"; the player and level parts are done — `Player` / `Attacker` in player events and the in/out parameters of `NcPlayers` are `NcPlayer` handles, `NcWorld.Overworld` / `Nether` / `End` / `Get` and `LevelTickArgs.Level` are `NcLevel` handles, and block coordinates are plain `x y z` ints; entities and the remaining value types (`BlockPos` / `BlockState` / `Vec3`) are not wrapped yet.

One more boundary to call out: **the wrapper layer does not shield injection**. The `hooks` rules or `[Inject]` annotations you write still bind to kernel class and method names, and break just the same when the kernel changes.

---

## 3. Migrating from Fabric

### 3.1 Concept mapping

| Fabric | NetCraft |
| --- | --- |
| `fabric.mod.json` | the `ncmod.json` embedded in the dll |
| `ModInitializer.onInitialize()` | the entry class's `public Task Init()` |
| `@Inject` / `@Redirect` | the `[Inject]` annotation, or rules like `Mark` / `Probe` / `CallSite` in `hooks` |
| `@ModifyVariable` | `LocalRead` / `LocalWrite`, see [2.5](#25-in-method-body-anchors-local-variables-and-constants); not writable as an annotation |
| `@ModifyConstant` | `Constant`, see [2.5](#25-in-method-body-anchors-local-variables-and-constants); not writable as an annotation |
| `@Accessor` | no equivalent yet (`private` members need no visibility widening; just write a rule) |
| `Registry.register(...)` | the kernel registry (`BuiltInRegistries`) |
| `ServerLifecycleEvents.SERVER_STARTED` | `ServerEvents.Started` |
| `ServerTickEvents.END_SERVER_TICK` | `ServerEvents.Tick` |
| `CommandRegistrationCallback` | `ServerEvents.CommandRegister` |
| `ClientTickEvents.END_CLIENT_TICK` | `ClientEvents.Tick` |
| `FabricLoader.getInstance().getModContainer(id)` | no equivalent yet (`ModManager` is not open to mods) |
| `@Mixin` / `@Unique` / `@Implements` | the `[Mixin]` annotation or the manifest's `mixins`, see [2.8](#28-mixins-adding-members-to-a-target-type) |

### 3.2 What does not carry over

- **Mixin's annotation system**: NC has two annotations, `[Inject]` and `[Mixin]`, both of which are only **declaration styles**, equivalent to `hooks` / `mixins` in `ncmod.json` and merged at assembly (they are resolved by the loader, not `Lead.Hook`, see [2.1](#21-injection-styles-annotations-or-manifest-pick-one)). Instruction rewriting lands at load time by default, and can be changed to runtime submission per [2.6](#26-runtime-injection-modifying-already-running-code); adding members and interfaces goes through the mixins of [2.8](#28-mixins-adding-members-to-a-target-type). Targeting has no `@At`-style string syntax, but forms like `CallSite`/`FieldRead`/`LocalWrite`/`Constant`, together with `Ordinal`, `InType`/`InMethod`, and `Placement`, can cover the usages of `HEAD`/`RETURN`/`INVOKE`/`FIELD`/`NEW`/`CONSTANT`/`LOAD`/`STORE` and `shift`; what is missing is `JUMP`.
- **AccessWidener**: none. Visibility is no obstacle to IL rewriting in NC; `private` methods can be hooked the same way (the rewriter works at the byte level).
- **Yarn / Mojang mappings**: not needed. NC is C# source translated directly from vanilla, with type and method names corresponding to vanilla, only with naming style following C#.
- **The vast majority of Fabric API modules**: only capabilities covered by `NetCraft-ModApi` are available; for the rest, write your own injection rules or wait for the API to catch up.

### 3.3 A side-by-side example

Fabric: log a line when the server starts and register a command.

```java
public class MyMod implements ModInitializer {
    @Override
    public void onInitialize() {
        ServerLifecycleEvents.SERVER_STARTED.register(server -> {
            System.out.println("server started");
        });
        CommandRegistrationCallback.EVENT.register((dispatcher, registry, env) -> {
            dispatcher.register(CommandManager.literal("mymod")
                .executes(ctx -> { ctx.getSource().sendSuccess(() -> Text.literal("hi"), false); return 1; }));
        });
    }
}
```

NC:

```csharp
public sealed class MyModEntry
{
    public Task Init()
    {
        ServerEvents.Started.Subscribe(_ => Log.Info("server started"));
        ServerEvents.CommandRegister.Subscribe(args =>
            args.Register("mymod", "my mod command", builder =>
                builder.Executes(context =>
                {
                    context.GetSource().SendSuccess("hi");
                    return 1;
                })));
        return Task.CompletedTask;
    }
}
```

Manifest (`ncmod.json`, as an embedded resource):

```json
{
  "id": "my-mod",
  "version": "0.1.0",
  "environment": "server",
  "entry": "MyMod.MyModEntry",
  "hooks": []
}
```

Note: even an empty `hooks` works here — events like `ServerEvents.Started` are provided by `NetCraft-ModApi`'s own probes, and your mod only needs to subscribe (`ServerEvents` is under `NetCraft.ModApi.Wrapper`, see [2.9](#29-two-routes-wrapper-layer-and-extension-points)). You only need to write your own hook rules when you want to hook a place in the kernel where ModApi does not yet provide an event.

---

## 4. Basic requirements for an ncm

ncm means NetCraft mod. An ncm is a .NET class library dll embedding an `ncmod.json`, placed in the `mods/` directory.

The easiest way to start is the template:

```
dotnet new install NetCraft.ModsProjectType
dotnet new ncm -n MyMod -e server
```

If the local package has not been published yet, use `dotnet new install <nupkg path>`, or run `dotnet pack` in the repository and then install the output.

`-e` takes `both` (default) / `server` / `client`, determining the manifest's `environment` and which side's subscription code is generated in the entry class. The template ships the NC reference assemblies, so no project reference is needed, and `ncmod.json`'s id / entry are filled in from the project name.

Starting at 4.1, the following covers what a hand-written project must satisfy.

### 4.1 Project file

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <!-- Private must be turned off, otherwise the NC assemblies get embedded into the mod dll by EmbedDependencies in 4.6 -->
    <ProjectReference Include="xxx\NetCraft\NetCraft.csproj" Private="false" />
    <ProjectReference Include="xxx\NetCraft.ModApi\NetCraft.ModApi.csproj" Private="false" />
  </ItemGroup>

  <ItemGroup>
    <EmbeddedResource Include="ncmod.json" LogicalName="ncmod.json" />
  </ItemGroup>
</Project>
```

`LogicalName` must be `ncmod.json`; the scanner recognizes only that name.

The template takes a different route: assembly references under `libs/` (`Reference Include="libs\*.dll" Private="false"`). Follow that when you do not have the NC source. What both approaches share is that **NC's own dlls must never enter the output directory** — `EmbedDependencies` from [4.6](#46-third-party-dependencies) embeds third-party dlls from the output directory into the mod, and if NC's assemblies get embedded too there will be two sets of type identity.

### 4.2 ncmod.json fields

```json
{
  "id": "my-mod",
  "version": "0.1.0",
  "name": "My Mod",
  "description": "one-line description",
  "authors": ["someone"],
  "license": "MIT",
  "contact": { "homepage": "https://...", "sources": "https://..." },
  "icon": "icon.png",
  "environment": "both",
  "entry": "MyMod.ModEntry",
  "hooks": []
}
```

| Field | Required | Notes |
| --- | --- | --- |
| `id` | Yes | mod identifier, used by dependencies and lookups. An empty string is skipped by the scanner |
| `version` | Recommended | version number; when depended on by other mods, version constraints are judged by it, see below |
| `name` | No | display name; this is what the mod page shows, defaulting back to `id` |
| `description` | No | one-line description |
| `authors` / `contributors` | No | credits, a string array |
| `license` | No | license identifier |
| `contact` | No | external links; may take `homepage` / `sources` / `issues` |
| `icon` | No | the icon's embedded resource name, see 4.7 |
| `environment` | No | `both` / `client` / `server`, default `both`. When it does not match the current side the whole mod is not loaded |
| `entry` | Yes | full name of the entry class; the class must have `public Task Init()` |
| `depends` | No | other mods it depends on and required versions, see below |
| `hooks` | No | list of injection rules; an empty array means subscribing only to events ModApi already has |
| `mixins` | No | list of mixin rules; moves members of one of this mod's classes into the target type, see [2.8](#28-mixins-adding-members-to-a-target-type) |

`depends` declares other mods it depends on and required versions; the key is a mod id and the value is a version constraint:

```json
{
  "depends": {
    "othermod": "^1.0.0",
    "another-mod": ">=2.1"
  }
}
```

**Dependencies not written into `depends` carry no version constraint**. Inter-mod dependencies are already inferred automatically from compile-time references (see [6.1](#61-mod-dependency--injecting-the-depended-on-mod)), and `depends` only adds version constraints on top. When the depended-on mod is not on the current side the check is skipped — that case is left to assembly resolution to report.

| Constraint syntax | Meaning |
| --- | --- |
| `*` | any version, equivalent to omitting the entry |
| `1.2.3` | segment prefix; `1.2` matches `1.2` and `1.2.9` but not `1.3` |
| `^1.2.3` | same major version and not less than the baseline; when the major version is 0 the minor version is used instead, so `0.1` and `0.2` count as incompatible |
| `>=1.2.3` | not less than the baseline |

Version numbers take only the leading digits of each segment, so `26.2-netcraft` participates as `26.2`. When versions do not match, **only the declaring mod is skipped** and the rest load as usual; the startup log states "dependency X requires version …, actual version …".

The judgment basis is the `version` field in the depended-on mod's manifest. So **a mod intended to be depended on must set `version` correctly** — an empty version number satisfies no specific constraint.

### 4.3 hook rule fields

| Field | Notes |
| --- | --- |
| `target` | full name of the target type; must be in a kernel assembly or one of the mod assemblies under `mods/` |
| `method` | target method name; same-name overloads all match |
| `type` | injection form, see the appendix |
| `patchMode` | landing method, `ILRewrite` (default) or `RuntimeInject`, see [2.6](#26-runtime-injection-modifying-already-running-code) |
| `ordinal` | when the same anchor matches multiple places in the host method, pick which, 0-based, see [2.4](#24-narrowing-to-one-site-host-scoping-and-placement) |
| `replaceType` | full name of the class containing the replacement method |
| `replaceMethod` | replacement method name |
| `label` | probe label, used only by `Mark` and `Probe` |
| `environment` | the side the rule applies to, default `both`; a rule targeting a server type run on the client has no target at all and is filtered out by `environment` |

The same rule can also be written as an annotation on the replacement method; the correspondence is:

```csharp
[Inject(typeof(SomeType), nameof(SomeType.SomeMethod),
        HookType = "CallSite", Label = "some_label", Environment = "server")]
public static void OnSomeMethod(object self) { }
```

| Manifest field | Annotation form |
| --- | --- |
| `target` | the first constructor parameter, written as `typeof(...)` |
| `method` | the second constructor parameter, preferably `nameof(...)` |
| `type` | named parameter `HookType`, default `CallSite` |
| `patchMode` | named parameter `PatchMode`, default `ILRewrite` |
| `ordinal` | named parameter `Ordinal` |
| `label` | named parameter `Label` |
| `environment` | named parameter `Environment`, default `both` |
| `replaceType` | not written; taken automatically from the class it annotates |
| `replaceMethod` | not written; taken automatically from the method it annotates |

The annotations come from `InjectAttribute` in `NetCraft.ModApi.Extension`, so mods using annotations must reference it. When annotations and the manifest both exist they are merged, and if the same injection point is declared in both **the annotation wins**.

Mixin rules use a different set of fields: `target` (full name of the target type), `source` (full name of the source type, which must be in the mod's own assembly), `interfaces` (optional, an array of interface full names); for semantics see [2.8](#28-mixins-adding-members-to-a-target-type). `[Mixin(typeof(target))]` on the source class is equivalent to the manifest entry.

### 4.4 What can and cannot be hooked

Can be hooked: kernel assemblies under `kernel/`, and other mods under `mods/`.

Cannot be hooked:

- the main library `NetCraft.dll`
- the loader `NetCraft.ModLoader.dll`
- entry assemblies (`NetCraft.ServerExe.dll`, etc.)

Every hook's `target` must be findable in the kernel or in some mod assembly, otherwise assembly reports "the injection target is not in any known assembly". Note that **a namespace does not imply an assembly** — `NetCraft.Game.Server.DedicatedServer` actually lives in `NetCraft.Server.dll`; the loader looks it up by an index built from metadata tables, so just write the full name.

Mod injection of mods follows the same system: the target mod is rewritten **the moment it is itself loaded**, regardless of the order in `mods/`. Cyclic rules (A injects B and B injects A) report "cyclic loading" at load time; such rules have no solution at the IL rewriting level, so just remove one of them.

Note the injected mod **cannot be your own mod** — self-injection also counts as a cycle.

### 4.5 Deployment

Copy the compiled dll into the output directory's `mods/` (top level, no subdirectory recursion) and restart the process.

**This step is already automated by the build**: `DeployModToHosts` in `NetCraft.ModApi.csproj` copies the dll into the `mods/` directory of each host project's output directory after building. The host list is the `ModHostProjects` property; when creating your own host project, just add its name. If no rule takes effect and there is no error at all, first check whether the dll in the host project's `mods/` is stale.

The startup log prints:

```
Rewrote and preloaded assembly NetCraft.Server
Rewrote and preloaded assembly NetCraft.Game
Mod scan finished: 1 found, injection targets NetCraft.Server,NetCraft.Game runtime
Mod init finished: 1 loaded, 0 skipped
```

Troubleshooting order:

- No rule takes effect: first check whether the dll in `mods/` is stale (most common).
- If you do not see `Rewrote and preloaded assembly`, the target assembly was already loaded before the bootstrap, so the rule was written too late.
- If you see `injection targets` but the target is wrong, it is usually a mistyped `target`; follow [4.4](#44-what-can-and-cannot-be-hooked) to check which assembly the type belongs to.
- A rule with `environment` set to `client` is silently skipped on the server (a mismatch at the mod level means the whole mod is not loaded); this is expected behavior, not a fault.

### 4.6 Third-party dependencies

Corresponds to Fabric's Jar-in-Jar.

Projects created from the template **do not need to worry about this**: libraries added via `dotnet add package` are automatically embedded into the mod dll at build time, and when the loader cannot resolve an assembly it looks through the mod's embedded `.dll` resources.

```
dotnet add package Newtonsoft.Json
```

That's all; `ncmod.json` needs no changes.

A hand-written project must copy the `EmbedDependencies` target from the template csproj, or embed it yourself:

```xml
<ItemGroup>
  <EmbeddedResource Include="deps\MyLib.dll" LogicalName="MyLib.dll" />
</ItemGroup>
```

Dependencies are not written into the manifest; resolution only looks at the mod's embedded `.dll` resources, and it just needs the resource name to match the assembly name.

Three notes:

- **Matching is by assembly name**. The loader compares the requested assembly name: after removing `.dll`, the resource name either equals the assembly name or ends with `.` + the assembly name. So both `MyLib.dll` and the default `MyProject.deps.MyLib.dll` work.
- **Only the mod's own embedded resources are recognized**. Libraries not embedded cannot be resolved and will not be searched for elsewhere.
- **Resolution order is kernel first**. `EmbeddedAssemblyLoader` looks among kernel assemblies and embedded sub-libraries first, and only falls back to mods if nothing is found, so mods should not embed assemblies with the same name as kernel ones.

The template's target uses `WithMetadataValue` to filter `.dll` rather than writing `Condition`, because the template engine evaluates `Condition` in `.csproj` at template time, when `%(...)` has no value, and the whole line would be deleted.

### 4.7 Icon and display info

Name, description, authors, links, and icon are all written in `ncmod.json`, corresponding to `name` / `description` / `authors` / `contact` / `icon` in vanilla's `fabric.mod.json`.

**The icon is an embedded resource**, not an external file, using the same resource-naming convention as embedded dependencies:

```xml
<ItemGroup>
  <EmbeddedResource Include="icon.png" LogicalName="icon.png" />
</ItemGroup>
```

```json
{ "icon": "icon.png" }
```

The template already ships an `icon.png` with both places configured; just replace that image. A 64×64 or 128×128 PNG is recommended.

The icon lookup order is: what the manifest's `icon` points to → an embedded resource named `icon.png` → if neither exists, the UI's default image (a gray question mark).

This information is visible on the **MODS** page of the server GUI. On the left is the mod list (small icon + display name + version + status); on the right are the description, credits, links, dependencies, initialization time, and injection rules of the selected one. Mod names in the dependency column are clickable and jump straight to that entry.

Mods that failed to load or were skipped are also in the list, marked in the status column — when diagnosing "why did my mod not take effect", check this column first.

---

## 5. Known limitations

- **Probe signatures may only use BCL types and `object`**, for the reason in 2.2; value-type parameters and return values are exceptions and must keep their real types.
- **`CallSite` is a replacement**, and the replacement method must restore the original call itself, see 2.3. Private methods cannot be restored and require reflection.
- **Dlls under `mods/` are deployed automatically by `DeployModToHosts`**; when creating your own host project, remember to add its name to `ModHostProjects`.
- **Entry assemblies cannot be injected**: if your target and hook fall in an entry assembly, it is ineffective.
- **The main library and loader cannot be hooked**; this is a design constraint preventing mods from changing the loading process itself.
- **Mismatched `environment` means the whole mod is not loaded**, not "some rules fail".
- **Runtime injection requires the native library**: rules using `RuntimeInject` require `lead_hook_native` to be attached at process start, and the loader restarts itself to do so; if the library is not found or the restart fails, this batch of rules is downgraded to a warning and startup is not blocked. For capability boundaries and cost see [2.6](#26-runtime-injection-modifying-already-running-code).
- **Annotations have fewer fields than the C# API**: `InType` / `InMethod` / `Placement` / `LocalIndex` / `ConstantValue` cannot be written in `[Inject]` (`PatchMode` and `Ordinal` are supported), see [2.1](#21-injection-styles-annotations-or-manifest-pick-one).
- **Mixins apply only to load-time rewriting**, and members in the source class are moved rather than copied; nested types and generic methods in the source class are outside coverage, and methods mixed in with an interface are marked virtual. See [2.8](#28-mixins-adding-members-to-a-target-type).
- When testing and debugging, if language tables or model resources are used, an `assets` directory (extracted from the vanilla jar) is required, otherwise the related features degrade to translation keys or placeholder textures.

---

## 6. Known behaviors

This section is a record of observed behavior, not a specification.

### 6.1 Mod dependency + injecting the depended-on mod

**Scenario**: b depends on a and also injects a.

**Conclusion**: it works and does not form a cycle.

The chain has three steps:

1. The rule table is built purely by reading PE metadata, without loading any assembly. The rule "b injects a" requires neither a nor b to be present.
2. `PreloadReplacers` loads all replacement classes (including b) before `ModManager`, at which point a is not loaded yet. `LoadFromStream` reads metadata only and does not JIT method bodies, so b's reference to a is lazy at this moment and loading does not fail.
3. `ModManager` then topologically sorts by `AssemblyRef`, with a before b. When rewriting a, the replacement class b is already in `Default`, so the type is fetched directly and one `AssemblyRef` pointing to b is added to a's metadata. **a does not need to know b exists at all.**

**Hard requirement**: when referencing the injected mod in csproj, you must write `Private="false"`. By default it copies `a.dll` into the output directory, which is then embedded into `b.dll` by `EmbedDependencies` as an embedded dependency, and at runtime `ModLibs` resolving `a` picks up the second copy, causing type identity checks between the two to fail.

**Verification case**: two template projects `NetCraft.Test1` (a) and `NetCraft.Test2` (b); a provides `Test1Api.Greet` and calls it in its own `ModEntry.Server`, while b replaces that call site with `Test2Probe.OnGreet` and also calls `Greet` once in b's `ModEntry.Server` as a control. Actual run log:

```
Mod netcraft.test2 injects NetCraft.Test1!NetCraft.Test1.Test1Api::Greet replaced by NetCraft.Test2.Test2Probe::OnGreet [CallSite/ILRewrite]
Mod scan finished: 2 found, injection targets NetCraft.Server,NetCraft.Game,NetCraft.Storage,NetCraft.Test1 runtime
Test1 internal greeting: Test2-rewritten greeting self    ← injection took effect
Test2 internal greeting: Test1 original greeting test2    ← control group: the rule only rewrites the target assembly, so b's own internal call site is untouched
```

Dependencies derive order only from `AssemblyRef` and carry no version constraint. To constrain versions, declare them in the manifest's `depends`, see [4.2](#42-ncmodjson-fields).

### 6.2 Annotation injection

**Scenario**: `[Inject(typeof(X), nameof(X.M))]` on the replacement method, with no rule in `ncmod.json`.

**Conclusion**: it works and needs no manifest declaration. Annotations and the manifest share one source, are merged at assembly, and the annotation wins for the same injection point.

**Why no declaration is needed**: annotations are just the `CustomAttribute` table in metadata, and the loader already reads the same metadata when scanning mods (manifest, embedded resources, and AssemblyRef — three items), so reading one more table introduces no new loading or timing constraint. The only precondition is that the mod references `NetCraft.ModApi` (the annotations' host).

**The premise is static reading**: reading annotations must go through `MetadataReader` and **must not use `Assembly.Load` + `GetCustomAttributes`** — the latter pulls up the mod assembly just to read rules, and the rewrite window is gone on the spot.

**`typeof` does not constitute a type reference**: what `typeof(X)` compiles into the parameter is the type's serialized name (`full name, assembly, Version=…`), which resolves to just a string and does not require `X` to be present. So writing `typeof(injected-mod)` in an annotation does **not** violate the constraint in 2.2 that "a replacement class must not reference the injected mod's types" — a name appearing in metadata and resolving a type at runtime are two different things.

**Verification case**: `NetCraft.Test2` against two methods of `NetCraft.Test1`; `Greet` goes through the annotation and `Farewell` through the manifest. Both rules are installed and both call sites are replaced:

```
Mod netcraft.test2 injects NetCraft.Test1!NetCraft.Test1.Test1Api::Greet replaced by NetCraft.Test2.Test2Probe::OnGreet [CallSite/ILRewrite]        ← annotation
Mod netcraft.test2 injects NetCraft.Test1!NetCraft.Test1.Test1Api::Farewell replaced by NetCraft.Test2.Test2Probe::OnFarewell [CallSite/ILRewrite]  ← manifest
Test1 internal greeting: Test2-rewritten greeting self
Test1 internal farewell: Test2-rewritten farewell self
Test2 internal greeting: Test1 original greeting test2     ← control group: the rule only rewrites the target assembly
Test2 internal farewell: Test1 original farewell test2     ← control group
```

The same case also incidentally verified that the annotation's named parameter (`Environment = "server"`) is resolved too.

### 6.3 The performance cost of attaching the profiler

**Scenario**: the same pure-computation program (a hundred million modulo-and-accumulate iterations), run once without the profiler and once with it attached (the three `CORECLR_ENABLE_PROFILING` environment variables). Five runs each.

**Conclusion**: no difference in steady state; the cost is entirely at startup.

| | Total process time (5 runs, ms) | In-program computation time |
| --- | --- | --- |
| without | 306 / 260 / 278 / 253 / 300 | 225 ms |
| with | 406 / 370 / 340 / 325 / 354 | 193 ms |

Total time is about 80–110 ms longer. The source is `COR_PRF_DISABLE_ALL_NGEN_IMAGES` — enabling ReJIT requires disabling ReadyToRun images at the same time, so framework code can only go through JIT; the timed loop inside the program is identical once JIT-compiled, showing no difference (the run with the profiler was actually slightly faster, which is noise).

**A waste also fixed along the way**: the initial implementation subscribed to `COR_PRF_MONITOR_JIT_COMPILATION`, a native callback fired after every method compiles, which we never used. After removing it the event mask changed from `0x80040024` to `0x80040004`; the table above is the data after removal.

**Verification case**: `__hookverify/BenchProbe`.

### 6.4 Two mods injecting the same target

**Scenario**: two mods each declare a rule that hits the same target method (`MethodBody` form, different replacement methods).

**Conclusion**: no error, no crash; **the first-assembled rule wins and the later one silently fails**.

Both rules enter the rule table — no cross-mod deduplication is done. When rewriting, the **first** entry in the same-key list for `OriginalType::OriginalMethod` is taken; host scoping behaves the same way, with `InType`/`InMethod` being "the first match wins". Which is first depends on assembly order, and assembly order comes from the enumeration order of the mods directory; **there is no priority field and it cannot be controlled by declaring dependencies** (dependencies affect only the order of `Init()`, not the assembly of injection rules).

**Runtime injection is first-come, first-served too**: a later registration request is sent normally, but `GetReJITParameters` claims by "module + method" and always matches the first request, so the later registration does not land. Measured after a second injection, the target method's behavior stays at the first result.

**One bad rule does not affect the others**: problems like the target type not being in any known assembly or a misspelled injection form are recorded in `ModHooks.Errors` at assembly time and that entry is skipped, while other mods' rules are assembled as usual.

**Conflicts are recorded**: when assembly detects the same injection point declared by multiple mods, the later-assembled one is written to `ModHooks.Warnings` and output in the startup log as `Mod injection conflict ...`, naming which two mods collided and which one will not take effect. It is a warning, not an error, does not affect loading, and the rule itself remains in the table (just unreachable).

**Runtime injection goes through this check too**: the conflict detection key includes `patchMode`, so one mod writing `ILRewrite` and another writing `RuntimeInject` does not count as a collision (two independent paths, each doing its own thing); only two of the same mode are judged conflicting and warned. Its actual landing is likewise first-come, first-served — `GetReJITParameters` claims by "module + method", matching the first request, so later ones are sent but do not land.

**The one hard crash point**: when two mods both patch the same method in `RuntimePatch` mode, the second hits `RuntimeHookEngine`'s duplicate-registration check and throws `InvalidOperationException`, and this path is not caught, so startup fails outright. `RuntimePatch` is on its way out (see [2.6](#26-runtime-injection-modifying-already-running-code)); do not use it in new rules.

**Verification case**: the `modinjection` module of `NetCraft.Test`, entries `same anchor first mod wins quietly` and `one bad rule does not sink the rest`.

### 6.5 Not yet verified

- **Runtime injection working on a real server**: `RuntimeInject` mode has been verified end-to-end in `__hookverify/RuntimeProbe` (after registration the target method's behavior is swapped to the replacement's), and the mod assembly side also has test coverage for routing and downgrade; but no build step currently places `lead_hook_native` into NC's run directory, so running this chain on a real server requires first putting the library in the program root (or pointing to it with `NC_PROFILER_PATH`). This step is not done.
- **A replacement class referencing the injected mod's types**: by reasoning, at the moment of rewriting a it would resolve a, while a is stuck just before load completion (it is not yet in `Default`, and neither `ModLibs` nor the kernel resolution callback recognizes mod assemblies), so `PrepareMod` is expected to throw and record into `result.Errors`. Not yet actually run. Note 6.2 only proves that **`typeof` in an annotation** is not a reference; **the type appearing in a method signature** is another matter.

---

## Appendix: HookType overview

| Form | Effect | Requirement on the replacement method signature |
| --- | --- | --- |
| `CallSite` | replace call sites to the target method with your method | parameter count matches the called method (instance call +1) |
| `MethodBody` | replace the entire target method body | matches the replaced method |
| `NewObj` | replace `new X(...)` | parameter count matches the constructor |
| `FieldRead` | instrument field reads | by the read type |
| `FieldWrite` | instrument field writes | by the write type |
| `TypeCheck` | instrument `isinst` / `castclass` | by the checked type |
| `Box` | instrument boxing/unboxing | by the element type |
| `FunctionPointer` | instrument function pointer loads | by the delegate type |
| `LocalRead` | instrument local variable reads | zero parameters, returns the variable's value |
| `LocalWrite` | instrument local variable writes | one parameter, receives the written value |
| `Constant` | instrument constant loads | zero parameters, returns the constant's value |
| `Probe` | keep the original method body, instrumenting the entry and every exit; with `LabelArgumentIndex`, one argument can be folded into the label | `Begin()` returns long, `End(string, long)` |
| `Mark` | report once at method entry only, without timing | `void method(string label)` |

`Probe` and `Mark` pass only the label text (`Probe` can also include one argument's `ToString()`); they cannot obtain object references. To get actual arguments, use `CallSite`.

`InType`/`InMethod`, `Placement`, and `Ordinal` (see [2.4](#24-narrowing-to-one-site-host-scoping-and-placement)) are meaningful only for instruction-level forms: the ten entries in the table above other than `MethodBody`, `Probe`, and `Mark` can choose replace or insert-before/after, and can use `Ordinal` to pick a single occurrence; `MethodBody` always replaces the whole thing, and `Probe`/`Mark` ignore these parameters.

For the three kinds `LocalRead` / `LocalWrite` / `Constant`, the host method is written in `target` rather than a referenced entity, and `localIndex` or `constantValue` is additionally required; see [2.5](#25-in-method-body-anchors-local-variables-and-constants).
