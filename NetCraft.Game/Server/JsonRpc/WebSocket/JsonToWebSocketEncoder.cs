using System.Text.Json;

namespace NetCraft.Game.Server.JsonRpc.WebSocket;

//JsonToWebSocketEncoder converts JSON to a service websocket text frame, maps to vanilla net.minecraft.server.jsonrpc.websocket.JsonToWebSocketEncoder
//Vanilla extends netty MessageToMessageEncoder; this directly provides the JSON element to text conversion
public static class JsonToWebSocketEncoder
{
    //Encode serializes the JSON element into websocket text frame content
    public static string Encode(JsonElement value) => value.GetRawText();
}
