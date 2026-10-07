using NetCraft.Game.Commands;
using NetCraft.Network.Protocol.Common;
using NetCraft.Network.Protocol.Cookie;
using NetCraft.Network.Protocol.Login;
using NetCraft.Network.Protocol.Ping;
using NetCraft.Game.Server;
using NetCraft.Game.World.Inventory;
using NetCraft.Game.World.Items;
using NetCraft.Game.World.Level;
using NetCraft.Game.World.Level.Block;
using NetCraft.Logging;
using NetCraft.Network;
using NetCraft.Network.Chat;
using NetCraft.Network.Component;
using NetCraft.Network.Protocol;
using NetCraft.Primitives;
using NetCraft.Storage;
using NetCraft.Util;
//Aliases avoid the Entity/Block stubs in NetCraft.Registry clashing with the business types
using SoundEvents = NetCraft.Registry.SoundEvents;
using SoundSource = NetCraft.Registry.SoundSource;
using BlockState = NetCraft.Registry.State.BlockState;

namespace NetCraft.Game.Network.Protocol.Game;

//ServerGamePacketListenerImpl server play phase listener implementation
//Maps to vanilla ServerGamePacketListenerImpl
//Core registered packets such as HandleChat/HandleMovePlayer/HandleClientCommand/HandleAcceptTeleportation log at Debug
//The remaining methods inherited from ServerCommonPacketListener/ServerCookiePacketListener/ServerPingPacketListener are empty for now
//Protocol explicitly returns Play because the inherited default is Configuration
public sealed class ServerGamePacketListenerImpl : ServerGamePacketListener, TickablePacketListener
{
    private readonly Connection _connection;
    private readonly GameProfile _profile;
    //_ackBlockChangesUpTo block change sequence pending acknowledgment, -1 means none, maps to vanilla ackBlockChangesUpTo
    private int _ackBlockChangesUpTo = -1;
    //_tickCount the listener's own tick count, maps to vanilla ServerGamePacketListenerImpl.tickCount
    private int _tickCount;
    //_awaitingTeleport teleport id awaiting client acknowledgment, -1 means none, maps to vanilla awaitingTeleport
    private int _awaitingTeleport = -1;
    //_awaitingPositionFromClient target position awaiting acknowledgment, maps to vanilla awaitingPositionFromClient
    //While non-null, client-reported coordinates are all treated as in-flight packets from before the teleport and the position is not adopted
    private Vec3? _awaitingPositionFromClient;
    //_awaitingTeleportTime tick of the last position packet sent; if unacknowledged for over 20 ticks it is resent, maps to vanilla awaitingTeleportTime
    private int _awaitingTeleportTime;
    //_isDestroyingBlock whether a block is being mined, maps to vanilla isDestroyingBlock
    private bool _isDestroyingBlock;
    //_destroyPos current mining target, maps to vanilla destroyPos
    private BlockPos _destroyPos = BlockPos.Zero;
    //_destroyProgressStart start tick of this mining; progress is computed from its difference with the current tick, maps to vanilla destroyProgressStart
    private int _destroyProgressStart;
    //_lastSentDestroyState last crack stage sent, 0-10; the stage is not resent when unchanged, maps to vanilla lastSentState
    private int _lastSentDestroyState = -1;
    //_hasDelayedDestroy a pending finish-destroy for when the client has released but the server-estimated progress is not yet reached, maps to vanilla hasDelayedDestroy
    private bool _hasDelayedDestroy;
    private BlockPos _delayedDestroyPos = BlockPos.Zero;
    private int _delayedDestroyTickStart;

    public ServerGamePacketListenerImpl(Connection connection, GameProfile profile)
    {
        _connection = connection;
        _profile = profile;
    }

    //Explicitly returns Play because ServerCookiePacketListener's default Protocol is Configuration
    ConnectionProtocol PacketListener.Protocol => ConnectionProtocol.Play;

    //TickListener acks the accumulated block change sequence to the client every tick, maps to vanilla ServerGamePacketListenerImpl.tick
    //The client uses it to end its local prediction; without the ack, predicted blocks stay on the client until the chunk is reloaded
    public void TickListener()
    {
        _tickCount++;
        //If the teleport stays unacknowledged it is resent in place, maps to vanilla updateAwaitingTeleport
        if (_awaitingPositionFromClient is not null && _tickCount - _awaitingTeleportTime > AwaitingTeleportTimeoutTicks)
            ResendTeleport();
        TickDestroyProgress();
        if (_ackBlockChangesUpTo < 0) return;
        _connection.Send(new ClientboundBlockChangedAckPacket(_ackBlockChangesUpTo));
        _ackBlockChangesUpTo = -1;
    }

    //AwaitingTeleportTimeoutTicks teleport resend interval, maps to vanilla 20 ticks
    private const int AwaitingTeleportTimeoutTicks = 20;

    //Teleport initiates a teleport from the server, maps to vanilla ServerGamePacketListenerImpl.teleport
    //target/yaw/pitch are the converted absolute values; they are applied to the player state and recorded as the position awaiting acknowledgment
    //Relative components are sent in the packet; the client adds them to its own current values per relatives to recover the absolute position
    //During the wait, stale client-reported coordinates are no longer adopted, so in-flight packets cannot push the player back to the pre-teleport position
    public void Teleport(Vec3 target, float yaw, float pitch,
        double relX, double relY, double relZ, float relYaw, float relPitch, int relatives)
    {
        var player = Player;
        if (player is null) return;
        player.Position = target;
        player.Yaw = yaw;
        player.Pitch = pitch;
        _awaitingPositionFromClient = target;
        _awaitingTeleport = NextTeleportId();
        SendPositionPacket(relX, relY, relZ, relYaw, relPitch, relatives);
    }

