using System.IO;
using NetCraft.Gpu;
using NetCraft.Resources;
using NetCraft.Registry;
using StbImageSharp;

namespace NetCraft.Game.Client.Render.Atlas;

//BlockTextureCollector 方块纹理收集器
//扫描 assets 资源收集方块纹理 sprite 列表喂给 Gpu 层 BlockTextureAtlas 拼接
//首版简化策略直接扫描 textures/block/*.png 收集全部方块纹理
//后续 W1 模型解析就绪后改为按 blockstates/models JSON 引用精确收集
//属 Game 层依赖 ResourceManager 读 assets + StbImageSharp 解码 PNG + Gpu 的 SpriteInput
public sealed class BlockTextureCollector
{
    private readonly ResourceManager _resourceManager;

    public BlockTextureCollector(ResourceManager resourceManager)
    {
        _resourceManager = resourceManager;
    }

    //Collect 收集所有方块纹理 sprite 输入列表
    //扫描所有 namespace 的 textures/block 目录
    //PNG 解码失败跳过不阻断返回已成功收集的列表
    public List<TextureStitcher.SpriteInput> Collect()
    {
        var result = new List<TextureStitcher.SpriteInput>();
        foreach (var ns in _resourceManager.GetNamespaces(PackType.ClientResources))
        {
            foreach (var resource in _resourceManager.ListResources(PackType.ClientResources, ns, "textures/block"))
            {
                //只收 .png 排除 .png.mcmeta
                if (!resource.Location.Path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                    continue;
                var spriteName = PathToSpriteName(ns, resource.Location.Path);
                var (width, height, data) = LoadPng(resource);
                if (data is null) continue;
                result.Add(new TextureStitcher.SpriteInput(spriteName, width, height, data));
            }
        }
        return result;
    }

    //PathToSpriteName 把资源路径转 sprite name
    //path=textures/block/stone.png → minecraft:block/stone
    //去掉 textures/ 前缀和 .png 后缀
    private static string PathToSpriteName(string ns, string path)
    {
        var p = path;
        if (p.StartsWith("textures/", StringComparison.OrdinalIgnoreCase))
            p = p["textures/".Length..];
        if (p.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            p = p[..^4];
        return $"{ns}:{p}";
    }

    //LoadPng 从 Resource 流解码 PNG
    //返回 (width, height, data) data 为 null 表示解码失败
    private static (int width, int height, byte[]? data) LoadPng(Resource resource)
    {
        try
        {
            using var stream = resource.Open();
            var result = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);
            if (result is null || result.Width <= 0 || result.Height <= 0)
                return (0, 0, null);
            return (result.Width, result.Height, result.Data);
        }
        catch
        {
            return (0, 0, null);
        }
    }
}
