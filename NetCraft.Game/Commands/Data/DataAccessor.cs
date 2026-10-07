using NetCraft.Game.Commands.Arguments;
using NetCraft.Nbt;

namespace NetCraft.Game.Commands.Data;

//IDataAccessor data accessor, maps to vanilla net.minecraft.server.commands.data.DataAccessor
//Unifies the three target kinds block/entity/storage into "take a copy of NBT, modify, write back"; the reply text is assembled separately
//The vanilla reply is a Component; this project's command replies are plain text, so string is used
public interface IDataAccessor
{
    //SetData writes the whole NBT back to the target, maps to vanilla setData
    void SetData(CompoundTag tag);

    //GetData takes the target's full NBT, maps to vanilla getData
    CompoundTag GetData();

    //ModifiedSuccess the reply for a successful modification
    string ModifiedSuccess { get; }

    //PrintSuccess the reply for querying the whole data
    string PrintSuccess(Tag data);

    //PrintSuccess the reply for querying a single numeric value, with path and scale
    string PrintSuccess(NbtPath path, double scale, int value);
}
