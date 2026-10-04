namespace NetCraft.Storage;

//被禁止的符号链接对应原版ForbiddenSymlinkInfo
//Link是链接本身，Target是它指向的地方
public sealed record ForbiddenSymlinkInfo(string Link, string Target);
