using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace ResinGrove;

public sealed class BlockHangingBucket : Block
{
    public BlockPos TapPosition(BlockPos pos) => pos.AddCopy(BlockFacing.FromCode(Variant["side"]).Opposite);

    public override void OnLoaded(ICoreAPI api)
    {
        base.OnLoaded(api);
        PlacedPriorityInteract = true;
    }

    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        if (world.Side == EnumAppSide.Client) return true;
        if (!world.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak)
            || !world.Claims.TryAccess(byPlayer, TapPosition(blockSel.Position), EnumBlockAccessFlags.BuildOrBreak)) return true;
        if (world.BlockAccessor.GetBlockEntity(blockSel.Position) is BlockEntityHangingBucket be)
            be.Interact(byPlayer, byPlayer.Entity.Controls.ShiftKey);
        return true;
    }

    public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos)
    {
        base.OnNeighbourBlockChange(world, pos, neibpos);
        if (world.Side == EnumAppSide.Server
            && world.BlockAccessor.GetBlockEntity(pos) is BlockEntityHangingBucket be) be.CheckSupport();
    }

    public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1) =>
        world.BlockAccessor.GetBlockEntity(pos) is BlockEntityHangingBucket be ? be.GetContents() : [];

    public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos) =>
        new(world.GetBlock(new AssetLocation("game:woodbucket")));

    public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack,
        BlockSelection blockSel, ref string failureCode)
    {
        failureCode = "resingrove:hang-on-tap";
        return false;
    }
}
