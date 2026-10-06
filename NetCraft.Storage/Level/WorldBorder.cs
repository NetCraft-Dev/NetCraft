using NetCraft.Nbt;
using NetCraft.Primitives;
using NetCraft.Primitives.Phys;
using NetCraft.Registry;
using NetCraft.Util;

namespace NetCraft.Storage;

//WorldBorder, world border save data, maps to vanilla net.minecraft.world.level.border.WorldBorder
//Size/center/damage and warning params persist with the save to data/minecraft/world_border.dat
//Size changes come in static and interpolated forms; the form switch is carried by the extent field
public sealed class WorldBorder : SavedData
{
    //MaxSize, the maximum border side length, maps to vanilla MAX_SIZE
    public const double MaxSize = 5.9999968E7;

    //MaxCenterCoordinate, the center coordinate absolute limit, maps to vanilla MAX_CENTER_COORDINATE
    public const double MaxCenterCoordinate = 2.9999984E7;

    //DefaultAbsoluteMaxSize, the absolute max size, maps to vanilla MinecraftServer.ABSOLUTE_MAX_WORLD_SIZE
    public const int DefaultAbsoluteMaxSize = 29999984;

    //TypeId, the save identifier, maps to the minecraft:world_border of the vanilla SavedDataType
    private const string TypeId = "minecraft:world_border";

    //Type, the SavedDataType factory; an empty tag builds a default border, a stored tag restores by field
    public static readonly SavedDataType<WorldBorder> Type = new WorldBorderType();

    private readonly Settings _settings;
    private readonly List<IBorderChangeListener> _listeners = new();
    private bool _initialized;
    private BorderExtent _extent = null!;

    public override string Id => TypeId;

    //DamagePerBlock, out-of-bounds damage per block
    public double DamagePerBlock { get; private set; } = 0.2;

    //SafeZone, out-of-bounds safe zone in blocks
    public double SafeZone { get; private set; } = 5.0;

    //WarningTime, out-of-bounds warning lead in ticks
    public int WarningTime { get; private set; } = 15;

    //WarningBlocks, out-of-bounds warning distance
    public int WarningBlocks { get; private set; } = 5;

    //CenterX, the border center X
    public double CenterX { get; private set; }

    //CenterZ, the border center Z
    public double CenterZ { get; private set; }

    //AbsoluteMaxSize, the border's absolute size limit
    public int AbsoluteMaxSize { get; private set; } = DefaultAbsoluteMaxSize;

    public WorldBorder() : this(Settings.Default) { }

    public WorldBorder(Settings settings)
    {
        _settings = settings;
        //The initial form is a static border at maximum size centered at the origin, maps to the extent initialization in the vanilla constructor
        _extent = new StaticBorderExtent(this, MaxSize);
    }

    //GetSize, the current border side length
    public double GetSize() => _extent.Size;

    //GetLerpTime, remaining interpolation ticks; 0 in the static form
    public long GetLerpTime() => _extent.LerpTime;

    //GetLerpTarget, the interpolation target size; the own size in the static form
    public double GetLerpTarget() => _extent.LerpTarget;

    //GetLerpSpeed, size change rate per tick
    public double GetLerpSpeed() => _extent.LerpSpeed;

    //GetStatus, the border's current status
    public BorderStatus GetStatus() => _extent.Status;

    //GetMinX, the border minimum X; the interpolated form interpolates by partial tick
    public double GetMinX(float deltaPartialTick = 0f) => _extent.GetMinX(deltaPartialTick);

    //GetMaxX, the border maximum X
    public double GetMaxX(float deltaPartialTick = 0f) => _extent.GetMaxX(deltaPartialTick);

    //GetMinZ, the border minimum Z
    public double GetMinZ(float deltaPartialTick = 0f) => _extent.GetMinZ(deltaPartialTick);

    //GetMaxZ, the border maximum Z
    public double GetMaxZ(float deltaPartialTick = 0f) => _extent.GetMaxZ(deltaPartialTick);

    //GetCollisionShape, the collision shape outside the border; inside is air
    public VoxelShape GetCollisionShape() => _extent.CollisionShape;

    //IsWithinBounds, whether the coords are inside the border; half-open per side as in vanilla
    public bool IsWithinBounds(double x, double z) => IsWithinBounds(x, z, 0.0);

    public bool IsWithinBounds(double x, double z, double margin)
        => x >= GetMinX() - margin && x < GetMaxX() + margin
            && z >= GetMinZ() - margin && z < GetMaxZ() + margin;

    //IsWithinBounds, whether the block pos is inside the border
    public bool IsWithinBounds(BlockPos pos) => IsWithinBounds(pos.X, pos.Z);

    //IsWithinBounds, whether the coords are inside the border
    public bool IsWithinBounds(Vec3 pos) => IsWithinBounds(pos.X, pos.Z);

    //IsWithinBounds, whether the chunk lies fully inside the border
    public bool IsWithinBounds(ChunkPos pos)
        => IsWithinBounds(pos.MinBlockX, pos.MinBlockZ) && IsWithinBounds(pos.MaxBlockX, pos.MaxBlockZ);

    //IsWithinBounds, whether the AABB is inside the border; the upper bound is pulled in half a block to avoid edge-fitting
    public bool IsWithinBounds(AABB aabb)
        => IsWithinBounds(aabb.Min.X, aabb.Min.Z, aabb.Max.X - 9.999999747378752E-6, aabb.Max.Z - 9.999999747378752E-6);

    private bool IsWithinBounds(double minX, double minZ, double maxX, double maxZ)
        => IsWithinBounds(minX, minZ) && IsWithinBounds(maxX, maxZ);

