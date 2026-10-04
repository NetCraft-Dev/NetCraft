using NetCraft.Nbt;
using NetCraft.Primitives;
using NetCraft.Primitives.Phys;
using NetCraft.Registry;
using NetCraft.Util;

namespace NetCraft.Storage;

//WorldBorder 世界边界存档对应原版 net.minecraft.world.level.border.WorldBorder
//尺寸/中心/伤害与警告参数随存档持久化到 data/minecraft/world_border.dat
//尺寸变化分静态与插值两种形态 形态切换由 extent 字段承载
public sealed class WorldBorder : SavedData
{
    //MaxSize 边界最大边长对应原版 MAX_SIZE
    public const double MaxSize = 5.9999968E7;

    //MaxCenterCoordinate 中心坐标绝对值上限对应原版 MAX_CENTER_COORDINATE
    public const double MaxCenterCoordinate = 2.9999984E7;

    //DefaultAbsoluteMaxSize 绝对最大尺寸对应原版 MinecraftServer.ABSOLUTE_MAX_WORLD_SIZE
    public const int DefaultAbsoluteMaxSize = 29999984;

    //TypeId 存档标识对应原版 SavedDataType 的 minecraft:world_border
    private const string TypeId = "minecraft:world_border";

    //Type SavedDataType 工厂 空标签建成默认边界 已存标签按字段还原
    public static readonly SavedDataType<WorldBorder> Type = new WorldBorderType();

    private readonly Settings _settings;
    private readonly List<IBorderChangeListener> _listeners = new();
    private bool _initialized;
    private BorderExtent _extent = null!;

    public override string Id => TypeId;

    //DamagePerBlock 每格越界伤害
    public double DamagePerBlock { get; private set; } = 0.2;

    //SafeZone 越界免伤缓冲格数
    public double SafeZone { get; private set; } = 5.0;

    //WarningTime 越界预警提前刻数
    public int WarningTime { get; private set; } = 15;

    //WarningBlocks 越界预警距离
    public int WarningBlocks { get; private set; } = 5;

    //CenterX 边界中心 X
    public double CenterX { get; private set; }

    //CenterZ 边界中心 Z
    public double CenterZ { get; private set; }

    //AbsoluteMaxSize 边界绝对尺寸上限
    public int AbsoluteMaxSize { get; private set; } = DefaultAbsoluteMaxSize;

    public WorldBorder() : this(Settings.Default) { }

    public WorldBorder(Settings settings)
    {
        _settings = settings;
        //初始形态是最大尺寸的静态边界 中心在原点 对应原版构造里的 extent 初始化
        _extent = new StaticBorderExtent(this, MaxSize);
    }

    //GetSize 当前边界边长
    public double GetSize() => _extent.Size;

    //GetLerpTime 插值剩余刻数 静态形态为 0
    public long GetLerpTime() => _extent.LerpTime;

    //GetLerpTarget 插值目标尺寸 静态形态为自身尺寸
    public double GetLerpTarget() => _extent.LerpTarget;

    //GetLerpSpeed 每刻尺寸变化速度
    public double GetLerpSpeed() => _extent.LerpSpeed;

    //GetStatus 边界当前状态
    public BorderStatus GetStatus() => _extent.Status;

    //GetMinX 边界最小 X 插值形态按部分刻插值
    public double GetMinX(float deltaPartialTick = 0f) => _extent.GetMinX(deltaPartialTick);

    //GetMaxX 边界最大 X
    public double GetMaxX(float deltaPartialTick = 0f) => _extent.GetMaxX(deltaPartialTick);

    //GetMinZ 边界最小 Z
    public double GetMinZ(float deltaPartialTick = 0f) => _extent.GetMinZ(deltaPartialTick);

    //GetMaxZ 边界最大 Z
    public double GetMaxZ(float deltaPartialTick = 0f) => _extent.GetMaxZ(deltaPartialTick);

