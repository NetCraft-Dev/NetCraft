using System.IO;

namespace NetCraft.Util;

//File utilities, maps to vanilla FileUtil
public static class FileUtil
{
    //Idempotent directory creation, maps to vanilla createDirectoriesSafe
    //Directory.CreateDirectory is already idempotent, swallows race-induced IOException
    public static void CreateDirectoriesSafe(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
        }
        catch (IOException)
        {
            if (!Directory.Exists(path)) throw;
        }
    }
}
