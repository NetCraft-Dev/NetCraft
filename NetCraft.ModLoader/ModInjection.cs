using System.Reflection;

namespace NetCraft.ModLoader;

//ModManager 服务注入 把宿主登记的服务填进模组实例
public sealed partial class ModManager
{
    //InjectServices 按类型把已登记的服务注入模组实例
    //两种接法 public 属性 setter 与 SetServices 方法参数 都按类型匹配不需要额外特性
    private void InjectServices(object? instance)
    {
        if (instance is null)
            return;

        var type = instance.GetType();

        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!property.CanWrite || property.GetIndexParameters().Length > 0)
                continue;
            var service = ResolveService(property.PropertyType);
            if (service is not null)
                property.SetValue(instance, service);
        }

        var setter = type.GetMethod("SetServices", BindingFlags.Public | BindingFlags.Instance);
        if (setter is null)
            return;

        var parameters = setter.GetParameters();
        var args = new object?[parameters.Length];
        for (int i = 0; i < parameters.Length; i++)
        {
            var service = ResolveService(parameters[i].ParameterType);
            if (service is null)
            {
                OnError?.Invoke($"模组 {type.Name} 的 SetServices 参数 {parameters[i].Name} 没有对应服务",
                    new InvalidOperationException("服务未登记"));
                return;
            }
            args[i] = service;
        }
        setter.Invoke(instance, args);
    }

    //ResolveService 按类型取服务 未登记时返回 null
    private object? ResolveService(Type serviceType)
        => _services.TryGetValue(serviceType, out var service) ? service : null;
}
