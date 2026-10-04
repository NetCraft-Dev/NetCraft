using System.Collections.Concurrent;

namespace NetCraft.Gpu.Font;

//FontSet 对标原版 FontSet 单字体集
//持有 providers 链按顺序查找 codepoint 首个命中返回 IUnbakedGlyph
//原版 FontSet.getGlyph 返回 BakedGlyph 内部持 wrappedStitcher 懒 Bake
//F3 阶段暂不接 Stitcher Bake 由调用方处理 F6 注入 Stitcher 后重构
public sealed class FontSet : IDisposable
{
    private List<IGlyphProvider.Conditional> _allProviders = new();
    private List<IGlyphProvider> _activeProviders = new();
    private readonly ConcurrentDictionary<int, IUnbakedGlyph?> _cache = new();

    public FontSet() { }

    //Reload 接收完整 providers 列表和当前激活的 FontOption 集合
    //对标原版 reload(providers, options)
    public void Reload(IEnumerable<IGlyphProvider.Conditional> providers, IReadOnlySet<FontOption> options)
    {
        _allProviders = providers.ToList();
        Reload(options);
    }

    //Reload 仅按 options 重新选择 active providers
    //对标原版 reload(options)
    public void Reload(IReadOnlySet<FontOption> options)
    {
        _activeProviders = _allProviders
            .Where(c => c.Filter.Apply(options))
            .Select(c => c.Provider)
            .ToList();
        _cache.Clear();
    }

    //ActiveProviders 暴露当前激活的 provider 列表供 F4/F6 阶段构建 glyphsByWidth 索引
    public IReadOnlyList<IGlyphProvider> ActiveProviders => _activeProviders;

    //GetGlyph 按 providers 顺序查找首个命中的 codepoint
    //对标原版 computeGlyphInfo 简化版去除 fishy advance 检测和 nonFishy 双重缓存
    public IUnbakedGlyph? GetGlyph(int codepoint)
    {
        return _cache.GetOrAdd(codepoint, ComputeGlyph);
    }

    private IUnbakedGlyph? ComputeGlyph(int codepoint)
    {
        foreach (var provider in _activeProviders)
        {
            var glyph = provider.GetGlyph(codepoint);
            if (glyph != null)
                return glyph;
        }
        return null;
    }

    public void Dispose()
    {
        foreach (var p in _allProviders)
            p.Provider.Dispose();
        _allProviders.Clear();
        _activeProviders.Clear();
        _cache.Clear();
    }
}
