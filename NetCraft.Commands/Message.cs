namespace NetCraft.Commands;

//Message 消息接口对应原版com.mojang.brigadier.Message
//承载异常与提示的文本内容供CommandSyntaxException使用
public interface IMessage
{
    string GetString();
}
