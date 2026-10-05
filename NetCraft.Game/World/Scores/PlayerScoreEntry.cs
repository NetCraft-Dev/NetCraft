using NetCraft.Network.Chat;

namespace NetCraft.Game.World.Scores;

//PlayerScoreEntry 计分板条目快照 对应原版 net.minecraft.world.scores.PlayerScoreEntry
//供显示与列表查询用 原版的编号格式覆盖字段未接通故省略
public sealed record PlayerScoreEntry(string Owner, int Value, Component? Display)
{
    //IsHidden 以井号开头的条目对玩家隐藏 对应原版 isHidden
    public bool IsHidden => Owner.StartsWith('#');

    //OwnerName 去掉隐藏前缀后的名字 对应原版 ownerName
    public string OwnerName() => IsHidden ? Owner[1..] : Owner;
}
