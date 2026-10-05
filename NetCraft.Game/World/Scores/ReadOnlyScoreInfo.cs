namespace NetCraft.Game.World.Scores;

//ReadOnlyScoreInfo 只读的分数视图 对应原版 net.minecraft.world.scores.ReadOnlyScoreInfo
//原版的 formatValue 依赖 NumberFormat 本项目编号格式体系未接通 暂不提供
public interface ReadOnlyScoreInfo
{
    //Value 分数值 对应原版 value
    int Value();

    //IsLocked 是否锁定 锁定的分数不接受命令改动 对应原版 isLocked
    bool IsLocked();
}
