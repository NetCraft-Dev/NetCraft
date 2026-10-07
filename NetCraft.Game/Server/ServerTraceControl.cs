namespace NetCraft.Game.Server;

//ServerTraceControl drives one runtime trace capture, the operation entry for /debug trace
//The host supplies the implementation: capturing runtime events needs the diagnostics client, which only the server host carries
//A capture is written as a nettrace file, openable by PerfView, dotnet-trace and Visual Studio
public abstract class ServerTraceControl
{
    //Start begins a capture and reports the file being written
    //Returns null when a capture is already running or one could not be started; the reason goes to the server log
    public abstract string? Start();

    //Stop ends the running capture and reports the file it wrote; returns null when none was running
    public abstract string? Stop();
}
