using NetCraft.Game.Network.Protocol.Game;
using NetCraft.Network.Protocol.Login;
using NetCraft.Game.World.Inventory;
using NetCraft.Game.World.Level;
using NetCraft.Logging;
using NetCraft.Network;
using NetCraft.Network.Chat;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Storage;

namespace NetCraft.Game.Server;

//PlayerList 在线玩家集合管理对应原版 PlayerList
//管理 ServerPlayer 生命周期提供 PlaceNewPlayer/RemovePlayer/Broadcast API
//满员拒绝新玩家加入对应原版 max-players 限制
public sealed class PlayerList
{
    private readonly MinecraftServer _server;
    private readonly List<ServerPlayer> _players = new();
    private readonly object _lock = new();

    public IReadOnlyList<ServerPlayer> Players
    {
        get { lock (_lock) return _players.ToList(); }
    }

    public int PlayerCount
    {
        get { lock (_lock) return _players.Count; }
    }

    public int MaxPlayers { get; }

    public PlayerList(MinecraftServer server, int maxPlayers)
    {
        _server = server;
        MaxPlayers = maxPlayers;
    }

    //PlaceNewPlayer 创建 ServerPlayer 加入玩家列表并发送进入世界所需的 Clientbound 包序列
    //满员返回 null 调用方应发送 disconnect 包并断连
    public ServerPlayer? PlaceNewPlayer(Connection connection, GameProfile profile)
    {
        ServerPlayer? player;
        bool restored;
        List<ServerPlayer> others;
        lock (_lock)
        {
            if (_players.Count >= MaxPlayers)
            {
                Log.Warning($"Player join rejected, server is full {MaxPlayers} name={profile.Name}");
                return null;
            }
            //tab 玩家列表要向新玩家公布已有玩家 先取快照 此刻新玩家还没进列表
            others = new List<ServerPlayer>(_players);
            player = new ServerPlayer(profile, connection, _server.Overworld);
            //玩家列表反向引用 物品入手音效要广播给全服 对应原版 ServerPlayer 拿到的 level/players
            player.OwnerList = this;
            //应用服务端默认游戏模式 settings.gamemode 解析结果
            player.GameType = _server.DefaultGameType;
            //权限等级按 ops.json 名单判定 不在名单即 0 级
            player.PermissionLevel = _server.OpList.GetPermissionLevel(profile);
            //玩家存档优先于默认值 位置/朝向/血量/经验/物品栏须在发入场包之前就位
            restored = _server.PlayerData.LoadInto(player);
            //实体追踪按玩家视距判定可见性 取服务端配置
            player.ViewDistanceChunks = _server.Settings.ViewDistance;
            _players.Add(player);
        }
        Log.Info(restored
            ? $"Player joined (save restored) {profile.Name} entityId={player.EntityId} online {PlayerCount}/{MaxPlayers}"
            : $"Player joined {profile.Name} entityId={player.EntityId} online {PlayerCount}/{MaxPlayers}");
        //背包菜单在进世界前建好 初始内容随 SendJoinPackets 下发
        player.SetUpInventoryMenu();
        SendJoinPackets(player);
        //tab 玩家列表: 新玩家收全部已有玩家 所有在线玩家含自己收新玩家 对应原版 PlayerList.addPlayer
        //不发这一步客户端 tab 只有自己 别人都看不见 选择器仍能按名字命中服务端的玩家列表
        if (others.Count > 0)
            player.Connection.Send(new ClientboundPlayerInfoUpdatePacket(
                PlayerInfoActions, others.Select(EntryOf).ToList()));
        BroadcastAll(new ClientboundPlayerInfoUpdatePacket(PlayerInfoActions, new[] { EntryOf(player) }));
        //进服提示 对应原版 PlayerList.addPlayer 里的 multiplayer.player.joined 黄色
        BroadcastSystemMessage(
            Component.Translatable("multiplayer.player.joined", Component.Literal(profile.Name))
                .WithStyle(ChatFormatting.Yellow),
            false);
        return player;
    }

    //PlayerInfoActions 玩家信息下发的动作集合 对应原版 createPlayerInitializing
    //不含 InitializeChat(需要聊天会话签名)与 26.2 新增的 hat/list_order 显示项
    private const int PlayerInfoActions = (1 << (int)PlayerInfoAction.AddPlayer)
        | (1 << (int)PlayerInfoAction.UpdateGameMode)
        | (1 << (int)PlayerInfoAction.UpdateListed)
        | (1 << (int)PlayerInfoAction.UpdateLatency)
        | (1 << (int)PlayerInfoAction.UpdateDisplayName);

