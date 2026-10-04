namespace NetCraft.Registry;

//RegistryIdentified 注册表元素能从注册名回填自身标识
//原版元素不持 id(id 只在注册表里) 本作为了网络同步与调试让元素自带
//代码注册的元素构造时就知道 id 数据驱动加载的元素只能等注册那一刻从文件名拿到
public interface RegistryIdentified
{
    //SetRegistryId 由注册表装载流程在写入注册表时回填
    void SetRegistryId(Identifier id);
}
