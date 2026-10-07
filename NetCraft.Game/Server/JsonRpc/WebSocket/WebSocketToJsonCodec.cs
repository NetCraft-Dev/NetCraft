using System.Text.Json;

namespace NetCraft.Game.Server.JsonRpc.WebSocket;

//WebSocketToJsonCodec converts a service websocket text frame to JSON, maps to vanilla net.minecraft.server.jsonrpc.websocket.WebSocketToJsonCodec
//Vanilla extends netty MessageToMessageDecoder; this directly provides the text to JSON element conversion
public static class WebSocketToJsonCodec
{
    //Decode parses the websocket text frame content into a JSON element; clone is needed when outliving the document
    public static JsonElement Decode(string message)
    {
        using var document = JsonDocument.Parse(message);
        return document.RootElement.Clone();
    }
}
