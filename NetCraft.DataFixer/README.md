# NetCraft.DataFixer

The DataFixerUpper adaptation. It maps to the original `com.mojang.datafixers` and `net.minecraft.util.datafix`, providing the type-directed migration framework that upgrades saved data across versions.

## Features

- Defines the fixer contract and the builder that accumulates schemas and fixes into a runnable fixer.
- Describes data shapes as types and type templates — products, sums, lists, constants, named fields, tagged choices, hooks, recursion points and checks — organised into type families.
- Manages versioned schemas, each owning its templates, its recursive type family and the parent link used to derive one schema from the previous one.
- Provides the optics layer (lenses, prisms, traversals, getters, adapters and their profunctor foundations) for addressing nested fields of a typed value.
- Simulates higher-kinded types through the `K1` / `K2` / `App` / `Kind` hierarchy, which is how the original generic type algebra is expressed in C#.
- Supplies the DSL of static constructors used by schemas and fixes, plus the concrete fixes themselves.
- Operates on `Dynamic<T>` over an abstract `DynamicOps<U>`, so the framework never hard-depends on NBT and stays clean across carriers. It also registers no schema or fix of its own — the business layer builds a `DataFixerBuilder`, adds its schemas and fixes, and calls `Build().Fixer()`.
