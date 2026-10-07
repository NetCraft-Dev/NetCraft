using NetCraft.Game.Server;
using NetCraft.Game.World.Level;
using NetCraft.Primitives;
using NetCraft.Registry;

namespace NetCraft.Game.Commands.Arguments;

//CommandTarget command-layer entity target, wrapping both players and level entities
//Selector filtering and sorting happen in this layer; execution dispatches concrete behavior by source
//ServerPlayer and Registry.Entity share no base class, so view adaptation happens only here
public sealed class CommandTarget
{
    private CommandTarget(ServerPlayer? player, Entity? worldEntity)
    {
        Player = player;
        WorldEntity = worldEntity;
    }

    //Player player source; null for non-player targets
    public ServerPlayer? Player { get; }

    //WorldEntity level entity source; null for player targets
    public Entity? WorldEntity { get; }

    //OfPlayer wraps a player target
    public static CommandTarget OfPlayer(ServerPlayer player) => new(player, null);

    //OfEntity wraps a level entity target
    public static CommandTarget OfEntity(Entity entity) => new(null, entity);

    //EntityId entity network id; players and entities share the same allocator
    public int EntityId => Player?.EntityId ?? WorldEntity!.EntityId;

    //Type entity type
    public EntityType<object>? Type => Player?.Type ?? WorldEntity!.Type;

    //Uuid unique identifier
    public Guid Uuid => Player?.Uuid ?? WorldEntity!.Uuid;

    //Position entity position
    public Vec3 Position => Player?.Position ?? WorldEntity!.Pos;

    //Yaw yaw angle
    public float Yaw => Player?.Yaw ?? WorldEntity!.YRot;

    //Pitch pitch angle
    public float Pitch => Player?.Pitch ?? WorldEntity!.XRot;

    //Name entity name; players use the profile name, entities use the registry name
    public string Name => Player?.Profile.Name ?? WorldEntity!.Id.ToString();

    //GameType game type; null for non-player targets, matching how the vanilla gamemode selector does not match non-players
    public GameType? GameType => Player?.GameType;

    //BoundingBox hit box; players use a fixed size, entities use their own bounding box
    public AABB BoundingBox
        => Player is not null ? EntitySelector.GetBoundingBox(Player) : WorldEntity!.BoundingBox;
}
