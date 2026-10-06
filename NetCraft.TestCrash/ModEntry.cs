using NetCraft.Logging;

namespace NetCraft.TestCrash;

//ModEntry mod entry point
//The loader finds this class through the entry field in ncmod.json and calls Init once at load time
public sealed partial class ModEntry
{
    //Init called once when the mod loads
    //Each side keeps its subscriptions in its own partial method; the half that does not exist on
    //this side is never generated and the call is compiled away
    public Task Init()
    {
        Log.Info("NetCraft.TestCrash loaded");
        InitServer();
        InitClient();
        return Task.CompletedTask;
    }

    //InitServer server-side subscriptions; implemented only when environment is both or server
    static partial void InitServer();

    //InitClient client-side subscriptions; implemented only when environment is both or client
    static partial void InitClient();
}