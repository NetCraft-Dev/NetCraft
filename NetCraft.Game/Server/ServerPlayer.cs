using NetCraft.Network.Protocol.Common;
using NetCraft.Game.Network.Protocol.Game;
using NetCraft.Network.Protocol.Login;
using NetCraft.Game.World.Effect;
using NetCraft.Game.World.Entity;
using NetCraft.Game.World.Inventory;
using NetCraft.Game.World.Items;
using NetCraft.Game.World.Level;
using NetCraft.Logging;
using NetCraft.Network;
using NetCraft.Network.Chat;
using NetCraft.Registry;
using NetCraft.Storage;
using NetCraft.Primitives;
//Attribute-related types carry their own namespace; only the needed names are taken here
using AttributeMap = NetCraft.Registry.EntityAttribute.AttributeMap;
using AttributeSupplier = NetCraft.Registry.EntityAttribute.AttributeSupplier;
using AttributeDef = NetCraft.Registry.EntityAttribute.Attribute;
using EntityAttributes = NetCraft.Registry.EntityAttribute.Attributes;
//The effect class has the same name as the registry placeholder interface, so the effect implementation class gets a short alias
using GameMobEffect = NetCraft.Game.World.Effect.MobEffect;

namespace NetCraft.Game.Server;

//ServerPlayer server-side player object, maps to vanilla ServerPlayer
//Minimal implementation holding Connection/GameProfile/position/game type for PlayerList to manage
//Does not extend NetCraft.Registry.Entity to avoid confusion with the existing entity system; this round only does the network-layer player
//Implements ITrackedEntity so players and entities share one tracking sync path
//Implements ISyncedEntity so metadata sync shares a mechanism with entities such as drops
public sealed class ServerPlayer : ITrackedEntity, ISyncedEntity
{
    //EntityId the entity id assigned by the server, used for sync such as ClientboundLoginPacket
    //Shares the same allocator as Registry.Entity; two independent counters would collide and make attack packets target the wrong entity
    public int EntityId { get; private set; }
    public GameProfile Profile { get; }
    public Connection Connection { get; }
    public ServerLevel Level { get; }

    //GameType the player's game type, initialized from the server default in PlaceNewPlayer
    //On switch it syncs the inventory's infinite materials flag: creative swallows items it cannot fit without counting it as failure
    //Only creative sets instabuild, spectator does not, consistent with vanilla updatePlayerAbilities
    public GameType GameType
    {
        get => _gameType;
        set
        {
            _gameType = value;
            Inventory.InfiniteMaterials = value == GameType.Creative;
            //Abilities are recomputed with the mode; the creative branch does not touch the flight state, so the player's toggled flight survives a mode switch, maps to vanilla updatePlayerAbilities
            Abilities.ApplyGameType(value);
        }
    }

    private GameType _gameType = GameType.Survival;

    //Abilities player ability state; the join ability packet and the player save both read it, maps to vanilla Player.abilities
    public Abilities Abilities { get; } = new();

    //PermissionLevel permission level 0-4, set by PlayerList from ops.json on join
    //After op/deop changes PlayerList updates it and syncs the client, maps to the vanilla ServerPlayer permission level
    public int PermissionLevel { get; set; }

    //HasPermissions whether it reaches the given permission level, maps to vanilla ServerPlayer.hasPermissions
    public bool HasPermissions(int level) => PermissionLevel >= level;

    //Position player position, defaults to spawn 0,0,0; spawn logic is wired in later
    public Vec3 Position { get; set; } = new(0, 64, 0);
    public float Yaw { get; set; }
    public float Pitch { get; set; }

    //_lastActivePosition the previous tick's position; differing from the current one means the player is active
    private Vec3 _lastActivePosition;

    //LastActiveMillis the last active moment, the baseline for the idle kick timer
    public long LastActiveMillis { get; private set; } = Environment.TickCount64;

    //RespawnPos personal respawn point, maps to the player respawn position set by the vanilla spawnpoint command; empty falls back to the world spawn
    public Vec3? RespawnPos { get; set; }

