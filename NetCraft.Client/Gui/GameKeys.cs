namespace NetCraft.Game.Gui;

//GameKeys 业务键盘码常量与 Silk.NET.Input.Key 数值一致
//避免 Game 层依赖 Silk.NET.Input 命名空间
public static class GameKeys
{
    //Esc 切屏键
    public const int Escape = 256;
    //F3 调试键
    public const int F3 = 290;
    //E 打开背包界面 对应原版 keyInventory
    public const int E = 69;
    //Q 丢弃手持物品 对应原版 keyDrop
    public const int Q = 81;
    //数字键 1-9 起止对应 slot 0-8
    public const int D1 = 49;
    public const int D9 = 57;
    //小键盘 1-9 起止对应 slot 0-8
    public const int Keypad1 = 321;
    public const int Keypad9 = 329;
}