    //EntryOf 玩家信息条目 延迟先写 0 尚未统计 ping
    private static PlayerInfoEntry EntryOf(ServerPlayer player)
        => new(player.Profile.Id, player.Profile.Name, player.GameType, true, 0, null);

    //SendJoinPackets 发送 placeNewPlayer 对应的 Clientbound 包序列
    //1.PlayerInfoUpdate(自己) 2.LoginPacket(含 SpawnInfo) 3.PlayerAbilities 4.SetHeldSlot
    //5.PlayerPosition(同步出生点) 6.SetDefaultSpawnPosition 7.SetHealth/SetExperience 8.SetChunkCacheRadius
    //9.LEVEL_CHUNKS_LOAD_START+UpdateCenter 入队视距区块由 ChunkSender 逐 tick 渐进发送
    private void SendJoinPackets(ServerPlayer player)
    {
        var gameType = player.GameType;
        var connection = player.Connection;
        var viewDistance = _server.Settings.ViewDistance;
        try
        {
            //PlayerInfoUpdate 自己 动作集合与后续广播一致
            connection.Send(new ClientboundPlayerInfoUpdatePacket(PlayerInfoActions, new[] { EntryOf(player) }));
            //LoginPacket 真实 CommonPlayerSpawnInfo 含维度/种子/游戏模式
            var spawnInfo = new CommonPlayerSpawnInfo(
                Identifier.WithDefaultNamespace("overworld"),
                LevelKeys.OVERWORLD,
                _server.WorldSeed,
                gameType,
                null,
                false, false, false, null, null, 0);
            connection.Send(new ClientboundLoginPacket(
                player.EntityId,
                false,
                new[] { LevelKeys.OVERWORLD },
                MaxPlayers,
                viewDistance,
                viewDistance,
                false, true, false,
                spawnInfo,
                false));
            //PlayerAbilities 按玩家能力状态下发 flying 取自存档 创造重进后仍在飞 不是每次都从地面开始
            var abilities = player.Abilities;
            connection.Send(new ClientboundPlayerAbilitiesPacket(
                abilities.Invulnerable, abilities.Flying, abilities.MayFly, abilities.Instabuild,
                abilities.FlyingSpeed, abilities.WalkingSpeed));
            //SetHeldSlot 恢复选中槽 无存档时为 0
            connection.Send(new ClientboundSetHeldSlotPacket(player.Inventory.SelectedSlot));
            //ContainerSetContent 背包菜单初始内容 客户端据此建好物品栏
            player.ContainerMenu?.SendAllDataToRemote();
            //PlayerPosition 同步玩家出生位置
            connection.Send(new ClientboundPlayerPositionPacket(
                player.Position.X, player.Position.Y, player.Position.Z,
                player.Yaw, player.Pitch, 0, 0));
            //InitializeBorder 边界初始状态 客户端据此渲染边界墙 对应原版 sendLevelInfo 首条
            var border = player.Level.WorldBorder;
            connection.Send(new ClientboundInitializeBorderPacket(
                border.CenterX, border.CenterZ, border.GetSize(), border.GetLerpTarget(),
                border.GetLerpTime(), border.AbsoluteMaxSize, border.WarningBlocks, border.WarningTime));
            //SetDefaultSpawnPosition 世界出生点 配置阶段预载与玩家出生均以此为中心
            connection.Send(new ClientboundSetDefaultSpawnPositionPacket(
                new BlockPos((int)_server.SpawnPos.X, (int)_server.SpawnPos.Y, (int)_server.SpawnPos.Z), 0f));
            //天气状态 正在下雨时入场补发开始下雨与雨雷等级 对应原版 sendLevelInfo 的 isRaining 分支
            var weatherLevel = player.Level;
            if (weatherLevel.IsRaining)
            {
                connection.Send(new ClientboundGameEventPacket(GameEventType.StartRaining, 0f));
                connection.Send(new ClientboundGameEventPacket(GameEventType.RainLevelChange, weatherLevel.GetRainLevel(1f)));
                connection.Send(new ClientboundGameEventPacket(GameEventType.ThunderLevelChange, weatherLevel.GetThunderLevel(1f)));
            }
            //SetTime 入场即同步全量时钟状态 否则客户端从本地 0 开始自己走 与服务端各走各的
            connection.Send(_server.ClockManager.CreateFullSyncPacket());
            //SetHealth/SetExperience 按存档恢复的血量与经验 无存档为满血零经验
            connection.Send(new ClientboundSetHealthPacket(player.Health, 20, 5f));
            connection.Send(new ClientboundSetExperiencePacket(player.XpProgress, player.XpTotal, player.XpLevel));
            //SetChunkCacheRadius 视野距离
            connection.Send(new ClientboundSetChunkCacheRadiusPacket(viewDistance));
            //权限等级 客户端没这个事件就认为自己是 0 级 F3+F4 等入口会被客户端自己挡掉
            SendPlayerPermissionLevel(player);
            var chunkX = (int)Math.Floor(player.Position.X / 16);
            var chunkZ = (int)Math.Floor(player.Position.Z / 16);
            //LEVEL_CHUNKS_LOAD_START 告知客户端区块即将下发
            //客户端 LevelLoadTracker 初始是等待服务端 收不到该事件永远停在加载地形
            connection.Send(new ClientboundGameEventPacket(GameEventType.LevelChunksLoadStart, 0f));
            //非阻塞取块 未就绪的坐标留在待发集合下次 tick 再取
            //生成在 ServerChunkCache 后台推进不再阻塞主循环
            //UpdateCenter 会发 SetChunkCacheCenter 并入队初始视野 玩家跨块后由 ServerPlayer.Tick 持续更新
            player.ChunkSender = new ChunkSender(
                connection,
                pos => _server.Overworld.GetChunk(pos),
                pos => _server.Overworld.IsChunkFailed(pos),
                _server.Overworld.ChunkSource,
                _server.Overworld.BlockEntityBridge);
            player.ChunkSender.UpdateCenter(chunkX, chunkZ, viewDistance);
            //刻速率状态 世界当前冻结的话客户端要立刻知道 否则本地世界自顾自推进 对应原版 updateJoiningPlayer
            _server.TickRate.SendStateToJoiningPlayer(player);
            //ClientboundCommands 命令树 客户端据此解析玩家输入的斜杠命令
            //放在入场包之后 编码异常不至于挡住位置/生命/区块链路
            _server.Commands.SendCommands(player);
            Log.Debug($"Join packets sent {player.Profile.Name} pending chunks {player.ChunkSender.PendingCount}");
        }
        catch (Exception e)
        {
            Log.Error($"Join packets failed {player.Profile.Name} {e.Message}");
        }
    }