    //RespawnAngle personal respawn facing
    public float RespawnAngle { get; set; }

    //Velocity/OnGround the player's motion state recorded by the server; movement is reported by the client and filled back
    public Vec3 Velocity { get; set; } = Vec3.Zero;
    public bool OnGround { get; set; }

    //--- spectator camera, maps to vanilla ServerPlayer.camera ---

    //IsSpectator whether in spectator mode, maps to vanilla isSpectator
    public bool IsSpectator => GameType == GameType.Spectator;

    //_camera the current spectator camera entity; null means the view is on the player, maps to the vanilla camera field
    private ITrackedEntity? _camera;

    //GetCamera the current camera entity; returns the player itself when not spectating, maps to vanilla getCamera
    public ITrackedEntity GetCamera() => _camera ?? this;

    //SetCamera switches the spectator camera, maps to vanilla ServerPlayer.setCamera
    //null means returning to the player's own view; switching syncs the position to the camera and sends the set-camera packet
    //Cross-dimension spectating is not implemented: ServerPlayer.Level is fixed after construction, so a camera in another dimension only follows position without changing dimension
    public void SetCamera(ITrackedEntity? newCamera)
    {
        var oldCamera = GetCamera();
        _camera = newCamera ?? this;
        if (ReferenceEquals(oldCamera, _camera)) return;
        //The position lands at the camera; chunk loading and entity tracking follow the camera, maps to teleportTo in vanilla setCamera
        var pos = _camera.Pos;
        if (Listener is { } listener)
            listener.Teleport(pos, Yaw, Pitch, pos.X, pos.Y, pos.Z, Yaw, Pitch, 0);
        else
            Position = pos;
        Connection.Send(new ClientboundSetCameraPacket(_camera.EntityId));
    }

    //TickCamera each tick a spectator follows the camera position, maps to the camera branch of vanilla ServerPlayer.tick
    //When the camera entity is removed it returns to the player's own view; the position lands directly in server state and the client view is already taken over by the camera packet
    private void TickCamera()
    {
        if (_camera is not { } camera || ReferenceEquals(camera, this)) return;
        if (camera is NetCraft.Registry.Entity { IsRemoved: true })
        {
            SetCamera(null);
            return;
        }
        Position = camera.Pos;
        Yaw = camera.YRot;
        Pitch = camera.XRot;
    }

    //--- ITrackedEntity implementation ---

    //Type the player entity type; players and entities share the tracking path and send AddEntity by this type
    public EntityType<object>? Type => EntityTypes.PLAYER;

    //Uuid the player's unique id, reusing the GameProfile id
    public Guid Uuid => Profile.Id;

    Vec3 ITrackedEntity.Pos => Position;
    float ITrackedEntity.YRot => Yaw;
    float ITrackedEntity.XRot => Pitch;

    //Attributes the player attribute map, taking the default map by entity type
    //maps to the vanilla ServerPlayer layer holding an AttributeMap via LivingEntity
    public AttributeMap Attributes { get; }

    //GetAttributeValue gets the final attribute value, maps to vanilla getAttributeValue
    public double GetAttributeValue(AttributeDef attribute) => Attributes.GetValue(attribute);

    //--- pose and shared flags, maps to vanilla Entity.DATA_SHARED_FLAGS_ID and DATA_POSE ---

    //PoseStanding/PoseCrouching pose ids, aligned with the vanilla Pose enum
    public const int PoseStanding = 0;
    public const int PoseCrouching = 5;

    //SyncedData player metadata container; shared flags and pose sync through it, maps to vanilla SynchedEntityData
    public SynchedEntityData SyncedData { get; } = new();

    //IsSprinting/IsSneaking sprint and sneak state, toggled by the player_command packet
    public bool IsSprinting { get; private set; }
    public bool IsSneaking { get; private set; }

    //SharedFlags shared flags: bit 1 sneak, bit 3 sprint, the rest unused here
    public byte SharedFlags => (byte)((IsSneaking ? 1 << 1 : 0) | (IsSprinting ? 1 << 3 : 0));

