namespace NetCraft.Registry;

//Permissions built-in permission constants, maps to vanilla net.minecraft.server.permissions.Permissions
//Command constants use HasCommandLevel and standalone non-command switches use Atom
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

    //ChatPermissions chat-related permission set, maps to vanilla CHAT_PERMISSIONS
    public static readonly IReadOnlySet<Permission> ChatPermissions = new HashSet<Permission>
    {
        ChatSendMessages,
        ChatSendCommands,
        ChatReceivePlayerMessages,
        ChatReceiveSystemMessages,
    };
}
