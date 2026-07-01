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
    /// 营地的放置/补种服务：新生成与"世界加载后已生成但尚无存活Actor"两条路径统一走这里，
    /// 只在服务端/单人下执行，客户端Actor通过框架自带的同步/晚加入快照自动获得
    /// </summary>
    internal static class OldDukeCampsiteGenerationService
    {
        //向上搜索地面的最大格数，鱼人钓搬家时地形通常紧邻水面，缩小搜索范围避免找到过远的地面
        private const int UpOffsetValue = 660;
        private const int UpOffsetValueRelocation = 30;

        //补种自检的节流计时器，避免GetActiveActors在每一帧都做一次列表扫描
        private static int ensureCheckTimer;

        /// <summary>
        /// 声明式补种检查：已生成但没有存活的老公爵Actor时，重新执行一次放置<br/>
        /// 放置内部对箱子有去重检测，世界重新加载时不会重复生成箱子，只会补回锅/旗杆/老公爵
        /// </summary>
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

        /// <summary>
        /// 在指定位置放置一整套营地内容：锅/旗杆/老公爵Actor + 老箱子(搬家时跳过)
        /// </summary>
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

        /// <summary>
        /// 清除所有存活的营地装饰/游荡Actor，供清除营地或搬家前调用
        /// </summary>
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
            //主要布置在老公爵前方和两侧，避免被遮挡
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

        /// <summary>
        /// 从<paramref name="searchPos"/>向下搜索最近的实心地面
        /// </summary>
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

                //旗杆贴地放置，锅稍微上抬避免嵌入地面
                finalPos = requireSolidSlope
                    ? new Vector2(tileX * 16f + 8f, y * 16f)
                    : new Vector2(tileX * 16f + 8f, y * 16f - 16f);
                return true;
            }

            finalPos = default;
            return false;
        }

        /// <summary>
        /// 放置老箱子到营地：区域内已存在老箱子时直接跳过，世界重新加载后不会重复生成
        /// </summary>
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

                //检查该区域是否已经存在老箱子，避免重复放置(世界重新加载后走这里补种)
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

                //清理箱子放置区域，箱子是6x4格
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

                //确保底座是实心的
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

                //放置老箱子(箱子的原点在3,3位置)
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