    //PoseId current pose; the model crouches when sneaking
    public int PoseId => IsSneaking ? PoseCrouching : PoseStanding;

    //SetSprinting toggles sprint, maps to vanilla setSprinting
    public void SetSprinting(bool value)
    {
        IsSprinting = value;
        SyncSharedState();
    }

    //SetSneaking toggles sneak, maps to vanilla setShiftKeyDown
    public void SetSneaking(bool value)
    {
        IsSneaking = value;
        SyncSharedState();
    }

    //SyncSharedState writes the shared flags and pose into metadata; Set only bumps the version when the value changed
    private void SyncSharedState()
    {
        SyncedData.Set(NetCraft.Registry.Entity.SharedFlagsIndex, SharedFlags);
        SyncedData.Set(NetCraft.Registry.Entity.PoseIndex, PoseId);
    }

    //ChunkSender progressive chunk sender, injected by PlayerList in PlaceNewPlayer and driven by tick
    public ChunkSender? ChunkSender { get; set; }

    //ViewDistanceChunks the player's view distance in chunks, taken from server config in PlaceNewPlayer
    //Entity tracking decides visibility by the smaller of it and the entity tracking distance
    public int ViewDistanceChunks { get; set; }

    //KeepAliveIntervalMillis heartbeat interval aligned with vanilla 15 seconds; it keeps the client's 30-second netty read timeout from firing
    public const long KeepAliveIntervalMillis = 15000;

    //Inventory player inventory, created with the player object; menu slots ultimately land here
    public PlayerInventory Inventory { get; } = new();

    //OwnerList the owning player list; sounds broadcast to the whole server, injected by PlayerList.PlaceNewPlayer
    public PlayerList? OwnerList { get; set; }

    //AddItem core give entry, maps to vanilla Player.addItem
    //All paths handing items to the player converge here; returns whether at least one was placed
    //What cannot be placed stays in the passed stack; the caller decides whether to drop it or leave it in place
    public bool AddItem(ItemStack stack) => Inventory.Add(stack);

    //GiveItem gives an item, dropping what cannot fit at the feet and playing the pickup sound when something was placed; returns the count actually placed
    //maps to the inventory.add plus player.drop combination in the vanilla give command; command giving and picking up share this path
    public int GiveItem(ItemStack stack)
    {
        var total = stack.GetCount();
        Inventory.Add(stack);
        var placed = total - stack.GetCount();
        if (!stack.IsEmpty()) Drop(stack, randomly: false, thrownFromHand: false);
        if (placed > 0) PlayPickupSound();
        ContainerMenu?.SendAllDataToRemote();
        return placed;
    }

    //PlayPickupSound plays the item pickup sound, maps to vanilla ServerPlayer.onItemPickup
    //Volume 0.2, pitch jittering around double by the vanilla formula; this is the only sound exit for items entering the inventory
    public void PlayPickupSound()
    {
        if (OwnerList is null) return;
        var pitch = (Random.Shared.NextSingle() - Random.Shared.NextSingle()) * 0.7f + 1.0f;
        ServerSounds.PlaySound(OwnerList, SoundEvents.ItemPickup, SoundSource.Players,
            Position.X, Position.Y, Position.Z, 0.2f, pitch * 2.0f);
    }

    //EyeHeight player eye height, maps to the eye height in the vanilla player entity dimensions; used to compute the hand position when dropping
    public const float EyeHeight = 1.62f;

    //DropPickupDelay pickup delay ticks after dropping, maps to vanilla 40 ticks
    private const int DropPickupDelay = 40;

    //Drop drops the item in the selected slot, maps to vanilla ServerPlayer.drop(boolean)
    //all true drops the whole slot, false drops one; the inventory change is synced to the client
    public ItemEntity? Drop(bool all)
    {
        var removed = Inventory.RemoveFromSelected(all);
        ContainerMenu?.SendAllDataToRemote();
        return Drop(removed, randomly: false, thrownFromHand: true);
    }

