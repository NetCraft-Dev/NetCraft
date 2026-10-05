# NetCraft.Tags

The data-tag subsystem. It maps to the original `net.minecraft.tags` package and turns the JSON tag files shipped in data packs into bound registry tags.

## Features

- Parses a tag file — a `replace` flag plus a `values` list — together with its entries.
- Models a single entry as either an element reference or a nested tag reference, each optionally required, using the original string prefix encoding (`#tag`, `!tag`, `!element`, `element`).
- Loads every tag file under a `tags/<category>` directory and builds the id-to-elements mapping, honouring the `replace` flag and resolving nested tag references.
- Acts as the global manager holding one loader per registry. On binding it converts the built tag sets into `TagKey`-to-`Holder` bindings on the target registry; loaders are keyed by `Identifier` and bound through a generic closure, since C# cannot treat a `TagLoader<Block>` as a `TagLoader<object>` the way the original's wildcard generics do.
- Tag files are JSON, so this module depends only on `NetCraft.Util` and `NetCraft.Registry` — NBT is not involved.
