namespace NetCraft.Game.World;

//MobEffect 药水效果枚举对标原版 net.minecraft.world.effect.MobEffect
//P1 仅定义 forPlayer 检测所需的最小集后续按需扩展
public enum MobEffect
{
    //Poison 中毒 POISONED 心（原版拼写 POISIONED）
    Poison,
    //Wither 凋零 WITHERED 心
    Wither,
    //Regeneration 再生驱动心跳上跳动画 heartOffsetIndex
    Regeneration,
}