    //Drop spawns the drop entity and adds it to the level, maps to vanilla LivingEntity.drop and createItemStackToDrop
    //The position is 0.3 blocks below the eyes; after dropping it has a 40-tick pickup delay so it is not picked back immediately
    public ItemEntity? Drop(ItemStack stack, bool randomly, bool thrownFromHand)
    {
        if (stack.IsEmpty()) return null;
        if (Level is not PersistentServerLevel level) return null;
        var drop = new ItemEntity(EntityTypes.ITEM)
        {
            Pos = new Vec3(Position.X, Position.Y + EyeHeight - 0.3, Position.Z),
            Item = stack,
            Thrower = thrownFromHand ? Profile.Id : null,
        };
        drop.SetPickUpDelay(DropPickupDelay);
        drop.Velocity = DropVelocity(randomly);
        level.AddEntity(drop);
        return drop;
    }

    //DropVelocity drop velocity, maps to the two branches of vanilla createItemStackToDrop
    //randomly is used for random scatter such as death drops; a hand drop is thrown along the look direction with a little random jitter
    private Vec3 DropVelocity(bool randomly)
    {
        if (randomly)
        {
            var power = Random.Shared.NextSingle() * 0.5f;
            var angle = Random.Shared.NextSingle() * MathF.PI * 2f;
            return new Vec3(-MathF.Sin(angle) * power, 0.2, MathF.Cos(angle) * power);
        }
        const float toRadians = MathF.PI / 180f;
        var sinX = MathF.Sin(Pitch * toRadians);
        var cosX = MathF.Cos(Pitch * toRadians);
        var sinY = MathF.Sin(Yaw * toRadians);
        var cosY = MathF.Cos(Yaw * toRadians);
        var spread = Random.Shared.NextSingle() * MathF.PI * 2f;
        var jitter = 0.02f * Random.Shared.NextSingle();
        return new Vec3(
            -sinY * cosX * 0.3f + MathF.Cos(spread) * jitter,
            -sinX * 0.3f + 0.1f + (Random.Shared.NextSingle() - Random.Shared.NextSingle()) * 0.1f,
            cosY * cosX * 0.3f + MathF.Sin(spread) * jitter);
    }

    //GetNearestLookingDirection the nearest of the six directions to the player's look; needed when placing blocks such as observers that include up/down, maps to the vanilla same-named method
    //The look vector is computed by vanilla getViewVector and includes pitch, so UP and DOWN can come out
    public Direction GetNearestLookingDirection()
    {
        const float toRadians = MathF.PI / 180f;
        var pitch = Pitch * toRadians;
        var yaw = -Yaw * toRadians;
        var cosPitch = MathF.Cos(pitch);
        return Direction.FromViewVector(
            MathF.Sin(yaw) * cosPitch, -MathF.Sin(pitch), MathF.Cos(yaw) * cosPitch);
    }

    //MaxHealth full health from the max_health attribute, maps to vanilla getMaxHealth
    //Once equipment and effects are wired up the max health with modifiers takes effect through the attribute
    public float MaxHealth => (float)GetAttributeValue(EntityAttributes.MaxHealth);

    //AttackDamage bare-hand attack damage from the attack_damage attribute, maps to the vanilla player's ATTACK_DAMAGE
    //Weapon bonus/cooldown/crit are not implemented, added in phase 2
    public float AttackDamage => (float)GetAttributeValue(EntityAttributes.AttackDamage);

    //Health current health; a new player is at full from the max_health attribute, restored by playerdata, and sent by the SetHealth packet on join
    public float Health { get; set; }

    //InvulnerableTime remaining ticks of the hurt invulnerability frames, maps to vanilla invulnerableTime
    public int InvulnerableTime { get; private set; }

    //IsDeadOrDying health reached zero, maps to vanilla isDeadOrDying
    public bool IsDeadOrDying => Health <= 0f;

