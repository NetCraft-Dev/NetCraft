namespace NetCraft.Gpu;

//TextureSetup 纹理绑定配置对标原版 TextureSetup
//最多 3 个 texture+sampler 对参与合批排序
//Equals 引用相等用于 SortElements 合批判断
public sealed class TextureSetup
{
    public GpuImage? Texture0 { get; }
    public GpuSampler? Sampler0 { get; }
    public GpuImage? Texture1 { get; }
    public GpuSampler? Sampler1 { get; }
    public GpuImage? Texture2 { get; }
    public GpuSampler? Sampler2 { get; }

    private TextureSetup(GpuImage? t0, GpuSampler? s0, GpuImage? t1, GpuSampler? s1, GpuImage? t2, GpuSampler? s2)
    {
        Texture0 = t0; Sampler0 = s0;
        Texture1 = t1; Sampler1 = s1;
        Texture2 = t2; Sampler2 = s2;
    }

    //NoTexture 无纹理纯色管线用共享单例
    public static readonly TextureSetup NoTexture = new(null, null, null, null, null, null);

    //SingleTexture 单纹理绑定
    public static TextureSetup SingleTexture(GpuImage texture, GpuSampler sampler)
        => new(texture, sampler, null, null, null, null);

    //SingleTextureWithLightmap 单纹理+光照图双纹理
    public static TextureSetup SingleTextureWithLightmap(GpuImage texture, GpuSampler sampler, GpuImage lightmap, GpuSampler lightmapSampler)
        => new(texture, sampler, lightmap, lightmapSampler, null, null);

    public override bool Equals(object? obj)
    {
        if (obj is not TextureSetup other) return false;
        return ReferenceEquals(Texture0, other.Texture0) && ReferenceEquals(Sampler0, other.Sampler0)
            && ReferenceEquals(Texture1, other.Texture1) && ReferenceEquals(Sampler1, other.Sampler1)
            && ReferenceEquals(Texture2, other.Texture2) && ReferenceEquals(Sampler2, other.Sampler2);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Texture0); hash.Add(Sampler0);
        hash.Add(Texture1); hash.Add(Sampler1);
        hash.Add(Texture2); hash.Add(Sampler2);
        return hash.ToHashCode();
    }
}
