using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Context;
using NetCraft.Registry;
using StringReader = NetCraft.Commands.StringReader;

namespace NetCraft.Game.Commands.Arguments;

//ResourceKeyArgument 注册表键参数 对应原版 net.minecraft.commands.arguments.ResourceKeyArgument
//与 ResourceArgument 的关键区别: 网络描述符只写注册表标识 客户端实例化时按标识符拼 key 不去查注册表
//所以能引用客户端 play 阶段没有的注册表(如 recipe 是数据驱动注册表 不在同步列表里)
//ResourceArgument 相反 客户端实例化会 lookupOrThrow 该注册表 注册表不存在直接崩客户端
public sealed class ResourceKeyArgument : ArgumentType<Identifier>
{
    private static readonly IReadOnlyList<string> ExamplesList = new[] { "foo", "foo:bar", "012" };

    //RegistryKey 目标注册表标识 只用于网络传输与补全 服务端解析本身不校验
    public Identifier RegistryKey { get; }

    public ResourceKeyArgument(Identifier registryKey)
    {
        RegistryKey = registryKey;
    }

    public Identifier Parse(StringReader reader) => IdentifierArgument.ReadIdentifier(reader);

    //GetResource 取解析出的资源标识
    public static Identifier GetResource(CommandContext<CommandSourceStack> context, string name)
        => context.GetArgument<Identifier>(name);

    public IReadOnlyList<string> Examples => ExamplesList;
}
