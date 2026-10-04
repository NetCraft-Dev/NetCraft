using System.Text.Json;
using System.Text.Json.Serialization;
using NetCraft.Config;

namespace NetCraft.Network.Protocol.Status;

//ServerStatus 服务器状态对应原版 net.minecraft.network.protocol.status.ServerStatus
//简化版用 System.Text.Json 序列化替代原版 Codec 体系
//原版依赖 Component/Codec/WorldVersion 等 NetCraft 暂未实现的类
//简化版 description 用 string 替代 Component
public sealed record ServerStatus
{
    //Description 服务器 MOTD 描述简化版用 string
    public string Description { get; init; } = string.Empty;

    //Players 玩家信息可选
    public PlayersData? Players { get; init; }

    //Version 版本信息可选
    public VersionData? Version { get; init; }

    //Favicon 服务器图标可选 base64 PNG
    public FaviconData? Favicon { get; init; }

    //EnforcesSecureChat 是否强制安全聊天默认 false
    public bool EnforcesSecureChat { get; init; }

    //ToJson 序列化为 JSON 字符串用于网络传输
    public string ToJson() => JsonSerializer.Serialize(this, JsonContext);

    //FromJson 反序列化 JSON 字符串为 ServerStatus
    public static ServerStatus? FromJson(string json) =>
        string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<ServerStatus>(json, JsonContext);

    //JsonContext 序列化配置忽略 null 值驼峰命名
    private static readonly JsonSerializerOptions JsonContext = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    //PlayersData 玩家信息对应原版 ServerStatus.Players
    //用 PlayersData 避免与外层属性 Players 同名冲突
    //Online 可写供服务端周期刷新在线人数
    public sealed record PlayersData
    {
        public int Max { get; init; }
        public int Online { get; set; }
        public List<NameAndId> Sample { get; init; } = new();
    }

    //VersionData 版本信息对应原版 ServerStatus.Version
    //用 VersionData 避免 System.Version 冲突
    public sealed record VersionData
    {
        public string Name { get; init; } = string.Empty;
        public int Protocol { get; init; }

        //Current 返回当前 NetCraft 实现版本
        //S4 对齐 26.2 version.json 名称 26.2 协议 776
        public static VersionData Current() => new()
        {
            Name = SharedConstants.Version,
            Protocol = SharedConstants.ProtocolVersion,
        };
    }

    //FaviconData 服务器图标对应原版 ServerStatus.Favicon
    //原版 base64 编码 PNG 简化版直接存 byte[]
    public sealed record FaviconData(byte[] IconBytes)
    {
        //Prefix base64 数据 URL 前缀
        public const string Prefix = "data:image/png;base64,";

        //ToBase64 编码为 base64 数据 URL
        public string ToBase64() => Prefix + Convert.ToBase64String(IconBytes);

        //FromBase64 解码 base64 数据 URL
        public static FaviconData? FromBase64(string base64)
        {
            if (!base64.StartsWith(Prefix)) return null;
            try
            {
                var data = Convert.FromBase64String(base64[Prefix.Length..]);
                return new FaviconData(data);
            }
            catch (FormatException)
            {
                return null;
            }
        }
    }
}

//NameAndId 玩家名和 UUID 简化对应原版 net.minecraft.server.players.NameAndId
//原版用 Codec 列表序列化简化版用 record JSON
public sealed record NameAndId
{
    public string Name { get; init; } = string.Empty;
    public string Id { get; init; } = string.Empty;
}
