using NetCraft.Network.Chat;

namespace NetCraft.Game.World.Scores;

//Objective 计分目标 对应原版 net.minecraft.world.scores.Objective
//一个目标绑定一个计分标准 持显示名与渲染类型
//原版的编号格式字段依赖 network.chat.numbers 该体系未接通 暂不提供
public sealed class Objective
{
    private Component _displayName;

    public Objective(Scoreboard scoreboard, string name, ObjectiveCriteria criteria, Component displayName,
        ObjectiveCriteria.RenderType renderType, bool displayAutoUpdate)
    {
        Scoreboard = scoreboard;
        Name = name;
        Criteria = criteria;
        _displayName = displayName;
        RenderType = renderType;
        DisplayAutoUpdate = displayAutoUpdate;
    }

    //Scoreboard 所属计分板 对应原版 getScoreboard
    public Scoreboard Scoreboard { get; }

    //Name 目标名 对应原版 getName
    public string Name { get; }

    //Criteria 计分标准 对应原版 getCriteria
    public ObjectiveCriteria Criteria { get; }

    //DisplayName 显示名 对应原版 getDisplayName
    public Component DisplayName => _displayName;

    //SetDisplayName 改显示名并通知计分板 对应原版 setDisplayName
    public void SetDisplayName(Component displayName)
    {
        _displayName = displayName;
        Scoreboard.OnObjectiveChanged(this);
    }

    //RenderType 渲染类型 对应原版 getRenderType
    public ObjectiveCriteria.RenderType RenderType { get; private set; }

    //SetRenderType 改渲染类型并通知计分板 对应原版 setRenderType
    public void SetRenderType(ObjectiveCriteria.RenderType renderType)
    {
        RenderType = renderType;
        Scoreboard.OnObjectiveChanged(this);
    }

    //DisplayAutoUpdate 分数变化时是否自动刷新显示 对应原版 displayAutoUpdate
    public bool DisplayAutoUpdate { get; private set; }

    //SetDisplayAutoUpdate 改自动刷新并通知计分板 对应原版 setDisplayAutoUpdate
    public void SetDisplayAutoUpdate(bool displayAutoUpdate)
    {
        DisplayAutoUpdate = displayAutoUpdate;
        Scoreboard.OnObjectiveChanged(this);
    }
}
