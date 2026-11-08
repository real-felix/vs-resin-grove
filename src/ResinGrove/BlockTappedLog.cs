using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace ResinGrove;

public sealed class BlockTappedLog : BlockLog
{
    public BlockFacing TapFace => BlockFacing.FromCode(Variant["side"]);

    public override void OnLoaded(ICoreAPI api)
    {
        base.OnLoaded(api);
        PlacedPriorityInteract = true;
    }

    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        if (world.Side == EnumAppSide.Client) return true;
        if (!world.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak)) return true;
        ItemSlot slot = byPlayer.InventoryManager.ActiveHotbarSlot;
        if (slot.Itemstack?.Block is not BlockBucket bucket || bucket.Code != new AssetLocation("game:woodbucket"))
        { Interaction.Error(api, byPlayer, "empty-bucket-required"); return true; }
        if (!bucket.IsEmpty(slot.Itemstack))
        { Interaction.Error(api, byPlayer, "empty-bucket-required"); return true; }

        BlockPos target = blockSel.Position.AddCopy(TapFace);
        if (!world.Claims.TryAccess(byPlayer, target, EnumBlockAccessFlags.BuildOrBreak)) return true;
        if (world.BlockAccessor.GetChunkAtBlockPos(target) == null
            || world.BlockAccessor.GetBlock(target).Id != 0
            || world.BlockAccessor.GetBlock(target, BlockLayersAccess.Fluid).Id != 0)
        { Interaction.Error(api, byPlayer, "tap-space-required"); return true; }
        Block? hanging = world.GetBlock(new AssetLocation("resingrove", "hangingbucket-" + TapFace.Code));
        if (hanging == null) return true;
        ItemStack stored = slot.Itemstack.Clone();
        stored.StackSize = 1;
        world.BlockAccessor.SetBlock(hanging.Id, target);
        if (world.BlockAccessor.GetBlockEntity(target) is not BlockEntityHangingBucket be)
        { world.BlockAccessor.SetBlock(0, target); return true; }
        be.Attach(stored);
        if (byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative) slot.TakeOut(1);
        slot.MarkDirty();
        return true;
    }

    public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
    {
        var drops = new List<ItemStack> { OnPickBlock(world, pos) };
        if (world.BlockAccessor.GetBlockEntity(pos) is BlockEntityTappedLog be && be.Tap != null)
            drops.Add(be.Tap.Clone());
        return drops.ToArray();
    }

    // Do not allow creative pick-block to produce placeable natural/tapped logs.
    public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos) =>
        new(world.GetBlock(new AssetLocation("game", $"log-placed-{Variant["wood"]}-ud")));

    public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack,
        BlockSelection blockSel, ref string failureCode)
    {
        failureCode = "resingrove:use-tap";
        return false;
    }

    public override void OnBlockRemoved(IWorldAccessor world, BlockPos pos)
    {
        if (world.Side == EnumAppSide.Server)
        {
            if (world.BlockAccessor.GetBlockEntity(pos) is BlockEntityTappedLog be && be.Root != null)
                TapRegistry.Release(world, be.Root);
            BlockPos bucketPos = pos.AddCopy(TapFace);
            if (world.BlockAccessor.GetBlockEntity(bucketPos) is BlockEntityHangingBucket bucket
                && bucket.TapPosition.Equals(pos)) bucket.DropAndRemove();
        }
        base.OnBlockRemoved(world, pos);
    }
}
