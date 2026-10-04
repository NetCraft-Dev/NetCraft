namespace NetCraft.DataFixer;

using System;
using System.Collections.Generic;
using NetCraft.DataFixer.Fixes;
using NetCraft.DataFixer.Schemas;

//MC数据修复器入口对应原版net.minecraft.util.datafix.DataFixers
//DFU内核层只提供框架不主动注册任何Schema或Fix
//具体Schema与Fix注册由业务层（NetCraft.Game）自行调用DataFixerBuilder.AddSchema/AddFixer完成
//参考原版buildFixer在Game层注册1000+个Schema与Fix
public static class DataFixers
{
    //DFU不维护全局DataFixer实例
    //业务层自行构造DataFixerBuilder后调用Build().Fixer()获取DataFixer实例
    //示例用法：
    //var builder = new DataFixerBuilder(dataVersion);
    //builder.AddSchema(version, subVersion, (key, parent) => new SomeSchema(key, parent));
    //builder.AddFixer(new SomeFix(outputSchema));
    //var fixer = builder.Build().Fixer();
}
