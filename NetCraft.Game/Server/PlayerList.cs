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

//PlayerList online player set management, maps to vanilla PlayerList
//Manages the ServerPlayer lifecycle, providing the PlaceNewPlayer/RemovePlayer/Broadcast APIs
//Rejects new players when full, maps to the vanilla max-players limit
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

    //PlaceNewPlayer creates a ServerPlayer, adds it to the player list and sends the Clientbound packet sequence needed to enter the world
    //Returns null when full; the caller should send a disconnect packet and disconnect
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
            //The tab player list must announce existing players to the new player; take a snapshot first since the new player is not in the list yet
            others = new List<ServerPlayer>(_players);
            player = new ServerPlayer(profile, connection, _server.Overworld);
            //The reverse reference on the player list; the item pickup sound must broadcast to the whole server, maps to the level/players the vanilla ServerPlayer gets
            player.OwnerList = this;
            //Applies the server default game type from settings.gamemode
            player.GameType = _server.DefaultGameType;
            //The permission level is decided by the ops.json list; not on the list means level 0
            player.PermissionLevel = _server.OpList.GetPermissionLevel(profile);
            //The player save takes precedence over defaults; position/facing/health/xp/inventory must be in place before the join packet is sent
            restored = _server.PlayerData.LoadInto(player);
            //Entity tracking decides visibility by player view distance; take the server configuration
            player.ViewDistanceChunks = _server.Settings.ViewDistance;
            _players.Add(player);
        }
        Log.Info(restored
            ? $"Player joined (save restored) {profile.Name} entityId={player.EntityId} online {PlayerCount}/{MaxPlayers}"
            : $"Player joined {profile.Name} entityId={player.EntityId} online {PlayerCount}/{MaxPlayers}");
        //The inventory menu is built before entering the world; initial content goes out with SendJoinPackets
        player.SetUpInventoryMenu();
        SendJoinPackets(player);
        //Tab player list: the new player receives all existing players and all online players including itself receive the new player, maps to vanilla PlayerList.addPlayer
        //Without this step the client tab only has itself and everyone else is invisible; selectors can still hit the server player list by name
        if (others.Count > 0)
            player.Connection.Send(new ClientboundPlayerInfoUpdatePacket(
                PlayerInfoActions, others.Select(EntryOf).ToList()));
        BroadcastAll(new ClientboundPlayerInfoUpdatePacket(PlayerInfoActions, new[] { EntryOf(player) }));
        //Join message, maps to the yellow multiplayer.player.joined in vanilla PlayerList.addPlayer
        BroadcastSystemMessage(
            Component.Translatable("multiplayer.player.joined", Component.Literal(profile.Name))
                .WithStyle(ChatFormatting.Yellow),
            false);
        return player;
    }

    //PlayerInfoActions the action set for sending player info, maps to vanilla createPlayerInitializing
    //Excludes InitializeChat (needs a chat session signature) and the hat/list_order display items added in 26.2
    private const int PlayerInfoActions = (1 << (int)PlayerInfoAction.AddPlayer)
        | (1 << (int)PlayerInfoAction.UpdateGameMode)
        | (1 << (int)PlayerInfoAction.UpdateListed)
        | (1 << (int)PlayerInfoAction.UpdateLatency)
        | (1 << (int)PlayerInfoAction.UpdateDisplayName);

    //EntryOf player info entry; latency is written as 0 first since ping is not yet measured
    private static PlayerInfoEntry EntryOf(ServerPlayer player)
        => new(player.Profile.Id, player.Profile.Name, player.GameType, true, 0, null);

    //SendJoinPackets sends the Clientbound packet sequence for placeNewPlayer
    //1.PlayerInfoUpdate(self) 2.LoginPacket(with SpawnInfo) 3.PlayerAbilities 4.SetHeldSlot
    //5.PlayerPosition(sync spawn) 6.SetDefaultSpawnPosition 7.SetHealth/SetExperience 8.SetChunkCacheRadius
    //9.LEVEL_CHUNKS_LOAD_START+UpdateCenter queues view-distance chunks, sent progressively per tick by ChunkSender
    private void SendJoinPackets(ServerPlayer player)
    {
        var gameType = player.GameType;
        var connection = player.Connection;
        var viewDistance = _server.Settings.ViewDistance;
        try
        {
            //PlayerInfoUpdate self, with the same action set as the later broadcast
            connection.Send(new ClientboundPlayerInfoUpdatePacket(PlayerInfoActions, new[] { EntryOf(player) }));
            //LoginPacket with the real CommonPlayerSpawnInfo including dimension/seed/game type
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
            //PlayerAbilities sent by player ability state, with flying from the save, so creative stays flying after rejoin instead of starting on the ground each time
            var abilities = player.Abilities;
            connection.Send(new ClientboundPlayerAbilitiesPacket(
                abilities.Invulnerable, abilities.Flying, abilities.MayFly, abilities.Instabuild,
                abilities.FlyingSpeed, abilities.WalkingSpeed));
            //SetHeldSlot restores the selected slot; 0 when there is no save
            connection.Send(new ClientboundSetHeldSlotPacket(player.Inventory.SelectedSlot));
            //ContainerSetContent the initial inventory menu content, from which the client builds the inventory
            player.ContainerMenu?.SendAllDataToRemote();
            //PlayerPosition syncs the player spawn position
            connection.Send(new ClientboundPlayerPositionPacket(
                player.Position.X, player.Position.Y, player.Position.Z,
                player.Yaw, player.Pitch, 0, 0));
            //InitializeBorder the initial border state, from which the client renders the border wall, maps to the first entry of vanilla sendLevelInfo
            var border = player.Level.WorldBorder;
            connection.Send(new ClientboundInitializeBorderPacket(
                border.CenterX, border.CenterZ, border.GetSize(), border.GetLerpTarget(),
                border.GetLerpTime(), border.AbsoluteMaxSize, border.WarningBlocks, border.WarningTime));
            //SetDefaultSpawnPosition the world spawn; both the config-phase preload and player spawn center on it
            connection.Send(new ClientboundSetDefaultSpawnPositionPacket(
                new BlockPos((int)_server.SpawnPos.X, (int)_server.SpawnPos.Y, (int)_server.SpawnPos.Z), 0f));
            //Weather state: when it is raining, resend the rain start and rain/thunder levels on join, maps to the isRaining branch of vanilla sendLevelInfo
            var weatherLevel = player.Level;
            if (weatherLevel.IsRaining)
            {
                connection.Send(new ClientboundGameEventPacket(GameEventType.StartRaining, 0f));
                connection.Send(new ClientboundGameEventPacket(GameEventType.RainLevelChange, weatherLevel.GetRainLevel(1f)));
                connection.Send(new ClientboundGameEventPacket(GameEventType.ThunderLevelChange, weatherLevel.GetThunderLevel(1f)));
            }
            //SetTime syncs the full clock state on join, or the client runs its own from local 0 and diverges from the server
            connection.Send(_server.ClockManager.CreateFullSyncPacket());
            //SetHealth/SetExperience from the restored health and xp; no save means full health and zero xp
            connection.Send(new ClientboundSetHealthPacket(player.Health, 20, 5f));
            connection.Send(new ClientboundSetExperiencePacket(player.XpProgress, player.XpTotal, player.XpLevel));
            //SetChunkCacheRadius view distance
            connection.Send(new ClientboundSetChunkCacheRadiusPacket(viewDistance));
            //Permission level; without this event the client thinks it is level 0 and the F3+F4 entries are blocked by the client itself
            SendPlayerPermissionLevel(player);
            var chunkX = (int)Math.Floor(player.Position.X / 16);
            var chunkZ = (int)Math.Floor(player.Position.Z / 16);
            //LEVEL_CHUNKS_LOAD_START tells the client chunks are about to be sent
            //The client LevelLoadTracker initially waits for the server; without this event it is stuck loading terrain forever
            connection.Send(new ClientboundGameEventPacket(GameEventType.LevelChunksLoadStart, 0f));
            //Non-blocking chunk fetch; not-ready coordinates stay in the pending set for the next tick
            //Generation advances in the ServerChunkCache background without blocking the main loop
            //UpdateCenter sends SetChunkCacheCenter and queues the initial view; after the player crosses a chunk ServerPlayer.Tick keeps updating it
            player.ChunkSender = new ChunkSender(
                connection,
                pos => _server.Overworld.GetChunk(pos),
                pos => _server.Overworld.IsChunkFailed(pos),
                _server.Overworld.ChunkSource,
                _server.Overworld.BlockEntityBridge);
            player.ChunkSender.UpdateCenter(chunkX, chunkZ, viewDistance);
            //Tick rate state: if the world is frozen the client must know immediately, or the local world advances on its own, maps to vanilla updateJoiningPlayer
            _server.TickRate.SendStateToJoiningPlayer(player);
            //ClientboundCommands the command tree, from which the client parses the player's slash commands
            //Placed after the join packets so an encoding exception does not block the position/health/block chain
            _server.Commands.SendCommands(player);
            Log.Debug($"Join packets sent {player.Profile.Name} pending chunks {player.ChunkSender.PendingCount}");
        }
        catch (Exception e)
        {
            Log.Error($"Join packets failed {player.Profile.Name} {e.Message}");
        }
    }

    //SendPlayerPermissionLevel tells the player's client its permission level
    //Vanilla uses entity event 24+level; the client uses it to decide whether permission-gated entries such as F3+F4 are usable
    public void SendPlayerPermissionLevel(ServerPlayer player)
    {
        if (!player.Connection.IsConnected) return;
        var level = Math.Clamp(player.PermissionLevel, 0, 4);
        player.Connection.Send(new ClientboundEntityEventPacket(player.EntityId, (byte)(24 + level)));
    }

    //ApplyPermissionLevel updates the player's permission level and syncs the client, called by the op/deop commands
    public void ApplyPermissionLevel(ServerPlayer player, int level)
    {
        player.PermissionLevel = Math.Clamp(level, 0, 4);
        SendPlayerPermissionLevel(player);
    }

    //ChangeGameMode switches the player's game type and syncs the client, maps to vanilla ServerPlayer.setGameMode
    //Order: set mode -> abilities packet -> game event -> broadcast the game mode to the whole player list
    //Both the gamemode command and the client change_gamemode packet go through here, keeping the two paths consistent
    public void ChangeGameMode(ServerPlayer player, GameType gameType)
    {
        if (player.GameType == gameType) return;
        player.GameType = gameType;
        //When leaving spectator mode the camera returns to the player, maps to setCamera(this) in vanilla ServerPlayer.setGameMode
        if (gameType != GameType.Spectator) player.SetCamera(null);
        //Assigning GameType above already recomputed the abilities for the new mode; this sends them as is: spectator forces flight and survival revokes it
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

    //GetPlayerByName looks up an online player by name, case-insensitive, for /op /deop to resolve targets
    public ServerPlayer? GetPlayerByName(string name)
    {
        lock (_lock)
            return _players.FirstOrDefault(p => string.Equals(p.Profile.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    //GetPlayerByEntityId looks up an online player by entity id, used to resolve targets of the attack and interact packets
    //Players are not in the level entity manager, so finding a player by id can only query here
    public ServerPlayer? GetPlayerByEntityId(int entityId)
    {
        lock (_lock)
            return _players.FirstOrDefault(p => p.EntityId == entityId);
    }

    //CanPlayerLogin login admission check, maps to vanilla PlayerList.canPlayerLogin
    //IP bans are checked first then player bans; on a hit it sends a disconnect packet and closes the connection and the caller does not let it into the world
    //The call site must be after the outbound protocol switches to Play, or the disconnect packet cannot be encoded
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
        //With the whitelist on, non-members are rejected; operators are exempt, maps to the isUsingWhitelist branch of vanilla
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

    //HurtPlayer deals damage to a player and syncs health and the hurt animation, maps to vanilla ServerPlayer.hurtServer
    //Returns whether damage was actually dealt; both invulnerability frames and already dead return false
    public bool HurtPlayer(ServerPlayer target, ServerPlayer? attacker, float amount)
    {
        if (!target.Hurt(amount)) return false;
        SyncHealth(target);
        //Vanilla sends the hurt animation only to trackers; this project does no range-limited hurt packet and broadcasts to the whole server
        BroadcastAll(new ClientboundHurtAnimationPacket(target.EntityId, target.Yaw));
        //The hurt sound is played to everyone at the player's position, maps to vanilla LivingEntity.playHurtSound
        ServerSounds.PlaySound(this, SoundEvents.PlayerHurt, SoundSource.Players,
            target.Position.X, target.Position.Y, target.Position.Z, 1f, 1f);
        if (target.IsDeadOrDying) RespawnPlayer(target, attacker);
        return true;
    }

    //SyncHealth syncs the player's health to itself; the client health bar refreshes from it
    public void SyncHealth(ServerPlayer player)
    {
        if (!player.Connection.IsConnected) return;
        player.Connection.Send(new ClientboundSetHealthPacket(player.Health, 20, 5f));
    }

    //RespawnPlayer handles player death: broadcasts the death message then resets to full health at the spawn point
    //Vanilla goes through ClientboundPlayerCombatKill plus a client respawn handshake; this project's client has no death screen so it resets directly
    private void RespawnPlayer(ServerPlayer player, ServerPlayer? attacker)
    {
        BroadcastSystemMessage(
            attacker is null
                ? Component.Translatable("death.attack.generic", Component.Literal(player.Profile.Name))
                : Component.Translatable("death.attack.player", Component.Literal(player.Profile.Name),
                    Component.Literal(attacker.Profile.Name)),
            false);
        Log.Info($"Player died {player.Profile.Name} killer={attacker?.Profile.Name ?? "none"}");
        //The death sound is played before the reset, using the death position, maps to vanilla LivingEntity.playDeathSound
        ServerSounds.PlaySound(this, SoundEvents.PlayerDeath, SoundSource.Players,
            player.Position.X, player.Position.Y, player.Position.Z, 1f, 1f);
        player.Velocity = Vec3.Zero;
        //Respawn full health is taken from the attribute, maps to vanilla setHealth(getMaxHealth())
        player.SetHealth(player.MaxHealth);
        //With a personal respawn point it returns there, otherwise to the world spawn, maps to vanilla ServerPlayer's respawnPosition
        player.Position = player.RespawnPos ?? _server.SpawnPos;
        SyncHealth(player);
        if (!player.Connection.IsConnected) return;
        //The position reset goes through PlayerPosition; the client replies accept_teleportation to correct local prediction
        player.Connection.Send(new ClientboundPlayerPositionPacket(
            player.Position.X, player.Position.Y, player.Position.Z, player.Yaw, player.Pitch, 0, 0));
    }

    //RemovePlayer removes a player and returns whether it succeeded
    //After removal it broadcasts the leave message, maps to multiplayer.player.left in vanilla removePlayerFromWorld
    public bool RemovePlayer(ServerPlayer player)
    {
        bool removed;
        lock (_lock) removed = _players.Remove(player);
        if (removed)
        {
            //Broadcast the entity removal first; other clients' models are removed by this packet
            //Must happen before ForgetPlayer; ForgetPlayer clears the observation records and PruneStale can no longer send this packet
            BroadcastAll(new ClientboundRemoveEntitiesPacket(new[] { player.EntityId }));
            //The visibility records are maintained per player lifecycle; when a player leaves its related table entries must be dropped or they stay in the tracker
            _server.EntityTracker.ForgetPlayer(player);
            //Remove the player from the tab list, maps to broadcastAll(ClientboundPlayerInfoRemovePacket) in vanilla PlayerList.remove
            BroadcastAll(new ClientboundPlayerInfoRemovePacket(new[] { player.Profile.Id }));
            BroadcastSystemMessage(
                Component.Translatable("multiplayer.player.left", Component.Literal(player.Profile.Name))
                    .WithStyle(ChatFormatting.Yellow),
                false);
            Log.Info($"Player removed {player.Profile.Name} online {PlayerCount}/{MaxPlayers}");
        }
        return removed;
    }

    //BroadcastSystemMessage broadcasts a system message to all online players, maps to vanilla PlayerList.broadcastSystemMessage
    //Also logs one to the server log; the console and GUI only see chat from this
    //Commands such as say/emote with no online players to send to go completely silent without the log
    public void BroadcastSystemMessage(Component message, bool overlay)
    {
        Log.Info($"[Chat] {message.GetString()}");
        BroadcastAll(new ClientboundSystemChatPacket(message, overlay));
    }

    //BroadcastAll sends a packet to all online players
    //After a kick/ban the player is removed only in handleDisconnection; skip disconnected connections here to avoid warning spam
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

    //BroadcastAllExcept sends a packet to all online players except the given one
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
