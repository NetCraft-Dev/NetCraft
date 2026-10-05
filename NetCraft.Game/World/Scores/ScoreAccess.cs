using NetCraft.Network.Chat;

namespace NetCraft.Game.World.Scores;

//ScoreAccess 可写分数视图 对应原版 net.minecraft.world.scores.ScoreAccess
//命令侧拿到它就能读写分数 只读 objective 上拿到的实现会挡住写操作
public interface ScoreAccess
{
    //Get 读当前分数 对应原版 get
    int Get();

    //Set 写分数 对应原版 set
    void Set(int value);

    //Add 加一个增量并返回新值 对应原版 add
    int Add(int amount)
    {
        var value = Get() + amount;
        Set(value);
        return value;
    }

    //Increment 自增一并返回新值 对应原版 increment
    int Increment() => Add(1);

    //Reset 分数归零 对应原版 reset
    void Reset() => Set(0);

    //Locked 分数是否锁定 对应原版 locked
    bool Locked();

    //Unlock 解锁分数 对应原版 unlock
    void Unlock();

    //Lock 锁定分数 对应原版 lock
    void Lock();

    //Display 读自定义显示文本 没有给 null 对应原版 display
    Component? Display();

    //Display 写自定义显示文本 对应原版 display
    void Display(Component? display);
}