    //GetCollisionShape 边界外的碰撞形状 边界内为空气
    public VoxelShape GetCollisionShape() => _extent.CollisionShape;

    //IsWithinBounds 坐标是否在边界内 单侧闭一开与原版一致
    public bool IsWithinBounds(double x, double z) => IsWithinBounds(x, z, 0.0);

    public bool IsWithinBounds(double x, double z, double margin)
        => x >= GetMinX() - margin && x < GetMaxX() + margin
            && z >= GetMinZ() - margin && z < GetMaxZ() + margin;

    //IsWithinBounds 方块位置是否在边界内
    public bool IsWithinBounds(BlockPos pos) => IsWithinBounds(pos.X, pos.Z);

    //IsWithinBounds 坐标是否在边界内
    public bool IsWithinBounds(Vec3 pos) => IsWithinBounds(pos.X, pos.Z);

    //IsWithinBounds 区块是否完整落在边界内
    public bool IsWithinBounds(ChunkPos pos)
        => IsWithinBounds(pos.MinBlockX, pos.MinBlockZ) && IsWithinBounds(pos.MaxBlockX, pos.MaxBlockZ);

    //IsWithinBounds 包围盒是否在边界内 上界收半个方块避免贴边判定
    public bool IsWithinBounds(AABB aabb)
        => IsWithinBounds(aabb.Min.X, aabb.Min.Z, aabb.Max.X - 9.999999747378752E-6, aabb.Max.Z - 9.999999747378752E-6);

    private bool IsWithinBounds(double minX, double minZ, double maxX, double maxZ)
        => IsWithinBounds(minX, minZ) && IsWithinBounds(maxX, maxZ);

    //ClampToBounds 把坐标夹进边界内 上界收半个方块
    public BlockPos ClampToBounds(double x, double y, double z)
        => new(Mth.Floor(ClampX(x)), Mth.Floor(y), Mth.Floor(ClampZ(z)));

    //ClampToBounds 把坐标夹进边界内
    public BlockPos ClampToBounds(BlockPos pos) => ClampToBounds(pos.X, pos.Y, pos.Z);

    //ClampToBounds 把坐标夹进边界内
    public BlockPos ClampToBounds(Vec3 pos) => ClampToBounds(pos.X, pos.Y, pos.Z);

    //ClampVec3ToBound 把坐标夹进边界内保留小数
    public Vec3 ClampVec3ToBound(double x, double y, double z) => new(ClampX(x), y, ClampZ(z));

    //ClampVec3ToBound 把坐标夹进边界内
    public Vec3 ClampVec3ToBound(Vec3 pos) => ClampVec3ToBound(pos.X, pos.Y, pos.Z);

    private double ClampX(double x) => Mth.Clamp(x, GetMinX(), GetMaxX() - 9.999999747378752E-6);

    private double ClampZ(double z) => Mth.Clamp(z, GetMinZ(), GetMaxZ() - 9.999999747378752E-6);

    //GetDistanceToBorder 到最近边界边的距离 边界外为负
    public double GetDistanceToBorder(double x, double z)
    {
        var fromNorth = z - GetMinZ();
        var fromSouth = GetMaxZ() - z;
        var fromWest = x - GetMinX();
        var fromEast = GetMaxX() - x;
        return Math.Min(Math.Min(fromWest, fromEast), Math.Min(fromNorth, fromSouth));
    }

    //SetCenter 设置边界中心
    public void SetCenter(double x, double z)
    {
        CenterX = x;
        CenterZ = z;
        _extent.OnCenterChange();
        SetDirty();
        foreach (var listener in _listeners.ToArray())
            listener.OnSetCenter(this, x, z);
    }

    //SetSize 直接设置边界尺寸 形态切回静态
    public void SetSize(double size)
    {
        _extent = new StaticBorderExtent(this, size);
        SetDirty();
        foreach (var listener in _listeners.ToArray())
            listener.OnSetSize(this, size);
    }

