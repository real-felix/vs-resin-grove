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
        string[] coords = Encoding.UTF8.GetString(data).Split(',');
        if (coords.Length != 4 || !int.TryParse(coords[0], out int x) || !int.TryParse(coords[1], out int y)
            || !int.TryParse(coords[2], out int z) || !int.TryParse(coords[3], out int dim)) return true;
        var tap = new BlockPos(x, y, z, dim);
        // Never clear a reservation based on an unloaded tap.
        if (world.BlockAccessor.GetChunkAtBlockPos(tap) == null) return true;
        if (world.BlockAccessor.GetBlock(tap) is BlockTappedLog) return true;
        chunk.RemoveModdata(Key(root));
        chunk.MarkModified();
        return false;
    }

    public static void Reserve(IWorldAccessor world, BlockPos root, BlockPos tap)
    {
        var chunk = world.BlockAccessor.GetChunkAtBlockPos(root);
        if (chunk == null) return;
        chunk.SetModdata(Key(root), Encoding.UTF8.GetBytes($"{tap.X},{tap.Y},{tap.Z},{tap.dimension}"));
        chunk.MarkModified();
    }

    public static void Release(IWorldAccessor world, BlockPos root)
    {
        var chunk = world.BlockAccessor.GetChunkAtBlockPos(root);
        if (chunk == null) return;
        chunk.RemoveModdata(Key(root));
        chunk.MarkModified();
    }
}
