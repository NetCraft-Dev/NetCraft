using NetCraft.Codec;
using NetCraft.Network.Chat;

namespace NetCraft.Game.World.Scores;

//Objective scoreboard objective, maps to vanilla net.minecraft.world.scores.Objective
//An objective binds one criteria and holds a display name and render type
//The vanilla number format field depends on network.chat.numbers, which is not wired up, so it is not provided yet
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

    //Scoreboard owning scoreboard, maps to vanilla getScoreboard
    public Scoreboard Scoreboard { get; }

    //Name objective name, maps to vanilla getName
    public string Name { get; }

    //Criteria the criteria, maps to vanilla getCriteria
    public ObjectiveCriteria Criteria { get; }

    //DisplayName display name, maps to vanilla getDisplayName
    public Component DisplayName => _displayName;

    //SetDisplayName changes the display name and notifies the scoreboard, maps to vanilla setDisplayName
    public void SetDisplayName(Component displayName)
    {
        _displayName = displayName;
        Scoreboard.OnObjectiveChanged(this);
    }

    //RenderType render type, maps to vanilla getRenderType
    public ObjectiveCriteria.RenderType RenderType { get; private set; }

    //SetRenderType changes the render type and notifies the scoreboard, maps to vanilla setRenderType
    public void SetRenderType(ObjectiveCriteria.RenderType renderType)
    {
        RenderType = renderType;
        Scoreboard.OnObjectiveChanged(this);
    }

    //DisplayAutoUpdate whether the display refreshes automatically when scores change, maps to vanilla displayAutoUpdate
    public bool DisplayAutoUpdate { get; private set; }

    //SetDisplayAutoUpdate changes auto update and notifies the scoreboard, maps to vanilla setDisplayAutoUpdate
    public void SetDisplayAutoUpdate(bool displayAutoUpdate)
    {
        DisplayAutoUpdate = displayAutoUpdate;
        Scoreboard.OnObjectiveChanged(this);
    }

    //Packed save form of the objective, maps to vanilla Objective.Packed
    //The vanilla number format fields depend on NumberFormat, not wired up, so they are omitted
    public sealed record Packed(string Name, ObjectiveCriteria Criteria, Component DisplayName,
        ObjectiveCriteria.RenderType RenderType, bool DisplayAutoUpdate)
    {
        //Codec persistence codec, field names Name/CriteriaName/DisplayName/RenderType/display_auto_update, maps to vanilla CODEC
        public static readonly Codec<Packed> Codec = RecordCodecBuilder.Of5(
            Codecs.String.FieldOf("Name").ForGetter((Packed packed) => packed.Name),
            ObjectiveCriteria.Codec.OptionalFieldOf("CriteriaName", ObjectiveCriteria.DUMMY)
                .ForGetter((Packed packed) => packed.Criteria),
            ComponentSerialization.Codec.FieldOf("DisplayName").ForGetter((Packed packed) => packed.DisplayName),
            ObjectiveCriteria.RenderTypeCodec.OptionalFieldOf("RenderType", ObjectiveCriteria.RenderType.INTEGER)
                .ForGetter((Packed packed) => packed.RenderType),
            Codecs.Bool.OptionalFieldOf("display_auto_update", false)
                .ForGetter((Packed packed) => packed.DisplayAutoUpdate),
            (name, criteria, displayName, renderType, displayAutoUpdate) =>
                new Packed(name, criteria, displayName, renderType, displayAutoUpdate));
    }
}