    //ResendTeleport timeout resend, maps to the resend branch of vanilla updateAwaitingTeleport
    //The resend uses absolute values with relatives cleared, so the client snaps directly to the target position using its own current rotation
    private void ResendTeleport()
    {
        var player = Player;
        if (player is null || _awaitingPositionFromClient is not { } target) return;
        _awaitingTeleport = NextTeleportId();
        SendPositionPacket(target.X, target.Y, target.Z, player.Yaw, player.Pitch, 0);
    }

    //NextTeleportId increments the teleport id, wrapping to 0 on overflow, matching the vanilla teleport value
    private int NextTeleportId()
        => _awaitingTeleport == int.MaxValue ? 0 : _awaitingTeleport + 1;

    //SendPositionPacket sends the position packet and records the send tick
    private void SendPositionPacket(double x, double y, double z, float yRot, float xRot, int relatives)
    {
        _awaitingTeleportTime = _tickCount;
        _connection.Send(new ClientboundPlayerPositionPacket(x, y, z, yRot, xRot, relatives, _awaitingTeleport));
    }

    //AckBlockChanges records the maximum pending block change sequence, maps to vanilla ackBlockChangesUpTo
    private void AckBlockChanges(int sequence)
    {
        if (sequence > _ackBlockChangesUpTo) _ackBlockChangesUpTo = sequence;
    }

    //Player the associated player object, injected by DedicatedServer.TransitionToGame
    //Used to apply client response packets to the player state
    public ServerPlayer? Player { get; set; }

    //Players player list, injected by DedicatedServer.TransitionToGame
    //Block changes must be broadcast to online players
    public PlayerList? Players { get; set; }

    //BlockEntities block entity collection, injected by DedicatedServer.TransitionToGame
    //Old block entities must be cleaned up in sync when a block is replaced or destroyed
    public BlockEntityManager? BlockEntities { get; set; }

    //Commands command manager, injected by DedicatedServer.TransitionToGame
    public CommandManager? Commands { get; set; }

    //HandlePlayerAction player action, maps to vanilla handlePlayerAction
    //START branch: creative mode is instant break and the client does not send STOP after breaking, so it must be handled here
    //Survival first computes progress once; blocks with progress >= 1 break immediately, otherwise a per-tick mining state is entered
    //The ABORT/STOP branches clear the state and finish up like vanilla; destroy progress packets are broadcast uniformly by this class
    public void HandlePlayerAction(ServerboundPlayerActionPacket packet)
    {
        if (Player is null || Players is null) return;
        if (Player.Level is not PersistentServerLevel level) return;
        //Vanilla records an ack for all three destroy actions, sent uniformly by TickListener
        AckBlockChanges(packet.Sequence);
        switch (packet.Action)
        {
            case ServerboundPlayerActionPacket.ActionType.StartDestroyBlock:
                StartDestroyBlock(level, packet.Pos);
                return;
            case ServerboundPlayerActionPacket.ActionType.StopDestroyBlock:
                StopDestroyBlock(level, packet.Pos);
                return;
            case ServerboundPlayerActionPacket.ActionType.AbortDestroyBlock:
                AbortDestroyBlock(level, packet.Pos);
                return;
            case ServerboundPlayerActionPacket.ActionType.DropItem:
            {
                var drop = Player.Drop(false);
                Log.Debug($"Drop item single profile={_profile.Name} drop={drop?.EntityId}");
                return;
            }
            case ServerboundPlayerActionPacket.ActionType.DropAllItems:
            {
                var drop = Player.Drop(true);
                Log.Debug($"Drop item stack profile={_profile.Name} drop={drop?.EntityId}");
                return;
            }
        }
    }

    //StartDestroyBlock starts breaking a block, maps to the START branch of vanilla handleBlockBreakAction
    //Creative breaks instantly; survival records the target into the mining state advanced each tick by TickDestroyProgress
    //Instant blocks (progress >= 1 in one step) break directly without sending crack packets
    private void StartDestroyBlock(PersistentServerLevel level, BlockPos pos)
    {
        var player = Player!;
        //Vanilla gives the block an attack callback at the moment mining starts; the note block relies on it for left-click audition
        if (level.GetBlockState(pos) is { Owner: BlockBehaviour behaviour } attacked)
            behaviour.OnAttack(level, player, pos, attacked);
        if (player.GameType == GameType.Creative)
        {
            var broken = ServerBlockUpdates.BreakBlock(level, Players!, player, pos);
            Log.Debug($"Break block (creative) {pos} result={broken} profile={_profile.Name}");
            return;
        }
        _destroyProgressStart = _tickCount;
        var state = level.GetBlockState(pos);
        //An empty position is treated as progress 1 like vanilla and does not enter the mining state
        var progress = 1f;
        if (state is not null && state.Value != Blocks.AIR.DefaultBlockState)
            progress = BlockBehaviour.GetDestroyProgress(state.Value);
        if (state is not null && state.Value != Blocks.AIR.DefaultBlockState && progress >= 1f)
        {
            DestroyBlockAndAck(pos);
            return;
        }
        //The target changed but the previous one was not finished; clear the old position's cracks first so they do not linger on other players' screens
        if (_isDestroyingBlock) BroadcastDestroyProgress(level, _destroyPos, -1);
        _isDestroyingBlock = true;
        _destroyPos = pos;
        var stage = (int)(progress * 10f);
        BroadcastDestroyProgress(level, pos, stage);
        _lastSentDestroyState = stage;
    }

