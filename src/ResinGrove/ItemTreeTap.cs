using ResinGrove.Core;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace ResinGrove;

public sealed class ItemTreeTap : Item
{
    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel,
        EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
    {
        if (blockSel == null || slot.Itemstack == null || byEntity is not EntityPlayer entityPlayer) return;
        handling = EnumHandHandling.PreventDefault;
        IPlayer player = byEntity.World.PlayerByUid(entityPlayer.PlayerUID);
        if (api.Side != EnumAppSide.Server) return;
        IWorldAccessor world = byEntity.World;
        if (!world.Claims.TryAccess(player, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak)) return;
        if (blockSel.Face.Axis == EnumAxis.Y) { Interaction.Error(api, player, "tap-side-required"); return; }

        TreeInspection tree = TreeInspector.Inspect(new TreeWorld(world.BlockAccessor, blockSel.Position.dimension),
            TreeWorld.Position(blockSel.Position));
        if (!tree.Valid) { Interaction.Error(api, player, tree.Error); return; }
        BlockPos root = TreeWorld.Position(tree.Root, blockSel.Position.dimension);
        if (TapRegistry.IsReserved(world, root)) { Interaction.Error(api, player, "already-tapped"); return; }
        BlockPos bucketPos = blockSel.Position.AddCopy(blockSel.Face);
        if (!world.Claims.TryAccess(player, root, EnumBlockAccessFlags.BuildOrBreak)
            || !world.Claims.TryAccess(player, bucketPos, EnumBlockAccessFlags.BuildOrBreak)) return;
        if (world.BlockAccessor.GetBlock(bucketPos).Id != 0
            || world.BlockAccessor.GetBlock(bucketPos, BlockLayersAccess.Fluid).Id != 0)
        { Interaction.Error(api, player, "tap-space-required"); return; }

        Block original = world.BlockAccessor.GetBlock(blockSel.Position);
        if (original.Variant == null) return;
        Block? tapped = world.GetBlock(new AssetLocation("resingrove",
            $"tappedlog-{original.Variant["wood"]}-{original.Variant["rotation"]}-{blockSel.Face.Code}"));
        if (tapped == null) return;
        ItemStack tap = slot.Itemstack.Clone();
        tap.StackSize = 1;
        world.BlockAccessor.SetBlock(tapped.Id, blockSel.Position);
        if (world.BlockAccessor.GetBlockEntity(blockSel.Position) is not BlockEntityTappedLog be)
        {
            world.BlockAccessor.SetBlock(original.Id, blockSel.Position);
            return;
        }
        be.Install(root, tap);
        if (player.WorldData.CurrentGameMode != EnumGameMode.Creative) slot.TakeOut(1);
        slot.MarkDirty();
        world.PlaySoundAt(new AssetLocation("game:sounds/block/ingot"), byEntity, player);
    }
}

internal static class Interaction
{
    public static void Error(ICoreAPI api, IPlayer player, string code)
    {
        if (player is Vintagestory.API.Server.IServerPlayer serverPlayer)
            serverPlayer.SendIngameError("resingrove-" + code, Lang.Get("resingrove:" + code));
        else if (api is ICoreClientAPI capi)
            capi.TriggerIngameError(null, "resingrove-" + code, Lang.Get("resingrove:" + code));
    }

    public static void GiveOrDrop(IWorldAccessor world, IPlayer player, ItemStack stack, BlockPos pos)
    {
        if (!player.InventoryManager.TryGiveItemstack(stack)) world.SpawnItemEntity(stack, pos.ToVec3d().Add(0.5, 0.5, 0.5));
    }
}
