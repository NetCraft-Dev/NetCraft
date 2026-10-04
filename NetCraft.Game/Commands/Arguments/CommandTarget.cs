using NetCraft.Game.Server;
using NetCraft.Game.World.Level;
using NetCraft.Primitives;
using NetCraft.Registry;

namespace NetCraft.Game.Commands.Arguments;

//CommandTarget 命令层实体目标 统一包装玩家与关卡实体两类来源
//选择器的过滤与排序都在这层做 命令执行时再按来源分派具体行为
//ServerPlayer 与 Registry.Entity 没有共同基类 只在这里做视图适配
public sealed class CommandTarget
{
    private CommandTarget(ServerPlayer? player, Entity? worldEntity)
    {
        Player = player;
        WorldEntity = worldEntity;
    }

    //Player 玩家来源 非玩家目标为 null
    public ServerPlayer? Player { get; }

    //WorldEntity 关卡实体来源 玩家目标为 null
    public Entity? WorldEntity { get; }

    //OfPlayer 包装玩家目标
    public static CommandTarget OfPlayer(ServerPlayer player) => new(player, null);

    //OfEntity 包装关卡实体目标
    public static CommandTarget OfEntity(Entity entity) => new(null, entity);

    //EntityId 实体网络 id 玩家与实体共用同一分配器
    public int EntityId => Player?.EntityId ?? WorldEntity!.EntityId;

    //Type 实体类型
    public EntityType<object>? Type => Player?.Type ?? WorldEntity!.Type;

    //Uuid 唯一标识
    public Guid Uuid => Player?.Uuid ?? WorldEntity!.Uuid;

    //Position 实体位置
    public Vec3 Position => Player?.Position ?? WorldEntity!.Pos;

    //Yaw 偏航角
    public float Yaw => Player?.Yaw ?? WorldEntity!.YRot;

    //Pitch 俯仰角
    public float Pitch => Player?.Pitch ?? WorldEntity!.XRot;

    //Name 实体名 玩家取档案名 实体取注册名
    public string Name => Player?.Profile.Name ?? WorldEntity!.Id.ToString();

    //GameType 游戏模式 非玩家目标为 null 对应原版 gamemode 选项对非玩家不匹配
    public GameType? GameType => Player?.GameType;

    //BoundingBox 碰撞箱 玩家按固定体型 实体取自身包围盒
    public AABB BoundingBox
        => Player is not null ? EntitySelector.GetBoundingBox(Player) : WorldEntity!.BoundingBox;
}