    //StopDestroyBlock client released, maps to the STOP branch of vanilla handleBlockBreakAction
    //If the target is not the one being mined it is ignored; at a server-estimated progress of 0.7 it breaks directly
    //Below 0.7 a delayed destroy is parked and finished over later ticks, maps to vanilla hasDelayedDestroy
    private void StopDestroyBlock(PersistentServerLevel level, BlockPos pos)
    {
        if (!_isDestroyingBlock || _destroyPos != pos) return;
        var state = level.GetBlockState(pos);
        if (state is null || state.Value == Blocks.AIR.DefaultBlockState) return;
        var ticksSpent = _tickCount - _destroyProgressStart;
        var progress = BlockBehaviour.GetDestroyProgress(state.Value) * (ticksSpent + 1);
        if (progress >= 0.7f)
        {
            _isDestroyingBlock = false;
            BroadcastDestroyProgress(level, pos, -1);
            DestroyBlockAndAck(pos);
            return;
        }
        if (_hasDelayedDestroy) return;
        _isDestroyingBlock = false;
        _hasDelayedDestroy = true;
        _delayedDestroyPos = pos;
        _delayedDestroyTickStart = _destroyProgressStart;
    }

    //AbortDestroyBlock aborts mining, maps to the ABORT branch of vanilla handleBlockBreakAction
    //Looking away mid-mine goes here; it clears the cracks it owns, and also clears the old position's when the target does not match
    private void AbortDestroyBlock(PersistentServerLevel level, BlockPos pos)
    {
        _isDestroyingBlock = false;
        if (_destroyPos != pos) BroadcastDestroyProgress(level, _destroyPos, -1);
        BroadcastDestroyProgress(level, pos, -1);
        _lastSentDestroyState = -1;
    }

    //TickDestroyProgress advances mining progress every tick, maps to vanilla ServerPlayerGameMode.tick
    //Delayed destroy branch: the client has released, so finish the remainder the server considers still owed
    //Normal branch: if the block is replaced by air mid-way, cancel and clear the cracks; otherwise accumulate progress and broadcast the crack stage
    private void TickDestroyProgress()
    {
        if (Player is null) return;
        if (Player.Level is not PersistentServerLevel level) return;
        if (_hasDelayedDestroy)
        {
            var delayedState = level.GetBlockState(_delayedDestroyPos);
            if (delayedState is null || delayedState.Value == Blocks.AIR.DefaultBlockState)
            {
                _hasDelayedDestroy = false;
                return;
            }
            if (IncrementDestroyProgress(level, delayedState.Value, _delayedDestroyPos, _delayedDestroyTickStart) >= 1f)
            {
                _hasDelayedDestroy = false;
                DestroyBlockAndAck(_delayedDestroyPos);
            }
            return;
        }
        if (!_isDestroyingBlock) return;
        var state = level.GetBlockState(_destroyPos);
        if (state is null || state.Value == Blocks.AIR.DefaultBlockState)
        {
            BroadcastDestroyProgress(level, _destroyPos, -1);
            _lastSentDestroyState = -1;
            _isDestroyingBlock = false;
            return;
        }
        IncrementDestroyProgress(level, state.Value, _destroyPos, _destroyProgressStart);
    }

    //IncrementDestroyProgress accumulates destroy progress and returns the total, maps to vanilla incrementDestroyProgress
    //Progress is the per-tick increment times ticks spent; the crack stage is an integer 0-10 and is not resent when unchanged
    private float IncrementDestroyProgress(PersistentServerLevel level, BlockState state, BlockPos pos, int startTick)
    {
        var ticksSpent = _tickCount - startTick;
        var progress = BlockBehaviour.GetDestroyProgress(state) * (ticksSpent + 1);
        var stage = (int)(progress * 10f);
        if (stage != _lastSentDestroyState)
        {
            BroadcastDestroyProgress(level, pos, stage);
            _lastSentDestroyState = stage;
        }
        return progress;
    }

    //BroadcastDestroyProgress broadcasts block destroy progress, maps to vanilla ServerLevel.destroyBlockProgress
    //The breaker does not receive it; the client has a local prediction animation for the block it is mining and receiving it would conflict
    //Only sent to same-dimension players within 32 blocks; further away the cracks are not visible
    private void BroadcastDestroyProgress(PersistentServerLevel level, BlockPos pos, int progress)
    {
        if (Players is null || Player is null) return;
        var packet = new ClientboundBlockDestructionPacket(Player.EntityId, pos, progress);
        foreach (var other in Players.Players)
        {
            if (ReferenceEquals(other, Player) || other.Level != level) continue;
            var dx = pos.X - other.Position.X;
            var dy = pos.Y - other.Position.Y;
            var dz = pos.Z - other.Position.Z;
            if (dx * dx + dy * dy + dz * dz >= 1024.0) continue;
            other.Connection.Send(packet);
        }
    }

