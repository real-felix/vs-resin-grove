using ResinGrove.Core;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace ResinGrove.Integration;

/// <summary>Runs only in the disposable world created by scripts/integration.py.</summary>
public sealed class IntegrationSystem : ModSystem
{
    private const string PhaseKey = "resingrove-integration-phase";
    private ICoreServerAPI api = null!;
    private readonly BlockPos root = new(512016, 220, 512016);
    private BlockPos TapPos => root.UpCopy(1);
    private BlockPos BucketPos => TapPos.AddCopy(BlockFacing.NORTH);
    private string RegistryKey => $"resingrove:tap:{root.dimension}:{root.X}:{root.Y}:{root.Z}";

    public override void StartServerSide(ICoreServerAPI api)
    {
        this.api = api;
        api.Event.ServerRunPhase(EnumServerRunPhase.RunGame, () =>
            api.WorldManager.LoadChunkColumnPriority(root.X / 32, root.Z / 32,
                new ChunkLoadOptions { KeepLoaded = true, OnLoaded = () => api.Event.RegisterCallback(_ => Run(), 200) }));
    }

    private void Run()
    {
        try
        {
            if (api.WorldManager.SaveGame.GetData(PhaseKey) == null) FirstRun();
            else SecondRun();
            api.Logger.Notification("RESINGROVE INTEGRATION PASS");
        }
        catch (Exception e) { api.Logger.Error("RESINGROVE INTEGRATION FAIL: {0}", e); }
        api.Event.RegisterCallback(_ => api.Server.ShutDown(), 500);
    }

    private void FirstRun()
    {
        CheckProductionRate();
        var recipes = api.GetSmithingRecipes().Where(r => r.Output.Code.Domain == "resingrove").ToArray();
        Check(recipes.Length == 9 && recipes.All(r => r.Output.StackSize == 2), "Nine smithing recipes must each output two taps");
        foreach (string wood in new[] { "pine", "acacia" })
        foreach (string rotation in new[] { "ud", "ns", "we" })
        foreach (string face in new[] { "north", "east", "south", "west" })
        {
            Block tapped = Block($"resingrove:tappedlog-{wood}-{rotation}-{face}");
            Check(tapped is BlockTappedLog && tapped.EntityClass == "ResinGroveTappedLog", "Tapped log class registration");
            Check(tapped.Attributes["treeFellingGroupCode"].AsString() == wood, "Tree felling species preserved");
            Check(tapped.Attributes["treeFellingGroupSpreadIndex"].AsInt() == (wood == "pine" ? 3 : 4), "Tree felling spread preserved");
            Check(tapped.PlacedPriorityInteract, "Tap gets bucket interactions before held bucket");
        }

        var blocks = api.World.BlockAccessor;
        for (int x = -3; x <= 3; x++)
        for (int y = -1; y <= 7; y++)
        for (int z = -3; z <= 3; z++) blocks.SetBlock(0, root.AddCopy(x, y, z));
        blocks.SetBlock(Block("game:soil-medium-normal").Id, root.DownCopy());
        for (int y = 0; y <= 4; y++) blocks.SetBlock(Block("game:log-grown-pine-ud").Id, root.UpCopy(y));
        blocks.SetBlock(Block("game:leaves-grown-pine").Id, root.AddCopy(1, 4, 0));
        blocks.SetBlock(Block("game:leaves-grown-pine").Id, root.AddCopy(-1, 4, 0));
        var inspection = TreeInspector.Inspect(new TreeWorld(blocks, 0), TreeWorld.Position(TapPos));
        Check(inspection.Valid && inspection.Root == TreeWorld.Position(root), "Real game log and leaf identification");

        blocks.SetBlock(Block("resingrove:tappedlog-pine-ud-north").Id, TapPos);
        var tap = (BlockEntityTappedLog)blocks.GetBlockEntity(TapPos);
        tap.Install(root, new ItemStack(api.World.GetItem(new AssetLocation("resingrove:tap-copper"))));
        Check(blocks.GetChunkAtBlockPos(root).GetModdata(RegistryKey)?.Length > 0, "Reservation lives in bottom log chunk");
        Check(TreeInspector.Inspect(new TreeWorld(blocks, 0), TreeWorld.Position(root)).Error == "already-tapped", "Duplicate tap rejected in real world");
        ItemStack[] drops = ((BlockTappedLog)tap.Block).GetDrops(api.World, TapPos, null!);
        Check(drops.Length == 2 && drops.Any(s => s.Collectible.Code.Path == "tap-copper")
            && drops.Any(s => s.Collectible.Code.Path == "log-placed-pine-ud"), "Tapped log returns log and original tap");

        blocks.SetBlock(Block("resingrove:hangingbucket-north").Id, BucketPos);
        var bucket = (BlockEntityHangingBucket)blocks.GetBlockEntity(BucketPos);
        bucket.Attach(new ItemStack(Block("game:woodbucket")));
        var state = new TreeAttribute();
        bucket.ToTreeAttributes(state);
        double hours = api.ModLoader.GetModSystem<ResinGroveSystem>().ResolveHoursPerResin(api.World, "pine");
        state.SetDouble("lastHours", api.World.Calendar.TotalHours - hours * 1.5);
        bucket.FromTreeAttributes(state, api.World);
        ItemStack[] contents = bucket.GetContents();
        Check(contents.Length == 2 && contents.Any(s => s.Collectible.Code.Path == "resin" && s.StackSize == 1), "Passive production yields vanilla resin");
        bucket.ToTreeAttributes(state);
        Check(state.GetDouble("resin") >= 1.5 && state.GetDouble("resin") < 1.6, "Fractional progress retained");
        api.WorldManager.SaveGame.StoreData(PhaseKey, new byte[] { 1 });
        api.Logger.Notification("RESINGROVE: registered recipes, tree validation, drops, production verified; saving for restart");
    }

