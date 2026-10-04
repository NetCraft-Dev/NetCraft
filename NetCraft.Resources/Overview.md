# NetCraft.Resources

> **完成度**：85% · **阶段**：基础完成 + 重载框架就绪 · **依赖**：NetCraft.Registry · **计划链位置**：Layer 4 资源系统，对应原版 resources/ (14 文件) + server/packs/ (49 文件)

Layer 4 子库，资源系统。承载资源包/数据包/资源加载。

对应原版 `net.minecraft.server.packs` 包。依赖 NetCraft.Registry（Identifier）。

## 实现

- `Pack` 资源包元数据对应原版 net.minecraft.server.packs.Pack
- `PackResources` 资源包内容访问抽象基类对应原版 PackResources 提供 namespace:path 到资源流访问
- 三种具体实现 `FolderPackResources`/`VanillaPackResources`/`ZipPackResources` 对应文件夹/原版内置/zip 文件来源
- `ResourceManager` 资源管理器按优先级合并多个 PackResources 提供统一访问 API
- `Resource` 单个资源包装资源标识与流式访问器含 sourcePackId 标记来源
- PackType 区分 ClientResources（assets/）与 ServerData（data/）
- `PreparableReloadListener` 资源重载监听器接口对应原版同名接口同步签名适配启动期阻塞模型
- `SimpleReloadInstance` 同步重载调度器按注册顺序遍历 listener 调 Reload
- `ReloadContext` 重载上下文携带 listener 名称与序号供进度报告
- `PackMetadataSection` pack.mcmeta 元数据段解析对应原版 PackMetadataSection 从根 JSON 解析 pack_format/description
- `ResourceManager.Reloaded` 事件 + `Reload()` 方法供 AddPack/RemovePack 后显式触发 Tags 等数据驱动重载

## 文件清单（10 个 cs）

| 文件 | 用途 |
|------|------|
| `Pack.cs` | 资源包元数据 id/标题/描述/优先级 |
| `PackResources.cs` | 资源包内容访问抽象基类 IDisposable + PackType 枚举 |
| `Resource.cs` | 单个资源包装 Identifier + 流工厂 + 来源标记 |
| `ResourceManager.cs` | 资源管理器按优先级合并多包 + Reloaded 事件 + Reload 方法 |
| `FolderPackResources.cs` | 文件夹型资源包对应 FilePackResources |
| `VanillaPackResources.cs` | 原版内置资源包委托 FolderPackResources PackId=vanilla |
| `ZipPackResources.cs` | zip 压缩资源包对应 ZipPackResources |
| `PreparableReloadListener.cs` | 资源重载监听器接口 + ReloadContext |
| `SimpleReloadInstance.cs` | 同步重载调度器按顺序执行 listener |
| `PackMetadataSection.cs` | pack.mcmeta 元数据段解析 + Reader |

## 接通状态

- `ReloadableServerResources`（NetCraft.Game）持 ResourceManager + TagManager 已通过 TagsReloadListener 接入 ServerMain/ClientMain 启动序列
- ServerMain/ClientMain 步骤 4.7/5.5 构造 ResourceManager 加 vanilla pack 触发首次 LoadResources
- Tags 端到端链路已通：AssetsExtractor 提取 jar 的 data/ → FolderPackResources → ResourceManager → TagsReloadListener → TagManager.BindAll → Registry.BindTags

## 测试

- 4 个 ResourcesTests 测试覆盖 ResourceManager 增删/优先级/FolderPackResources 文件读/VanillaPackResources 资源读，详见 NetCraft.Test/Overview
- 7 个 PackMetadataSectionTests 测试覆盖 FromJson 标准/缺 description/缺 pack 节点/description 为对象/缺 pack_format/Reader 返回 null/Reader 从 StubPack 读取
- 5 个 ReloadableServerResourcesTests 测试覆盖空 packs 不抛/TagsReloadListener 注册/Reload 重新触发/listeners 顺序/ReloadContext 序号

## 规划

- RecipeManager/AdvancementListener/FunctionListener 等业务监听器追加到 ReloadableServerResources._listeners
- 异步 CompletableFuture 调度（当前同步启动期阻塞模型）
- Pack 选择器 UI 与外部 datapacks/ 目录扫描 *.zip 加载
