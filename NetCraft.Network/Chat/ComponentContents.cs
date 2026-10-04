namespace NetCraft.Network.Chat;

using NetCraft.Codec;
using NetCraft.DataFixer.Util;

//组件内容接口对应原版net.minecraft.network.chat.ComponentContents
//承载Component的实际内容(纯文本/翻译/按键绑定/计分板/选择器/NBT/对象)
public interface ComponentContents
{
    //返回此内容类型的MapCodec用于序列化对应原版codec()
    MapCodec<ComponentContents> Codec();

    //带样式消费者遍历对应原版visit(StyledContentConsumer,Style)
    //默认返回空Optional表示此内容不产生文本
    Optional<T> Visit<T>(FormattedText.StyledContentConsumer<T> output, Style currentStyle)
        => Optional<T>.Empty();

    //无样式消费者遍历对应原版visit(ContentConsumer)
    //默认返回空Optional表示此内容不产生文本
    Optional<T> Visit<T>(FormattedText.ContentConsumer<T> output)
        => Optional<T>.Empty();
}
