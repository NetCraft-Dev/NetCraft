# NetCraft Server Console

Once the server starts, commands have two entry points: the command input box in the GUI window, and the command line in the current terminal.
The terminal route is independent of the GUI switch; it is available both with `--nogui` and when starting with the GUI, and with the GUI it runs on its own background thread.

## Two modes

At startup it checks whether input and output are redirected and automatically picks a route:

| Case                     | Behavior                                      |
| ------------------------ | --------------------------------------------- |
| run directly in a terminal | interactive mode: per-keystroke reading, inline editing, history, Tab completion, logs and prompt coexisting |
| stdin or stdout redirected | pass-through mode: one command per line, output passed through verbatim, no control sequences emitted at all |

Pass-through mode is for scripts:

```powershell
@('version', 'stop') | .\NetCraft.Server.Exe.exe --nogui
```

The pipe can feed commands before the server is ready: when `stop` arrives before the main loop, the shutdown request is recorded and the main loop exits immediately once it starts.

To turn off the terminal command line entirely (handing it to a script, CI, or an external wrapper), add `--noconsole`:

```powershell
.\NetCraft.Server.Exe.exe --nogui --noconsole
```

## Shortcuts

| Key                     | Action                                  |
| ----------------------- | --------------------------------------- |
| `Enter`                 | execute the current line                |
| `Tab`                   | complete. A single candidate is inserted directly; with multiple candidates it first completes to the common prefix, and pressing again lists the candidates |
| `↑` / `↓`               | move up/down through history            |
| `Ctrl+R`                | reverse history search; press again to keep searching backward, `Enter` accepts and executes, `Esc` abandons |
| `←` / `→`               | move the cursor left/right              |
| `Home` / `End`          | move the cursor to the start/end of the line |
| `Backspace` / `Delete`  | delete the character to the left/right of the cursor |
| `Esc`                   | discard the current line                |
| `Ctrl+L`                | clear the screen                        |
| `Ctrl+C`                | stop the server, reusing the kernel's existing CancelKeyPress and going through the normal shutdown flow |

## Input history

Submitted commands are appended to `console_history` in the program root and can be recalled on the next startup; both memory and file keep only the most recent 1000 entries.
Empty lines and commands identical to the previous one do not enter history.

## Syntax highlighting

The input line is highlighted in real time based on the command tree, taken from Paper's `BrigadierCommandHighlighter`:

- literal nodes such as commands and subcommands use the default color
- argument nodes cycle through `6 3 2 5 4` (cyan, yellow, green, magenta, blue) in order of appearance
- the part not parsed by the command tree is marked red, indicating there is no matching node here

## Command execution path

Submitted commands execute under the **server console command source**, going through the same command manager as the GUI console input box and the chat bar's `/command`:

- commands are accepted with or without a leading slash
- permission level is maxed out; the console is the server itself
- responses go out through the log, unrelated to the player-side system chat packet
- completion uses the command tree's real data; subcommands, arguments, and registered names all complete, the same path as pressing Tab in-game

Commands do not run on the console thread: the console only enqueues them, and each tick the main loop takes them out and executes them inside `_worldGate`.
This corresponds to vanilla's `DedicatedServer.serverCommandQueue` and `handleConsoleInputs`, so commands always see a consistent world state at tick boundaries.

## The completion candidate list

When there are multiple candidates and completion cannot reach a common prefix, the candidates are listed above the prompt and stay there; later logs do not wash them away.
They are only dismissed on any editing key (typing, backspace, cursor movement, history navigation) or on submitting a command.
Candidates are listed one per line, the same layout as the completion list that appears in-game on Tab.
Dismissing erases the candidates together with the prompt line and redraws, leaving no block of blank lines.
One per line also makes erasure accurate: the line length is far less than the terminal width, so the terminal never wraps on its own, and as many lines are recorded as there are entries.

Pressing Enter on an empty line leaves nothing on screen; the prompt redraws in place.

## Known limitations

- Wide characters (such as Chinese) in the input line are counted as one column per character when redrawing, so cursor positioning is off. Commands themselves are ASCII, so the impact is limited to typing Chinese into the input line.
- When a log line wraps, only the line the cursor is on is cleared, leaving the lines above on screen. This is intentional; logs are supposed to stay there.
