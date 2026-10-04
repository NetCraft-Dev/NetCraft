namespace NetCraft.Storage;

//CowFS 系列异常 对应原版 net.minecraft.util.filefix.virtualfilesystem.exception 包
//原版各自继承 java.nio.file 的异常 这里按语义映射到对应的 BCL 基类

//CowFSFileSystemException 虚拟文件系统通用错误
public class CowFSFileSystemException : IOException
{
    public CowFSFileSystemException(string message) : base(message)
    {
    }
}

//CowFSCreationException 构建虚拟文件系统失败
public class CowFSCreationException : CowFSFileSystemException
{
    public CowFSCreationException(string message) : base(message)
    {
    }
}

//CowFSSymlinkException 构建时遇到符号链接
public class CowFSSymlinkException : CowFSCreationException
{
    public CowFSSymlinkException(string message) : base(message)
    {
    }
}

//CowFSNotDirectoryException 期望目录却拿到文件
public class CowFSNotDirectoryException : IOException
{
    public CowFSNotDirectoryException(string message) : base(message)
    {
    }
}

//CowFSNoSuchFileException 路径不存在
public class CowFSNoSuchFileException : IOException
{
    public CowFSNoSuchFileException(string message) : base(message)
    {
    }
}

//CowFSIllegalArgumentException 传入了别的文件系统的路径
public class CowFSIllegalArgumentException : ArgumentException
{
    public CowFSIllegalArgumentException(string message) : base(message)
    {
    }
}

//CowFSFileAlreadyExistsException 目标已存在
public class CowFSFileAlreadyExistsException : IOException
{
    public CowFSFileAlreadyExistsException(string message) : base(message)
    {
    }
}

//CowFSDirectoryNotEmptyException 目录非空不能删
public class CowFSDirectoryNotEmptyException : IOException
{
    public CowFSDirectoryNotEmptyException(string message) : base(message)
    {
    }
}
