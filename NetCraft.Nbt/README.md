# NetCraft.Nbt

The NBT subsystem. It maps to the original `net.minecraft.nbt` package and is the byte-level serialization foundation for saves and protocol payloads.

## Features

- Defines all NBT tag types and their type registry, with tag ids ordered exactly as in the reference format.
- Reads and writes NBT: big-endian binary encoding, GZIP-compressed files, streaming readers, and a memory-mapped path for large files. Byte-level compatibility with the reference format is mandatory — tag ids, layout, modified UTF-8 strings and the compressed-file format all match.
- Provides two traversal models: an object-building `TagVisitor` and a `StreamTagVisitor` that walks raw data without materializing tags, plus the field-selecting visitors used for partial reads. Array values are exposed as `ReadOnlySpan<T>` on the streaming path to avoid intermediate allocations.
- Enforces resource limits through `NbtAccounter`, which tracks consumed bytes and nesting depth to reject malicious saves.
- Bridges into the codec layer with `NbtOps`, a `DynamicOps<Tag>` implementation.
- Implements SNBT, the stringified form of NBT: the parser entry point, grammar, operations and codecs.
