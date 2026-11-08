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
    ("Diagonal acacia branches connect to their trunk", () =>
    {
        var world = FakeTree.Alive("acacia");
        world.Cells[new(1, 5, 0)] = new(TreeCellKind.GrownLog, "acacia");
        world.Cells[new(2, 6, 0)] = new(TreeCellKind.GrownLog, "acacia");
        var result = TreeInspector.Inspect(world, new(2, 6, 0));
        Check(result.Valid && result.Root == new TreePosition(0, 1, 0));
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
    ("Huge connected wood graphs stop at a fixed work limit", () =>
    {
        var world = new FakeTree();
        for (int x = 0; x < 10; x++)
        for (int y = 1; y < 10; y++)
        for (int z = 0; z < 10; z++) world.Cells[new(x, y, z)] = new(TreeCellKind.GrownLog, "pine");
        Check(TreeInspector.Inspect(world, new(0, 1, 0)).Error == "tree-too-large");
        Check(world.Reads < 16000);
    }),
    ("Long trees stop at the spatial limit", () =>
    {
        var world = FakeTree.Alive();
        for (int x = 1; x < 100; x++) world.Cells[new(x, 4, 0)] = new(TreeCellKind.GrownLog, "pine");
        Check(TreeInspector.Inspect(world, new(0, 1, 0)).Error == "tree-too-large");
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
        world.Cells[new(0, 0, 0)] = new(TreeCellKind.Ground);
        for (int y = 1; y <= 4; y++) world.Cells[new(0, y, 0)] = new(TreeCellKind.GrownLog, wood);
        world.Cells[new(1, 4, 0)] = new(TreeCellKind.Leaves, wood);
        world.Cells[new(-1, 4, 0)] = new(TreeCellKind.Leaves, wood);
        return world;
    }
}
