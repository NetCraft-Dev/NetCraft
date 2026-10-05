# NetCraft

The kernel entry point and main aggregation library. It maps to the original `net.minecraft.Bootstrap` and `net.minecraft.server.Main` bootstrap path, and it is the assembly that ships the other kernel libraries embedded as resources.

## Features

- `NetCraftKernel.Initialize` boots the runtime: enables debug mode, sets log levels, installs the embedded assembly resolver, loads the language table, prints the banner and parses launch arguments. The call is idempotent.
- Embeds each sub-library as an assembly resource (`NetCraft.Embedded.*.dll`) and resolves it on demand through `AssemblyLoadContext.Resolving`, so a deployment can be a single assembly. Assemblies that reference this one — game, server, client, GPU — cannot be embedded and are placed in a `kernel/` subdirectory instead.
- Exposes a byte rewriter hook on the resolver, which the mod loader uses to rewrite assembly bytes before they are loaded.
- Parses launch arguments in an event-driven way: the kernel consumes the flags it has declared itself and broadcasts every unrecognized token through `LaunchOptions.UnhandledArgument` for the business layers to consume.
- Loads NetCraft's own language files from `lang/` at the earliest startup stage, independently of resource packs, falling back to the default language when the requested code is unavailable.
- Bootstraps the built-in registries and loads the built-in tag files into the tag manager.
- Holds the client and server configuration surface (`GameConfig`, `ServerSettings`, `PropertiesConfig`, `Settings`) and filesystem path resolution (`AppPaths`).
