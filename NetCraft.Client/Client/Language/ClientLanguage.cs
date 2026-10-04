using NetCraft.Game;
using NetCraft.Resources;

namespace NetCraft.Game.Client.Language;

//ClientLanguage 客户端语言表对应原版 net.minecraft.client.resources.language.ClientLanguage
//作为资源重载监听器挂在重载链上 每次重载按当前语言码重新装配语言表并替换全局实例
//原版重载时同时刷新可选语言清单 这里一并读 pack.mcmeta 的 language 段
public sealed class ClientLanguage : PreparableReloadListener
{
    //LanguageCode 当前语言码对应原版 Minecraft.options.languageCode 切换语言改这个再触发重载
    public string LanguageCode { get; set; }

    //AvailableLanguages 资源包声明的可选语言 尚未重载过时为空表
    public IReadOnlyDictionary<string, LanguageInfo> AvailableLanguages => _available.Languages;

    private LanguageMetadataSection _available = LanguageMetadataSection.Empty;

    public ClientLanguage(string languageCode) => LanguageCode = languageCode;

    //Reload 按当前语言码装配并替换全局实例 语言码没有对应文件时表里只剩 en_us 兜底
    public void Reload(ResourceManager rm, ReloadContext ctx)
    {
        LanguageTable.Load(rm, LanguageCode);
        _available = LanguageMetadataSection.ReadAll(rm);
    }
}
