using System.Text.Json;

namespace NetCraft.Game.Server.JsonRpc.WebSocket;

//WebSocketToJsonCodec 管理服务 websocket 文本帧转 JSON 对应原版 net.minecraft.server.jsonrpc.websocket.WebSocketToJsonCodec
//原版继承 netty MessageToMessageDecoder 这里直接给出文本到 JSON 元素的转换
public static class WebSocketToJsonCodec
{
    //Decode 把 websocket 文本帧内容解析为 JSON 元素 脱离文档生命周期需克隆
    public static JsonElement Decode(string message)
    {
        using var document = JsonDocument.Parse(message);
        return document.RootElement.Clone();
    }
}
