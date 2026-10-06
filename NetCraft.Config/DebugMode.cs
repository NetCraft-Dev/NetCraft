namespace NetCraft.Config;

//Global debug mode switch, maps to vanilla SharedConstants.IS_DEBUG
//Mutable at runtime, set by the Loader --debug flag or test code
//Subsystems check IsEnabled to enable extra debug behavior such as startup info, verbose logs and assertions
public static class DebugMode
{
    public static bool IsEnabled { get; set; }
}