    //LerpSizeBetween 在 ticks 刻内把尺寸从 from 插值到 to 对应原版 lerpSizeBetween
    public void LerpSizeBetween(double from, double to, long ticks, long gameTime)
    {
        _extent = from == to
            ? new StaticBorderExtent(this, to)
            : new MovingBorderExtent(this, from, to, ticks, gameTime);
        SetDirty();
        foreach (var listener in _listeners.ToArray())
            listener.OnLerpSize(this, from, to, ticks, gameTime);
    }

    //SetAbsoluteMaxSize 设置绝对尺寸上限
    public void SetAbsoluteMaxSize(int absoluteMaxSize)
    {
        AbsoluteMaxSize = absoluteMaxSize;
        _extent.OnAbsoluteMaxSizeChange();
    }

    //SetSafeZone 设置越界免伤缓冲
    public void SetSafeZone(double safeZone)
    {
        SafeZone = safeZone;
        SetDirty();
        foreach (var listener in _listeners.ToArray())
            listener.OnSetSafeZone(this, safeZone);
    }

    //SetDamagePerBlock 设置每格越界伤害
    public void SetDamagePerBlock(double damagePerBlock)
    {
        DamagePerBlock = damagePerBlock;
        SetDirty();
        foreach (var listener in _listeners.ToArray())
            listener.OnSetDamagePerBlock(this, damagePerBlock);
    }

    //SetWarningTime 设置越界预警提前刻数
    public void SetWarningTime(int warningTime)
    {
        WarningTime = warningTime;
        SetDirty();
        foreach (var listener in _listeners.ToArray())
            listener.OnSetWarningTime(this, warningTime);
    }

    //SetWarningBlocks 设置越界预警距离
    public void SetWarningBlocks(int warningBlocks)
    {
        WarningBlocks = warningBlocks;
        SetDirty();
        foreach (var listener in _listeners.ToArray())
            listener.OnSetWarningBlocks(this, warningBlocks);
    }

    //AddListener 挂边界变化监听
    public void AddListener(IBorderChangeListener listener) => _listeners.Add(listener);

    //RemoveListener 摘边界变化监听
    public void RemoveListener(IBorderChangeListener listener) => _listeners.Remove(listener);

    //Tick 推进边界形态 插值形态每刻走一格
    public void Tick() => _extent = _extent.Update();

    //ApplyInitialSettings 首次访问时把存档参数灌进运行时字段 对应原版 applyInitialSettings
    //只做一次 之后以运行时字段为准
    public void ApplyInitialSettings(long gameTime)
    {
        if (_initialized) return;
        SetCenter(_settings.CenterX, _settings.CenterZ);
        SetDamagePerBlock(_settings.DamagePerBlock);
        SetSafeZone(_settings.SafeZone);
        SetWarningBlocks(_settings.WarningBlocks);
        SetWarningTime(_settings.WarningTime);
        if (_settings.LerpTime > 0)
            LerpSizeBetween(_settings.Size, _settings.LerpTarget, _settings.LerpTime, gameTime);
        else
            SetSize(_settings.Size);
        _initialized = true;
    }

    //Save 把当前边界参数写进标签 字段名对齐原版 Settings 的 codec
    public override CompoundTag Save(CompoundTag tag)
    {
        var settings = Settings.From(this);
        tag.PutDouble("center_x", settings.CenterX);
        tag.PutDouble("center_z", settings.CenterZ);
        tag.PutDouble("damage_per_block", settings.DamagePerBlock);
        tag.PutDouble("safe_zone", settings.SafeZone);
        tag.PutInt("warning_blocks", settings.WarningBlocks);
        tag.PutInt("warning_time", settings.WarningTime);
        tag.PutDouble("size", settings.Size);
        tag.PutLong("lerp_time", settings.LerpTime);
        tag.PutDouble("lerp_target", settings.LerpTarget);
        return tag;
    }

