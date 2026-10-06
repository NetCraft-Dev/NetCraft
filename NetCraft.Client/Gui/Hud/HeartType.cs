namespace NetCraft.Game.Gui.Hud;

//HeartType heart type enum, maps to vanilla Hud.HeartType (6 kinds)
//Each has 8 sprite variants (full/half × blink × hardcore); container has no half variant, both full/half use container
public enum HeartType
{
    //Container container background, empty heart
    Container,
    //Normal normal red heart
    Normal,
    //Poisoned poisoned green heart (vanilla spells it POISIONED)
    Poisoned,
    //Withered withered black heart
    Withered,
    //Absorbing absorption golden heart (not inside forPlayer; handled separately by extractHearts)
    Absorbing,
    //Frozen frozen blue heart
    Frozen,
}

//HeartSprites heart sprite identifier factory, maps to vanilla HeartType.getSprite's ternary selection
//Path format minecraft:textures/gui/sprites/hud/heart/{type}[_state][_blinking][_hardcore]
//8 variant index = (isHardcore?4:0) | (isHalf?2:0) | (isBlink?1:0)
public static class HeartSprites
{
    private const string Prefix = "minecraft:textures/gui/sprites/hud/heart/";

    //8 variant paths per type, matching the vanilla HeartType constructor parameter order
    //0=full 1=fullBlinking 2=half 3=halfBlinking 4=hardcoreFull 5=hardcoreFullBlinking 6=hardcoreHalf 7=hardcoreHalfBlinking
    //Container has no half variant; both full/half use container (same as vanilla)
    private static readonly string[][] Paths = {
        new[] { "container", "container_blinking", "container", "container_blinking", "container_hardcore", "container_hardcore_blinking", "container_hardcore", "container_hardcore_blinking" },
        new[] { "full", "full_blinking", "half", "half_blinking", "hardcore_full", "hardcore_full_blinking", "hardcore_half", "hardcore_half_blinking" },
        new[] { "poisoned_full", "poisoned_full_blinking", "poisoned_half", "poisoned_half_blinking", "poisoned_hardcore_full", "poisoned_hardcore_full_blinking", "poisoned_hardcore_half", "poisoned_hardcore_half_blinking" },
        new[] { "withered_full", "withered_full_blinking", "withered_half", "withered_half_blinking", "withered_hardcore_full", "withered_hardcore_full_blinking", "withered_hardcore_half", "withered_hardcore_half_blinking" },
        new[] { "absorbing_full", "absorbing_full_blinking", "absorbing_half", "absorbing_half_blinking", "absorbing_hardcore_full", "absorbing_hardcore_full_blinking", "absorbing_hardcore_half", "absorbing_hardcore_half_blinking" },
        new[] { "frozen_full", "frozen_full_blinking", "frozen_half", "frozen_half_blinking", "frozen_hardcore_full", "frozen_hardcore_full_blinking", "frozen_hardcore_half", "frozen_hardcore_half_blinking" },
    };

    //GetSprite ternary selection returning the sprite identifier, maps to vanilla HeartType.getSprite
    public static string GetSprite(HeartType type, bool isHardcore, bool isHalf, bool isBlink)
    {
        int idx = (isHardcore ? 4 : 0) | (isHalf ? 2 : 0) | (isBlink ? 1 : 0);
        return Prefix + Paths[(int)type][idx];
    }
}
