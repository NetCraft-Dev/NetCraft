using System.IO;
using NetCraft.Gpu;
using NetCraft.Resources;
using NetCraft.Registry;
using StbImageSharp;

namespace NetCraft.Game.Client.Render.Atlas;

//BlockTextureCollector block texture collector
//Scans assets resources to collect the block texture sprite list and feeds it to the Gpu layer's BlockTextureAtlas for stitching
//The first version's simplified strategy scans textures/block/*.png directly to collect all block textures
//Later, once W1 model parsing is ready, switch to collecting precisely from blockstates/models JSON references
//Belongs to the Game layer; depends on ResourceManager to read assets + StbImageSharp to decode PNG + Gpu's SpriteInput
public sealed class BlockTextureCollector
{
    private readonly ResourceManager _resourceManager;

    public BlockTextureCollector(ResourceManager resourceManager)
    {
        _resourceManager = resourceManager;
    }

    //Collect collects the sprite input list for all block textures
    //Scans the textures/block directory of all namespaces
    //Failed PNG decodes are skipped without aborting; returns the successfully collected list
    public List<TextureStitcher.SpriteInput> Collect()
    {
        var result = new List<TextureStitcher.SpriteInput>();
        foreach (var ns in _resourceManager.GetNamespaces(PackType.ClientResources))
        {
            foreach (var resource in _resourceManager.ListResources(PackType.ClientResources, ns, "textures/block"))
            {
                //Only collect .png, excluding .png.mcmeta
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

    //PathToSpriteName converts a resource path to a sprite name
    //path=textures/block/stone.png → minecraft:block/stone
    //Strips the textures/ prefix and .png suffix
    private static string PathToSpriteName(string ns, string path)
    {
        var p = path;
        if (p.StartsWith("textures/", StringComparison.OrdinalIgnoreCase))
            p = p["textures/".Length..];
        if (p.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            p = p[..^4];
        return $"{ns}:{p}";
    }

    //LoadPng decodes a PNG from a Resource stream
    //Returns (width, height, data); data is null when decoding fails
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
