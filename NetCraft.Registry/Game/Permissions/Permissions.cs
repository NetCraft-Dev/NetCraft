namespace NetCraft.Registry;

//Permissions 内置权限常量对应原版 net.minecraft.server.permissions.Permissions
//命令类常量走 HasCommandLevel 非命令的独立开关走 Atom
public static class Permissions
{
    public static readonly Permission CommandsModerator = new Permission.HasCommandLevel(PermissionLevel.Moderators);
    public static readonly Permission CommandsGamemaster = new Permission.HasCommandLevel(PermissionLevel.Gamemasters);
    public static readonly Permission CommandsAdmin = new Permission.HasCommandLevel(PermissionLevel.Admins);
    public static readonly Permission CommandsOwner = new Permission.HasCommandLevel(PermissionLevel.Owners);
    public static readonly Permission CommandsEntitySelectors = Permission.Atom.Create("commands/entity_selectors");
    public static readonly Permission ChatSendMessages = Permission.Atom.Create("chat/send_messages");
    public static readonly Permission ChatSendCommands = Permission.Atom.Create("chat/send_commands");
    public static readonly Permission ChatReceivePlayerMessages = Permission.Atom.Create("chat/receive_player_messages");
    public static readonly Permission ChatReceiveSystemMessages = Permission.Atom.Create("chat/receive_system_messages");

    //ChatPermissions 聊天相关权限集合对应原版 CHAT_PERMISSIONS
    public static readonly IReadOnlySet<Permission> ChatPermissions = new HashSet<Permission>
    {
        ChatSendMessages,
        ChatSendCommands,
        ChatReceivePlayerMessages,
        ChatReceiveSystemMessages,
    };
}
