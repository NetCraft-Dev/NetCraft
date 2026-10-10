namespace NetCraft.Client.Gui;

//GameKeys business key code constants, matching Silk.NET.Input.Key values
//Avoids the Game layer depending on the Silk.NET.Input namespace
public static class GameKeys
{
    //Esc screen toggle key
    public const int Escape = 256;
    //F3 debug key
    public const int F3 = 290;
    //E opens the inventory screen, maps to vanilla keyInventory
    public const int E = 69;
    //Q drops the held item, maps to vanilla keyDrop
    public const int Q = 81;
    //Number keys 1-9 from start to end map to slots 0-8
    public const int D1 = 49;
    public const int D9 = 57;
    //Keypad 1-9 from start to end map to slots 0-8
    public const int Keypad1 = 321;
    public const int Keypad9 = 329;
}
