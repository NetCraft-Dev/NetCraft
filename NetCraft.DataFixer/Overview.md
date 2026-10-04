# NetCraft.DataFixer

> **完成度**：95% · **阶段**：阶段 A-E 全部完成 · **依赖**：NetCraft.Codec + NetCraft.Util · **计划链位置**：Layer 1 存档升级框架，对应原版 com.mojang.datafixers

Layer 1 子库，Mojang DFU（com.mojang.datafixers）完整框架的 C# 移植。

阶段 A-D 全部完成：kinds/util/Products + optics 子包（HKT 模拟与 Profunctor 光学体系）、types/functions/schemas 子包、datafixers 根入口类、MC 集成层（V1Foundation/NamespacedSchema + V1_21 段 Schema/Fix 留作参考，业务注册由 Game 层负责）。

DFU 内核零业务依赖，只提供框架。具体 Schema 与 Fix 注册由 NetCraft.Game.DFU.GameDataFixers 调用 DataFixerBuilder 完成，详见 [NetCraft.Game Overview](../NetCraft.Game/Overview.md)。

## HKT 模拟方案

C# 泛型系统不支持高阶类型（HKT），用三层结构模拟：

- 空标记接口 K1/K2 表示类型构造器（F<A> 的 F）
- 类型应用接口 App<F,A> 模拟 F<A>
- 类型构造器接口 Kind1/Kind2 提供类型类约束与 Unbox 还原

引入非泛型标记接口（TypeClasses.cs）解决泛型嵌套 Mu 标记外部访问需带类型参数的问题，IKind1Mu 继承 K1 形成继承链 IApplicativeMu → IFunctorMu → IKind1Mu → K1 满足 App<F,A> 的 F:K1 约束。

二元类型类标记 IKind2Mu 继承 K1 对齐原版 Kind2.Mu extends K1，使二元类型类标记可作为 Optic<Proof:K1> 的 Proof。

## Profunctor 光学体系

Profunctor 是二元类型类，提供 Dimap（双映射）/Lmap（左映射）/Rmap（右映射），作为光学组合子的基础抽象。基于 Profunctor 约束强度递增：

- Profunctor：Adapter（from/to 直接转换）
- Cartesian：Lens（view/update），通过 First/Second 扩展到 Pair
- Cocartesian：Prism（match/build），通过 Left/Right 扩展到 Either
- AffineP = Cartesian + Cocartesian：Affine（preview/set）
- TraversalP = AffineP + Wander：Traversal（遍历容器）
- Closed：Grate（函数空间嵌套）
- GetterP = Profunctor + Bicontravariant：Getter（只读 view）
- ReCartesian/ReCocartesian：Forget 系列（遗忘光学，用于读取/写入求值）

## 实现

### Kinds 子包（18 个 cs）

- K1/K2 一元/二元高阶类型标记
- App/App2 类型应用接口
- Kind1/Kind2 类型构造器接口提供 Unbox 还原与 group 乘积组合
- TypeClasses 非泛型标记接口链 IKind1Mu→K1 / IKind2Mu→K1 等
- Functor/Applicative/Traversable/Monoid 类型类
- CartesianLike/CocartesianLike 笛卡尔/余笛卡尔积
- IdF/Const/OptionalBox/ListBox 盒实例
- Representable Representable 类型类（Reader 函子抽象）

### Util 子包（5 个 cs）

- Unit 单元类型
- Either 二元类型 + Eithers.Mu 标记
- Pair 二元组 + PairInstance
- Functions3..16 多参数函数委托
- OptionalExtensions 扩展 Codec.Optional

### Optics 子包（28 个 cs）

- Optic 光学核心接口与 CompositionOptic 链式组合
- Adapter/Lens/Prism/Affine/Traversal/Getter/Grate 7 种核心光学
- IdAdapter/Proj1/Proj2/Inj1/Inj2/InjTagged 6 种单例光学
- Forget/ForgetE/ForgetOpt/ReForget/ReForgetE/ReForgetEP/ReForgetP/ReForgetc 8 种遗忘光学
- ListTraversal 列表遍历
- PStore 位置存储
- Procompose 光学组合容器
- Optics 静态工厂类含所有光学工厂方法与 ToAdapter/ToLens/ToPrism 等转换方法
- Wander 策略接口
- FuncInvokerHelper 表达式树缓存绕过 C# 泛型不变性

### Optics/Profunctors 子包（15 个 cs）

- Profunctor 二元类型类接口
- Cartesian/Cocartesian/AffineP/Closed/GetterP/TraversalP Profunctor 约束接口
- Monoidal/MonoidProfunctor/Mapping/Bicontravariant/FunctorProfunctor 高级约束
- ReCartesian/ReCocartesian 逆向 Profunctor
- ProfunctorFunctorWrapper Profunctor 与 Functor 桥接

### Types 子包（20 个 cs）

