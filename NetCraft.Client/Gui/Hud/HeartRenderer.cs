using NetCraft.Game.World;
using NetCraft.Game.World.Entity;
//MobEffect ambiguous: NetCraft.Game.World (enum) vs NetCraft.Registry (interface stub); alias pins it to the enum
using MobEffect = NetCraft.Game.World.MobEffect;

namespace NetCraft.Game.Gui.Hud;

//HeartRenderer heart renderer, maps to vanilla Hud.extractPlayerHealth L782-830 + extractHearts L900-936 + forPlayer L885-897
//forPlayer detects POISON/WITHER/FROZEN to pick the type
//extractHearts container count + row/column layout + low-health jitter + regeneration bounce + absorption hearts + old-health blink overlay + current health
//blink is driven by the healthBlinkTime mechanism: 20 ticks after being hurt, 10 ticks after recovery
//displayHealth catches up to currentHealth every 20 ticks to produce a health-loss delay, maps to vanilla lastHealthTime > 1000ms
public sealed class HeartRenderer
{
    //tickCount increments each frame, driving blink phase/low-health jitter/regeneration bounce
    private int _tickCount;
    //healthBlinkTime set to tickCount+20 after being hurt and tickCount+10 after recovery; blinks while active
    private int _healthBlinkTime;
    //lastHealth previous tick's currentHealth, used to detect hurt/recovery and trigger blink
    private int _lastHealth = -1;
    //displayHealth catches up to currentHealth with delay, used to briefly blink the old value while oldHealth loses health
    private int _displayHealth;
    //displayHealthTickCounter 20-tick timer, maps to vanilla timeMillis - lastHealthTime > 1000
    private int _displayHealthTickCounter;

    //Tick advances tickCount and blink/displayHealth state each frame, called by GameScreen.Tick
    public void Tick(Player player)
    {
        _tickCount++;
        int currentHealth = (int)Math.Ceiling(player.Health);
        //Initialize lastHealth/displayHealth on the first frame to avoid a spurious blink
        if (_lastHealth < 0)
        {
            _lastHealth = currentHealth;
            _displayHealth = currentHealth;
        }
        //Hurt blinks 20 ticks, recovery blinks 10 ticks, maps to vanilla setting healthBlinkTime when invulnerableTime > 0
        //NetCraft has no invulnerableTime; simplified to blink on any hurt
        if (currentHealth < _lastHealth)
            _healthBlinkTime = _tickCount + 20;
        else if (currentHealth > _lastHealth)
            _healthBlinkTime = _tickCount + 10;
        //displayHealth catches up to currentHealth every 20 ticks to produce a health-loss delay
        if (++_displayHealthTickCounter >= 20)
        {
            _displayHealth = currentHealth;
            _displayHealthTickCounter = 0;
        }
        _lastHealth = currentHealth;
    }

    //ForPlayer detects effects and picks HeartType, maps to vanilla forPlayer L885-897
    public static HeartType ForPlayer(Player player)
    {
        if (player.ActiveEffects.Contains(MobEffect.Poison)) return HeartType.Poisoned;
        if (player.ActiveEffects.Contains(MobEffect.Wither)) return HeartType.Withered;
        if (player.IsFullyFrozen) return HeartType.Frozen;
        return HeartType.Normal;
    }

    //ExtractHearts full heart render algorithm, maps to vanilla extractHearts L900-936
    //The renderHeart delegate is passed in by the caller and actually calls DrawSprite(identifier, xo, yo, 9, 9, tint)
    public void ExtractHearts(Player player, int xLeft, int yLineBase, int healthRowHeight,
        Action<string, int, int> renderHeart)
    {
        int currentHealth = (int)Math.Ceiling(player.Health);
        int absorption = (int)Math.Ceiling(player.AbsorptionHealth);
        //blink blinks while (diff/3)%2==1 during the hurt blink window, maps to vanilla L788
        bool blink = _healthBlinkTime > _tickCount && ((_healthBlinkTime - _tickCount) / 3) % 2 == 1;
        //random reseeds each tick for consistent jitter, maps to vanilla random.setSeed(tickCount*312871)
        var random = new Random(_tickCount * 312871);
        int oldHealth = _displayHealth;
        //maxHealth takes the max of the three, maps to vanilla max(maxHealth, max(oldHealth, currentHealth)) L807
        float maxHealth = Math.Max(player.MaxHealth, Math.Max(oldHealth, currentHealth));

        HeartType type = ForPlayer(player);
        bool isHardcore = player.IsHardcore;
        int healthContainerCount = (int)Math.Ceiling(maxHealth / 2.0);
        int absorptionContainerCount = (int)Math.Ceiling(absorption / 2.0);
        int maxHealthHalvesCount = healthContainerCount * 2;

        //heartOffsetIndex only enabled with REGENERATION, maps to vanilla tickCount % ceil(maxHealth + 5) L812-814
        int heartOffsetIndex = player.ActiveEffects.Contains(MobEffect.Regeneration)
            ? _tickCount % (int)Math.Ceiling(maxHealth + 5.0f)
            : -1;

        //containerIndex from high to low: high index = absorption hearts, low index = health hearts
        for (int containerIndex = healthContainerCount + absorptionContainerCount - 1; containerIndex >= 0; containerIndex--)
        {
            int row = containerIndex / 10;
            int column = containerIndex % 10;
            int xo = xLeft + column * 8;
            int yo = yLineBase - row * healthRowHeight;
            //Low-health jitter: yo offset when currentHealth+absorption<=4, maps to vanilla L913-914
            if (currentHealth + absorption <= 4)
                yo += random.Next(2);
            //Regeneration bounce, health hearts only, maps to vanilla containerIndex<healthContainerCount && ==heartOffsetIndex L916
            if (containerIndex < healthContainerCount && containerIndex == heartOffsetIndex)
                yo -= 2;

            //1. Container background; using the blink parameter, containers also blink at low health, maps to vanilla L919
            renderHeart(HeartSprites.GetSprite(HeartType.Container, isHardcore, false, blink), xo, yo);

            int halves = containerIndex * 2;
            bool isAbsorptionHeart = containerIndex >= healthContainerCount;
            //2. Absorption hearts; while withered they stay WITHERED, otherwise ABSORBING, maps to vanilla L921-924
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
            //3. Old-health blink, blink && halves < oldHealth, maps to vanilla L926-928
            if (blink && halves < oldHealth)
            {
                bool halfHeart2 = halves + 1 == oldHealth;
                renderHeart(HeartSprites.GetSprite(type, isHardcore, halfHeart2, true), xo, yo);
            }
            //4. Current health, halves < currentHealth, maps to vanilla L930-932
            if (halves < currentHealth)
            {
                bool halfHeart3 = halves + 1 == currentHealth;
                renderHeart(HeartSprites.GetSprite(type, isHardcore, halfHeart3, false), xo, yo);
            }
        }
    }
}
