namespace NetCraft.Commands;

//IImmutableStringReader read-only string reader interface, maps to vanilla ImmutableStringReader
//StringReader implements it so callers can inspect cursor state without modifying it
//GetRead is a method rather than a property to avoid clashing with StringReader.Read
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