    //DestroyBlockAndAck destroys the block and syncs the failure result back to the client, maps to vanilla destroyAndAck
    //If the destroy did not take effect (e.g. the block was already replaced) the true server state is sent back, ending the client's local prediction
    private void DestroyBlockAndAck(BlockPos pos)
    {
        var player = Player;
        if (player is null || Players is null) return;
        if (player.Level is not PersistentServerLevel level) return;
        if (ServerBlockUpdates.BreakBlock(level, Players, player, pos)) return;
        var state = level.GetBlockState(pos);
        if (state is not null) player.Connection.Send(new ClientboundBlockUpdatePacket(pos, state.Value.Id));
    }

    //HandleUseItemOn player using on a block; first handed to the block's own behavior, then to held block placement if unhandled
    public void HandleUseItemOn(ServerboundUseItemOnPacket packet)
    {
        if (Player is null || Players is null) return;
        if (Player.Level is not PersistentServerLevel level) return;
        //Vanilla records the ack before entering the placement flow; the client prediction must end regardless of success
        AckBlockChanges(packet.Sequence);
        var pos = packet.BlockHit.BlockPos;
        var face = packet.BlockHit.Direction;
        //Out-of-range build height only shows an action bar message and does not take effect, maps to the maxY/minY checks in vanilla handleUseItemOn
        //The upper bound is 319 and the lower bound is -64, matching the height shown by the client's F3
        var maxY = level.MaxBuildHeight - 1;
        var minY = level.MinBuildHeight;
        if (pos.Y > maxY)
        {
            Player.SendBuildLimitMessage(true, maxY);
            return;
        }
        if (pos.Y < minY)
        {
            Player.SendBuildLimitMessage(false, minY);
            return;
        }
        //Vanilla order: block's own behavior -> item useOn -> block item placement
        //When sneaking and holding something, the block's own behavior is skipped, maps to vanilla suppressUsingBlock
        //Otherwise sneaking to press a button or open a chest would still trigger it; vanilla then only uses the item and places
        var haveSomethingInOurHands = !Player.Inventory.GetSelectedItem().IsEmpty()
            || !Player.Inventory.GetItem(PlayerInventory.OffhandSlot).IsEmpty();
        var suppressUsingBlock = Player.IsSneaking && haveSomethingInOurHands;
        var handled = !suppressUsingBlock && ServerBlockUpdates.UseOn(level, Player, pos, face);
        if (!handled) handled = ServerBlockUpdates.UseItemOn(level, Player, pos, face, packet.Hand);
        if (!handled)
        {
            var hit = packet.BlockHit.Location;
            handled = ServerBlockUpdates.PlaceHeldBlock(level, Players, Player, pos, face, packet.Hand,
                new NetCraft.Primitives.Vec3(hit.X - pos.X, hit.Y - pos.Y, hit.Z - pos.Z));
            //The facing of the attached block is entirely determined by this cell's state; if the client display does not match, the problem is in state encoding or the packet sent
            var placePos = pos.Offset(face);
            if (handled && level.GetBlockState(placePos) is { } placed)
                Log.Debug($"Place result {placed.Owner.Id}[{placed.Id}] pos={placePos} face={face} yaw={Player.Yaw} properties={string.Join(",", placed.GetValues().Select(pv => $"{pv.Property.Name}={pv.Value}"))}");
        }
        //Placement failed while hugging the bounds means the height is insufficient, maps to the two messages after vanilla wasBlockPlacementAttempt
        if (!handled && face == Direction.Up && pos.Y >= maxY) Player.SendBuildLimitMessage(true, maxY);
        else if (!handled && face == Direction.Down && pos.Y <= minY) Player.SendBuildLimitMessage(false, minY);
        Log.Debug($"Use block {pos} handled={handled} profile={_profile.Name}");
    }

    //HandleChat player chat broadcast, maps to vanilla handleChat
    //Not wired into the signing chain; broadcasts chat.type.text via system_chat, displaying the same as vanilla player chat
    public void HandleChat(ServerboundChatPacket packet)
    {
        if (Player is null || Players is null) return;
        var message = packet.Message;
        if (string.IsNullOrEmpty(message)) return;
        //Vanilla writeUtf(message,256) should fail at the client encoding stage when too long; this blocks it once more
        if (message.Length > ServerboundChatPacket.MaxMessageLength) return;
        var name = Player.Profile.Name;
        Log.Info($"Chat {name}: {message}");
        Players.BroadcastSystemMessage(CreateChatMessage(name, message), false);
    }

    //CreateChatMessage builds the player chat component, maps to the chat.type.text decoration of vanilla ChatType.bind(CHAT,player)
    public static Component CreateChatMessage(string senderName, string message)
        => Component.Translatable("chat.type.text", Component.Literal(senderName), Component.Literal(message));

    //HandleMovePlayer player movement packet; syncs coordinates and rotation to ServerPlayer
    //Player coordinates are client-authoritative and the server only writes them back; view updates from crossing chunks are detected by ServerPlayer.Tick
    //On-ground must also be written back; entity tracking uses it to decide whether to send an onGround change packet
    //While a teleport awaits acknowledgment only rotation is accepted; client-reported coordinates are still in-flight values from before the teleport
    //Adopting them would push the player back to the old coordinates and entity tracking would immediately broadcast the old position, leaving observers' models stuck at the old spot
    public void HandleMovePlayer(ServerboundMovePlayerPacket packet)
    {
        var player = Player;
        if (player is null) return;
        if (_awaitingPositionFromClient is not null)
        {
            if (packet.HasRot)
            {
                player.Yaw = packet.YRot;
                player.Pitch = packet.XRot;
            }
            return;
        }
        if (packet.HasPos) player.Position = new Vec3(packet.X, packet.Y, packet.Z);
        if (packet.HasRot)
        {
            player.Yaw = packet.YRot;
            player.Pitch = packet.XRot;
        }
        player.OnGround = packet.OnGround;
    }

