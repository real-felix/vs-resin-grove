using ResinGrove.Core;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace ResinGrove;

public sealed class TreeWorld(IBlockAccessor accessor, int dimension) : ITreeWorld
{
    public static TreePosition Position(BlockPos pos) => new(pos.X, pos.Y, pos.Z);
    public static BlockPos Position(TreePosition pos, int dimension) => new(pos.X, pos.Y, pos.Z, dimension);

    public TreeCell Read(TreePosition position)
    {
        BlockPos pos = Position(position, dimension);
        if (!accessor.IsValidPos(pos)) return new(TreeCellKind.Other);
        if (accessor.GetChunkAtBlockPos(pos) == null) return new(TreeCellKind.Unloaded);
        Block block = accessor.GetBlock(pos);
        string? wood = block.Variant?["wood"];
        if (block is BlockTappedLog) return new(TreeCellKind.TappedLog, wood);
        if (block.Code?.Domain == "game" && block.Code.Path.StartsWith("log-grown-", StringComparison.Ordinal))
            return new(TreeCellKind.GrownLog, wood);
        if (block.BlockMaterial == EnumBlockMaterial.Leaves) return new(TreeCellKind.Leaves, wood);
        if (block.SideSolid[BlockFacing.UP.Index] && block.BlockMaterial is
            EnumBlockMaterial.Soil or EnumBlockMaterial.Stone or EnumBlockMaterial.Sand or EnumBlockMaterial.Gravel)
            return new(TreeCellKind.Ground);
        return new(TreeCellKind.Other);
    }
}
