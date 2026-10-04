using System.IO;

namespace NetCraft.Util;

//文件工具对应原版FileUtil
public static class FileUtil
{
    //幂等创建目录对应原版createDirectoriesSafe
    //Directory.CreateDirectory已自带幂等，吞竞态产生的IOException
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
