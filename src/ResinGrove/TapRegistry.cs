using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace ResinGrove;

/// <summary>The bottom log's chunk owns the reservation. Unloading the tap never releases it.</summary>
internal static class TapRegistry
{
    private static string Key(BlockPos root) => $"resingrove:tap:{root.dimension}:{root.X}:{root.Y}:{root.Z}";

    public static bool IsReserved(IWorldAccessor world, BlockPos root)
    {
        var chunk = world.BlockAccessor.GetChunkAtBlockPos(root);
        if (chunk == null) return true;
        byte[]? data = chunk.GetModdata(Key(root));
        if (data == null || data.Length == 0) return false;
        BlockPos? tap = Decode(data);
        if (tap == null) return true;
        // Never clear a reservation based on an unloaded tap.
        if (world.BlockAccessor.GetChunkAtBlockPos(tap) == null) return true;
        if (world.BlockAccessor.GetBlock(tap) is BlockTappedLog)
        {
            if (world.BlockAccessor.GetBlockEntity(tap) is not BlockEntityTappedLog be) return true;
            // A legacy canopy-wide reservation may point at a different trunk.
            if (!be.RefreshRoot() || be.Root == null || be.Root.Equals(root)) return true;
        }
        Release(world, root, tap);
        return false;
    }

    private static BlockPos? Decode(byte[] data)
    {
        string[] coords = Encoding.UTF8.GetString(data).Split(',');
        return coords.Length == 4 && int.TryParse(coords[0], out int x) && int.TryParse(coords[1], out int y)
            && int.TryParse(coords[2], out int z) && int.TryParse(coords[3], out int dim)
            ? new BlockPos(x, y, z, dim) : null;
    }

    public static bool TryMove(IWorldAccessor world, BlockPos? previous, BlockPos root, BlockPos tap)
    {
        var chunk = world.BlockAccessor.GetChunkAtBlockPos(root);
        if (chunk == null) return false;
        byte[]? data = chunk.GetModdata(Key(root));
        bool alreadyOwned = false;
        if (data != null)
        {
            BlockPos? owner = Decode(data);
            if (owner == null) return false;
            alreadyOwned = owner.Equals(tap);
            if (!owner.Equals(tap))
            {
                // Preserve any live or unloaded competing reservation; never overwrite it.
                if (world.BlockAccessor.GetChunkAtBlockPos(owner) == null
                    || world.BlockAccessor.GetBlock(owner) is BlockTappedLog) return false;
            }
        }
        if (!alreadyOwned) Reserve(world, root, tap);
        if (previous != null && !previous.Equals(root)) Release(world, previous, tap);
        return true;
    }

    public static void Reserve(IWorldAccessor world, BlockPos root, BlockPos tap)
    {
        var chunk = world.BlockAccessor.GetChunkAtBlockPos(root);
        if (chunk == null) return;
        chunk.SetModdata(Key(root), Encoding.UTF8.GetBytes($"{tap.X},{tap.Y},{tap.Z},{tap.dimension}"));
        chunk.MarkModified();
    }

    public static void Release(IWorldAccessor world, BlockPos root, BlockPos tap)
    {
        var chunk = world.BlockAccessor.GetChunkAtBlockPos(root);
        if (chunk == null) return;
        byte[]? data = chunk.GetModdata(Key(root));
        if (data == null || Decode(data)?.Equals(tap) != true) return;
        chunk.RemoveModdata(Key(root));
        chunk.MarkModified();
    }
}
