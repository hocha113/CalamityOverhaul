using CalamityOverhaul.Content.Scenarios.OldDuke.OldDuchests;
using InnoVault.Actors;
using InnoVault.TileProcessors;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.OldDuke.Campsites
{
    /// <summary>
    /// 营地放置/补种，仅服务端/单人；客户端靠Actor同步拿实体
    /// </summary>
    internal static class OldDukeCampsiteGenerationService
    {
        //向上搜地最大格数；搬家用更小值
        private const int UpOffsetValue = 660;
        private const int UpOffsetValueRelocation = 30;

        //补种节流，别每帧扫Actor
        private static int ensureCheckTimer;

        /// <summary>缺老公爵Actor则补种；箱子自带去重</summary>
        public static void EnsureCampsitePlaced() {
            if (!OldDukeCampsite.IsGenerated) {
                return;
            }
            if (!VaultUtils.isServer && !VaultUtils.isSinglePlayer) {
                return;
            }

            ensureCheckTimer++;
            if (ensureCheckTimer < 60) {
                return;
            }
            ensureCheckTimer = 0;

            if (ActorLoader.GetActiveActors<OldDukeWanderingActor>().Count > 0) {
                return;
            }

            PlaceCampsite(OldDukeCampsite.CampsitePosition, isRelocation: false);
        }

        /// <summary>放锅/旗杆/老公爵，搬家跳过箱子</summary>
        public static void PlaceCampsite(Vector2 campsiteCenter, bool isRelocation) {
            if (!VaultUtils.isServer && !VaultUtils.isSinglePlayer) {
                return;
            }

            int upOffsetValue = isRelocation ? UpOffsetValueRelocation : UpOffsetValue;

            PlacePots(campsiteCenter, upOffsetValue);
            PlaceFlagpoles(campsiteCenter, upOffsetValue);

            if (!isRelocation) {
                PlaceOldChest(campsiteCenter);
            }

            ActorLoader.NewActor<OldDukeWanderingActor>(campsiteCenter);
        }

        /// <summary>清营地装饰/游荡Actor</summary>
        public static void ClearCampsiteActors() {
            KillAllOf<CampsitePotActor>();
            KillAllOf<CampsiteFlagpoleActor>();
            KillAllOf<OldDukeWanderingActor>();
        }

        private static void KillAllOf<T>() where T : Actor {
            List<T> actors = ActorLoader.GetActiveActors<T>();
            foreach (T actor in actors) {
                ActorLoader.KillActor(actor.WhoAmI);
            }
        }

        private static void PlacePots(Vector2 campsiteCenter, int upOffsetValue) {
            //前方与两侧
            Vector2[] potOffsets = [
                new Vector2(220f, 40f),
                new Vector2(-240f, 35f),
                new Vector2(280f, 50f),
                new Vector2(-160f, 55f)
            ];

            foreach (Vector2 offset in potOffsets) {
                if (TryFindGround(campsiteCenter + offset, upOffsetValue, requireSolidSlope: false, out Vector2 finalPos)) {
                    ActorLoader.NewActor<CampsitePotActor>(finalPos);
                }
            }
        }

        private static void PlaceFlagpoles(Vector2 campsiteCenter, int upOffsetValue) {
            Vector2[] flagpoleOffsets = [
                new Vector2(-180f, -20f),
                new Vector2(200f, -15f)
            ];

            foreach (Vector2 offset in flagpoleOffsets) {
                if (TryFindGround(campsiteCenter + offset, upOffsetValue, requireSolidSlope: true, out Vector2 finalPos)) {
                    ActorLoader.NewActor<CampsiteFlagpoleActor>(finalPos);
                }
            }
        }

        /// <summary>向下搜最近实心地面</summary>
        private static bool TryFindGround(Vector2 searchPos, int upOffsetValue, bool requireSolidSlope, out Vector2 finalPos) {
            int tileX = (int)(searchPos.X / 16f);
            int tileY = (int)(searchPos.Y / 16f) - upOffsetValue;

            for (int y = tileY; y < tileY + upOffsetValue * 2; y++) {
                if (y < 0 || y >= Main.maxTilesY) {
                    continue;
                }

                Tile tile = Main.tile[tileX, y];
                if (tile == null || !tile.HasSolidTile()) {
                    continue;
                }
                if (requireSolidSlope && tile.Slope != SlopeType.Solid) {
                    continue;
                }

                //旗杆贴地，锅上抬16
                finalPos = requireSolidSlope
                    ? new Vector2(tileX * 16f + 8f, y * 16f)
                    : new Vector2(tileX * 16f + 8f, y * 16f - 16f);
                return true;
            }

            finalPos = default;
            return false;
        }

        /// <summary>放老箱子，区内已有则跳过</summary>
        private static void PlaceOldChest(Vector2 campsiteCenter) {
            Vector2 chestOffset = new Vector2(-320f, 20f);
            Vector2 searchPos = campsiteCenter + chestOffset;
            int baseTileX = (int)(searchPos.X / 16f);
            int baseTileY = (int)(searchPos.Y / 16f) - UpOffsetValue;

            int chestType = ModContent.TileType<OldDuchestTile>();

            for (int y = baseTileY; y < baseTileY + UpOffsetValue * 2; y++) {
                if (!WorldGen.InWorld(baseTileX, y)) {
                    continue;
                }

                Tile tile = Main.tile[baseTileX, y];
                if (tile == null || !tile.HasSolidTile()) {
                    continue;
                }

                int chestTileX = baseTileX - 2;
                int chestTileY = y - 1;

                //区内已有老箱子则跳过
                for (int cx = -12; cx < 18; cx++) {
                    for (int cy = -12; cy < 16; cy++) {
                        int checkX = chestTileX + cx;
                        int checkY = chestTileY - cy;
                        if (!WorldGen.InWorld(checkX, checkY)) {
                            continue;
                        }

                        Tile checkTile = Main.tile[checkX, checkY];
                        if (checkTile != null && checkTile.HasTile && checkTile.TileType == chestType) {
                            return;
                        }
                    }
                }

                //清6x4区域
                for (int cx = 0; cx < 6; cx++) {
                    for (int cy = 0; cy < 4; cy++) {
                        int clearX = chestTileX + cx;
                        int clearY = chestTileY - cy;
                        if (!WorldGen.InWorld(clearX, clearY)) {
                            continue;
                        }

                        Tile clearTile = Main.tile[clearX, clearY];
                        if (clearTile != null && clearTile.HasTile) {
                            WorldGen.KillTile(clearX, clearY, false, false, true);
                        }
                    }
                }

                //底座实心
                for (int bx = 0; bx < 6; bx++) {
                    int baseX = chestTileX + bx;
                    int baseY = chestTileY + 1;
                    if (!WorldGen.InWorld(baseX, baseY)) {
                        continue;
                    }

                    Tile baseTile = Main.tile[baseX, baseY];
                    baseTile.Slope = SlopeType.Solid;
                    WorldGen.PlaceTile(baseX, baseY, CWRID.Tile_SulphurousSand, true, true);
                }

                //原点在(3,3)
                int placeX = chestTileX + 3;
                int placeY = chestTileY;

                WorldGen.PlaceTile(placeX, placeY, chestType, true, false, -1, 0);

                if (TPUtils.TryGetTopLeft(placeX, placeY, out var point)) {
                    var tp = TileProcessorLoader.AddInWorld(chestType, point, null);

                    if (tp != null && TileProcessorLoader.ByPositionGetTP(point, out OldDuchestTP chestTP)) {
                        chestTP.storedItems = OldDuchestLootGenerator.GenerateDailyLoot();
                        chestTP.SendData();
                    }

                    if (Main.netMode == NetmodeID.Server) {
                        NetMessage.SendObjectPlacement(-1, placeX, placeY, chestType, 0, 0, -1, -1);
                    }
                }

                break;
            }
        }
    }
}
