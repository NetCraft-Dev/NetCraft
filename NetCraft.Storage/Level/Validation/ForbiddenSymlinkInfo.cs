namespace NetCraft.Storage;

//Forbidden symlink info, maps to vanilla ForbiddenSymlinkInfo
//Link is the symlink itself, Target is what it points to
public sealed record ForbiddenSymlinkInfo(string Link, string Target);
