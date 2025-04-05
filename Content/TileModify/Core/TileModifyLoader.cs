using CalamityOverhaul.Common;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.TileModify.Core
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

            if (TileOverrideDic.TryGetValue(tile.TileType, out var rTile)) {
                reset = rTile.RightClick(i, j, tile);
            }

            if (reset.HasValue) {
                return reset.Value;
            }

            return orig.Invoke(i, j);
        }

        public static void KillTE(ModTileEntity te) => te.Kill(te.Position.X, te.Position.Y);

        public static void SendKillTE(ModTileEntity te) {
            if (VaultUtils.isSinglePlayer) {
                return;
            }

            ModPacket modPacket = CWRMod.Instance.GetPacket();
            modPacket.Write((byte)CWRMessageType.KillTileEntity);
            modPacket.WritePoint16(te.Position);
            modPacket.Send();
        }

        public static void HandlerNetKillTE(BinaryReader reader, int whoAmI) {
            Point16 point = reader.ReadPoint16();
            if (!TileEntity.ByPosition.TryGetValue(point, out TileEntity te)) {
                return;
            }
            KillTE((ModTileEntity)te);
            if (VaultUtils.isServer) {
                ModPacket modPacket = CWRMod.Instance.GetPacket();
                modPacket.Write((byte)CWRMessageType.KillTileEntity);
                modPacket.WritePoint16(te.Position);
                modPacket.Send(-1, whoAmI);
            }
        }
    }
}
