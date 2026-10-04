using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using NetCraft.TPGA.Auth;
using NetCraft.TPGA.Crypto;
using NetCraft.TPGA.Logging;

namespace NetCraft.TPGA.Protocol;

//WssHandler WebSocket 接受/消息分发/生命周期
//连接后下发公钥 客户端加密身份握手 颁发 UUID 后续 api_call 校验 token
//api_call 调度 4 个玩家管理 method create/delete/freeze/update
//断开时清理 ApiTokenStore 移除对应会话
public sealed class WssHandler
{
    private readonly RsaKeyProvider _rsa;
    private readonly WssHandshake _handshake;
    private readonly ApiTokenStore _tokens;
    private readonly UserRepository _users;

    public WssHandler(RsaKeyProvider rsa, WssHandshake handshake, ApiTokenStore tokens, UserRepository users)
    {
        _rsa = rsa;
        _handshake = handshake;
        _tokens = tokens;
        _users = users;
    }

    //AcceptAsync 接受 wss 连接并处理消息循环直到断开
    public async Task AcceptAsync(HttpContext ctx)
    {
        if (!ctx.WebSockets.IsWebSocketRequest)
        {
            ctx.Response.StatusCode = 400;
            return;
        }
        using var ws = await ctx.WebSockets.AcceptWebSocketAsync();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ctx.RequestAborted);
        try
        {
            await HandleAsync(ws, cts.Token);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Log.Warning("Wss", $"connection error: {ex.Message}");
        }
        finally
        {
            _tokens.RemoveBySocket(ws);
            //收到 Close 帧后 State=CloseReceived 无条件 CloseAsync 回 Close 响应完成握手
            //已 Aborted/Closed 时抛异常被 catch 吞掉不影响清理
            try { await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "server closing", CancellationToken.None); }
            catch { }
        }
    }

    //HandleAsync 消息循环 先发公钥 再按消息 type 分发
    private async Task HandleAsync(WebSocket ws, CancellationToken ct)
    {
        await SendAsync(ws, new ApiMessage { Type = "pubkey", Pem = _rsa.PublicKeyPem }, ct);
        Log.Info("Wss", "pubkey sent");
        var buffer = new byte[8192];
        while (ws.State == WebSocketState.Open)
        {
            string? text;
            try { text = await ReceiveTextAsync(ws, buffer, ct); }
            catch { break; }
            if (text == null) break;
            await DispatchAsync(ws, text, ct);
        }
    }

    //DispatchAsync 按消息 type 分发 auth/api_call 其他回 error
    private async Task DispatchAsync(WebSocket ws, string text, CancellationToken ct)
    {
        ApiMessage? msg;
        try
        {
            msg = JsonSerializer.Deserialize<ApiMessage>(text, _jsonOptions);
            if (msg == null || string.IsNullOrEmpty(msg.Type)) return;
        }
        catch
        {
            await SendAsync(ws, new ApiMessage { Type = "error", Error = "invalid message json" }, ct);
            return;
        }
        switch (msg.Type)
        {
            case "auth":
                await OnAuth(ws, msg, ct);
                break;
            case "api_call":
                await OnApiCall(ws, msg, ct);
                break;
            default:
                await SendAsync(ws, new ApiMessage { Type = "error", Error = $"unknown type {msg.Type}" }, ct);
                break;
        }
    }

    //OnAuth 握手 解密身份包 颁发 UUID 注册会话
    private async Task OnAuth(WebSocket ws, ApiMessage msg, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(msg.Payload))
        {
            await SendAsync(ws, new ApiMessage { Type = "auth_fail", Error = "missing payload" }, ct);
            return;
        }
        try
        {
            var api = _handshake.Authenticate(msg.Payload);
            var token = Guid.NewGuid().ToString("N");
            _tokens.Register(new ApiSession
            {
                Token = token,
                Socket = ws,
                ApiAccountId = api.Id,
                ConnectedAt = DateTime.UtcNow
            });
            Log.Info("Wss", $"auth ok api={api.Username} token={token}");
            await SendAsync(ws, new ApiMessage { Type = "auth_ok", Token = token }, ct);
        }
        catch (WssException ex)
        {
            Log.Warning("Wss", $"auth fail: {ex.Message}");
            await SendAsync(ws, new ApiMessage { Type = "auth_fail", Error = ex.Message }, ct);
        }
    }

    //OnApiCall 校验 token 后按 method 分发 4 个玩家管理 method
    //校验失败回 error unauthorized 业务异常回 api_result ok=false 结构化错误码
    private async Task OnApiCall(WebSocket ws, ApiMessage msg, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(msg.Token) || !_tokens.Verify(msg.Token, ws))
        {
            await SendAsync(ws, new ApiMessage { Type = "error", Error = "unauthorized" }, ct);
            return;
        }
        if (string.IsNullOrEmpty(msg.Method))
        {
            await SendAsync(ws, new ApiMessage { Type = "api_result", Ok = false, Error = "missing_method", ErrorMessage = "method is required" }, ct);
            return;
        }
        //Verify 通过 Find 必非空 用 ! 抑制可空告警
        var session = _tokens.Find(msg.Token)!;
        ApiMessage result;
        try
        {
            result = msg.Method switch
            {
                "player.create" => CreatePlayer(msg.Args, session.ApiAccountId),
                "player.delete" => DeletePlayer(msg.Args, session.ApiAccountId),
                "player.freeze" => FreezePlayer(msg.Args, session.ApiAccountId),
                "player.update" => UpdatePlayer(msg.Args, session.ApiAccountId),
                _ => throw new WssApiException("unknown_method", $"unknown method: {msg.Method}")
            };
        }
        catch (WssApiException ex)
        {
            await SendAsync(ws, new ApiMessage { Type = "api_result", Ok = false, Error = ex.Code, ErrorMessage = ex.Message }, ct);
            return;
        }
        catch (Exception ex)
        {
            Log.Warning("Wss", $"api_call error method={msg.Method}: {ex.Message}");
            await SendAsync(ws, new ApiMessage { Type = "api_result", Ok = false, Error = "internal_error", ErrorMessage = "internal error" }, ct);
            return;
        }
        await SendAsync(ws, result, ct);
    }

    //CreatePlayer player.create username/password 必填 email 可空 重名/重邮箱检查
    private ApiMessage CreatePlayer(JsonElement? args, int apiAccountId)
    {
        var a = DeserializeArgs<CreatePlayerArgs>(args);
        if (string.IsNullOrEmpty(a.Username) || string.IsNullOrEmpty(a.Password))
            throw new WssApiException("fields_required", "username and password required");
        if (_users.FindByUsername(a.Username) != null)
            throw new WssApiException("username_exists", "username already exists");
        if (!string.IsNullOrEmpty(a.Email) && _users.FindByEmail(a.Email) != null)
            throw new WssApiException("email_exists", "email already exists");
        var uuid = Guid.NewGuid().ToString("N");
        var id = _users.Insert(uuid, a.Username, PasswordHasher.HashGameAccount(a.Password), a.Email);
        Log.Info("Wss", $"player created api={apiAccountId} id={id} user={a.Username} email={a.Email ?? "-"} uuid={uuid}");
        return new ApiMessage
        {
            Type = "api_result",
            Ok = true,
            Data = JsonSerializer.SerializeToElement(new { id, uuid })
        };
    }

    //DeletePlayer player.delete 按 uuid 删除 不存在回 player_not_found
    private ApiMessage DeletePlayer(JsonElement? args, int apiAccountId)
    {
        var a = DeserializeArgs<DeletePlayerArgs>(args);
        if (string.IsNullOrEmpty(a.Uuid))
            throw new WssApiException("fields_required", "uuid is required");
        var user = _users.FindByUuid(a.Uuid) ?? throw new WssApiException("player_not_found", "player not found");
        _users.Delete(user.Id);
        Log.Info("Wss", $"player deleted api={apiAccountId} id={user.Id} user={user.Username} uuid={a.Uuid}");
        return new ApiMessage { Type = "api_result", Ok = true };
    }

    //FreezePlayer player.freeze enabled true 解冻 false 冻结
    private ApiMessage FreezePlayer(JsonElement? args, int apiAccountId)
    {
        var a = DeserializeArgs<FreezePlayerArgs>(args);
        if (string.IsNullOrEmpty(a.Uuid))
            throw new WssApiException("fields_required", "uuid is required");
        var user = _users.FindByUuid(a.Uuid) ?? throw new WssApiException("player_not_found", "player not found");
        _users.UpdateEnabled(user.Id, a.Enabled);
        Log.Info("Wss", $"player enabled={a.Enabled} api={apiAccountId} id={user.Id} user={user.Username}");
        return new ApiMessage
        {
            Type = "api_result",
            Ok = true,
            Data = JsonSerializer.SerializeToElement(new { enabled = a.Enabled })
        };
    }

    //UpdatePlayer player.update uuid 必填 username/password/email 非空则更新
    //email null 不变 空串清空 非空查重 与 AdminEndpoints 一致
    private ApiMessage UpdatePlayer(JsonElement? args, int apiAccountId)
    {
        var a = DeserializeArgs<UpdatePlayerArgs>(args);
        if (string.IsNullOrEmpty(a.Uuid))
            throw new WssApiException("fields_required", "uuid is required");
        var user = _users.FindByUuid(a.Uuid) ?? throw new WssApiException("player_not_found", "player not found");
        if (!string.IsNullOrEmpty(a.Username) && a.Username != user.Username)
        {
            if (_users.FindByUsername(a.Username) != null)
                throw new WssApiException("username_exists", "username already exists");
            _users.UpdateUsername(user.Id, a.Username);
        }
        if (!string.IsNullOrEmpty(a.Password))
        {
            if (a.Password.Length < 4)
                throw new WssApiException("password_too_short", "password must be at least 4 characters");
            _users.UpdatePassword(user.Id, PasswordHasher.HashGameAccount(a.Password));
        }
        //email null 不变 空串清空 非空查重
        var currentEmail = user.Email ?? "";
        if (a.Email != null && a.Email != currentEmail)
        {
            if (!string.IsNullOrEmpty(a.Email) && _users.FindByEmail(a.Email) != null)
                throw new WssApiException("email_exists", "email already exists");
            _users.UpdateEmail(user.Id, string.IsNullOrEmpty(a.Email) ? null : a.Email);
        }
        Log.Info("Wss", $"player updated api={apiAccountId} id={user.Id} user={user.Username}");
        return new ApiMessage { Type = "api_result", Ok = true };
    }

    //DeserializeArgs 反序列化 args JSON 为指定类型 null 或格式错误抛 WssApiException
    private static T DeserializeArgs<T>(JsonElement? args) where T : class, new()
    {
        if (args == null || args.Value.ValueKind == JsonValueKind.Undefined)
            throw new WssApiException("missing_args", "args is required");
        try
        {
            return args.Value.Deserialize<T>(_jsonOptions) ?? throw new WssApiException("invalid_args", "args is invalid");
        }
        catch (WssApiException) { throw; }
        catch (Exception ex)
        {
            throw new WssApiException("invalid_args", $"args parse error: {ex.Message}");
        }
    }

    //ReceiveTextAsync 接收一条完整文本消息 支持分片拼接
    private static async Task<string?> ReceiveTextAsync(WebSocket ws, byte[] buffer, CancellationToken ct)
    {
        var sb = new StringBuilder();
        WebSocketReceiveResult result;
        do
        {
            result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
            if (result.MessageType == WebSocketMessageType.Close) return null;
            sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
        } while (!result.EndOfMessage);
        return sb.ToString();
    }

    //SendAsync 序列化 JSON 并发送文本帧
    private static async Task SendAsync(WebSocket ws, ApiMessage msg, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(msg, _jsonOptions);
        var bytes = Encoding.UTF8.GetBytes(json);
        await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, endOfMessage: true, ct);
    }

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        //可空字段 null 时不输出 避免消息冗余
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}
