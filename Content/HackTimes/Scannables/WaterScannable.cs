using CalamityOverhaul.Content.HackTimes.Targets;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.HackTimes.Scannables
{
    /// <summary>液体扫描</summary>
    internal class WaterScannable : IHackTarget
    {
        private readonly int tileX;
        private readonly int tileY;

        public WaterScannable(int tileX, int tileY) {
            this.tileX = tileX;
            this.tileY = tileY;
        }

        public Vector2 WorldCenter => new(tileX * 16f + 8f, tileY * 16f + 8f);

        public bool IsValid {
            get {
                if (!InWorld(tileX, tileY)) return false;
                return Main.tile[tileX, tileY].LiquidAmount > 0;
            }
        }

        public bool IsHackable => false;

        public int ScanRowCount => 7;

        public void BuildScanData(string[] labels, string[] values, Color[] colors) {
            if (!IsValid) return;
            Tile tile = Main.tile[tileX, tileY];

            labels[0] = HackTime.WaterScanLiquid.Value;
            values[0] = GetLiquidName(tile.LiquidType);
            colors[0] = GetLiquidColor(tile.LiquidType);

            labels[1] = HackTime.WaterScanEnvironment.Value;
            values[1] = GetEnvironmentText(tileX, tileY, tile.LiquidType);
            colors[1] = HackTheme.Accent;

            labels[2] = HackTime.WaterScanDepth.Value;
            values[2] = $"{tile.LiquidAmount} / 255 ({tile.LiquidAmount / 255f:P0})";
            colors[2] = tile.LiquidAmount >= 200 ? HackTheme.AccentAlt : HackTheme.TextBright;

            labels[3] = HackTime.WaterScanWorldLayer.Value;
            values[3] = GetWorldLayerText(tileY);
            colors[3] = HackTheme.TextBright;

            labels[4] = HackTime.WaterScanTileCoord.Value;
            values[4] = $"{tileX}, {tileY}";
            colors[4] = HackTheme.TextDim;

            labels[5] = HackTime.WaterScanContainment.Value;
            values[5] = GetContainmentText(tileX, tileY);
            colors[5] = HackTheme.TextBright;

            labels[6] = HackTime.TileScanStatus.Value;
            values[6] = tile.LiquidAmount >= 240 ? HackTime.WaterScanStatusStill.Value : HackTime.WaterScanStatusFlowing.Value;
            colors[6] = tile.LiquidAmount >= 240 ? HackTheme.Accent : HackTheme.Uploading;
        }

        public HackTargetType TargetType => HackTargetType.Get<WaterTargetType>();

        public Vector2 LockFrameHalfSize => new(34f, 34f);

        public string LockFrameTitle {
            get {
                if (!IsValid) return string.Empty;
                Tile tile = Main.tile[tileX, tileY];
                return $"{GetLiquidName(tile.LiquidType)} / {GetEnvironmentText(tileX, tileY, tile.LiquidType)}";
            }
        }

        public bool TryGetLockFrameStatus(out string text, out Color color) {
            text = null;
            color = default;
            if (!IsValid) return false;
            Tile tile = Main.tile[tileX, tileY];
            text = $"{tile.LiquidAmount / 255f:P0}";
            color = GetLiquidColor(tile.LiquidType);
            return true;
        }

        public bool ApplyHack(QuickHackDef hack, Player caster) => false;

        public bool TargetEquals(IHackTarget other) {
            return other is WaterScannable w && w.tileX == tileX && w.tileY == tileY;
        }

        public static bool TryGetScannableLiquid(Vector2 worldPos, out int outX, out int outY) {
            outX = (int)(worldPos.X / 16f);
            outY = (int)(worldPos.Y / 16f);

            if (!InWorld(outX, outY)) return false;
            return Main.tile[outX, outY].LiquidAmount > 0;
        }

        private static bool InWorld(int x, int y) {
            return x >= 0 && x < Main.maxTilesX && y >= 0 && y < Main.maxTilesY;
        }

        private static string GetLiquidName(int liquidType) {
            return liquidType switch {
                1 => HackTime.WaterScanLava.Value,
                2 => HackTime.WaterScanHoney.Value,
                3 => HackTime.WaterScanShimmer.Value,
                _ => HackTime.WaterScanWater.Value,
            };
        }

        private static Color GetLiquidColor(int liquidType) {
            return liquidType switch {
                1 => HackTheme.Danger,
                2 => new Color(220, 170, 60),
                3 => HackTheme.AccentAlt,
                _ => HackTheme.Accent,
            };
        }

        private static string GetWorldLayerText(int y) {
            if (y < Main.worldSurface * 0.35) return HackTime.WaterScanLayerSky.Value;
            if (y < Main.worldSurface) return HackTime.WaterScanLayerSurface.Value;
            if (y < Main.rockLayer) return HackTime.WaterScanLayerUnderground.Value;
            if (y < Main.UnderworldLayer) return HackTime.WaterScanLayerCavern.Value;
            return HackTime.WaterScanLayerUnderworld.Value;
        }

        private static string GetEnvironmentText(int x, int y, int liquidType) {
            if (IsOceanCoordinate(x, y) && liquidType == 0) return HackTime.WaterScanEnvOcean.Value;
            if (y >= Main.UnderworldLayer) return HackTime.WaterScanEnvUnderworld.Value;

            CountNearbyBiomeTiles(x, y, out int desert, out int snow, out int jungle,
                out int corruption, out int crimson, out int hallow, out int dungeon, out int mushroom);

            if (dungeon >= 12) return HackTime.WaterScanEnvDungeon.Value;
            if (jungle >= 18) return HackTime.WaterScanEnvJungle.Value;
            if (snow >= 18) return HackTime.WaterScanEnvSnow.Value;
            if (desert >= 18) return HackTime.WaterScanEnvDesert.Value;
            if (corruption >= 12) return HackTime.WaterScanEnvCorruption.Value;
            if (crimson >= 12) return HackTime.WaterScanEnvCrimson.Value;
            if (hallow >= 12) return HackTime.WaterScanEnvHallow.Value;
            if (mushroom >= 12) return HackTime.WaterScanEnvMushroom.Value;

            return GetWorldLayerText(y);
        }

        private static bool IsOceanCoordinate(int x, int y) {
            bool nearWorldEdge = x < 380 || x > Main.maxTilesX - 380;
            return nearWorldEdge && y > Main.worldSurface - 80;
        }

        private static void CountNearbyBiomeTiles(
            int centerX,
            int centerY,
            out int desert,
            out int snow,
            out int jungle,
            out int corruption,
            out int crimson,
            out int hallow,
            out int dungeon,
            out int mushroom) {
            desert = snow = jungle = corruption = crimson = hallow = dungeon = mushroom = 0;
            const int radius = 30;

            for (int x = centerX - radius; x <= centerX + radius; x++) {
                for (int y = centerY - radius; y <= centerY + radius; y++) {
                    if (!InWorld(x, y)) continue;
                    Tile tile = Main.tile[x, y];
                    if (!tile.HasTile) continue;

                    int type = tile.TileType;
                    if (Main.tileDungeon[type]) dungeon++;
                    if (Main.tileSand[type] || type == TileID.Sandstone || type == TileID.HardenedSand) desert++;
                    if (type == TileID.SnowBlock || type == TileID.IceBlock) snow++;
                    if (type == TileID.JungleGrass || type == TileID.Mud || type == TileID.JunglePlants) jungle++;
                    if (type == TileID.Ebonstone || type == TileID.CorruptGrass) corruption++;
                    if (type == TileID.Crimstone || type == TileID.CrimsonGrass) crimson++;
                    if (type == TileID.Pearlstone || type == TileID.HallowedGrass) hallow++;
                    if (type == TileID.MushroomGrass) mushroom++;
                }
            }
        }

        private static string GetContainmentText(int x, int y) {
            int solidSides = 0;
            if (IsSolidTile(x - 1, y)) solidSides++;
            if (IsSolidTile(x + 1, y)) solidSides++;
            if (IsSolidTile(x, y - 1)) solidSides++;
            if (IsSolidTile(x, y + 1)) solidSides++;

            if (solidSides >= 3) return HackTime.WaterScanContainmentPocket.Value;
            if (solidSides == 2) return HackTime.WaterScanContainmentChannel.Value;
            return HackTime.WaterScanContainmentOpen.Value;
        }

        private static bool IsSolidTile(int x, int y) {
            if (!InWorld(x, y)) return false;
            Tile tile = Main.tile[x, y];
            return tile.HasTile && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType];
        }
    }
}
