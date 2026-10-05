# NetCraft.Commands

The command framework. It is a C# port of Mojang's Brigadier, mapping to the original `com.mojang.brigadier` package, together with the NetCraft-specific command source supplied by the game layer.

## Features

- Reads a raw command string through a cursor-based reader that tracks position, remaining text and quoted or escaped segments. Argument parsers read exclusively through this reader, so parse failures carry precise ranges back into the context.
- Models the command tree as literal and argument nodes, merged by name when children are added, and builds trees through fluent argument builders.
- Dispatches: parses a line into a context, executes a command, and produces tab-completion suggestions, including redirects and forks.
- Supplies argument types for integers, longs, floats, doubles, booleans and strings (single word, quoted, greedy).
- Produces suggestions through providers and builders, with ranges and sorted result sets.
- Builds execution context: the parsed argument and node chain, string ranges, and the immutable reader handed to argument types.
- Reports errors as syntax exceptions carrying cursor and context, with simple and dynamic exception types for varying argument counts.
- Carries messages as literal or translatable text, and describes a command source: sender name, permission level, output writer and separate success and failure channels.
- The framework is generic over the source type, so the dispatcher, context and builders are reusable; NetCraft supplies `CommandSourceStack` as the concrete source.
