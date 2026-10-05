# NetCraft.Resources

The resource-pack subsystem. It maps to the original `net.minecraft.server.packs` and `net.minecraft.server.packs.resources` packages, turning packs of files on disk, in archives or embedded in the build into a single addressable resource view.

## Features

- Models a pack's metadata — id, title, description, priority and content source — parsed from JSON sections alongside language metadata.
- Reads resources as streams from folders, zip archives or the built-in vanilla tree, keyed by pack type (client resources or server data) and `namespace:path`. Each call opens a fresh stream, so callers own the returned stream.
- Merges packs by priority so the highest-priority pack wins, with probing, listing and namespace enumeration across all packs.
- Models a single resolved resource together with the pack it came from.
- Serves reloads through preparable reload listeners and a synchronous reload runner.
- Loads registry data from `data/<namespace>/<registry>/<element>.json`, decoding each element through its codec with cross-registry reference resolution and aggregated error reporting. Loading never aborts on one bad element: elements are retried until a full pass makes no progress, and only then are the remaining failures reported, preserving the original's tolerance for element-to-element references.
- Provides a read-only virtual filesystem that mounts several real directories into one tree, matching the original link filesystem used for pack access.
