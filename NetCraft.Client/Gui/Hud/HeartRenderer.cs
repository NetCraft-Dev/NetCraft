using NetCraft.Game.World;
using NetCraft.Game.World.Entity;
//MobEffect 歧义 NetCraft.Game.World(enum) vs NetCraft.Registry(interface stub) 别名锁到 enum
using MobEffect = NetCraft.Game.World.MobEffect;

namespace NetCraft.Game.Gui.Hud;

//HeartRenderer 心渲染器对标原版 Hud.extractPlayerHealth L782-830 + extractHearts L900-936 + forPlayer L885-897
//forPlayer 检测 POISON/WITHER/FROZEN 选类型
//extractHearts 容器数计算+row/column 布局+低血抖动+再生上跳+吸收心+旧生命闪烁覆盖+当前生命
//blink 由 healthBlinkTime 机制驱动受伤后 20 tick 闪烁恢复后 10 tick
//displayHealth 每 20 tick 追上 currentHealth 产生掉血延迟对标原版 lastHealthTime > 1000ms
public sealed class HeartRenderer
{
    //tickCount 每帧 +1 驱动闪烁相位/低血抖动/再生上跳
    private int _tickCount;
    //healthBlinkTime 受伤后设为 tickCount+20 恢复后设为 tickCount+10 blink 持续期内闪烁
    private int _healthBlinkTime;
    //lastHealth 上一 tick 的 currentHealth 用于检测受伤/恢复触发 blink
    private int _lastHealth = -1;
    //displayHealth 延迟追上 currentHealth 用于 oldHealth 掉血时短暂闪烁旧值
    private int _displayHealth;
    //displayHealthTickCounter 20 tick 计时对标原版 timeMillis - lastHealthTime > 1000
    private int _displayHealthTickCounter;

    //Tick 每帧推进 tickCount 和 blink/displayHealth 状态由 GameScreen.Tick 调用
    public void Tick(Player player)
    {
        _tickCount++;
        int currentHealth = (int)Math.Ceiling(player.Health);
        //首帧初始化 lastHealth/displayHealth 避免首帧误触发 blink
        if (_lastHealth < 0)
        {
            _lastHealth = currentHealth;
            _displayHealth = currentHealth;
        }
        //受伤闪烁 20 tick 恢复闪烁 10 tick 对标原版 healthBlinkTime=invulnerableTime>0 时设置
        //NetCraft 无 invulnerableTime 简化为受伤即闪烁
        if (currentHealth < _lastHealth)
            _healthBlinkTime = _tickCount + 20;
        else if (currentHealth > _lastHealth)
            _healthBlinkTime = _tickCount + 10;
        //displayHealth 每 20 tick 追上 currentHealth 产生掉血延迟
        if (++_displayHealthTickCounter >= 20)
        {
            _displayHealth = currentHealth;
            _displayHealthTickCounter = 0;
        }
        _lastHealth = currentHealth;
    }

    //ForPlayer 检测效果选 HeartType 对标原版 forPlayer L885-897
    public static HeartType ForPlayer(Player player)
    {
        if (player.ActiveEffects.Contains(MobEffect.Poison)) return HeartType.Poisoned;
        if (player.ActiveEffects.Contains(MobEffect.Wither)) return HeartType.Withered;
        if (player.IsFullyFrozen) return HeartType.Frozen;
        return HeartType.Normal;
    }

    //ExtractHearts 完整心渲染算法对标原版 extractHearts L900-936
    //renderHeart 委托由调用方传入实际调 DrawSprite(identifier, xo, yo, 9, 9, tint)
    public void ExtractHearts(Player player, int xLeft, int yLineBase, int healthRowHeight,
        Action<string, int, int> renderHeart)
    {
        int currentHealth = (int)Math.Ceiling(player.Health);
        int absorption = (int)Math.Ceiling(player.AbsorptionHealth);
        //blink 受伤后闪烁期内 (差/3)%2==1 时闪烁 对标原版 L788
        bool blink = _healthBlinkTime > _tickCount && ((_healthBlinkTime - _tickCount) / 3) % 2 == 1;
        //random 每 tick 重置种子保证抖动一致性 对标原版 random.setSeed(tickCount*312871)
        var random = new Random(_tickCount * 312871);
        int oldHealth = _displayHealth;
        //maxHealth 取三者最大 对标原版 max(maxHealth, max(oldHealth, currentHealth)) L807
        float maxHealth = Math.Max(player.MaxHealth, Math.Max(oldHealth, currentHealth));

        HeartType type = ForPlayer(player);
        bool isHardcore = player.IsHardcore;
        int healthContainerCount = (int)Math.Ceiling(maxHealth / 2.0);
        int absorptionContainerCount = (int)Math.Ceiling(absorption / 2.0);
        int maxHealthHalvesCount = healthContainerCount * 2;

        //heartOffsetIndex 仅 REGENERATION 时启用 对标原版 tickCount % ceil(maxHealth + 5) L812-814
        int heartOffsetIndex = player.ActiveEffects.Contains(MobEffect.Regeneration)
            ? _tickCount % (int)Math.Ceiling(maxHealth + 5.0f)
            : -1;

        //containerIndex 从高到低 高 index = 吸收心 低 index = 生命心
        for (int containerIndex = healthContainerCount + absorptionContainerCount - 1; containerIndex >= 0; containerIndex--)
        {
            int row = containerIndex / 10;
            int column = containerIndex % 10;
            int xo = xLeft + column * 8;
            int yo = yLineBase - row * healthRowHeight;
            //低血抖动 currentHealth+absorption<=4 时 yo 偏移 对标原版 L913-914
            if (currentHealth + absorption <= 4)
                yo += random.Next(2);
            //再生上跳 仅生命心 对标原版 containerIndex<healthContainerCount && ==heartOffsetIndex L916
            if (containerIndex < healthContainerCount && containerIndex == heartOffsetIndex)
                yo -= 2;

            //1. 容器背景 用 blink 参数低血时容器也闪烁 对标原版 L919
            renderHeart(HeartSprites.GetSprite(HeartType.Container, isHardcore, false, blink), xo, yo);

            int halves = containerIndex * 2;
            bool isAbsorptionHeart = containerIndex >= healthContainerCount;
            //2. 吸收心 凋零时吸收心保持 WITHERED 否则 ABSORBING 对标原版 L921-924
            if (isAbsorptionHeart)
            {
                int absorptionHalves = halves - maxHealthHalvesCount;
                if (absorptionHalves < absorption)
                {
                    bool halfHeart = absorptionHalves + 1 == absorption;
                    HeartType absorbType = type == HeartType.Withered ? HeartType.Withered : HeartType.Absorbing;
                    renderHeart(HeartSprites.GetSprite(absorbType, isHardcore, halfHeart, false), xo, yo);
                }
            }
            //3. 旧生命闪烁 blink && halves < oldHealth 对标原版 L926-928
            if (blink && halves < oldHealth)
            {
                bool halfHeart2 = halves + 1 == oldHealth;
                renderHeart(HeartSprites.GetSprite(type, isHardcore, halfHeart2, true), xo, yo);
            }
            //4. 当前生命 halves < currentHealth 对标原版 L930-932
            if (halves < currentHealth)
            {
                bool halfHeart3 = halves + 1 == currentHealth;
                renderHeart(HeartSprites.GetSprite(type, isHardcore, halfHeart3, false), xo, yo);
            }
        }
    }
}
