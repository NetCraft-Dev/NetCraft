using System.Text;
using NetCraft.Game.Commands;
using NetCraft.Game.Server;

namespace NetCraft.Game.Server.Rcon;

//RconConsoleSource, the RCON command executor and output buffer, maps to vanilla net.minecraft.server.rcon.RconConsoleSource
//Command output is written into the buffer, RconClient sends the buffer content back to the RCON client after the command finishes
//The command source is built by ServerCommandSource.Rcon, named Rcon with permission level owners
public class RconConsoleSource
{
    private readonly StringBuilder _buffer = new();
    private readonly MinecraftServer _server;
    private readonly RconBufferWriter _writer;

    public RconConsoleSource(MinecraftServer server)
    {
        _server = server;
        _writer = new RconBufferWriter(_buffer);
    }

    //PrepareForCommand clears the buffer
    public void PrepareForCommand() => _buffer.Clear();

    //GetCommandResponse returns the buffer content
    public string GetCommandResponse() => _buffer.ToString();

    //CreateCommandSourceStack builds the RCON command source, output goes to the buffer and not to logs
    public ServerCommandSource CreateCommandSourceStack()
        => ServerCommandSource.Rcon(_server, _writer);

    //RconBufferWriter writes command replies into the buffer, NewLine is empty so WriteLine does not append a newline, matching vanilla append behavior
    private sealed class RconBufferWriter(StringBuilder buffer) : TextWriter
    {
        public override Encoding Encoding => Encoding.UTF8;
        public override string NewLine => string.Empty;

        public override void Write(char value) => buffer.Append(value);

        public override void Write(string? value) => buffer.Append(value);
    }
}
