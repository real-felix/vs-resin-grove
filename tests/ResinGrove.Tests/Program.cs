using ResinGrove.Core;

var tests = new (string Name, Action Run)[]
{
    ("Pine and acacia resolve the same root from different logs", () =>
    {
        foreach (string wood in new[] { "pine", "acacia" })
        {
            var world = FakeTree.Alive(wood);
            var bottom = TreeInspector.Inspect(world, new(0, 1, 0));
            var top = TreeInspector.Inspect(world, new(0, 4, 0));
            Check(bottom.Valid && top.Valid && bottom.Root == top.Root && bottom.Root == new TreePosition(0, 1, 0));
        }
    }),
    ("Unsupported species and placed logs are rejected", () =>
    {
        var oak = FakeTree.Alive("oak");
        Check(!TreeInspector.Inspect(oak, new(0, 1, 0)).Valid);
        var placed = FakeTree.Alive();
        placed.Cells[new(0, 1, 0)] = new(TreeCellKind.Other);
        Check(!TreeInspector.Inspect(placed, new(0, 1, 0)).Valid);
        Check(!TreeInspector.Inspect(placed, new(0, 2, 0)).Valid);
    }),
    ("Floating trees, bare trunks, and stumps are rejected", () =>
    {
        var floating = FakeTree.Alive();
        floating.Cells.Remove(new(0, 0, 0));
        Check(!TreeInspector.Inspect(floating, new(0, 2, 0)).Valid);
        var bare = FakeTree.Alive();
        foreach (var pos in bare.Cells.Where(c => c.Value.Kind == TreeCellKind.Leaves).Select(c => c.Key).ToArray()) bare.Cells.Remove(pos);
        Check(!TreeInspector.Inspect(bare, new(0, 1, 0)).Valid);
        var stump = FakeTree.Alive();
        for (int y = 2; y <= 4; y++) stump.Cells.Remove(new(0, y, 0));
        Check(!TreeInspector.Inspect(stump, new(0, 1, 0)).Valid);
    }),
    ("A second tap is rejected even if the grounded root changes", () =>
    {
        var world = FakeTree.Alive();
        world.Cells[new(0, 2, 0)] = new(TreeCellKind.TappedLog, "pine");
        Check(TreeInspector.Inspect(world, new(0, 4, 0)).Error == "already-tapped");
        Check(TreeInspector.Inspect(world, new(0, 2, 0), true).Valid);
        world.Cells[new(-1, 1, 0)] = new(TreeCellKind.GrownLog, "pine");
        world.Cells[new(-1, 0, 0)] = new(TreeCellKind.Ground);
        Check(TreeInspector.Inspect(world, new(-1, 1, 0)).Error == "already-tapped");
    }),
    ("Diagonal acacia trunk steps connect to their root", () =>
    {
        var world = FakeTree.Alive("acacia");
        world.Cells[new(1, 5, 0)] = new(TreeCellKind.GrownLog, "acacia");
        world.Cells[new(2, 6, 0)] = new(TreeCellKind.GrownLog, "acacia");
        var result = TreeInspector.Inspect(world, new(2, 6, 0));
        Check(result.Valid && result.Root == new TreePosition(0, 1, 0));
    }),
    ("Upper logs above every diagonal trunk direction share one tap reservation", () =>
    {
        for (int dx = -1; dx <= 1; dx++)
        for (int dz = -1; dz <= 1; dz++)
        {
            if (dx == 0 && dz == 0) continue;
            var world = FakeTree.Alive("acacia");
            world.Cells.Remove(new(0, 4, 0));
            for (int y = 4; y <= 6; y++) world.Cells[new(dx, y, dz)] = new(TreeCellKind.GrownLog, "acacia");
            world.Cells[new(dx + 1, 6, dz)] = new(TreeCellKind.Leaves, "acacia");
            world.Cells[new(dx - 1, 6, dz)] = new(TreeCellKind.Leaves, "acacia");
            var above = TreeInspector.Inspect(world, new(dx, 6, dz));
            Check(above.Valid && above.Root == new TreePosition(0, 1, 0));
            world.Cells[new(0, 2, 0)] = new(TreeCellKind.TappedLog, "acacia");
            Check(TreeInspector.Inspect(world, new(dx, 6, dz)).Error == "already-tapped");
            Check(TreeInspector.Inspect(world, new(dx, 6, dz), true).Valid);
            world.Cells[new(0, 2, 0)] = new(TreeCellKind.GrownLog, "acacia");
            world.Cells[new(dx, 6, dz)] = new(TreeCellKind.TappedLog, "acacia");
            Check(TreeInspector.Inspect(world, new(0, 2, 0)).Error == "already-tapped");
        }
    }),
    ("Six acacias in a connected plantation can each receive one tap", () =>
    {
        var world = new FakeTree();
        for (int i = 0; i < 6; i++) world.AddTree(i * 4, "acacia");
        for (int i = 0; i < 5; i++)
        {
            world.Cells[new(i * 4 + 1, 5, 0)] = new(TreeCellKind.GrownLog, "acacia");
            world.Cells[new(i * 4 + 2, 6, 0)] = new(TreeCellKind.GrownLog, "acacia");
            world.Cells[new(i * 4 + 3, 5, 0)] = new(TreeCellKind.GrownLog, "acacia");
        }
        for (int i = 0; i < 6; i++)
        {
            var result = TreeInspector.Inspect(world, new(i * 4, 2, 0));
            Check(result.Valid && result.Root == new TreePosition(i * 4, 1, 0));
            world.Cells[new(i * 4, 2, 0)] = new(TreeCellKind.TappedLog, "acacia");
        }
    }),
    ("Touching acacia crowns do not merge separately grounded trunks", () =>
    {
        foreach (bool diagonal in new[] { false, true })
        {
            var world = FakeTree.Alive("acacia");
            world.AddTree(4, "acacia");
            world.Cells[new(1, 5, 0)] = new(TreeCellKind.GrownLog, "acacia");
            world.Cells[new(3, 5, 0)] = new(TreeCellKind.GrownLog, "acacia");
            world.Cells[new(2, diagonal ? 6 : 5, 0)] = new(TreeCellKind.GrownLog, "acacia");
            world.Cells[new(0, 2, 0)] = new(TreeCellKind.TappedLog, "acacia");
            var second = TreeInspector.Inspect(world, new(4, 2, 0));
            Check(second.Valid && second.Root == new TreePosition(4, 1, 0));
            world.Cells[new(4, 3, 0)] = new(TreeCellKind.TappedLog, "acacia");
            Check(TreeInspector.Inspect(world, new(4, 2, 0)).Error == "already-tapped");
            Check(TreeInspector.Inspect(world, new(0, 1, 0), true).Root == new TreePosition(0, 1, 0));
        }
    }),
    ("A straight trunk wins over a neighboring diagonal path", () =>
    {
        var world = FakeTree.Alive("acacia");
        world.AddTree(2, "acacia");
        world.Cells[new(1, 3, 0)] = new(TreeCellKind.GrownLog, "acacia");
        world.Cells[new(2, 2, 0)] = new(TreeCellKind.TappedLog, "acacia");
        var result = TreeInspector.Inspect(world, new(0, 4, 0));
        Check(result.Valid && result.Root == new TreePosition(0, 1, 0));
    }),
    ("A diagonal junction leading to different roots is refused", () =>
    {
        var world = FakeTree.Alive("acacia");
        world.AddTree(4, "acacia");
        world.Cells[new(1, 5, 0)] = new(TreeCellKind.GrownLog, "acacia");
        world.Cells[new(3, 5, 0)] = new(TreeCellKind.GrownLog, "acacia");
        world.Cells[new(2, 6, 0)] = new(TreeCellKind.GrownLog, "acacia");
        Check(TreeInspector.FindRoot(world, new(2, 6, 0)).Error == "tree-ambiguous-root");
        Check(TreeInspector.Inspect(world, new(0, 2, 0)).Valid);
        Check(TreeInspector.Inspect(world, new(4, 2, 0)).Valid);
    }),
    ("Diagonal paths reconverging on the same root are accepted", () =>
    {
        var world = FakeTree.Alive("acacia");
        world.Cells.Remove(new(0, 4, 0));
        world.Cells[new(-1, 4, 0)] = new(TreeCellKind.GrownLog, "acacia");
        world.Cells[new(1, 4, 0)] = new(TreeCellKind.GrownLog, "acacia");
        world.Cells[new(0, 5, 0)] = new(TreeCellKind.GrownLog, "acacia");
        world.Cells[new(0, 6, 0)] = new(TreeCellKind.Leaves, "acacia");
        world.Cells[new(1, 6, 0)] = new(TreeCellKind.Leaves, "acacia");
        Check(TreeInspector.Inspect(world, new(0, 5, 0)).Valid);
    }),
    ("Wide bases have one owner while diagonally adjacent separate bases stay independent", () =>
    {
        var wide = FakeTree.Alive("pine");
        wide.AddTree(1, "pine");
        wide.Cells[new(0, 2, 0)] = new(TreeCellKind.TappedLog, "pine");
        Check(TreeInspector.FindRoot(wide, new(1, 4, 0)).Root == new TreePosition(0, 1, 0));
        Check(TreeInspector.Inspect(wide, new(1, 3, 0)).Error == "already-tapped");

        var diagonal = FakeTree.Alive("pine");
        var second = FakeTree.Alive("pine");
        foreach (var pair in second.Cells) diagonal.Cells[pair.Key.Offset(1, 0, 1)] = pair.Value;
        diagonal.Cells[new(0, 2, 0)] = new(TreeCellKind.TappedLog, "pine");
        var other = TreeInspector.Inspect(diagonal, new(1, 3, 1));
        Check(other.Valid && other.Root == new TreePosition(1, 1, 1));
    }),
    ("A diagonal trunk on stepped terrain retains its lowest base", () =>
    {
        var world = new FakeTree();
        world.Cells[new(0, 0, 0)] = new(TreeCellKind.Ground);
        world.Cells[new(0, 1, 0)] = new(TreeCellKind.GrownLog, "acacia");
        world.Cells[new(1, 1, 1)] = new(TreeCellKind.Ground);
        for (int y = 2; y <= 5; y++) world.Cells[new(1, y, 1)] = new(TreeCellKind.GrownLog, "acacia");
        world.Cells[new(0, 5, 1)] = new(TreeCellKind.Leaves, "acacia");
        world.Cells[new(2, 5, 1)] = new(TreeCellKind.Leaves, "acacia");
        var result = TreeInspector.Inspect(world, new(1, 4, 1));
        Check(result.Valid && result.Root == new TreePosition(0, 1, 0));
    }),
    ("Horizontal branches cannot borrow a ground path from the canopy", () =>
    {
        var world = FakeTree.Alive("acacia");
        world.Cells[new(1, 4, 0)] = new(TreeCellKind.GrownLog, "acacia");
        world.Cells[new(2, 4, 0)] = new(TreeCellKind.GrownLog, "acacia");
        Check(!TreeInspector.Inspect(world, new(2, 4, 0)).Valid);
    }),
    ("A neighboring tree cannot supply a stump's missing trunk height", () =>
    {
        var world = FakeTree.Alive("acacia");
        world.AddTree(3, "acacia");
        for (int y = 2; y <= 4; y++) world.Cells.Remove(new(0, y, 0));
        Check(TreeInspector.Inspect(world, new(0, 1, 0)).Error == "tree-foliage-required");
    }),
    ("Leaves do not connect two separate trees", () =>
    {
        var world = FakeTree.Alive();
        for (int y = 1; y <= 4; y++) world.Cells[new(3, y, 0)] = new(TreeCellKind.GrownLog, "pine");
        world.Cells[new(3, 0, 0)] = new(TreeCellKind.Ground);
        world.Cells[new(3, 2, 0)] = new(TreeCellKind.TappedLog, "pine");
        world.Cells[new(2, 4, 0)] = new(TreeCellKind.Leaves, "pine");
        Check(TreeInspector.Inspect(world, new(0, 1, 0)).Valid);
    }),
    ("Unloaded boundaries fail closed", () =>
    {
        var world = FakeTree.Alive();
        world.Cells[new(1, 3, 0)] = new(TreeCellKind.Unloaded);
        Check(TreeInspector.Inspect(world, new(0, 1, 0)).Error == "tree-unloaded");
    }),
    ("Huge grounded bases stop at a fixed work limit", () =>
    {
        var world = new FakeTree();
        for (int x = 0; x < 25; x++)
        for (int z = 0; z < 25; z++)
        {
            world.Cells[new(x, 0, z)] = new(TreeCellKind.Ground);
            world.Cells[new(x, 1, z)] = new(TreeCellKind.GrownLog, "pine");
        }
        Check(TreeInspector.Inspect(world, new(0, 1, 0)).Error == "tree-too-large");
        Check(world.Reads < 16000);
    }),
    ("Horizontal canopy networks do not force a whole-forest traversal", () =>
    {
        var world = FakeTree.Alive();
        for (int x = 1; x < 100; x++) world.Cells[new(x, 4, 0)] = new(TreeCellKind.GrownLog, "pine");
        world.Cells[new(0, 5, 0)] = new(TreeCellKind.Leaves, "pine");
        world.Cells[new(1, 5, 0)] = new(TreeCellKind.Leaves, "pine");
        Check(TreeInspector.Inspect(world, new(0, 1, 0)).Valid);
        Check(world.Reads < 1000);
    }),
    ("Deep and widely sloping trunks stop at the spatial limits", () =>
    {
        var tall = FakeTree.Alive();
        for (int y = 5; y <= 80; y++) tall.Cells[new(0, y, 0)] = new(TreeCellKind.GrownLog, "pine");
        Check(TreeInspector.FindRoot(tall, new(0, 80, 0)).Error == "tree-too-large");
        var sloped = new FakeTree();
        sloped.Cells[new(0, 0, 0)] = new(TreeCellKind.Ground);
        for (int y = 1; y <= 40; y++) sloped.Cells[new(y - 1, y, 0)] = new(TreeCellKind.GrownLog, "acacia");
        Check(TreeInspector.FindRoot(sloped, new(39, 40, 0)).Error == "tree-too-large");
    }),
    ("Fractional resin survives collection and save/restore", () =>
    {
        var fill = new ResinProduction(0, 10);
        fill.Advance(46, 24, 8, true);
        Check(fill.Collect() == 1);
        Near(fill.Amount, 0.5);
        var restored = new ResinProduction(fill.Amount, fill.LastHours);
        restored.Advance(58, 24, 8, true);
        Check(restored.Collect() == 1 && restored.Collect() == 0);
    }),
    ("Full buckets cap offline catch-up and never bank overflow", () =>
    {
        var fill = new ResinProduction(0, 0);
        fill.Advance(2400, 24, 8, true);
        Check(fill.Collect() == 8);
        fill.Advance(2400, 24, 8, true);
        Check(fill.Collect() == 0);
        fill.Advance(2424, 24, 8, true);
        Check(fill.Collect() == 1);
    }),
    ("Dead or unloaded trees cannot bank elapsed time", () =>
    {
        var fill = new ResinProduction(0, 0);
        fill.Advance(240, 24, 8, false);
        fill.Advance(264, 24, 8, true);
        Check(fill.Collect() == 1);
    }),
    ("Calendar rewind produces no negative resin", () =>
    {
        var fill = new ResinProduction(0.5, 100);
        fill.Advance(50, 24, 8, true);
        Near(fill.Amount, 0.5);
        fill.Advance(62, 24, 8, true);
        Check(fill.Collect() == 1);
    }),
    ("Invalid saved values cannot poison production", () =>
    {
        var fill = new ResinProduction(double.NaN, double.NaN);
        fill.Advance(24, 24, 8, true);
        Check(fill.Collect() == 1);
        fill.Advance(double.NaN, 24, 8, true);
        Near(fill.Amount, 0);
    })
};

