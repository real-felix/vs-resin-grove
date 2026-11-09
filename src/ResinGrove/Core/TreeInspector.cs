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

/// <summary>Root ownership follows logs downward, never across a crown into another tree.</summary>
public static class TreeInspector
{
    public const int MaxLogs = 512;
    public const int MaxHorizontalRange = 32;
    public const int MaxVerticalRange = 64;

    public static TreeInspection Inspect(ITreeWorld world, TreePosition start, bool allowExistingTap = false) =>
        Run(world, start, search => search.Inspect(allowExistingTap));

    /// <summary>Resolve ownership without requiring foliage, including for old saved taps.</summary>
    public static TreeInspection FindRoot(ITreeWorld world, TreePosition start) =>
        Run(world, start, search => search.FindRoot(start));

    private static TreeInspection Run(ITreeWorld world, TreePosition start, Func<Search, TreeInspection> action)
    {
        TreeCell initial = world.Read(start);
        if (initial.Kind is not (TreeCellKind.GrownLog or TreeCellKind.TappedLog)
            || initial.Wood is not ("pine" or "acacia")) return TreeInspection.Reject("tree-required");
        try { return action(new Search(world, start, initial.Wood)); }
        catch (SearchStopped stopped) { return TreeInspection.Reject(stopped.Message); }
    }

    private sealed class SearchStopped(string error) : Exception(error);

    private sealed class Search(ITreeWorld world, TreePosition start, string wood)
    {
        private readonly Dictionary<TreePosition, TreeCell> cells = new();
        private readonly Dictionary<TreePosition, TreeInspection> roots = new();
        private readonly Dictionary<TreePosition, List<TreePosition>> bases = new();
        private int logsRead;

        private TreeCell Read(TreePosition pos)
        {
            if (cells.TryGetValue(pos, out TreeCell cell)) return cell;
            cell = world.Read(pos);
            if (cell.Kind == TreeCellKind.Unloaded) throw new SearchStopped("tree-unloaded");
            cells[pos] = cell;
            if (IsWood(cell) && (++logsRead > MaxLogs
                || Math.Abs(pos.X - start.X) > MaxHorizontalRange
                || Math.Abs(pos.Z - start.Z) > MaxHorizontalRange
                || Math.Abs(pos.Y - start.Y) > MaxVerticalRange))
                throw new SearchStopped("tree-too-large");
            return cell;
        }

        private bool IsWood(TreeCell cell) => cell.Wood == wood
            && cell.Kind is TreeCellKind.GrownLog or TreeCellKind.TappedLog;

        public TreeInspection FindRoot(TreePosition pos)
        {
            if (roots.TryGetValue(pos, out TreeInspection? cached)) return cached;
            if (!IsWood(Read(pos))) return TreeInspection.Reject("tree-ground-required");
            TreePosition below = pos.Offset(0, -1, 0);
            TreeCell support = Read(below);

            // A vertical continuation is stronger evidence than a nearby diagonal branch.
            if (IsWood(support)) return roots[pos] = FindRoot(below);
            if (support.Kind == TreeCellKind.Ground) return roots[pos] = FindBase(pos);

            TreePosition? root = null;
            for (int x = -1; x <= 1; x++)
            for (int z = -1; z <= 1; z++)
            {
                if (x == 0 && z == 0) continue;
                TreePosition next = below.Offset(x, 0, z);
                if (!IsWood(Read(next))) continue;
                TreeInspection candidate = FindRoot(next);
                if (candidate.Error == "tree-ambiguous-root") return roots[pos] = candidate;
                if (!candidate.Valid) continue;
                if (root != null && root.Value != candidate.Root)
                    return roots[pos] = TreeInspection.Reject("tree-ambiguous-root");
                root = candidate.Root;
            }
            return roots[pos] = root is TreePosition found
                ? new TreeInspection(true, "", found) : TreeInspection.Reject("tree-ground-required");
        }

        private TreeInspection FindBase(TreePosition first)
        {
            // Join a wide or stepped grounded base, not branches higher up the tree.
            var group = new List<TreePosition>();
            var queue = new Queue<TreePosition>();
            var seen = new HashSet<TreePosition> { first };
            queue.Enqueue(first);
            while (queue.TryDequeue(out TreePosition pos))
            {
                group.Add(pos);
                for (int x = -1; x <= 1; x++)
                for (int y = -1; y <= 1; y++)
                for (int z = -1; z <= 1; z++)
                {
                    if (x == 0 && z == 0) continue;
                    // Same-height bases join at faces; sloping bases may join diagonally.
                    if (y == 0 && Math.Abs(x) + Math.Abs(z) != 1) continue;
                    TreePosition next = pos.Offset(x, y, z);
                    if (!seen.Add(next) || !IsWood(Read(next))) continue;
                    if (Read(next.Offset(0, -1, 0)).Kind == TreeCellKind.Ground) queue.Enqueue(next);
                }
            }
            TreePosition root = group.OrderBy(p => p.Y).ThenBy(p => p.X).ThenBy(p => p.Z).First();
            var result = new TreeInspection(true, "", root);
            foreach (TreePosition pos in group) roots[pos] = result;
            bases[root] = group;
            return result;
        }

        public TreeInspection Inspect(bool allowExistingTap)
        {
            TreeInspection ownership = FindRoot(start);
            if (!ownership.Valid) return ownership;
            TreePosition root = ownership.Root;
            var queue = new Queue<TreePosition>(bases[root]);
            var seen = new HashSet<TreePosition>(bases[root]);
            var owned = new HashSet<TreePosition>();
            var foliage = new HashSet<TreePosition>();
            int highest = root.Y;
            bool tapped = false;

            while (queue.TryDequeue(out TreePosition pos))
            {
                owned.Add(pos);
                highest = Math.Max(highest, pos.Y);
                tapped |= Read(pos).Kind == TreeCellKind.TappedLog;

                // Sample nearby foliage without following it into neighboring crowns.
                for (int x = -2; x <= 2; x++)
                for (int y = -1; y <= 1; y++)
                for (int z = -2; z <= 2; z++)
                {
                    TreePosition leaf = pos.Offset(x, y, z);
                    if (leaf.Y < root.Y + 2) continue;
                    TreeCell cell = Read(leaf);
                    if (cell.Kind == TreeCellKind.Leaves && cell.Wood == wood) foliage.Add(leaf);
                }

                for (int x = -1; x <= 1; x++)
                for (int z = -1; z <= 1; z++)
                {
                    TreePosition next = pos.Offset(x, 1, z);
                    if (!seen.Add(next) || !IsWood(Read(next))) continue;
                    TreeInspection candidate = FindRoot(next);
                    if (candidate.Valid && candidate.Root == root) queue.Enqueue(next);
                }
            }

            if (!owned.Contains(start)) return TreeInspection.Reject("tree-trunk-required");
            if (tapped && !allowExistingTap) return TreeInspection.Reject("already-tapped");
            if (owned.Count < 3 || highest < root.Y + 2 || foliage.Count < 2)
                return TreeInspection.Reject("tree-foliage-required");
            return ownership;
        }
    }
}
