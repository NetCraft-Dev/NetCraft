using System.Reflection;

namespace NetCraft.ModLoader;

//ModManager service injection: fills services registered by the host into mod instances
public sealed partial class ModManager
{
    //InjectServices: injects registered services into a mod instance by type
    //Two injection styles: public property setters and SetServices method parameters, both matched by type with no extra attributes needed
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
                OnError?.Invoke($"SetServices parameter {parameters[i].Name} of mod {type.Name} has no matching service",
                    new InvalidOperationException("service not registered"));
                return;
            }
            args[i] = service;
        }
        setter.Invoke(instance, args);
    }

    //ResolveService: gets a service by type, returns null if not registered
    private object? ResolveService(Type serviceType)
        => _services.TryGetValue(serviceType, out var service) ? service : null;
}
