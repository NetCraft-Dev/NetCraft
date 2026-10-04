namespace NetCraft.Game.DFU.Schemas;

using NetCraft.DataFixer;

using NetCraft.DataFixer.Schemas;

using System;
using System.Collections.Generic;
using NetCraft.DataFixer.Fixes;
using NetCraft.DataFixer.Types.Templates;
using NetCraft.DataFixer.Util;

//V4312对应原版net.minecraft.util.datafix.schemas.V4312
//1.21.4注册PLAYER类型原版组合7字段AND模板
//端到端测试用Remainder简化PLAYER避免And(Product TypeTemplate Apply后产生SumType导致Type<object>强转失败
public class V4312 : NamespacedSchema
{
    public V4312(int versionKey, Schema? parent) : base(versionKey, parent) { }

    public override void RegisterTypes(Schema schema, Dictionary<string, Func<TypeTemplate>> entityTypes, Dictionary<string, Func<TypeTemplate>> blockEntityTypes)
    {
        base.RegisterTypes(schema, entityTypes, blockEntityTypes);
        //PLAYER用Remainder透传Update流程不参与字段级修复
        //原版AND+OptionalFields组合在TypeTemplate.Apply后产生SumType强转Type<object>失败
        //后续业务层需要PLAYER字段级修复时改回原版AND结构
        schema.RegisterType(false, References.Player, () => DSL.Remainder());
    }
}
