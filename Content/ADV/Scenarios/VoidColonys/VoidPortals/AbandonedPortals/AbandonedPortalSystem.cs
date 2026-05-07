using CalamityOverhaul.OtherMods.SubWorld;
using InnoVault.TileProcessors;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.ADV.Scenarios.VoidColonys.VoidPortals.AbandonedPortals
{
    internal class AbandonedPortalSystem : ModSystem
    {
        private const string SavePosXKey = "AbandonedPortalPosX";
        private const string SavePosYKey = "AbandonedPortalPosY";
        private const string SaveResolvedKey = "AbandonedPortalResolved";

        private bool spawnPending;

        internal static int SavedTileX { get; private set; }
        internal static int SavedTileY { get; private set; }
        internal static bool PositionResolved { get; private set; }

        public override void OnWorldLoad() {
            spawnPending = true;
        }

        public override void OnWorldUnload() {
            spawnPending = false;
            SavedTileX = 0;
            SavedTileY = 0;
            PositionResolved = false;
            AbandonedPortalSession.Close();
        }

        public override void PostUpdateWorld() {
            AbandonedPortalSession.Update();

            if (Main.gameMenu || VoidColony.Active || VaultUtils.isClient || SubWorldRef.AnyActiveSubWorld()) {
                return;
            }

            if (!spawnPending) return;

            int tileType = ModContent.TileType<AbandonedPortalTile>();

            //位置已知且物块已存在，无需重复放置
            if (PositionResolved && SavedTileX > 0 && SavedTileY > 0) {
                Tile t = Framing.GetTileSafely(SavedTileX, SavedTileY);
                if (t.HasTile && t.TileType == tileType) {
                    spawnPending = false;
                    //确保 TP 已注册
                    if (!TileProcessorLoader.ByPositionGetTP(new Point16(SavedTileX, SavedTileY), out AbandonedPortalTP _)) {
                        TileProcessorLoader.AddInWorld(tileType, new Point16(SavedTileX, SavedTileY), null);
                        if (Main.netMode == NetmodeID.Server) {
                            TileProcessorNetWork.PlaceInWorldNetSend(VaultMod.Instance, tileType, new Point16(SavedTileX, SavedTileY));
                        }
                    }
                    return;
                }
            }

            bool firstTime = !PositionResolved;
            if (firstTime) {
                Point spawnTile = AbandonedPortalSiteFinder.Resolve();
                SavedTileX = spawnTile.X;
                SavedTileY = spawnTile.Y;
                PositionResolved = true;
            }

            if (firstTime) {
                AbandonedPortalSiteFinder.PreparePortalSite(SavedTileX, SavedTileY);
            }

            WorldGen.PlaceTile(SavedTileX, SavedTileY, tileType, true, true);
            if (TPUtils.TryGetTopLeft(SavedTileX, SavedTileY, out Point16 topLeft)) {
                TileProcessorLoader.AddInWorld(tileType, topLeft, null);
                if (Main.netMode == NetmodeID.Server) {
                    NetMessage.SendTileSquare(-1, SavedTileX, SavedTileY, AbandonedPortalTile.Width, AbandonedPortalTile.Height);
                    TileProcessorNetWork.PlaceInWorldNetSend(VaultMod.Instance, tileType, topLeft);
                }
            }

            spawnPending = false;
        }

        public override void SaveWorldData(TagCompound tag) {
            if (PositionResolved) {
                tag[SavePosXKey] = SavedTileX;
                tag[SavePosYKey] = SavedTileY;
                tag[SaveResolvedKey] = true;
            }
        }

        public override void LoadWorldData(TagCompound tag) {
            if (tag.ContainsKey(SaveResolvedKey) && tag.GetBool(SaveResolvedKey)) {
                SavedTileX = tag.GetInt(SavePosXKey);
                SavedTileY = tag.GetInt(SavePosYKey);
                PositionResolved = SavedTileX > 0 && SavedTileY > 0;
            }
        }
    }
}