    //BorderExtent 边界形态接口对应原版 BorderExtent 静态与插值两种实现
    private interface BorderExtent
    {
        double GetMinX(float deltaPartialTick);
        double GetMaxX(float deltaPartialTick);
        double GetMinZ(float deltaPartialTick);
        double GetMaxZ(float deltaPartialTick);
        double Size { get; }
        double LerpSpeed { get; }
        long LerpTime { get; }
        double LerpTarget { get; }
        BorderStatus Status { get; }
        void OnAbsoluteMaxSizeChange();
        void OnCenterChange();
        BorderExtent Update();
        VoxelShape CollisionShape { get; }
    }

    //StaticBorderExtent 静态边界形态 尺寸固定 边界盒在中心或上限变化时重算
    private sealed class StaticBorderExtent : BorderExtent
    {
        private readonly WorldBorder _owner;
        private double _minX;
        private double _minZ;
        private double _maxX;
        private double _maxZ;
        private VoxelShape _shape = null!;

        public StaticBorderExtent(WorldBorder owner, double size)
        {
            _owner = owner;
            Size = size;
            UpdateBox();
        }

        public double Size { get; }

        public double LerpSpeed => 0.0;

        public long LerpTime => 0L;

        public double LerpTarget => Size;

        public BorderStatus Status => BorderStatus.Stationary;

        public VoxelShape CollisionShape => _shape;

        public double GetMinX(float deltaPartialTick) => _minX;

        public double GetMaxX(float deltaPartialTick) => _maxX;

        public double GetMinZ(float deltaPartialTick) => _minZ;

        public double GetMaxZ(float deltaPartialTick) => _maxZ;

        private void UpdateBox()
        {
            var half = Size / 2.0;
            var limit = (double)_owner.AbsoluteMaxSize;
            _minX = Mth.Clamp(_owner.CenterX - half, -limit, limit);
            _minZ = Mth.Clamp(_owner.CenterZ - half, -limit, limit);
            _maxX = Mth.Clamp(_owner.CenterX + half, -limit, limit);
            _maxZ = Mth.Clamp(_owner.CenterZ + half, -limit, limit);
            _shape = Shapes.Join(Shapes.Infinity,
                Shapes.Box(Math.Floor(_minX), double.NegativeInfinity, Math.Floor(_minZ),
                    Math.Ceiling(_maxX), double.PositiveInfinity, Math.Ceiling(_maxZ)),
                BooleanOps.OnlyFirst);
        }

        public void OnAbsoluteMaxSizeChange() => UpdateBox();

        public void OnCenterChange() => UpdateBox();

        public BorderExtent Update() => this;
    }

    //MovingBorderExtent 插值边界形态 每刻推进一格进度插值形态结束后切回静态
    private sealed class MovingBorderExtent : BorderExtent
    {
        private readonly WorldBorder _owner;
        private readonly double _from;
        private readonly double _to;
        private readonly long _lerpEnd;
        private readonly long _lerpBegin;
        private readonly double _lerpDuration;
        private long _lerpProgress;
        private double _size;
        private double _previousSize;

        public MovingBorderExtent(WorldBorder owner, double from, double to, long duration, long gameTime)
        {
            _owner = owner;
            _from = from;
            _to = to;
            _lerpDuration = duration;
            _lerpProgress = duration;
            _lerpBegin = gameTime;
            _lerpEnd = _lerpBegin + duration;
            _size = CalculateSize();
            _previousSize = _size;
        }

        public double Size => _size;

        public double LerpSpeed => Math.Abs(_from - _to) / (_lerpEnd - _lerpBegin);

        public long LerpTime => _lerpProgress;

        public double LerpTarget => _to;

        public BorderStatus Status => _to < _from ? BorderStatus.Shrinking : BorderStatus.Growing;

        public VoxelShape CollisionShape => Shapes.Join(Shapes.Infinity,
            Shapes.Box(Math.Floor(GetMinX(0f)), double.NegativeInfinity, Math.Floor(GetMinZ(0f)),
                Math.Ceiling(GetMaxX(0f)), double.PositiveInfinity, Math.Ceiling(GetMaxZ(0f))),
            BooleanOps.OnlyFirst);