    //Hurt deals damage, the minimal set of vanilla hurtServer
    //No repeated damage within the invulnerability frames; armor reduction/knockback/damage source tracking are not done
    public bool Hurt(float amount)
    {
        if (IsDeadOrDying || InvulnerableTime > 0) return false;
        Health = Math.Max(0f, Health - amount);
        //Invulnerability frames 10 ticks; vanilla is 20 of which 10 are for the hurt animation
        InvulnerableTime = 10;
        return true;
    }

    //SetHealth sets health and clamps to 0..full; save restore and respawn go through it, maps to vanilla setHealth
    public void SetHealth(float value) => Health = Math.Clamp(value, 0f, MaxHealth);

    //XpLevel xp level, XpProgress current level progress 0-1, XpTotal accumulated xp
    public int XpLevel { get; set; }
    public float XpProgress { get; set; }
    public int XpTotal { get; set; }

    //ContainerMenu the menu the player currently has open; PlaceNewPlayer creates the inventory menu and sends the initial content
    public AbstractContainerMenu? ContainerMenu { get; set; }

    //BackpackMenu the player inventory menu; switching back to it after closing a container menu, maps to vanilla inventoryMenu
    public InventoryMenu BackpackMenu { get; private set; } = null!;

    //_containerCounter container menu id allocator; vanilla starts at 1, with 0 reserved for the inventory menu
    private int _containerCounter;

    //SetUpInventoryMenu builds the inventory menu and makes it current, called once when the player enters the world
    public void SetUpInventoryMenu()
    {
        BackpackMenu = new InventoryMenu(Inventory) { Synchronizer = new ServerContainerSynchronizer(this) };
        ContainerMenu = BackpackMenu;
    }

    //OpenMenu opens a menu: closes the previous container menu, allocates an id, sends open_screen then syncs the initial content
    //maps to vanilla ServerPlayer.openMenu(MenuProvider)
    public void OpenMenu(MenuProvider provider)
    {
        if (ContainerMenu is { } current && !ReferenceEquals(current, BackpackMenu)) DoCloseContainer();
        var menu = provider.CreateMenu(++_containerCounter, Inventory, this);
        if (menu is null) return;
        menu.Synchronizer = new ServerContainerSynchronizer(this);
        ContainerMenu = menu;
        if (menu.Kind is { } kind)
            Connection.Send(new ClientboundOpenScreenPacket(menu.ContainerId, kind, provider.DisplayName));
        menu.SendAllDataToRemote();
    }

    //CloseContainer the server closes the container menu, telling the client first then switching back to the backpack
    //Used when a block is broken or the player walks away, maps to vanilla ServerPlayer.closeContainer
    public void CloseContainer()
    {
        if (ContainerMenu is not { } menu || ReferenceEquals(menu, BackpackMenu)) return;
        Connection.Send(new ClientboundContainerClosePacket(menu.ContainerId));
        DoCloseContainer();
    }

    //DoCloseContainer switches back to the inventory menu and syncs the inventory content, maps to vanilla doCloseContainer
    //A client-initiated menu close only goes here; the server does not send container_close back, consistent with vanilla
    public void DoCloseContainer()
    {
        if (BackpackMenu is null) return;
        ContainerMenu = BackpackMenu;
        BackpackMenu.SendAllDataToRemote();
    }

    //SendSystemMessage sends system chat through the chat bar by default, maps to vanilla sendSystemMessage(Component)
    public void SendSystemMessage(Component message) => SendSystemMessage(message, false);

    //SendOverlayMessage sends an overlay message rendered in the action bar, maps to vanilla sendOverlayMessage
    //This is the generic action bar entry; the title command's actionbar branch has a dedicated packet and both render the same on the client
    public void SendOverlayMessage(Component message) => SendSystemMessage(message, true);

    //SendSystemMessage with overlay true uses the action bar, maps to vanilla sendSystemMessage(Component,boolean)
    public void SendSystemMessage(Component message, bool overlay)
        => Connection.Send(new ClientboundSystemChatPacket(message, overlay));