int failed = 0;
foreach (var test in tests)
{
    try { test.Run(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception e) { failed++; Console.Error.WriteLine($"FAIL {test.Name}: {e.Message}"); }
}
Console.WriteLine($"{tests.Length - failed}/{tests.Length} checks passed");
return failed == 0 ? 0 : 1;

static void Check(bool value) { if (!value) throw new Exception("Assertion failed"); }
static void Near(double value, double expected) => Check(Math.Abs(value - expected) < 1e-8);

sealed class FakeTree : ITreeWorld
{
    public Dictionary<TreePosition, TreeCell> Cells { get; } = new();
    public int Reads { get; private set; }
    public TreeCell Read(TreePosition position) { Reads++; return Cells.GetValueOrDefault(position); }

    public static FakeTree Alive(string wood = "pine")
    {
        var world = new FakeTree();
        world.AddTree(0, wood);
        return world;
    }

    public void AddTree(int x, string wood)
    {
        Cells[new(x, 0, 0)] = new(TreeCellKind.Ground);
        for (int y = 1; y <= 4; y++) Cells[new(x, y, 0)] = new(TreeCellKind.GrownLog, wood);
        Cells[new(x + 1, 4, 0)] = new(TreeCellKind.Leaves, wood);
        Cells[new(x - 1, 4, 0)] = new(TreeCellKind.Leaves, wood);
    }
}
