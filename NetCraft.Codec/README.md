# NetCraft.Codec

The serialization framework. It maps to the original `com.mojang.serialization` package: a carrier-agnostic codec layer that encodes and decodes typed values into an abstract data model.

## Features

- `Codec<T>` and `MapCodec<T>` — element-level and field-level encode and decode contracts.
- `DynamicOps<T>` — the abstract carrier operations: how to create, read and merge values of the target representation. Each concrete format supplies its own implementation; NBT does so in `NetCraft.Nbt`, so this layer stays format-agnostic and never references NBT.
- `DataResult<T>` — success or failure carrying an optional partial value; `Dynamic<T>` — a value paired with its ops that can be converted across ops.
- `MapLike<T>` and `RecordBuilder<T>` — the map view read during decoding and the field accumulator used while encoding.
- `Codecs` — scalar codecs and combinators such as `ListOf`, `ComapFlatMap`, `WithAlternative`, `Either`, `DispatchedMap` and `UnboundedMap`.
- `RecordCodecBuilder.Of1` through `Of16` — N-ary overloads standing in for the original `group(...).apply(instance, ctor)` chain.
- `FieldCodec` and `FieldCodecs`, `JsonOps`, `Alt` and the optional wrappers round out the field and alternative plumbing.