    //HandleClientCommand client command packet such as requesting respawn, logs at Debug
    public void HandleClientCommand(ServerboundClientCommandPacket packet)
    {
        Log.Debug($"HandleClientCommand profile={_profile.Name}");
    }

    //HandleAcceptTeleportPacket client acknowledges the teleport, maps to vanilla handleAcceptTeleportation
    //Only a matching id snaps the player to the awaited target position and clears the wait; afterwards reported positions are adopted again
    //A mismatched id means the ack belongs to a different teleport round; the wait is kept and TickListener resends
    public void HandleAcceptTeleportPacket(ServerboundAcceptTeleportationPacket packet)
    {
        var player = Player;
        if (player is null || _awaitingPositionFromClient is not { } target) return;
        if (packet.Id != _awaitingTeleport)
        {
            //On mismatch the player stays in the waiting state and every reported movement position is discarded
            //Every 20 ticks the resent position packet yanks them back to the target with absolute values, so any movement after the teleport pulls them back
            //This warning is the only entry point for that symptom; if none appears, the ack never reached the server
            Log.Warning($"Teleport ack id mismatch received={packet.Id} waiting={_awaitingTeleport} profile={_profile.Name}");
            return;
        }
        player.Position = target;
        _awaitingPositionFromClient = null;
        _awaitingTeleport = -1;
    }

    //HandleAnimate swing animation, maps to the broadcast branch of vanilla handleAnimate
    //Sends animate to other players; the client already played its own swing locally so it is not echoed
    public void HandleAnimate(ServerboundSwingPacket packet)
    {
        var player = Player;
        if (player is null || Players is null) return;
        Players.BroadcastAllExcept(player,
            new ClientboundAnimatePacket(player.EntityId, AnimateAction(packet.Hand)));
    }

    //AnimateAction swing action id, maps to the main hand/off hand constants of vanilla ClientboundAnimatePacket
    private static int AnimateAction(InteractionHand hand)
        => hand == InteractionHand.OffHand ? ClientboundAnimatePacket.SwingOffHand : ClientboundAnimatePacket.SwingMainHand;

    //The following 54 methods of ServerGamePacketListener have no registered PacketType this round and will not decode to the empty implementations

    //HandleChatCommand player executing a slash command; handed to the command manager to parse and run
    public void HandleChatCommand(ServerboundChatCommandPacket packet)
        => ExecuteChatCommand(packet.Command);

    //HandleSignedChatCommand signed commands are not verified and go down the same execution path as unsigned ones
    public void HandleSignedChatCommand(ServerboundChatCommandSignedPacket packet)
        => ExecuteChatCommand(packet.Command);

    //ExecuteChatCommand unified entry for chat commands
    private void ExecuteChatCommand(string command)
    {
        var player = Player;
        if (player is null || Commands is null) return;
        Log.Debug($"HandleChatCommand profile={_profile.Name} command={command}");
        Commands.Execute(player, command);
    }

    public void HandleChatAck(ServerboundChatAckPacket packet) { }
    //HandleContainerButtonClick container button click; handed to the current menu
    //Interactions without slot numbers like selecting a stonecutter recipe go here, maps to clickMenuButton in vanilla handleContainerButtonClick
    public void HandleContainerButtonClick(ServerboundContainerButtonClickPacket packet)
    {
        var player = Player;
        if (player?.ContainerMenu is not { } menu) return;
        if (packet.ContainerId != menu.ContainerId) return;
        menu.ClickMenuButton(player, packet.ButtonId);
    }

    //HandleContainerClick container click; handed to the player's current menu
    //Vanilla compares stateId and the carriedItem hash here for anti-cheat; this project processes the full stack without validation
    public void HandleContainerClick(ServerboundContainerClickPacket packet)
    {
        var player = Player;
        if (player?.ContainerMenu is not { } menu)
        {
            Log.Warning($"[Container] click packet arrived but the player has no menu profile={_profile.Name} slot={packet.SlotNum}");
            return;
        }
        if (packet.ContainerId != menu.ContainerId)
        {
            Log.Warning($"[Container] click packet container id mismatch packet={packet.ContainerId} server={menu.ContainerId} profile={_profile.Name}");
            return;
        }
        //Container clicks are low-frequency, so an Info log helps cross-check client behavior; the slot and click type must match the client pressing Q
        Log.Info($"[Container] click slot={packet.SlotNum} button={packet.ButtonNum} type={packet.Input} profile={_profile.Name}");
        menu.Clicked(packet.SlotNum, packet.ButtonNum, packet.Input, player);
    }

    public void HandlePlaceRecipe(ServerboundPlaceRecipePacket packet) { }

