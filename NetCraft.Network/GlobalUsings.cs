//Network global usings for the protocol layer to simplify protocol file references
//Needed after the connection-layer protocols (handshake/status/ping/login/common/cookie/configuration) were moved into this library
//Deliberately excludes NetCraft.Network.Chat, whose Component clashes with the NetCraft.Network.Component namespace name
//Under NetCraft.Network.* it would resolve to the namespace, so files that need it must use their own using or a fully qualified name
global using NetCraft.Config;
global using NetCraft.Network;
global using NetCraft.Network.Protocol;
global using NetCraft.Registry;
global using NetCraft.Network.Protocol.Common;
global using NetCraft.Network.Protocol.Cookie;
