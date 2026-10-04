namespace NetCraft.Game.Gui.Hud;

//HeartType 心类型枚举对标原版 Hud.HeartType（6 种）
//每种 8 sprite 变体（full/half × blink × hardcore）container 无 half 变体 full/half 都用 container
public enum HeartType
{
    //Container 容器背景空心
    Container,
    //Normal 普通红心
    Normal,
    //Poisoned 中毒绿心（原版拼写 POISIONED）
    Poisoned,
    //Withered 凋零黑心
    Withered,
    //Absorbing 吸收金心（不在 forPlayer 内由 extractHearts 单独处理）
    Absorbing,
    //Frozen 冻结蓝心
    Frozen,
}

//HeartSprites 心 sprite identifier 工厂对标原版 HeartType.getSprite 三元选择
//路径格式 minecraft:textures/gui/sprites/hud/heart/{type}[_state][_blinking][_hardcore]
//8 变体索引 = (isHardcore?4:0) | (isHalf?2:0) | (isBlink?1:0)
public static class HeartSprites
{
    private const string Prefix = "minecraft:textures/gui/sprites/hud/heart/";

    //每种 8 变体路径对标原版 HeartType 构造参数顺序
    //0=full 1=fullBlinking 2=half 3=halfBlinking 4=hardcoreFull 5=hardcoreFullBlinking 6=hardcoreHalf 7=hardcoreHalfBlinking
    //Container 无 half 变体 full/half 都用 container（原版同此）
    private static readonly string[][] Paths = {
        new[] { "container", "container_blinking", "container", "container_blinking", "container_hardcore", "container_hardcore_blinking", "container_hardcore", "container_hardcore_blinking" },
        new[] { "full", "full_blinking", "half", "half_blinking", "hardcore_full", "hardcore_full_blinking", "hardcore_half", "hardcore_half_blinking" },
        new[] { "poisoned_full", "poisoned_full_blinking", "poisoned_half", "poisoned_half_blinking", "poisoned_hardcore_full", "poisoned_hardcore_full_blinking", "poisoned_hardcore_half", "poisoned_hardcore_half_blinking" },
        new[] { "withered_full", "withered_full_blinking", "withered_half", "withered_half_blinking", "withered_hardcore_full", "withered_hardcore_full_blinking", "withered_hardcore_half", "withered_hardcore_half_blinking" },
        new[] { "absorbing_full", "absorbing_full_blinking", "absorbing_half", "absorbing_half_blinking", "absorbing_hardcore_full", "absorbing_hardcore_full_blinking", "absorbing_hardcore_half", "absorbing_hardcore_half_blinking" },
        new[] { "frozen_full", "frozen_full_blinking", "frozen_half", "frozen_half_blinking", "frozen_hardcore_full", "frozen_hardcore_full_blinking", "frozen_hardcore_half", "frozen_hardcore_half_blinking" },
    };

    //GetSprite 三元选择返回 sprite identifier 对标原版 HeartType.getSprite
    public static string GetSprite(HeartType type, bool isHardcore, bool isHalf, bool isBlink)
    {
        int idx = (isHardcore ? 4 : 0) | (isHalf ? 2 : 0) | (isBlink ? 1 : 0);
        return Prefix + Paths[(int)type][idx];
    }
}
