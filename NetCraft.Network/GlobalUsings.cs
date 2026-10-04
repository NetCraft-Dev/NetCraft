//Network 协议层全局 using 简化协议文件引用
//连接层协议(handshake/status/ping/login/common/cookie/configuration)搬进本库后需要这套
//故意不含 NetCraft.Network.Chat 它的 Component 与 NetCraft.Network.Component 命名空间同名
//在 NetCraft.Network.* 下会被解析成命名空间 需要的文件自己 using 或用全限定名
global using NetCraft.Config;
global using NetCraft.Network;
global using NetCraft.Network.Protocol;
global using NetCraft.Registry;
global using NetCraft.Network.Protocol.Common;
global using NetCraft.Network.Protocol.Cookie;
