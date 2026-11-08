namespace ResinGrove.Core;

public readonly record struct TreePosition(int X, int Y, int Z)
{
    public TreePosition Offset(int x, int y, int z) => new(X + x, Y + y, Z + z);
}

public enum TreeCellKind { Other, Unloaded, Ground, GrownLog, TappedLog, Leaves }
public readonly record struct TreeCell(TreeCellKind Kind, string? Wood = null);

public interface ITreeWorld
{
    TreeCell Read(TreePosition position);
}

public sealed record TreeInspection(bool Valid, string Error, TreePosition Root)
{
    public static TreeInspection Reject(string error) => new(false, error, default);
}

/// <summary>Bounded, iterative wood-only traversal; foliage is sampled locally, never traversed.</summary>
public static class TreeInspector
{
    public const int MaxLogs = 512;
    public const int MaxHorizontalRange = 32;
    public const int MaxVerticalRange = 64;

    public static TreeInspection Inspect(ITreeWorld world, TreePosition start, bool allowExistingTap = false)
    {
        TreeCell initial = world.Read(start);
        if (initial.Kind is not (TreeCellKind.GrownLog or TreeCellKind.TappedLog)
            || initial.Wood is not ("pine" or "acacia")) return TreeInspection.Reject("tree-required");

        string wood = initial.Wood;
        var queue = new Queue<TreePosition>();
        var visited = new HashSet<TreePosition> { start };
        var logs = new List<TreePosition>();
        var roots = new List<TreePosition>();
        var foliage = new HashSet<TreePosition>();
        queue.Enqueue(start);
        bool tapped = false;
        int highest = start.Y;

        while (queue.TryDequeue(out TreePosition pos))
        {
            TreeCell cell = world.Read(pos);
            tapped |= cell.Kind == TreeCellKind.TappedLog;
            logs.Add(pos);
            highest = Math.Max(highest, pos.Y);
            if (logs.Count > MaxLogs) return TreeInspection.Reject("tree-too-large");

            if (world.Read(pos.Offset(0, -1, 0)).Kind == TreeCellKind.Ground) roots.Add(pos);

            // Diagonal wood links support acacia branches and wide trunks.
            for (int x = -1; x <= 1; x++)
            for (int y = -1; y <= 1; y++)
            for (int z = -1; z <= 1; z++)
            {
                if (x == 0 && y == 0 && z == 0) continue;
                TreePosition next = pos.Offset(x, y, z);
                if (!visited.Add(next)) continue;
                TreeCell neighbor = world.Read(next);
                if (neighbor.Kind == TreeCellKind.Unloaded) return TreeInspection.Reject("tree-unloaded");
                if (neighbor.Wood != wood) continue;
                if (neighbor.Kind == TreeCellKind.Leaves) foliage.Add(next);
                if (neighbor.Kind is not (TreeCellKind.GrownLog or TreeCellKind.TappedLog)) continue;
                if (Math.Abs(next.X - start.X) > MaxHorizontalRange
                    || Math.Abs(next.Z - start.Z) > MaxHorizontalRange
                    || Math.Abs(next.Y - start.Y) > MaxVerticalRange)
                    return TreeInspection.Reject("tree-too-large");
                queue.Enqueue(next);
            }
        }

        if (tapped && !allowExistingTap) return TreeInspection.Reject("already-tapped");
        if (roots.Count == 0) return TreeInspection.Reject("tree-ground-required");
        TreePosition root = roots.OrderBy(p => p.Y).ThenBy(p => p.X).ThenBy(p => p.Z).First();
        if (logs.Count < 3 || highest < root.Y + 2 || foliage.Count(p => p.Y >= root.Y + 2) < 2)
            return TreeInspection.Reject("tree-foliage-required");
        return new TreeInspection(true, "", root);
    }
}
