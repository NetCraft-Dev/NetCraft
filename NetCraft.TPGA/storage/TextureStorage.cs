using NetCraft.TPGA.Logging;

namespace NetCraft.TPGA.Storage;

//TextureStorage 贴图文件存储 文件名=sha256 hex
//textures 目录存程序根目录 同 hash 跳过写入节省空间
public sealed class TextureStorage
{
    private readonly string _dir;

    public TextureStorage()
    {
        _dir = Path.Combine(AppContext.BaseDirectory, "textures");
        Directory.CreateDirectory(_dir);
    }

    //Save 写入 data 到 textures/{hash} 已存在跳过
    public void Save(string hash, byte[] data)
    {
        var path = Path.Combine(_dir, hash);
        if (File.Exists(path)) return;
        File.WriteAllBytes(path, data);
        Log.Info("Texture", $"saved hash={hash} size={data.Length}");
    }

    //Load 读取贴图 不存在返回 null
    public byte[]? Load(string hash)
    {
        var path = Path.Combine(_dir, hash);
        return File.Exists(path) ? File.ReadAllBytes(path) : null;
    }

    //Exists 贴图是否存在
    public bool Exists(string hash) => File.Exists(Path.Combine(_dir, hash));

    //Delete 删除贴图 不存在忽略
    public void Delete(string hash)
    {
        var path = Path.Combine(_dir, hash);
        if (File.Exists(path)) File.Delete(path);
    }
}