    //ClampToBounds clamps coords into the border; the upper bound is pulled in half a block
    public BlockPos ClampToBounds(double x, double y, double z)
        => new(Mth.Floor(ClampX(x)), Mth.Floor(y), Mth.Floor(ClampZ(z)));

    //ClampToBounds clamps coords into the border
    public BlockPos ClampToBounds(BlockPos pos) => ClampToBounds(pos.X, pos.Y, pos.Z);

    //ClampToBounds clamps coords into the border
    public BlockPos ClampToBounds(Vec3 pos) => ClampToBounds(pos.X, pos.Y, pos.Z);

    //ClampVec3ToBound clamps coords into the border, keeping the fraction
    public Vec3 ClampVec3ToBound(double x, double y, double z) => new(ClampX(x), y, ClampZ(z));

    //ClampVec3ToBound clamps coords into the border
    public Vec3 ClampVec3ToBound(Vec3 pos) => ClampVec3ToBound(pos.X, pos.Y, pos.Z);

    private double ClampX(double x) => Mth.Clamp(x, GetMinX(), GetMaxX() - 9.999999747378752E-6);

    private double ClampZ(double z) => Mth.Clamp(z, GetMinZ(), GetMaxZ() - 9.999999747378752E-6);

    //GetDistanceToBorder, the distance to the nearest border edge; negative outside
    public double GetDistanceToBorder(double x, double z)
    {
        var fromNorth = z - GetMinZ();
        var fromSouth = GetMaxZ() - z;
        var fromWest = x - GetMinX();
        var fromEast = GetMaxX() - x;
        return Math.Min(Math.Min(fromWest, fromEast), Math.Min(fromNorth, fromSouth));
    }

    //SetCenter sets the border center
    public void SetCenter(double x, double z)
    {
        CenterX = x;
        CenterZ = z;
        _extent.OnCenterChange();
        SetDirty();
        foreach (var listener in _listeners.ToArray())
            listener.OnSetCenter(this, x, z);
    }

    //SetSize sets the border size directly and switches the form back to static
    public void SetSize(double size)
    {
        _extent = new StaticBorderExtent(this, size);
        SetDirty();
        foreach (var listener in _listeners.ToArray())
            listener.OnSetSize(this, size);
    }

    //LerpSizeBetween interpolates the size from from to to over ticks, maps to vanilla lerpSizeBetween
    public void LerpSizeBetween(double from, double to, long ticks, long gameTime)
    {
        _extent = from == to
            ? new StaticBorderExtent(this, to)
            : new MovingBorderExtent(this, from, to, ticks, gameTime);
        SetDirty();
        foreach (var listener in _listeners.ToArray())
            listener.OnLerpSize(this, from, to, ticks, gameTime);
    }

    //SetAbsoluteMaxSize sets the absolute size limit
    public void SetAbsoluteMaxSize(int absoluteMaxSize)
    {
        AbsoluteMaxSize = absoluteMaxSize;
        _extent.OnAbsoluteMaxSizeChange();
    }

    //SetSafeZone sets the out-of-bounds safe zone
    public void SetSafeZone(double safeZone)
    {
        SafeZone = safeZone;
        SetDirty();
        foreach (var listener in _listeners.ToArray())
            listener.OnSetSafeZone(this, safeZone);
    }

    //SetDamagePerBlock sets the out-of-bounds damage per block
    public void SetDamagePerBlock(double damagePerBlock)
    {
        DamagePerBlock = damagePerBlock;
        SetDirty();
        foreach (var listener in _listeners.ToArray())
            listener.OnSetDamagePerBlock(this, damagePerBlock);
    }

    //SetWarningTime sets the out-of-bounds warning lead in ticks
    public void SetWarningTime(int warningTime)
    {
        WarningTime = warningTime;
        SetDirty();
        foreach (var listener in _listeners.ToArray())
            listener.OnSetWarningTime(this, warningTime);
    }

    //SetWarningBlocks sets the out-of-bounds warning distance
    public void SetWarningBlocks(int warningBlocks)
    {
        WarningBlocks = warningBlocks;
        SetDirty();
        foreach (var listener in _listeners.ToArray())
            listener.OnSetWarningBlocks(this, warningBlocks);
    }

    //AddListener attaches a border change listener
    public void AddListener(IBorderChangeListener listener) => _listeners.Add(listener);

    //RemoveListener detaches a border change listener
    public void RemoveListener(IBorderChangeListener listener) => _listeners.Remove(listener);

    //Tick advances the border form; the interpolated form steps once per tick
    public void Tick() => _extent = _extent.Update();

    //ApplyInitialSettings pours saved params into runtime fields on first access, maps to vanilla applyInitialSettings
    //Done only once; afterwards runtime fields take precedence
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

    //Save writes the current border params into the tag; field names align with the vanilla Settings codec
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

    //BorderExtent, border form interface, maps to vanilla BorderExtent with static and interpolated implementations
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

    //StaticBorderExtent, static border form; the size is fixed and the border box is recomputed when the center or limit changes
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

    //MovingBorderExtent, interpolated border form; progress advances one step per tick and it switches back to static when done
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

    //Settings, border persistence params, maps to vanilla WorldBorder.Settings
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
        //Default, default border params, maps to vanilla Settings.DEFAULT
        public static readonly Settings Default = new(0.0, 0.0, 0.2, 5.0, 5, 300, MaxSize, 0, 0.0);

        //From takes a snapshot of the current border, maps to vanilla Settings(WorldBorder)
        public static Settings From(WorldBorder border)
            => new(border.CenterX, border.CenterZ, border.DamagePerBlock, border.SafeZone,
                border.WarningBlocks, border.WarningTime, border.GetSize(),
                border.GetLerpTime(), border.GetLerpTarget());
    }

    //WorldBorderType SavedDataType implementation; an empty tag builds a default border, a stored tag restores field by field
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