    //SendBuildLimitMessage build height out-of-bounds message, maps to vanilla sendBuildLimitMessage
    //Above the upper bound gives build.tooHigh with 319, below the lower bound build.tooLow with -64, both red action bar text
    public void SendBuildLimitMessage(bool isTooHigh, int limit)
        => SendOverlayMessage(Component.Translatable(isTooHigh ? "build.tooHigh" : "build.tooLow", limit)
            .WithStyle(ChatFormatting.Red));

    //Listener the associated play-stage listener, injected by DedicatedServer.TransitionToGame
    //Teleports must go through its wait-for-client-ack flow; directly changing coordinates and sending packets would be pulled back by in-flight old position packets
    public ServerGamePacketListenerImpl? Listener { get; set; }

    private long _keepAliveSentAt;
    private long _keepAliveId;
    private bool _keepAlivePending;
    //_lastChunkX/_lastChunkZ the last view center chunk, for cross-chunk detection, initial invalid value
    private int _lastChunkX = int.MinValue;
    private int _lastChunkZ;

    //--- mob effects, maps to vanilla LivingEntity.activeEffects ---

    //_activeEffects the effects currently active on the player, indexed by effect value
    private readonly Dictionary<NetCraft.Registry.MobEffect, MobEffectInstance> _activeEffects = new();

        //ActiveEffects all active effect instances
    public IReadOnlyCollection<MobEffectInstance> ActiveEffects => _activeEffects.Values;

        //GetEffect gets the instance of the given effect; returns null when absent
    public MobEffectInstance? GetEffect(NetCraft.Registry.MobEffect effect)
        => _activeEffects.GetValueOrDefault(effect);

        //AddEffect applies an effect and returns whether it took effect; with an existing same-kind effect it follows vanilla update semantics and a weaker one does not override
        //Only sent to the player itself, maps to vanilla ServerPlayer.onEffectUpdated syncing only this connection
    public bool AddEffect(MobEffectInstance instance)
    {
        var effect = instance.Effect.Value;
        if (_activeEffects.TryGetValue(effect, out var existing))
        {
                //A lower level, or the same level with a shorter duration, does not override
            if (instance.Amplifier < existing.Amplifier) return false;
            if (instance.Amplifier == existing.Amplifier && instance.Duration < existing.Duration) return false;
        }
        _activeEffects[effect] = instance;
        Connection.Send(ClientboundUpdateMobEffectPacket.Create(EntityId, instance,
            effect is GameMobEffect gameEffect && gameEffect.NeedsBlend));
        return true;
    }

        //RemoveEffect removes the given effect and notifies the client; returns whether it was removed
    public bool RemoveEffect(NetCraft.Registry.MobEffect effect)
    {
        if (!_activeEffects.Remove(effect, out var removed)) return false;
        Connection.Send(new ClientboundRemoveMobEffectPacket(EntityId, removed.Effect));
        return true;
    }

        //RemoveAllEffects removes all effects notifying the client for each; returns whether any were removed
    public bool RemoveAllEffects()
    {
        if (_activeEffects.Count == 0) return false;
        foreach (var instance in _activeEffects.Values)
            Connection.Send(new ClientboundRemoveMobEffectPacket(EntityId, instance.Effect));
        _activeEffects.Clear();
        return true;
    }

        //TickEffects advances effect durations every tick; expired ones are removed and the client notified
    public void TickEffects()
    {
        List<MobEffectInstance>? expired = null;
        foreach (var instance in _activeEffects.Values)
        {
            instance.Tick();
            if (instance.Expired) (expired ??= new()).Add(instance);
        }
        if (expired is null) return;
            //The dictionary cannot be modified while iterating; expired entries are collected then removed together
        foreach (var instance in expired)
        {
            _activeEffects.Remove(instance.Effect.Value);
            Connection.Send(new ClientboundRemoveMobEffectPacket(EntityId, instance.Effect));
        }
    }

