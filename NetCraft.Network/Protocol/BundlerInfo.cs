namespace NetCraft.Network.Protocol;

//BundlerInfo 打包信息对应原版 net.minecraft.network.protocol.BundlerInfo
//描述 bundle 包的拆解和组装逻辑
//THandler 是包处理器类型所有 bundle 子包都继承 Packet<THandler>
public interface BundlerInfo<THandler>
{
    //BundleSizeLimit 单个 bundle 最多 4096 个子包
    public const int BundleSizeLimit = 4096;

    //Bundler 子包收集器
    //AddPacket 加入子包返回 null 继续收集返回非 null 表示 bundle 完成应发送
    public interface Bundler<THandler>
    {
        Packet<THandler>? AddPacket(Packet<THandler> packet);
    }

    //UnbundlePacket 拆解 bundle 包为子包序列输出到 output
    void UnbundlePacket(Packet<THandler> packet, Action<Packet<THandler>> output);

    //StartPacketBundling 收到 bundle 起始分隔符时开始组装返回 Bundler 否则返回 null
    Bundler<THandler>? StartPacketBundling(Packet<THandler> packet);

    //CreateForPacket 为指定 bundle 包类型创建 BundlerInfo
    //bundlePacketType bundle 包类型
    //constructor 子包序列构造 bundle 包的工厂
    //delimiterPacket 起止分隔符包实例
    static BundlerInfo<THandler> CreateForPacket(
        PacketType<THandler> bundlePacketType,
        Func<IEnumerable<Packet<THandler>>, BundlePacket<THandler>> constructor,
        BundleDelimiterPacket<THandler> delimiterPacket)
        => new ForPacketBundlerInfo<THandler>(constructor, delimiterPacket);
}

//ForPacketBundlerInfo BundlerInfo.CreateForPacket 实现
file sealed class ForPacketBundlerInfo<THandler> : BundlerInfo<THandler>
{
    private readonly Func<IEnumerable<Packet<THandler>>, BundlePacket<THandler>> _constructor;
    private readonly BundleDelimiterPacket<THandler> _delimiterPacket;

    public ForPacketBundlerInfo(
        Func<IEnumerable<Packet<THandler>>, BundlePacket<THandler>> constructor,
        BundleDelimiterPacket<THandler> delimiterPacket)
    {
        _constructor = constructor;
        _delimiterPacket = delimiterPacket;
    }

    public void UnbundlePacket(Packet<THandler> packet, Action<Packet<THandler>> output)
    {
        //bundle 包：输出分隔符 + 子包序列 + 分隔符
        if (packet is BundlePacket<THandler> bundle)
        {
            output(_delimiterPacket);
            foreach (var sub in bundle.SubPackets())
                output(sub);
            output(_delimiterPacket);
            return;
        }
        output(packet);
    }

    public BundlerInfo<THandler>.Bundler<THandler>? StartPacketBundling(Packet<THandler> packet)
    {
        //收到起始分隔符包开始组装 Bundler
        if (ReferenceEquals(packet, _delimiterPacket))
            return new PacketBundler<THandler>(_constructor, _delimiterPacket);
        return null;
    }
}

//PacketBundler 收集子包达到上限或收到结束分隔符时构造 bundle 包
file sealed class PacketBundler<THandler> : BundlerInfo<THandler>.Bundler<THandler>
{
    private readonly Func<IEnumerable<Packet<THandler>>, BundlePacket<THandler>> _constructor;
    private readonly BundleDelimiterPacket<THandler> _delimiter;
    private readonly List<Packet<THandler>> _bundlePackets = new();

    public PacketBundler(
        Func<IEnumerable<Packet<THandler>>, BundlePacket<THandler>> constructor,
        BundleDelimiterPacket<THandler> delimiter)
    {
        _constructor = constructor;
        _delimiter = delimiter;
    }

    public Packet<THandler>? AddPacket(Packet<THandler> packet)
    {
        //收到结束分隔符包构造 bundle 包返回
        if (ReferenceEquals(packet, _delimiter))
            return _constructor(_bundlePackets);
        //超出上限抛异常
        if (_bundlePackets.Count >= BundlerInfo<THandler>.BundleSizeLimit)
            throw new InvalidOperationException("Bundle 子包数量超限 " + BundlerInfo<THandler>.BundleSizeLimit);
        _bundlePackets.Add(packet);
        return null;
    }
}