类型系统是阶段 A 完成后接通端到端示例的关键。

- `Types/Type.cs` 类型抽象基类含 Rewrite/OptionalTemplate
- `Types/Func.cs` FuncType 函数类型
- `Types/Templates/`（12 个）类型模板：TypeTemplate 接口基类 + Hook/Check/Const/List/Named/Product/RecursivePoint/Sum/Tag/TaggedChoice/CompoundList 等模板
- `Types/Families/`（4 个）递归类型族：TypeFamily 抽象基类 + RecursiveTypeFamily 递归实现 + Algebra/ListAlgebra 代数接口
- `Types/Constant/`（2 个）EmptyPart 空部件 + EmptyPartPassthrough 透传 codec（阶段 12.1 修复 ops 类型转换）

### Functions 子包（13 个 cs）

PointFree 函数式规则系统。

- Apply/Bang/Comp/Fold/Id/In/Out/View 函数规则
- PointFree 抽象基类 + PointFreeRule 5 条规则（对齐 Java PointFreeRule）
- FunctionWrapper 包装类型
- Functions 静态工厂
- ProfunctorTransformer Profunctor 转换器

### Schemas 子包（16 个 cs）

Schema 模式框架 + V1_21 段 Schema 留作参考（业务注册由 Game 层负责）。

- `Schema.cs`/`NamespacedSchema.cs`/`ExampleSchema.cs` Schema 框架基类与示例
- 13 个 V1_21 段 Schema（V1Foundation/V2505/V3448/V3685/V3818_3/V3825/V3938/V4059/V4067/V4300/V4306/V4307/V4312），DFU 层保留作参考，Game 层复制一份相同代码 namespace 改 NetCraft.Game.DFU.Schemas

### Fixes 子包（28 个 cs，DFU 层保留作参考）

- `FixConstants.cs` 集中游戏业务字面常量
- `References.cs` 实体引用常量
- `AddNewChoices.cs`/`ExampleCounterIncrementFix.cs` 框架示例 Fix
- 4 个抽象父类：NamedEntityFix/NamedEntityWriteReadFix/AttributesRenameFix(抽象)/DataComponentRemainderFix(抽象)
- 21 个 V1_21 Fix 类（DFU 层保留作参考，Game 层复制一份相同代码 namespace 改 NetCraft.Game.DFU.Fixes）

### 根目录（23 个 cs，datafixers 入口类）

| 文件 | 用途 |
|------|------|
| `DataFix.cs` | 数据修复基类 |
| `DataFixer.cs` | DataFixer 主类 |
| `DataFixerBuilder.cs` | DataFixer 构建器链式 AddSchema/AddFixer |
| `DataFixerUpper.cs` | DataFixerUpper 实现 |
| `DataFixers.cs` | 简化为空壳（业务层自行构造） |
| `DSL.cs` | DSL 静态工厂含 OptionalFields/AllWithRemainder/And 等 |
| `DataFixTypes.cs` | 类型常量 |
| `DataFixUtils.cs`/`ExtraDataFixUtils.cs` | 工具方法（WriteAndReadTypedOrThrow/FixBlockPos/ChainAllFilters 等） |
| `FamilyOptic.cs` | Family 光学 |
| `FieldFinder.cs`/`OpticFinder.cs`/`NamedChoiceFinder.cs` | 查找器 |
| `Typed.cs` | Typed<T> 类型化值含 Read/ReadTyped/Pair 辅助 |
| `TypedOptic.cs`/`TypedOptics.cs` | TypedOptic 类型化光学与静态工厂 |
| `TypeObjectWrapper.cs` | 类型对象包装器 SG 生成的通用包装类解 Type<A> 到 Type<object> 转换 |
| `TypeRewriteRule.cs` | 类型重写规则 |
| `RewriteResult.cs` | 重写结果 |
| `FunctionType.cs` | 函数类型 HKT 包装实现 TraversalP/Monoidal/Mapping/MonoidProfunctor |
| `Products.cs` | P1..P16 乘积类型 record 与 Of 工厂 |
| `NoOpDataFixer.cs` | NoOp DataFixer |
| `WanderInvokerCache.cs` | Wander 调用缓存（表达式树编译委托） |

## 文件清单（166 个 cs）

| 子包 | 数量 | 说明 |
|------|------|------|
| 根目录 | 23 | datafixers 入口类（DataFixer/DataFixerBuilder/DSL/Typed/Types 等） |
| Kinds | 18 | HKT 模拟（K1/K2/App/Kind1/TypeClasses/Functor 等） |
| Util | 5 | Unit/Either/Pair/Functions/OptionalExtensions |
| Optics | 28 | 7 种核心光学 + 6 种单例 + 8 种遗忘 + 工厂 |
| Optics/Profunctors | 15 | Profunctor 类型类约束体系 |
| Types | 2 | Type/Func 根 |
| Types/Constant | 2 | EmptyPart 透传 codec |
| Types/Families | 4 | RecursiveTypeFamily 递归类型族 |
| Types/Templates | 12 | TypeTemplate 模板体系 |
| Functions | 13 | PointFree 规则系统 |
| Schemas | 16 | Schema 框架 + 13 个 V1_21 Schema 留作参考 |
| Fixes | 28 | 4 个抽象父类 + 21 个 V1_21 Fix + 3 个框架类留作参考 |

