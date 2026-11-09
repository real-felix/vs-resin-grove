using System.Text;
using ResinGrove.Core;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace ResinGrove;

public sealed class BlockEntityTappedLog : BlockEntity
{
    public BlockPos? Root { get; private set; }
    public ItemStack? Tap { get; private set; }
    private MeshData? mesh;

    public override void Initialize(ICoreAPI api)
    {
        base.Initialize(api);
        if (api.Side == EnumAppSide.Server)
        {
            if (Tap != null)
                api.Event.EnqueueMainThreadTask(() =>
                {
                    if (api.World.BlockAccessor.GetBlockEntity(Pos) == this) RefreshRoot();
                }, "resingrove-migrate-root");
            // Adjacent chunks may not be ready during Initialize; retry after loading.
            RegisterGameTickListener(_ => RefreshRoot(), 30000);
        }
    }

    public bool RefreshRoot()
    {
        if (Api?.Side != EnumAppSide.Server || Tap == null) return false;
        TreeInspection result = TreeInspector.FindRoot(new TreeWorld(Api.World.BlockAccessor, Pos.dimension),
            TreeWorld.Position(Pos));
        if (!result.Valid) return false;
        BlockPos root = TreeWorld.Position(result.Root, Pos.dimension);
        if (!TapRegistry.TryMove(Api.World, Root, root, Pos)) return false;
        if (Root?.Equals(root) != true)
        {
            Root = root;
            MarkDirty();
        }
        return true;
    }

    public void Install(BlockPos root, ItemStack tap)
    {
        Root = root.Copy();
        Tap = tap;
        TapRegistry.Reserve(Api.World, Root, Pos);
        MarkDirty(true);
    }

    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);
        if (Root != null)
        {
            tree.SetInt("rootX", Root.X);
            tree.SetInt("rootY", Root.Y);
            tree.SetInt("rootZ", Root.Z);
            tree.SetInt("rootDimension", Root.dimension);
            tree.SetBool("hasRoot", true);
        }
        tree.SetItemstack("tap", Tap);
    }

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
    {
        base.FromTreeAttributes(tree, worldForResolving);
        Root = tree.GetBool("hasRoot") ? new BlockPos(tree.GetInt("rootX"), tree.GetInt("rootY"),
            tree.GetInt("rootZ"), tree.GetInt("rootDimension")) : null;
        Tap = tree.GetItemstack("tap");
        Tap?.ResolveBlockOrItem(worldForResolving);
        mesh = null;
        if (Api?.Side == EnumAppSide.Client) MarkDirty(true);
    }

    public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
    {
        if (Tap?.Item != null)
        {
            if (mesh == null)
            {
                // Terrain meshes use the block atlas, even though the tap is also a held item.
                var source = new TapTextureSource(tesselator.GetTextureSource(Block), Tap.Item.Variant["metal"]);
                tesselator.TesselateItem(Tap.Item, out mesh, source);
                mesh.Rotate(new Vec3f(0.5f, 0.5f, 0.5f), 0,
                    FacingRotation(((BlockTappedLog)Block).TapFace), 0);
            }
            mesher.AddMeshData(mesh);
        }
        return false; // Keep the log's ordinary cube mesh.
    }

    internal static float FacingRotation(BlockFacing facing) => facing.Code switch
    {
        "east" => -GameMath.PIHALF,
        "south" => GameMath.PI,
        "west" => GameMath.PIHALF,
        _ => 0
    };

    public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc) =>
        dsc.AppendLine(Lang.Get("resingrove:tap-info"));

    private sealed class TapTextureSource(ITexPositionSource blockSource, string metal) : ITexPositionSource
    {
        public Size2i AtlasSize => blockSource.AtlasSize
            ?? throw new InvalidOperationException("Block texture atlas has no size.");
        public TextureAtlasPosition this[string textureCode] => blockSource["tap-" + metal]
            ?? throw new InvalidOperationException("Missing block-atlas texture for tree tap: " + metal);
    }
}
