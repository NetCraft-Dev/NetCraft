# NetCraft.Storage

The world storage subsystem. It maps to the original `net.minecraft.world.level.chunk.storage`, `net.minecraft.server.level` and `net.minecraft.world.level.lighting` packages, and owns how chunk data is persisted, scheduled, lit and abstracted as a server level.

## Features

- Persists chunks into MCA region files: sector allocation, external `.mcc` overflow streams, big-endian chunk framing and version-tagged payloads.
- Drives asynchronous chunk IO through a worker that serialises region access, coalesces queued stores per position and exposes load, store and bulk-scan operations. Repeated stores for the same position are merged so only the latest snapshot reaches disk.
- Converts chunk contents to and from NBT through a serialisation representation covering sections, heightmaps, scheduled ticks, entities, block entities, carving masks and structure data.
- Stores block state and biome data compactly with palette containers over bit storage.
- Schedules block and fluid ticks at chunk and level scope, with per-tick collection budgets.
- Computes and propagates block light and sky light across chunks, including the per-section light storage and its delivery hooks.
- Tracks chunk load and simulation levels through tickets, holders, distance propagation and loaders.
- Abstracts a server level: dimension identity, level data, weather state, world border, entity, POI and saved-data managers.
- Game-layer side effects enter through injected bridges rather than direct references — block update sinks, block entity and structure data bridges, and the light update callback — so this module never references `NetCraft.Game`.
