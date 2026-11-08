using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace ResinGrove;

public sealed class ResinGroveConfig
{
    // Null follows the harvested resin log's configured transient interval.
    public double? HoursPerResin { get; set; }
    public int BucketCapacity { get; set; } = 8;

    public void Validate()
    {
        if (HoursPerResin is double hours && (!double.IsFinite(hours) || hours <= 0)) HoursPerResin = null;
        BucketCapacity = Math.Clamp(BucketCapacity, 1, 32);
    }
}

public sealed class ResinGroveSystem : ModSystem
{
    public ResinGroveConfig Config { get; private set; } = new();
    private readonly HashSet<string> missingRateWarnings = new();

    public double ResolveHoursPerResin(IWorldAccessor world, string wood)
    {
        if (Config.HoursPerResin is double hours) return hours;

        // Read resolved block attributes so vanilla changes and other mods' patches apply.
        // Use vanilla's own schema, including its default if inGameHours is omitted.
        Block? harvested = world.GetBlock(new AssetLocation("game", $"log-resinharvested-{wood}-ud"));
        TransientProperties? properties = harvested?.Attributes?["transientProps"].AsObject<TransientProperties>();
        double interval = properties?.InGameHours ?? 0;
        if (double.IsFinite(interval) && interval > 0) return interval;

        if (missingRateWarnings.Add(wood))
            world.Logger.Error("Resin Grove: no valid vanilla resin regrowth interval for {0}; production is paused. Set HoursPerResin to override it.", wood);
        return 0;
    }

    public override void Start(ICoreAPI api)
    {
        api.RegisterItemClass("ResinGroveTap", typeof(ItemTreeTap));
        api.RegisterBlockClass("ResinGroveTappedLog", typeof(BlockTappedLog));
        api.RegisterBlockClass("ResinGroveHangingBucket", typeof(BlockHangingBucket));
        api.RegisterBlockEntityClass("ResinGroveTappedLog", typeof(BlockEntityTappedLog));
        api.RegisterBlockEntityClass("ResinGroveHangingBucket", typeof(BlockEntityHangingBucket));
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        Config = api.LoadModConfig<ResinGroveConfig>("resingrove.json") ?? new();
        Config.Validate();
        api.StoreModConfig(Config, "resingrove.json");
    }
}
