# NetCraft.Network

> **完成度**：80% · **阶段**：M4 端到端握手测试通过（真实 TCP）· **依赖**：NetCraft.Nbt + NetCraft.Registry · **计划链位置**：Layer 3 网络框架，对应原版 network/ (392 文件) 框架部分

Layer 3 子库，网络底层框架。提供协议帧编解码、数据包抽象、连接管道、数据组件实现层等通用机制。

对应原版 `net.minecraft.network` + `net.minecraft.network.codec` + `net.minecraft.network.protocol` 框架层。

## 架构边界（重要）

**NetCraft.Network 只保留通用框架，业务数据包（具体 Packet 类）由 NetCraft.Game 模块注册。**

具体业务包（Handshake/Status/Ping/Login/Common/Cookie/Configuration/Play 包）原移植代码已迁移到 `NetCraft.Game/Network/Protocol/` 子目录，参考 [NetCraft.Game Overview](../NetCraft.Game/Overview.md)。

业务包注册到 PacketTypeRegistry 时按 `ConnectionProtocol` 索引，Game 层负责声明所有具体协议常量和监听器接口。

DataComponent 子系统的接口层位于 Registry（DataComponentType/DataComponentMap/TypedDataComponent），实现层位于 Network 的 Component 子目录。Registry 不依赖 Network，故接口不含 StreamCodec；Network 持有 StreamCodec 实现。

## 实现

### 网络框架核心

- `StreamCodec<B, V>` 流式编解码器接口对应原版 net.minecraft.network.codec.StreamCodec
- `FriendlyByteBuf` 协议缓冲对应原版 ByteBuf，提供 VarInt/VarLong/String/Identifier/Uuid/Nullable/ByteArray 等读写。`sealed class` 改 `class` 允许 RegistryFriendlyByteBuf 继承
- `RegistryFriendlyByteBuf` 继承 FriendlyByteBuf 持有 RegistryAccess，供 StreamCodec 按注册表 id 编解码
- `ByteBufCodecs` Holder 编解码从注册表 id 转 Holder.Reference + Collection 集合编解码 VarInt 长度前缀
- `ItemCodecs` Item.STREAM_CODEC 用 ByteBufCodecs.Holder(Registries.ITEM)
- `Packet<THandler>` 数据包接口对应原版 Packet
- `PacketType<THandler>` 包类型标识含 int Id + Identifier + FlowDirection
- `PacketTypeRegistry` 全局注册表按 (Protocol, Direction, Id) 索引包类型
- `PacketListener` / `ServerboundPacketListener` / `ClientboundPacketListener` 监听器接口
- `ConnectionProtocol` 协议枚举 Handshake/Play/Status/Login/Configuration

### 连接管道

- `Connection` 协议连接对应原版 net.minecraft.network.Connection
  - 用双向 Stream 替代 netty Channel
  - 持有 inbound/outbound 协议状态
  - 提供 Send/Receive/Tick/Disconnect 完整生命周期
  - 加密（CryptoHelper）和压缩（CompressionHelper）通过阈值开关控制
  - 业务包相关方法（InitiateServerboundStatus/LoginConnection 等）已移至 Game 层扩展方法
- `PacketProcessor` 包处理器，主线程调度和执行 packet.handle(listener)
- `INonGenericProtocol` 非泛型协议接口，避免 Connection 直接持有泛型 ProtocolInfo

### Protocol 框架层

- `ProtocolInfo<THandler>` 协议信息接口
- `ProtocolInfoBuilder` 协议信息构建器，链式 addPacket 注册
- `ProtocolCodecBuilder` 协议编解码器构建器
- `UnboundProtocol<S, B>` 未绑定上下文的协议接口
- `UnitStreamCodec<B, V>` 恒定值编解码器（无 payload 包用）
- `BundlePacket` / `BundleDelimiterPacket` / `BundlerInfo` 包打包机制
- `CodecModifier` 编解码器修饰器
- `PacketFlow` / `FlowDirection` 方向枚举
- `PacketUtils` 包工具类

### Component 子目录（DataComponent 实现层）

由于 Registry 不依赖 Network，DataComponent 的 StreamCodec 实现集中在 Network 的 Component 子目录。

- `IDataComponentTypeCodec` 非泛型编解码接口绕过 C# 泛型不变性，DataComponentPatch.STREAM_CODEC 跨泛型编解码组件值时 cast type 为此接口调 EncodeValue/DecodeValue
- `SimpleDataComponentType<T>` DataComponentType<T> 实现持有 Codec + StreamCodec，实现 IDataComponentTypeCodec
- `DataComponentTypeBuilder` Builder persistent/networkSynchronized/cacheEncoding/ignoreSwapAnimation build
- `SimpleDataComponentMap` DataComponentMap 实现 Dictionary<object,object> 存储 + Builder
- `DataComponentPatch` 补丁类 + EMPTY + StreamCodec positiveCount+negativeCount 编码 + DataComponentTypeCodecs 工具
- `PatchedDataComponentMap` 应用 patch 的可读 map，prototype+patch

