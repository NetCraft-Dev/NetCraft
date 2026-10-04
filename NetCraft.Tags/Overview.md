# NetCraft.Tags

> **完成度**：90% · **阶段**：框架完成 + 启动序列已接通 · **依赖**：NetCraft.Registry · **计划链位置**：Layer 1 标签框架，对应原版 tags/ (28 文件)

Layer 1 子库，数据标签系统。承载 tag 的加载、解析、绑定框架。

对应原版 `net.minecraft.tags` 包。`TagKey<T>` 类放在 `NetCraft.Registry` 子库（标识符语义层）。

## 实现

- `TagEntry` 标签条目，元素引用或标签引用带 required 标志，前缀编码 `#tag`/`!id`/`!#tag`/`id`
- `TagFile` 标签文件格式 JSON `{replace, entries}`，FromJson/ToJson
- `TagLoader<T>` 标签加载器，BuildSingle/BuildAll/LoadDirectory，支持 replace 标志合并和标签引用递归解析
- `TagManager` 全局标签管理器，按注册表 Identifier 索引 TagLoader，RegisterLoader/GetLoader/BindAll
- `ITagLoader` 非泛型标记接口，绕过 C# 泛型不变性（`TagLoader<Block>` 无法 cast 为 `TagLoader<object>`）

TagManager.BindAll 时遍历各注册表的 binder 闭包，调用 Registry.BindTags 把 TagKey→Holder 列表绑定到 NamedHolderSet，刷新 Reference 的 tag 缓存。

## 文件清单（4 个 cs）

| 文件 | 用途 |
|------|------|
| `TagEntry.cs` | 标签条目 record，元素/标签引用 + required + 前缀编码 |
| `TagFile.cs` | 标签文件格式 record，replace 标志 + entries 列表 + JSON 解析 |
| `TagLoader.cs` | 泛型标签加载器，BuildSingle/BuildAll/LoadDirectory |
| `TagManager.cs` | 全局标签管理器，ITagLoader 标记接口 + 注册表 Identifier 索引 |

## 接通状态

- Bootstrap.LoadBuiltinTags 已绑定 5 个核心注册表：BLOCK/ITEM/ENTITY_TYPE/FLUID/GAME_EVENT
- TagsReloadListener（NetCraft.Bootstrap）已接入 ReloadableServerResources（NetCraft.Game）启动序列
- ServerMain/ClientMain 步骤 4.7/5.5 调 ReloadableServerResources.LoadResources 触发 TagsReloadListener.Reload
- 完整链路：AssetsExtractor 提取 jar 的 data/ → FolderPackResources → ResourceManager → TagsReloadListener → LoadBuiltinTags → BindAll → Registry.BindTags
- 当前 BLOCK/ITEM 等注册表为空 stub，framework 完整但实际 Holder 绑定等具体注册表填充后生效
- 后续业务层实现具体 Block/Item/Entity 后 TagManager.BindAll 会真正生效（无需改 Tags 代码）

## 测试

- 5 个 TagsTests 覆盖 TagEntry 前缀编码/TagFile JSON 解析/TagLoader BuildAll，详见 NetCraft.Test/Overview
- 5 个 BootstrapTagsTests 覆盖 LoadBuiltinTags 端到端：无 tag 文件/加载 block tag/多 namespace 合并/跳过无效 JSON/replace 标志
- 5 个 ReloadableServerResourcesTests 间接覆盖 TagsReloadListener.Reload 链路（空 packs 不抛/Reload 重新触发）

## 规划

- 扩展 LoadBuiltinTags 覆盖更多注册表（POTION/ENCHANTMENT/SOUND_EVENT 等）按需追加
- 当前注册表填充真实 Block/Item 后 BindAll 真正生效（无需 framework 调整）
- /reload 命令接入调 ReloadableServerResources.Reload 重新扫描 tag 文件并重新 BindAll
