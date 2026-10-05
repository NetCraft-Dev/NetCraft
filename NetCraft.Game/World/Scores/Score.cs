using NetCraft.Codec;
using NetCraft.Network.Chat;

namespace NetCraft.Game.World.Scores;

//Score 单条分数 对应原版 net.minecraft.world.scores.Score
//持分数值与锁定标记与可选的自定义显示文本 新建默认锁定
public sealed class Score : ReadOnlyScoreInfo
{
    private int _value;
    private bool _locked = true;
    private Component? _display;

    //Value 读分数值 对应原版 value
    public int Value() => _value;

    //SetValue 写分数值 对应原版 value(int)
    public void SetValue(int value) => _value = value;

    //IsLocked 分数是否锁定 对应原版 isLocked
    public bool IsLocked() => _locked;

    //SetLocked 设置锁定 对应原版 setLocked
    public void SetLocked(bool locked) => _locked = locked;

    //Display 读自定义显示文本 对应原版 display
    public Component? Display() => _display;

    //SetDisplay 写自定义显示文本 对应原版 display(Component)
    public void SetDisplay(Component? display) => _display = display;

    //Packed 分数的存档形态 对应原版 Score.Packed
    //原版的编号格式字段依赖 NumberFormat 未接通故省略
    public sealed record Packed(int Value, bool Locked, Optional<Component> Display)
    {
        //Codec 持久化编解码 字段名 Score/Locked/display 对应原版 MAP_CODEC
        public static readonly Codec<Packed> Codec = RecordCodecBuilder.Of3(
            Codecs.Int.OptionalFieldOf("Score", 0).ForGetter((Packed packed) => packed.Value),
            Codecs.Bool.OptionalFieldOf("Locked", false).ForGetter((Packed packed) => packed.Locked),
            ComponentSerialization.Codec.OptionalFieldOf("display").ForGetter((Packed packed) => packed.Display),
            (value, locked, display) => new Packed(value, locked, display));
    }
}