## 设计决策

- 静态工厂方法移至非泛型类（IdFs/Consts/Eithers/Pairs/OptionalBoxes/ListBoxes）解决泛型类静态方法类型推断问题
- Instance 类独立放置避免泛型类型参数上下文污染
- Applicative.Ap2..Ap16 用中间变量拆分嵌套调用解决类型推断失败
- ListBox.Flip 通过静态 Traverse 实现避免接口默认方法调用歧义
- Products record 参数名小写（t1/t2）避免与类型参数同名冲突
- Pair 不复用 Codec.Pair 因 DataFixer.Util.Pair 是 HKT 版本继承 App<Pairs.Mu<S>,F>
- OptionalBox 复用 Codec.Optional 避免重复实现
- Profunctor.Mu 用非泛型标记接口 IProfunctorMu 替代避免泛型类型参数访问问题
- Dimap/Lmap/Rmap 类型参数显式指定避免 C# lambda 推断失败
- Either 实例方法 GetLeft/GetRight 与静态工厂 Left/Right 分离避免重名冲突
- TraversalInstance.TMu 直接用 ITraversalPMu 使实例可作 App<ITraversalPMu,Traversals.Mu<A,B>> 传入 Optic.Eval
- Optics 静态工厂 Proj1/Proj2/Inj1/Inj2 用 global::全限定访问 Instance 字段避免方法组与类名冲突 CS0119
- GrateInstance.Dimap/Closed 原版依赖 Java 类型擦除实现函数空间嵌套 C# 无法精确翻译占位抛 NotSupportedException 阶段 B 接通时实现
- GetterP.SecondPhantom 默认方法体 `_ => _` 改 `x => x` 避免 C# discard 解析歧义导致方法签名解析失败
- FunctionType.GetFunc 用 Unsafe.As 绕过 Pair<String,Object> 与 Pair<Object,Object> 之间运行时类型检查（对齐 Java 类型擦除）
- TypeObjectWrapper.CodecAdapter 用 CastTo<T> helper + 表达式树编译委托避开 CheckValue 的泛型不变量校验
- DFU 内部 Pair 统一为 Util.Pair 对齐原版 Java 单一 Pair 设计（阶段 12.2 修复 v121fix 端到端崩溃根因）
- TaggedChoiceCodec.EncodeStart 用 MergeToMap(valueMap, nameField, keyValue) 对齐原版 partialDispatch 语义把 key 字段合并到 value map

## 验证

- 0 编译错误
- DFU 模块 47 个测试 + 端到端测试全过：counter 41→42 验证 +1 修复规则正确应用
- v121fix 6 个测试全过（BuildFixer construction / GetSchema / GetTypeRaw / Update no-op / MemoryExpiryDataFix wraps memory value 端到端）

## 阶段进度

- 阶段 A：完成 Kinds/Util/Products + Optics + Profunctors
- 阶段 A：完成 Types 子包（Type/Func/Templates/Families/Constant）
- 阶段 A：完成 Functions 子包（PointFree 规则系统）
- 阶段 A：完成 Schemas 子包（Schema/NamespacedSchema/ExampleSchema + V1_21 段 13 个）
- 阶段 B：完成 datafixers 根入口类（DataFixer/DataFixerBuilder/DataFixerUpper/DSL/Typed/TypeObjectWrapper 等）
- 阶段 B：完成 TypeObjectConverter SG 真扫描子类生成 TypeObjectWrapper 通用包装类，端到端示例打通（counter 41→42）
- 阶段 C：完成 MC 集成层（V1Foundation 注册 ENTITY/BLOCK_ENTITY 等递归类型 + NamespacedSchema）+ 21 个 V1_21 Fix 类移植
- 阶段 D：v121fix 端到端测试全绿（阶段 12.2 修复 Pair 类型统一崩溃根因）
- 阶段 E：DFU 架构调整——业务层 Game 接手 Fix 注册，DFU 层 V1_21 段代码保留作参考

## 规划

- 简单存档升级路径：等 Game 层 ChunkStorage/LevelStorage 业务调度接入后由 Game 层调用 GameDataFixers.BuildV1_21Fixer 实现 SimpleRegionStorage.UpgradeChunkTag 真实路径
- 复杂 Grate 光学的函数空间嵌套在阶段 B 已用占位 NotSupportedException 接通，若未来需要可基于 Unsafe.As 实现
