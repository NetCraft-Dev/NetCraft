namespace NetCraft.Network.Chat;

using NetCraft.Codec;
using NetCraft.DataFixer.Util;

//Component contents interface, maps to vanilla net.minecraft.network.chat.ComponentContents
//Carries the actual content of a Component (plain text/translation/keybind/scoreboard/selector/NBT/object)
public interface ComponentContents
{
    //Returns the MapCodec for this content type for serialization, maps to vanilla codec()
    MapCodec<ComponentContents> Codec();

    //Styled consumer traversal, maps to vanilla visit(StyledContentConsumer,Style)
    //Returns empty Optional by default, meaning this content produces no text
    Optional<T> Visit<T>(FormattedText.StyledContentConsumer<T> output, Style currentStyle)
        => Optional<T>.Empty();

    //Unstyled consumer traversal, maps to vanilla visit(ContentConsumer)
    //Returns empty Optional by default, meaning this content produces no text
    Optional<T> Visit<T>(FormattedText.ContentConsumer<T> output)
        => Optional<T>.Empty();
}
