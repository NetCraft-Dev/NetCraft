using System.Text;
using NetCraft.Game.Commands;
using NetCraft.Game.Server;

namespace NetCraft.Game.Server.Rcon;

//RconConsoleSource RCON 命令的执行者与输出缓冲对应原版 net.minecraft.server.rcon.RconConsoleSource
//命令输出写进缓冲 RconClient 在命令执行完后把缓冲内容发回给 RCON 客户端
//命令源由 ServerCommandSource.Rcon 造 名字 Rcon 权限等级 owners
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

    //PrepareForCommand 清空缓冲
    public void PrepareForCommand() => _buffer.Clear();

    //GetCommandResponse 取缓冲内容
    public string GetCommandResponse() => _buffer.ToString();

    //CreateCommandSourceStack 造 RCON 命令源 输出走缓冲 不落日志
    public ServerCommandSource CreateCommandSourceStack()
        => ServerCommandSource.Rcon(_server, _writer);

    //RconBufferWriter 把命令回执写进缓冲 NewLine 置空让 WriteLine 不追加换行 与原版 append 行为一致
    private sealed class RconBufferWriter(StringBuilder buffer) : TextWriter
    {
        public override Encoding Encoding => Encoding.UTF8;
        public override string NewLine => string.Empty;

        public override void Write(char value) => buffer.Append(value);

        public override void Write(string? value) => buffer.Append(value);
    }
}