### Chat 子目录（23 个 cs，文本组件系统）

阶段 12.1 ChatType 跨层迁移：原 Chat 系统在 Game 层但 CHAT_TYPE 注册表在 Registry 层用 stub 接口，C# 泛型不变性导致 ByteBufCodecs.Holder/ResourceKey.Create 类型不兼容。Registry 层无法引用 DataFixer/Commands（循环依赖），故 ChatType 系统整体迁移到 Network.Chat。

根目录（16 个 cs）：
- `Component.cs` 文本组件接口含 Style/Siblings/Contents
- `MutableComponent.cs` 可变组件实现 11 个 WithXxx 方法委托 Style
- `ComponentContents.cs` 内容接口
- `CommonComponents.cs` 公共常量 EMPTY/NEWLINE/SPACE
- `FormattedText.cs` 格式化文本接口
- `Style.cs` 样式容器
- `TextColor.cs` 文本颜色含 == 运算符按值比较
- `ChatFormatting.cs` 格式化代码枚举
- `ClickEvent.cs` 点击事件
- `HoverEvent.cs` 悬停事件
- `FontDescription.cs` 字体描述
- `ComponentSerialization.cs` 基于 System.Text.Json 的序列化器 ToJson/FromJson + StreamCodec<FriendlyByteBuf, Component>
- `PlaceholderMapCodec.cs` 占位 MapCodec
- `ChatType.cs` 聊天类型类含 Chat/Narration 装饰字段 + 7 个预定义 ResourceKey<object> + Bound 嵌套类 + StreamCodec 手写 ChatTypeHolderCodec（CHAT_TYPE 为 Registry<object> 弱类型需 cast）
- `ChatTypeDecoration.cs` 聊天装饰参数扩展类 ParameterExtensions 移到顶级静态类
- `MessageSignature.cs` 消息签名 Size 常量 + FullSignatureId 常量 + Read/Write/Packed.Read/Write 编解码

Contents 子目录（7 个 cs）：
- `PlainTextContents.cs` 纯文本含 Equals 值比较
- `TranslatableContents.cs` 翻译内容
- `KeybindContents.cs` 按键绑定
- `ScoreContents.cs` 分数
- `SelectorContents.cs` 选择器
- `NbtContents.cs` NBT 含 DataSource 第 5 参数
- `ObjectContents.cs` 对象

### Inventory 子目录（1 个 cs，菜单类型）

- `MenuType.cs` 菜单类型类对应原版 net.minecraft.world.inventory.MenuType，MENU 为 Registry<object> 弱类型，StreamCodec 手写 MenuTypeRegistryCodec 按 VarInt id 编解码，简化不含 MenuSupplier/FeatureFlagSet

### InternalsVisibleTo

- `NetCraft.Game` 和 `NetCraft.Test` 可访问 Network 的 internal 成员
- 主要用于 Connection 的 `SetInitialInboundProtocolInternal` / `SetDisconnectListenerInternal` 方法
- 业务扩展方法在 Game 层调用这些 internal 方法

## 文件清单（56 个 cs）

| 区域 | 数量 | 说明 |
|------|------|------|
| 根目录 | 14 | Connection/FriendlyByteBuf/RegistryFriendlyByteBuf/ByteBufCodecs/ItemCodecs/Packet/PacketListener/PacketProcessor/PacketType/PacketTypeRegistry/StreamCodec/ConnectionProtocol/CompressionHelper/CryptoHelper |
| Protocol 子目录 | 12 | ProtocolInfo/Builder/CodecBuilder/UnboundProtocol/INonGenericProtocol/UnitStreamCodec/BundlePacket/BundleDelimiterPacket/BundlerInfo/CodecModifier/PacketFlow/PacketUtils |
| Component 子目录 | 6 | DataComponent 实现层 IDataComponentTypeCodec/SimpleDataComponentType/DataComponentTypeBuilder/SimpleDataComponentMap/DataComponentPatch/PatchedDataComponentMap |
| Chat 子目录 | 16 | 文本组件根目录 Component/MutableComponent/Style/TextColor/ComponentSerialization/ChatType 等 |
| Chat/Contents 子目录 | 7 | 文本组件内容实现 PlainText/Translatable/Keybind/Score/Selector/Nbt/Object |
| Inventory 子目录 | 1 | MenuType |

## 规划

- DataComponents 预定义组件类型实现（MAX_STACK_SIZE/DAMAGE/ENCHANTMENTS 等）注册到 BuiltInRegistries.DATA_COMPONENT_TYPE
- ScoreContents/SelectorContents/NbtContents/ObjectContents 等复杂内容类型的 Codec 完善
- 后续 Play 阶段数据包和未来扩展业务包一律注册到 Game 模块
- Network 仅在框架 API 需要扩展时修改（如新增 ByteBuf 读写方法）
