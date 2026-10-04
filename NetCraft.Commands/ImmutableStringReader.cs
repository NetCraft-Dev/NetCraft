namespace NetCraft.Commands;

//IImmutableStringReader 只读字符串读取器接口对应原版ImmutableStringReader
//StringReader实现此接口供外部读取游标状态不修改
//GetRead用方法而非属性避免与StringReader.Read方法冲突
public interface IImmutableStringReader
{
    string String { get; }
    int RemainingLength { get; }
    int TotalLength { get; }
    int Cursor { get; }
    string GetRead();
    string Remaining { get; }
    bool CanRead(int length);
    bool CanRead();
    char Peek();
    char Peek(int offset);
}