    //HandleContainerClose client closing a menu, maps to player.doCloseContainer in vanilla handleContainerClose
    //Only switches back to the inventory menu and does not echo container_close; the client already closed the screen itself
    public void HandleContainerClose(ServerboundContainerClosePacket packet) => Player?.DoCloseContainer();
    //HandleAttack left-click attacking an entity; the 26.2 attack packet is separate from the swing packet
    //The target is looked up among online players first, then in level entities; players are not in the entity manager, so both paths must be checked
    //Attack cooldown/critical/hit sweep are not implemented; it aligns with the base damage of vanilla Player.attack
    public void HandleAttack(ServerboundAttackPacket packet)
    {
        var attacker = Player;
        if (attacker is null || Players is null) return;
        //Attacking yourself deals no damage, maps to the self exclusion in vanilla isAttackable
        if (packet.EntityId == attacker.EntityId) return;
        //Damage comes from the attacker's attack_damage attribute, maps to vanilla Player.attack reading ATTACK_DAMAGE
        var damage = attacker.AttackDamage;
        var playerTarget = Players.GetPlayerByEntityId(packet.EntityId);
        if (playerTarget is not null)
        {
            var damaged = Players.HurtPlayer(playerTarget, attacker, damage);
            PlayAttackSound(attacker, damaged);
            Log.Debug($"Attack player target={playerTarget.Profile.Name} damage={damage} hit={damaged} profile={_profile.Name}");
            return;
        }
        if (attacker.Level is not PersistentServerLevel level) return;
        var entity = level.EntityManager.GetByEntityId(packet.EntityId);
        if (entity is null) return;
        //Carries the attacker's position so the target is knocked back along the attacker-to-target direction, maps to the knockback in vanilla Player.attack
        var hurt = entity.Hurt(damage, attacker.Position);
        //The mob hurt animation uses entity event 2, maps to broadcastEntityEvent(2) in vanilla LivingEntity.hurt
        if (hurt)
        {
            Players.BroadcastAll(new ClientboundEntityEventPacket(entity.EntityId, 2));
            //The hurt sound plays at the mob's own position; this project has no per-entity sound table so the generic hurt sound is used
            ServerSounds.PlaySound(Players, SoundEvents.GenericHurt, SoundSource.Neutral,
                entity.Pos.X, entity.Pos.Y, entity.Pos.Z, 1f, 1f);
        }
        PlayAttackSound(attacker, hurt);
        Log.Debug($"Attack entity target={entity.Id} damage={damage} hit={hurt} health={entity.Health} profile={_profile.Name}");
    }

    //PlayAttackSound swing sound: strong hit on hit, weak on miss, maps to the playback branch at the end of vanilla Player.attack
    private void PlayAttackSound(ServerPlayer attacker, bool hit)
    {
        if (Players is null) return;
        ServerSounds.PlaySound(Players, hit ? SoundEvents.PlayerAttackStrong : SoundEvents.PlayerAttackWeak,
            SoundSource.Players, attacker.Position.X, attacker.Position.Y, attacker.Position.Z, 1f, 1f);
    }
    //HandleInteract right-click entity; mounting/trading/feeding all go through this packet
    //This project has no entity interaction behavior; like vanilla it plays the swing animation first then logs, regardless of whether the target exists
    public void HandleInteract(ServerboundInteractPacket packet)
    {
        var player = Player;
        if (player is null || Players is null) return;
        Players.BroadcastAllExcept(player,
            new ClientboundAnimatePacket(player.EntityId, AnimateAction(packet.Hand)));
        Log.Debug($"Interact entity target={packet.EntityId} hand={packet.Hand} secondary={packet.UsingSecondaryAction} profile={_profile.Name}");
    }
    public void HandleSpectatorAction(ServerboundSpectatorActionPacket packet) { }
    //HandlePlayerAbilities client reports starting/stopping flight, maps to vanilla handlePlayerAbilities
    //Without mayfly permission the report is treated as not flying; a survival client editing the packet cannot fly
    public void HandlePlayerAbilities(ServerboundPlayerAbilitiesPacket packet)
    {
        if (Player is not { } player) return;
        player.Abilities.Flying = packet.IsFlying && player.Abilities.MayFly;
    }
    //HandlePlayerCommand player state switches; this project only handles sprint start/stop, other actions (riding jump/open inventory/elytra) have no corresponding system
    //State changes are detected by EntityTracker each tick and sent to other players
    public void HandlePlayerCommand(ServerboundPlayerCommandPacket packet)
    {
        var player = Player;
        if (player is null) return;
        switch (packet.Action)
        {
            case PlayerCommandAction.StartSprinting:
                player.SetSprinting(true);
                return;
            case PlayerCommandAction.StopSprinting:
                player.SetSprinting(false);
                return;
        }
    }

    //HandlePlayerInput player keyboard input; the sneak state is driven by the input bit, maps to vanilla handlePlayerInput setting the shift key state
    //Sprint is not handled here but managed by the player_command start/stop action; otherwise the input bit would override an active sprint
    public void HandlePlayerInput(ServerboundPlayerInputPacket packet)
    {
        Player?.SetSneaking(packet.Input.Shift);
    }
    public void HandleSetCarriedItem(ServerboundSetCarriedItemPacket packet)
    {
        var player = Player;
        if (player is null) return;
        //Not applying the slot would make later placement and use keep reading the old slot's item, mismatching the client's actual held item
        if (packet.Slot >= 0 && packet.Slot < PlayerInventory.HotbarSlots)
        {
            player.Inventory.SelectedSlot = packet.Slot;
            return;
        }
        Log.Warning($"Hotbar slot out of range {packet.Slot} profile={_profile.Name}");
        player.Disconnect("Invalid hotbar selection (Hacking?)");
    }

