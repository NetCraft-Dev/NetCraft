using System.Text.Json;

namespace NetCraft.Game.Server.JsonRpc.WebSocket;

//JsonToWebSocketEncoder JSON 转管理服务 websocket 文本帧 对应原版 net.minecraft.server.jsonrpc.websocket.JsonToWebSocketEncoder
//原版继承 netty MessageToMessageEncoder 这里直接给出 JSON 元素到文本的转换
public static class JsonToWebSocketEncoder
{
    //Encode 把 JSON 元素序列化为 websocket 文本帧内容
    public static string Encode(JsonElement value) => value.GetRawText();
}
