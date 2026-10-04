# NetCraft-ModApi Reference

`NetCraft.ModApi` is the API surface NetCraft exposes to mods. It has two identities: for you it is an API library; for itself it is an ordinary mod (`id` is `netcraft-modapi`, shipping its own `ncmod.json` and injection probes).

This file grows as the API grows. For architectural background, differences from Fabric, and how to write a mod, see [modding-guide.md](modding-guide.md).

- Assembly: `NetCraft.ModApi.dll`

- Dependencies: `NetCraft` (the main library), `NetCraft.Game`

The public surface is split into three namespaces:

| Namespace | Contents | Notes |
| --- | --- | --- |
| `NetCraft.ModApi.Wrapper` | Event and subscription base class `NcEvent<T>`, `Nc*` facades, `Nc*` object handles | Wrapper layer; no kernel types on the public surface |
| `NetCraft.ModApi.Extension` | `[Inject]` / `[Mixin]` annotations | Extension points; rules bind to kernel class and method names |
| `NetCraft.ModApi.Internal` | Injection probes | Do not reference directly |

The root namespace `NetCraft.ModApi` contains only the entry class `ModApiEntry`. `Wrapper` and `Extension` are two parallel routes; for how to choose, see [modding-guide.md 2.9](modding-guide.md#29-two-routes-wrapper-layer-and-extension-points).

***

## 1. Quick start

```csharp
using NetCraft.ModApi.Wrapper;

public sealed class MyModEntry
{
    public Task Init()
    {
        //subscription returns a handle; disposing it unregisters
        var handle = ServerEvents.Tick.Subscribe(args =>
            Log.Info($"tick {args.TickCount}"));

        ServerEvents.CommandRegister.Subscribe(args =>
            args.Register("mymod", "my mod command", builder =>
                builder.Executes(context =>
                {
                    context.GetSource().SendSuccess("hi");
                    handle.Dispose();          //you can also do something else with the handle
                    return 1;
                })));

        return Task.CompletedTask;
    }
}
```

In `ncmod.json`, point `entry` at this class and leave `hooks` empty — the events below are all provided by ModApi's own probes.

***

## 2. Events

All events live under `NetCraft.ModApi.Wrapper`; after `using NetCraft.ModApi.Wrapper;` they are available.

### 2.1 Summary table

| Event                           | Args type              | Trigger                                                   | Side   | ModApi hook point                                        |
| ------------------------------- | ---------------------- | --------------------------------------------------------- | ------ | -------------------------------------------------------- |
| `ServerEvents.Tick`             | `ServerTickArgs`       | every tick of the server main loop                        | server | `DedicatedServer::Tick` (Mark)                           |
| `ServerEvents.Started`          | `ServerPhaseArgs`      | main loop started, after `Done (x.xxxs)!` is printed      | server | `MinecraftServer::Run` (Mark)                            |
| `ServerEvents.Stopping`         | `ServerPhaseArgs`      | server begins shutting down; players are about to be disconnected | server | `DedicatedServer::Stop` (Mark)                    |
| `ServerEvents.CommandRegister`  | `CommandRegisterArgs`  | all built-in commands have been registered                | server | `EffectCommand::Register` call site (CallSite)           |
| `ServerEvents.PlayerJoin`       | `PlayerJoinArgs`       | the join packet sequence has been sent                    | server | `PlayerList::PlaceNewPlayer` call site (CallSite)        |
| `ServerEvents.PlayerLeave`      | `PlayerLeaveArgs`      | player removed from the online list                       | server | `PlayerList::RemovePlayer` call site (CallSite)          |
| `ServerEvents.PlayerDisconnect` | `PlayerDisconnectArgs` | disconnect packet sent and connection closed              | server | `ServerPlayer::Disconnect` call site (CallSite)          |
| `ServerEvents.PlayerHurt`       | `PlayerHurtArgs`       | damage actually dealt                                     | server | `PlayerList::HurtPlayer` call site (CallSite)            |
| `ServerEvents.PlayerDeath`      | `PlayerDeathArgs`      | immediately after health resets to zero                   | server | `PlayerList::RespawnPlayer` call site (CallSite)         |
| `ServerEvents.PlayerChat`       | `PlayerChatArgs`       | after the chat broadcast                                  | server | `ServerGamePacketListenerImpl::HandleChat` call site (CallSite) |
| `ServerEvents.ChunkLoaded`      | `ChunkLoadedArgs`      | chunk enters memory for the first time                    | server | `ServerChunkCache::set_ChunkLoaded` assignment site (CallSite) |
| `ServerEvents.ChunkUnloaded`    | `ChunkUnloadedArgs`    | chunk leaves memory                                       | server | `ServerChunkCache::set_ChunkUnloaded` assignment site (CallSite) |
| `ServerEvents.ChunkSaved`       | `ChunkSavedArgs`       | snapshot taken before the chunk is written to disk        | server | `ServerChunkCache::set_ChunkSaveSink` assignment site (CallSite) |
| `ServerEvents.CommandExecuted`  | `CommandExecutedArgs`  | a command finished running; syntax errors and permission denials count too | server | `CommandManager::Execute` (CallSite) |
| `ServerEvents.LevelTick`        | `LevelTickArgs`        | level tick, once per loaded level per tick                | server | `PersistentServerLevel::Tick` (CallSite)                 |
| `ServerEvents.SavedDataSaving`  | `SavedDataSavingArgs`  | saved data written to disk, one step later than chunk saving | server | `SavedDataStorage::ScheduleSave` (CallSite)          |
| `ServerEvents.BlockChanged`     | `BlockChangedArgs`     | block state changed, about to sync to clients             | server | `IBlockUpdateSink::BlockChanged` (CallSite)              |
| `ServerEvents.BlockBroken`      | `BlockBrokenArgs`      | block broken; player mining and redstone self-destruction both count | server | `ServerBlockUpdates::BreakBlock` (CallSite) |
| `ServerEvents.ItemDropped`      | `ItemDroppedArgs`      | dropped item entity spawned, including block-break drops and cooking products | server | `ServerBlockUpdates::SpawnDrop` (CallSite) |
| `NetworkEvents.PacketReceived`  | `PacketReceivedArgs`   | every inbound packet queued to a handler, including handshake and status phases | both   | `PacketProcessor::ScheduleIfPossible` and `HandleNow` (CallSite) |
| `ClientEvents.Tick`             | `ClientTickArgs`       | every tick of the client main loop                        | client | `MinecraftClient::Tick` (Mark)                           |

Ordering of player events: death is nested inside the hurt flow, so `PlayerDeath` precedes the corresponding `PlayerHurt`; `PlayerLeave` and `PlayerDisconnect` are two different things — the former means removal from the online list (even after `/kick` it only fires once the connection drops), the latter means the connection drop itself, and the two are not guaranteed to appear in pairs.

### 2.2 Subscribing and unregistering

```csharp
IDisposable Subscribe(Action<T> handler)
```

- Subscribing to the same event multiple times delivers notifications in subscription order.

- Dispatch takes a snapshot of the callback list, so subscribing or unregistering from inside a callback does not affect the current dispatch.

- Without unregistering it stays effective forever; mods provide no unload mechanism, so manual unregistration is usually unnecessary.

### 2.3 Args types

**`ServerTickArgs`**

| Property    | Type   | Notes                                |
| ----------- | ------ | ------------------------------------ |
| `TickCount` | `long` | tick count since this launch, starting at 1 |

Note this is counted by ModApi itself, not the kernel's `TickCount`.

**`ClientTickArgs`**

| Property    | Type   | Notes                           |
| ----------- | ------ | ------------------------------- |
| `TickCount` | `long` | same as above, counted independently on the client side |

**`ServerPhaseArgs`**

| Property | Type     | Notes                        |
| -------- | -------- | ---------------------------- |
| `Phase`  | `string` | phase name, `started` or `stopping` |

The field duplicates the event itself; it is kept so logging can use one uniform format.

**`CommandRegisterArgs`**

| Member                               | Type                                    | Notes            |
| ------------------------------------ | --------------------------------------- | ---------------- |
| `Dispatcher`                         | `CommandDispatcher<CommandSourceStack>` | the kernel's command dispatcher |
| `Register(name, description, build)` | method                                  | register a command and record it in the ledger, see 3.1 |

**`PlayerJoinArgs`**

| Property      | Type           | Notes                                          |
| ------------- | -------------- | ---------------------------------------------- |
| `Player`      | `ServerPlayer` | the player who just joined; join packets already sent, state is safe to read |
| `ProfileName` | `string`       | player name                                    |

**`PlayerLeaveArgs`**

| Property  | Type           | Notes                                 |
| --------- | -------------- | ------------------------------------- |
| `Player`  | `ServerPlayer` | the leaving player, no longer in the online list at this point |
| `Removed` | `bool`         | whether actually removed; `false` on repeated removal |

**`PlayerDisconnectArgs`**

| Property | Type           | Notes                                 |
| -------- | -------------- | ------------------------------------- |
| `Player` | `ServerPlayer` | the disconnected player               |
| `Reason` | `string`       | disconnect reason; plain text when given as a component |

The disconnect packet has been sent and the connection closed; sending packets to this player now has no effect.

**`PlayerHurtArgs`**

| Property   | Type            | Notes                                        |
| ---------- | --------------- | -------------------------------------------- |
| `Player`   | `ServerPlayer`  | the player who was hurt                      |
| `Attacker` | `ServerPlayer?` | the player who dealt the damage; `null` for environmental and command damage |
| `Amount`   | `float`         | damage amount this time                      |

Does not fire during invulnerability frames or after death (the kernel's `Hurt` returns `false`).

**`PlayerDeathArgs`**

| Property   | Type            | Notes                          |
| ---------- | --------------- | ------------------------------ |
| `Player`   | `ServerPlayer`  | the player who died            |
| `Attacker` | `ServerPlayer?` | the killer; `null` when there is none |

The kernel resets immediately after health reaches zero, so when the event fires the player is already at full health at the respawn point; the coordinates and drops at the moment of death are not available.

**`PlayerChatArgs`**

| Property     | Type     | Notes                |
| ------------ | -------- | -------------------- |
| `SenderName` | `string` | sender name          |
| `Message`    | `string` | plain-text message   |

This event is a **read-only notification**: the original method has already broadcast the message, so changing `Message` here has no effect.

**`ChunkLoadedArgs`** **/** **`ChunkUnloadedArgs`** **/** **`ChunkSavedArgs`**

The three events share the same args shape:

| Property | Type  | Notes              |
| -------- | ----- | ------------------ |
| `X`      | `int` | chunk coordinate X |
| `Z`      | `int` | chunk coordinate Z |

The easiest one to get wrong is `ChunkSaved`: the kernel requires this callback to **take the snapshot synchronously**, while serialization and disk writes are done asynchronously by the kernel itself. So time-consuming work in this callback directly slows down chunk unloading, and destructive operations (removing blocks, changing inventories) should not go here either — it only promises the snapshot moment.

When `ChunkUnloaded` fires the block entities have already been cleaned up along with the chunk; if you want to read blocks, use `ChunkSaved` (which cannot access blocks either) or an earlier point.

**`CommandExecutedArgs`**

| Property  | Type                   | Notes                                 |
| --------- | ---------------------- | ------------------------------------- |
| `Command` | `string`               | raw command text; chat commands carry no leading slash |
| `Result`  | `int`                  | command return value; 0 means failure or denial |
| `Source`  | `CommandSourceStack?`  | command source; `null` on the player-overload path |
| `Player`  | `ServerPlayer?`        | the player who issued the command; `null` when issued from the console |

The event fires **after** the command finishes; it cannot change execution. Syntax errors and permission denials also come through here; use `Result` to tell them apart. Commands a player sends from the chat bar go through the `Execute(ServerPlayer, string)` overload, where the kernel builds the command source internally, so in that case `Source` is `null` and only `Player` is set.

**`LevelTickArgs`**

| Property       | Type                    | Notes                                        |
| -------------- | ----------------------- | -------------------------------------------- |
| `Level`        | `NcLevel`               | the level advanced this tick                 |
| `RunsNormally` | `bool`                  | whether it advanced normally; `false` during `/tick freeze` |

Fires once per loaded level per tick, so a multi-level world receives several per tick. The trigger point is after the level tick **has completed**; it is an observation point, not an interception point.

**`SavedDataSavingArgs`**

| Property  | Type                | Notes                       |
| --------- | ------------------- | --------------------------- |
| `Storage` | `SavedDataStorage`  | the saved-data table being persisted |

This is a different path from `ServerEvents.ChunkSaved`: the chunk one only takes a snapshot and writes asynchronously, whereas this one fires after a synchronous write completes. World clock, game rules, and world border data go through it.

**`BlockChangedArgs`**

| Property | Type         | Notes                       |
| -------- | ------------ | --------------------------- |
| `Pos`    | `BlockPos`   | position of the changed block |
| `State`  | `BlockState` | block state after the change  |

The state has already been written to the chunk and is about to sync to clients, so the change itself cannot be modified here. Redstone components changing their own state in behavior callbacks also exit through this path; it is high-frequency, so do not do time-consuming work in the callback.

**`BlockBrokenArgs`**

| Property | Type            | Notes                                      |
| -------- | --------------- | ------------------------------------------ |
| `Pos`    | `BlockPos`      | position of the broken block                |
| `Player` | `ServerPlayer?` | the breaker; `null` for non-player causes such as redstone |

Fires only when the block is actually replaced; empty positions and rejected breaks do not trigger it. Break effects and drops have already been handled, so what you read in the event is the result.

**`ItemDroppedArgs`**

| Property | Type        | Notes                   |
| -------- | ----------- | ----------------------- |
| `Pos`    | `BlockPos`  | where the dropped item appeared |
| `Stack`  | `ItemStack` | the dropped item stack   |

Block-break drops and campfire cooking products both go through here. An empty item stack spawns no entity, so there is no event.

**`PacketReceivedArgs`**

| Property        | Type     | Notes                    |
| --------------- | -------- | ------------------------ |
| `Listener`      | `object` | the listener receiving this packet |
| `Packet`        | `object` | the packet object itself  |
| `IsServerbound` | `bool`   | whether it is a serverbound packet |

Fires for every inbound packet, covering all four phases: handshake, status, configuration, and play. Movement packets arrive several times per tick, so do not do time-consuming work in the callback. The packet is already decoded into an object but has not entered the business layer; to distinguish types, inspect `Packet` yourself. Outbound packets are outside this event's scope.

***

## 3. Extension points

### 3.1 Command registration

The timing is `ServerEvents.CommandRegister`. Do not cache this event's args; the internal command tree is built only once at startup.

```csharp
public void Register(
    string name,                                          //command literal, without the slash
    string description,                                   //one-line description, shown in the /ncmapi ledger
    Action<LiteralArgumentBuilder<CommandSourceStack>> build);   //attach arguments and the executor
```

The `build` you receive is the kernel's brigadier builder; write arguments, subcommands, and permission predicates the kernel's way:

```csharp
ServerEvents.CommandRegister.Subscribe(args =>
    args.Register("tpall", "teleport all players to the executor", builder =>
        builder
            .Requires(s => s.HasPermission(2))            //permission predicate
            .Executes(context => { /* ... */ return 1; })));
```

With arguments:

```csharp
args.Register("heal", "heal the target players", builder =>
    builder.Requires(s => s.HasPermission(2))
        .Then(RequiredArgumentBuilder<CommandSourceStack, EntitySelector>
            .Argument("targets", EntityArgument.Players())
            .Executes(context =>
            {
                foreach (var player in EntityArgument.GetPlayers(context, "targets"))
                    player.Heal(20f);                     //illustrative
                return 1;
            })));
```

Key points:

- Calling `args.Dispatcher.Register(...)` directly also installs a command, but it does not enter the ledger and `/ncmapi` will not show it. Use `args.Register` if you want it listed.

- Commands have no permission restriction by default; add `.Requires(...)` yourself if needed.

- The behavior at execution time is entirely up to you; ModApi does not intercept it.

### 3.2 Viewing registered commands

There is a built-in `/ncmapi`, requiring permission level 2:

```
/ncmapi
Commands registered via NetCraft-ModApi: 2 total
/ncmapi  list commands registered via NetCraft-ModApi
  /ncmapi
/tpall  teleport all players to the executor
  /tpall
/heal  heal the target players
  /heal <targets>
```

Usage lines are computed on the fly from the command tree's node structure: literals are written by name, arguments are wrapped in angle brackets, and intermediate nodes that are themselves executable get their own line too.

***

## 4. Server facades

The facades in this chapter all live under `NetCraft.ModApi.Wrapper`; after `using NetCraft.ModApi.Wrapper;` they are available.

Facades are `Nc*` static classes that gather capabilities scattered across the kernel into a few entry points. The kernel instance is captured by a probe when the main loop starts; when `NcServer.IsAvailable` is false everything below throws — use them only inside event callbacks, not from `Init`.

| Facade | Purpose |
| --- | --- |
| `NcServer` | server instance, tick rate, commands, entity tracking, player data, game rules, broadcast, command execution |
| `NcPlayers` | online player queries and operations (kick, teleport, health, game mode, permissions) |
| `NcWorld` | overworld block read/write and breaking, weather, time, border, clock, sounds, level events; takes `NcLevel` handles to reach other dimensions, coordinates are plain `x y z` ints |
| `NcRegistries` | built-in registries looked up by name (blocks, items, fluids, effects, biomes, particles, entities, block entities) |
| `NcRecipes` | recipe queries (grid crafting, stonecutting, cooking; fetch recipes by id) |
| `NcLists` | lists and config (whitelist, ops, bans, `server.properties`) |
| `NcStartup` | startup arguments (kernel-unrecognized tokens and name-based subscription) |

`NcPlayer` is not a static facade but an **object handle**: `NcPlayers.All` / `Find` return it, and `Player` / `Attacker` in player events are also it. Handles are read-only and constructed by probes; mods cannot get the kernel's `ServerPlayer` — the first anchor of "no kernel types on the public surface". The same kernel player always maps to the same handle, cached internally by weak reference and automatically invalidated once the player logs off.

`NcLevel` follows the same shape for levels. `NcWorld.Overworld` / `Nether` / `End` and `NcWorld.Get("minecraft:the_nether")` return it, and `LevelTickArgs.Level` is one too. It carries the dimension id, time, weather, build height, tick count, and chunk force-loading; block operations stay on `NcWorld` and take the handle plus `x y z`. `BlockPos` never shows up, so a mod's dll carries no reference to the kernel level type.

### 4.1 Registries

`NcRegistries` provides both whole tables and lookups by name. Whole tables are for iteration and tag-based lookup; lookups by name are for getting a single element:

```csharp
var stone = NcRegistries.FindState("minecraft:stone");     //block default state
var diamond = NcRegistries.FindItem("minecraft:diamond");
var over = NcRegistries.FindBiome("minecraft:plains");

//iterate the whole table
foreach (var id in NcRegistries.Blocks.KeySet)
    Log.Info(id.ToString());
```

Registries are assembled gradually during startup, and mods load before assembly completes, so do not cache anything looked up in `Init` — assembly is still ongoing, and a cached value will be a null reference or a stale value. Currently `BuiltInRegistries.BootStrap` is still an empty implementation; each registry is populated separately by its own Bootstrap, and the data-driven ones (biomes, recipes, etc.) have very few entries before data pack loading is wired up.

### 4.2 Recipes

`NcRecipes` is backed by a recipe table loaded from data packs; `/reload` replaces the whole table, so do not hold a `RecipeHolder` across reloads.

```csharp
if (NcRecipes.IsAvailable)
{
    var result = NcRecipes.Craft(input);                    //compute the output for a crafting grid
    var recipes = NcRecipes.StonecuttingFor(stack);         //stonecutting recipes available for this input
    var smelting = NcRecipes.CookingFor("smelting", stack); //look up by cooking type
    var byId = NcRecipes.Find("minecraft:oak_planks");      //fetch a recipe by id
}
```

***

## 5. Internals

You do not need this section to write mods, but it may help when debugging.

### 5.1 Probes

| Class                                                    | Form        | Responsibility                                              |
| -------------------------------------------------------- | ----------- | ----------------------------------------------------------- |
| `Internal.SignalProbe.OnSignal(string)`                  | Mark ×4     | all "something happened" signals funnel into one method, dispatched to the matching event by `label` |
| `Internal.CommandProbe.OnCommandsReady(object)`          | CallSite    | replaces the call to `EffectCommand::Register`; after restoring the original call it fires `CommandRegister` |
| `Internal.PlayerProbe.OnXxx(object, ...)`                | CallSite ×6 | player events, one method per hook point; after restoring the original call it publishes the event |
| `Internal.LevelProbe.OnChunkXxxAssigned(object, object)` | CallSite ×3 | chunk events, hooked at the assignment sites of `ServerChunkCache`'s three callback properties; a wrapper delegate is layered on before handing control back to the kernel |
| `Internal.BlockProbe.OnXxx(...)`                         | CallSite ×3 | block events; breaking and drops hook `ServerBlockUpdates`, state changes hook the interface method on `IBlockUpdateSink` |

`SignalProbe`'s signature takes only `string`, and the parameters of `CommandProbe`, `PlayerProbe`, and `LevelProbe` are declared as `object` — this is deliberate: during assembly `Lead.Hook` resolves the replacement method's signature, and once a kernel type appears in it, resolving it pulls up the kernel assembly early and injection misses its window. Kernel types appear only inside method bodies, by which time the code is already running.

The only things that cannot be `object` in a signature are value-type parameters and return values: `object` is a reference on the stack while `float`/`bool` are values, and a mismatch is invalid IL. So `PlayerProbe.OnHurtPlayer` keeps `float` for the damage amount, and `OnRemovePlayer` and `OnHurtPlayer` keep `bool` return values.

`BlockProbe` is an extension of this constraint: block position and state are the two value types `BlockPos`/`BlockState`, which can only be written into the signature as themselves. These two types come from `NetCraft.Primitives` and `NetCraft.Registry`, neither of which is on the injection list, so resolving them during assembly does not pull up the assemblies to be rewritten early.

### 5.2 Hook point list

ModApi's `ncmod.json` contains twenty-four rules, matching the table in 2.1 one-to-one. To change a hook point or add a rule, edit this file; after editing, rebuild (it is an embedded resource) and put the resulting dll back into `mods/` — the latter is already done automatically by `DeployModToHosts` in `NetCraft.ModApi.csproj`, and missing it manifests as the rules not taking effect at all.

`CommandManager::Execute` has two overloads that share one rule. `Lead.Hook`'s CallSite matches call sites by "type + method name", not by parameter list, and both overloads take two parameters, so the probe can take `object` for the first parameter and dispatch by the real type.

The two `PacketProcessor` rules are complementary: play-phase packets go through `ScheduleIfPossible` into the main-thread queue, while handshake and status phases go through `HandleNow` for immediate handling; any given packet passes through only one of them. Hooking only the former misses the handshake and status phases — which happen to be the easiest to probe with scripts, so during debugging this is easily misread as "the rule did not take effect".

The three chunk rules hook the **assignment sites** of `ServerChunkCache`'s three callback properties, not the read sites. The reason is that those three properties are unicast and already occupied by the kernel itself when `PersistentServerLevel` is constructed (they inject save and block-entity cleanup logic); a mod assigning directly would override the kernel's copy — unloads not persisted, block entities not cleaned up, and with no error at all. Hooking the assignment site lets the kernel's callback and the probe be chained at that moment; the assignment happens only once, and each subsequent trigger adds one layer of delegate forwarding.

`PlayerList::RespawnPlayer` is private, so the probe cannot restore the original call; that one goes through reflection (called once per death, so the overhead is negligible). This also leaves a door open for kernel alignment: if `InternalsVisibleTo` is added for it in the future, it can be swapped to a direct call.

### 5.3 Ledger

`Internal.NcCommandRegistry` records commands registered through `args.Register`. It is only a ledger and does not take part in command execution; the commands themselves are installed on the kernel dispatcher, so even if the ledger has problems, the commands still work.

***

## 6. To be added

The following are hook points whose locations are confirmed but that have not yet become events (the list was produced by `__scan_mod_api.py` at the repository root):

| Direction         | Candidate hook points                                                        |
| ----------------- | ---------------------------------------------------------------------------- |
| Entities          | `ClientLevel::AddEntity`, `Entity::Die`                                      |
| World             | level load and unload, `ServerChunkCache` chunk batching                     |
| Terrain generation | `ChunkGenerator::Generate` stages per `ChunkStatus`, `WorldGenRegion::SetBlockState` |
| Command execution | `CommandSourceStack::SendSuccess` / `SendFailure` (the response half, with many call sites) |
| Network           | per-packet-type `ServerGamePacketListenerImpl::HandleXxx` (currently only a unified entry point) |

Directions already done: level ticks became `ServerEvents.LevelTick`, saved data persistence became `ServerEvents.SavedDataSaving`, command execution became `ServerEvents.CommandExecuted`, and blocks became `ServerEvents.BlockChanged` / `BlockBroken` / `ItemDropped`.

Hooking the block cell on `SetBlock` does not work: it has two default parameters, `notifyNeighbors` and `strict`, so compiled call sites take anywhere from 4 to 6 parameters, and since CallSite matches by "type + method name" without looking at the parameter list, one replacement method cannot handle all three stack shapes. Instead it hooks two places: state sync hooks `IBlockUpdateSink::BlockChanged` (the only interface call in `ServerLevel.SetBlock`, covering every change with client sync), and breaking and drops hook `ServerBlockUpdates`' own methods.

The entities cell is troublesome because `Entity` is defined in `NetCraft.Registry`, which is not on the injection list, so call sites that target it cannot be rewritten. `ClientLevel::AddEntity` is in the client assembly and is doable; a death event first requires resolving whether `Registry` can be rewritten.

For the network cell, `HandleChat` has long existed and `NetworkEvents.PacketReceived` provides a unified entry point, so per-`HandleXxx` hooks are much less valuable; only scenarios needing fine-grained filtering by packet type are worth adding.

Level load and unload have no convergence point on the NC side: `DedicatedServer::CreateLevel` is private, so the original call can only be restored by reflection like `PlayerList::RespawnPlayer`; the unload path is even more scattered. To do it, first settle what the event args should be.

All of the remaining ones need object references (entity instances, etc.), so they must use `CallSite` rather than `Mark`; if a kernel value type appears among the parameters, it can only be written into the replacement method's signature as itself.

The interface-surface list (`__modapi_api.txt`, produced by `__scan_mod_api.py --api`) was also reviewed: entries qualified as capability entry points were gathered into the chapter 4 facades by domain, and the rest that are not exposed fall into three categories — protocol and packet handling (`Network.Protocol.*`), rendering and models (`Client.Render.*`), and terrain generation and density functions (`LevelGen.*`). These are kernel internals; using them directly would tie mods to implementation details, so a stable interface should first be opened in the kernel.

On the server side there are two more things not wrapped as facades: the `ReloadableServerResources` instance hangs off `DedicatedServer`, and since ModApi does not reference `NetCraft.Server`, wrapping it requires first opening a property on the kernel base class; `ChunkSender` and `ServerWorldBorderListener` are internal flows with no use case for mods.