    //SendPlayerPermissionLevel 把权限等级告知该玩家客户端
    //原版用实体事件 24+等级 客户端据此判断 F3+F4 等需要权限的入口是否可用
    public void SendPlayerPermissionLevel(ServerPlayer player)
    {
        if (!player.Connection.IsConnected) return;
        var level = Math.Clamp(player.PermissionLevel, 0, 4);
        player.Connection.Send(new ClientboundEntityEventPacket(player.EntityId, (byte)(24 + level)));
    }

    //ApplyPermissionLevel 更新玩家权限等级并同步客户端 供 op/deop 命令调用
    public void ApplyPermissionLevel(ServerPlayer player, int level)
    {
        player.PermissionLevel = Math.Clamp(level, 0, 4);
        SendPlayerPermissionLevel(player);
    }

    //ChangeGameMode 切换玩家游戏模式并同步客户端 对应原版 ServerPlayer.setGameMode
    //顺序: 模式落位 -> 能力包 -> 游戏事件 -> 全服玩家列表游戏模式广播
    //gamemode 命令与客户端 change_gamemode 包都走这里 保证两条路径表现一致
    public void ChangeGameMode(ServerPlayer player, GameType gameType)
    {
        if (player.GameType == gameType) return;
        player.GameType = gameType;
        //切出旁观模式时把相机收回自身 对应原版 ServerPlayer.setGameMode 的 setCamera(this)
        if (gameType != GameType.Spectator) player.SetCamera(null);
        //上面给 GameType 赋值时已经按新模式重算过能力 这里照实下发 旁观强制飞 生存收回飞行
        var abilities = player.Abilities;
        player.Connection.Send(new ClientboundPlayerAbilitiesPacket(
            abilities.Invulnerable, abilities.Flying, abilities.MayFly, abilities.Instabuild,
            abilities.FlyingSpeed, abilities.WalkingSpeed));
        player.Connection.Send(new ClientboundGameEventPacket(GameEventType.ChangeGameMode, gameType.Id));
        BroadcastAll(new ClientboundPlayerInfoUpdatePacket(
            1 << (int)PlayerInfoAction.UpdateGameMode, new[]
            {
                new PlayerInfoEntry(player.Profile.Id, player.Profile.Name, gameType, true, 0, null)
            }));
    }

