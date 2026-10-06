using NetCraft.Logging;

namespace NetCraft.TestCrash;

//Minimal injection probe: hook CrashReport.ForThrowable and log one line right before a crash
//report is created. Uses CallSite + Before so the original call still runs untouched.
public static class CrashProbe
{
    //Inserted before the CrashReport.ForThrowable call
    //Probe signatures may only use BCL types and object, so the first parameter is object
    public static void OnBeforeForThrowable(object exception, string title)
    {
        Log.Info($"[TestCrash] crash report about to be created: {title}");
    }

    //Inserted before the Stop() call inside the main loop's catch block, which is the crash
    //path: the report has just been written and the original Stop still runs afterwards
    public static void OnBeforeCrashStop(object self)
    {
        Log.Info("[TestCrash] server tick crashed, stopping");
    }
}