    private void SecondRun()
    {
        Check(api.ModLoader.GetModSystem<ResinGroveSystem>().Config.HoursPerResin == null,
            "Following vanilla remains the default after config save and reload");
        var blocks = api.World.BlockAccessor;
        Check(blocks.GetBlockEntity(TapPos) is BlockEntityTappedLog, "Tapped log survived restart");
        var tap = (BlockEntityTappedLog)blocks.GetBlockEntity(TapPos);
        Check(tap.Root?.Equals(root) == true && tap.Tap?.Collectible.Code.Path == "tap-copper", "Tap root and metal survived restart");
        Check(blocks.GetChunkAtBlockPos(root).GetModdata(RegistryKey)?.Length > 0, "Root reservation survived restart");
        Check(blocks.GetBlockEntity(BucketPos) is BlockEntityHangingBucket, "Hanging bucket survived restart");
        var bucket = (BlockEntityHangingBucket)blocks.GetBlockEntity(BucketPos);
        ItemStack[] contents = bucket.GetContents();
        Check(contents.Any(s => s.Collectible.Code.Path == "woodbucket")
            && contents.Any(s => s.Collectible.Code.Path == "resin" && s.StackSize >= 1), "Bucket and resin survived restart");
        blocks.SetBlock(0, TapPos);
        Check(blocks.GetBlock(BucketPos).Id == 0, "Removing tapped log safely removes hanging bucket");
        Check(blocks.GetChunkAtBlockPos(root).GetModdata(RegistryKey) == null, "Removing tap releases root reservation");
        api.Logger.Notification("RESINGROVE: restart persistence and support removal verified");
    }

    private void CheckProductionRate()
    {
        var system = api.ModLoader.GetModSystem<ResinGroveSystem>();
        Check(system.Config.HoursPerResin == null, "New configs follow vanilla by default");
        foreach (string wood in new[] { "pine", "acacia" })
        {
            Block vanilla = Block($"game:log-resinharvested-{wood}-ud");
            var properties = vanilla.Attributes["transientProps"];
            double vanillaHours = (properties.AsObject<TransientProperties>()
                ?? throw new Exception("Missing vanilla transient properties: " + wood)).InGameHours;
            Check(system.ResolveHoursPerResin(api.World, wood) == vanillaHours, "Default rate matches vanilla " + wood);
            JToken token = properties.Token ?? throw new Exception("Missing vanilla transient JSON: " + wood);
            JToken original = token["inGameHours"]?.DeepClone() ?? throw new Exception("Missing vanilla regrowth hours: " + wood);
            try
            {
                // Simulate another mod changing this species' resolved block properties.
                double patchedHours = vanillaHours + 12;
                token["inGameHours"] = patchedHours;
                Check(system.ResolveHoursPerResin(api.World, wood) == patchedHours, "Patched vanilla rates are respected");
                system.Config.HoursPerResin = 5.5;
                Check(system.ResolveHoursPerResin(api.World, wood) == 5.5, "Explicit production override wins");
            }
            finally
            {
                token["inGameHours"] = original;
                system.Config.HoursPerResin = null;
            }
        }
        foreach (double invalid in new[] { 0d, -1, double.NaN, double.PositiveInfinity })
        {
            var config = new ResinGroveConfig { HoursPerResin = invalid };
            config.Validate();
            Check(config.HoursPerResin == null, "Invalid production overrides fall back to vanilla");
        }
        var existing = new ResinGroveConfig { HoursPerResin = 24 };
        existing.Validate();
        Check(existing.HoursPerResin == 24, "Existing numeric configs retain their override");
        api.Logger.Notification("RESINGROVE: vanilla resin defaults, patched values, and explicit overrides verified");
    }

    private Block Block(string code) => api.World.GetBlock(new AssetLocation(code)) is { Id: > 0 } block
        ? block : throw new Exception("Missing block: " + code);
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
}
