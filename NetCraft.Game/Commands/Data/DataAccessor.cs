using NetCraft.Game.Commands.Arguments;
using NetCraft.Nbt;

namespace NetCraft.Game.Commands.Data;

//IDataAccessor 数据访问器对应原版 net.minecraft.server.commands.data.DataAccessor
//把 block/entity/storage 三类目标统一成"取出一份 NBT 改完写回" 回执文本各自拼
//原版回执是 Component 本作命令回执走纯文本 故用 string
public interface IDataAccessor
{
    //SetData 把整份 NBT 写回目标 对应原版 setData
    void SetData(CompoundTag tag);

    //GetData 取出目标的完整 NBT 对应原版 getData
    CompoundTag GetData();

    //ModifiedSuccess 修改成功的回执
    string ModifiedSuccess { get; }

    //PrintSuccess 查询整份数据的回执
    string PrintSuccess(Tag data);

    //PrintSuccess 查询单个数值的回执 带路径与倍率
    string PrintSuccess(NbtPath path, double scale, int value);
}
