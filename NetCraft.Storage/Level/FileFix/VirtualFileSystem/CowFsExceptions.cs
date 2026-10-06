namespace NetCraft.Storage;

//The CowFS exception family, maps to the vanilla net.minecraft.util.filefix.virtualfilesystem.exception package
//Vanilla each extends a java.nio.file exception; here they map by semantics to the corresponding BCL base classes

//CowFSFileSystemException, generic virtual filesystem error
public class CowFSFileSystemException : IOException
{
    public CowFSFileSystemException(string message) : base(message)
    {
    }
}

//CowFSCreationException, failed to build the virtual filesystem
public class CowFSCreationException : CowFSFileSystemException
{
    public CowFSCreationException(string message) : base(message)
    {
    }
}

//CowFSSymlinkException, hit a symlink during build
public class CowFSSymlinkException : CowFSCreationException
{
    public CowFSSymlinkException(string message) : base(message)
    {
    }
}

//CowFSNotDirectoryException, expected a directory but got a file
public class CowFSNotDirectoryException : IOException
{
    public CowFSNotDirectoryException(string message) : base(message)
    {
    }
}

//CowFSNoSuchFileException, path does not exist
public class CowFSNoSuchFileException : IOException
{
    public CowFSNoSuchFileException(string message) : base(message)
    {
    }
}

//CowFSIllegalArgumentException, a path from another filesystem was passed in
public class CowFSIllegalArgumentException : ArgumentException
{
    public CowFSIllegalArgumentException(string message) : base(message)
    {
    }
}

//CowFSFileAlreadyExistsException, target already exists
public class CowFSFileAlreadyExistsException : IOException
{
    public CowFSFileAlreadyExistsException(string message) : base(message)
    {
    }
}

//CowFSDirectoryNotEmptyException, directory not empty and cannot be deleted
public class CowFSDirectoryNotEmptyException : IOException
{
    public CowFSDirectoryNotEmptyException(string message) : base(message)
    {
    }
}