    public ServerPlayer(GameProfile profile, Connection connection, ServerLevel level)
    {
        EntityId = level.GetNextEntityId();
        Profile = profile;
        Connection = connection;
        Level = level;
            //The player attribute map takes the default by entity type, from the same source as the client's local one; falls back to an empty map when unregistered
        Attributes = new AttributeMap(DefaultAttributes.GetSupplier(EntityTypes.PLAYER) ?? AttributeSupplier.Empty);
            //A new player is at full health, maps to setHealth(getMaxHealth()) in the vanilla LivingEntity constructor
        Health = MaxHealth;
        _keepAliveSentAt = Environment.TickCount64;
            //Player metadata baseline, maps to vanilla Player's defineSynchedData
        SyncedData.Define(NetCraft.Registry.Entity.SharedFlagsIndex, EntityDataSerializers.Byte, (byte)0);
        SyncedData.Define(NetCraft.Registry.Entity.PoseIndex, EntityDataSerializers.Pose, PoseStanding);
    }

        //Tick per-frame scheduling, maps to vanilla ServerPlayer.tick
        //On a chunk crossing it updates the chunk view, drives ChunkSender progressive sending, keeps the heartbeat and syncs the menu's dirty slots to the client
    public void Tick()
    {
            //A spectator follows the camera position every tick, maps to the camera branch near the start of vanilla ServerPlayer.tick
        TickCamera();
            //Mob effects decrement duration every tick; expired ones are removed automatically and synced to the client
        TickEffects();
            //Invulnerability frames decrement every tick, maps to invulnerableTime-- in vanilla LivingEntity.tick
        if (InvulnerableTime > 0) InvulnerableTime--;
            //A position change counts as active; the idle kick timer resets from it
        if (Position != _lastActivePosition)
        {
            _lastActivePosition = Position;
            LastActiveMillis = Environment.TickCount64;
        }
            //Out-of-bounds damage; creative and spectator carry the invulnerable flag and vanilla blocks it via isInvulnerableTo
        if (GameType != GameType.Creative && GameType != GameType.Spectator) TickWorldBorderDamage();
        UpdateChunkTracking();
        ChunkSender?.Tick();
        TickItemPickup();
        ContainerMenu?.BroadcastChanges();
            //A container menu is closed automatically when it becomes invalid, a block broken or the player more than 8 blocks away, maps to the stillValid check in vanilla ServerPlayer.tick
        if (ContainerMenu is { } openMenu && BackpackMenu is not null
            && !ReferenceEquals(openMenu, BackpackMenu) && !openMenu.StillValid(this))
            CloseContainer();
        TickKeepAlive();
    }

        //TickItemPickup checks drops underfoot and nearby and tries to pick them up
        //Vanilla triggers playerTouch on entity contact during movement; this project has no entity-entity collision and queries actively each tick by the pickup box
    private void TickItemPickup()
    {
        if (Level is not PersistentServerLevel level) return;
        var box = PickupBox();
        foreach (var entity in level.EntitiesInBox(box))
        {
            if (entity is not ItemEntity item) continue;
                //The spatial index is the bucketing result; filter once more by bounding box intersection
            if (!box.Intersects(item.BoundingBox)) continue;
            item.PlayerTouch(this);
        }
    }

        //PickupBox the pickup test box: the player hit box 0.6 wide 1.8 tall inflated by vanilla 1.0/0.5/1.0 blocks
    private AABB PickupBox()
        => new AABB(Position.X - 0.3, Position.Y, Position.Z - 0.3,
            Position.X + 0.3, Position.Y + 1.8, Position.Z + 0.3).Inflate(1.0, 0.5, 1.0);

        //TickWorldBorderDamage continuous damage outside the world border, maps to the border damage section of vanilla LivingEntity.baseTick
        //No damage within the damage buffer; damage is the out-of-bounds blocks times damage per block with a minimum of 1
    private void TickWorldBorderDamage()
    {
        var border = Level.WorldBorder;
        var box = new AABB(Position.X - 0.3, Position.Y, Position.Z - 0.3,
            Position.X + 0.3, Position.Y + 1.8, Position.Z + 0.3);
        if (border.IsWithinBounds(box)) return;
        var distance = border.GetDistanceToBorder(Position.X, Position.Z) + border.SafeZone;
        if (distance >= 0.0 || border.DamagePerBlock <= 0.0) return;
        Hurt(Math.Max(1f, MathF.Floor((float)(-distance * border.DamagePerBlock))));
    }

