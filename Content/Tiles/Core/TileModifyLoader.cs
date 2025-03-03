using CalamityOverhaul.Common;
using System.Collections.Generic;
using System.Reflection;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Tiles.Core
{
    internal class TileModifyLoader : ICWRLoader
    {
        public delegate bool OnRightClickDelegate(int i, int j);
        public static List<TileOverride> TileOverrides { get; private set; } = [];
        public static Dictionary<int, TileOverride> TileOverrideDic { get; private set; } = [];
        void ICWRLoader.LoadData() {
            TileOverrides = VaultUtils.GetSubclassInstances<TileOverride>();
            TileOverrides.RemoveAll(tile => !tile.CanLoad() || tile.TargetID < 0);
            MethodInfo method = typeof(TileLoader).GetMethod("RightClick", BindingFlags.Static | BindingFlags.Public);
            if (method != null) {
                CWRHook.Add(method, HookRightClick);
            }
        }
        void ICWRLoader.SetupData() {
            foreach (var rTile in TileOverrides) {
                TileOverrideDic.Add(rTile.TargetID, rTile);
            }
        }
        void ICWRLoader.UnLoadData() {
            TileOverrides?.Clear();
            TileOverrideDic?.Clear();
        }

        public static bool HookRightClick(OnRightClickDelegate orig, int i, int j) {
            Tile tile = Framing.GetTileSafely(i, j);

            bool? reset = null;

            if (TileOverrideDic.TryGetValue(tile.TileType, out var rTile)){
                reset = rTile.RightClick(i, j, tile);
            }

            if (reset.HasValue) {
                return reset.Value;
            }

            return orig.Invoke(i, j);
        }
    }
}
