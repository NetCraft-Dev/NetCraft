using NetCraft.Util;

namespace NetCraft.Util.Parsing.Packrat.Commands;

//StringReaderParserState对应原版net.minecraft.util.parsing.packrat.commands.StringReaderParserState
//用CommandStringReader的Cursor实现mark/restore
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