    //GetPlayerByName 按名字查在线玩家忽略大小写 供 /op /deop 解析目标
    public ServerPlayer? GetPlayerByName(string name)
    {
        lock (_lock)
            return _players.FirstOrDefault(p => string.Equals(p.Profile.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    //GetPlayerByEntityId 按实体 id 查在线玩家 攻击与交互包的目标解析用
    //玩家不在关卡实体管理器里 按 id 找玩家只能查这里
    public ServerPlayer? GetPlayerByEntityId(int entityId)
    {
        lock (_lock)
            return _players.FirstOrDefault(p => p.EntityId == entityId);
    }

    //CanPlayerLogin 登录准入检查 对应原版 PlayerList.canPlayerLogin
    //IP 封禁先判再判玩家封禁 命中就发断连包并关连接 调用方不再让该连接进世界
    //调用点必须在出站协议切到 Play 之后 否则断连包编码不出
    public bool CanPlayerLogin(Connection connection, GameProfile profile)
    {
        if (_server.IpBanList.IsBanned(connection.RemoteAddress))
        {
            Log.Info($"Login rejected, IP is banned ip={connection.RemoteAddress} name={profile.Name}");
            connection.Send(new ClientboundDisconnectPacket(Component.Translatable("disconnect.banned.ip")));
            connection.Disconnect("banned-ip");
            return false;
        }
        var ban = _server.BanList.Find(profile);
        if (ban is not null)
        {
            Log.Info($"Login rejected, player is banned name={profile.Name} reason={ban.Reason}");
            connection.Send(new ClientboundDisconnectPacket(
                Component.Translatable("disconnect.banned.reason", ban.Reason)));
            connection.Disconnect("banned");
            return false;
        }
        //白名单开启时非名单成员被拒 管理员例外 对应原版 isUsingWhitelist 分支
        if (_server.IsWhiteListEnabled && !_server.OpList.IsOp(profile) && !_server.WhiteList.IsAllowed(profile))
        {
            Log.Info($"Login rejected, not in whitelist name={profile.Name}");
            connection.Send(new ClientboundDisconnectPacket(
                Component.Translatable("multiplayer.disconnect.not_whitelisted")));
            connection.Disconnect("not-whitelisted");
            return false;
        }
        return true;
    }

    //HurtPlayer 对玩家造成伤害并同步血量与受伤动画 对应原版 ServerPlayer.hurtServer
    //返回是否真正造成伤害 无敌帧内与已死亡都返回 false
    public bool HurtPlayer(ServerPlayer target, ServerPlayer? attacker, float amount)
    {
        if (!target.Hurt(amount)) return false;
        SyncHealth(target);
        //受伤动画原版只发给追踪者 本作未做受伤包的分范围下发直接全服广播
        BroadcastAll(new ClientboundHurtAnimationPacket(target.EntityId, target.Yaw));
        //受伤声按玩家位置播给所有人 对应原版 LivingEntity.playHurtSound
        ServerSounds.PlaySound(this, SoundEvents.PlayerHurt, SoundSource.Players,
            target.Position.X, target.Position.Y, target.Position.Z, 1f, 1f);
        if (target.IsDeadOrDying) RespawnPlayer(target, attacker);
        return true;
    }

    //SyncHealth 把玩家血量同步给玩家自己 客户端血条靠它刷新
    public void SyncHealth(ServerPlayer player)
    {
        if (!player.Connection.IsConnected) return;
        player.Connection.Send(new ClientboundSetHealthPacket(player.Health, 20, 5f));
    }

    //RespawnPlayer 玩家死亡处理 广播死亡消息后满血复位到出生点
    //原版走 ClientboundPlayerCombatKill 加客户端重生握手 本作客户端没有死亡界面故直接复位
    private void RespawnPlayer(ServerPlayer player, ServerPlayer? attacker)
    {
        BroadcastSystemMessage(
            attacker is null
                ? Component.Translatable("death.attack.generic", Component.Literal(player.Profile.Name))
                : Component.Translatable("death.attack.player", Component.Literal(player.Profile.Name),
                    Component.Literal(attacker.Profile.Name)),
            false);
        Log.Info($"Player died {player.Profile.Name} killer={attacker?.Profile.Name ?? "none"}");
        //死亡声在复位之前播 用的是死亡位置 对应原版 LivingEntity.playDeathSound
        ServerSounds.PlaySound(this, SoundEvents.PlayerDeath, SoundSource.Players,
            player.Position.X, player.Position.Y, player.Position.Z, 1f, 1f);
        player.Velocity = Vec3.Zero;
        //重生满血按属性取 对应原版 setHealth(getMaxHealth())
        player.SetHealth(player.MaxHealth);
        //有个人重生点就回个人重生点 否则回世界出生点 对应原版 ServerPlayer 的 respawnPosition
        player.Position = player.RespawnPos ?? _server.SpawnPos;
        SyncHealth(player);
        if (!player.Connection.IsConnected) return;
        //位置复位走 PlayerPosition 客户端会回 accept_teleportation 校正本地预测
        player.Connection.Send(new ClientboundPlayerPositionPacket(
            player.Position.X, player.Position.Y, player.Position.Z, player.Yaw, player.Pitch, 0, 0));
    }

    //RemovePlayer 移除玩家返回是否成功
    //移除后广播退服提示 对应原版 removePlayerFromWorld 的 multiplayer.player.left
    public bool RemovePlayer(ServerPlayer player)
    {
        bool removed;
        lock (_lock) removed = _players.Remove(player);
        if (removed)
        {
            //先广播实体移除 别的客户端上的模型靠这个包摘掉
            //必须赶在 ForgetPlayer 之前 ForgetPlayer 会清掉观察记录 之后 PruneStale 再也补不了这一包
            BroadcastAll(new ClientboundRemoveEntitiesPacket(new[] { player.EntityId }));
            //可见性记录按玩家生命周期维护 玩家走了要摘掉它相关的表项否则一直留在追踪器里
            _server.EntityTracker.ForgetPlayer(player);
            //tab 列表移除该玩家 对应原版 PlayerList.remove 的 broadcastAll(ClientboundPlayerInfoRemovePacket)
            BroadcastAll(new ClientboundPlayerInfoRemovePacket(new[] { player.Profile.Id }));
            BroadcastSystemMessage(
                Component.Translatable("multiplayer.player.left", Component.Literal(player.Profile.Name))
                    .WithStyle(ChatFormatting.Yellow),
                false);
            Log.Info($"Player removed {player.Profile.Name} online {PlayerCount}/{MaxPlayers}");
        }
        return removed;
    }

    //BroadcastSystemMessage 向所有在线玩家广播系统消息 对应原版 PlayerList.broadcastSystemMessage
    //同时记一条到服务端日志 控制台与 GUI 靠这条才能看到聊天
    //say/emote 这类命令在控制台执行时没有在线玩家可发 不回日志就完全静默
    public void BroadcastSystemMessage(Component message, bool overlay)
    {
        Log.Info($"[Chat] {message.GetString()}");
        BroadcastAll(new ClientboundSystemChatPacket(message, overlay));
    }

    //BroadcastAll 向所有在线玩家发包
    //踢出/封禁后玩家要等 handleDisconnection 才移出列表 这里跳过已断连的连接免得刷告警
    public void BroadcastAll<THandler>(Packet<THandler> packet) where THandler : class
    {
        List<ServerPlayer> snapshot;
        lock (_lock) snapshot = _players.ToList();
        foreach (var p in snapshot)
        {
            if (!p.Connection.IsConnected) continue;
            try { p.Connection.Send(packet); }
            catch (Exception e) { Log.Warning($"Broadcast failed player={p.Profile.Name} {e.Message}"); }
        }
    }

    //BroadcastAllExcept 向除指定玩家外的所有在线玩家发包
    public void BroadcastAllExcept<THandler>(ServerPlayer exclude, Packet<THandler> packet) where THandler : class
    {
        List<ServerPlayer> snapshot;
        lock (_lock) snapshot = _players.ToList();
        foreach (var p in snapshot)
        {
            if (ReferenceEquals(p, exclude) || !p.Connection.IsConnected) continue;
            try { p.Connection.Send(packet); }
            catch (Exception e) { Log.Warning($"Broadcast failed player={p.Profile.Name} {e.Message}"); }
        }
    }
}
