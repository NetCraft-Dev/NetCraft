using NetCraft.Util;

namespace NetCraft.Util.Parsing.Packrat.Commands;

//StringReaderParserState maps to vanilla net.minecraft.util.parsing.packrat.commands.StringReaderParserState
//Implements mark/restore using CommandStringReader's Cursor
public sealed class StringReaderParserState : CachedParseState<CommandStringReader>
{
    public override CommandStringReader Input { get; }

    public StringReaderParserState(ErrorCollector<CommandStringReader> errorCollector, CommandStringReader input)
        : base(errorCollector)
    {
        Input = input;
    }

    public override int Mark() => Input.Cursor;

    public override void Restore(int mark) => Input.Cursor = mark;
}
