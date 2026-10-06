namespace NetCraft.Commands;

//Message interface maps to vanilla com.mojang.brigadier.Message
//Carries the text of exceptions and hints for CommandSyntaxException
public interface IMessage
{
    string GetString();
}