    //HandleSetCreativeModeSlot creative inventory slot set, maps to vanilla handleSetCreativeModeSlot
    //slotNum<0 means dropping an item; this project has no world drop entities yet, so the drop branch is ignored
    //Slot validation 1-45 and the count cap align with vanilla; changes are broadcast after setting
    public void HandleSetCreativeModeSlot(ServerboundSetCreativeModeSlotPacket packet)
    {
        var player = Player;
        if (player?.ContainerMenu is not { } menu || player.GameType != GameType.Creative) return;
        var validSlot = packet.SlotNum >= 1 && packet.SlotNum < menu.Slots.Count;
        var validData = packet.Stack.IsEmpty()
            || packet.Stack.GetCount() <= packet.Stack.GetItem().GetDefaultMaxStackSize();
        if (validSlot && validData)
        {
            menu.GetSlot(packet.SlotNum).Set(packet.Stack);
            menu.BroadcastChanges();
        }
    }
    public void HandleSignUpdate(ServerboundSignUpdatePacket packet) { }

    //HandleUseItem player using an item into the air, maps to vanilla handleUseItem
    //Takes the item from the corresponding hand, ignores an empty stack, and corrects rotation from the client report
    //Throwable items are released here; other item use behavior awaits the item system
    public void HandleUseItem(ServerboundUseItemPacket packet)
    {
        var player = Player;
        if (player is null) return;
        //Using into the air also carries a block change sequence and must likewise end the client prediction
        AckBlockChanges(packet.Sequence);
        var stack = packet.Hand == InteractionHand.OffHand
            ? player.Inventory.GetItem(PlayerInventory.OffhandSlot)
            : player.Inventory.GetSelectedItem();
        if (stack.IsEmpty()) return;
        var yRot = Mth.WrapDegrees(packet.YRot);
        var xRot = Mth.WrapDegrees(packet.XRot);
        if (yRot != player.Yaw || xRot != player.Pitch)
        {
            player.Yaw = yRot;
            player.Pitch = xRot;
        }
        //Snowballs, eggs, ender pearls and fireballs are thrown directly, maps to spawnProjectileFromRotation in vanilla SnowballItem.use
        if (player.Level is PersistentServerLevel level && stack.GetItem() is ProjectileItem projectileItem)
        {
            projectileItem.Use(level, player, stack);
            //Creative throws are not consumed, maps to the vanilla abilities.instabuild branch
            if (player.GameType != GameType.Creative)
            {
                stack.SetCount(stack.GetCount() - 1);
                player.ContainerMenu?.SendAllDataToRemote();
            }
        }
        Log.Debug($"Use item {stack.GetItem().Id} hand={packet.Hand} sequence={packet.Sequence} profile={_profile.Name}");
    }
    public void HandleTeleportToEntityPacket(ServerboundTeleportToEntityPacket packet) { }
    public void HandlePaddleBoat(ServerboundPaddleBoatPacket packet) { }
    public void HandleMoveVehicle(ServerboundMoveVehiclePacket packet) { }
    public void HandleAcceptPlayerLoad(ServerboundPlayerLoadedPacket packet)
    {
        //Signal that the client finished loading terrain and entered the world
        Log.Info($"Client finished loading and entered the world profile={_profile.Name}");
    }
    public void HandleRecipeBookSeenRecipePacket(ServerboundRecipeBookSeenRecipePacket packet) { }
    public void HandleBundleItemSelectedPacket(ServerboundSelectBundleItemPacket packet) { }
    public void HandleRecipeBookChangeSettingsPacket(ServerboundRecipeBookChangeSettingsPacket packet) { }
    public void HandleSeenAdvancements(ServerboundSeenAdvancementsPacket packet) { }
    //HandleCustomCommandSuggestions client requests command completion with tab; the server computes and replies with a suggestions packet
    //Vanilla has the same-named method; without implementing this the client is left with local completion only (literals and coordinates) and shows none of the entity/item candidates
    public void HandleCustomCommandSuggestions(ServerboundCommandSuggestionPacket packet)
    {
        var player = Player;
        if (player is null || Commands is null) return;
        var suggestions = Commands.GetCompletions(player, packet.Command);
        //For troubleshooting missing time completion; only logs for the time command and will be removed once located
        if (packet.Command.StartsWith("time", StringComparison.OrdinalIgnoreCase))
            Log.Info($"Command suggestions request id={packet.Id} command=\"{packet.Command}\" candidates={suggestions.List.Count} range=[{suggestions.Range.Start},{suggestions.Range.Length}]");
        var entries = new List<CommandSuggestionEntry>(suggestions.List.Count);
        foreach (var suggestion in suggestions.List)
            entries.Add(new CommandSuggestionEntry(suggestion.Text, suggestion.Tooltip as Component));
        player.Connection.Send(new ClientboundCommandSuggestionsPacket(
            packet.Id, suggestions.Range.Start, suggestions.Range.Length, entries));
    }
    public void HandleSetCommandBlock(ServerboundSetCommandBlockPacket packet) { }
    public void HandleSetCommandMinecart(ServerboundSetCommandMinecartPacket packet) { }
    //HandlePickItemFromBlock middle-click pick block, maps to vanilla handlePickItemFromBlock following tryPickItem
    //The IncludeData variant carrying block entity data will be extended once block entity component serialization is complete
    public void HandlePickItemFromBlock(ServerboundPickItemFromBlockPacket packet)
    {
        var player = Player;
        if (player?.Level is not PersistentServerLevel level) return;
        var state = level.GetBlockState(packet.Pos);
        //States without an Owner such as unloaded chunks or air are ignored
        if (state?.Owner is not { } block) return;
        //Looks up the same-named item by block registry name; the vast majority of blocks have a corresponding BlockItem
        var holder = BuiltInRegistries.ITEM.Get(block.Id);
        if (holder is null) return;
        var inventory = player.Inventory;
        var stack = new ItemStack(holder, 1, DataComponentPatch.Empty);
        //Vanilla tryPickItem order: find the same item in the whole inventory -> switch to it if in the hotbar, swap it out if in the main inventory -> only give a new one if neither and in creative mode
        var matching = inventory.FindSlotMatchingItem(stack);
        if (matching != -1)
        {
            if (matching < PlayerInventory.HotbarSlots) inventory.SelectedSlot = matching;
            else inventory.PickSlot(matching);
        }
        else if (inventory.InfiniteMaterials)
        {
            inventory.AddAndPickItem(stack);
        }
        _connection.Send(new ClientboundSetHeldSlotPacket(inventory.SelectedSlot));
        player.ContainerMenu?.BroadcastChanges();
    }

