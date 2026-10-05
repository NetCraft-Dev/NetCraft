# NetCraft.Registry

The registry and game-content model. It maps to the original `net.minecraft.core` package (identifiers, resource keys, holders, registries, component maps), together with the block-state model from `net.minecraft.world.level.block.state` and the core content types those registries hold.

## Features

- `Identifier` — the `namespace:path` value type used to name registries, resources and content.
- `ResourceKey<T>` — typed, interned keys into a named registry. Interning goes through a per-closed-generic concurrent pool rather than the original's wildcard pool plus unchecked casts.
- `Holder<T>` and `HolderSet<T>` — the two-phase binding of registry values, with direct and reference holders and tag-backed or list-backed sets. Registration creates standalone holders and freezing binds them to their values; any write after freezing is rejected.
- Registry implementations over id, location, key and value maps, with lifecycle tracking, freezing into an immutable lookup and default-value variants.
- `RegistryAccess` and `RegistryOps` — the registry snapshot passed to codecs and the registry-aware dynamic ops that resolve cross-registry references.
- Data component maps, context parameter maps and feature flag sets.
- The block-state model: `BlockState` as a lightweight id, the state holder and definition, typed `Property<T>` values, and the flat state registry backing all property lookups. Both `Identifier` and `BlockState` are readonly structs holding only interned strings or an int id — per-state data is looked up in a registry instead of stored once per state instance.
- The built-in registry declarations, the `Registries` key constants that name them, and the core content types living in this layer: blocks, items, fluids, biomes, damage types and entity attributes.