        //UpdateChunkTracking updates the view center when a player crosses a chunk, maps to vanilla ChunkMap.move
        //Each tick it compares the current chunk with the last recorded one and notifies ChunkSender only on change
    private void UpdateChunkTracking()
    {
        var chunkX = (int)Math.Floor(Position.X / 16);
        var chunkZ = (int)Math.Floor(Position.Z / 16);
        if (chunkX == _lastChunkX && chunkZ == _lastChunkZ) return;
        _lastChunkX = chunkX;
        _lastChunkZ = chunkZ;
        ChunkSender?.UpdateCenter(chunkX, chunkZ, ViewDistanceChunks);
            //Player tickets follow the view center: load tickets within view distance and a simulation ticket for the player's own chunk, maps to vanilla ChunkMap.move
        if (Level is PersistentServerLevel persistent)
            persistent.ChunkSource.UpdatePlayerTickets(this, chunkX, chunkZ, ViewDistanceChunks);
    }

        //TickKeepAlive sends a heartbeat every 15 seconds
        //If the client receives no packet for 30 seconds it declares a read timeout and disconnects; after chunks are sent the server has no other outbound packets
        //An unanswered last heartbeat means the link is dead and disconnects directly, aligned with vanilla ServerGamePacketListenerImpl.tick
    private void TickKeepAlive()
    {
        if (!Connection.IsConnected) return;
        var now = Environment.TickCount64;
        if (now - _keepAliveSentAt < KeepAliveIntervalMillis) return;
        if (_keepAlivePending)
        {
            Log.Warning($"Disconnecting due to keep-alive timeout {Profile.Name}");
            Disconnect("disconnect.timeout");
            return;
        }
        _keepAliveSentAt = now;
        _keepAliveId = now;
        _keepAlivePending = true;
        try
        {
            Connection.Send(new ClientboundKeepAlivePacket(_keepAliveId));
        }
        catch (Exception e)
        {
            Log.Warning($"Keep-alive send failed {Profile.Name} {e.Message}");
        }
    }

        //PendingKeepAliveId the heartbeat id awaiting a reply; null when nothing is pending
        //For a fake client to emulate a real client's reply behavior; a fake connection has no netty to auto-reply
    public long? PendingKeepAliveId => _keepAlivePending ? _keepAliveId : null;

        //HandleKeepAliveResponse clears the wait marker after the client replies to the heartbeat
    public void HandleKeepAliveResponse(long id)
    {
            //Prints the match result, used when a heartbeat times out to distinguish a missing reply from an id mismatch
        Log.Debug($"HandleKeepAliveResponse id={id} expected={_keepAliveId} pending={_keepAlivePending} match={id == _keepAliveId}");
        if (id == _keepAliveId) _keepAlivePending = false;
    }

        //Disconnect disconnects the player, aligned with vanilla ServerPlayer.disconnect
        //Disconnect actively disconnects the player, maps to vanilla ServerGamePacketListenerImpl.disconnect
        //The disconnect packet must be sent before closing the connection, or the client only sees the connection drop without the kick reason
        //Vanilla stop goes through multiPlayerList.removeAll sending multiplayer.disconnect.server_shutdown to every player
    public void Disconnect(string reason)
    {
        if (Connection.IsConnected)
            Connection.Send(new ClientboundDisconnectPacket(Component.Literal(reason)));
        Connection.Disconnect(reason);
    }

        //Disconnect with a component reason; bans/kicks use a translation key component and the client shows it in its local language
    public void Disconnect(Component reason)
    {
        if (Connection.IsConnected)
            Connection.Send(new ClientboundDisconnectPacket(reason));
        Connection.Disconnect("disconnected");
    }
}