        public double GetMinX(float deltaPartialTick)
            => Mth.Clamp(_owner.CenterX - Mth.Lerp(deltaPartialTick, _previousSize, _size) / 2.0,
                -_owner.AbsoluteMaxSize, _owner.AbsoluteMaxSize);

        public double GetMaxX(float deltaPartialTick)
            => Mth.Clamp(_owner.CenterX + Mth.Lerp(deltaPartialTick, _previousSize, _size) / 2.0,
                -_owner.AbsoluteMaxSize, _owner.AbsoluteMaxSize);

        public double GetMinZ(float deltaPartialTick)
            => Mth.Clamp(_owner.CenterZ - Mth.Lerp(deltaPartialTick, _previousSize, _size) / 2.0,
                -_owner.AbsoluteMaxSize, _owner.AbsoluteMaxSize);

        public double GetMaxZ(float deltaPartialTick)
            => Mth.Clamp(_owner.CenterZ + Mth.Lerp(deltaPartialTick, _previousSize, _size) / 2.0,
                -_owner.AbsoluteMaxSize, _owner.AbsoluteMaxSize);

        private double CalculateSize()
        {
            var progress = (_lerpDuration - _lerpProgress) / _lerpDuration;
            return progress < 1.0 ? Mth.Lerp(progress, _from, _to) : _to;
        }

        public void OnCenterChange() { }

        public void OnAbsoluteMaxSizeChange() { }

        public BorderExtent Update()
        {
            _lerpProgress--;
            _previousSize = _size;
            _size = CalculateSize();
            _owner.SetDirty();
            return _lerpProgress <= 0 ? new StaticBorderExtent(_owner, _to) : this;
        }
    }

    //Settings 边界持久化参数对应原版 WorldBorder.Settings
    public sealed record Settings(
        double CenterX,
        double CenterZ,
        double DamagePerBlock,
        double SafeZone,
        int WarningBlocks,
        int WarningTime,
        double Size,
        long LerpTime,
        double LerpTarget)
    {
        //Default 默认边界参数对应原版 Settings.DEFAULT
        public static readonly Settings Default = new(0.0, 0.0, 0.2, 5.0, 5, 300, MaxSize, 0, 0.0);

        //From 取当前边界快照 对应原版 Settings(WorldBorder)
        public static Settings From(WorldBorder border)
            => new(border.CenterX, border.CenterZ, border.DamagePerBlock, border.SafeZone,
                border.WarningBlocks, border.WarningTime, border.GetSize(),
                border.GetLerpTime(), border.GetLerpTarget());
    }

    //WorldBorderType SavedDataType 实现 空标签建默认边界 已存标签逐字段还原
    private sealed class WorldBorderType : SavedDataType<WorldBorder>
    {
        public string Id => TypeId;

        public WorldBorder Create(CompoundTag tag, RegistryAccess registryAccess)
        {
            if (tag.Count == 0) return new WorldBorder();
            var fallback = Settings.Default;
            var settings = new Settings(
                tag.GetDouble("center_x")?.Value ?? fallback.CenterX,
                tag.GetDouble("center_z")?.Value ?? fallback.CenterZ,
                tag.GetDouble("damage_per_block")?.Value ?? fallback.DamagePerBlock,
                tag.GetDouble("safe_zone")?.Value ?? fallback.SafeZone,
                tag.GetInt("warning_blocks")?.Value ?? fallback.WarningBlocks,
                tag.GetInt("warning_time")?.Value ?? fallback.WarningTime,
                tag.GetDouble("size")?.Value ?? fallback.Size,
                tag.GetLong("lerp_time")?.Value ?? fallback.LerpTime,
                tag.GetDouble("lerp_target")?.Value ?? fallback.LerpTarget);
            return new WorldBorder(settings);
        }
    }
}