    //HandlePickItemFromEntity middle-click pick entity; will be extended once the entity-to-drop mapping is complete
    public void HandlePickItemFromEntity(ServerboundPickItemFromEntityPacket packet) { }
    public void HandleRenameItem(ServerboundRenameItemPacket packet) { }
    public void HandleSetBeaconPacket(ServerboundSetBeaconPacket packet) { }
    public void HandleSetGameRule(ServerboundSetGameRulePacket packet) { }
    public void HandleSetStructureBlock(ServerboundSetStructureBlockPacket packet) { }
    public void HandleSetTestBlock(ServerboundSetTestBlockPacket packet) { }
    public void HandleTestInstanceBlockAction(ServerboundTestInstanceBlockActionPacket packet) { }
    public void HandleSelectTrade(ServerboundSelectTradePacket packet) { }
    public void HandleEditBook(ServerboundEditBookPacket packet) { }
    public void HandleEntityTagQuery(ServerboundEntityTagQueryPacket packet) { }
    public void HandleContainerSlotStateChanged(ServerboundContainerSlotStateChangedPacket packet) { }
    public void HandleBlockEntityTagQuery(ServerboundBlockEntityTagQueryPacket packet) { }
    public void HandleSetJigsawBlock(ServerboundSetJigsawBlockPacket packet) { }
    public void HandleJigsawGenerate(ServerboundJigsawGeneratePacket packet) { }
    public void HandleChangeDifficulty(ServerboundChangeDifficultyPacket packet) { }
    //HandleChangeGameMode client switching game mode with F3+F4, requires permission level 2
    //The client entry is disabled by permission level; this is the server-side check against spoofing
    //After switching, only the operator is acked, maps to the private commands.gamemode.success.self message of vanilla F3+F4
    public void HandleChangeGameMode(ServerboundChangeGameModePacket packet)
    {
        if (Player is null || Players is null) return;
        if (!Player.HasPermissions(2)) return;
        if (Player.GameType == packet.Mode) return;
        Players.ChangeGameMode(Player, packet.Mode);
        Player.Connection.Send(new ClientboundSystemChatPacket(
            Component.Literal($"Your game mode has been set to {packet.Mode.Name}"), false));
    }
    public void HandleLockDifficulty(ServerboundLockDifficultyPacket packet) { }
    public void HandleChatSessionUpdate(ServerboundChatSessionUpdatePacket packet) { }
    public void HandleConfigurationAcknowledged(ServerboundConfigurationAcknowledgedPacket packet) { }
    public void HandleChunkBatchReceived(ServerboundChunkBatchReceivedPacket packet) { }
    public void HandleDebugSubscriptionRequest(ServerboundDebugSubscriptionRequestPacket packet) { }
    public void HandleClientTickEnd(ServerboundClientTickEndPacket packet) { }

    //The following 6 methods inherited from ServerCommonPacketListener are empty this round
    public void HandleClientInformation(ServerboundClientInformationPacket packet) { }
    public void HandleCustomPayload(ServerboundCustomPayloadPacket packet) { }
    public void HandleKeepAlive(ServerboundKeepAlivePacket packet)
    {
        //Logs the response id and associated player; when keepalive is abnormal this distinguishes a missing response from an unassociated player
        Log.Debug($"HandleKeepAlive received id={packet.Id} player={(Player is null ? "null" : Player.Profile.Name)}");
        Player?.HandleKeepAliveResponse(packet.Id);
    }
    public void HandlePong(ServerboundPongPacket packet) { }
    public void HandleResourcePack(ServerboundResourcePackPacket packet) { }
    public void HandleCustomClickAction(ServerboundCustomClickActionPacket packet) { }

    //Inherited from ServerCookiePacketListener
    public void HandleCookieResponse(ServerboundCookieResponsePacket packet) { }

    //Inherited from ServerPingPacketListener
    //The client's PingDebugMonitor periodically sends ping_request; the same timestamp is echoed back for the client to compute round-trip latency
    //The play pong protocol id differs from Status; the current outbound protocol table assigns the ID by packet class, so it need not be specified here
    public void HandlePingRequest(ServerboundPingRequestPacket packet)
        => Player?.Connection.Send(new ClientboundPongResponsePacket(packet.Time));

    public void OnDisconnect(string reason)
    {
        Log.Info($"play phase disconnect reason={reason} profile={_profile.Name}");
    }
}
