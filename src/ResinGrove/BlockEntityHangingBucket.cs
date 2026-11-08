using System.Text;
using ResinGrove.Core;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace ResinGrove;

public sealed class BlockEntityHangingBucket : BlockEntity
{
    private ItemStack? bucket;
    private ResinProduction production = new(0, 0);
    private int capacity = 8;
    private MeshData? mesh;
    private bool removing;
    public BlockPos TapPosition => ((BlockHangingBucket)Block).TapPosition(Pos);

    public override void Initialize(ICoreAPI api)
    {
        base.Initialize(api);
        if (api.Side == EnumAppSide.Server) RegisterGameTickListener(Tick, 30000);
    }

    public void Attach(ItemStack storedBucket)
    {
        bucket = storedBucket;
        production = new ResinProduction(0, Api.World.Calendar.TotalHours);
        capacity = Api.ModLoader.GetModSystem<ResinGroveSystem>().Config.BucketCapacity;
        MarkDirty(true);
    }

    public void CheckSupport()
    {
        if (Api.World.BlockAccessor.GetChunkAtBlockPos(TapPosition) == null) return;
        if (Api.World.BlockAccessor.GetBlock(TapPosition) is not BlockTappedLog tapped
            || !TapPosition.AddCopy(tapped.TapFace).Equals(Pos)) DropAndRemove();
    }

    private void Tick(float dt)
    {
        CheckSupport();
        if (!removing) UpdateProduction();
    }

    private void UpdateProduction()
    {
        ResinGroveSystem system = Api.ModLoader.GetModSystem<ResinGroveSystem>();
        ResinGroveConfig config = system.Config;
        capacity = config.BucketCapacity;
        TreeInspection tree = TreeInspector.Inspect(new TreeWorld(Api.World.BlockAccessor, Pos.dimension),
            TreeWorld.Position(TapPosition), allowExistingTap: true);
        string? wood = Api.World.BlockAccessor.GetBlock(TapPosition).Variant?["wood"];
        double hours = tree.Valid && wood != null ? system.ResolveHoursPerResin(Api.World, wood) : 0;
        production.Advance(Api.World.Calendar.TotalHours, hours, capacity, tree.Valid);
        // Save fractional progress too; a reload must not reset the production clock.
        MarkDirty(true);
    }

    public void Interact(IPlayer player, bool remove)
    {
        UpdateProduction();
        int count = production.Collect();
        if (count > 0) Interaction.GiveOrDrop(Api.World, player,
            new ItemStack(Api.World.GetItem(new AssetLocation("game:resin")), count), Pos);
        if (remove)
        {
            // Resin is a discrete vanilla item: only complete units are collectible.
            if (bucket != null) Interaction.GiveOrDrop(Api.World, player, bucket.Clone(), Pos);
            removing = true;
            Api.World.BlockAccessor.SetBlock(0, Pos);
        }
        else MarkDirty(true);
    }

    public ItemStack[] GetContents()
    {
        if (Api.Side == EnumAppSide.Server) UpdateProduction();
        var contents = new List<ItemStack>();
        if (bucket != null) contents.Add(bucket.Clone());
        int count = (int)Math.Floor(production.Amount + 1e-9);
        if (count > 0) contents.Add(new ItemStack(Api.World.GetItem(new AssetLocation("game:resin")), count));
        return contents.ToArray();
    }

    public void DropAndRemove()
    {
        if (removing) return;
        removing = true;
        foreach (ItemStack stack in GetContents())
            Api.World.SpawnItemEntity(stack, Pos.ToVec3d().Add(0.5, 0.5, 0.5));
        Api.World.BlockAccessor.SetBlock(0, Pos);
    }

    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);
        tree.SetItemstack("bucket", bucket);
        tree.SetDouble("resin", production.Amount);
        tree.SetDouble("lastHours", production.LastHours);
        tree.SetInt("capacity", capacity);
    }

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
    {
        base.FromTreeAttributes(tree, worldForResolving);
        bucket = tree.GetItemstack("bucket");
        bucket?.ResolveBlockOrItem(worldForResolving);
        production = new ResinProduction(tree.GetDouble("resin"), tree.GetDouble("lastHours"));
        capacity = Math.Clamp(tree.GetInt("capacity", 8), 1, 32);
        mesh = null;
        if (Api?.Side == EnumAppSide.Client) MarkDirty(true);
    }

    public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
    {
        if (Api is not ICoreClientAPI capi) return true;
        if (mesh == null)
        {
            if (capi.World.GetBlock(new AssetLocation("game:woodbucket")) is not BlockBucket vanilla) return true;
            mesh = vanilla.GenMesh(capi, null, Pos);
            if (production.Amount > 0)
            {
                Shape shape = Shape.TryGet(capi, new AssetLocation("resingrove:shapes/block/resinsurface.json"));
                tesselator.TesselateShape(Block, shape, out MeshData surface);
                surface.Translate(0, 0.1f + 0.4f * (float)Math.Clamp(production.Amount / capacity, 0, 1), 0);
                mesh.AddMeshData(surface);
            }
            BlockFacing side = BlockFacing.FromCode(Block.Variant["side"]);
            mesh.Rotate(new Vec3f(0.5f, 0.5f, 0.5f), 0, BlockEntityTappedLog.FacingRotation(side), 0);
            // Move toward the trunk: the vanilla handle meets the protruding tap.
            mesh.Translate(-side.Normali.X * 0.18f, 0.05f, -side.Normali.Z * 0.18f);
        }
        mesher.AddMeshData(mesh);
        return true;
    }

    public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
    {
        dsc.AppendLine(Lang.Get("resingrove:bucket-contents", (int)Math.Floor(production.Amount + 1e-9), capacity));
        dsc.AppendLine(Lang.Get("resingrove:bucket-controls"));
    }
}
